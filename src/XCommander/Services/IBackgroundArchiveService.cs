using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Archive operation type
/// </summary>
public enum BackgroundArchiveOperationType
{
    Create,
    Extract,
    Add,
    Delete,
    Test,
    Repair,
    Convert
}

/// <summary>
/// Background archive operation status
/// </summary>
public enum BackgroundArchiveStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Background archive operation
/// </summary>
public record BackgroundArchiveOperation
{
    /// <summary>Unique operation ID</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Operation type</summary>
    public BackgroundArchiveOperationType OperationType { get; init; }
    
    /// <summary>Archive path</summary>
    public string ArchivePath { get; init; } = string.Empty;
    
    /// <summary>Source path(s) for create/add</summary>
    public IReadOnlyList<string> SourcePaths { get; init; } = Array.Empty<string>();
    
    /// <summary>Destination path for extract</summary>
    public string? DestinationPath { get; init; }
    
    /// <summary>Compression level</summary>
    public int CompressionLevel { get; init; } = -1;
    
    /// <summary>Current status</summary>
    public BackgroundArchiveStatus Status { get; set; }
    
    /// <summary>Progress percentage</summary>
    public double ProgressPercent { get; set; }
    
    /// <summary>Current file being processed</summary>
    public string CurrentFile { get; set; } = string.Empty;
    
    /// <summary>Files processed</summary>
    public int FilesProcessed { get; set; }
    
    /// <summary>Total files</summary>
    public int TotalFiles { get; set; }
    
    /// <summary>Bytes processed</summary>
    public long BytesProcessed { get; set; }
    
    /// <summary>Total bytes</summary>
    public long TotalBytes { get; set; }
    
    /// <summary>Started time</summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>Completed time</summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Priority (higher runs first)</summary>
    public int Priority { get; init; }
}

/// <summary>
/// Progress update for background archive operation
/// </summary>
public record BackgroundArchiveProgress
{
    public string OperationId { get; init; } = string.Empty;
    public BackgroundArchiveStatus Status { get; init; }
    public double ProgressPercent { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
}

/// <summary>
/// Service for background archive operations.
/// TC equivalent: Background packing of large archives.
/// </summary>
public interface IBackgroundArchiveService
{
    /// <summary>
    /// Queue an archive creation operation
    /// </summary>
    Task<string> QueueCreateAsync(
        string archivePath,
        IEnumerable<string> sourcePaths,
        int compressionLevel = -1,
        int priority = 0);
    
    /// <summary>
    /// Queue an archive extraction operation
    /// </summary>
    Task<string> QueueExtractAsync(
        string archivePath,
        string destinationPath,
        int priority = 0);
    
    /// <summary>
    /// Queue add to archive operation
    /// </summary>
    Task<string> QueueAddAsync(
        string archivePath,
        IEnumerable<string> sourcePaths,
        int compressionLevel = -1,
        int priority = 0);
    
    /// <summary>
    /// Queue archive test operation
    /// </summary>
    Task<string> QueueTestAsync(string archivePath, int priority = 0);
    
    /// <summary>
    /// Queue archive convert operation
    /// </summary>
    Task<string> QueueConvertAsync(
        string sourceArchive,
        string destinationArchive,
        int compressionLevel = -1,
        int priority = 0);
    
    /// <summary>
    /// Get operation by ID
    /// </summary>
    BackgroundArchiveOperation? GetOperation(string operationId);
    
    /// <summary>
    /// Get all operations
    /// </summary>
    IReadOnlyList<BackgroundArchiveOperation> GetAllOperations();
    
    /// <summary>
    /// Get queued operations
    /// </summary>
    IReadOnlyList<BackgroundArchiveOperation> GetQueuedOperations();
    
    /// <summary>
    /// Get running operations
    /// </summary>
    IReadOnlyList<BackgroundArchiveOperation> GetRunningOperations();
    
    /// <summary>
    /// Get completed operations
    /// </summary>
    IReadOnlyList<BackgroundArchiveOperation> GetCompletedOperations();
    
    /// <summary>
    /// Pause an operation
    /// </summary>
    Task<bool> PauseOperationAsync(string operationId);
    
    /// <summary>
    /// Resume a paused operation
    /// </summary>
    Task<bool> ResumeOperationAsync(string operationId);
    
    /// <summary>
    /// Cancel an operation
    /// </summary>
    Task<bool> CancelOperationAsync(string operationId);
    
    /// <summary>
    /// Remove completed/failed operation from list
    /// </summary>
    bool RemoveOperation(string operationId);
    
    /// <summary>
    /// Clear all completed operations
    /// </summary>
    void ClearCompletedOperations();
    
    /// <summary>
    /// Set maximum concurrent operations
    /// </summary>
    void SetMaxConcurrent(int max);
    
    /// <summary>
    /// Get maximum concurrent operations
    /// </summary>
    int GetMaxConcurrent();
    
    /// <summary>
    /// Event raised when operation status changes
    /// </summary>
    event EventHandler<BackgroundArchiveProgress>? OperationProgress;
    
    /// <summary>
    /// Event raised when operation completes
    /// </summary>
    event EventHandler<BackgroundArchiveOperation>? OperationCompleted;
    
    /// <summary>
    /// Event raised when operation fails
    /// </summary>
    event EventHandler<BackgroundArchiveOperation>? OperationFailed;
}
