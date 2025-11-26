// DirectorySyncService.cs - Implementation of directory synchronization
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Directory synchronization implementation
/// </summary>
public class DirectorySyncService : IDirectorySyncService
{
    private readonly ILongPathService _longPathService;
    
    public event EventHandler<SyncConflictEventArgs>? ConflictEncountered;
    
    public DirectorySyncService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
    }
    
    public async Task<IReadOnlyList<SyncFileEntry>> CompareDirectoriesAsync(
        string leftPath,
        string rightPath,
        SyncOptions options,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new ConcurrentDictionary<string, SyncFileEntry>(StringComparer.OrdinalIgnoreCase);
        
        // Scan left directory
        progress?.Report(new SyncProgressInfo { Phase = "Scanning left..." });
        await ScanDirectoryAsync(leftPath, entries, true, options, cancellationToken);
        
        // Scan right directory
        progress?.Report(new SyncProgressInfo { Phase = "Scanning right..." });
        await ScanDirectoryAsync(rightPath, entries, false, options, cancellationToken);
        
        // Compare files
        var result = new List<SyncFileEntry>();
        var allEntries = entries.Values.ToList();
        int processed = 0;
        
        foreach (var entry in allEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var status = DetermineStatus(entry, options);
            var action = SyncFileAction.Skip;
            
            result.Add(entry with { Status = status, Action = action });
            
            processed++;
            progress?.Report(new SyncProgressInfo
            {
                Phase = "Comparing...",
                CurrentFile = entry.RelativePath,
                TotalFiles = allEntries.Count,
                ProcessedFiles = processed,
                ProgressPercent = (double)processed / allEntries.Count * 100
            });
        }
        
        // Filter out equal files if not showing them
        if (!options.ShowEqualFiles)
        {
            result = result.Where(e => e.Status != SyncFileStatus.Equal).ToList();
        }
        
        return result.OrderBy(e => e.RelativePath).ToList().AsReadOnly();
    }
    
    public Task<IReadOnlyList<SyncFileEntry>> RefreshComparisonAsync(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options,
        CancellationToken cancellationToken = default)
    {
        var refreshed = new List<SyncFileEntry>();
        
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Re-read file info
            var leftInfo = !string.IsNullOrEmpty(entry.LeftFullPath) && File.Exists(entry.LeftFullPath)
                ? new FileInfo(entry.LeftFullPath)
                : null;
            var rightInfo = !string.IsNullOrEmpty(entry.RightFullPath) && File.Exists(entry.RightFullPath)
                ? new FileInfo(entry.RightFullPath)
                : null;
            
            var updated = entry with
            {
                LeftSize = leftInfo?.Length,
                LeftModified = leftInfo?.LastWriteTime,
                RightSize = rightInfo?.Length,
                RightModified = rightInfo?.LastWriteTime
            };
            
            var status = DetermineStatus(updated, options);
            refreshed.Add(updated with { Status = status });
        }
        
        return Task.FromResult<IReadOnlyList<SyncFileEntry>>(refreshed.AsReadOnly());
    }
    
    public async Task<SyncOperationResult> SynchronizeAsync(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var errors = new List<string>();
        var processed = new List<SyncFileEntry>();
        int filesCopiedLeft = 0, filesCopiedRight = 0, filesDeleted = 0, dirsCreated = 0;
        long bytesTransferred = 0;
        
        var toProcess = entries.Where(e => e.Action != SyncFileAction.Skip && e.Action != SyncFileAction.Equal).ToList();
        int total = toProcess.Count;
        int current = 0;
        
        foreach (var entry in toProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                switch (entry.Action)
                {
                    case SyncFileAction.CopyRight:
                        if (!string.IsNullOrEmpty(entry.LeftFullPath) && !string.IsNullOrEmpty(entry.RightFullPath))
                        {
                            if (entry.IsDirectory)
                            {
                                Directory.CreateDirectory(entry.RightFullPath);
                                dirsCreated++;
                            }
                            else
                            {
                                var dir = Path.GetDirectoryName(entry.RightFullPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                    dirsCreated++;
                                }
                                File.Copy(entry.LeftFullPath, entry.RightFullPath, true);
                                filesCopiedRight++;
                                bytesTransferred += entry.LeftSize ?? 0;
                            }
                        }
                        break;
                        
                    case SyncFileAction.CopyLeft:
                        if (!string.IsNullOrEmpty(entry.RightFullPath) && !string.IsNullOrEmpty(entry.LeftFullPath))
                        {
                            if (entry.IsDirectory)
                            {
                                Directory.CreateDirectory(entry.LeftFullPath);
                                dirsCreated++;
                            }
                            else
                            {
                                var dir = Path.GetDirectoryName(entry.LeftFullPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                    dirsCreated++;
                                }
                                File.Copy(entry.RightFullPath, entry.LeftFullPath, true);
                                filesCopiedLeft++;
                                bytesTransferred += entry.RightSize ?? 0;
                            }
                        }
                        break;
                        
                    case SyncFileAction.DeleteLeft:
                        if (!string.IsNullOrEmpty(entry.LeftFullPath))
                        {
                            if (entry.IsDirectory)
                                Directory.Delete(entry.LeftFullPath, true);
                            else
                                File.Delete(entry.LeftFullPath);
                            filesDeleted++;
                        }
                        break;
                        
                    case SyncFileAction.DeleteRight:
                        if (!string.IsNullOrEmpty(entry.RightFullPath))
                        {
                            if (entry.IsDirectory)
                                Directory.Delete(entry.RightFullPath, true);
                            else
                                File.Delete(entry.RightFullPath);
                            filesDeleted++;
                        }
                        break;
                }
                
                processed.Add(entry);
            }
            catch (Exception ex)
            {
                errors.Add($"{entry.RelativePath}: {ex.Message}");
            }
            
            current++;
            progress?.Report(new SyncProgressInfo
            {
                Phase = "Synchronizing...",
                CurrentFile = entry.RelativePath,
                TotalFiles = total,
                ProcessedFiles = current,
                ProcessedBytes = bytesTransferred,
                ProgressPercent = (double)current / total * 100
            });
        }
        
        return new SyncOperationResult
        {
            Success = errors.Count == 0,
            FilesCopiedLeft = filesCopiedLeft,
            FilesCopiedRight = filesCopiedRight,
            FilesDeleted = filesDeleted,
            DirectoriesCreated = dirsCreated,
            BytesTransferred = bytesTransferred,
            Duration = DateTime.Now - startTime,
            Errors = errors.AsReadOnly(),
            ProcessedFiles = processed.AsReadOnly()
        };
    }
    
    public SyncOperationResult PreviewSync(IReadOnlyList<SyncFileEntry> entries)
    {
        var toProcess = entries.Where(e => e.Action != SyncFileAction.Skip && e.Action != SyncFileAction.Equal).ToList();
        
        return new SyncOperationResult
        {
            Success = true,
            FilesCopiedLeft = toProcess.Count(e => e.Action == SyncFileAction.CopyLeft),
            FilesCopiedRight = toProcess.Count(e => e.Action == SyncFileAction.CopyRight),
            FilesDeleted = toProcess.Count(e => e.Action == SyncFileAction.DeleteLeft || e.Action == SyncFileAction.DeleteRight),
            BytesTransferred = toProcess.Sum(e => 
                e.Action == SyncFileAction.CopyLeft ? (e.RightSize ?? 0) :
                e.Action == SyncFileAction.CopyRight ? (e.LeftSize ?? 0) : 0),
            ProcessedFiles = toProcess.AsReadOnly()
        };
    }
    
    public IReadOnlyList<SyncFileEntry> AutoAssignActions(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options)
    {
        return entries.Select(e => SetActionFromOptions(e, options)).ToList().AsReadOnly();
    }
    
    public SyncFileEntry SetAction(SyncFileEntry entry, SyncFileAction action)
    {
        return entry with { Action = action };
    }
    
    public IReadOnlyList<SyncFileEntry> SetActionsForAll(
        IReadOnlyList<SyncFileEntry> entries,
        SyncFileAction action,
        Func<SyncFileEntry, bool>? filter = null)
    {
        return entries.Select(e =>
            filter == null || filter(e) ? e with { Action = action } : e
        ).ToList().AsReadOnly();
    }
    
    public IReadOnlyList<SyncFileEntry> FilterByStatus(
        IReadOnlyList<SyncFileEntry> entries,
        params SyncFileStatus[] statuses)
    {
        var statusSet = statuses.ToHashSet();
        return entries.Where(e => statusSet.Contains(e.Status)).ToList().AsReadOnly();
    }
    
    public IReadOnlyList<SyncFileEntry> FilterByAction(
        IReadOnlyList<SyncFileEntry> entries,
        params SyncFileAction[] actions)
    {
        var actionSet = actions.ToHashSet();
        return entries.Where(e => actionSet.Contains(e.Action)).ToList().AsReadOnly();
    }
    
    public SyncStatistics GetStatistics(IReadOnlyList<SyncFileEntry> entries)
    {
        return new SyncStatistics
        {
            TotalFiles = entries.Count,
            EqualFiles = entries.Count(e => e.Status == SyncFileStatus.Equal),
            LeftNewerFiles = entries.Count(e => e.Status == SyncFileStatus.LeftNewer),
            RightNewerFiles = entries.Count(e => e.Status == SyncFileStatus.RightNewer),
            LeftOnlyFiles = entries.Count(e => e.Status == SyncFileStatus.LeftOnly),
            RightOnlyFiles = entries.Count(e => e.Status == SyncFileStatus.RightOnly),
            DifferentFiles = entries.Count(e => e.Status == SyncFileStatus.Different),
            FilesToCopyLeft = entries.Count(e => e.Action == SyncFileAction.CopyLeft),
            FilesToCopyRight = entries.Count(e => e.Action == SyncFileAction.CopyRight),
            FilesToDelete = entries.Count(e => e.Action == SyncFileAction.DeleteLeft || e.Action == SyncFileAction.DeleteRight),
            BytesToCopyLeft = entries.Where(e => e.Action == SyncFileAction.CopyLeft).Sum(e => e.RightSize ?? 0),
            BytesToCopyRight = entries.Where(e => e.Action == SyncFileAction.CopyRight).Sum(e => e.LeftSize ?? 0)
        };
    }
    
    // ======= Private Methods =======
    
    private async Task ScanDirectoryAsync(
        string basePath,
        ConcurrentDictionary<string, SyncFileEntry> entries,
        bool isLeft,
        SyncOptions options,
        CancellationToken cancellationToken)
    {
        var searchOption = options.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        await Task.Run(() =>
        {
            try
            {
                var files = Directory.EnumerateFiles(basePath, "*", searchOption);
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var relativePath = Path.GetRelativePath(basePath, file);
                    
                    // Apply filters
                    if (!PassesFilters(relativePath, options))
                        continue;
                    
                    var info = new FileInfo(file);
                    
                    if (options.IgnoreHiddenFiles && info.Attributes.HasFlag(FileAttributes.Hidden))
                        continue;
                    if (options.IgnoreSystemFiles && info.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    
                    entries.AddOrUpdate(
                        relativePath,
                        _ => isLeft
                            ? new SyncFileEntry
                            {
                                RelativePath = relativePath,
                                LeftFullPath = file,
                                LeftSize = info.Length,
                                LeftModified = info.LastWriteTime
                            }
                            : new SyncFileEntry
                            {
                                RelativePath = relativePath,
                                RightFullPath = file,
                                RightSize = info.Length,
                                RightModified = info.LastWriteTime
                            },
                        (_, existing) => isLeft
                            ? existing with
                            {
                                LeftFullPath = file,
                                LeftSize = info.Length,
                                LeftModified = info.LastWriteTime
                            }
                            : existing with
                            {
                                RightFullPath = file,
                                RightSize = info.Length,
                                RightModified = info.LastWriteTime
                            }
                    );
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (DirectoryNotFoundException)
            {
                // Directory doesn't exist
            }
        }, cancellationToken);
    }
    
    private bool PassesFilters(string relativePath, SyncOptions options)
    {
        var fileName = Path.GetFileName(relativePath);
        
        // Include filter
        if (!string.IsNullOrEmpty(options.IncludeFilter))
        {
            var patterns = options.IncludeFilter.Split(';', StringSplitOptions.RemoveEmptyEntries);
            bool matches = false;
            foreach (var pattern in patterns)
            {
                if (MatchesWildcard(fileName, pattern.Trim(), options.IgnoreCase))
                {
                    matches = true;
                    break;
                }
            }
            if (!matches) return false;
        }
        
        // Exclude filter
        if (!string.IsNullOrEmpty(options.ExcludeFilter))
        {
            var patterns = options.ExcludeFilter.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in patterns)
            {
                if (MatchesWildcard(fileName, pattern.Trim(), options.IgnoreCase))
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    private bool MatchesWildcard(string input, string pattern, bool ignoreCase)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.IsMatch(input, regexPattern, options);
    }
    
    private SyncFileStatus DetermineStatus(SyncFileEntry entry, SyncOptions options)
    {
        // Only on left
        if (string.IsNullOrEmpty(entry.RightFullPath) || entry.RightSize == null)
            return SyncFileStatus.LeftOnly;
        
        // Only on right
        if (string.IsNullOrEmpty(entry.LeftFullPath) || entry.LeftSize == null)
            return SyncFileStatus.RightOnly;
        
        // Both exist - compare based on mode
        switch (options.CompareMode)
        {
            case SyncCompareMode.BySize:
                if (entry.LeftSize == entry.RightSize)
                    return SyncFileStatus.Equal;
                return entry.LeftSize > entry.RightSize ? SyncFileStatus.LeftNewer : SyncFileStatus.RightNewer;
                
            case SyncCompareMode.ByDate:
                return CompareDates(entry.LeftModified, entry.RightModified, options.DateToleranceSeconds);
                
            case SyncCompareMode.BySizeAndDate:
                if (entry.LeftSize == entry.RightSize)
                {
                    var dateStatus = CompareDates(entry.LeftModified, entry.RightModified, options.DateToleranceSeconds);
                    if (dateStatus == SyncFileStatus.Equal)
                        return SyncFileStatus.Equal;
                    return dateStatus;
                }
                return entry.LeftSize > entry.RightSize ? SyncFileStatus.LeftNewer : SyncFileStatus.RightNewer;
                
            case SyncCompareMode.ByContent:
            case SyncCompareMode.ByHash:
                // For simplicity, compare sizes first, then assume different if sizes match but need content check
                if (entry.LeftSize != entry.RightSize)
                    return SyncFileStatus.Different;
                // Content comparison would be done here
                return SyncFileStatus.Equal;
                
            default:
                return SyncFileStatus.Unknown;
        }
    }
    
    private SyncFileStatus CompareDates(DateTime? left, DateTime? right, int toleranceSeconds)
    {
        if (left == null && right == null) return SyncFileStatus.Equal;
        if (left == null) return SyncFileStatus.RightNewer;
        if (right == null) return SyncFileStatus.LeftNewer;
        
        var diff = (left.Value - right.Value).TotalSeconds;
        if (Math.Abs(diff) <= toleranceSeconds)
            return SyncFileStatus.Equal;
        
        return diff > 0 ? SyncFileStatus.LeftNewer : SyncFileStatus.RightNewer;
    }
    
    private SyncFileEntry SetActionFromOptions(SyncFileEntry entry, SyncOptions options)
    {
        if (entry.Status == SyncFileStatus.Equal)
            return entry with { Action = SyncFileAction.Equal };
        
        SyncFileAction action = entry.Status switch
        {
            SyncFileStatus.LeftOnly => options.Direction switch
            {
                SyncCopyDirection.LeftToRight or SyncCopyDirection.Both => SyncFileAction.CopyRight,
                SyncCopyDirection.RightToLeft => options.AsymmetricMode ? SyncFileAction.DeleteLeft : SyncFileAction.Skip,
                _ => SyncFileAction.Skip
            },
            SyncFileStatus.RightOnly => options.Direction switch
            {
                SyncCopyDirection.RightToLeft or SyncCopyDirection.Both => SyncFileAction.CopyLeft,
                SyncCopyDirection.LeftToRight => options.AsymmetricMode ? SyncFileAction.DeleteRight : SyncFileAction.Skip,
                _ => SyncFileAction.Skip
            },
            SyncFileStatus.LeftNewer => options.Direction switch
            {
                SyncCopyDirection.LeftToRight or SyncCopyDirection.Both => SyncFileAction.CopyRight,
                SyncCopyDirection.RightToLeft => SyncFileAction.CopyLeft,
                _ => SyncFileAction.Skip
            },
            SyncFileStatus.RightNewer => options.Direction switch
            {
                SyncCopyDirection.RightToLeft or SyncCopyDirection.Both => SyncFileAction.CopyLeft,
                SyncCopyDirection.LeftToRight => SyncFileAction.CopyRight,
                _ => SyncFileAction.Skip
            },
            SyncFileStatus.Different => SyncFileAction.Different,
            _ => SyncFileAction.Skip
        };
        
        return entry with { Action = action };
    }
}
