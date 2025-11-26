using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of IFileLoggingService for TC-style file operation logging.
/// </summary>
public class FileLoggingService : IFileLoggingService
{
    private FileLoggingConfig _config;
    private readonly ConcurrentQueue<FileOperationLogEntry> _inMemoryBuffer = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private const int MaxInMemoryEntries = 10000;
    
    public event EventHandler<FileOperationLogEntry>? LogEntryAdded;

    public FileLoggingService()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander",
            "Logs");
            
        _config = new FileLoggingConfig
        {
            LogDirectory = logDir,
            Enabled = true
        };
        
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
    }

    public async Task LogOperationAsync(FileOperationLogEntry entry)
    {
        if (!_config.Enabled)
            return;
            
        // Check severity filter
        if (entry.Severity < _config.MinimumSeverity)
            return;
            
        // Check operation type filter
        if (_config.OperationFilter.Count > 0 && !_config.OperationFilter.Contains(entry.OperationType))
            return;

        // Add system info if configured
        if (_config.IncludeSystemInfo)
        {
            entry = entry with
            {
                UserName = entry.UserName ?? Environment.UserName,
                MachineName = entry.MachineName ?? Environment.MachineName
            };
        }

        // Add to in-memory buffer
        _inMemoryBuffer.Enqueue(entry);
        while (_inMemoryBuffer.Count > MaxInMemoryEntries)
        {
            _inMemoryBuffer.TryDequeue(out _);
        }

        // Write to file
        await WriteToFileAsync(entry);
        
        LogEntryAdded?.Invoke(this, entry);
    }

    public Task LogSuccessAsync(
        FileOperationType operationType,
        string sourcePath,
        string? destinationPath = null,
        long? fileSize = null,
        TimeSpan? duration = null,
        string? message = null)
    {
        var entry = new FileOperationLogEntry
        {
            OperationType = operationType,
            Severity = FileOperationSeverity.Success,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            FileSize = fileSize,
            Duration = duration,
            Message = message ?? $"{operationType} completed: {sourcePath}"
        };
        
        return LogOperationAsync(entry);
    }

    public Task LogErrorAsync(
        FileOperationType operationType,
        string sourcePath,
        string? destinationPath,
        Exception exception,
        string? message = null)
    {
        var entry = new FileOperationLogEntry
        {
            OperationType = operationType,
            Severity = FileOperationSeverity.Error,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Message = message ?? $"{operationType} failed: {sourcePath}",
            ErrorMessage = exception.Message,
            ExceptionDetails = _config.IncludeExceptionDetails ? exception.ToString() : null
        };
        
        return LogOperationAsync(entry);
    }

    public Task LogWarningAsync(
        FileOperationType operationType,
        string message,
        string? path = null)
    {
        var entry = new FileOperationLogEntry
        {
            OperationType = operationType,
            Severity = FileOperationSeverity.Warning,
            SourcePath = path,
            Message = message
        };
        
        return LogOperationAsync(entry);
    }

    private async Task WriteToFileAsync(FileOperationLogEntry entry)
    {
        await _writeLock.WaitAsync();
        try
        {
            var filePath = GetLogFilePath(entry.OperationType, entry.Timestamp);
            var line = FormatLogEntry(entry);
            
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine);
            
            // Check if rotation is needed
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists && fileInfo.Length > _config.MaxFileSizeBytes)
            {
                await RotateLogFileAsync(filePath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string GetLogFilePath(FileOperationType operationType, DateTime timestamp)
    {
        var fileName = _config.FileNamePattern
            .Replace("{date:yyyy-MM-dd}", timestamp.ToString("yyyy-MM-dd"))
            .Replace("{date:yyyyMMdd}", timestamp.ToString("yyyyMMdd"))
            .Replace("{date}", timestamp.ToString("yyyy-MM-dd"));
            
        if (_config.SeparateLogsByType)
        {
            var ext = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{nameWithoutExt}-{operationType}{ext}";
        }
        
        return Path.Combine(_config.LogDirectory, fileName);
    }

    private string FormatLogEntry(FileOperationLogEntry entry)
    {
        return _config.Format.ToUpperInvariant() switch
        {
            "JSON" => FormatAsJson(entry),
            "CSV" => FormatAsCsv(entry),
            _ => FormatAsText(entry)
        };
    }

    private static string FormatAsText(FileOperationLogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{entry.Severity,-7}] ");
        sb.Append($"[{entry.OperationType,-12}] ");
        
        if (!string.IsNullOrEmpty(entry.SourcePath))
        {
            sb.Append($"Source: {entry.SourcePath} ");
        }
        
        if (!string.IsNullOrEmpty(entry.DestinationPath))
        {
            sb.Append($"-> {entry.DestinationPath} ");
        }
        
        if (entry.FileSize.HasValue)
        {
            sb.Append($"({FormatSize(entry.FileSize.Value)}) ");
        }
        
        if (entry.Duration.HasValue)
        {
            sb.Append($"[{entry.Duration.Value.TotalMilliseconds:F0}ms] ");
        }
        
        sb.Append(entry.Message);
        
        if (!string.IsNullOrEmpty(entry.ErrorMessage))
        {
            sb.Append($" ERROR: {entry.ErrorMessage}");
        }
        
        return sb.ToString();
    }

    private static string FormatAsJson(FileOperationLogEntry entry)
    {
        return JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string FormatAsCsv(FileOperationLogEntry entry)
    {
        var fields = new[]
        {
            entry.Timestamp.ToString("o"),
            entry.Severity.ToString(),
            entry.OperationType.ToString(),
            EscapeCsv(entry.SourcePath ?? ""),
            EscapeCsv(entry.DestinationPath ?? ""),
            entry.FileSize?.ToString() ?? "",
            entry.Duration?.TotalMilliseconds.ToString(CultureInfo.InvariantCulture) ?? "",
            EscapeCsv(entry.Message),
            EscapeCsv(entry.ErrorMessage ?? "")
        };
        
        return string.Join(",", fields);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        
        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }
        
        return $"{size:F1} {suffixes[i]}";
    }

    private async Task RotateLogFileAsync(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? _config.LogDirectory;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        
        // Rename current file with timestamp
        var newName = $"{name}_{DateTime.Now:HHmmss}{ext}";
        var newPath = Path.Combine(dir, newName);
        
        if (File.Exists(filePath))
        {
            File.Move(filePath, newPath);
        }
        
        // Delete old files if over limit
        await CleanupOldLogsAsync();
    }

    private Task CleanupOldLogsAsync()
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(_config.LogDirectory))
                return;
                
            var logFiles = Directory.GetFiles(_config.LogDirectory, "*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(_config.MaxFileCount)
                .ToList();
                
            foreach (var file in logFiles)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        });
    }

    public async Task<IReadOnlyList<FileOperationLogEntry>> QueryLogsAsync(
        LogQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new LogQueryOptions();
        
        // First check in-memory buffer
        var results = _inMemoryBuffer.ToList();
        
        // Apply filters
        results = ApplyFilters(results, options);
        
        // Sort
        results = options.NewestFirst
            ? results.OrderByDescending(e => e.Timestamp).ToList()
            : results.OrderBy(e => e.Timestamp).ToList();
        
        // Pagination
        results = results.Skip(options.Skip).Take(options.MaxResults).ToList();
        
        return await Task.FromResult<IReadOnlyList<FileOperationLogEntry>>(results);
    }

    private static List<FileOperationLogEntry> ApplyFilters(List<FileOperationLogEntry> entries, LogQueryOptions options)
    {
        var query = entries.AsEnumerable();
        
        if (options.StartDate.HasValue)
        {
            query = query.Where(e => e.Timestamp >= options.StartDate.Value);
        }
        
        if (options.EndDate.HasValue)
        {
            query = query.Where(e => e.Timestamp <= options.EndDate.Value);
        }
        
        if (options.OperationTypes?.Count > 0)
        {
            query = query.Where(e => options.OperationTypes.Contains(e.OperationType));
        }
        
        if (options.MinimumSeverity.HasValue)
        {
            query = query.Where(e => e.Severity >= options.MinimumSeverity.Value);
        }
        
        if (!string.IsNullOrEmpty(options.SearchText))
        {
            var search = options.SearchText;
            query = query.Where(e =>
                (e.SourcePath?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.DestinationPath?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Message.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        
        return query.ToList();
    }

    public async Task<IReadOnlyList<FileOperationLogEntry>> GetTodayLogsAsync(
        CancellationToken cancellationToken = default)
    {
        return await QueryLogsAsync(new LogQueryOptions
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1)
        }, cancellationToken);
    }

    public Task<FileOperationLogEntry?> GetLogEntryAsync(Guid id)
    {
        var entry = _inMemoryBuffer.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(entry);
    }

    public Task<int> DeleteLogsAsync(LogQueryOptions filter, CancellationToken cancellationToken = default)
    {
        // For in-memory buffer, we can't selectively delete
        // This would require persisted storage implementation
        return Task.FromResult(0);
    }

    public Task<int> DeleteLogsOlderThanAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            int deleted = 0;
            
            if (!Directory.Exists(_config.LogDirectory))
                return deleted;
                
            var oldFiles = Directory.GetFiles(_config.LogDirectory, "*.log")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime < date)
                .ToList();
                
            foreach (var file in oldFiles)
            {
                try
                {
                    file.Delete();
                    deleted++;
                }
                catch
                {
                    // Ignore
                }
            }
            
            return deleted;
        }, cancellationToken);
    }

    public async Task ExportLogsAsync(
        string filePath,
        LogQueryOptions? filter = null,
        string format = "Text",
        CancellationToken cancellationToken = default)
    {
        var entries = await QueryLogsAsync(filter, cancellationToken);
        var originalFormat = _config.Format;
        
        try
        {
            _config = _config with { Format = format };
            
            var lines = entries.Select(FormatLogEntry);
            await File.WriteAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            _config = _config with { Format = originalFormat };
        }
    }

    public IReadOnlyList<string> GetLogFilePaths()
    {
        if (!Directory.Exists(_config.LogDirectory))
            return Array.Empty<string>();
            
        return Directory.GetFiles(_config.LogDirectory, "*.log")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .ToList();
    }

    public Task RotateLogsAsync()
    {
        return CleanupOldLogsAsync();
    }

    public FileLoggingConfig GetConfiguration()
    {
        return _config;
    }

    public Task SetConfigurationAsync(FileLoggingConfig config)
    {
        _config = config;
        
        if (!Directory.Exists(config.LogDirectory))
        {
            Directory.CreateDirectory(config.LogDirectory);
        }
        
        return Task.CompletedTask;
    }

    public Task<LogStatistics> GetStatisticsAsync(DateTime? since = null)
    {
        var entries = _inMemoryBuffer.ToList();
        
        if (since.HasValue)
        {
            entries = entries.Where(e => e.Timestamp >= since.Value).ToList();
        }
        
        var stats = new LogStatistics
        {
            TotalEntries = entries.Count,
            EntriesByType = entries.GroupBy(e => e.OperationType)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            EntriesBySeverity = entries.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            TotalBytesProcessed = entries.Sum(e => e.FileSize ?? 0),
            EarliestEntry = entries.Min(e => (DateTime?)e.Timestamp),
            LatestEntry = entries.Max(e => (DateTime?)e.Timestamp),
            LogFilesTotalSize = GetLogFilePaths().Sum(f => new FileInfo(f).Length)
        };
        
        return Task.FromResult(stats);
    }
}
