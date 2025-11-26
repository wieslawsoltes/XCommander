using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of virtual scrolling for large directories.
/// Handles 100K+ files efficiently using virtualization.
/// </summary>
public sealed class VirtualScrollingService : IVirtualScrollingService
{
    private readonly ILongPathService _longPathService;
    
    private List<VirtualFileItem> _allItems = new();
    private List<VirtualFileItem> _filteredItems = new();
    private readonly HashSet<int> _selectedIndices = new();
    private int _focusedIndex = -1;
    private Predicate<VirtualFileItem>? _currentFilter;
    private string? _currentPath;
    
    public event EventHandler<VirtualScrollEventArgs>? ScrollChanged;
    public event EventHandler<IReadOnlyList<VirtualFileItem>>? SelectionChanged;
    public event EventHandler<int>? ItemsLoaded;
    
    public int TotalCount => _filteredItems.Count;
    public string? CurrentPath => _currentPath;
    
    public VirtualScrollingService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
    }
    
    public async Task<int> LoadDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        _currentPath = directoryPath;
        _allItems.Clear();
        _filteredItems.Clear();
        _selectedIndices.Clear();
        _focusedIndex = -1;
        
        var normalizedPath = _longPathService.NormalizePath(directoryPath);
        
        await Task.Run(() =>
        {
            var index = 0;
            
            // Load directories first
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(normalizedPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        _allItems.Add(new VirtualFileItem
                        {
                            Index = index++,
                            Path = dir,
                            Name = dirInfo.Name,
                            IsDirectory = true,
                            Size = 0,
                            LastModified = dirInfo.LastWriteTime,
                            Attributes = dirInfo.Attributes,
                            Extension = string.Empty
                        });
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            
            // Load files
            try
            {
                foreach (var file in Directory.EnumerateFiles(normalizedPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        _allItems.Add(new VirtualFileItem
                        {
                            Index = index++,
                            Path = file,
                            Name = fileInfo.Name,
                            IsDirectory = false,
                            Size = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            Attributes = fileInfo.Attributes,
                            Extension = fileInfo.Extension
                        });
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }, cancellationToken);
        
        // Apply filter if exists
        if (_currentFilter != null)
        {
            _filteredItems = _allItems.Where(item => _currentFilter(item)).ToList();
            ReindexItems(_filteredItems);
        }
        else
        {
            _filteredItems = new List<VirtualFileItem>(_allItems);
        }
        
        ItemsLoaded?.Invoke(this, _filteredItems.Count);
        
        return _filteredItems.Count;
    }
    
    public IReadOnlyList<VirtualFileItem> GetVisibleItems(VirtualScrollRange range)
    {
        return GetItemsInRange(range.StartIndex, range.EndIndex);
    }
    
    public IReadOnlyList<VirtualFileItem> GetItemsInRange(int startIndex, int endIndex)
    {
        if (_filteredItems.Count == 0)
            return Array.Empty<VirtualFileItem>();
        
        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(_filteredItems.Count - 1, endIndex);
        
        if (startIndex > endIndex)
            return Array.Empty<VirtualFileItem>();
        
        var result = new List<VirtualFileItem>(endIndex - startIndex + 1);
        for (int i = startIndex; i <= endIndex; i++)
        {
            var item = _filteredItems[i];
            item.IsSelected = _selectedIndices.Contains(i);
            item.IsFocused = i == _focusedIndex;
            result.Add(item);
        }
        
        return result;
    }
    
    public VirtualFileItem? GetItemAt(int index)
    {
        if (index < 0 || index >= _filteredItems.Count)
            return null;
        
        var item = _filteredItems[index];
        item.IsSelected = _selectedIndices.Contains(index);
        item.IsFocused = index == _focusedIndex;
        return item;
    }
    
    public int GetIndexOfPath(string path)
    {
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            if (string.Equals(_filteredItems[i].Path, path, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
    
    public VirtualScrollRange CalculateVisibleRange(
        double scrollOffset,
        double viewportHeight,
        VirtualScrollConfig config)
    {
        if (_filteredItems.Count == 0 || config.ItemHeight <= 0)
        {
            return new VirtualScrollRange
            {
                StartIndex = 0,
                EndIndex = -1,
                TotalCount = 0,
                ScrollOffset = scrollOffset,
                ViewportHeight = viewportHeight
            };
        }
        
        var startIndex = (int)(scrollOffset / config.ItemHeight);
        var visibleCount = (int)Math.Ceiling(viewportHeight / config.ItemHeight);
        var endIndex = startIndex + visibleCount;
        
        // Apply overscan
        startIndex = Math.Max(0, startIndex - config.OverscanCount);
        endIndex = Math.Min(_filteredItems.Count - 1, endIndex + config.OverscanCount);
        
        return new VirtualScrollRange
        {
            StartIndex = startIndex,
            EndIndex = endIndex,
            TotalCount = _filteredItems.Count,
            ScrollOffset = scrollOffset,
            ViewportHeight = viewportHeight
        };
    }
    
    public double GetTotalHeight(VirtualScrollConfig config)
    {
        return _filteredItems.Count * config.ItemHeight;
    }
    
    public double GetScrollOffsetForItem(int index, VirtualScrollConfig config)
    {
        return index * config.ItemHeight;
    }
    
    public VirtualScrollRange ScrollToItem(
        int index,
        double viewportHeight,
        VirtualScrollConfig config)
    {
        if (index < 0 || index >= _filteredItems.Count)
        {
            return CalculateVisibleRange(0, viewportHeight, config);
        }
        
        var scrollOffset = GetScrollOffsetForItem(index, config);
        
        // Center the item if possible
        var centerOffset = scrollOffset - (viewportHeight / 2) + (config.ItemHeight / 2);
        centerOffset = Math.Max(0, centerOffset);
        
        var maxOffset = GetTotalHeight(config) - viewportHeight;
        centerOffset = Math.Min(maxOffset, centerOffset);
        
        var range = CalculateVisibleRange(centerOffset, viewportHeight, config);
        
        ScrollChanged?.Invoke(this, new VirtualScrollEventArgs
        {
            Range = range,
            VisibleItems = GetVisibleItems(range),
            IsScrolling = false
        });
        
        return range;
    }
    
    public VirtualScrollRange? ScrollToPath(
        string path,
        double viewportHeight,
        VirtualScrollConfig config)
    {
        var index = GetIndexOfPath(path);
        if (index < 0)
            return null;
        
        return ScrollToItem(index, viewportHeight, config);
    }
    
    public void SelectItem(int index, bool addToSelection = false)
    {
        if (index < 0 || index >= _filteredItems.Count)
            return;
        
        if (!addToSelection)
        {
            _selectedIndices.Clear();
        }
        
        if (!_selectedIndices.Add(index))
        {
            // Toggle off if already selected
            _selectedIndices.Remove(index);
        }
        
        _focusedIndex = index;
        
        SelectionChanged?.Invoke(this, GetSelectedItems());
    }
    
    public void SelectRange(int startIndex, int endIndex)
    {
        if (startIndex > endIndex)
            (startIndex, endIndex) = (endIndex, startIndex);
        
        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(_filteredItems.Count - 1, endIndex);
        
        for (int i = startIndex; i <= endIndex; i++)
        {
            _selectedIndices.Add(i);
        }
        
        SelectionChanged?.Invoke(this, GetSelectedItems());
    }
    
    public void ClearSelection()
    {
        _selectedIndices.Clear();
        SelectionChanged?.Invoke(this, Array.Empty<VirtualFileItem>());
    }
    
    public IReadOnlyList<VirtualFileItem> GetSelectedItems()
    {
        return _selectedIndices
            .OrderBy(i => i)
            .Where(i => i >= 0 && i < _filteredItems.Count)
            .Select(i => _filteredItems[i])
            .ToList();
    }
    
    public IReadOnlySet<int> GetSelectedIndices()
    {
        return _selectedIndices;
    }
    
    public void SetFocusedItem(int index)
    {
        _focusedIndex = index >= 0 && index < _filteredItems.Count ? index : -1;
    }
    
    public int GetFocusedIndex()
    {
        return _focusedIndex;
    }
    
    public void Sort(Comparison<VirtualFileItem> comparison)
    {
        // Preserve selection by path
        var selectedPaths = GetSelectedItems().Select(i => i.Path).ToHashSet();
        var focusedPath = _focusedIndex >= 0 && _focusedIndex < _filteredItems.Count
            ? _filteredItems[_focusedIndex].Path
            : null;
        
        _filteredItems.Sort(comparison);
        ReindexItems(_filteredItems);
        
        // Restore selection
        _selectedIndices.Clear();
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            if (selectedPaths.Contains(_filteredItems[i].Path))
                _selectedIndices.Add(i);
            if (focusedPath != null && _filteredItems[i].Path == focusedPath)
                _focusedIndex = i;
        }
    }
    
    public void ApplyFilter(Predicate<VirtualFileItem> filter)
    {
        _currentFilter = filter;
        
        // Preserve selection by path
        var selectedPaths = GetSelectedItems().Select(i => i.Path).ToHashSet();
        var focusedPath = _focusedIndex >= 0 && _focusedIndex < _filteredItems.Count
            ? _filteredItems[_focusedIndex].Path
            : null;
        
        _filteredItems = _allItems.Where(item => filter(item)).ToList();
        ReindexItems(_filteredItems);
        
        // Restore selection
        _selectedIndices.Clear();
        _focusedIndex = -1;
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            if (selectedPaths.Contains(_filteredItems[i].Path))
                _selectedIndices.Add(i);
            if (focusedPath != null && _filteredItems[i].Path == focusedPath)
                _focusedIndex = i;
        }
        
        ItemsLoaded?.Invoke(this, _filteredItems.Count);
    }
    
    public void ClearFilter()
    {
        _currentFilter = null;
        
        // Preserve selection by path
        var selectedPaths = GetSelectedItems().Select(i => i.Path).ToHashSet();
        var focusedPath = _focusedIndex >= 0 && _focusedIndex < _filteredItems.Count
            ? _filteredItems[_focusedIndex].Path
            : null;
        
        _filteredItems = new List<VirtualFileItem>(_allItems);
        
        // Restore selection
        _selectedIndices.Clear();
        _focusedIndex = -1;
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            if (selectedPaths.Contains(_filteredItems[i].Path))
                _selectedIndices.Add(i);
            if (focusedPath != null && _filteredItems[i].Path == focusedPath)
                _focusedIndex = i;
        }
        
        ItemsLoaded?.Invoke(this, _filteredItems.Count);
    }
    
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_currentPath != null)
        {
            await LoadDirectoryAsync(_currentPath, cancellationToken);
        }
    }
    
    private void ReindexItems(List<VirtualFileItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i] = items[i] with { Index = i };
        }
    }
}
