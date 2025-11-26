using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Print service for file lists, directory trees, and file contents.
/// Supports export to various formats.
/// </summary>
public class PrintService : IPrintService
{
    private const int DefaultLinesPerPage = 60;
    private const int DefaultCharsPerLine = 80;
    
    public async Task<PrintDocument> GenerateFileListAsync(string directoryPath, FileListPrintOptions options,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }
            
            var searchOption = options.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            // Get files
            var files = Directory.EnumerateFiles(directoryPath, 
                    options.FilterPattern ?? "*", searchOption)
                .Select(f => new FileInfo(f))
                .ToList();
            
            // Sort files
            files = SortFiles(files, options.SortBy, options.SortDescending);
            
            // Build content
            var sb = new StringBuilder();
            var htmlSb = new StringBuilder();
            
            // Header
            var header = options.Header ?? $"File List: {directoryPath}";
            sb.AppendLine(header);
            sb.AppendLine(new string('=', Math.Min(header.Length, DefaultCharsPerLine)));
            sb.AppendLine();
            
            htmlSb.AppendLine("<!DOCTYPE html><html><head>");
            htmlSb.AppendLine("<style>body{font-family:monospace;} table{border-collapse:collapse;} th,td{padding:4px 8px;text-align:left;border-bottom:1px solid #ddd;}</style>");
            htmlSb.AppendLine($"</head><body><h1>{EscapeHtml(header)}</h1>");
            htmlSb.AppendLine("<table><thead><tr><th>Name</th>");
            
            if (options.ShowSize) htmlSb.Append("<th>Size</th>");
            if (options.ShowDate) htmlSb.Append("<th>Date</th>");
            if (options.ShowTime) htmlSb.Append("<th>Time</th>");
            if (options.ShowAttributes) htmlSb.Append("<th>Attr</th>");
            
            htmlSb.AppendLine("</tr></thead><tbody>");
            
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var name = options.ShowFullPath ? file.FullName : file.Name;
                var line = new StringBuilder(name);
                var htmlRow = new StringBuilder($"<tr><td>{EscapeHtml(name)}</td>");
                
                if (options.ShowSize)
                {
                    var size = FormatFileSize(file.Length);
                    line.Append($"  {size,12}");
                    htmlRow.Append($"<td style=\"text-align:right\">{size}</td>");
                }
                
                if (options.ShowDate)
                {
                    var date = file.LastWriteTime.ToString("yyyy-MM-dd");
                    line.Append($"  {date}");
                    htmlRow.Append($"<td>{date}</td>");
                }
                
                if (options.ShowTime)
                {
                    var time = file.LastWriteTime.ToString("HH:mm:ss");
                    line.Append($"  {time}");
                    htmlRow.Append($"<td>{time}</td>");
                }
                
                if (options.ShowAttributes)
                {
                    var attrs = GetAttributeString(file.Attributes);
                    line.Append($"  {attrs}");
                    htmlRow.Append($"<td>{attrs}</td>");
                }
                
                sb.AppendLine(line.ToString());
                htmlRow.Append("</tr>");
                htmlSb.AppendLine(htmlRow.ToString());
            }
            
            // Summary
            sb.AppendLine();
            sb.AppendLine(new string('-', DefaultCharsPerLine));
            var summary = $"Total: {files.Count} files, {FormatFileSize(files.Sum(f => f.Length))}";
            sb.AppendLine(summary);
            
            htmlSb.AppendLine("</tbody></table>");
            htmlSb.AppendLine($"<p><strong>{EscapeHtml(summary)}</strong></p>");
            
            // Footer
            if (options.Footer != null)
            {
                sb.AppendLine();
                sb.AppendLine(options.Footer);
                htmlSb.AppendLine($"<p>{EscapeHtml(options.Footer)}</p>");
            }
            
            htmlSb.AppendLine("</body></html>");
            
            return new PrintDocument
            {
                Title = header,
                PlainTextContent = sb.ToString(),
                HtmlContent = htmlSb.ToString(),
                Pages = PaginateText(sb.ToString(), DefaultLinesPerPage)
            };
        }, cancellationToken);
    }
    
    public async Task<PrintDocument> GenerateDirectoryTreeAsync(string directoryPath, DirectoryTreePrintOptions options,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }
            
            var sb = new StringBuilder();
            var htmlSb = new StringBuilder();
            
            var header = options.Header ?? $"Directory Tree: {directoryPath}";
            sb.AppendLine(header);
            sb.AppendLine(new string('=', Math.Min(header.Length, DefaultCharsPerLine)));
            sb.AppendLine();
            
            htmlSb.AppendLine("<!DOCTYPE html><html><head>");
            htmlSb.AppendLine("<style>body{font-family:monospace;} .tree{white-space:pre;}</style>");
            htmlSb.AppendLine($"</head><body><h1>{EscapeHtml(header)}</h1><div class=\"tree\">");
            
            var stats = new TreeStats();
            BuildDirectoryTree(sb, htmlSb, directoryPath, "", options, 0, stats, cancellationToken);
            
            sb.AppendLine();
            sb.AppendLine(new string('-', DefaultCharsPerLine));
            var summary = $"Total: {stats.DirectoryCount} directories, {stats.FileCount} files";
            if (options.ShowSize)
            {
                summary += $", {FormatFileSize(stats.TotalSize)}";
            }
            sb.AppendLine(summary);
            
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine($"<p><strong>{EscapeHtml(summary)}</strong></p>");
            
            if (options.Footer != null)
            {
                sb.AppendLine();
                sb.AppendLine(options.Footer);
                htmlSb.AppendLine($"<p>{EscapeHtml(options.Footer)}</p>");
            }
            
            htmlSb.AppendLine("</body></html>");
            
            return new PrintDocument
            {
                Title = header,
                PlainTextContent = sb.ToString(),
                HtmlContent = htmlSb.ToString(),
                Pages = PaginateText(sb.ToString(), DefaultLinesPerPage)
            };
        }, cancellationToken);
    }
    
    public async Task<PrintDocument> GenerateFileContentAsync(string filePath, FileContentPrintOptions options,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }
            
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            
            // Apply line range filter
            var startLine = options.StartLine ?? 1;
            var endLine = options.EndLine ?? lines.Length;
            startLine = Math.Max(1, startLine);
            endLine = Math.Min(lines.Length, endLine);
            
            var selectedLines = lines.Skip(startLine - 1).Take(endLine - startLine + 1).ToList();
            
            var sb = new StringBuilder();
            var htmlSb = new StringBuilder();
            
            var header = options.Header ?? $"File: {Path.GetFileName(filePath)}";
            sb.AppendLine(header);
            sb.AppendLine(new string('=', Math.Min(header.Length, DefaultCharsPerLine)));
            sb.AppendLine();
            
            htmlSb.AppendLine("<!DOCTYPE html><html><head>");
            htmlSb.AppendLine("<style>body{font-family:monospace;} .content{white-space:pre;} .line-num{color:#888;}</style>");
            htmlSb.AppendLine($"</head><body><h1>{EscapeHtml(header)}</h1><div class=\"content\">");
            
            var lineNumber = startLine;
            var maxLineNumWidth = endLine.ToString().Length;
            
            foreach (var line in selectedLines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var processedLine = line.Replace("\t", new string(' ', options.TabSize));
                
                if (options.ShowLineNumbers)
                {
                    var lineNumStr = lineNumber.ToString().PadLeft(maxLineNumWidth);
                    sb.AppendLine($"{lineNumStr} | {processedLine}");
                    htmlSb.AppendLine($"<span class=\"line-num\">{lineNumStr}</span> | {EscapeHtml(processedLine)}");
                }
                else
                {
                    sb.AppendLine(processedLine);
                    htmlSb.AppendLine(EscapeHtml(processedLine));
                }
                
                lineNumber++;
            }
            
            htmlSb.AppendLine("</div>");
            
            // Footer
            sb.AppendLine();
            sb.AppendLine(new string('-', DefaultCharsPerLine));
            var summary = $"Lines {startLine}-{endLine} of {lines.Length}";
            sb.AppendLine(summary);
            
            htmlSb.AppendLine($"<p><em>{EscapeHtml(summary)}</em></p>");
            
            if (options.Footer != null)
            {
                sb.AppendLine();
                sb.AppendLine(options.Footer);
                htmlSb.AppendLine($"<p>{EscapeHtml(options.Footer)}</p>");
            }
            
            htmlSb.AppendLine("</body></html>");
            
            return new PrintDocument
            {
                Title = header,
                PlainTextContent = sb.ToString(),
                HtmlContent = htmlSb.ToString(),
                Pages = PaginateText(sb.ToString(), DefaultLinesPerPage)
            };
        }, cancellationToken);
    }
    
    public async Task ExportDocumentAsync(PrintDocument document, string outputPath, PrintExportFormat format,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(async () =>
        {
            var content = format switch
            {
                PrintExportFormat.PlainText => document.PlainTextContent,
                PrintExportFormat.Html => document.HtmlContent,
                PrintExportFormat.Rtf => ConvertToRtf(document),
                PrintExportFormat.Csv => ConvertToCsv(document),
                PrintExportFormat.Pdf => throw new NotSupportedException("PDF export requires additional library"),
                _ => document.PlainTextContent
            };
            
            await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        }, cancellationToken);
    }
    
    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run<IReadOnlyList<PrinterInfo>>(() =>
        {
            var printers = new List<PrinterInfo>();
            
            // Platform-specific printer enumeration
            if (OperatingSystem.IsWindows())
            {
                // Would use System.Drawing.Printing.PrinterSettings.InstalledPrinters
                // For now, return empty list as it requires specific references
                printers.Add(new PrinterInfo
                {
                    Name = "Default",
                    Description = "System default printer",
                    IsDefault = true,
                    Status = PrinterStatus.Ready
                });
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                // Use lpstat or similar to get printers
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "lpstat",
                        Arguments = "-a",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                printers.Add(new PrinterInfo
                                {
                                    Name = parts[0],
                                    Status = PrinterStatus.Ready
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // lpstat not available
                }
            }
            
            if (printers.Count == 0)
            {
                printers.Add(new PrinterInfo
                {
                    Name = "Default",
                    Description = "System default printer",
                    IsDefault = true,
                    Status = PrinterStatus.Unknown
                });
            }
            
            return printers;
        }, cancellationToken);
    }
    
    public async Task PrintDocumentAsync(PrintDocument document, PrinterInfo? printer = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            // Create a temporary file with the content
            var tempFile = Path.Combine(Path.GetTempPath(), $"print_{Guid.NewGuid():N}.txt");
            
            try
            {
                File.WriteAllText(tempFile, document.PlainTextContent);
                
                if (OperatingSystem.IsWindows())
                {
                    // Use notepad /p for simple printing
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad",
                        Arguments = $"/p \"{tempFile}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // Use lpr on macOS
                    var args = printer != null ? $"-P \"{printer.Name}\" \"{tempFile}\"" : $"\"{tempFile}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "lpr",
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Use lpr on Linux
                    var args = printer != null ? $"-P \"{printer.Name}\" \"{tempFile}\"" : $"\"{tempFile}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "lpr",
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit();
                }
            }
            finally
            {
                // Clean up temp file
                try { File.Delete(tempFile); } catch { }
            }
        }, cancellationToken);
    }
    
    #region Private Helpers
    
    private static List<FileInfo> SortFiles(List<FileInfo> files, FileSortField sortBy, bool descending)
    {
        IEnumerable<FileInfo> sorted = sortBy switch
        {
            FileSortField.Name => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            FileSortField.Extension => files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name),
            FileSortField.Size => files.OrderBy(f => f.Length),
            FileSortField.Date => files.OrderBy(f => f.LastWriteTime),
            FileSortField.Path => files.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
        };
        
        return descending ? sorted.Reverse().ToList() : sorted.ToList();
    }
    
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
    
    private static string GetAttributeString(FileAttributes attrs)
    {
        var sb = new StringBuilder();
        if ((attrs & FileAttributes.ReadOnly) != 0) sb.Append('R');
        if ((attrs & FileAttributes.Hidden) != 0) sb.Append('H');
        if ((attrs & FileAttributes.System) != 0) sb.Append('S');
        if ((attrs & FileAttributes.Archive) != 0) sb.Append('A');
        return sb.ToString().PadRight(4);
    }
    
    private void BuildDirectoryTree(StringBuilder sb, StringBuilder htmlSb, string path, string prefix,
        DirectoryTreePrintOptions options, int depth, TreeStats stats, CancellationToken cancellationToken)
    {
        if (depth >= options.MaxDepth) return;
        
        cancellationToken.ThrowIfCancellationRequested();
        
        var dirInfo = new DirectoryInfo(path);
        var (branch, pipe, space, corner) = GetTreeChars(options.Style);
        
        // Get directories
        var directories = dirInfo.GetDirectories()
            .Where(d => options.ShowHidden || (d.Attributes & FileAttributes.Hidden) == 0)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        // Get files if requested
        var files = options.ShowFiles
            ? dirInfo.GetFiles()
                .Where(f => options.ShowHidden || (f.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<FileInfo>();
        
        var items = new List<(string name, bool isDir, long size)>();
        items.AddRange(directories.Select(d => (d.Name, true, 0L)));
        items.AddRange(files.Select(f => (f.Name, false, f.Length)));
        
        for (int i = 0; i < items.Count; i++)
        {
            var (name, isDir, size) = items[i];
            var isLast = i == items.Count - 1;
            var marker = isLast ? corner : branch;
            var continuation = isLast ? space : pipe;
            
            var line = $"{prefix}{marker}{name}";
            if (!isDir && options.ShowSize)
            {
                line += $" ({FormatFileSize(size)})";
            }
            else if (isDir && options.ShowFileCount)
            {
                try
                {
                    var subDirPath = Path.Combine(path, name);
                    var fileCount = Directory.GetFiles(subDirPath, "*", SearchOption.TopDirectoryOnly).Length;
                    line += $" [{fileCount} files]";
                }
                catch { }
            }
            
            sb.AppendLine(line);
            htmlSb.AppendLine(EscapeHtml(line));
            
            if (isDir)
            {
                stats.DirectoryCount++;
                var subDirPath = Path.Combine(path, name);
                BuildDirectoryTree(sb, htmlSb, subDirPath, prefix + continuation, 
                    options, depth + 1, stats, cancellationToken);
            }
            else
            {
                stats.FileCount++;
                stats.TotalSize += size;
            }
        }
    }
    
    private static (string branch, string pipe, string space, string corner) GetTreeChars(TreeStyle style)
    {
        return style switch
        {
            TreeStyle.Unicode => ("├── ", "│   ", "    ", "└── "),
            TreeStyle.Simple => ("    ", "    ", "    ", "    "),
            _ => ("+-- ", "|   ", "    ", "\\-- ") // Ascii
        };
    }
    
    private static List<PrintPage> PaginateText(string content, int linesPerPage)
    {
        var lines = content.Split('\n');
        var pages = new List<PrintPage>();
        var pageNumber = 1;
        
        for (int i = 0; i < lines.Length; i += linesPerPage)
        {
            var pageLines = lines.Skip(i).Take(linesPerPage);
            pages.Add(new PrintPage
            {
                PageNumber = pageNumber++,
                Content = string.Join('\n', pageLines)
            });
        }
        
        return pages;
    }
    
    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
    
    private static string ConvertToRtf(PrintDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"{\rtf1\ansi\deff0");
        sb.AppendLine(@"{\fonttbl{\f0 Consolas;}}");
        sb.AppendLine(@"\f0\fs20");
        
        foreach (var line in document.PlainTextContent.Split('\n'))
        {
            var escaped = line
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}");
            sb.AppendLine($"{escaped}\\par");
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
    
    private static string ConvertToCsv(PrintDocument document)
    {
        // Simple CSV conversion for file lists
        var lines = document.PlainTextContent.Split('\n');
        var sb = new StringBuilder();
        
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            // Escape and quote fields
            var escaped = line.Replace("\"", "\"\"");
            sb.AppendLine($"\"{escaped}\"");
        }
        
        return sb.ToString();
    }
    
    private class TreeStats
    {
        public int DirectoryCount { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
    }
    
    #endregion
}
