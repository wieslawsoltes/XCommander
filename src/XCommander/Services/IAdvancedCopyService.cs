namespace XCommander.Services;

/// <summary>
/// Copy/move operation mode
/// </summary>
public enum CopyTransferMode
{
    Copy,
    Move
}

/// <summary>
/// Verification method for file transfers
/// </summary>
public enum VerificationMethod
{
    None,
    Crc32,
    MD5,
    SHA1,
    SHA256
}

/// <summary>
/// Conflict resolution strategy
/// </summary>
public enum ConflictResolution
{
    Ask,
    Skip,
    Overwrite,
    OverwriteIfNewer,
    OverwriteIfSizeDiffers,
    Rename,
    RenameWithNumber
}

/// <summary>
/// Transfer speed mode
/// </summary>
public enum TransferSpeedMode
{
    Maximum,
    Normal,
    Slow,          // For unstable connections
    VerySlow,      // For very slow media
    Throttled      // Custom speed limit
}

/// <summary>
/// Transfer queue priority
/// </summary>
public enum QueuePriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Copy transfer operation status
/// </summary>
public enum CopyTransferStatus
{
    Pending,
    Queued,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled,
    Verifying
}

/// <summary>
/// Single file copy transfer item
/// </summary>
public class CopyTransferItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public CopyTransferMode Mode { get; set; }
    public long Size { get; set; }
    public long BytesTransferred { get; set; }
    public CopyTransferStatus Status { get; set; } = CopyTransferStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? SourceHash { get; set; }
    public string? DestinationHash { get; set; }
    public bool VerificationPassed { get; set; }
}

/// <summary>
/// Transfer operation containing multiple items
/// </summary>
public class CopyTransferOperation
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public CopyTransferMode Mode { get; init; }
    public List<CopyTransferItem> Items { get; init; } = new();
    public CopyTransferStatus Status { get; set; } = CopyTransferStatus.Pending;
    public DateTime Created { get; init; } = DateTime.Now;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public QueuePriority Priority { get; set; } = QueuePriority.Normal;
    
    // Settings
    public VerificationMethod Verification { get; init; } = VerificationMethod.None;
    public ConflictResolution ConflictHandling { get; init; } = ConflictResolution.Ask;
    public TransferSpeedMode SpeedMode { get; init; } = TransferSpeedMode.Maximum;
    public long? SpeedLimitBytesPerSecond { get; init; }
    public bool PreserveTimestamps { get; init; } = true;
    public bool PreserveAttributes { get; init; } = true;
    public bool PreservePermissions { get; init; }
    public bool CopyNtfsStreams { get; init; }
    public bool FollowSymlinks { get; init; } = true;
    public bool DeleteSourceAfterVerification { get; init; }
    
    // Statistics
    public long TotalBytes => Items.Sum(i => i.Size);
    public long BytesTransferred => Items.Sum(i => i.BytesTransferred);
    public int TotalFiles => Items.Count;
    public int CompletedFiles => Items.Count(i => i.Status == CopyTransferStatus.Completed);
    public int FailedFiles => Items.Count(i => i.Status == CopyTransferStatus.Failed);
    public double Progress => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
}

/// <summary>
/// Transfer progress event args
/// </summary>
public class CopyTransferProgressEventArgs : EventArgs
{
    public CopyTransferOperation Operation { get; init; } = null!;
    public CopyTransferItem? CurrentItem { get; init; }
    public long BytesPerSecond { get; init; }
    public TimeSpan EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Conflict event args for asking user
/// </summary>
public class CopyTransferConflictEventArgs : EventArgs
{
    public CopyTransferItem Item { get; init; } = null!;
    public FileInfo SourceFile { get; init; } = null!;
    public FileInfo DestinationFile { get; init; } = null!;
    public ConflictResolution Resolution { get; set; } = ConflictResolution.Ask;
    public bool ApplyToAll { get; set; }
}

/// <summary>
/// Service for advanced copy/move operations with verification and queue
/// </summary>
public interface IAdvancedCopyService
{
    /// <summary>
    /// Event raised on transfer progress
    /// </summary>
    event EventHandler<CopyTransferProgressEventArgs>? Progress;
    
    /// <summary>
    /// Event raised when a conflict occurs (if ConflictResolution is Ask)
    /// </summary>
    event EventHandler<CopyTransferConflictEventArgs>? ConflictOccurred;
    
    /// <summary>
    /// Event raised when operation completes
    /// </summary>
    event EventHandler<CopyTransferOperation>? OperationCompleted;
    
    // Queue management
    
    /// <summary>
    /// Get all queued operations
    /// </summary>
    IReadOnlyList<CopyTransferOperation> GetQueue();
    
    /// <summary>
    /// Get currently running operation
    /// </summary>
    CopyTransferOperation? GetCurrentOperation();
    
    /// <summary>
    /// Add operation to queue
    /// </summary>
    Task<CopyTransferOperation> QueueOperationAsync(CopyTransferOperation operation);
    
    /// <summary>
    /// Remove operation from queue
    /// </summary>
    Task RemoveFromQueueAsync(string operationId);
    
    /// <summary>
    /// Clear completed operations from queue
    /// </summary>
    Task ClearCompletedAsync();
    
    /// <summary>
    /// Change operation priority
    /// </summary>
    Task SetPriorityAsync(string operationId, QueuePriority priority);
    
    /// <summary>
    /// Move operation up in queue
    /// </summary>
    Task MoveUpInQueueAsync(string operationId);
    
    /// <summary>
    /// Move operation down in queue
    /// </summary>
    Task MoveDownInQueueAsync(string operationId);
    
    // Operation control
    
    /// <summary>
    /// Start processing queue
    /// </summary>
    Task StartQueueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause current operation
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// Resume paused operation
    /// </summary>
    Task ResumeAsync();
    
    /// <summary>
    /// Cancel current operation
    /// </summary>
    Task CancelCurrentAsync();
    
    /// <summary>
    /// Cancel all operations
    /// </summary>
    Task CancelAllAsync();
    
    // Direct operations (bypass queue)
    
    /// <summary>
    /// Copy files immediately
    /// </summary>
    Task<CopyTransferOperation> CopyAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.None,
        ConflictResolution conflictHandling = ConflictResolution.Ask,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move files immediately
    /// </summary>
    Task<CopyTransferOperation> MoveAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.None,
        ConflictResolution conflictHandling = ConflictResolution.Ask,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy with full verification
    /// </summary>
    Task<CopyTransferOperation> CopyWithVerificationAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.MD5,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);
    
    // Verification
    
    /// <summary>
    /// Verify a completed transfer
    /// </summary>
    Task<bool> VerifyTransferAsync(CopyTransferItem item, VerificationMethod method, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify entire operation
    /// </summary>
    Task<IReadOnlyList<CopyTransferItem>> VerifyOperationAsync(CopyTransferOperation operation, VerificationMethod method, CancellationToken cancellationToken = default);
    
    // Retry
    
    /// <summary>
    /// Retry failed items in operation
    /// </summary>
    Task RetryFailedAsync(string operationId);
    
    /// <summary>
    /// Retry specific item
    /// </summary>
    Task RetryItemAsync(string operationId, string itemId);
    
    // Statistics
    
    /// <summary>
    /// Get current transfer speed
    /// </summary>
    long GetCurrentSpeed();
    
    /// <summary>
    /// Get estimated time remaining
    /// </summary>
    TimeSpan GetEstimatedTimeRemaining();
    
    /// <summary>
    /// Get transfer history
    /// </summary>
    Task<IReadOnlyList<CopyTransferOperation>> GetHistoryAsync(int maxCount = 50);
}
