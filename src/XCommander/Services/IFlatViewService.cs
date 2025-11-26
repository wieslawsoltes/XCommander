using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for flat view (branch view) operations.
/// Shows all files from subdirectories in a single flat list.
/// </summary>
public interface IFlatViewService
{
    /// <summary>
    /// Gets a flat view of all files in a directory tree.
    /// </summary>
    Task<FlatViewResult> GetFlatViewAsync(string rootPath, FlatViewOptions? options = null,
        IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets selected files from flat view.
    /// </summary>
    IReadOnlyList<FlatViewItem> GetSelectedItems();
    
    /// <summary>
    /// Selects items in flat view.
    /// </summary>
    void SelectItems(IEnumerable<FlatViewItem> items);
    
    /// <summary>
    /// Clears selection.
    /// </summary>
    void ClearSelection();
    
    /// <summary>
    /// Performs operation on selected files.
    /// </summary>
    Task<FlatViewOperationResult> PerformOperationAsync(FlatViewOperation operation,
        string? destination = null,
        IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refreshes the flat view.
    /// </summary>
    Task<FlatViewResult> RefreshAsync(IProgress<FlatViewProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets current flat view state.
    /// </summary>
    FlatViewResult? CurrentView { get; }
    
    /// <summary>
    /// Event raised when selection changes.
    /// </summary>
    event EventHandler<FlatViewSelectionChangedEventArgs>? SelectionChanged;
}

/// <summary>
/// Options for flat view.
/// </summary>
public record FlatViewOptions
{
    public int MaxDepth { get; init; } = int.MaxValue;
    public string? FilePattern { get; init; }
    public long MinSize { get; init; }
    public long MaxSize { get; init; } = long.MaxValue;
    public DateTime? ModifiedAfter { get; init; }
    public DateTime? ModifiedBefore { get; init; }
    public bool IncludeHidden { get; init; }
    public bool IncludeSystem { get; init; }
    public FlatViewSortBy SortBy { get; init; } = FlatViewSortBy.Name;
    public bool SortDescending { get; init; }
    public bool GroupByDirectory { get; init; }
}

/// <summary>
/// Sort options for flat view.
/// </summary>
public enum FlatViewSortBy
{
    Name,
    Size,
    Modified,
    Extension,
    Path,
    Depth
}

/// <summary>
/// Result of flat view operation.
/// </summary>
public record FlatViewResult
{
    public string RootPath { get; init; } = string.Empty;
    public IReadOnlyList<FlatViewItem> Items { get; init; } = Array.Empty<FlatViewItem>();
    public int TotalFiles { get; init; }
    public int TotalDirectories { get; init; }
    public long TotalSize { get; init; }
    public int MaxDepth { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Item in flat view.
/// </summary>
public record FlatViewItem
{
    public string FullPath { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public DateTime Created { get; init; }
    public FileAttributes Attributes { get; init; }
    public int Depth { get; init; }
    public bool IsSelected { get; set; }
    
    public string DisplaySize => FormatSize(Size);
    public string Extension => Path.GetExtension(Name);
    
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
/// Progress for flat view operations.
/// </summary>
public record FlatViewProgress
{
    public string CurrentPath { get; init; } = string.Empty;
    public int FilesProcessed { get; init; }
    public int DirectoriesProcessed { get; init; }
    public string Phase { get; init; } = string.Empty;
}

/// <summary>
/// Operations that can be performed on flat view selection.
/// </summary>
public enum FlatViewOperation
{
    Copy,
    Move,
    Delete,
    Rename,
    SetAttributes,
    Compress,
    Calculate
}

/// <summary>
/// Result of flat view operation.
/// </summary>
public record FlatViewOperationResult
{
    public FlatViewOperation Operation { get; init; }
    public int TotalItems { get; init; }
    public int SuccessfulItems { get; init; }
    public int FailedItems { get; init; }
    public long BytesProcessed { get; init; }
    public List<string> Errors { get; init; } = new();
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Event args for selection change.
/// </summary>
public class FlatViewSelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<FlatViewItem> SelectedItems { get; init; } = Array.Empty<FlatViewItem>();
    public int SelectedCount { get; init; }
    public long SelectedSize { get; init; }
}
