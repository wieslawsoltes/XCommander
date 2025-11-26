using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for flat view (branch view) operations.
/// Displays all files from subdirectories in a single flat list.
/// </summary>
public class FlatViewService : IFlatViewService
{
    private readonly List<FlatViewItem> _selectedItems = [];
    private readonly object _lock = new();
    private FlatViewResult? _currentView;
    private string _currentRootPath = string.Empty;
    private FlatViewOptions? _currentOptions;
    
    public FlatViewResult? CurrentView => _currentView;
    
    public event EventHandler<FlatViewSelectionChangedEventArgs>? SelectionChanged;
    
    public async Task<FlatViewResult> GetFlatViewAsync(string rootPath, FlatViewOptions? options = null,
        IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _currentRootPath = rootPath;
        _currentOptions = options ?? new FlatViewOptions();
        
        var stopwatch = Stopwatch.StartNew();
        var items = new List<FlatViewItem>();
        int totalDirectories = 0;
        int maxDepth = 0;
        
        await Task.Run(() =>
        {
            CollectItems(rootPath, rootPath, 0, items, ref totalDirectories, ref maxDepth, 
                _currentOptions, progress, cancellationToken);
        }, cancellationToken);
        
        // Sort items
        var sortedItems = SortItems(items, _currentOptions);
        
        stopwatch.Stop();
        
        _currentView = new FlatViewResult
        {
            RootPath = rootPath,
            Items = sortedItems,
            TotalFiles = items.Count(i => !i.IsDirectory),
            TotalDirectories = totalDirectories,
            TotalSize = items.Where(i => !i.IsDirectory).Sum(i => i.Size),
            MaxDepth = maxDepth,
            Duration = stopwatch.Elapsed
        };
        
        return _currentView;
    }
    
    public IReadOnlyList<FlatViewItem> GetSelectedItems()
    {
        lock (_lock)
        {
            return _selectedItems.ToList();
        }
    }
    
    public void SelectItems(IEnumerable<FlatViewItem> items)
    {
        lock (_lock)
        {
            foreach (var item in items)
            {
                item.IsSelected = true;
                if (!_selectedItems.Any(i => i.FullPath == item.FullPath))
                {
                    _selectedItems.Add(item);
                }
            }
        }
        
        RaiseSelectionChanged();
    }
    
    public void ClearSelection()
    {
        lock (_lock)
        {
            foreach (var item in _selectedItems)
            {
                item.IsSelected = false;
            }
            _selectedItems.Clear();
        }
        
        RaiseSelectionChanged();
    }
    
    public async Task<FlatViewOperationResult> PerformOperationAsync(FlatViewOperation operation,
        string? destination = null,
        IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        List<FlatViewItem> items;
        
        lock (_lock)
        {
            items = _selectedItems.ToList();
        }
        
        var result = new FlatViewOperationResult
        {
            Operation = operation,
            TotalItems = items.Count,
            Errors = new List<string>()
        };
        
        int successful = 0;
        int failed = 0;
        long bytesProcessed = 0;
        
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            progress?.Report(new FlatViewProgress
            {
                Phase = $"Processing: {operation}",
                CurrentPath = item.FullPath,
                FilesProcessed = successful + failed
            });
            
            try
            {
                switch (operation)
                {
                    case FlatViewOperation.Copy:
                        if (!string.IsNullOrEmpty(destination))
                        {
                            var destPath = Path.Combine(destination, item.Name);
                            if (item.IsDirectory)
                                CopyDirectory(item.FullPath, destPath);
                            else
                                File.Copy(item.FullPath, destPath, overwrite: true);
                            bytesProcessed += item.Size;
                        }
                        break;
                        
                    case FlatViewOperation.Move:
                        if (!string.IsNullOrEmpty(destination))
                        {
                            var destPath = Path.Combine(destination, item.Name);
                            if (item.IsDirectory)
                                Directory.Move(item.FullPath, destPath);
                            else
                                File.Move(item.FullPath, destPath, overwrite: true);
                            bytesProcessed += item.Size;
                        }
                        break;
                        
                    case FlatViewOperation.Delete:
                        if (item.IsDirectory)
                            Directory.Delete(item.FullPath, recursive: true);
                        else
                            File.Delete(item.FullPath);
                        bytesProcessed += item.Size;
                        break;
                        
                    case FlatViewOperation.Calculate:
                        // Just count size
                        bytesProcessed += item.Size;
                        break;
                        
                    default:
                        throw new NotSupportedException($"Operation {operation} not supported");
                }
                
                successful++;
            }
            catch (Exception ex)
            {
                failed++;
                result.Errors.Add($"{item.FullPath}: {ex.Message}");
            }
        }
        
        stopwatch.Stop();
        
        // Clear selection after successful operation
        if (operation is FlatViewOperation.Delete or FlatViewOperation.Move)
        {
            ClearSelection();
        }
        
        return result with
        {
            SuccessfulItems = successful,
            FailedItems = failed,
            BytesProcessed = bytesProcessed,
            Duration = stopwatch.Elapsed
        };
    }
    
    public async Task<FlatViewResult> RefreshAsync(IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_currentRootPath))
            throw new InvalidOperationException("No current view to refresh");
        
        return await GetFlatViewAsync(_currentRootPath, _currentOptions, progress, cancellationToken);
    }
    
    private void CollectItems(string rootPath, string currentPath, int depth,
        List<FlatViewItem> items, ref int totalDirectories, ref int maxDepth,
        FlatViewOptions options, IProgress<FlatViewProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (depth > options.MaxDepth)
            return;
        
        if (depth > maxDepth)
            maxDepth = depth;
        
        progress?.Report(new FlatViewProgress
        {
            Phase = "Scanning directories...",
            CurrentPath = currentPath,
            FilesProcessed = items.Count,
            DirectoriesProcessed = totalDirectories
        });
        
        try
        {
            // Process files
            foreach (var file in Directory.EnumerateFiles(currentPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var info = new FileInfo(file);
                    
                    // Apply filters
                    if (!options.IncludeHidden && (info.Attributes & FileAttributes.Hidden) != 0)
                        continue;
                    if (!options.IncludeSystem && (info.Attributes & FileAttributes.System) != 0)
                        continue;
                    if (info.Length < options.MinSize || info.Length > options.MaxSize)
                        continue;
                    if (options.ModifiedAfter.HasValue && info.LastWriteTime < options.ModifiedAfter.Value)
                        continue;
                    if (options.ModifiedBefore.HasValue && info.LastWriteTime > options.ModifiedBefore.Value)
                        continue;
                    if (!string.IsNullOrEmpty(options.FilePattern))
                    {
                        var pattern = options.FilePattern.Replace("*", ".*").Replace("?", ".");
                        if (!System.Text.RegularExpressions.Regex.IsMatch(info.Name, pattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                    }
                    
                    items.Add(new FlatViewItem
                    {
                        FullPath = file,
                        Name = info.Name,
                        RelativePath = Path.GetRelativePath(rootPath, file),
                        Directory = info.DirectoryName ?? string.Empty,
                        IsDirectory = false,
                        Size = info.Length,
                        Modified = info.LastWriteTime,
                        Created = info.CreationTime,
                        Attributes = info.Attributes,
                        Depth = depth
                    });
                }
                catch { } // Skip files we can't access
            }
            
            // Process subdirectories
            foreach (var dir in Directory.EnumerateDirectories(currentPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var info = new DirectoryInfo(dir);
                    
                    if (!options.IncludeHidden && (info.Attributes & FileAttributes.Hidden) != 0)
                        continue;
                    if (!options.IncludeSystem && (info.Attributes & FileAttributes.System) != 0)
                        continue;
                    
                    totalDirectories++;
                    
                    // Recurse into subdirectory
                    CollectItems(rootPath, dir, depth + 1, items, ref totalDirectories, ref maxDepth,
                        options, progress, cancellationToken);
                }
                catch { } // Skip directories we can't access
            }
        }
        catch { } // Skip paths we can't enumerate
    }
    
    private static List<FlatViewItem> SortItems(List<FlatViewItem> items, FlatViewOptions options)
    {
        IOrderedEnumerable<FlatViewItem> sorted = options.SortBy switch
        {
            FlatViewSortBy.Name => options.SortDescending 
                ? items.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            FlatViewSortBy.Size => options.SortDescending 
                ? items.OrderByDescending(i => i.Size)
                : items.OrderBy(i => i.Size),
            FlatViewSortBy.Modified => options.SortDescending 
                ? items.OrderByDescending(i => i.Modified)
                : items.OrderBy(i => i.Modified),
            FlatViewSortBy.Extension => options.SortDescending 
                ? items.OrderByDescending(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase),
            FlatViewSortBy.Path => options.SortDescending 
                ? items.OrderByDescending(i => i.RelativePath, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(i => i.RelativePath, StringComparer.OrdinalIgnoreCase),
            FlatViewSortBy.Depth => options.SortDescending 
                ? items.OrderByDescending(i => i.Depth)
                : items.OrderBy(i => i.Depth),
            _ => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        };
        
        if (options.GroupByDirectory)
        {
            sorted = sorted.ThenBy(i => i.Directory, StringComparer.OrdinalIgnoreCase);
        }
        
        return sorted.ToList();
    }
    
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }
    
    private void RaiseSelectionChanged()
    {
        List<FlatViewItem> selected;
        lock (_lock)
        {
            selected = _selectedItems.ToList();
        }
        
        SelectionChanged?.Invoke(this, new FlatViewSelectionChangedEventArgs
        {
            SelectedItems = selected,
            SelectedCount = selected.Count,
            SelectedSize = selected.Sum(i => i.Size)
        });
    }
}
