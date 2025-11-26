// IOperationLogService.cs - File Operation Logging
// Provides logging of all file operations for audit and troubleshooting

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for logging file operations.
/// </summary>
public interface IOperationLogService
{
    /// <summary>
    /// Logs a file operation.
    /// </summary>
    Task LogOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets recent log entries.
    /// </summary>
    Task<IReadOnlyList<OperationLogEntry>> GetRecentEntriesAsync(int count = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets log entries for a specific date range.
    /// </summary>
    Task<IReadOnlyList<OperationLogEntry>> GetEntriesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets log entries for a specific operation type.
    /// </summary>
    Task<IReadOnlyList<OperationLogEntry>> GetEntriesByTypeAsync(OperationType type, int count = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches log entries.
    /// </summary>
    Task<IReadOnlyList<OperationLogEntry>> SearchEntriesAsync(OperationLogSearchCriteria criteria, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears old log entries.
    /// </summary>
    Task ClearOldEntriesAsync(TimeSpan retention, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports log to file.
    /// </summary>
    Task ExportLogAsync(string filePath, DateTime from, DateTime to, LogExportFormat format, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets or sets whether logging is enabled.
    /// </summary>
    bool IsLoggingEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets the log file path.
    /// </summary>
    string? LogFilePath { get; set; }
}

/// <summary>
/// Type of file operation.
/// </summary>
public enum OperationType
{
    Copy,
    Move,
    Delete,
    Rename,
    CreateDirectory,
    CreateFile,
    Archive,
    Extract,
    FtpUpload,
    FtpDownload,
    Synchronize,
    Compare,
    Checksum,
    Split,
    Combine,
    Encode,
    Decode,
    ChangeAttributes,
    CreateLink,
    SecureDelete,
    Other
}

/// <summary>
/// Status of an operation.
/// </summary>
public enum OperationStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Skipped,
    PartialSuccess
}

/// <summary>
/// A log entry for a file operation.
/// </summary>
public record OperationLogEntry
{
    /// <summary>
    /// Unique identifier for this operation.
    /// </summary>
    public Guid OperationId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Type of operation.
    /// </summary>
    public required OperationType Type { get; init; }
    
    /// <summary>
    /// Status of the operation.
    /// </summary>
    public required OperationStatus Status { get; init; }
    
    /// <summary>
    /// When the operation started.
    /// </summary>
    public required DateTime StartTime { get; init; }
    
    /// <summary>
    /// When the operation ended.
    /// </summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>
    /// Duration of the operation.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    /// <summary>
    /// Source path(s).
    /// </summary>
    public required IReadOnlyList<string> SourcePaths { get; init; }
    
    /// <summary>
    /// Destination path.
    /// </summary>
    public string? DestinationPath { get; init; }
    
    /// <summary>
    /// Total bytes processed.
    /// </summary>
    public long BytesProcessed { get; init; }
    
    /// <summary>
    /// Total files processed.
    /// </summary>
    public int FilesProcessed { get; init; }
    
    /// <summary>
    /// Total folders processed.
    /// </summary>
    public int FoldersProcessed { get; init; }
    
    /// <summary>
    /// Number of items skipped.
    /// </summary>
    public int ItemsSkipped { get; init; }
    
    /// <summary>
    /// Number of items that failed.
    /// </summary>
    public int ItemsFailed { get; init; }
    
    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Additional details.
    /// </summary>
    public string? Details { get; init; }
    
    /// <summary>
    /// Username who performed the operation.
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Machine name.
    /// </summary>
    public string? MachineName { get; init; }
}

/// <summary>
/// Search criteria for log entries.
/// </summary>
public class OperationLogSearchCriteria
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public OperationType? Type { get; set; }
    public OperationStatus? Status { get; set; }
    public string? PathContains { get; set; }
    public long? MinBytes { get; set; }
    public int MaxResults { get; set; } = 1000;
}

/// <summary>
/// Format for log export.
/// </summary>
public enum LogExportFormat
{
    Text,
    Csv,
    Json,
    Xml,
    Html
}
