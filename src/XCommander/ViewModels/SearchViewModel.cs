using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private CancellationTokenSource? _cancellationTokenSource;
    
    [ObservableProperty]
    private string _searchPath = string.Empty;
    
    [ObservableProperty]
    private string _searchPattern = "*.*";
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private bool _searchInSubfolders = true;
    
    [ObservableProperty]
    private bool _caseSensitive;
    
    [ObservableProperty]
    private bool _useRegex;
    
    [ObservableProperty]
    private bool _searchInContent;
    
    [ObservableProperty]
    private bool _searchInArchives;
    
    [ObservableProperty]
    private bool _searchHiddenFiles;
    
    [ObservableProperty]
    private DateTime? _dateFrom;
    
    [ObservableProperty]
    private DateTime? _dateTo;
    
    [ObservableProperty]
    private long? _sizeFrom;
    
    [ObservableProperty]
    private long? _sizeTo;
    
    [ObservableProperty]
    private string _attributes = string.Empty;
    
    [ObservableProperty]
    private bool _isSearching;
    
    [ObservableProperty]
    private string _statusText = "Ready";
    
    [ObservableProperty]
    private int _filesFound;
    
    [ObservableProperty]
    private int _filesSearched;
    
    [ObservableProperty]
    private SearchResultItem? _selectedResult;
    
    public ObservableCollection<SearchResultItem> Results { get; } = [];
    
    /// <summary>
    /// Event raised when user wants to feed results to panel.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? FeedToPanelRequested;
    
    /// <summary>
    /// Event raised when user wants to navigate to a result.
    /// </summary>
    public event EventHandler<string>? NavigateRequested;
    
    public SearchViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
    
    public void Initialize(string startPath)
    {
        SearchPath = startPath;
    }
    
    [RelayCommand]
    public async Task StartSearchAsync()
    {
        if (IsSearching)
            return;
            
        if (string.IsNullOrEmpty(SearchPath) || !Directory.Exists(SearchPath))
        {
            StatusText = "Invalid search path";
            return;
        }
        
        Results.Clear();
        FilesFound = 0;
        FilesSearched = 0;
        IsSearching = true;
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            StatusText = "Searching...";
            await SearchDirectoryAsync(SearchPath, _cancellationTokenSource.Token);
            StatusText = $"Search complete. Found {FilesFound} items.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Search cancelled. Found {FilesFound} items.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
    
    [RelayCommand]
    public void StopSearch()
    {
        _cancellationTokenSource?.Cancel();
    }
    
    [RelayCommand]
    public void ClearResults()
    {
        Results.Clear();
        FilesFound = 0;
        FilesSearched = 0;
        StatusText = "Ready";
    }
    
    [RelayCommand]
    public void FeedToPanel()
    {
        if (Results.Count == 0)
            return;
            
        var paths = Results.Select(r => r.FullPath).ToList();
        FeedToPanelRequested?.Invoke(this, paths);
    }
    
    [RelayCommand]
    public void GoToResult()
    {
        if (SelectedResult == null)
            return;
            
        var path = SelectedResult.IsDirectory 
            ? SelectedResult.FullPath 
            : SelectedResult.Directory;
            
        NavigateRequested?.Invoke(this, path);
    }
    
    [RelayCommand]
    public void SelectAll()
    {
        // This would require multi-selection support in the view
    }
    
    private async Task SearchDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        IEnumerable<string> entries;
        
        try
        {
            entries = Directory.EnumerateFileSystemEntries(path);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var isDirectory = Directory.Exists(entry);
                var name = Path.GetFileName(entry);
                
                // Check if hidden
                var attr = File.GetAttributes(entry);
                var isHidden = (attr & FileAttributes.Hidden) != 0;
                
                if (isHidden && !SearchHiddenFiles)
                    continue;
                
                // Check pattern match
                if (MatchesPattern(name))
                {
                    bool contentMatch = true;
                    
                    // Content search for files
                    if (!isDirectory && SearchInContent && !string.IsNullOrEmpty(SearchText))
                    {
                        contentMatch = await SearchInFileContentAsync(entry, cancellationToken);
                    }
                    
                    // Check additional filters
                    if (contentMatch && MatchesFilters(entry, isDirectory))
                    {
                        FilesFound++;
                        var fileInfo = new FileInfo(entry);
                        var result = new SearchResultItem
                        {
                            Name = name,
                            FullPath = entry,
                            Directory = Path.GetDirectoryName(entry) ?? string.Empty,
                            Size = isDirectory ? 0 : fileInfo.Length,
                            DateModified = fileInfo.LastWriteTime,
                            IsDirectory = isDirectory
                        };
                        
                        // Update UI on main thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Results.Add(result);
                        });
                    }
                }
                
                FilesSearched++;
                
                // Update status every 100 files
                if (FilesSearched % 100 == 0)
                {
                    StatusText = $"Searching... {FilesSearched} files checked, {FilesFound} found";
                }
                
                // Recurse into subdirectories
                if (isDirectory && SearchInSubfolders)
                {
                    await SearchDirectoryAsync(entry, cancellationToken);
                }
            }
            catch (Exception)
            {
                // Skip inaccessible entries
            }
        }
    }
    
    private bool MatchesPattern(string fileName)
    {
        if (string.IsNullOrEmpty(SearchPattern) || SearchPattern == "*.*" || SearchPattern == "*")
        {
            // Also check text search in filename if not doing content search
            if (!SearchInContent && !string.IsNullOrEmpty(SearchText))
            {
                return MatchesTextSearch(fileName);
            }
            return true;
        }
        
        // Convert glob pattern to regex
        var patterns = SearchPattern.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pattern in patterns)
        {
            var regexPattern = "^" + Regex.Escape(pattern.Trim())
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
            
            var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            if (Regex.IsMatch(fileName, regexPattern, options))
            {
                // Also check text search in filename if not doing content search
                if (!SearchInContent && !string.IsNullOrEmpty(SearchText))
                {
                    return MatchesTextSearch(fileName);
                }
                return true;
            }
        }
        
        return false;
    }
    
    private bool MatchesTextSearch(string text)
    {
        if (UseRegex)
        {
            try
            {
                var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.IsMatch(text, SearchText, options);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            var comparison = CaseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
            return text.Contains(SearchText, comparison);
        }
    }
    
    private async Task<bool> SearchInFileContentAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SearchText))
            return true;
            
        try
        {
            // Skip large files (>100MB)
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 100 * 1024 * 1024)
                return false;
                
            // Skip binary files by extension
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var binaryExtensions = new HashSet<string> 
            { 
                ".exe", ".dll", ".bin", ".zip", ".rar", ".7z", ".tar", ".gz",
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".svg",
                ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
            };
            
            if (binaryExtensions.Contains(ext))
                return false;
            
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return MatchesTextSearch(content);
        }
        catch
        {
            return false;
        }
    }
    
    private bool MatchesFilters(string path, bool isDirectory)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            
            // Date filter
            if (DateFrom.HasValue && fileInfo.LastWriteTime < DateFrom.Value)
                return false;
            if (DateTo.HasValue && fileInfo.LastWriteTime > DateTo.Value)
                return false;
            
            // Size filter (for files only)
            if (!isDirectory)
            {
                if (SizeFrom.HasValue && fileInfo.Length < SizeFrom.Value)
                    return false;
                if (SizeTo.HasValue && fileInfo.Length > SizeTo.Value)
                    return false;
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class SearchResultItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDirectory { get; set; }
    
    public string Icon => IsDirectory ? "📁" : "📄";
    public string DisplaySize => IsDirectory ? "<DIR>" : FormatSize(Size);
    
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
