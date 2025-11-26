using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Virtual scroll range for visible items
/// </summary>
public record VirtualScrollRange
{
    /// <summary>Start index of visible items</summary>
    public int StartIndex { get; init; }
    
    /// <summary>End index of visible items</summary>
    public int EndIndex { get; init; }
    
    /// <summary>Number of visible items</summary>
    public int VisibleCount => EndIndex - StartIndex + 1;
    
    /// <summary>Total items in the list</summary>
    public int TotalCount { get; init; }
    
    /// <summary>Scroll offset in pixels</summary>
    public double ScrollOffset { get; init; }
    
    /// <summary>Viewport height in pixels</summary>
    public double ViewportHeight { get; init; }
}

/// <summary>
/// Virtual item representing a file/folder in the scrollable list
/// </summary>
public record VirtualFileItem
{
    /// <summary>Unique item index</summary>
    public int Index { get; init; }
    
    /// <summary>Full path to file or folder</summary>
    public string Path { get; init; } = string.Empty;
    
    /// <summary>File or folder name</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Is this a directory</summary>
    public bool IsDirectory { get; init; }
    
    /// <summary>File size (0 for directories)</summary>
    public long Size { get; init; }
    
    /// <summary>Last modified time</summary>
    public DateTime? LastModified { get; init; }
    
    /// <summary>File attributes</summary>
    public System.IO.FileAttributes Attributes { get; init; }
    
    /// <summary>File extension</summary>
    public string Extension { get; init; } = string.Empty;
    
    /// <summary>Is selected</summary>
    public bool IsSelected { get; set; }
    
    /// <summary>Is focused</summary>
    public bool IsFocused { get; set; }
}

/// <summary>
/// Configuration for virtual scrolling
/// </summary>
public record VirtualScrollConfig
{
    /// <summary>Height of each item in pixels</summary>
    public double ItemHeight { get; init; } = 24;
    
    /// <summary>Number of items to overscan beyond visible range</summary>
    public int OverscanCount { get; init; } = 5;
    
    /// <summary>Enable smooth scrolling</summary>
    public bool SmoothScrolling { get; init; } = true;
    
    /// <summary>Scroll speed multiplier</summary>
    public double ScrollSpeed { get; init; } = 1.0;
    
    /// <summary>Minimum items to trigger virtualization</summary>
    public int VirtualizationThreshold { get; init; } = 100;
}

/// <summary>
/// Scroll event arguments
/// </summary>
public record VirtualScrollEventArgs
{
    public VirtualScrollRange Range { get; init; } = new();
    public IReadOnlyList<VirtualFileItem> VisibleItems { get; init; } = Array.Empty<VirtualFileItem>();
    public bool IsScrolling { get; init; }
}

/// <summary>
/// Service for virtual scrolling to handle 100K+ file directories.
/// TC equivalent: Fast file list rendering with virtual scrolling.
/// </summary>
public interface IVirtualScrollingService
{
    /// <summary>
    /// Load items from a directory for virtual scrolling
    /// </summary>
    Task<int> LoadDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get visible items for current scroll position
    /// </summary>
    IReadOnlyList<VirtualFileItem> GetVisibleItems(VirtualScrollRange range);
    
    /// <summary>
    /// Get items in a specific range
    /// </summary>
    IReadOnlyList<VirtualFileItem> GetItemsInRange(int startIndex, int endIndex);
    
    /// <summary>
    /// Get item at specific index
    /// </summary>
    VirtualFileItem? GetItemAt(int index);
    
    /// <summary>
    /// Get index of item by path
    /// </summary>
    int GetIndexOfPath(string path);
    
    /// <summary>
    /// Calculate visible range from scroll position
    /// </summary>
    VirtualScrollRange CalculateVisibleRange(
        double scrollOffset,
        double viewportHeight,
        VirtualScrollConfig config);
    
    /// <summary>
    /// Get total content height
    /// </summary>
    double GetTotalHeight(VirtualScrollConfig config);
    
    /// <summary>
    /// Get scroll offset for specific item
    /// </summary>
    double GetScrollOffsetForItem(int index, VirtualScrollConfig config);
    
    /// <summary>
    /// Scroll to specific item
    /// </summary>
    VirtualScrollRange ScrollToItem(
        int index,
        double viewportHeight,
        VirtualScrollConfig config);
    
    /// <summary>
    /// Scroll to path
    /// </summary>
    VirtualScrollRange? ScrollToPath(
        string path,
        double viewportHeight,
        VirtualScrollConfig config);
    
    /// <summary>
    /// Get total item count
    /// </summary>
    int TotalCount { get; }
    
    /// <summary>
    /// Current directory path
    /// </summary>
    string? CurrentPath { get; }
    
    /// <summary>
    /// Select item at index
    /// </summary>
    void SelectItem(int index, bool addToSelection = false);
    
    /// <summary>
    /// Select range of items
    /// </summary>
    void SelectRange(int startIndex, int endIndex);
    
    /// <summary>
    /// Clear selection
    /// </summary>
    void ClearSelection();
    
    /// <summary>
    /// Get selected items
    /// </summary>
    IReadOnlyList<VirtualFileItem> GetSelectedItems();
    
    /// <summary>
    /// Get selected indices
    /// </summary>
    IReadOnlySet<int> GetSelectedIndices();
    
    /// <summary>
    /// Set focused item
    /// </summary>
    void SetFocusedItem(int index);
    
    /// <summary>
    /// Get focused item index
    /// </summary>
    int GetFocusedIndex();
    
    /// <summary>
    /// Sort items
    /// </summary>
    void Sort(Comparison<VirtualFileItem> comparison);
    
    /// <summary>
    /// Filter items
    /// </summary>
    void ApplyFilter(Predicate<VirtualFileItem> filter);
    
    /// <summary>
    /// Clear filter
    /// </summary>
    void ClearFilter();
    
    /// <summary>
    /// Refresh from disk
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when scroll position changes
    /// </summary>
    event EventHandler<VirtualScrollEventArgs>? ScrollChanged;
    
    /// <summary>
    /// Event raised when selection changes
    /// </summary>
    event EventHandler<IReadOnlyList<VirtualFileItem>>? SelectionChanged;
    
    /// <summary>
    /// Event raised when items are loaded
    /// </summary>
    event EventHandler<int>? ItemsLoaded;
}
