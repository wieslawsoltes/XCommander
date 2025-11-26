using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Archive sync comparison result for a single file
/// </summary>
public record ArchiveSyncItem
{
    /// <summary>Relative path within archive/directory</summary>
    public string RelativePath { get; init; } = string.Empty;
    
    /// <summary>True if file exists in source</summary>
    public bool ExistsInSource { get; init; }
    
    /// <summary>True if file exists in target</summary>
    public bool ExistsInTarget { get; init; }
    
    /// <summary>Size in source (if exists)</summary>
    public long? SourceSize { get; init; }
    
    /// <summary>Size in target (if exists)</summary>
    public long? TargetSize { get; init; }
    
    /// <summary>Modified date in source</summary>
    public DateTime? SourceModified { get; init; }
    
    /// <summary>Modified date in target</summary>
    public DateTime? TargetModified { get; init; }
    
    /// <summary>Suggested action</summary>
    public ArchiveSyncAction SuggestedAction { get; init; }
    
    /// <summary>User-selected action (may differ from suggested)</summary>
    public ArchiveSyncAction SelectedAction { get; set; }
    
    /// <summary>Is this item a directory</summary>
    public bool IsDirectory { get; init; }
}

/// <summary>
/// Sync action for archive/directory sync
/// </summary>
public enum ArchiveSyncAction
{
    /// <summary>No action needed</summary>
    None,
    
    /// <summary>Add to archive (file only in directory)</summary>
    AddToArchive,
    
    /// <summary>Extract from archive (file only in archive)</summary>
    ExtractFromArchive,
    
    /// <summary>Update archive (directory file is newer)</summary>
    UpdateArchive,
    
    /// <summary>Update directory (archive file is newer)</summary>
    UpdateDirectory,
    
    /// <summary>Delete from archive</summary>
    DeleteFromArchive,
    
    /// <summary>Delete from directory</summary>
    DeleteFromDirectory,
    
    /// <summary>Skip this file</summary>
    Skip
}

/// <summary>
/// Archive sync options
/// </summary>
public record ArchiveSyncOptions
{
    /// <summary>Compare by content (hash) instead of date/size</summary>
    public bool CompareByContent { get; init; }
    
    /// <summary>Include subdirectories</summary>
    public bool Recursive { get; init; } = true;
    
    /// <summary>Sync both directions (bidirectional)</summary>
    public bool Bidirectional { get; init; }
    
    /// <summary>Delete files from archive that don't exist in directory</summary>
    public bool DeleteOrphansFromArchive { get; init; }
    
    /// <summary>Delete files from directory that don't exist in archive</summary>
    public bool DeleteOrphansFromDirectory { get; init; }
    
    /// <summary>File pattern filter (e.g., *.txt;*.doc)</summary>
    public string? FileFilter { get; init; }
    
    /// <summary>Exclude pattern</summary>
    public string? ExcludeFilter { get; init; }
    
    /// <summary>Ignore timestamp when comparing</summary>
    public bool IgnoreTimestamp { get; init; }
    
    /// <summary>Time tolerance for comparison (in seconds)</summary>
    public int TimeTolerance { get; init; } = 2;
    
    /// <summary>Compression level for new archive entries (0-9)</summary>
    public int CompressionLevel { get; init; } = 5;
}

/// <summary>
/// Archive sync comparison result
/// </summary>
public record ArchiveSyncCompareResult
{
    /// <summary>All compared items</summary>
    public IReadOnlyList<ArchiveSyncItem> Items { get; init; } = Array.Empty<ArchiveSyncItem>();
    
    /// <summary>Files only in source (directory)</summary>
    public int SourceOnlyCount { get; init; }
    
    /// <summary>Files only in target (archive)</summary>
    public int TargetOnlyCount { get; init; }
    
    /// <summary>Files that differ</summary>
    public int DifferentCount { get; init; }
    
    /// <summary>Files that are identical</summary>
    public int IdenticalCount { get; init; }
    
    /// <summary>Total source size</summary>
    public long TotalSourceSize { get; init; }
    
    /// <summary>Total target size</summary>
    public long TotalTargetSize { get; init; }
}

/// <summary>
/// Archive sync execution result
/// </summary>
public record ArchiveSyncResult
{
    /// <summary>Operation succeeded</summary>
    public bool Success { get; init; }
    
    /// <summary>Files added to archive</summary>
    public int FilesAdded { get; init; }
    
    /// <summary>Files updated in archive</summary>
    public int FilesUpdated { get; init; }
    
    /// <summary>Files deleted from archive</summary>
    public int FilesDeleted { get; init; }
    
    /// <summary>Files extracted to directory</summary>
    public int FilesExtracted { get; init; }
    
    /// <summary>Bytes processed</summary>
    public long BytesProcessed { get; init; }
    
    /// <summary>Duration of operation</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>Errors that occurred</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Progress for archive sync operations
/// </summary>
public record ArchiveSyncProgress
{
    /// <summary>Current file being processed</summary>
    public string CurrentFile { get; init; } = string.Empty;
    
    /// <summary>Current operation</summary>
    public ArchiveSyncAction CurrentAction { get; init; }
    
    /// <summary>Total files to process</summary>
    public int TotalFiles { get; init; }
    
    /// <summary>Files processed so far</summary>
    public int ProcessedFiles { get; init; }
    
    /// <summary>Total bytes to process</summary>
    public long TotalBytes { get; init; }
    
    /// <summary>Bytes processed so far</summary>
    public long ProcessedBytes { get; init; }
    
    /// <summary>Overall percentage (0-100)</summary>
    public double PercentComplete => TotalFiles > 0 ? (ProcessedFiles * 100.0 / TotalFiles) : 0;
}

/// <summary>
/// Service for synchronizing directories with archive files.
/// TC equivalent: Synchronize directory with ZIP archive.
/// </summary>
public interface IArchiveSyncService
{
    /// <summary>
    /// Compare directory with archive to determine sync actions
    /// </summary>
    Task<ArchiveSyncCompareResult> CompareAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute synchronization based on comparison result
    /// </summary>
    Task<ArchiveSyncResult> SynchronizeAsync(
        string directoryPath,
        string archivePath,
        IReadOnlyList<ArchiveSyncItem> items,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Quick sync - compare and execute in one step
    /// </summary>
    Task<ArchiveSyncResult> QuickSyncAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update archive with changed files only
    /// </summary>
    Task<ArchiveSyncResult> UpdateArchiveAsync(
        string directoryPath,
        string archivePath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restore directory from archive (extract changed/missing files)
    /// </summary>
    Task<ArchiveSyncResult> RestoreFromArchiveAsync(
        string archivePath,
        string directoryPath,
        ArchiveSyncOptions? options = null,
        IProgress<ArchiveSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if archive type supports modification
    /// </summary>
    bool CanModifyArchive(string archivePath);
    
    /// <summary>
    /// Get supported archive extensions for sync
    /// </summary>
    IReadOnlyList<string> GetSupportedExtensions();
    
    /// <summary>
    /// Verify archive integrity
    /// </summary>
    Task<bool> VerifyArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when sync operation completes
    /// </summary>
    event EventHandler<ArchiveSyncResult>? SyncCompleted;
}
