using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Provides optimized icon caching with folder-specific optimizations.
/// </summary>
public class OptimizedIconCache
{
    private readonly ConcurrentDictionary<string, IconCacheEntry> _cache = new();
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30);
    private readonly int _maxCacheSize = 1000;
    private DateTime _lastCleanup = DateTime.UtcNow;
    
    /// <summary>
    /// Get an icon from cache or create it using the factory.
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // Check cache
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            entry.Touch();
            return entry.Value as T;
        }
        
        // Create new entry
        var value = await factory(cancellationToken);
        if (value != null)
        {
            var newEntry = new IconCacheEntry(value, expiry ?? _defaultExpiry);
            _cache.AddOrUpdate(key, newEntry, (_, _) => newEntry);
            
            // Periodic cleanup
            await TryCleanupAsync();
        }
        
        return value;
    }
    
    /// <summary>
    /// Invalidate a specific cache entry.
    /// </summary>
    public void Invalidate(string key)
    {
        _cache.TryRemove(key, out _);
    }
    
    /// <summary>
    /// Invalidate all entries matching a prefix.
    /// </summary>
    public void InvalidatePrefix(string prefix)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _cache.TryRemove(key, out _);
            }
        }
    }
    
    /// <summary>
    /// Clear all cached entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
    
    private async Task TryCleanupAsync()
    {
        // Only cleanup every 5 minutes
        if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5)
            return;
        
        if (!await _cleanupLock.WaitAsync(0))
            return;
        
        try
        {
            _lastCleanup = DateTime.UtcNow;
            
            // Remove expired entries
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    _cache.TryRemove(kvp.Key, out _);
                }
            }
            
            // If still too large, remove least recently used
            if (_cache.Count > _maxCacheSize)
            {
                var toRemove = _cache
                    .OrderBy(kvp => kvp.Value.LastAccess)
                    .Take(_cache.Count - _maxCacheSize / 2)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in toRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
    
    private class IconCacheEntry
    {
        public object Value { get; }
        public DateTime CreatedAt { get; }
        public DateTime ExpiresAt { get; }
        public DateTime LastAccess { get; private set; }
        
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        
        public IconCacheEntry(object value, TimeSpan expiry)
        {
            Value = value;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = CreatedAt + expiry;
            LastAccess = CreatedAt;
        }
        
        public void Touch()
        {
            LastAccess = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Debounced git status updater to prevent excessive git calls.
/// </summary>
public class DebouncedGitStatusUpdater
{
    private readonly TimeSpan _debounceDelay;
    private readonly ConcurrentDictionary<string, DebouncedOperation> _operations = new();
    
    public DebouncedGitStatusUpdater(TimeSpan? debounceDelay = null)
    {
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(300);
    }
    
    /// <summary>
    /// Request a git status update for a path, debounced.
    /// </summary>
    public async Task<T?> RequestUpdateAsync<T>(
        string path,
        Func<string, CancellationToken, Task<T>> updateAction,
        CancellationToken cancellationToken = default)
    {
        var operation = _operations.GetOrAdd(path, _ => new DebouncedOperation());
        
        // Cancel any pending operation
        operation.Cancel();
        
        // Create new cancellation for this request
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancellationSource = cts;
        
        try
        {
            // Wait for debounce period
            await Task.Delay(_debounceDelay, cts.Token);
            
            // If we weren't cancelled, perform the update
            return await updateAction(path, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
        finally
        {
            _operations.TryRemove(path, out _);
        }
    }
    
    /// <summary>
    /// Cancel any pending update for a path.
    /// </summary>
    public void Cancel(string path)
    {
        if (_operations.TryGetValue(path, out var operation))
        {
            operation.Cancel();
        }
    }
    
    /// <summary>
    /// Cancel all pending updates.
    /// </summary>
    public void CancelAll()
    {
        foreach (var operation in _operations.Values)
        {
            operation.Cancel();
        }
        _operations.Clear();
    }
    
    private class DebouncedOperation
    {
        public CancellationTokenSource? CancellationSource { get; set; }
        
        public void Cancel()
        {
            CancellationSource?.Cancel();
            CancellationSource = null;
        }
    }
}

/// <summary>
/// Batch loader for folder icons with efficient caching.
/// </summary>
public class BatchFolderIconLoader
{
    private readonly IIconService _iconService;
    private readonly OptimizedIconCache _cache;
    
    public BatchFolderIconLoader(IIconService iconService, OptimizedIconCache? cache = null)
    {
        _iconService = iconService;
        _cache = cache ?? new OptimizedIconCache();
    }
    
    /// <summary>
    /// Queue a folder icon request for batch loading.
    /// </summary>
    public async Task<IconInfo> GetFolderIconAsync(string path, IconSize size, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"folder:{path}:{size}";
        var cached = await _cache.GetOrCreateAsync(cacheKey, async ct =>
        {
            return await _iconService.GetIconAsync(path, size, ct);
        }, null, cancellationToken);
        
        return cached ?? new IconInfo { Category = FileTypeCategory.Folder };
    }
    
    /// <summary>
    /// Preload icons for a list of paths.
    /// </summary>
    public async Task PreloadIconsAsync(IEnumerable<string> paths, IconSize size, CancellationToken cancellationToken = default)
    {
        var tasks = paths.Select(p => GetFolderIconAsync(p, size, cancellationToken));
        await Task.WhenAll(tasks);
    }
}
