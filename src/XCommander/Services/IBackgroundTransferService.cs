// IBackgroundTransferService.cs - TC-style background file transfer queue
// Queue and manage file operations with priority, scheduling, and throttling

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Types of background transfer operations
/// </summary>
public enum TransferOperationType
{
    Copy,
    Move,
    Delete,
    Upload,
    Download,
    Sync,
    Archive,
    Extract
}

/// <summary>
/// Status of a background transfer operation
/// </summary>
public enum BackgroundTransferStatus
{
    Pending,
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled,
    Waiting  // Waiting for user input (overwrite dialog, etc.)
}

/// <summary>
/// Priority levels for transfer operations
/// </summary>
public enum TransferPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Immediate = 3
}

/// <summary>
/// Represents a single file transfer operation
/// </summary>
public record TransferOperation
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public TransferOperationType Type { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    public BackgroundTransferStatus Status { get; init; } = BackgroundTransferStatus.Pending;
    public TransferPriority Priority { get; init; } = TransferPriority.Normal;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? ScheduledFor { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string? CurrentFile { get; init; }
    public double SpeedBytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public TransferOptions Options { get; init; } = new();
}

/// <summary>
/// Options for transfer operations
/// </summary>
public record TransferOptions
{
    public bool OverwriteExisting { get; init; }
    public bool SkipExisting { get; init; }
    public bool VerifyAfterTransfer { get; init; }
    public bool PreserveAttributes { get; init; } = true;
    public bool PreserveTimestamps { get; init; } = true;
    public bool DeleteAfterTransfer { get; init; } // For move operations
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public long? SpeedLimit { get; init; } // Bytes per second, null = unlimited
    public bool UseRecycleBin { get; init; } = true;
    public bool ContinueOnError { get; init; }
    public string? ConflictResolution { get; init; }
}

/// <summary>
/// Transfer queue statistics
/// </summary>
public record TransferQueueStatistics
{
    public int TotalOperations { get; init; }
    public int PendingOperations { get; init; }
    public int RunningOperations { get; init; }
    public int CompletedOperations { get; init; }
    public int FailedOperations { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public double AverageSpeedBytesPerSecond { get; init; }
    public TimeSpan? EstimatedTotalTimeRemaining { get; init; }
    public int ConcurrentTransfers { get; init; }
}

/// <summary>
/// Transfer event arguments
/// </summary>
public class TransferEventArgs : EventArgs
{
    public TransferOperation Operation { get; }
    public BackgroundTransferStatus PreviousStatus { get; }
    
    public TransferEventArgs(TransferOperation operation, BackgroundTransferStatus previousStatus)
    {
        Operation = operation;
        PreviousStatus = previousStatus;
    }
}

/// <summary>
/// Transfer progress event arguments
/// </summary>
public class TransferProgressEventArgs : EventArgs
{
    public TransferOperation Operation { get; }
    public long BytesTransferred { get; }
    public string? CurrentFile { get; }
    
    public TransferProgressEventArgs(TransferOperation operation, long bytesTransferred, string? currentFile)
    {
        Operation = operation;
        BytesTransferred = bytesTransferred;
        CurrentFile = currentFile;
    }
}

/// <summary>
/// Service for managing background file transfer operations
/// </summary>
public interface IBackgroundTransferService
{
    /// <summary>
    /// Queue a new transfer operation
    /// </summary>
    Task<TransferOperation> QueueOperationAsync(
        TransferOperationType type,
        string sourcePath,
        string? targetPath,
        IReadOnlyList<string>? files = null,
        TransferOptions? options = null,
        TransferPriority priority = TransferPriority.Normal,
        DateTime? scheduledFor = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all operations in the queue
    /// </summary>
    IReadOnlyList<TransferOperation> GetAllOperations();
    
    /// <summary>
    /// Get operations by status
    /// </summary>
    IReadOnlyList<TransferOperation> GetOperationsByStatus(params BackgroundTransferStatus[] statuses);
    
    /// <summary>
    /// Get operation by ID
    /// </summary>
    TransferOperation? GetOperation(string operationId);
    
    /// <summary>
    /// Pause an operation
    /// </summary>
    Task<bool> PauseOperationAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resume a paused operation
    /// </summary>
    Task<bool> ResumeOperationAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancel an operation
    /// </summary>
    Task<bool> CancelOperationAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retry a failed operation
    /// </summary>
    Task<bool> RetryOperationAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove an operation from the queue
    /// </summary>
    Task<bool> RemoveOperationAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear completed operations
    /// </summary>
    Task ClearCompletedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Change operation priority
    /// </summary>
    Task<bool> SetPriorityAsync(string operationId, TransferPriority priority, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move operation up in queue
    /// </summary>
    Task<bool> MoveUpAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move operation down in queue
    /// </summary>
    Task<bool> MoveDownAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move operation to top of queue
    /// </summary>
    Task<bool> MoveToTopAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move operation to bottom of queue
    /// </summary>
    Task<bool> MoveToBottomAsync(string operationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause all operations
    /// </summary>
    Task PauseAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resume all paused operations
    /// </summary>
    Task ResumeAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get queue statistics
    /// </summary>
    TransferQueueStatistics GetStatistics();
    
    /// <summary>
    /// Set global speed limit
    /// </summary>
    void SetGlobalSpeedLimit(long? bytesPerSecond);
    
    /// <summary>
    /// Get global speed limit
    /// </summary>
    long? GetGlobalSpeedLimit();
    
    /// <summary>
    /// Set maximum concurrent transfers
    /// </summary>
    void SetMaxConcurrentTransfers(int count);
    
    /// <summary>
    /// Get maximum concurrent transfers
    /// </summary>
    int GetMaxConcurrentTransfers();
    
    /// <summary>
    /// Start/resume processing the queue
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop processing the queue (pauses all operations)
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if queue is processing
    /// </summary>
    bool IsRunning { get; }
    
    // Events
    event EventHandler<TransferEventArgs>? OperationStarted;
    event EventHandler<TransferEventArgs>? OperationCompleted;
    event EventHandler<TransferEventArgs>? OperationFailed;
    event EventHandler<TransferEventArgs>? OperationPaused;
    event EventHandler<TransferEventArgs>? OperationCancelled;
    event EventHandler<TransferProgressEventArgs>? ProgressChanged;
    event EventHandler<EventArgs>? QueueEmpty;
}
