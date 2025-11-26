using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace XCommander.Services;

/// <summary>
/// Implementation of ILoggingService with in-memory buffer and file export.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _maxEntries;
    private readonly bool _enableDebug;
    
    public event EventHandler<LogEntry>? LogEntryAdded;
    
    public LoggingService(int maxEntries = 10000, bool enableDebug = false)
    {
        _maxEntries = maxEntries;
        _enableDebug = enableDebug;
    }
    
    public void Debug(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        if (!_enableDebug) return;
        
        AddEntry(new LogEntry
        {
            Level = LogLevel.Debug,
            Message = message,
            Caller = caller,
            File = GetFileName(file)
        });
    }
    
    public void Info(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        AddEntry(new LogEntry
        {
            Level = LogLevel.Info,
            Message = message,
            Caller = caller,
            File = GetFileName(file)
        });
    }
    
    public void Warning(string message, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        AddEntry(new LogEntry
        {
            Level = LogLevel.Warning,
            Message = message,
            Caller = caller,
            File = GetFileName(file)
        });
    }
    
    public void Error(string message, Exception? exception = null, [CallerMemberName] string? caller = null, [CallerFilePath] string? file = null)
    {
        AddEntry(new LogEntry
        {
            Level = LogLevel.Error,
            Message = message,
            Caller = caller,
            File = GetFileName(file),
            ExceptionType = exception?.GetType().Name,
            ExceptionMessage = exception?.Message,
            StackTrace = exception?.StackTrace
        });
        
        // Also write to debug output for development
        System.Diagnostics.Debug.WriteLine($"[ERROR] {message}: {exception?.Message}");
    }
    
    public void SkippedFile(string path, string reason, [CallerMemberName] string? caller = null)
    {
        AddEntry(new LogEntry
        {
            Level = LogLevel.Skipped,
            Message = $"Skipped: {path} - {reason}",
            Caller = caller
        });
    }
    
    public IReadOnlyList<LogEntry> GetRecentEntries(int count = 100)
    {
        return _entries.TakeLast(count).ToList();
    }
    
    public void Clear()
    {
        _entries.Clear();
    }
    
    public async Task ExportAsync(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Level,Caller,File,Message,Exception");
        
        foreach (var entry in _entries)
        {
            var message = entry.Message.Replace("\"", "\"\"");
            var exception = entry.ExceptionMessage?.Replace("\"", "\"\"") ?? "";
            sb.AppendLine($"\"{entry.Timestamp:O}\",\"{entry.Level}\",\"{entry.Caller}\",\"{entry.File}\",\"{message}\",\"{exception}\"");
        }
        
        await File.WriteAllTextAsync(filePath, sb.ToString());
    }
    
    private void AddEntry(LogEntry entry)
    {
        _entries.Enqueue(entry);
        
        // Trim old entries if needed
        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
        {
            // Just dequeue to trim
        }
        
        LogEntryAdded?.Invoke(this, entry);
    }
    
    private static string? GetFileName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return Path.GetFileName(path);
    }
}
