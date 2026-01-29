using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace XCommander.Services;

/// <summary>
/// Implementation of IArchiveSyncService for TC-style archive synchronization.
/// </summary>
public class ArchiveSyncService : IArchiveSyncService
{
    private static readonly HashSet<string> ModifiableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip"
    };
    
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz"
    };
    
    public event EventHandler<ArchiveSyncResult>? SyncCompleted;

    public async Task<ArchiveSyncCompareResult> CompareAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ArchiveSyncOptions();
        
        var items = new List<ArchiveSyncItem>();
        var archiveEntries = new Dictionary<string, (long Size, DateTime Modified)>(StringComparer.OrdinalIgnoreCase);
        var directoryFiles = new Dictionary<string, (long Size, DateTime Modified)>(StringComparer.OrdinalIgnoreCase);
        
        // Read archive contents
        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var key = entry.Key?.Replace('\\', '/').TrimStart('/') ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && MatchesFilter(key, options))
                {
                    archiveEntries[key] = (entry.Size, entry.LastModifiedTime ?? DateTime.MinValue);
                }
            }
        }, cancellationToken);
        
        // Read directory contents
        await Task.Run(() =>
        {
            if (!Directory.Exists(directoryPath))
                return;
                
            var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var relativePath = Path.GetRelativePath(directoryPath, file).Replace('\\', '/');
                
                if (MatchesFilter(relativePath, options))
                {
                    var info = new FileInfo(file);
                    directoryFiles[relativePath] = (info.Length, info.LastWriteTime);
                }
            }
        }, cancellationToken);
        
        // Compare and build sync items
        var allPaths = archiveEntries.Keys.Union(directoryFiles.Keys).ToList();
        var totalCount = allPaths.Count;
        var processedCount = 0;
        
        long totalSourceSize = 0;
        long totalTargetSize = 0;
        int sourceOnlyCount = 0;
        int targetOnlyCount = 0;
        int differentCount = 0;
        int identicalCount = 0;
        
        foreach (var path in allPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var inSource = directoryFiles.TryGetValue(path, out var sourceInfo);
            var inTarget = archiveEntries.TryGetValue(path, out var targetInfo);
            
            ArchiveSyncAction suggestedAction;
            
            if (inSource && inTarget)
            {
                // File exists in both
                bool identical = IsIdentical(sourceInfo, targetInfo, options);
                
                if (identical)
                {
                    suggestedAction = ArchiveSyncAction.None;
                    identicalCount++;
                }
                else
                {
                    // Determine which is newer
                    if (sourceInfo.Modified > targetInfo.Modified)
                    {
                        suggestedAction = ArchiveSyncAction.UpdateArchive;
                    }
                    else
                    {
                        suggestedAction = options.Bidirectional 
                            ? ArchiveSyncAction.UpdateDirectory 
                            : ArchiveSyncAction.None;
                    }
                    differentCount++;
                }
                
                totalSourceSize += sourceInfo.Size;
                totalTargetSize += targetInfo.Size;
            }
            else if (inSource)
            {
                // Only in directory
                suggestedAction = ArchiveSyncAction.AddToArchive;
                sourceOnlyCount++;
                totalSourceSize += sourceInfo.Size;
            }
            else
            {
                // Only in archive
                suggestedAction = options.Bidirectional 
                    ? ArchiveSyncAction.ExtractFromArchive 
                    : (options.DeleteOrphansFromArchive ? ArchiveSyncAction.DeleteFromArchive : ArchiveSyncAction.None);
                targetOnlyCount++;
                totalTargetSize += targetInfo.Size;
            }
            
            items.Add(new ArchiveSyncItem
            {
                RelativePath = path,
                ExistsInSource = inSource,
                ExistsInTarget = inTarget,
                SourceSize = inSource ? sourceInfo.Size : null,
                TargetSize = inTarget ? targetInfo.Size : null,
                SourceModified = inSource ? sourceInfo.Modified : null,
                TargetModified = inTarget ? targetInfo.Modified : null,
                SuggestedAction = suggestedAction,
                SelectedAction = suggestedAction,
                IsDirectory = false
            });
            
            processedCount++;
            progress?.Report(new ArchiveSyncProgress
            {
                CurrentFile = path,
                TotalFiles = totalCount,
                ProcessedFiles = processedCount
            });
        }
        
        return new ArchiveSyncCompareResult
        {
            Items = items,
            SourceOnlyCount = sourceOnlyCount,
            TargetOnlyCount = targetOnlyCount,
            DifferentCount = differentCount,
            IdenticalCount = identicalCount,
            TotalSourceSize = totalSourceSize,
            TotalTargetSize = totalTargetSize
        };
    }

    private static bool MatchesFilter(string path, ArchiveSyncOptions options)
    {
        var fileName = Path.GetFileName(path);
        
        // Check include filter
        if (!string.IsNullOrEmpty(options.FileFilter))
        {
            var patterns = options.FileFilter.Split(';', StringSplitOptions.RemoveEmptyEntries);
            bool matches = false;
            
            foreach (var pattern in patterns)
            {
                if (WildcardMatch(fileName, pattern.Trim()))
                {
                    matches = true;
                    break;
                }
            }
            
            if (!matches)
                return false;
        }
        
        // Check exclude filter
        if (!string.IsNullOrEmpty(options.ExcludeFilter))
        {
            var patterns = options.ExcludeFilter.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var pattern in patterns)
            {
                if (WildcardMatch(fileName, pattern.Trim()))
                {
                    return false;
                }
            }
        }
        
        return true;
    }

    private static bool WildcardMatch(string text, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
            
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsIdentical((long Size, DateTime Modified) source, (long Size, DateTime Modified) target, ArchiveSyncOptions options)
    {
        // Size must match
        if (source.Size != target.Size)
            return false;
            
        // If ignoring timestamp, only compare size
        if (options.IgnoreTimestamp)
            return true;
            
        // Compare timestamps with tolerance
        var diff = Math.Abs((source.Modified - target.Modified).TotalSeconds);
        return diff <= options.TimeTolerance;
    }

    public async Task<ArchiveSyncResult> SynchronizeAsync(
        string directoryPath,
        string archivePath,
        IReadOnlyList<ArchiveSyncItem> items,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ArchiveSyncOptions();
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        
        int filesAdded = 0;
        int filesUpdated = 0;
        int filesDeleted = 0;
        int filesExtracted = 0;
        long bytesProcessed = 0;
        
        var itemsToProcess = items.Where(i => i.SelectedAction != ArchiveSyncAction.None && i.SelectedAction != ArchiveSyncAction.Skip).ToList();
        var totalCount = itemsToProcess.Count;
        var processedCount = 0;
        
        try
        {
            // Group by action type for efficiency
            var toAdd = itemsToProcess.Where(i => i.SelectedAction == ArchiveSyncAction.AddToArchive).ToList();
            var toUpdate = itemsToProcess.Where(i => i.SelectedAction == ArchiveSyncAction.UpdateArchive).ToList();
            var toDelete = itemsToProcess.Where(i => i.SelectedAction == ArchiveSyncAction.DeleteFromArchive).ToList();
            var toExtract = itemsToProcess.Where(i => 
                i.SelectedAction == ArchiveSyncAction.ExtractFromArchive || 
                i.SelectedAction == ArchiveSyncAction.UpdateDirectory).ToList();
            
            // Process archive modifications (add, update, delete)
            if ((toAdd.Count > 0 || toUpdate.Count > 0 || toDelete.Count > 0) && CanModifyArchive(archivePath))
            {
                await Task.Run(() =>
                {
                    using var archive = (ZipArchive)ZipArchive.OpenArchive(archivePath, new ReaderOptions());
                    
                    // Delete first
                    foreach (var item in toDelete)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var entry = archive.Entries.FirstOrDefault(e => 
                            e.Key?.Replace('\\', '/').TrimStart('/').Equals(item.RelativePath, StringComparison.OrdinalIgnoreCase) == true);
                            
                        if (entry != null)
                        {
                            archive.RemoveEntry(entry);
                            filesDeleted++;
                        }
                        
                        ReportProgress(progress, item.RelativePath, ArchiveSyncAction.DeleteFromArchive, ++processedCount, totalCount);
                    }
                    
                    // Add and update
                    foreach (var item in toAdd.Concat(toUpdate))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var sourcePath = Path.Combine(directoryPath, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        
                        if (File.Exists(sourcePath))
                        {
                            // Remove existing entry if updating
                            if (item.SelectedAction == ArchiveSyncAction.UpdateArchive)
                            {
                                var existing = archive.Entries.FirstOrDefault(e => 
                                    e.Key?.Replace('\\', '/').TrimStart('/').Equals(item.RelativePath, StringComparison.OrdinalIgnoreCase) == true);
                                    
                                if (existing != null)
                                {
                                    archive.RemoveEntry(existing);
                                }
                            }
                            
                            archive.AddEntry(item.RelativePath, sourcePath);
                            bytesProcessed += item.SourceSize ?? 0;
                            
                            if (item.SelectedAction == ArchiveSyncAction.AddToArchive)
                                filesAdded++;
                            else
                                filesUpdated++;
                        }
                        
                        ReportProgress(progress, item.RelativePath, item.SelectedAction, ++processedCount, totalCount);
                    }
                    
                    // Save changes
                    var tempPath = archivePath + ".tmp";
                    using (var tempStream = File.Create(tempPath))
                    {
                        archive.SaveTo(tempStream, new WriterOptions(CompressionType.Deflate));
                    }
                    
                }, cancellationToken);
                
                // Replace original with updated archive
                if (File.Exists(archivePath + ".tmp"))
                {
                    File.Delete(archivePath);
                    File.Move(archivePath + ".tmp", archivePath);
                }
            }
            
            // Extract to directory
            if (toExtract.Count > 0)
            {
                await Task.Run(() =>
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
                    
                    foreach (var item in toExtract)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var entry = archive.Entries.FirstOrDefault(e => 
                            e.Key?.Replace('\\', '/').TrimStart('/').Equals(item.RelativePath, StringComparison.OrdinalIgnoreCase) == true);
                            
                        if (entry != null)
                        {
                            var destPath = Path.Combine(directoryPath, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                            var destDir = Path.GetDirectoryName(destPath);
                            
                            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            
                            entry.WriteToFile(destPath, new ExtractionOptions
                            {
                                ExtractFullPath = false,
                                Overwrite = true
                            });
                            
                            bytesProcessed += entry.Size;
                            filesExtracted++;
                        }
                        
                        ReportProgress(progress, item.RelativePath, item.SelectedAction, ++processedCount, totalCount);
                    }
                    
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }
        finally
        {
            // Clean up temp file if it exists
            if (File.Exists(archivePath + ".tmp"))
            {
                try { File.Delete(archivePath + ".tmp"); } catch { }
            }
        }
        
        sw.Stop();
        
        var result = new ArchiveSyncResult
        {
            Success = errors.Count == 0,
            FilesAdded = filesAdded,
            FilesUpdated = filesUpdated,
            FilesDeleted = filesDeleted,
            FilesExtracted = filesExtracted,
            BytesProcessed = bytesProcessed,
            Duration = sw.Elapsed,
            Errors = errors
        };
        
        SyncCompleted?.Invoke(this, result);
        return result;
    }

    private static void ReportProgress(IProgress<ArchiveSyncProgress>? progress, string file, ArchiveSyncAction action, int processed, int total)
    {
        progress?.Report(new ArchiveSyncProgress
        {
            CurrentFile = file,
            CurrentAction = action,
            ProcessedFiles = processed,
            TotalFiles = total
        });
    }

    public async Task<ArchiveSyncResult> QuickSyncAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var compareResult = await CompareAsync(directoryPath, archivePath, options, progress, cancellationToken);
        return await SynchronizeAsync(directoryPath, archivePath, compareResult.Items, options, progress, cancellationToken);
    }

    public async Task<ArchiveSyncResult> UpdateArchiveAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ArchiveSyncOptions();
        options = options with { Bidirectional = false, DeleteOrphansFromArchive = false };
        
        var compareResult = await CompareAsync(directoryPath, archivePath, options, progress, cancellationToken);
        
        // Only keep add and update actions
        var itemsToSync = compareResult.Items
            .Select(i => i.SuggestedAction is ArchiveSyncAction.AddToArchive or ArchiveSyncAction.UpdateArchive
                ? i
                : i with { SelectedAction = ArchiveSyncAction.None })
            .ToList();
            
        return await SynchronizeAsync(directoryPath, archivePath, itemsToSync, options, progress, cancellationToken);
    }

    public async Task<ArchiveSyncResult> RestoreFromArchiveAsync(
        string archivePath,
        string directoryPath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ArchiveSyncOptions();
        options = options with { Bidirectional = true, DeleteOrphansFromDirectory = false };
        
        var compareResult = await CompareAsync(directoryPath, archivePath, options, progress, cancellationToken);
        
        // Only keep extract actions
        var itemsToSync = compareResult.Items
            .Select(i => i.SuggestedAction is ArchiveSyncAction.ExtractFromArchive or ArchiveSyncAction.UpdateDirectory
                ? i
                : i with { SelectedAction = ArchiveSyncAction.None })
            .ToList();
            
        return await SynchronizeAsync(directoryPath, archivePath, itemsToSync, options, progress, cancellationToken);
    }

    public bool CanModifyArchive(string archivePath)
    {
        var ext = Path.GetExtension(archivePath);
        return ModifiableExtensions.Contains(ext);
    }

    public IReadOnlyList<string> GetSupportedExtensions()
    {
        return SupportedExtensions.ToList();
    }

    public async Task<bool> VerifyArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
                
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Try to read entry to verify it
                    using var stream = entry.OpenEntryStream();
                    var buffer = new byte[8192];
                    while (stream.Read(buffer, 0, buffer.Length) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
}
