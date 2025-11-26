// IDirectorySyncService.cs - TC-style directory synchronization
// Synchronize directories like Total Commander's Synchronize Dirs (Alt+F9)

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// How files are compared during sync
/// </summary>
public enum SyncCompareMode
{
    ByContent,      // Full content comparison (slow but accurate)
    BySize,         // Compare file sizes
    ByDate,         // Compare modification dates
    BySizeAndDate,  // Compare size and date (default)
    ByHash          // Compare file hashes (slower but accurate)
}

/// <summary>
/// Direction for synchronization
/// </summary>
public enum SyncCopyDirection
{
    LeftToRight,    // Copy from left to right
    RightToLeft,    // Copy from right to left
    Both,           // Two-way sync (both directions)
    None            // No synchronization
}

/// <summary>
/// What to do with a specific file
/// </summary>
public enum SyncFileAction
{
    Skip,           // Do nothing
    CopyLeft,       // Copy to left
    CopyRight,      // Copy to right
    DeleteLeft,     // Delete from left
    DeleteRight,    // Delete from right
    Equal,          // Files are equal
    Different,      // Files differ but no action set
    Conflict        // Conflict (modified on both sides)
}

/// <summary>
/// Status of a file in sync comparison
/// </summary>
public enum SyncFileStatus
{
    Equal,          // Identical files
    LeftNewer,      // Left file is newer
    RightNewer,     // Right file is newer  
    LeftOnly,       // File only exists on left
    RightOnly,      // File only exists on right
    Different,      // Files differ (same date/size but different content)
    Unknown         // Status not yet determined
}

/// <summary>
/// Information about a file in sync comparison
/// </summary>
public record SyncFileEntry
{
    public string RelativePath { get; init; } = string.Empty;
    public string? LeftFullPath { get; init; }
    public string? RightFullPath { get; init; }
    public long? LeftSize { get; init; }
    public long? RightSize { get; init; }
    public DateTime? LeftModified { get; init; }
    public DateTime? RightModified { get; init; }
    public bool IsDirectory { get; init; }
    public SyncFileStatus Status { get; init; } = SyncFileStatus.Unknown;
    public SyncFileAction Action { get; init; } = SyncFileAction.Skip;
    public string? Hash { get; init; }
}

/// <summary>
/// Options for directory synchronization
/// </summary>
public record SyncOptions
{
    public SyncCompareMode CompareMode { get; init; } = SyncCompareMode.BySizeAndDate;
    public SyncCopyDirection Direction { get; init; } = SyncCopyDirection.Both;
    public bool IncludeSubfolders { get; init; } = true;
    public bool IgnoreHiddenFiles { get; init; } = false;
    public bool IgnoreSystemFiles { get; init; } = false;
    public string? IncludeFilter { get; init; }    // e.g., "*.cs;*.txt"
    public string? ExcludeFilter { get; init; }    // e.g., "*.bak;*.tmp"
    public bool IgnoreCase { get; init; } = true;
    public int DateToleranceSeconds { get; init; } = 2;  // For FAT file systems
    public bool ShowEqualFiles { get; init; } = false;
    public bool AsymmetricMode { get; init; } = false;   // Right side is always correct
    public bool DeleteDifferentOnRight { get; init; } = false;
}

/// <summary>
/// Result of synchronization operation
/// </summary>
public record SyncOperationResult
{
    public bool Success { get; init; }
    public int FilesCopiedLeft { get; init; }
    public int FilesCopiedRight { get; init; }
    public int FilesDeleted { get; init; }
    public int DirectoriesCreated { get; init; }
    public long BytesTransferred { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SyncFileEntry> ProcessedFiles { get; init; } = Array.Empty<SyncFileEntry>();
}

/// <summary>
/// Progress information for sync operation
/// </summary>
public record SyncProgressInfo
{
    public string CurrentFile { get; init; } = string.Empty;
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public double ProgressPercent { get; init; }
    public string Phase { get; init; } = string.Empty;  // "Comparing", "Synchronizing"
}

/// <summary>
/// Service for synchronizing directories (TC's Alt+F9)
/// </summary>
public interface IDirectorySyncService
{
    // ======= Comparison =======
    
    /// <summary>
    /// Compare two directories
    /// </summary>
    Task<IReadOnlyList<SyncFileEntry>> CompareDirectoriesAsync(
        string leftPath,
        string rightPath,
        SyncOptions options,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update comparison after manual changes
    /// </summary>
    Task<IReadOnlyList<SyncFileEntry>> RefreshComparisonAsync(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options,
        CancellationToken cancellationToken = default);
    
    // ======= Synchronization =======
    
    /// <summary>
    /// Execute synchronization based on file entries
    /// </summary>
    Task<SyncOperationResult> SynchronizeAsync(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Preview sync (show what would be done without actually doing it)
    /// </summary>
    SyncOperationResult PreviewSync(IReadOnlyList<SyncFileEntry> entries);
    
    // ======= Action Assignment =======
    
    /// <summary>
    /// Automatically assign actions based on options
    /// </summary>
    IReadOnlyList<SyncFileEntry> AutoAssignActions(
        IReadOnlyList<SyncFileEntry> entries,
        SyncOptions options);
    
    /// <summary>
    /// Set action for specific file
    /// </summary>
    SyncFileEntry SetAction(SyncFileEntry entry, SyncFileAction action);
    
    /// <summary>
    /// Set action for multiple files
    /// </summary>
    IReadOnlyList<SyncFileEntry> SetActionsForAll(
        IReadOnlyList<SyncFileEntry> entries,
        SyncFileAction action,
        Func<SyncFileEntry, bool>? filter = null);
    
    // ======= Filtering =======
    
    /// <summary>
    /// Filter entries by status
    /// </summary>
    IReadOnlyList<SyncFileEntry> FilterByStatus(
        IReadOnlyList<SyncFileEntry> entries,
        params SyncFileStatus[] statuses);
    
    /// <summary>
    /// Filter entries by action
    /// </summary>
    IReadOnlyList<SyncFileEntry> FilterByAction(
        IReadOnlyList<SyncFileEntry> entries,
        params SyncFileAction[] actions);
    
    // ======= Statistics =======
    
    /// <summary>
    /// Get statistics about sync entries
    /// </summary>
    SyncStatistics GetStatistics(IReadOnlyList<SyncFileEntry> entries);
    
    // ======= Events =======
    
    /// <summary>
    /// Raised when a file conflict is encountered during sync
    /// </summary>
    event EventHandler<SyncConflictEventArgs>? ConflictEncountered;
}

/// <summary>
/// Statistics about sync entries
/// </summary>
public record SyncStatistics
{
    public int TotalFiles { get; init; }
    public int EqualFiles { get; init; }
    public int LeftNewerFiles { get; init; }
    public int RightNewerFiles { get; init; }
    public int LeftOnlyFiles { get; init; }
    public int RightOnlyFiles { get; init; }
    public int DifferentFiles { get; init; }
    public int FilesToCopyLeft { get; init; }
    public int FilesToCopyRight { get; init; }
    public int FilesToDelete { get; init; }
    public long BytesToCopyLeft { get; init; }
    public long BytesToCopyRight { get; init; }
}

/// <summary>
/// Conflict event args
/// </summary>
public class SyncConflictEventArgs : EventArgs
{
    public SyncFileEntry Entry { get; }
    public SyncFileAction ResolvedAction { get; set; } = SyncFileAction.Skip;
    
    public SyncConflictEventArgs(SyncFileEntry entry)
    {
        Entry = entry;
    }
}
