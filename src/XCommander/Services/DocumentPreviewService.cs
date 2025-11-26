using System.Diagnostics;
using System.Text;
using Avalonia.Media.Imaging;

namespace XCommander.Services;

/// <summary>
/// Service for previewing documents (PDF, Office documents).
/// </summary>
public class DocumentPreviewService
{
    private static readonly string[] PdfExtensions = { ".pdf" };
    private static readonly string[] OfficeExtensions = 
    { 
        ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
        ".odt", ".ods", ".odp", ".rtf"
    };
    
    /// <summary>
    /// Checks if a file is a PDF document.
    /// </summary>
    public static bool IsPdfFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return PdfExtensions.Contains(ext);
    }
    
    /// <summary>
    /// Checks if a file is an Office document.
    /// </summary>
    public static bool IsOfficeDocument(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return OfficeExtensions.Contains(ext);
    }
    
    /// <summary>
    /// Checks if a file can be previewed.
    /// </summary>
    public static bool CanPreview(string path)
    {
        return IsPdfFile(path) || IsOfficeDocument(path);
    }
    
    /// <summary>
    /// Gets basic info about a PDF file.
    /// </summary>
    public async Task<PdfInfo?> GetPdfInfoAsync(string path)
    {
        if (!IsPdfFile(path) || !File.Exists(path))
            return null;
        
        try
        {
            return await Task.Run(() =>
            {
                var info = new PdfInfo();
                
                using var stream = File.OpenRead(path);
                using var reader = new StreamReader(stream, Encoding.ASCII, true, 4096, true);
                
                // Read first few KB to find page count and metadata
                var buffer = new char[8192];
                var read = reader.Read(buffer, 0, buffer.Length);
                var content = new string(buffer, 0, read);
                
                // Try to find page count
                var pageCountMatch = System.Text.RegularExpressions.Regex.Match(
                    content, @"/Count\s+(\d+)");
                if (pageCountMatch.Success)
                {
                    info.PageCount = int.Parse(pageCountMatch.Groups[1].Value);
                }
                
                // Try to find title
                var titleMatch = System.Text.RegularExpressions.Regex.Match(
                    content, @"/Title\s*\(([^)]+)\)");
                if (titleMatch.Success)
                {
                    info.Title = titleMatch.Groups[1].Value;
                }
                
                // Try to find author
                var authorMatch = System.Text.RegularExpressions.Regex.Match(
                    content, @"/Author\s*\(([^)]+)\)");
                if (authorMatch.Success)
                {
                    info.Author = authorMatch.Groups[1].Value;
                }
                
                info.FileSize = new FileInfo(path).Length;
                
                return info;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get PDF info: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets basic info about an Office document.
    /// </summary>
    public async Task<OfficeDocumentInfo?> GetOfficeDocumentInfoAsync(string path)
    {
        if (!IsOfficeDocument(path) || !File.Exists(path))
            return null;
        
        try
        {
            return await Task.Run(() =>
            {
                var info = new OfficeDocumentInfo();
                var ext = Path.GetExtension(path).ToLowerInvariant();
                
                info.FileSize = new FileInfo(path).Length;
                info.Extension = ext;
                info.DocumentType = ext switch
                {
                    ".docx" or ".doc" or ".odt" or ".rtf" => "Word Document",
                    ".xlsx" or ".xls" or ".ods" => "Spreadsheet",
                    ".pptx" or ".ppt" or ".odp" => "Presentation",
                    _ => "Document"
                };
                
                // For modern Office formats (OOXML), try to extract metadata
                if (ext is ".docx" or ".xlsx" or ".pptx")
                {
                    try
                    {
                        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                        
                        // Read docProps/core.xml for metadata
                        var coreEntry = zip.GetEntry("docProps/core.xml");
                        if (coreEntry != null)
                        {
                            using var stream = coreEntry.Open();
                            using var reader = new StreamReader(stream);
                            var xml = reader.ReadToEnd();
                            
                            // Extract title
                            var titleMatch = System.Text.RegularExpressions.Regex.Match(
                                xml, @"<dc:title>([^<]+)</dc:title>");
                            if (titleMatch.Success)
                                info.Title = titleMatch.Groups[1].Value;
                            
                            // Extract author
                            var authorMatch = System.Text.RegularExpressions.Regex.Match(
                                xml, @"<dc:creator>([^<]+)</dc:creator>");
                            if (authorMatch.Success)
                                info.Author = authorMatch.Groups[1].Value;
                        }
                        
                        // For Word docs, try to get page count from app.xml
                        if (ext == ".docx")
                        {
                            var appEntry = zip.GetEntry("docProps/app.xml");
                            if (appEntry != null)
                            {
                                using var stream = appEntry.Open();
                                using var reader = new StreamReader(stream);
                                var xml = reader.ReadToEnd();
                                
                                var pageMatch = System.Text.RegularExpressions.Regex.Match(
                                    xml, @"<Pages>(\d+)</Pages>");
                                if (pageMatch.Success)
                                    info.PageCount = int.Parse(pageMatch.Groups[1].Value);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore OOXML parsing errors
                    }
                }
                
                return info;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get Office document info: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Extracts text content from a PDF (basic extraction).
    /// </summary>
    public async Task<string> ExtractPdfTextAsync(string path, int maxLength = 10000)
    {
        if (!IsPdfFile(path) || !File.Exists(path))
            return string.Empty;
        
        try
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                
                using var stream = File.OpenRead(path);
                using var reader = new StreamReader(stream, Encoding.ASCII, true);
                
                var content = reader.ReadToEnd();
                
                // Very basic text extraction - look for text between BT and ET markers
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    content, @"\(([^)]+)\)");
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var text = match.Groups[1].Value;
                    // Filter printable characters
                    foreach (var c in text)
                    {
                        if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c))
                        {
                            sb.Append(c);
                        }
                    }
                    
                    if (sb.Length >= maxLength)
                        break;
                }
                
                return sb.ToString().Trim();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract PDF text: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Extracts text from an Office document (basic extraction for OOXML).
    /// </summary>
    public async Task<string> ExtractOfficeTextAsync(string path, int maxLength = 10000)
    {
        if (!IsOfficeDocument(path) || !File.Exists(path))
            return string.Empty;
        
        var ext = Path.GetExtension(path).ToLowerInvariant();
        
        if (ext is not ".docx" and not ".xlsx" and not ".pptx")
            return "[Preview not available for this document type]";
        
        try
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                
                string? contentEntry = ext switch
                {
                    ".docx" => "word/document.xml",
                    ".xlsx" => "xl/sharedStrings.xml",
                    ".pptx" => null, // PPTX has multiple slide files
                    _ => null
                };
                
                if (ext == ".pptx")
                {
                    // For PPTX, read all slides
                    foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")))
                    {
                        using var stream = entry.Open();
                        using var reader = new StreamReader(stream);
                        var xml = reader.ReadToEnd();
                        
                        ExtractTextFromXml(xml, sb);
                        
                        if (sb.Length >= maxLength)
                            break;
                    }
                }
                else if (!string.IsNullOrEmpty(contentEntry))
                {
                    var entry = zip.GetEntry(contentEntry);
                    if (entry != null)
                    {
                        using var stream = entry.Open();
                        using var reader = new StreamReader(stream);
                        var xml = reader.ReadToEnd();
                        
                        ExtractTextFromXml(xml, sb);
                    }
                }
                
                return sb.ToString().Trim();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract Office text: {ex.Message}");
            return string.Empty;
        }
    }
    
    private static void ExtractTextFromXml(string xml, StringBuilder sb)
    {
        // Remove XML tags and extract text content
        var textMatches = System.Text.RegularExpressions.Regex.Matches(xml, @">([^<]+)<");
        foreach (System.Text.RegularExpressions.Match match in textMatches)
        {
            var text = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("xml"))
            {
                sb.AppendLine(text);
            }
        }
    }
    
    /// <summary>
    /// Extracts text from any supported document type.
    /// </summary>
    public async Task<string> ExtractTextAsync(string path, int maxLength = 10000)
    {
        if (!File.Exists(path))
            return string.Empty;
        
        if (IsPdfFile(path))
        {
            return await ExtractPdfTextAsync(path, maxLength);
        }
        else if (IsOfficeDocument(path))
        {
            return await ExtractOfficeTextAsync(path, maxLength);
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Generates a thumbnail for a PDF using external tools (pdftoppm or pdftoimage).
    /// </summary>
    public async Task<string?> GeneratePdfThumbnailAsync(string path, int width = 200)
    {
        if (!IsPdfFile(path) || !File.Exists(path))
            return null;
        
        try
        {
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XCommander", "DocumentThumbnails");
            
            Directory.CreateDirectory(cacheDir);
            
            var hash = GetFileHash(path);
            var thumbnailPath = Path.Combine(cacheDir, $"{hash}.png");
            
            // Return cached thumbnail if it exists
            if (File.Exists(thumbnailPath))
                return thumbnailPath;
            
            // Try pdftoppm (from poppler-utils)
            var pdftoppm = await FindExecutableAsync("pdftoppm");
            if (!string.IsNullOrEmpty(pdftoppm))
            {
                var tempOutput = Path.Combine(cacheDir, hash);
                var psi = new ProcessStartInfo
                {
                    FileName = pdftoppm,
                    Arguments = $"-png -f 1 -l 1 -scale-to {width} \"{path}\" \"{tempOutput}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    // pdftoppm adds page number suffix
                    var outputFile = $"{tempOutput}-1.png";
                    if (File.Exists(outputFile))
                    {
                        File.Move(outputFile, thumbnailPath);
                        return thumbnailPath;
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to generate PDF thumbnail: {ex.Message}");
            return null;
        }
    }
    
    private static async Task<string?> FindExecutableAsync(string name)
    {
        return await Task.Run(() =>
        {
            // Check common paths on macOS/Linux
            var paths = new[]
            {
                $"/usr/bin/{name}",
                $"/usr/local/bin/{name}",
                $"/opt/homebrew/bin/{name}"
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
            
            // Try which command
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = name,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        return output;
                }
            }
            catch
            {
                // Ignore errors finding executable
            }
            
            return null;
        });
    }
    
    private static string GetFileHash(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(path + File.GetLastWriteTimeUtc(path).Ticks);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}

/// <summary>
/// PDF document information.
/// </summary>
public class PdfInfo
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public long FileSize { get; set; }
    
    public string FileSizeDisplay => FormatSize(FileSize);
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Office document information.
/// </summary>
public class OfficeDocumentInfo
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public long FileSize { get; set; }
    
    public string FileSizeDisplay => FormatSize(FileSize);
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
