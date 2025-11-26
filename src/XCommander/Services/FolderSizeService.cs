using System.Collections.Concurrent;

namespace XCommander.Services;

/// <summary>
/// Information about a folder's size
/// </summary>
public class FolderSizeInfo
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public int FolderCount { get; set; }
    public DateTime CalculatedAt { get; init; } = DateTime.Now;
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
    
    public string DisplaySize => FormatSize(TotalSize);
    
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
/// Progress information for folder size calculation
/// </summary>
public class FolderSizeProgress
{
    public string CurrentPath { get; init; } = string.Empty;
    public long CurrentSize { get; init; }
    public int FilesProcessed { get; init; }
    public int FoldersProcessed { get; init; }
}

/// <summary>
/// Service for calculating folder sizes with caching
/// </summary>
public interface IFolderSizeService
{
    /// <summary>
    /// Calculate the size of a single folder
    /// </summary>
    Task<FolderSizeInfo> CalculateFolderSizeAsync(
        string path, 
        bool includeSubfolders = true,
        IProgress<FolderSizeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate sizes for multiple folders
    /// </summary>
    Task<IEnumerable<FolderSizeInfo>> CalculateFolderSizesAsync(
        IEnumerable<string> paths,
        bool includeSubfolders = true,
        IProgress<FolderSizeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get cached folder size if available
    /// </summary>
    FolderSizeInfo? GetCachedSize(string path);
    
    /// <summary>
    /// Check if folder size is cached and still valid
    /// </summary>
    bool IsCached(string path, TimeSpan? maxAge = null);
    
    /// <summary>
    /// Clear the size cache for a specific folder
    /// </summary>
    void InvalidateCache(string path);
    
    /// <summary>
    /// Clear all cached folder sizes
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Start background calculation for folders in a directory
    /// </summary>
    Task StartBackgroundCalculationAsync(
        string parentDirectory,
        IProgress<(string Path, FolderSizeInfo Info)>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when a folder size calculation completes
    /// </summary>
    event EventHandler<FolderSizeInfo>? FolderSizeCalculated;
}

public class FolderSizeService : IFolderSizeService
{
    private readonly ConcurrentDictionary<string, FolderSizeInfo> _cache = new();
    private readonly TimeSpan _defaultCacheExpiry = TimeSpan.FromMinutes(5);
    
    public event EventHandler<FolderSizeInfo>? FolderSizeCalculated;
    
    public async Task<FolderSizeInfo> CalculateFolderSizeAsync(
        string path, 
        bool includeSubfolders = true,
        IProgress<FolderSizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var info = new FolderSizeInfo
        {
            Path = path,
            Name = Path.GetFileName(path) ?? path
        };
        
        if (!Directory.Exists(path))
        {
            info.Error = "Directory not found";
            return info;
        }
        
        try
        {
            await Task.Run(() => CalculateSizeRecursive(path, info, includeSubfolders, progress, cancellationToken), cancellationToken);
            info.IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            info.Error = "Calculation cancelled";
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        
        // Cache the result
        _cache[path] = info;
        FolderSizeCalculated?.Invoke(this, info);
        
        return info;
    }
    
    private void CalculateSizeRecursive(
        string path, 
        FolderSizeInfo info, 
        bool includeSubfolders,
        IProgress<FolderSizeProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        try
        {
            // Add files in current directory
            foreach (var file in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var fileInfo = new FileInfo(file);
                    info.TotalSize += fileInfo.Length;
                    info.FileCount++;
                    
                    progress?.Report(new FolderSizeProgress
                    {
                        CurrentPath = file,
                        CurrentSize = info.TotalSize,
                        FilesProcessed = info.FileCount,
                        FoldersProcessed = info.FolderCount
                    });
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            
            // Process subdirectories
            if (includeSubfolders)
            {
                foreach (var subdir in Directory.EnumerateDirectories(path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        info.FolderCount++;
                        CalculateSizeRecursive(subdir, info, true, progress, cancellationToken);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
    
    public async Task<IEnumerable<FolderSizeInfo>> CalculateFolderSizesAsync(
        IEnumerable<string> paths,
        bool includeSubfolders = true,
        IProgress<FolderSizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = paths.Select(p => CalculateFolderSizeAsync(p, includeSubfolders, progress, cancellationToken));
        return await Task.WhenAll(tasks);
    }
    
    public FolderSizeInfo? GetCachedSize(string path)
    {
        return _cache.TryGetValue(path, out var info) ? info : null;
    }
    
    public bool IsCached(string path, TimeSpan? maxAge = null)
    {
        if (!_cache.TryGetValue(path, out var info))
            return false;
        
        var age = maxAge ?? _defaultCacheExpiry;
        return DateTime.Now - info.CalculatedAt < age;
    }
    
    public void InvalidateCache(string path)
    {
        _cache.TryRemove(path, out _);
        
        // Also invalidate parent folders as their sizes are now incorrect
        var parent = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(parent))
        {
            _cache.TryRemove(parent, out _);
            parent = Path.GetDirectoryName(parent);
        }
    }
    
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    public async Task StartBackgroundCalculationAsync(
        string parentDirectory,
        IProgress<(string Path, FolderSizeInfo Info)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(parentDirectory))
            return;
        
        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(parentDirectory);
        }
        catch
        {
            return;
        }
        
        foreach (var subdir in subdirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Skip if already cached and fresh
            if (IsCached(subdir))
            {
                var cached = GetCachedSize(subdir);
                if (cached != null)
                {
                    progress?.Report((subdir, cached));
                    continue;
                }
            }
            
            var info = await CalculateFolderSizeAsync(subdir, true, null, cancellationToken);
            progress?.Report((subdir, info));
        }
    }
}
