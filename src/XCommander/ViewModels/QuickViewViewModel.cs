using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class QuickViewViewModel : ViewModelBase
{
    private readonly DocumentPreviewService _documentPreviewService = new();
    
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private string _fileName = string.Empty;
    
    [ObservableProperty]
    private string _content = string.Empty;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private bool _isImage;
    
    [ObservableProperty]
    private string? _imagePath;
    
    [ObservableProperty]
    private bool _isText;
    
    [ObservableProperty]
    private bool _isHex;
    
    [ObservableProperty]
    private bool _isDocument;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private bool _hasContent;
    
    [ObservableProperty]
    private long _fileSize;
    
    [ObservableProperty]
    private DateTime _dateModified;
    
    [ObservableProperty]
    private string _fileType = string.Empty;
    
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".svg"
    };
    
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".fs", ".vb", ".java", ".py", ".rb", ".php", ".go", ".rs", ".swift",
        ".c", ".cpp", ".h", ".hpp", ".m", ".mm", ".sh", ".bash", ".zsh", ".ps1",
        ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".config",
        ".sql", ".log", ".csv", ".tsv", ".axaml", ".xaml", ".csproj", ".sln",
        ".gitignore", ".editorconfig", ".dockerfile", "makefile"
    };
    
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".odt", ".ods", ".odp", ".rtf"
    };
    
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bin", ".dat", ".db", ".sqlite", ".so", ".dylib",
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".wav", ".flac", ".ogg"
    };
    
    public QuickViewViewModel()
    {
    }
    
    public async Task LoadPreviewAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Clear();
            return;
        }
        
        IsLoading = true;
        FilePath = path;
        FileName = Path.GetFileName(path);
        
        try
        {
            var fileInfo = new FileInfo(path);
            FileSize = fileInfo.Length;
            DateModified = fileInfo.LastWriteTime;
            
            var extension = Path.GetExtension(path).ToLowerInvariant();
            
            // Determine file type
            if (ImageExtensions.Contains(extension))
            {
                await LoadImagePreviewAsync(path);
            }
            else if (DocumentExtensions.Contains(extension))
            {
                await LoadDocumentPreviewAsync(path);
            }
            else if (TextExtensions.Contains(extension) || IsLikelyTextFile(path, extension))
            {
                await LoadTextPreviewAsync(path);
            }
            else if (BinaryExtensions.Contains(extension))
            {
                await LoadHexPreviewAsync(path);
            }
            else
            {
                // Try to detect if it's text or binary
                if (await IsTextFileAsync(path))
                {
                    await LoadTextPreviewAsync(path);
                }
                else
                {
                    await LoadHexPreviewAsync(path);
                }
            }
            
            HasContent = true;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Content = $"Error loading preview: {ex.Message}";
            StatusText = "Error";
            IsText = true;
            HasContent = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task LoadImagePreviewAsync(string path)
    {
        IsImage = true;
        IsText = false;
        IsHex = false;
        IsDocument = false;
        ImagePath = path;
        FileType = "Image";
        Content = string.Empty;
        await Task.CompletedTask;
    }
    
    private async Task LoadDocumentPreviewAsync(string path)
    {
        IsImage = false;
        IsText = false;
        IsHex = false;
        IsDocument = true;
        ImagePath = null;
        
        var extension = Path.GetExtension(path).ToLowerInvariant();
        FileType = extension switch
        {
            ".pdf" => "PDF Document",
            ".doc" or ".docx" => "Word Document",
            ".xls" or ".xlsx" => "Excel Spreadsheet",
            ".ppt" or ".pptx" => "PowerPoint Presentation",
            ".odt" => "OpenDocument Text",
            ".ods" => "OpenDocument Spreadsheet",
            ".odp" => "OpenDocument Presentation",
            ".rtf" => "Rich Text Format",
            _ => "Document"
        };
        
        // Try to extract text from the document
        var text = await _documentPreviewService.ExtractTextAsync(path);
        
        if (!string.IsNullOrEmpty(text))
        {
            Content = text;
        }
        else
        {
            // Show document info if text extraction fails
            Content = $"Document Preview\n" +
                      $"================\n\n" +
                      $"File: {FileName}\n" +
                      $"Type: {FileType}\n" +
                      $"Size: {FormatSize(FileSize)}\n" +
                      $"Modified: {DateModified:yyyy-MM-dd HH:mm}\n\n" +
                      $"Note: Text extraction requires external tools:\n" +
                      $"- PDF: poppler-utils (pdftotext)\n" +
                      $"- Office: unzip utility for docx files\n\n" +
                      $"Install with: brew install poppler (macOS)\n" +
                      $"             apt install poppler-utils (Linux)";
        }
        
        // Try to generate a thumbnail for PDFs
        if (extension == ".pdf")
        {
            var thumbnailPath = await _documentPreviewService.GeneratePdfThumbnailAsync(path);
            if (thumbnailPath != null)
            {
                ImagePath = thumbnailPath;
                // Show image alongside text for PDFs
            }
        }
    }
    
    private async Task LoadTextPreviewAsync(string path)
    {
        IsImage = false;
        IsText = true;
        IsHex = false;
        IsDocument = false;
        ImagePath = null;
        FileType = "Text";
        
        // Limit preview size
        const int maxPreviewSize = 256 * 1024; // 256 KB
        
        if (FileSize > maxPreviewSize)
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[maxPreviewSize];
            var read = await fs.ReadAsync(buffer);
            Content = Encoding.UTF8.GetString(buffer, 0, read);
            Content += $"\n\n--- Preview truncated ({FormatSize(FileSize)} total) ---";
        }
        else
        {
            Content = await File.ReadAllTextAsync(path);
        }
    }
    
    private async Task LoadHexPreviewAsync(string path)
    {
        IsImage = false;
        IsText = false;
        IsHex = true;
        IsDocument = false;
        ImagePath = null;
        FileType = "Binary";
        
        // Limit hex preview size
        const int maxHexSize = 16 * 1024; // 16 KB
        
        var bytesToRead = (int)Math.Min(FileSize, maxHexSize);
        var buffer = new byte[bytesToRead];
        
        using (var fs = File.OpenRead(path))
        {
            await fs.ReadAsync(buffer);
        }
        
        Content = GenerateHexView(buffer);
        
        if (FileSize > maxHexSize)
        {
            Content += $"\n\n--- Preview truncated ({FormatSize(FileSize)} total) ---";
        }
    }
    
    private static async Task<bool> IsTextFileAsync(string path)
    {
        try
        {
            var buffer = new byte[8192];
            using var fs = File.OpenRead(path);
            var read = await fs.ReadAsync(buffer);
            
            // Check for null bytes (binary indicator)
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                    return false;
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsLikelyTextFile(string path, string extension)
    {
        // Files without extension might be text (like Makefile, Dockerfile)
        var fileName = Path.GetFileName(path).ToLowerInvariant();
        return fileName is "makefile" or "dockerfile" or "rakefile" or "gemfile" 
            or "procfile" or "vagrantfile" or "brewfile" or "readme" or "license"
            or "changelog" or "authors" or "contributing" or "todo";
    }
    
    private static string GenerateHexView(byte[] bytes)
    {
        var sb = new StringBuilder();
        const int bytesPerLine = 16;
        
        for (int i = 0; i < bytes.Length; i += bytesPerLine)
        {
            // Offset
            sb.Append($"{i:X8}  ");
            
            // Hex values
            for (int j = 0; j < bytesPerLine; j++)
            {
                if (i + j < bytes.Length)
                {
                    sb.Append($"{bytes[i + j]:X2} ");
                }
                else
                {
                    sb.Append("   ");
                }
                
                if (j == 7)
                    sb.Append(' ');
            }
            
            sb.Append(" ");
            
            // ASCII representation
            for (int j = 0; j < bytesPerLine && i + j < bytes.Length; j++)
            {
                byte b = bytes[i + j];
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private void UpdateStatus()
    {
        StatusText = $"{FileType} • {FormatSize(FileSize)} • {DateModified:yyyy-MM-dd HH:mm}";
    }
    
    [RelayCommand]
    public void Clear()
    {
        FilePath = string.Empty;
        FileName = string.Empty;
        Content = string.Empty;
        StatusText = string.Empty;
        IsImage = false;
        IsText = false;
        IsHex = false;
        IsDocument = false;
        ImagePath = null;
        HasContent = false;
        FileSize = 0;
        FileType = string.Empty;
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
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
}
