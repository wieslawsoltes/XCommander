using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class TabViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    
    [ObservableProperty]
    private string _currentPath = string.Empty;
    
    [ObservableProperty]
    private string _title = "New Tab";
    
    [ObservableProperty]
    private bool _isLocked;
    
    [ObservableProperty]
    private bool _isSelected;
    
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
    private string _quickFilter = string.Empty;
    
    [ObservableProperty]
    private bool _isVirtualMode;
    
    [ObservableProperty]
    private string _virtualModeTitle = string.Empty;
    
    public Guid Id { get; } = Guid.NewGuid();
    
    public ObservableCollection<FileItemViewModel> Items { get; } = [];
    public ObservableCollection<FileItemViewModel> SelectedItems { get; } = [];
    public ObservableCollection<DriveItem> Drives { get; } = [];
    public ObservableCollection<string> PathSegments { get; } = [];
    
    /// <summary>
    /// Fired when navigation to a new path occurs.
    /// </summary>
    public event EventHandler<string>? Navigated;
    
    // Navigation history
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;
    
    public TabViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        LoadDrives();
    }
    
    public void LoadDrives()
    {
        Drives.Clear();
        foreach (var drive in _fileSystemService.GetDrives())
        {
            Drives.Add(drive);
        }
    }
    
    partial void OnCurrentPathChanged(string value)
    {
        UpdateTitle();
    }
    
    partial void OnQuickFilterChanged(string value)
    {
        RefreshItems();
    }
    
    private void UpdateTitle()
    {
        if (IsVirtualMode)
        {
            Title = string.IsNullOrEmpty(VirtualModeTitle) ? "Search Results" : VirtualModeTitle;
        }
        else if (string.IsNullOrEmpty(CurrentPath))
        {
            Title = "New Tab";
        }
        else
        {
            var name = Path.GetFileName(CurrentPath);
            Title = string.IsNullOrEmpty(name) ? CurrentPath : name;
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
        
        // Notify listeners about navigation
        Navigated?.Invoke(this, path);
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
    
    /// <summary>
    /// Populates the tab with virtual items (e.g., search results) from various paths.
    /// </summary>
    public void PopulateWithPaths(IReadOnlyList<string> paths, string title = "Search Results")
    {
        Items.Clear();
        SelectedItems.Clear();
        IsVirtualMode = true;
        VirtualModeTitle = title;
        CurrentPath = string.Empty; // Virtual mode has no single current path
        UpdateTitle();
        
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    var item = new FileSystemItem
                    {
                        Name = fileInfo.Name,
                        FullPath = path,
                        Size = fileInfo.Length,
                        DateModified = fileInfo.LastWriteTime,
                        ItemType = FileSystemItemType.File,
                        Extension = fileInfo.Extension,
                        Attributes = fileInfo.Attributes
                    };
                    Items.Add(new FileItemViewModel(item));
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var item = new FileSystemItem
                    {
                        Name = dirInfo.Name,
                        FullPath = path,
                        Size = 0,
                        DateModified = dirInfo.LastWriteTime,
                        ItemType = FileSystemItemType.Directory,
                        Extension = string.Empty,
                        Attributes = dirInfo.Attributes
                    };
                    Items.Add(new FileItemViewModel(item));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding path {path}: {ex.Message}");
            }
        }
        
        StatusText = $"{Items.Count} items";
    }
    
    /// <summary>
    /// Exits virtual mode and navigates to a real path.
    /// </summary>
    public void ExitVirtualMode(string? navigateTo = null)
    {
        IsVirtualMode = false;
        VirtualModeTitle = string.Empty;
        
        if (!string.IsNullOrEmpty(navigateTo))
        {
            NavigateTo(navigateTo);
        }
        else
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            NavigateTo(homeDir);
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
    /// Selects the current item and moves to the next (Insert key in TC).
    /// </summary>
    public void SelectItemAndMoveNext()
    {
        if (SelectedItem == null)
            return;
            
        // Select current item if not parent directory
        if (SelectedItem.ItemType != FileSystemItemType.ParentDirectory)
        {
            SelectedItem.IsSelected = true;
            if (!SelectedItems.Contains(SelectedItem))
                SelectedItems.Add(SelectedItem);
        }
        
        // Move to next item
        var index = Items.IndexOf(SelectedItem);
        if (index >= 0 && index < Items.Count - 1)
        {
            SelectedItem = Items[index + 1];
        }
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
    
    [RelayCommand]
    public void ClearQuickFilter()
    {
        QuickFilter = string.Empty;
    }
    
    private void RefreshItems()
    {
        Items.Clear();
        SelectedItems.Clear();
        
        if (string.IsNullOrEmpty(CurrentPath))
            return;
            
        var items = _fileSystemService.GetDirectoryContents(CurrentPath, ShowHiddenFiles)
            .Select(i => new FileItemViewModel(i));
            
        // Apply quick filter
        if (!string.IsNullOrEmpty(QuickFilter))
        {
            items = items.Where(i => 
                i.ItemType == FileSystemItemType.ParentDirectory || 
                i.Name.Contains(QuickFilter, StringComparison.OrdinalIgnoreCase));
        }
            
        // Sort items
        var sortedItems = SortItems(items);
        
        foreach (var item in sortedItems)
        {
            Items.Add(item);
        }
        
        UpdateStatusText();
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
    
    [ObservableProperty]
    private FilePanelViewMode _viewMode = FilePanelViewMode.Details;
    
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
    /// Calculates directory sizes for all selected directories (Ctrl+L in Total Commander).
    /// </summary>
    public async void CalculateDirectorySizes()
    {
        var directories = Items
            .Where(i => i.IsDirectory && i.ItemType != FileSystemItemType.ParentDirectory)
            .ToList();
            
        if (directories.Count == 0)
            return;
            
        foreach (var dir in directories)
        {
            await Task.Run(() =>
            {
                try
                {
                    var size = CalculateDirectorySize(dir.FullPath);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        dir.CalculatedSize = size;
                    });
                }
                catch
                {
                    // Ignore access denied errors
                }
            });
        }
    }
    
    private long CalculateDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    size += fi.Length;
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
        return size;
    }
}
