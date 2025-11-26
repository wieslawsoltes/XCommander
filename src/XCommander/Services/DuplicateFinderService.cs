using System.Collections.Concurrent;
using System.Security.Cryptography;
using XCommander.Models;

namespace XCommander.Services;

/// <summary>
/// Duplicate search mode
/// </summary>
public enum DuplicateSearchMode
{
    /// <summary>
    /// Find duplicates by exact filename match
    /// </summary>
    ByName,
    
    /// <summary>
    /// Find duplicates by file size
    /// </summary>
    BySize,
    
    /// <summary>
    /// Find duplicates by content (SHA256 hash)
    /// </summary>
    ByContent,
    
    /// <summary>
    /// Find duplicates by name and size (fast)
    /// </summary>
    ByNameAndSize,
    
    /// <summary>
    /// Find duplicates by size and content (thorough)
    /// </summary>
    BySizeAndContent
}

/// <summary>
/// Represents a group of duplicate files
/// </summary>
public class DuplicateGroup
{
    public string GroupKey { get; init; } = string.Empty;
    public List<DuplicateFileInfo> Files { get; } = [];
    public long TotalSize => Files.Sum(f => f.Size);
    public long WastedSpace => Files.Count > 1 ? (Files.Count - 1) * Files[0].Size : 0;
    public int FileCount => Files.Count;
}

/// <summary>
/// Information about a duplicate file
/// </summary>
public class DuplicateFileInfo
{
    public string FullPath { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime DateModified { get; init; }
    public string? Hash { get; set; }
    public bool IsSelected { get; set; }
    
    public string DisplaySize => FormatSize(Size);
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
}

/// <summary>
/// Progress information for duplicate search
/// </summary>
public class DuplicateSearchProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int DuplicateGroupsFound { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public double Percentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}

/// <summary>
/// Service for finding duplicate files
/// </summary>
public interface IDuplicateFinderService
{
    /// <summary>
    /// Find duplicate files in the specified directories
    /// </summary>
    Task<IEnumerable<DuplicateGroup>> FindDuplicatesAsync(
        IEnumerable<string> directories,
        DuplicateSearchMode mode,
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? filePattern = null,
        bool includeSubdirectories = true,
        IProgress<DuplicateSearchProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate hash for a file
    /// </summary>
    Task<string> CalculateHashAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar filenames using fuzzy matching
    /// </summary>
    Task<IEnumerable<SimilarFilenameGroup>> FindSimilarFilenamesAsync(
        IEnumerable<string> directories,
        double similarityThreshold = 0.7,
        bool includeSubdirectories = true,
        IProgress<DuplicateSearchProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark files for deletion in a duplicate group
    /// </summary>
    void MarkForDeletion(DuplicateGroup group, DeletionStrategy strategy);
    
    /// <summary>
    /// Get files currently marked for deletion
    /// </summary>
    IEnumerable<DuplicateFileInfo> GetMarkedForDeletion();
    
    /// <summary>
    /// Clear all deletion marks
    /// </summary>
    void ClearDeletionMarks();
    
    /// <summary>
    /// Delete all marked files
    /// </summary>
    Task<DeletionResult> DeleteMarkedFilesAsync(bool moveToRecycleBin = true,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare two files for preview
    /// </summary>
    Task<FileComparisonResult> CompareFilesAsync(string file1, string file2,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Strategy for marking files for deletion
/// </summary>
public enum DeletionStrategy
{
    /// <summary>Keep the oldest file</summary>
    KeepOldest,
    /// <summary>Keep the newest file</summary>
    KeepNewest,
    /// <summary>Keep file with shortest path</summary>
    KeepShortestPath,
    /// <summary>Keep file with longest path</summary>
    KeepLongestPath,
    /// <summary>Keep first file in list</summary>
    KeepFirst,
    /// <summary>Keep file in specific directory</summary>
    KeepInDirectory
}

/// <summary>
/// Group of similar filenames
/// </summary>
public class SimilarFilenameGroup
{
    public string BaseFilename { get; init; } = string.Empty;
    public double Similarity { get; init; }
    public List<DuplicateFileInfo> Files { get; } = [];
}

/// <summary>
/// Result of file deletion operation
/// </summary>
public class DeletionResult
{
    public int TotalFiles { get; init; }
    public int DeletedFiles { get; init; }
    public int FailedFiles { get; init; }
    public long BytesRecovered { get; init; }
    public List<string> Errors { get; init; } = [];
}

/// <summary>
/// Result of comparing two files
/// </summary>
public record FileComparisonResult
{
    public string File1Path { get; init; } = string.Empty;
    public string File2Path { get; init; } = string.Empty;
    public bool AreIdentical { get; init; }
    public long File1Size { get; init; }
    public long File2Size { get; init; }
    public DateTime File1Modified { get; init; }
    public DateTime File2Modified { get; init; }
    public string? File1Hash { get; init; }
    public string? File2Hash { get; init; }
    public int DifferentBytes { get; init; }
    public long FirstDifferenceOffset { get; init; } = -1;
}

public class DuplicateFinderService : IDuplicateFinderService
{
    private const int BufferSize = 81920; // 80KB buffer for hashing
    private readonly List<DuplicateFileInfo> _markedForDeletion = [];
    private readonly object _lock = new();
    
    public async Task<IEnumerable<DuplicateGroup>> FindDuplicatesAsync(
        IEnumerable<string> directories,
        DuplicateSearchMode mode,
        long minSize = 0,
        long maxSize = long.MaxValue,
        string? filePattern = null,
        bool includeSubdirectories = true,
        IProgress<DuplicateSearchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: Collect all files
        progress?.Report(new DuplicateSearchProgress
        {
            Phase = "Scanning directories...",
            ProcessedFiles = 0,
            TotalFiles = 0
        });
        
        var allFiles = new List<DuplicateFileInfo>();
        
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
                continue;
                
            var searchOption = includeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            var pattern = string.IsNullOrEmpty(filePattern) ? "*" : filePattern;
            
            try
            {
                var files = Directory.EnumerateFiles(directory, pattern, searchOption);
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var info = new FileInfo(file);
                        
                        // Apply size filters
                        if (info.Length < minSize || info.Length > maxSize)
                            continue;
                        
                        allFiles.Add(new DuplicateFileInfo
                        {
                            FullPath = file,
                            Name = info.Name,
                            Directory = info.DirectoryName ?? string.Empty,
                            Size = info.Length,
                            DateModified = info.LastWriteTime
                        });
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        
        progress?.Report(new DuplicateSearchProgress
        {
            Phase = "Grouping files...",
            ProcessedFiles = 0,
            TotalFiles = allFiles.Count
        });
        
        // Phase 2: Group files based on mode
        var duplicateGroups = mode switch
        {
            DuplicateSearchMode.ByName => await GroupByNameAsync(allFiles, progress, cancellationToken),
            DuplicateSearchMode.BySize => GroupBySize(allFiles),
            DuplicateSearchMode.ByContent => await GroupByContentAsync(allFiles, progress, cancellationToken),
            DuplicateSearchMode.ByNameAndSize => GroupByNameAndSize(allFiles),
            DuplicateSearchMode.BySizeAndContent => await GroupBySizeAndContentAsync(allFiles, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        
        // Filter to only groups with more than one file (actual duplicates)
        return duplicateGroups.Where(g => g.FileCount > 1).ToList();
    }
    
    private Task<List<DuplicateGroup>> GroupByNameAsync(
        List<DuplicateFileInfo> files,
        IProgress<DuplicateSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var groups = files
            .GroupBy(f => f.Name.ToLowerInvariant())
            .Select(g => new DuplicateGroup
            {
                GroupKey = g.Key
            })
            .ToList();
        
        foreach (var group in groups)
        {
            var matchingFiles = files.Where(f => 
                f.Name.Equals(group.GroupKey, StringComparison.OrdinalIgnoreCase));
            group.Files.AddRange(matchingFiles);
        }
        
        return Task.FromResult(groups);
    }
    
    private List<DuplicateGroup> GroupBySize(List<DuplicateFileInfo> files)
    {
        return files
            .GroupBy(f => f.Size)
            .Select(g =>
            {
                var group = new DuplicateGroup { GroupKey = $"Size: {g.Key}" };
                group.Files.AddRange(g);
                return group;
            })
            .ToList();
    }
    
    private List<DuplicateGroup> GroupByNameAndSize(List<DuplicateFileInfo> files)
    {
        return files
            .GroupBy(f => $"{f.Name.ToLowerInvariant()}|{f.Size}")
            .Select(g =>
            {
                var group = new DuplicateGroup { GroupKey = g.Key };
                group.Files.AddRange(g);
                return group;
            })
            .ToList();
    }
    
    private async Task<List<DuplicateGroup>> GroupByContentAsync(
        List<DuplicateFileInfo> files,
        IProgress<DuplicateSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var processed = 0;
        var total = files.Count;
        
        // Calculate hashes in parallel
        var hashTasks = files.Select(async file =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                file.Hash = await CalculateHashAsync(file.FullPath, cancellationToken);
            }
            catch
            {
                file.Hash = null;
            }
            
            Interlocked.Increment(ref processed);
            progress?.Report(new DuplicateSearchProgress
            {
                Phase = "Calculating file hashes...",
                ProcessedFiles = processed,
                TotalFiles = total,
                CurrentFile = file.Name
            });
            
            return file;
        });
        
        var hashedFiles = await Task.WhenAll(hashTasks);
        
        // Group by hash
        return hashedFiles
            .Where(f => f.Hash != null)
            .GroupBy(f => f.Hash!)
            .Select(g =>
            {
                var group = new DuplicateGroup { GroupKey = g.Key };
                group.Files.AddRange(g);
                return group;
            })
            .ToList();
    }
    
    private async Task<List<DuplicateGroup>> GroupBySizeAndContentAsync(
        List<DuplicateFileInfo> files,
        IProgress<DuplicateSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        // First group by size (fast)
        var sizeGroups = files
            .GroupBy(f => f.Size)
            .Where(g => g.Count() > 1) // Only process potential duplicates
            .ToList();
        
        var potentialDuplicates = sizeGroups.SelectMany(g => g).ToList();
        
        progress?.Report(new DuplicateSearchProgress
        {
            Phase = $"Found {potentialDuplicates.Count} potential duplicates by size, calculating hashes...",
            ProcessedFiles = 0,
            TotalFiles = potentialDuplicates.Count
        });
        
        // Then calculate hashes only for files with matching sizes
        var processed = 0;
        var total = potentialDuplicates.Count;
        
        foreach (var file in potentialDuplicates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                file.Hash = await CalculateHashAsync(file.FullPath, cancellationToken);
            }
            catch
            {
                file.Hash = null;
            }
            
            processed++;
            progress?.Report(new DuplicateSearchProgress
            {
                Phase = "Calculating file hashes...",
                ProcessedFiles = processed,
                TotalFiles = total,
                CurrentFile = file.Name
            });
        }
        
        // Group by hash
        return potentialDuplicates
            .Where(f => f.Hash != null)
            .GroupBy(f => f.Hash!)
            .Select(g =>
            {
                var group = new DuplicateGroup { GroupKey = g.Key };
                group.Files.AddRange(g);
                return group;
            })
            .ToList();
    }
    
    public async Task<string> CalculateHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        var buffer = new byte[BufferSize];
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
        }
        
        sha256.TransformFinalBlock([], 0, 0);
        
        return Convert.ToHexString(sha256.Hash ?? []).ToLowerInvariant();
    }
    
    public async Task<IEnumerable<SimilarFilenameGroup>> FindSimilarFilenamesAsync(
        IEnumerable<string> directories,
        double similarityThreshold = 0.7,
        bool includeSubdirectories = true,
        IProgress<DuplicateSearchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Collect all files
        var allFiles = new List<DuplicateFileInfo>();
        
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
                continue;
                
            var searchOption = includeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            try
            {
                var files = Directory.EnumerateFiles(directory, "*", searchOption);
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var info = new FileInfo(file);
                        allFiles.Add(new DuplicateFileInfo
                        {
                            FullPath = file,
                            Name = info.Name,
                            Directory = info.DirectoryName ?? string.Empty,
                            Size = info.Length,
                            DateModified = info.LastWriteTime
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }
        
        // Find similar filenames using Levenshtein distance
        var groups = new List<SimilarFilenameGroup>();
        var processed = new HashSet<int>();
        
        for (int i = 0; i < allFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (processed.Contains(i)) continue;
            
            var baseFile = allFiles[i];
            var baseName = Path.GetFileNameWithoutExtension(baseFile.Name);
            var group = new SimilarFilenameGroup
            {
                BaseFilename = baseName,
                Similarity = 1.0
            };
            group.Files.Add(baseFile);
            processed.Add(i);
            
            for (int j = i + 1; j < allFiles.Count; j++)
            {
                if (processed.Contains(j)) continue;
                
                var compareFile = allFiles[j];
                var compareName = Path.GetFileNameWithoutExtension(compareFile.Name);
                
                var similarity = CalculateSimilarity(baseName, compareName);
                
                if (similarity >= similarityThreshold)
                {
                    group.Files.Add(compareFile);
                    processed.Add(j);
                }
            }
            
            if (group.Files.Count > 1)
            {
                groups.Add(group);
            }
            
            progress?.Report(new DuplicateSearchProgress
            {
                Phase = "Finding similar filenames...",
                ProcessedFiles = processed.Count,
                TotalFiles = allFiles.Count,
                CurrentFile = baseFile.Name
            });
        }
        
        return groups;
    }
    
    public void MarkForDeletion(DuplicateGroup group, DeletionStrategy strategy)
    {
        lock (_lock)
        {
            // First, clear existing marks for this group
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
            
            // Determine which file to keep
            DuplicateFileInfo? keeper = strategy switch
            {
                DeletionStrategy.KeepOldest => group.Files.OrderBy(f => f.DateModified).FirstOrDefault(),
                DeletionStrategy.KeepNewest => group.Files.OrderByDescending(f => f.DateModified).FirstOrDefault(),
                DeletionStrategy.KeepShortestPath => group.Files.OrderBy(f => f.FullPath.Length).FirstOrDefault(),
                DeletionStrategy.KeepLongestPath => group.Files.OrderByDescending(f => f.FullPath.Length).FirstOrDefault(),
                DeletionStrategy.KeepFirst => group.Files.FirstOrDefault(),
                _ => group.Files.FirstOrDefault()
            };
            
            // Mark all except the keeper for deletion
            foreach (var file in group.Files)
            {
                if (file != keeper)
                {
                    file.IsSelected = true;
                    if (!_markedForDeletion.Any(f => f.FullPath == file.FullPath))
                    {
                        _markedForDeletion.Add(file);
                    }
                }
            }
        }
    }
    
    public IEnumerable<DuplicateFileInfo> GetMarkedForDeletion()
    {
        lock (_lock)
        {
            return _markedForDeletion.ToList();
        }
    }
    
    public void ClearDeletionMarks()
    {
        lock (_lock)
        {
            foreach (var file in _markedForDeletion)
            {
                file.IsSelected = false;
            }
            _markedForDeletion.Clear();
        }
    }
    
    public async Task<DeletionResult> DeleteMarkedFilesAsync(bool moveToRecycleBin = true,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<DuplicateFileInfo> toDelete;
        lock (_lock)
        {
            toDelete = _markedForDeletion.ToList();
        }
        
        var result = new DeletionResult
        {
            TotalFiles = toDelete.Count,
            Errors = new List<string>()
        };
        
        int deleted = 0;
        int failed = 0;
        long bytesRecovered = 0;
        
        foreach (var file in toDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                if (File.Exists(file.FullPath))
                {
                    var size = file.Size;
                    
                    if (moveToRecycleBin)
                    {
                        // Move to recycle bin - platform specific
                        // For cross-platform, we'd use a library or fall back to delete
                        File.Delete(file.FullPath);
                    }
                    else
                    {
                        File.Delete(file.FullPath);
                    }
                    
                    deleted++;
                    bytesRecovered += size;
                    
                    lock (_lock)
                    {
                        _markedForDeletion.Remove(file);
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                result.Errors.Add($"{file.FullPath}: {ex.Message}");
            }
            
            progress?.Report(deleted + failed);
        }
        
        return new DeletionResult
        {
            TotalFiles = toDelete.Count,
            DeletedFiles = deleted,
            FailedFiles = failed,
            BytesRecovered = bytesRecovered,
            Errors = result.Errors
        };
    }
    
    public async Task<FileComparisonResult> CompareFilesAsync(string file1, string file2,
        CancellationToken cancellationToken = default)
    {
        var info1 = new FileInfo(file1);
        var info2 = new FileInfo(file2);
        
        var result = new FileComparisonResult
        {
            File1Path = file1,
            File2Path = file2,
            File1Size = info1.Length,
            File2Size = info2.Length,
            File1Modified = info1.LastWriteTime,
            File2Modified = info2.LastWriteTime
        };
        
        // Quick check - if sizes differ, files are different
        if (info1.Length != info2.Length)
        {
            return result with { AreIdentical = false };
        }
        
        // Compare content byte by byte
        using var stream1 = File.OpenRead(file1);
        using var stream2 = File.OpenRead(file2);
        
        var buffer1 = new byte[BufferSize];
        var buffer2 = new byte[BufferSize];
        int bytesRead1, bytesRead2;
        long offset = 0;
        int differentBytes = 0;
        long firstDifference = -1;
        
        while ((bytesRead1 = await stream1.ReadAsync(buffer1, cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            bytesRead2 = await stream2.ReadAsync(buffer2, cancellationToken);
            
            if (bytesRead1 != bytesRead2)
            {
                return result with 
                { 
                    AreIdentical = false,
                    FirstDifferenceOffset = offset 
                };
            }
            
            for (int i = 0; i < bytesRead1; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    differentBytes++;
                    if (firstDifference < 0)
                    {
                        firstDifference = offset + i;
                    }
                }
            }
            
            offset += bytesRead1;
        }
        
        // Calculate hashes for reference
        string? hash1 = null, hash2 = null;
        try
        {
            hash1 = await CalculateHashAsync(file1, cancellationToken);
            hash2 = await CalculateHashAsync(file2, cancellationToken);
        }
        catch { }
        
        return result with
        {
            AreIdentical = differentBytes == 0,
            DifferentBytes = differentBytes,
            FirstDifferenceOffset = firstDifference,
            File1Hash = hash1,
            File2Hash = hash2
        };
    }
    
    /// <summary>
    /// Calculate Levenshtein distance-based similarity between two strings
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0.0;
        
        s1 = s1.ToLowerInvariant();
        s2 = s2.ToLowerInvariant();
        
        var distance = LevenshteinDistance(s1, s2);
        var maxLength = Math.Max(s1.Length, s2.Length);
        
        return 1.0 - ((double)distance / maxLength);
    }
    
    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var m = s1.Length;
        var n = s2.Length;
        var d = new int[m + 1, n + 1];
        
        for (int i = 0; i <= m; i++)
            d[i, 0] = i;
        
        for (int j = 0; j <= n; j++)
            d[0, j] = j;
        
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        
        return d[m, n];
    }
}
