// OperationLogService.cs - File Operation Logging Implementation

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XCommander.Services;

/// <summary>
/// Implementation of file operation logging.
/// </summary>
public class OperationLogService : IOperationLogService
{
    private readonly List<OperationLogEntry> _inMemoryLog = new();
    private readonly object _lock = new();
    private readonly int _maxInMemoryEntries;
    
    public bool IsLoggingEnabled { get; set; } = true;
    public string? LogFilePath { get; set; }
    
    public OperationLogService(string? logFilePath = null, int maxInMemoryEntries = 10000)
    {
        LogFilePath = logFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander",
            "Logs",
            $"operations_{DateTime.Now:yyyyMMdd}.log");
        
        _maxInMemoryEntries = maxInMemoryEntries;
        
        // Ensure log directory exists
        if (LogFilePath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
        }
    }
    
    public async Task LogOperationAsync(OperationLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (!IsLoggingEnabled)
            return;
        
        // Add to in-memory log
        lock (_lock)
        {
            _inMemoryLog.Add(entry);
            
            // Trim if too large
            if (_inMemoryLog.Count > _maxInMemoryEntries)
            {
                _inMemoryLog.RemoveRange(0, _inMemoryLog.Count - _maxInMemoryEntries);
            }
        }
        
        // Write to file
        if (!string.IsNullOrEmpty(LogFilePath))
        {
            var line = FormatLogEntry(entry);
            await File.AppendAllTextAsync(LogFilePath, line + Environment.NewLine, cancellationToken);
        }
    }
    
    public Task<IReadOnlyList<OperationLogEntry>> GetRecentEntriesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entries = _inMemoryLog
                .OrderByDescending(e => e.StartTime)
                .Take(count)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<OperationLogEntry>>(entries);
        }
    }
    
    public Task<IReadOnlyList<OperationLogEntry>> GetEntriesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entries = _inMemoryLog
                .Where(e => e.StartTime >= from && e.StartTime <= to)
                .OrderByDescending(e => e.StartTime)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<OperationLogEntry>>(entries);
        }
    }
    
    public Task<IReadOnlyList<OperationLogEntry>> GetEntriesByTypeAsync(OperationType type, int count = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entries = _inMemoryLog
                .Where(e => e.Type == type)
                .OrderByDescending(e => e.StartTime)
                .Take(count)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<OperationLogEntry>>(entries);
        }
    }
    
    public Task<IReadOnlyList<OperationLogEntry>> SearchEntriesAsync(OperationLogSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IEnumerable<OperationLogEntry> query = _inMemoryLog;
            
            if (criteria.From.HasValue)
                query = query.Where(e => e.StartTime >= criteria.From.Value);
            
            if (criteria.To.HasValue)
                query = query.Where(e => e.StartTime <= criteria.To.Value);
            
            if (criteria.Type.HasValue)
                query = query.Where(e => e.Type == criteria.Type.Value);
            
            if (criteria.Status.HasValue)
                query = query.Where(e => e.Status == criteria.Status.Value);
            
            if (!string.IsNullOrEmpty(criteria.PathContains))
            {
                var search = criteria.PathContains.ToLowerInvariant();
                query = query.Where(e => 
                    e.SourcePaths.Any(p => p.ToLowerInvariant().Contains(search)) ||
                    (e.DestinationPath?.ToLowerInvariant().Contains(search) ?? false));
            }
            
            if (criteria.MinBytes.HasValue)
                query = query.Where(e => e.BytesProcessed >= criteria.MinBytes.Value);
            
            var entries = query
                .OrderByDescending(e => e.StartTime)
                .Take(criteria.MaxResults)
                .ToList();
            
            return Task.FromResult<IReadOnlyList<OperationLogEntry>>(entries);
        }
    }
    
    public Task ClearOldEntriesAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - retention;
        
        lock (_lock)
        {
            _inMemoryLog.RemoveAll(e => e.StartTime < cutoff);
        }
        
        return Task.CompletedTask;
    }
    
    public async Task ExportLogAsync(string filePath, DateTime from, DateTime to, LogExportFormat format, CancellationToken cancellationToken = default)
    {
        var entries = await GetEntriesAsync(from, to, cancellationToken);
        
        var content = format switch
        {
            LogExportFormat.Text => ExportAsText(entries),
            LogExportFormat.Csv => ExportAsCsv(entries),
            LogExportFormat.Json => ExportAsJson(entries),
            LogExportFormat.Xml => ExportAsXml(entries),
            LogExportFormat.Html => ExportAsHtml(entries),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        
        await File.WriteAllTextAsync(filePath, content, cancellationToken);
    }
    
    /// <summary>
    /// Creates a started operation log entry.
    /// </summary>
    public static OperationLogEntry CreateStartedEntry(
        OperationType type,
        IEnumerable<string> sourcePaths,
        string? destinationPath = null)
    {
        return new OperationLogEntry
        {
            Type = type,
            Status = OperationStatus.Started,
            StartTime = DateTime.UtcNow,
            SourcePaths = sourcePaths.ToList(),
            DestinationPath = destinationPath,
            Username = Environment.UserName,
            MachineName = Environment.MachineName
        };
    }
    
    /// <summary>
    /// Creates a completed operation log entry from a started entry.
    /// </summary>
    public static OperationLogEntry CreateCompletedEntry(
        OperationLogEntry started,
        long bytesProcessed,
        int filesProcessed,
        int foldersProcessed = 0,
        int itemsSkipped = 0,
        int itemsFailed = 0)
    {
        var status = itemsFailed > 0
            ? (itemsFailed < filesProcessed + foldersProcessed ? OperationStatus.PartialSuccess : OperationStatus.Failed)
            : OperationStatus.Completed;
        
        return started with
        {
            Status = status,
            EndTime = DateTime.UtcNow,
            BytesProcessed = bytesProcessed,
            FilesProcessed = filesProcessed,
            FoldersProcessed = foldersProcessed,
            ItemsSkipped = itemsSkipped,
            ItemsFailed = itemsFailed
        };
    }
    
    /// <summary>
    /// Creates a failed operation log entry.
    /// </summary>
    public static OperationLogEntry CreateFailedEntry(
        OperationLogEntry started,
        string errorMessage,
        int filesProcessed = 0,
        int foldersProcessed = 0)
    {
        return started with
        {
            Status = OperationStatus.Failed,
            EndTime = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            FilesProcessed = filesProcessed,
            FoldersProcessed = foldersProcessed,
            ItemsFailed = 1
        };
    }
    
    private static string FormatLogEntry(OperationLogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append(entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append($" | {entry.Type,-15}");
        sb.Append($" | {entry.Status,-12}");
        
        if (entry.SourcePaths.Count == 1)
        {
            sb.Append($" | {entry.SourcePaths[0]}");
        }
        else
        {
            sb.Append($" | [{entry.SourcePaths.Count} items]");
        }
        
        if (!string.IsNullOrEmpty(entry.DestinationPath))
        {
            sb.Append($" -> {entry.DestinationPath}");
        }
        
        if (entry.BytesProcessed > 0)
        {
            sb.Append($" | {FormatBytes(entry.BytesProcessed)}");
        }
        
        if (entry.Duration.HasValue)
        {
            sb.Append($" | {entry.Duration.Value.TotalSeconds:F2}s");
        }
        
        if (!string.IsNullOrEmpty(entry.ErrorMessage))
        {
            sb.Append($" | ERROR: {entry.ErrorMessage}");
        }
        
        return sb.ToString();
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    private static string ExportAsText(IReadOnlyList<OperationLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("XCommander Operation Log");
        sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Entries: {entries.Count}");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();
        
        foreach (var entry in entries)
        {
            sb.AppendLine(FormatLogEntry(entry));
        }
        
        return sb.ToString();
    }
    
    private static string ExportAsCsv(IReadOnlyList<OperationLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OperationId,Type,Status,StartTime,EndTime,DurationSeconds,SourcePaths,DestinationPath,BytesProcessed,FilesProcessed,FoldersProcessed,ItemsSkipped,ItemsFailed,ErrorMessage");
        
        foreach (var entry in entries)
        {
            sb.AppendLine(string.Join(",",
                entry.OperationId,
                entry.Type,
                entry.Status,
                entry.StartTime.ToString("o"),
                entry.EndTime?.ToString("o") ?? "",
                entry.Duration?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? "",
                $"\"{string.Join(";", entry.SourcePaths)}\"",
                $"\"{entry.DestinationPath ?? ""}\"",
                entry.BytesProcessed,
                entry.FilesProcessed,
                entry.FoldersProcessed,
                entry.ItemsSkipped,
                entry.ItemsFailed,
                $"\"{entry.ErrorMessage?.Replace("\"", "\"\"") ?? ""}\""
            ));
        }
        
        return sb.ToString();
    }
    
    private static string ExportAsJson(IReadOnlyList<OperationLogEntry> entries)
    {
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    
    private static string ExportAsXml(IReadOnlyList<OperationLogEntry> entries)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("OperationLog",
                new XAttribute("ExportTime", DateTime.Now.ToString("o")),
                new XAttribute("Count", entries.Count),
                entries.Select(e => new XElement("Entry",
                    new XElement("OperationId", e.OperationId),
                    new XElement("Type", e.Type),
                    new XElement("Status", e.Status),
                    new XElement("StartTime", e.StartTime.ToString("o")),
                    e.EndTime.HasValue ? new XElement("EndTime", e.EndTime.Value.ToString("o")) : null,
                    new XElement("SourcePaths",
                        e.SourcePaths.Select(p => new XElement("Path", p))),
                    !string.IsNullOrEmpty(e.DestinationPath) ? new XElement("DestinationPath", e.DestinationPath) : null,
                    new XElement("BytesProcessed", e.BytesProcessed),
                    new XElement("FilesProcessed", e.FilesProcessed),
                    new XElement("FoldersProcessed", e.FoldersProcessed),
                    new XElement("ItemsSkipped", e.ItemsSkipped),
                    new XElement("ItemsFailed", e.ItemsFailed),
                    !string.IsNullOrEmpty(e.ErrorMessage) ? new XElement("ErrorMessage", e.ErrorMessage) : null
                ))
            )
        );
        
        return doc.ToString();
    }
    
    private static string ExportAsHtml(IReadOnlyList<OperationLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine("<title>XCommander Operation Log</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine(".success { color: green; }");
        sb.AppendLine(".failed { color: red; }");
        sb.AppendLine(".partial { color: orange; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>XCommander Operation Log</h1>");
        sb.AppendLine($"<p>Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Entries: {entries.Count}</p>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Time</th><th>Type</th><th>Status</th><th>Source</th><th>Destination</th><th>Size</th><th>Duration</th></tr>");
        
        foreach (var entry in entries)
        {
            var statusClass = entry.Status switch
            {
                OperationStatus.Completed => "success",
                OperationStatus.Failed => "failed",
                OperationStatus.PartialSuccess => "partial",
                _ => ""
            };
            
            sb.AppendLine($"<tr>");
            sb.AppendLine($"<td>{entry.StartTime:yyyy-MM-dd HH:mm:ss}</td>");
            sb.AppendLine($"<td>{entry.Type}</td>");
            sb.AppendLine($"<td class=\"{statusClass}\">{entry.Status}</td>");
            sb.AppendLine($"<td>{(entry.SourcePaths.Count == 1 ? entry.SourcePaths[0] : $"[{entry.SourcePaths.Count} items]")}</td>");
            sb.AppendLine($"<td>{entry.DestinationPath ?? ""}</td>");
            sb.AppendLine($"<td>{FormatBytes(entry.BytesProcessed)}</td>");
            sb.AppendLine($"<td>{(entry.Duration.HasValue ? $"{entry.Duration.Value.TotalSeconds:F2}s" : "")}</td>");
            sb.AppendLine($"</tr>");
        }
        
        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        
        return sb.ToString();
    }
}
