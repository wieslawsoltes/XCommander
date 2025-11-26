using System.Runtime.CompilerServices;

namespace XCommander.Services;

/// <summary>
/// Service for application logging with structured output.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Log a debug message.
    /// </summary>
    void Debug(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null);
    
    /// <summary>
    /// Log an informational message.
    /// </summary>
    void Info(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null);
    
    /// <summary>
    /// Log a warning message.
    /// </summary>
    void Warning(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null);
    
    /// <summary>
    /// Log an error message.
    /// </summary>
    void Error(string message, Exception? exception = null, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null);
    
    /// <summary>
    /// Log an error for a file operation that was skipped (e.g., access denied).
    /// </summary>
    void SkippedFile(string path, string reason, [CallerMemberName] string? caller = null);
    
    /// <summary>
    /// Get recent log entries.
    /// </summary>
    IReadOnlyList<LogEntry> GetRecentEntries(int count = 100);
    
    /// <summary>
    /// Clear all log entries.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Export logs to file.
    /// </summary>
    Task ExportAsync(string filePath);
    
    /// <summary>
    /// Event raised when a new log entry is added.
    /// </summary>
    event EventHandler<LogEntry>? LogEntryAdded;
}

/// <summary>
/// Represents a log entry.
/// </summary>
public record LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public LogLevel Level { get; init; }
    public required string Message { get; init; }
    public string? Caller { get; init; }
    public string? File { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? StackTrace { get; init; }
}

/// <summary>
/// Log severity levels.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Skipped
}
