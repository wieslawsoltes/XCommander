using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Interface for print operations - file lists, directory trees, and file contents.
/// </summary>
public interface IPrintService
{
    /// <summary>
    /// Generates a printable file list from a directory.
    /// </summary>
    Task<PrintDocument> GenerateFileListAsync(string directoryPath, FileListPrintOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a printable directory tree structure.
    /// </summary>
    Task<PrintDocument> GenerateDirectoryTreeAsync(string directoryPath, DirectoryTreePrintOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates printable content from a text file.
    /// </summary>
    Task<PrintDocument> GenerateFileContentAsync(string filePath, FileContentPrintOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports print document to various formats.
    /// </summary>
    Task ExportDocumentAsync(PrintDocument document, string outputPath, PrintExportFormat format,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets available printers on the system.
    /// </summary>
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Prints document to specified printer (platform-specific).
    /// </summary>
    Task PrintDocumentAsync(PrintDocument document, PrinterInfo? printer = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Print document containing formatted content ready for printing/export.
/// </summary>
public class PrintDocument
{
    public string Title { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public List<PrintPage> Pages { get; init; } = new();
    public PrintPageSettings PageSettings { get; init; } = new();
    public string PlainTextContent { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
}

/// <summary>
/// Single page in a print document.
/// </summary>
public class PrintPage
{
    public int PageNumber { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? Header { get; init; }
    public string? Footer { get; init; }
}

/// <summary>
/// Page settings for printing.
/// </summary>
public class PrintPageSettings
{
    public PageSize Size { get; init; } = PageSize.A4;
    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;
    public PageMargins Margins { get; init; } = new();
    public string FontFamily { get; init; } = "Consolas";
    public int FontSize { get; init; } = 10;
    public bool ShowHeader { get; init; } = true;
    public bool ShowFooter { get; init; } = true;
    public bool ShowPageNumbers { get; init; } = true;
}

/// <summary>
/// Page margins in millimeters.
/// </summary>
public class PageMargins
{
    public int Top { get; init; } = 15;
    public int Bottom { get; init; } = 15;
    public int Left { get; init; } = 15;
    public int Right { get; init; } = 15;
}

/// <summary>
/// Paper size options.
/// </summary>
public enum PageSize
{
    A4,
    A3,
    Letter,
    Legal,
    Tabloid
}

/// <summary>
/// Page orientation.
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>
/// Options for printing file lists.
/// </summary>
public class FileListPrintOptions
{
    public bool IncludeSubdirectories { get; init; } = false;
    public bool ShowSize { get; init; } = true;
    public bool ShowDate { get; init; } = true;
    public bool ShowTime { get; init; } = true;
    public bool ShowAttributes { get; init; } = false;
    public bool ShowFullPath { get; init; } = false;
    public FileSortField SortBy { get; init; } = FileSortField.Name;
    public bool SortDescending { get; init; } = false;
    public string? FilterPattern { get; init; }
    public string? Header { get; init; }
    public string? Footer { get; init; }
    public PageSettings? PageSettings { get; init; }
}

/// <summary>
/// Field to sort files by.
/// </summary>
public enum FileSortField
{
    Name,
    Extension,
    Size,
    Date,
    Path
}

/// <summary>
/// Options for printing directory tree.
/// </summary>
public class DirectoryTreePrintOptions
{
    public int MaxDepth { get; init; } = int.MaxValue;
    public bool ShowFiles { get; init; } = true;
    public bool ShowHidden { get; init; } = false;
    public bool ShowSize { get; init; } = false;
    public bool ShowFileCount { get; init; } = true;
    public TreeStyle Style { get; init; } = TreeStyle.Ascii;
    public string? Header { get; init; }
    public string? Footer { get; init; }
    public PageSettings? PageSettings { get; init; }
}

/// <summary>
/// Tree display style.
/// </summary>
public enum TreeStyle
{
    Ascii,      // +-- and |
    Unicode,    // └── and │
    Simple      // Indentation only
}

/// <summary>
/// Options for printing file content.
/// </summary>
public class FileContentPrintOptions
{
    public bool ShowLineNumbers { get; init; } = true;
    public bool WrapLines { get; init; } = true;
    public int TabSize { get; init; } = 4;
    public bool SyntaxHighlight { get; init; } = false;
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public string? Header { get; init; }
    public string? Footer { get; init; }
    public PageSettings? PageSettings { get; init; }
}

/// <summary>
/// Page settings helper class.
/// </summary>
public class PageSettings
{
    public PageSize Size { get; init; } = PageSize.A4;
    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;
    public PageMargins? Margins { get; init; }
    public string? FontFamily { get; init; }
    public int? FontSize { get; init; }
}

/// <summary>
/// Export format for print documents.
/// </summary>
public enum PrintExportFormat
{
    PlainText,
    Html,
    Rtf,
    Pdf,    // Requires additional library
    Csv     // For file lists
}

/// <summary>
/// Information about an available printer.
/// </summary>
public class PrinterInfo
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsNetwork { get; init; }
    public PrinterStatus Status { get; init; } = PrinterStatus.Ready;
}

/// <summary>
/// Printer status.
/// </summary>
public enum PrinterStatus
{
    Ready,
    Offline,
    Busy,
    Error,
    Unknown
}
