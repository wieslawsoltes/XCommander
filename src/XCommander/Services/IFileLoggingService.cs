using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Log entry severity
/// </summary>
public enum FileOperationSeverity
{
    Info,
    Warning,
    Error,
    Success
}

/// <summary>
/// Type of file operation being logged
/// </summary>
public enum FileOperationType
{
    Copy,
    Move,
    Delete,
    Rename,
    CreateDirectory,
    CreateFile,
    Extract,
    Pack,
    Upload,
    Download,
    Sync,
    Compare,
    SetAttributes,
    SetPermissions,
    CreateLink,
    Checksum,
    Split,
    Combine,
    Encode,
    Decode,
    Encrypt,
    Decrypt,
    Search,
    MultiRename,
    Other
}

/// <summary>
/// A single file operation log entry
/// </summary>
public record FileOperationLogEntry
{
    /// <summary>Unique ID for this entry</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>Timestamp when operation occurred</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>Type of operation</summary>
    public FileOperationType OperationType { get; init; }
    
    /// <summary>Severity of the log entry</summary>
    public FileOperationSeverity Severity { get; init; }
    
    /// <summary>Source path (if applicable)</summary>
    public string? SourcePath { get; init; }
    
    /// <summary>Destination path (if applicable)</summary>
    public string? DestinationPath { get; init; }
    
    /// <summary>File size in bytes (if applicable)</summary>
    public long? FileSize { get; init; }
    
    /// <summary>Operation duration</summary>
    public TimeSpan? Duration { get; init; }
    
    /// <summary>Descriptive message</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>Error message if operation failed</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Exception details if available</summary>
    public string? ExceptionDetails { get; init; }
    
    /// <summary>User who performed the operation</summary>
    public string? UserName { get; init; }
    
    /// <summary>Machine name where operation occurred</summary>
    public string? MachineName { get; init; }
    
    /// <summary>Additional custom data</summary>
    public Dictionary<string, string>? CustomData { get; init; }
}

/// <summary>
/// Log file configuration
/// </summary>
public record FileLoggingConfig
{
    /// <summary>Enable file logging</summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>Directory to store log files</summary>
    public string LogDirectory { get; init; } = string.Empty;
    
    /// <summary>Log file name pattern (supports date placeholders)</summary>
    public string FileNamePattern { get; init; } = "xcommander-{date:yyyy-MM-dd}.log";
    
    /// <summary>Maximum log file size in bytes before rotation</summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024; // 10MB
    
    /// <summary>Maximum number of log files to keep</summary>
    public int MaxFileCount { get; init; } = 30;
    
    /// <summary>Operation types to log</summary>
    public HashSet<FileOperationType> OperationFilter { get; init; } = new();
    
    /// <summary>Minimum severity to log</summary>
    public FileOperationSeverity MinimumSeverity { get; init; } = FileOperationSeverity.Info;
    
    /// <summary>Include detailed exception info</summary>
    public bool IncludeExceptionDetails { get; init; } = true;
    
    /// <summary>Log format (Text, JSON, CSV)</summary>
    public string Format { get; init; } = "Text";
    
    /// <summary>Include machine/user info in logs</summary>
    public bool IncludeSystemInfo { get; init; } = true;
    
    /// <summary>Separate log files per operation type</summary>
    public bool SeparateLogsByType { get; init; } = false;
}

/// <summary>
/// Log query filter options
/// </summary>
public record LogQueryOptions
{
    /// <summary>Start date filter</summary>
    public DateTime? StartDate { get; init; }
    
    /// <summary>End date filter</summary>
    public DateTime? EndDate { get; init; }
    
    /// <summary>Operation types to include</summary>
    public HashSet<FileOperationType>? OperationTypes { get; init; }
    
    /// <summary>Minimum severity to include</summary>
    public FileOperationSeverity? MinimumSeverity { get; init; }
    
    /// <summary>Search text in path or message</summary>
    public string? SearchText { get; init; }
    
    /// <summary>Maximum results to return</summary>
    public int MaxResults { get; init; } = 1000;
    
    /// <summary>Skip first N results (for pagination)</summary>
    public int Skip { get; init; } = 0;
    
    /// <summary>Sort order (true = newest first)</summary>
    public bool NewestFirst { get; init; } = true;
}

/// <summary>
/// Service for logging all file operations to files.
/// TC equivalent: File operation logging feature.
/// </summary>
public interface IFileLoggingService
{
    /// <summary>
    /// Log a file operation
    /// </summary>
    Task LogOperationAsync(FileOperationLogEntry entry);
    
    /// <summary>
    /// Log a successful operation
    /// </summary>
    Task LogSuccessAsync(
        FileOperationType operationType,
        string sourcePath,
        string? destinationPath = null,
        long? fileSize = null,
        TimeSpan? duration = null,
        string? message = null);
    
    /// <summary>
    /// Log a failed operation
    /// </summary>
    Task LogErrorAsync(
        FileOperationType operationType,
        string sourcePath,
        string? destinationPath,
        Exception exception,
        string? message = null);
    
    /// <summary>
    /// Log a warning
    /// </summary>
    Task LogWarningAsync(
        FileOperationType operationType,
        string message,
        string? path = null);
    
    /// <summary>
    /// Query log entries
    /// </summary>
    Task<IReadOnlyList<FileOperationLogEntry>> QueryLogsAsync(
        LogQueryOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get log entries for today
    /// </summary>
    Task<IReadOnlyList<FileOperationLogEntry>> GetTodayLogsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get log entry by ID
    /// </summary>
    Task<FileOperationLogEntry?> GetLogEntryAsync(Guid id);
    
    /// <summary>
    /// Delete log entries matching filter
    /// </summary>
    Task<int> DeleteLogsAsync(LogQueryOptions filter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete logs older than specified date
    /// </summary>
    Task<int> DeleteLogsOlderThanAsync(DateTime date, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export logs to file
    /// </summary>
    Task ExportLogsAsync(
        string filePath,
        LogQueryOptions? filter = null,
        string format = "Text",
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get log file paths
    /// </summary>
    IReadOnlyList<string> GetLogFilePaths();
    
    /// <summary>
    /// Rotate log files (delete old, compress, etc.)
    /// </summary>
    Task RotateLogsAsync();
    
    /// <summary>
    /// Get current configuration
    /// </summary>
    FileLoggingConfig GetConfiguration();
    
    /// <summary>
    /// Update configuration
    /// </summary>
    Task SetConfigurationAsync(FileLoggingConfig config);
    
    /// <summary>
    /// Get log statistics
    /// </summary>
    Task<LogStatistics> GetStatisticsAsync(DateTime? since = null);
    
    /// <summary>
    /// Event raised when a new log entry is added
    /// </summary>
    event EventHandler<FileOperationLogEntry>? LogEntryAdded;
}

/// <summary>
/// Log statistics
/// </summary>
public record LogStatistics
{
    /// <summary>Total number of entries</summary>
    public long TotalEntries { get; init; }
    
    /// <summary>Entries by operation type</summary>
    public Dictionary<FileOperationType, long> EntriesByType { get; init; } = new();
    
    /// <summary>Entries by severity</summary>
    public Dictionary<FileOperationSeverity, long> EntriesBySeverity { get; init; } = new();
    
    /// <summary>Total bytes processed</summary>
    public long TotalBytesProcessed { get; init; }
    
    /// <summary>Earliest log date</summary>
    public DateTime? EarliestEntry { get; init; }
    
    /// <summary>Latest log date</summary>
    public DateTime? LatestEntry { get; init; }
    
    /// <summary>Log files size on disk</summary>
    public long LogFilesTotalSize { get; init; }
}
