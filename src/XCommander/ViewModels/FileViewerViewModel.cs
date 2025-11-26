using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class FileViewerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private string _content = string.Empty;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private ViewMode _viewMode = ViewMode.Text;
    
    [ObservableProperty]
    private Encoding _encoding = Encoding.UTF8;
    
    [ObservableProperty]
    private bool _wordWrap = true;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private int _currentMatch;
    
    [ObservableProperty]
    private int _totalMatches;
    
    [ObservableProperty]
    private bool _isHex;
    
    [ObservableProperty]
    private long _fileSize;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private bool _isImage;
    
    [ObservableProperty]
    private string? _imagePath;
    
    private byte[]? _fileBytes;
    private readonly List<int> _matchPositions = [];
    
    public string[] AvailableEncodings { get; } = ["UTF-8", "ASCII", "UTF-16 LE", "UTF-16 BE", "Latin-1"];
    
    public FileViewerViewModel()
    {
    }
    
    public async Task LoadFileAsync(string path)
    {
        if (!File.Exists(path))
            return;
            
        IsLoading = true;
        FilePath = path;
        
        try
        {
            var fileInfo = new FileInfo(path);
            FileSize = fileInfo.Length;
            
            // Check if it's an image
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var imageExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp" };
            
            if (imageExtensions.Contains(extension))
            {
                IsImage = true;
                ImagePath = path;
                ViewMode = ViewMode.Image;
                StatusText = $"Image: {fileInfo.Length:N0} bytes";
                return;
            }
            
            IsImage = false;
            ImagePath = null;
            
            // For large files, only load first portion
            const long maxTextSize = 10 * 1024 * 1024; // 10 MB
            const long maxHexSize = 1 * 1024 * 1024; // 1 MB for hex view
            
            if (FileSize > maxTextSize && ViewMode == ViewMode.Text)
            {
                StatusText = $"File too large for text view ({FormatSize(FileSize)}). Showing first {FormatSize(maxTextSize)}.";
                _fileBytes = new byte[maxTextSize];
                using var fs = File.OpenRead(path);
                await fs.ReadAsync(_fileBytes.AsMemory(0, (int)maxTextSize));
            }
            else if (FileSize > maxHexSize && ViewMode == ViewMode.Hex)
            {
                StatusText = $"File too large for hex view ({FormatSize(FileSize)}). Showing first {FormatSize(maxHexSize)}.";
                _fileBytes = new byte[maxHexSize];
                using var fs = File.OpenRead(path);
                await fs.ReadAsync(_fileBytes.AsMemory(0, (int)maxHexSize));
            }
            else
            {
                _fileBytes = await File.ReadAllBytesAsync(path);
                StatusText = $"Size: {FormatSize(FileSize)}";
            }
            
            UpdateContent();
        }
        catch (Exception ex)
        {
            Content = $"Error loading file: {ex.Message}";
            StatusText = "Error loading file";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void UpdateContent()
    {
        if (_fileBytes == null)
            return;
            
        if (ViewMode == ViewMode.Hex)
        {
            Content = GenerateHexView(_fileBytes);
            IsHex = true;
        }
        else
        {
            Content = Encoding.GetString(_fileBytes);
            IsHex = false;
        }
        
        UpdateSearchMatches();
    }
    
    partial void OnViewModeChanged(ViewMode value)
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            UpdateContent();
        }
    }
    
    partial void OnEncodingChanged(Encoding value)
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            UpdateContent();
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        UpdateSearchMatches();
    }
    
    [RelayCommand]
    public void SetEncodingByName(string encodingName)
    {
        Encoding = encodingName switch
        {
            "UTF-8" => Encoding.UTF8,
            "ASCII" => Encoding.ASCII,
            "UTF-16 LE" => Encoding.Unicode,
            "UTF-16 BE" => Encoding.BigEndianUnicode,
            "Latin-1" => Encoding.Latin1,
            _ => Encoding.UTF8
        };
    }
    
    [RelayCommand]
    public void SwitchToText()
    {
        ViewMode = ViewMode.Text;
    }
    
    [RelayCommand]
    public void SwitchToHex()
    {
        ViewMode = ViewMode.Hex;
    }
    
    [RelayCommand]
    public void ToggleWordWrap()
    {
        WordWrap = !WordWrap;
    }
    
    [RelayCommand]
    public void FindNext()
    {
        if (_matchPositions.Count == 0 || TotalMatches == 0)
            return;
            
        CurrentMatch = (CurrentMatch % TotalMatches) + 1;
        // The view will scroll to the match position
    }
    
    [RelayCommand]
    public void FindPrevious()
    {
        if (_matchPositions.Count == 0 || TotalMatches == 0)
            return;
            
        CurrentMatch = CurrentMatch <= 1 ? TotalMatches : CurrentMatch - 1;
        // The view will scroll to the match position
    }
    
    private void UpdateSearchMatches()
    {
        _matchPositions.Clear();
        
        if (string.IsNullOrEmpty(SearchText) || string.IsNullOrEmpty(Content))
        {
            TotalMatches = 0;
            CurrentMatch = 0;
            return;
        }
        
        int index = 0;
        while ((index = Content.IndexOf(SearchText, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            _matchPositions.Add(index);
            index += SearchText.Length;
        }
        
        TotalMatches = _matchPositions.Count;
        CurrentMatch = TotalMatches > 0 ? 1 : 0;
    }
    
    public int GetMatchPosition(int matchIndex)
    {
        if (matchIndex <= 0 || matchIndex > _matchPositions.Count)
            return -1;
        return _matchPositions[matchIndex - 1];
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

public enum ViewMode
{
    Text,
    Hex,
    Image
}
