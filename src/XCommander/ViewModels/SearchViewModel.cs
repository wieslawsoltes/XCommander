using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Converters;
using XCommander.Helpers;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private const string NameColumnKey = "name";
    private const string DirectoryColumnKey = "directory";
    private const string SizeColumnKey = "size";
    private const string DateColumnKey = "date";

    private readonly IFileSystemService _fileSystemService;
    private readonly IAdvancedSearchService? _advancedSearchService;
    private readonly IArchiveService? _archiveService;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly SynchronizationContext? _syncContext;
    
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
    
    [ObservableProperty]
    private SavedSearchQuery? _selectedTemplate;
    
    [ObservableProperty]
    private string _newTemplateName = string.Empty;
    
    public ObservableCollection<SearchResultItem> Results { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    
    /// <summary>
    /// Saved search templates.
    /// </summary>
    public ObservableCollection<SavedSearchQuery> SavedTemplates { get; } = [];
    
    /// <summary>
    /// Event raised when user wants to feed results to panel.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? FeedToPanelRequested;
    
    /// <summary>
    /// Event raised when user wants to navigate to a result.
    /// </summary>
    public event EventHandler<string>? NavigateRequested;
    
    public SearchViewModel(
        IFileSystemService fileSystemService,
        AppSettings settings,
        IAdvancedSearchService? advancedSearchService = null,
        IArchiveService? archiveService = null)
    {
        _fileSystemService = fileSystemService;
        _settings = settings;
        _advancedSearchService = advancedSearchService;
        _archiveService = archiveService;
        _syncContext = SynchronizationContext.Current;
        SearchHiddenFiles = settings.SearchIncludeHidden;
        
        // Load saved templates
        _ = LoadSavedTemplatesAsync();

        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<SearchResultItem>();

        IPropertyInfo directoryProperty = DataGridColumnHelper.CreateProperty(
            nameof(SearchResultItem.Directory),
            (SearchResultItem item) => item.Directory);
        IPropertyInfo displaySizeProperty = DataGridColumnHelper.CreateProperty(
            nameof(SearchResultItem.DisplaySize),
            (SearchResultItem item) => item.DisplaySize);
        IPropertyInfo dateProperty = DataGridColumnHelper.CreateProperty(
            nameof(SearchResultItem.DateModified),
            (SearchResultItem item) => item.DateModified);

        var dateColumn = builder.Text(
            header: "Date Modified",
            property: dateProperty,
            getter: item => item.DateModified,
            configure: column =>
            {
                column.ColumnKey = DateColumnKey;
                column.Width = new DataGridLength(130);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
            });

        if (dateColumn.Binding != null)
        {
            dateColumn.Binding.Converter = DateTimeConverter.Instance;
        }

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Name",
                cellTemplateKey: "SearchResultNameTemplate",
                configure: column =>
                {
                    column.ColumnKey = NameColumnKey;
                    column.Width = new DataGridLength(250);
                    column.MinWidth = 150;
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<SearchResultItem, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                }),
            builder.Text(
                header: "Directory",
                property: directoryProperty,
                getter: item => item.Directory,
                configure: column =>
                {
                    column.ColumnKey = DirectoryColumnKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Size",
                property: displaySizeProperty,
                getter: item => item.DisplaySize,
                configure: column =>
                {
                    column.ColumnKey = SizeColumnKey;
                    column.Width = new DataGridLength(80);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SearchResultItem, long>(
                            item => item.Size)
                    };
                }),
            dateColumn
        };
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
            if (FilesFound >= _settings.SearchMaxResults)
            {
                StatusText = $"Search stopped at limit ({_settings.SearchMaxResults} results).";
            }
            else
            {
                StatusText = $"Search cancelled. Found {FilesFound} items.";
            }
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
                        
                        await PostToUiAsync(() => Results.Add(result));

                        if (FilesFound >= _settings.SearchMaxResults)
                        {
                            throw new OperationCanceledException();
                        }
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
                else if (!isDirectory && SearchInArchives)
                {
                    await SearchArchiveAsync(entry, cancellationToken);
                }
            }
            catch (Exception)
            {
                // Skip inaccessible entries
            }
        }
    }

    private Task PostToUiAsync(Action action)
    {
        if (_syncContext == null)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();
        _syncContext.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    private async Task SearchArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        if (_archiveService == null || !_archiveService.IsArchive(archivePath))
            return;

        List<ArchiveEntry> entries;
        try
        {
            entries = await _archiveService.ListEntriesAsync(archivePath, cancellationToken);
        }
        catch
        {
            return;
        }

        var archiveName = Path.GetFileName(archivePath);
        var archiveDirectory = Path.GetDirectoryName(archivePath) ?? string.Empty;

        var requiresContentMatch = SearchInContent && !string.IsNullOrEmpty(SearchText);
        var tempRoot = requiresContentMatch
            ? Path.Combine(Path.GetTempPath(), "XCommander", "search", Guid.NewGuid().ToString("N"))
            : string.Empty;

        try
        {
            if (requiresContentMatch)
                Directory.CreateDirectory(tempRoot);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                    continue;

                if (!SearchInSubfolders && EntryHasSubfolder(entry))
                    continue;

                if (!MatchesPattern(entry.Name))
                    continue;

                if (!MatchesArchiveFilters(entry))
                    continue;

                if (requiresContentMatch && _archiveService != null)
                {
                    if (!await MatchesArchiveContentAsync(archivePath, entry, tempRoot, cancellationToken))
                        continue;
                }

                FilesFound++;
                FilesSearched++;

                var entryDisplay = string.IsNullOrWhiteSpace(entry.Path) ? entry.Name : entry.Path;
                var result = new SearchResultItem
                {
                    Name = $"{entryDisplay} ({archiveName})",
                    FullPath = archivePath,
                    Directory = archiveDirectory,
                    Size = entry.Size,
                    DateModified = entry.LastModified ?? File.GetLastWriteTime(archivePath),
                    IsDirectory = false
                };

                await PostToUiAsync(() => Results.Add(result));

                if (FilesFound >= _settings.SearchMaxResults)
                {
                    throw new OperationCanceledException();
                }
            }
        }
        finally
        {
            if (requiresContentMatch && Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }
    }

    private async Task<bool> MatchesArchiveContentAsync(string archivePath, ArchiveEntry entry, string tempRoot, CancellationToken cancellationToken)
    {
        if (_archiveService == null || string.IsNullOrWhiteSpace(entry.Path))
            return false;

        var entryPath = entry.Path.Replace('/', Path.DirectorySeparatorChar);
        var extractPath = Path.Combine(tempRoot, entryPath);
        var extractDir = Path.GetDirectoryName(extractPath);
        if (!string.IsNullOrWhiteSpace(extractDir))
        {
            Directory.CreateDirectory(extractDir);
        }

        try
        {
            await _archiveService.ExtractEntriesAsync(
                archivePath,
                new[] { entry.Path },
                tempRoot,
                cancellationToken: cancellationToken);

            if (!File.Exists(extractPath))
                return false;

            return await SearchInFileContentAsync(extractPath, cancellationToken);
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(extractPath))
                    File.Delete(extractPath);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }

    private static bool EntryHasSubfolder(ArchiveEntry entry)
    {
        var path = entry.Path;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Contains('/') || path.Contains('\\');
    }

    private bool MatchesArchiveFilters(ArchiveEntry entry)
    {
        if (SizeFrom.HasValue && entry.Size < SizeFrom.Value)
            return false;
        if (SizeTo.HasValue && entry.Size > SizeTo.Value)
            return false;

        if (DateFrom.HasValue || DateTo.HasValue)
        {
            if (!entry.LastModified.HasValue)
                return false;

            if (DateFrom.HasValue && entry.LastModified.Value < DateFrom.Value)
                return false;
            if (DateTo.HasValue && entry.LastModified.Value > DateTo.Value)
                return false;
        }

        return true;
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
    
    #region Template Management
    
    /// <summary>
    /// Load saved search templates.
    /// </summary>
    private async Task LoadSavedTemplatesAsync()
    {
        if (_advancedSearchService == null) return;
        
        try
        {
            var templates = await _advancedSearchService.GetSavedQueriesAsync();
            SavedTemplates.Clear();
            foreach (var template in templates)
            {
                SavedTemplates.Add(template);
            }
        }
        catch { /* Ignore errors */ }
    }
    
    /// <summary>
    /// Save current search as a template.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsTemplateAsync()
    {
        if (_advancedSearchService == null) return;
        if (string.IsNullOrWhiteSpace(NewTemplateName)) return;
        
        var criteria = CreateCriteriaFromCurrentSearch();
        var template = new SavedSearchQuery
        {
            Name = NewTemplateName.Trim(),
            Criteria = criteria,
            Created = DateTime.Now,
            LastUsed = DateTime.Now
        };
        
        await _advancedSearchService.SaveQueryAsync(template);
        await LoadSavedTemplatesAsync();
        NewTemplateName = string.Empty;
        StatusText = $"Template '{template.Name}' saved.";
    }
    
    /// <summary>
    /// Load a saved template.
    /// </summary>
    [RelayCommand]
    private async Task LoadTemplateAsync(SavedSearchQuery? template)
    {
        if (template == null) return;
        
        // Apply template settings
        ApplyCriteriaToCurrentSearch(template.Criteria);
        
        // Update usage
        if (_advancedSearchService != null)
        {
            await _advancedSearchService.UpdateQueryUsageAsync(template.Id);
        }
        
        StatusText = $"Template '{template.Name}' loaded.";
    }
    
    /// <summary>
    /// Delete a saved template.
    /// </summary>
    [RelayCommand]
    private async Task DeleteTemplateAsync(SavedSearchQuery? template)
    {
        if (template == null || _advancedSearchService == null) return;
        
        await _advancedSearchService.DeleteQueryAsync(template.Id);
        await LoadSavedTemplatesAsync();
        StatusText = $"Template '{template.Name}' deleted.";
    }
    
    private AdvancedSearchCriteria CreateCriteriaFromCurrentSearch()
    {
        return new AdvancedSearchCriteria
        {
            SearchPath = SearchPath,
            FileNamePattern = SearchPattern,
            ContentPattern = SearchText,
            IncludeSubdirectories = SearchInSubfolders,
            ContentCaseSensitive = CaseSensitive,
            ContentRegex = UseRegex,
            IsHidden = SearchHiddenFiles ? null : false,
            ModifiedAfter = DateFrom,
            ModifiedBefore = DateTo,
            MinSize = SizeFrom,
            MaxSize = SizeTo
        };
    }
    
    private void ApplyCriteriaToCurrentSearch(AdvancedSearchCriteria criteria)
    {
        SearchPath = criteria.SearchPath ?? string.Empty;
        SearchPattern = criteria.FileNamePattern ?? "*.*";
        SearchText = criteria.ContentPattern ?? string.Empty;
        SearchInSubfolders = criteria.IncludeSubdirectories;
        CaseSensitive = criteria.ContentCaseSensitive;
        UseRegex = criteria.ContentRegex;
        SearchHiddenFiles = criteria.IsHidden != false;
        DateFrom = criteria.ModifiedAfter;
        DateTo = criteria.ModifiedBefore;
        SizeFrom = criteria.MinSize;
        SizeTo = criteria.MaxSize;
    }
    
    #endregion
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
