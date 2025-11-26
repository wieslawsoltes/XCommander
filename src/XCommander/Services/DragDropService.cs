// IDragDropService.cs - Drag and Drop Service
// Provides enhanced drag and drop with Explorer integration

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for handling drag and drop operations.
/// </summary>
public interface IDragDropService
{
    /// <summary>
    /// Starts a drag operation from XCommander.
    /// </summary>
    Task<DragDropResult> StartDragAsync(
        IEnumerable<string> paths,
        DragDropEffects allowedEffects,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handles a drop from external source.
    /// </summary>
    Task<DragDropResult> HandleDropAsync(
        object data,
        string targetPath,
        DragDropEffects effect,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets file paths from dropped data.
    /// </summary>
    IReadOnlyList<string>? GetDroppedFilePaths(object data);
    
    /// <summary>
    /// Creates drag data for files.
    /// </summary>
    object CreateDragData(IEnumerable<string> paths);
    
    /// <summary>
    /// Determines the appropriate effect for a drag over target.
    /// </summary>
    DragDropEffects GetAllowedEffect(object data, string targetPath, DragDropEffects allowedEffects, bool ctrlPressed, bool shiftPressed);
    
    /// <summary>
    /// Gets drag preview information.
    /// </summary>
    DragPreviewInfo GetDragPreview(IEnumerable<string> paths);
}

/// <summary>
/// Result of a drag/drop operation.
/// </summary>
public record DragDropResult
{
    public required DragDropEffects Effect { get; init; }
    public required bool Success { get; init; }
    public int ItemsProcessed { get; init; }
    public int ItemsFailed { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string>? ProcessedPaths { get; init; }
}

/// <summary>
/// Drag/drop effects.
/// </summary>
[Flags]
public enum DragDropEffects
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
    All = Copy | Move | Link
}

/// <summary>
/// Information for drag preview.
/// </summary>
public record DragPreviewInfo
{
    public required int TotalItems { get; init; }
    public required int FileCount { get; init; }
    public required int FolderCount { get; init; }
    public required long TotalSize { get; init; }
    public string? PreviewText { get; init; }
    public byte[]? PreviewImage { get; init; }
}

/// <summary>
/// Implementation of drag and drop service.
/// </summary>
public class DragDropService : IDragDropService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IOperationLogService? _logService;
    
    public DragDropService(IFileSystemService fileSystemService, IOperationLogService? logService = null)
    {
        _fileSystemService = fileSystemService;
        _logService = logService;
    }
    
    public async Task<DragDropResult> StartDragAsync(
        IEnumerable<string> paths,
        DragDropEffects allowedEffects,
        CancellationToken cancellationToken = default)
    {
        var pathList = paths.ToList();
        
        if (pathList.Count == 0)
        {
            return new DragDropResult
            {
                Effect = DragDropEffects.None,
                Success = false,
                ErrorMessage = "No items to drag"
            };
        }
        
        // Create drag data
        var data = CreateDragData(pathList);
        
        // The actual drag operation would be started by the UI layer
        // This just prepares the data
        
        return new DragDropResult
        {
            Effect = allowedEffects,
            Success = true,
            ItemsProcessed = pathList.Count,
            ProcessedPaths = pathList
        };
    }
    
    public async Task<DragDropResult> HandleDropAsync(
        object data,
        string targetPath,
        DragDropEffects effect,
        CancellationToken cancellationToken = default)
    {
        var paths = GetDroppedFilePaths(data);
        
        if (paths == null || paths.Count == 0)
        {
            return new DragDropResult
            {
                Effect = DragDropEffects.None,
                Success = false,
                ErrorMessage = "No files in drop data"
            };
        }
        
        var processed = 0;
        var failed = 0;
        
        foreach (var sourcePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetPath, fileName);
                
                if (effect == DragDropEffects.Move)
                {
                    if (Directory.Exists(sourcePath))
                        Directory.Move(sourcePath, destPath);
                    else
                        File.Move(sourcePath, destPath);
                }
                else if (effect == DragDropEffects.Copy)
                {
                    if (Directory.Exists(sourcePath))
                        await CopyDirectoryAsync(sourcePath, destPath, cancellationToken);
                    else
                        File.Copy(sourcePath, destPath);
                }
                else if (effect == DragDropEffects.Link)
                {
                    // Create symbolic link
                    if (Directory.Exists(sourcePath))
                        Directory.CreateSymbolicLink(destPath, sourcePath);
                    else
                        File.CreateSymbolicLink(destPath, sourcePath);
                }
                
                processed++;
            }
            catch
            {
                failed++;
            }
        }
        
        // Log operation
        if (_logService != null)
        {
            var logEntry = new OperationLogEntry
            {
                Type = effect switch
                {
                    DragDropEffects.Move => OperationType.Move,
                    DragDropEffects.Link => OperationType.CreateLink,
                    _ => OperationType.Copy
                },
                Status = failed == 0 ? OperationStatus.Completed : 
                         (processed > 0 ? OperationStatus.PartialSuccess : OperationStatus.Failed),
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                SourcePaths = paths.ToList(),
                DestinationPath = targetPath,
                FilesProcessed = processed,
                ItemsFailed = failed
            };
            
            await _logService.LogOperationAsync(logEntry, cancellationToken);
        }
        
        return new DragDropResult
        {
            Effect = effect,
            Success = failed == 0,
            ItemsProcessed = processed,
            ItemsFailed = failed,
            ProcessedPaths = paths
        };
    }
    
    public IReadOnlyList<string>? GetDroppedFilePaths(object data)
    {
        // Handle various data formats
        if (data is IEnumerable<string> stringPaths)
        {
            return stringPaths.ToList();
        }
        
        if (data is string singlePath && (File.Exists(singlePath) || Directory.Exists(singlePath)))
        {
            return new[] { singlePath };
        }
        
        // In a real implementation, handle platform-specific formats:
        // - Windows: IDataObject with CF_HDROP format
        // - macOS: NSPasteboard with file URLs
        // - Linux: text/uri-list MIME type
        
        return null;
    }
    
    public object CreateDragData(IEnumerable<string> paths)
    {
        // In a real implementation, create platform-specific drag data:
        // - Windows: DataObject with CF_HDROP
        // - macOS: NSPasteboard
        // - Linux: appropriate MIME types
        
        return paths.ToArray();
    }
    
    public DragDropEffects GetAllowedEffect(object data, string targetPath, DragDropEffects allowedEffects, bool ctrlPressed, bool shiftPressed)
    {
        var paths = GetDroppedFilePaths(data);
        
        if (paths == null || paths.Count == 0)
            return DragDropEffects.None;
        
        // Check if target is a valid directory
        if (!Directory.Exists(targetPath))
            return DragDropEffects.None;
        
        // Check if dragging to same location
        var targetDir = Path.GetFullPath(targetPath);
        foreach (var path in paths)
        {
            var sourceDir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (sourceDir?.Equals(targetDir, StringComparison.OrdinalIgnoreCase) == true)
            {
                // Same directory - only allow if creating a copy
                if (!ctrlPressed)
                    return DragDropEffects.None;
            }
        }
        
        // Determine effect based on modifiers and source/target locations
        if (ctrlPressed && shiftPressed)
        {
            return (allowedEffects & DragDropEffects.Link) != 0 
                ? DragDropEffects.Link 
                : DragDropEffects.None;
        }
        
        if (ctrlPressed)
        {
            return (allowedEffects & DragDropEffects.Copy) != 0 
                ? DragDropEffects.Copy 
                : DragDropEffects.None;
        }
        
        if (shiftPressed)
        {
            return (allowedEffects & DragDropEffects.Move) != 0 
                ? DragDropEffects.Move 
                : DragDropEffects.None;
        }
        
        // Default: check if same drive (move) or different drive (copy)
        var sourceDrive = Path.GetPathRoot(paths[0]);
        var targetDrive = Path.GetPathRoot(targetPath);
        
        if (sourceDrive?.Equals(targetDrive, StringComparison.OrdinalIgnoreCase) == true)
        {
            return (allowedEffects & DragDropEffects.Move) != 0 
                ? DragDropEffects.Move 
                : DragDropEffects.Copy;
        }
        else
        {
            return (allowedEffects & DragDropEffects.Copy) != 0 
                ? DragDropEffects.Copy 
                : DragDropEffects.Move;
        }
    }
    
    public DragPreviewInfo GetDragPreview(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();
        int fileCount = 0, folderCount = 0;
        long totalSize = 0;
        
        foreach (var path in pathList)
        {
            if (Directory.Exists(path))
            {
                folderCount++;
                // Optionally calculate folder size
            }
            else if (File.Exists(path))
            {
                fileCount++;
                totalSize += new FileInfo(path).Length;
            }
        }
        
        string previewText;
        if (pathList.Count == 1)
        {
            previewText = Path.GetFileName(pathList[0]);
        }
        else
        {
            var parts = new List<string>();
            if (fileCount > 0)
                parts.Add($"{fileCount} file{(fileCount > 1 ? "s" : "")}");
            if (folderCount > 0)
                parts.Add($"{folderCount} folder{(folderCount > 1 ? "s" : "")}");
            previewText = string.Join(", ", parts);
        }
        
        return new DragPreviewInfo
        {
            TotalItems = pathList.Count,
            FileCount = fileCount,
            FolderCount = folderCount,
            TotalSize = totalSize,
            PreviewText = previewText
        };
    }
    
    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);
        
        // Copy files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }
        
        // Copy subdirectories recursively
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir, cancellationToken);
        }
    }
}
