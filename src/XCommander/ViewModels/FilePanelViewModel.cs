using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Converters;
using XCommander.Helpers;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public enum FilePanelViewMode
{
    Details,
    List,
    Thumbnails
}

public partial class FileItemViewModel : ViewModelBase
{
    private static readonly VideoThumbnailService _videoThumbnailService = new();
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _thumbnail;
    
    [ObservableProperty]
    private bool _thumbnailLoading;
    
    [ObservableProperty]
    private bool _thumbnailLoaded;
    
    [ObservableProperty]
    private string _gitStatusIcon = string.Empty;
    
    [ObservableProperty]
    private string _gitStatusText = string.Empty;
    
    [ObservableProperty]
    private string _videoDuration = string.Empty;
    
    [ObservableProperty]
    private long _calculatedSize = -1;  // -1 means not calculated
    
    /// <summary>
    /// File description from descript.ion (TC-style comments).
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;
    
    /// <summary>
    /// Indicates if the description has been loaded.
    /// </summary>
    [ObservableProperty]
    private bool _descriptionLoaded;
    
    public FileSystemItem Item { get; }
    
    public string Name => Item.Name;
    public string FullPath => Item.FullPath;
    public FileSystemItemType ItemType => Item.ItemType;
    public long Size => CalculatedSize >= 0 ? CalculatedSize : Item.Size;
    public DateTime DateModified => Item.DateModified;
    public string Extension => Item.Extension;
    public string DisplaySize => CalculatedSize >= 0 
        ? FormatFileSize(CalculatedSize) 
        : Item.DisplaySize;
    public bool IsDirectory => Item.IsDirectory;
    public bool IsHidden => Item.IsHidden;
    
    public bool IsImageFile => IsImageExtension(Extension);
    public bool IsVideoFile => VideoThumbnailService.IsVideoFile(FullPath);
    public bool CanHaveThumbnail => IsImageFile || IsVideoFile;
    
    // RTL support
    public bool IsRtlName => RtlSupportService.ContainsRtl(Name);
    public FlowDirection NameFlowDirection => RtlSupportService.GetFlowDirection(Name);
    
    public string Icon => ItemType switch
    {
        FileSystemItemType.ParentDirectory => "📂",
        FileSystemItemType.Directory => "📁",
        FileSystemItemType.Drive => "💾",
        _ => GetFileIcon(Extension)
    };
    
    public FileItemViewModel(FileSystemItem item)
    {
        Item = item;
    }
    
    private static string FormatFileSize(long bytes)
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
    
    public async Task LoadThumbnailAsync(int maxSize = 128)
    {
        if (ThumbnailLoaded || ThumbnailLoading || !CanHaveThumbnail)
            return;
            
        ThumbnailLoading = true;
        
        try
        {
            if (IsImageFile)
            {
                await LoadImageThumbnailAsync(maxSize);
            }
            else if (IsVideoFile)
            {
                await LoadVideoThumbnailAsync(maxSize);
            }
        }
        finally
        {
            ThumbnailLoading = false;
            ThumbnailLoaded = true;
        }
    }
    
    private async Task LoadImageThumbnailAsync(int maxSize)
    {
        await Task.Run(() =>
        {
            try
            {
                using var stream = System.IO.File.OpenRead(FullPath);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                
                // Scale down if needed
                var scale = Math.Min((double)maxSize / bitmap.PixelSize.Width, (double)maxSize / bitmap.PixelSize.Height);
                if (scale < 1)
                {
                    var newWidth = (int)(bitmap.PixelSize.Width * scale);
                    var newHeight = (int)(bitmap.PixelSize.Height * scale);
                    
                    // Create scaled bitmap
                    Thumbnail = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(newWidth, newHeight));
                }
                else
                {
                    Thumbnail = bitmap;
                }
            }
            catch
            {
                // Ignore thumbnail loading errors
            }
        });
    }
    
    private async Task LoadVideoThumbnailAsync(int maxSize)
    {
        if (!_videoThumbnailService.IsAvailable)
            return;
        
        try
        {
            var thumbnail = await _videoThumbnailService.GenerateThumbnailAsync(FullPath, maxSize);
            if (thumbnail != null)
            {
                Thumbnail = thumbnail;
                
                // Also get video duration for overlay
                var info = await _videoThumbnailService.GetVideoInfoAsync(FullPath);
                if (info != null)
                {
                    VideoDuration = info.DurationDisplay;
                }
            }
        }
        catch
        {
            // Ignore video thumbnail errors
        }
    }
    
    private static bool IsImageExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => true,
            _ => false
        };
    }
    
    private static string GetFileIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".log" => "📄",
            ".pdf" => "📕",
            ".doc" or ".docx" => "📘",
            ".xls" or ".xlsx" => "📗",
            ".ppt" or ".pptx" => "📙",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
            ".exe" or ".msi" => "⚙️",
            ".dll" or ".so" or ".dylib" => "🔧",
            ".cs" or ".java" or ".py" or ".js" or ".ts" => "📜",
            ".html" or ".htm" or ".css" => "🌐",
            ".json" or ".xml" or ".yaml" or ".yml" => "📋",
            _ => "📄"
        };
    }
}

public partial class FilePanelViewModel : ViewModelBase
{
    private const string NameColumnKey = "name";
    private const string ExtensionColumnKey = "extension";
    private const string SizeColumnKey = "size";
    private const string DateColumnKey = "date";
    private const string NameHeaderThemeKey = "FilePanelNameColumnHeaderTheme";
    private const string ExtensionHeaderThemeKey = "FilePanelExtensionColumnHeaderTheme";
    private const string SizeHeaderThemeKey = "FilePanelSizeColumnHeaderTheme";
    private const string DateHeaderThemeKey = "FilePanelDateColumnHeaderTheme";

    private readonly IFileSystemService _fileSystemService;
    private readonly GitService _gitService = new();
    
    [ObservableProperty]
    private string _currentPath = string.Empty;
    
    [ObservableProperty]
    private bool _isActive;
    
    [ObservableProperty]
    private bool _showHiddenFiles;
    
    [ObservableProperty]
    private FileItemViewModel? _selectedItem;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private string _sortColumn = "Name";
    
    [ObservableProperty]
    private bool _sortAscending = true;
    
    [ObservableProperty]
    private FilePanelViewMode _viewMode = FilePanelViewMode.Details;
    
    [ObservableProperty]
    private bool _showGitStatus = true;
    
    [ObservableProperty]
    private GitRepositoryInfo? _gitRepositoryInfo;
    
    public ObservableCollection<FileItemViewModel> Items { get; } = [];
    public ObservableCollection<FileItemViewModel> SelectedItems { get; } = [];
    public ObservableCollection<DriveItem> Drives { get; } = [];
    public ObservableCollection<string> PathSegments { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    public TextFilterContext NameFilter { get; }
    public TextFilterContext ExtensionFilter { get; }
    public NumberFilterContext SizeFilter { get; }
    public DateFilterContext DateFilter { get; }
    
    // Navigation history
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;
    
    public FilePanelViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        LoadDrives();
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        NameFilter = new TextFilterContext(
            "Name contains",
            text => ApplyTextFilter(NameColumnKey, text),
            () => FilteringModel.Remove(NameColumnKey));
        ExtensionFilter = new TextFilterContext(
            "Extension contains",
            text => ApplyTextFilter(ExtensionColumnKey, text),
            () => FilteringModel.Remove(ExtensionColumnKey));
        SizeFilter = new NumberFilterContext(
            "Size between",
            (min, max) => ApplyNumberFilter(SizeColumnKey, min, max),
            () => FilteringModel.Remove(SizeColumnKey));
        DateFilter = new DateFilterContext(
            "Modified between",
            (from, to) => ApplyDateFilter(DateColumnKey, from, to),
            () => FilteringModel.Remove(DateColumnKey));
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
        var builder = DataGridColumnDefinitionBuilder.For<FileItemViewModel>();

        IPropertyInfo nameProperty = DataGridColumnHelper.CreateProperty(
            nameof(FileItemViewModel.Name),
            (FileItemViewModel item) => item.Name);
        IPropertyInfo extensionProperty = DataGridColumnHelper.CreateProperty(
            nameof(FileItemViewModel.Extension),
            (FileItemViewModel item) => item.Extension);
        IPropertyInfo displaySizeProperty = DataGridColumnHelper.CreateProperty(
            nameof(FileItemViewModel.DisplaySize),
            (FileItemViewModel item) => item.DisplaySize);
        IPropertyInfo dateProperty = DataGridColumnHelper.CreateProperty(
            nameof(FileItemViewModel.DateModified),
            (FileItemViewModel item) => item.DateModified);

        var dateColumn = builder.Text(
            header: "Date Modified",
            property: dateProperty,
            getter: item => item.DateModified,
            configure: column =>
            {
                column.ColumnKey = DateColumnKey;
                column.HeaderThemeKey = DateHeaderThemeKey;
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
                cellTemplateKey: "FileItemNameTemplate",
                configure: column =>
                {
                    column.ColumnKey = NameColumnKey;
                    column.HeaderThemeKey = NameHeaderThemeKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.MinWidth = 150;
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<FileItemViewModel, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                }),
            builder.Text(
                header: "Ext",
                property: extensionProperty,
                getter: item => item.Extension,
                configure: column =>
                {
                    column.ColumnKey = ExtensionColumnKey;
                    column.HeaderThemeKey = ExtensionHeaderThemeKey;
                    column.Width = new DataGridLength(60);
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
                    column.HeaderThemeKey = SizeHeaderThemeKey;
                    column.Width = new DataGridLength(80);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<FileItemViewModel, long>(
                            item => item.Size),
                        FilterValueAccessor = new DataGridColumnValueAccessor<FileItemViewModel, double>(
                            item => item.Size)
                    };
                }),
            dateColumn
        };
    }

    private void ApplyTextFilter(string columnKey, string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            FilteringModel.Remove(columnKey);
            return;
        }

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: columnKey,
            @operator: FilteringOperator.Contains,
            value: trimmed,
            stringComparison: StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyNumberFilter(string columnKey, double? min, double? max)
    {
        if (min == null && max == null)
        {
            FilteringModel.Remove(columnKey);
            return;
        }

        var lower = min ?? double.MinValue;
        var upper = max ?? double.MaxValue;

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: columnKey,
            @operator: FilteringOperator.Between,
            values: new object[] { lower, upper }));
    }

    private void ApplyDateFilter(string columnKey, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from == null && to == null)
        {
            FilteringModel.Remove(columnKey);
            return;
        }

        var start = from?.LocalDateTime ?? DateTime.MinValue;
        var end = to?.LocalDateTime ?? DateTime.MaxValue;

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: columnKey,
            @operator: FilteringOperator.Between,
            values: new object[] { start, end }));
    }
    
    public void LoadDrives()
    {
        Drives.Clear();
        foreach (var drive in _fileSystemService.GetDrives())
        {
            Drives.Add(drive);
        }
    }
    
    [RelayCommand]
    public void NavigateTo(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;
            
        // Add to history
        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }
        _history.Add(path);
        _historyIndex = _history.Count - 1;
        
        CurrentPath = path;
        RefreshItems();
        UpdatePathSegments();
        
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }
    
    [RelayCommand]
    public void GoBack()
    {
        if (CanGoBack)
        {
            _historyIndex--;
            CurrentPath = _history[_historyIndex];
            RefreshItems();
            UpdatePathSegments();
            
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }
    
    [RelayCommand]
    public void GoForward()
    {
        if (CanGoForward)
        {
            _historyIndex++;
            CurrentPath = _history[_historyIndex];
            RefreshItems();
            UpdatePathSegments();
            
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }
    
    [RelayCommand]
    public void GoToParent()
    {
        var parent = _fileSystemService.GetParentDirectory(CurrentPath);
        if (!string.IsNullOrEmpty(parent) && parent != CurrentPath)
        {
            NavigateTo(parent);
        }
    }
    
    [RelayCommand]
    public void GoToRoot()
    {
        var root = Path.GetPathRoot(CurrentPath);
        if (!string.IsNullOrEmpty(root))
        {
            NavigateTo(root);
        }
    }
    
    [RelayCommand]
    public void Refresh()
    {
        RefreshItems();
    }
    
    [RelayCommand]
    public void ToggleHiddenFiles()
    {
        ShowHiddenFiles = !ShowHiddenFiles;
        RefreshItems();
    }
    
    [RelayCommand]
    public void OpenItem(FileItemViewModel? item)
    {
        if (item == null)
            return;
            
        if (item.IsDirectory)
        {
            NavigateTo(item.FullPath);
        }
        else
        {
            // Open file with default application
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening file: {ex.Message}");
            }
        }
    }
    
    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in Items.Where(i => i.ItemType != FileSystemItemType.ParentDirectory))
        {
            item.IsSelected = true;
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
        }
        UpdateStatusText();
    }
    
    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }
        SelectedItems.Clear();
        UpdateStatusText();
    }
    
    [RelayCommand]
    public void InvertSelection()
    {
        foreach (var item in Items.Where(i => i.ItemType != FileSystemItemType.ParentDirectory))
        {
            item.IsSelected = !item.IsSelected;
            if (item.IsSelected && !SelectedItems.Contains(item))
                SelectedItems.Add(item);
            else if (!item.IsSelected)
                SelectedItems.Remove(item);
        }
        UpdateStatusText();
    }
    
    public void ToggleItemSelection(FileItemViewModel item)
    {
        if (item.ItemType == FileSystemItemType.ParentDirectory)
            return;
            
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected && !SelectedItems.Contains(item))
            SelectedItems.Add(item);
        else if (!item.IsSelected)
            SelectedItems.Remove(item);
        UpdateStatusText();
    }
    
    /// <summary>
    /// Selects all items from the current selection to the first item (Shift+Home in TC).
    /// </summary>
    public void SelectRangeToFirst()
    {
        if (SelectedItem == null || Items.Count == 0)
            return;
            
        var currentIndex = Items.IndexOf(SelectedItem);
        if (currentIndex < 0)
            return;
            
        for (var i = 0; i <= currentIndex; i++)
        {
            var item = Items[i];
            if (item.ItemType != FileSystemItemType.ParentDirectory)
            {
                item.IsSelected = true;
                if (!SelectedItems.Contains(item))
                    SelectedItems.Add(item);
            }
        }
        
        SelectedItem = Items[0];
        UpdateStatusText();
    }
    
    /// <summary>
    /// Selects all items from the current selection to the last item (Shift+End in TC).
    /// </summary>
    public void SelectRangeToLast()
    {
        if (SelectedItem == null || Items.Count == 0)
            return;
            
        var currentIndex = Items.IndexOf(SelectedItem);
        if (currentIndex < 0)
            return;
            
        for (var i = currentIndex; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.ItemType != FileSystemItemType.ParentDirectory)
            {
                item.IsSelected = true;
                if (!SelectedItems.Contains(item))
                    SelectedItems.Add(item);
            }
        }
        
        SelectedItem = Items[^1];
        UpdateStatusText();
    }
    
    [RelayCommand]
    public void SortBy(string column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }
        RefreshItems();
    }
    
    private void RefreshItems()
    {
        Items.Clear();
        SelectedItems.Clear();
        
        if (string.IsNullOrEmpty(CurrentPath))
            return;
            
        var items = _fileSystemService.GetDirectoryContents(CurrentPath, ShowHiddenFiles)
            .Select(i => new FileItemViewModel(i));
            
        // Sort items
        var sortedItems = SortItems(items).ToList();
        
        foreach (var item in sortedItems)
        {
            Items.Add(item);
        }
        
        // Update git status in background
        if (ShowGitStatus)
        {
            UpdateGitStatusAsync();
        }
        
        UpdateStatusText();
    }
    
    private async void UpdateGitStatusAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                GitRepositoryInfo = _gitService.GetRepositoryInfo(CurrentPath);
                
                if (GitRepositoryInfo != null)
                {
                    var statuses = _gitService.GetDirectoryStatus(CurrentPath);
                    
                    foreach (var item in Items)
                    {
                        if (statuses.TryGetValue(item.FullPath, out var status))
                        {
                            item.GitStatusIcon = status.StatusIcon;
                            item.GitStatusText = status.StatusText;
                        }
                        else
                        {
                            item.GitStatusIcon = string.Empty;
                            item.GitStatusText = string.Empty;
                        }
                    }
                }
                else
                {
                    foreach (var item in Items)
                    {
                        item.GitStatusIcon = string.Empty;
                        item.GitStatusText = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update git status: {ex.Message}");
            }
        });
    }
    
    [RelayCommand]
    public void ToggleGitStatus()
    {
        ShowGitStatus = !ShowGitStatus;
        RefreshItems();
    }
    
    private IEnumerable<FileItemViewModel> SortItems(IEnumerable<FileItemViewModel> items)
    {
        // Parent directory always first
        var parent = items.Where(i => i.ItemType == FileSystemItemType.ParentDirectory);
        var directories = items.Where(i => i.ItemType == FileSystemItemType.Directory);
        var files = items.Where(i => i.ItemType == FileSystemItemType.File);
        
        directories = SortCollection(directories);
        files = SortCollection(files);
        
        return parent.Concat(directories).Concat(files);
    }
    
    private IEnumerable<FileItemViewModel> SortCollection(IEnumerable<FileItemViewModel> items)
    {
        return SortColumn switch
        {
            "Name" => SortAscending ? items.OrderBy(i => i.Name) : items.OrderByDescending(i => i.Name),
            "Extension" => SortAscending ? items.OrderBy(i => i.Extension) : items.OrderByDescending(i => i.Extension),
            "Size" => SortAscending ? items.OrderBy(i => i.Size) : items.OrderByDescending(i => i.Size),
            "Date" => SortAscending ? items.OrderBy(i => i.DateModified) : items.OrderByDescending(i => i.DateModified),
            _ => items.OrderBy(i => i.Name)
        };
    }
    
    private void UpdatePathSegments()
    {
        PathSegments.Clear();
        if (string.IsNullOrEmpty(CurrentPath))
            return;
            
        var segments = CurrentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            PathSegments.Add(segment);
        }
    }
    
    private void UpdateStatusText()
    {
        var fileCount = Items.Count(i => i.ItemType == FileSystemItemType.File);
        var dirCount = Items.Count(i => i.ItemType == FileSystemItemType.Directory);
        var selectedCount = SelectedItems.Count;
        var selectedSize = SelectedItems.Where(i => !i.IsDirectory).Sum(i => i.Size);
        
        if (selectedCount > 0)
        {
            StatusText = $"{selectedCount} selected, {FormatSize(selectedSize)} in {fileCount} files, {dirCount} folders";
        }
        else
        {
            var totalSize = Items.Where(i => !i.IsDirectory).Sum(i => i.Size);
            StatusText = $"{FormatSize(totalSize)} in {fileCount} files, {dirCount} folders";
        }
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
    
    public IEnumerable<string> GetSelectedPaths()
    {
        if (SelectedItems.Count > 0)
        {
            return SelectedItems.Select(i => i.FullPath);
        }
        else if (SelectedItem != null && SelectedItem.ItemType != FileSystemItemType.ParentDirectory)
        {
            return [SelectedItem.FullPath];
        }
        return [];
    }
    
    [RelayCommand]
    public void SetViewMode(FilePanelViewMode mode)
    {
        ViewMode = mode;
    }
    
    [RelayCommand]
    public void CycleViewMode()
    {
        ViewMode = ViewMode switch
        {
            FilePanelViewMode.Details => FilePanelViewMode.List,
            FilePanelViewMode.List => FilePanelViewMode.Thumbnails,
            FilePanelViewMode.Thumbnails => FilePanelViewMode.Details,
            _ => FilePanelViewMode.Details
        };
    }
    
    /// <summary>
    /// Select item and move cursor to next item (Insert key behavior).
    /// </summary>
    public void SelectItemAndMoveNext()
    {
        if (SelectedItem == null || SelectedItem.ItemType == FileSystemItemType.ParentDirectory)
            return;
            
        // Toggle selection on current item
        SelectedItem.IsSelected = true;
        if (!SelectedItems.Contains(SelectedItem))
            SelectedItems.Add(SelectedItem);
            
        // Move to next item
        var currentIndex = Items.IndexOf(SelectedItem);
        if (currentIndex >= 0 && currentIndex < Items.Count - 1)
        {
            SelectedItem = Items[currentIndex + 1];
        }
        
        UpdateStatusText();
    }
    
    /// <summary>
    /// Open directory in new tab (middle-click behavior).
    /// </summary>
    [RelayCommand]
    public void OpenInNewTab(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
            
        // This will be handled by the TabbedFilePanel or MainWindowViewModel
        // For now, navigate to the path (will be enhanced later)
        NavigateTo(path);
    }
    
    /// <summary>
    /// Show file/folder properties dialog.
    /// </summary>
    [RelayCommand]
    public void ShowProperties()
    {
        // This will trigger an event that MainWindow will handle
        ShowPropertiesRequested?.Invoke(this, GetSelectedPaths().ToList());
    }
    
    /// <summary>
    /// Event raised when properties dialog is requested.
    /// </summary>
    public event EventHandler<List<string>>? ShowPropertiesRequested;
    
    /// <summary>
    /// Event raised to request copy operation.
    /// </summary>
    public event EventHandler<List<string>>? CopyRequested;
    
    /// <summary>
    /// Event raised to request move operation.
    /// </summary>
    public event EventHandler<List<string>>? MoveRequested;
    
    /// <summary>
    /// Event raised to request delete operation.
    /// </summary>
    public event EventHandler<List<string>>? DeleteRequested;
    
    /// <summary>
    /// Event raised to request rename operation.
    /// </summary>
    public event EventHandler<string>? RenameRequested;
    
    [RelayCommand]
    public void CopyToOtherPanel()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            CopyRequested?.Invoke(this, paths);
    }
    
    [RelayCommand]
    public void MoveToOtherPanel()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            MoveRequested?.Invoke(this, paths);
    }
    
    [RelayCommand]
    public void RenameSelected()
    {
        if (SelectedItem != null && SelectedItem.ItemType != FileSystemItemType.ParentDirectory)
            RenameRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    [RelayCommand]
    public void DeleteSelected()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            DeleteRequested?.Invoke(this, paths);
    }
    
    [RelayCommand]
    public void ViewSelected()
    {
        if (SelectedItem != null && !SelectedItem.IsDirectory)
            ViewFileRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    [RelayCommand]
    public void EditSelected()
    {
        if (SelectedItem != null && !SelectedItem.IsDirectory)
            EditFileRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    [RelayCommand]
    public void CreateNewFolder()
    {
        CreateFolderRequested?.Invoke(this, CurrentPath);
    }
    
    [RelayCommand]
    public void CreateNewFile()
    {
        CreateFileRequested?.Invoke(this, CurrentPath);
    }
    
    /// <summary>
    /// Event raised to request viewing a file.
    /// </summary>
    public event EventHandler<string>? ViewFileRequested;
    
    /// <summary>
    /// Event raised to request editing a file.
    /// </summary>
    public event EventHandler<string>? EditFileRequested;
    
    /// <summary>
    /// Event raised to request creating a folder.
    /// </summary>
    public event EventHandler<string>? CreateFolderRequested;
    
    /// <summary>
    /// Event raised to request creating a file.
    /// </summary>
    public event EventHandler<string>? CreateFileRequested;
    
    /// <summary>
    /// Create a virtual tab showing specific files (e.g., search results).
    /// </summary>
    public void CreateVirtualTab(List<string> filePaths, string tabName)
    {
        // For now, just refresh - actual tab creation is handled by TabbedFilePanel
        Items.Clear();
        foreach (var path in filePaths)
        {
            try
            {
                var item = _fileSystemService.GetFileInfo(path);
                if (item != null)
                {
                    Items.Add(new FileItemViewModel(item));
                }
            }
            catch
            {
                // Skip files that can't be accessed
            }
        }
        UpdateStatusText();
    }
    
    #region Open With Commands
    
    /// <summary>
    /// Open with default application.
    /// </summary>
    [RelayCommand]
    public void OpenWithDefault()
    {
        if (SelectedItem == null || SelectedItem.IsDirectory)
            return;
            
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = SelectedItem.FullPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Open with text editor.
    /// </summary>
    [RelayCommand]
    public void OpenWithTextEditor()
    {
        if (SelectedItem == null || SelectedItem.IsDirectory)
            return;
            
        OpenWithEditorRequested?.Invoke(this, (SelectedItem.FullPath, "text"));
    }
    
    /// <summary>
    /// Open with hex editor.
    /// </summary>
    [RelayCommand]
    public void OpenWithHexEditor()
    {
        if (SelectedItem == null || SelectedItem.IsDirectory)
            return;
            
        OpenWithEditorRequested?.Invoke(this, (SelectedItem.FullPath, "hex"));
    }
    
    /// <summary>
    /// Choose application to open with.
    /// </summary>
    [RelayCommand]
    public void OpenWithChoose()
    {
        if (SelectedItem == null || SelectedItem.IsDirectory)
            return;
            
        OpenWithChooseRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    /// <summary>
    /// Event raised when opening with a specific editor type.
    /// </summary>
    public event EventHandler<(string Path, string EditorType)>? OpenWithEditorRequested;
    
    /// <summary>
    /// Event raised when user wants to choose an application.
    /// </summary>
    public event EventHandler<string>? OpenWithChooseRequested;
    
    #endregion
    
    #region Archive Commands
    
    /// <summary>
    /// Create ZIP archive from selected items.
    /// </summary>
    [RelayCommand]
    public void CreateZipArchive()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            CreateArchiveRequested?.Invoke(this, (paths, "zip"));
    }
    
    /// <summary>
    /// Create 7z archive from selected items.
    /// </summary>
    [RelayCommand]
    public void Create7zArchive()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            CreateArchiveRequested?.Invoke(this, (paths, "7z"));
    }
    
    /// <summary>
    /// Create TAR.GZ archive from selected items.
    /// </summary>
    [RelayCommand]
    public void CreateTarGzArchive()
    {
        var paths = GetSelectedPaths().ToList();
        if (paths.Count > 0)
            CreateArchiveRequested?.Invoke(this, (paths, "tar.gz"));
    }
    
    /// <summary>
    /// Extract archive to current directory.
    /// </summary>
    [RelayCommand]
    public void ExtractHere()
    {
        if (SelectedItem == null)
            return;
            
        ExtractArchiveRequested?.Invoke(this, (SelectedItem.FullPath, CurrentPath));
    }
    
    /// <summary>
    /// Extract archive to a chosen folder.
    /// </summary>
    [RelayCommand]
    public void ExtractToFolder()
    {
        if (SelectedItem == null)
            return;
            
        ExtractToFolderRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    /// <summary>
    /// Test archive integrity.
    /// </summary>
    [RelayCommand]
    public void TestArchive()
    {
        if (SelectedItem == null)
            return;
            
        TestArchiveRequested?.Invoke(this, SelectedItem.FullPath);
    }
    
    /// <summary>
    /// Event raised when creating an archive.
    /// </summary>
    public event EventHandler<(List<string> Paths, string Format)>? CreateArchiveRequested;
    
    /// <summary>
    /// Event raised when extracting an archive.
    /// </summary>
    public event EventHandler<(string ArchivePath, string DestinationPath)>? ExtractArchiveRequested;
    
    /// <summary>
    /// Event raised when extracting to a folder (needs folder selection).
    /// </summary>
    public event EventHandler<string>? ExtractToFolderRequested;
    
    /// <summary>
    /// Event raised when testing archive integrity.
    /// </summary>
    public event EventHandler<string>? TestArchiveRequested;
    
    #endregion
    
    #region Selection Commands
    
    /// <summary>
    /// Select items by pattern.
    /// </summary>
    [RelayCommand]
    public void SelectByPattern()
    {
        SelectByPatternRequested?.Invoke(this, true);
    }
    
    /// <summary>
    /// Deselect items by pattern.
    /// </summary>
    [RelayCommand]
    public void DeselectByPattern()
    {
        SelectByPatternRequested?.Invoke(this, false);
    }
    
    /// <summary>
    /// Select all files (not folders).
    /// </summary>
    [RelayCommand]
    public void SelectAllFiles()
    {
        foreach (var item in Items.Where(i => i.ItemType == FileSystemItemType.File))
        {
            item.IsSelected = true;
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
        }
        UpdateStatusText();
    }
    
    /// <summary>
    /// Select all folders (not files).
    /// </summary>
    [RelayCommand]
    public void SelectAllFolders()
    {
        foreach (var item in Items.Where(i => i.ItemType == FileSystemItemType.Directory))
        {
            item.IsSelected = true;
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
        }
        UpdateStatusText();
    }
    
    /// <summary>
    /// Save current selection for later restoration.
    /// </summary>
    [RelayCommand]
    public void SaveSelection()
    {
        SaveSelectionRequested?.Invoke(this, GetSelectedPaths().ToList());
    }
    
    /// <summary>
    /// Restore previously saved selection.
    /// </summary>
    [RelayCommand]
    public void RestoreSelection()
    {
        RestoreSelectionRequested?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Event raised when select/deselect by pattern is requested.
    /// </summary>
    public event EventHandler<bool>? SelectByPatternRequested;
    
    /// <summary>
    /// Event raised when saving selection.
    /// </summary>
    public event EventHandler<List<string>>? SaveSelectionRequested;
    
    /// <summary>
    /// Event raised when restoring selection.
    /// </summary>
    public event EventHandler? RestoreSelectionRequested;
    
    #endregion
}
