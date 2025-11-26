using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Interface for batch job operations - recording, playback, and scheduling.
/// </summary>
public interface IBatchJobService
{
    /// <summary>
    /// Starts recording operations for a new batch job.
    /// </summary>
    void StartRecording(string? jobName = null);
    
    /// <summary>
    /// Stops recording and returns the recorded batch job.
    /// </summary>
    BatchJob StopRecording();
    
    /// <summary>
    /// Records a single operation to the current batch job.
    /// </summary>
    void RecordOperation(BatchOperation operation);
    
    /// <summary>
    /// Whether recording is currently active.
    /// </summary>
    bool IsRecording { get; }
    
    /// <summary>
    /// Executes a batch job.
    /// </summary>
    Task<BatchJobResult> ExecuteAsync(BatchJob job, BatchExecutionOptions? options = null,
        IProgress<BatchJobProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves a batch job to file.
    /// </summary>
    Task SaveJobAsync(BatchJob job, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads a batch job from file.
    /// </summary>
    Task<BatchJob> LoadJobAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all saved batch jobs.
    /// </summary>
    Task<IReadOnlyList<BatchJobInfo>> GetSavedJobsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a batch job before execution.
    /// </summary>
    Task<BatchJobValidation> ValidateAsync(BatchJob job, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Schedules a batch job for later execution.
    /// </summary>
    Task<string> ScheduleJobAsync(BatchJob job, BatchSchedule schedule, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancels a scheduled job.
    /// </summary>
    Task CancelScheduledJobAsync(string scheduleId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all scheduled jobs.
    /// </summary>
    Task<IReadOnlyList<ScheduledJob>> GetScheduledJobsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A batch job containing a sequence of operations.
/// </summary>
public class BatchJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }
    public List<BatchOperation> Operations { get; init; } = new();
    public Dictionary<string, string> Variables { get; init; } = new();
    public BatchJobOptions Options { get; init; } = new();
}

/// <summary>
/// Single operation in a batch job.
/// </summary>
public class BatchOperation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public int Order { get; set; }
    public BatchOperationType Type { get; init; }
    public string? Description { get; set; }
    public Dictionary<string, string> Parameters { get; init; } = new();
    public bool ContinueOnError { get; init; } = false;
    public int RetryCount { get; init; } = 0;
    public TimeSpan? Timeout { get; init; }
    public string? Condition { get; init; }
}

/// <summary>
/// Types of batch operations.
/// </summary>
public enum BatchOperationType
{
    // File operations
    Copy,
    Move,
    Delete,
    Rename,
    CreateDirectory,
    DeleteDirectory,
    
    // Archive operations
    CreateArchive,
    ExtractArchive,
    
    // File manipulation
    Encode,
    Decode,
    Split,
    Combine,
    
    // Search operations
    FindFiles,
    FindDuplicates,
    
    // Checksum operations
    CalculateChecksum,
    VerifyChecksum,
    
    // System operations
    RunCommand,
    RunScript,
    
    // Control flow
    Delay,
    Condition,
    Loop,
    
    // Custom
    Custom
}

/// <summary>
/// Options for batch job execution.
/// </summary>
public class BatchJobOptions
{
    public bool StopOnFirstError { get; init; } = true;
    public int DefaultRetryCount { get; init; } = 0;
    public TimeSpan DefaultRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
    public bool LogOperations { get; init; } = true;
    public string? LogFilePath { get; init; }
    public bool DryRun { get; init; } = false;
}

/// <summary>
/// Options for batch execution.
/// </summary>
public class BatchExecutionOptions
{
    public bool DryRun { get; init; } = false;
    public bool Interactive { get; init; } = false;
    public Dictionary<string, string>? VariableOverrides { get; init; }
}

/// <summary>
/// Result of batch job execution.
/// </summary>
public class BatchJobResult
{
    public string JobId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public int TotalOperations { get; init; }
    public int SuccessfulOperations { get; init; }
    public int FailedOperations { get; init; }
    public int SkippedOperations { get; init; }
    public List<BatchOperationResult> OperationResults { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a single operation.
/// </summary>
public class BatchOperationResult
{
    public string OperationId { get; init; } = string.Empty;
    public BatchOperationType Type { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Output { get; init; } = new();
}

/// <summary>
/// Progress of batch job execution.
/// </summary>
public class BatchJobProgress
{
    public int CurrentOperation { get; init; }
    public int TotalOperations { get; init; }
    public BatchOperationType CurrentOperationType { get; init; }
    public string CurrentDescription { get; init; } = string.Empty;
    public double ProgressPercent => TotalOperations > 0 ? (double)CurrentOperation / TotalOperations * 100 : 0;
}

/// <summary>
/// Information about a saved batch job.
/// </summary>
public class BatchJobInfo
{
    public string FilePath { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public int OperationCount { get; init; }
}

/// <summary>
/// Validation result for a batch job.
/// </summary>
public class BatchJobValidation
{
    public bool IsValid { get; init; }
    public List<BatchValidationError> Errors { get; init; } = new();
    public List<BatchValidationWarning> Warnings { get; init; } = new();
}

/// <summary>
/// Validation error.
/// </summary>
public class BatchValidationError
{
    public int OperationIndex { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Validation warning.
/// </summary>
public class BatchValidationWarning
{
    public int OperationIndex { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Schedule for batch job execution.
/// </summary>
public class BatchSchedule
{
    public ScheduleType Type { get; init; }
    public DateTime? RunAt { get; init; }
    public TimeSpan? Interval { get; init; }
    public DayOfWeek[]? DaysOfWeek { get; init; }
    public TimeSpan? TimeOfDay { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int? MaxRuns { get; init; }
}

/// <summary>
/// Type of schedule.
/// </summary>
public enum ScheduleType
{
    Once,           // Run once at specified time
    Interval,       // Run at regular intervals
    Daily,          // Run daily at specified time
    Weekly,         // Run on specific days of week
    OnStartup       // Run when application starts
}

/// <summary>
/// Information about a scheduled job.
/// </summary>
public class ScheduledJob
{
    public string Id { get; init; } = string.Empty;
    public BatchJob Job { get; init; } = null!;
    public BatchSchedule Schedule { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastRunAt { get; init; }
    public DateTime? NextRunAt { get; init; }
    public int RunCount { get; init; }
    public bool IsActive { get; init; }
}
