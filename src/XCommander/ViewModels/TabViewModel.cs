using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Helpers;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class TabViewModel : ViewModelBase
{
    private const string NameColumnKey = "name";
    private const string ExtensionColumnKey = "extension";
    private const string SizeColumnKey = "size";
    private const string DateColumnKey = "date";
    private const string DescriptionColumnKey = "description";
    private const string NameHeaderThemeKey = "FilePanelNameColumnHeaderTheme";
    private const string ExtensionHeaderThemeKey = "FilePanelExtensionColumnHeaderTheme";
    private const string SizeHeaderThemeKey = "FilePanelSizeColumnHeaderTheme";
    private const string DateHeaderThemeKey = "FilePanelDateColumnHeaderTheme";
    private const string DescriptionHeaderThemeKey = "FilePanelDescriptionColumnHeaderTheme";
    private static readonly string[] DefaultQuickFilterPresets =
        ["All files", "*.txt", "*.cs", "*.jpg;*.png", "*.zip;*.7z"];

    private readonly IFileSystemService _fileSystemService;
    private readonly AppSettings _settings;
    private readonly IFileAssociationService? _fileAssociationService;
    
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
    private bool _showSystemFiles;
    
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
    private bool _quickFilterCaseSensitive;

    [ObservableProperty]
    private bool _quickFilterUseRegex;

    [ObservableProperty]
    private bool _quickFilterIncludeDirectories = true;
    
    [ObservableProperty]
    private bool _isVirtualMode;
    
    [ObservableProperty]
    private string _virtualModeTitle = string.Empty;
    
    /// <summary>
    /// Whether to show the description column (descript.ion comments).
    /// </summary>
    [ObservableProperty]
    private bool _showDescriptionColumn;
    
    /// <summary>
    /// Service for loading file descriptions.
    /// </summary>
    private readonly IDescriptionFileService? _descriptionService;
    
    /// <summary>
    /// Service for selection operations including undo/redo.
    /// </summary>
    private readonly ISelectionService? _selectionService;
    
    public Guid Id { get; } = Guid.NewGuid();
    
    public ObservableCollection<FileItemViewModel> Items { get; } = [];
    public ObservableCollection<FileItemViewModel> SelectedItems { get; } = [];
    public ObservableCollection<DriveItem> Drives { get; } = [];
    public ObservableCollection<string> PathSegments { get; } = [];
    public ObservableCollection<string> QuickFilterPresets { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    public TextFilterContext NameFilter { get; }
    public TextFilterContext ExtensionFilter { get; }
    public NumberFilterContext SizeFilter { get; }
    public TextFilterContext DescriptionFilter { get; }
    public DateFilterContext DateFilter { get; }

    public bool IsCloseButtonVisible => !IsLocked && _settings.ShowTabCloseButton;
    public bool IsDetailsView => ViewMode == FilePanelViewMode.Details;
    public bool IsListView => ViewMode == FilePanelViewMode.List;
    public bool IsThumbnailsView => ViewMode == FilePanelViewMode.Thumbnails;
    public bool IsGridView => ViewMode != FilePanelViewMode.Thumbnails;
    public bool ShowExtensionColumn => _settings.ShowFileExtensions && ViewMode == FilePanelViewMode.Details;
    public bool ShowSizeColumn => _settings.ShowFileSizes && ViewMode == FilePanelViewMode.Details;
    public bool ShowDateColumn => _settings.ShowFileDates && ViewMode == FilePanelViewMode.Details;
    
    /// <summary>
    /// Fired when navigation to a new path occurs.
    /// </summary>
    public event EventHandler<string>? Navigated;
    public event EventHandler? SelectedItemChanged;
    public event EventHandler<ArchiveRequestEventArgs>? ArchiveOpenRequested;

    public sealed class ArchiveRequestEventArgs : EventArgs
    {
        public string ArchivePath { get; init; } = string.Empty;
        public string ExtractPath { get; init; } = string.Empty;
    }
    
    // Navigation history
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;
    
    public TabViewModel(
        IFileSystemService fileSystemService,
        AppSettings settings,
        IDescriptionFileService? descriptionService = null,
        ISelectionService? selectionService = null,
        IFileAssociationService? fileAssociationService = null)
    {
        _fileSystemService = fileSystemService;
        _settings = settings;
        _descriptionService = descriptionService;
        _selectionService = selectionService;
        _fileAssociationService = fileAssociationService;
        ShowHiddenFiles = settings.ShowHiddenFiles;
        ShowSystemFiles = settings.ShowSystemFiles;
        QuickFilterCaseSensitive = settings.QuickFilterCaseSensitive;
        QuickFilterUseRegex = settings.QuickFilterUseRegex;
        QuickFilterIncludeDirectories = settings.QuickFilterIncludeDirectories;
        ShowDescriptionColumn = settings.ShowDescriptionColumn;
        ViewMode = ResolveViewMode(settings.DefaultViewMode);
        InitializeQuickFilterPresets();
        _settings.PropertyChanged += OnSettingsChanged;
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
        DescriptionFilter = new TextFilterContext(
            "Description contains",
            text => ApplyTextFilter(DescriptionColumnKey, text),
            () => FilteringModel.Remove(DescriptionColumnKey));
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
        UpdateColumnVisibility();
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
        IPropertyInfo descriptionProperty = DataGridColumnHelper.CreateProperty(
            nameof(FileItemViewModel.Description),
            (FileItemViewModel item) => item.Description,
            (item, value) => item.Description = value);
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
            builder.Text(
                header: "Description",
                property: descriptionProperty,
                getter: item => item.Description,
                configure: column =>
                {
                    column.ColumnKey = DescriptionColumnKey;
                    column.HeaderThemeKey = DescriptionHeaderThemeKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.MinWidth = 150;
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Template(
                header: "Date Modified",
                cellTemplateKey: "FileItemDateTemplate",
                configure: column =>
                {
                    column.ColumnKey = DateColumnKey;
                    column.HeaderThemeKey = DateHeaderThemeKey;
                    column.Width = new DataGridLength(130);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<FileItemViewModel, DateTime>(
                        item => item.DateModified);
                    column.ValueType = typeof(DateTime);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<FileItemViewModel, DateTime>(
                            item => item.DateModified)
                    };
                })
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

    private void UpdateColumnVisibility()
    {
        SetColumnVisibility(ExtensionColumnKey, ShowExtensionColumn);
        SetColumnVisibility(SizeColumnKey, ShowSizeColumn);
        SetColumnVisibility(DateColumnKey, ShowDateColumn);
        SetColumnVisibility(DescriptionColumnKey, ShowDescriptionColumn && ViewMode == FilePanelViewMode.Details);
    }

    private void SetColumnVisibility(string columnKey, bool isVisible)
    {
        for (var index = 0; index < ColumnDefinitions.Count; index++)
        {
            var column = ColumnDefinitions[index];
            if (column.ColumnKey is string key && string.Equals(key, columnKey, StringComparison.Ordinal))
            {
                column.IsVisible = isVisible;
                break;
            }
        }
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

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCloseButtonVisible));
    }

    partial void OnSelectedItemChanged(FileItemViewModel? value)
    {
        SelectedItemChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnShowHiddenFilesChanged(bool value)
    {
        if (!value && ShowSystemFiles)
            ShowSystemFiles = false;
        if (_settings.ShowHiddenFiles != value)
            _settings.ShowHiddenFiles = value;
    }

    partial void OnShowSystemFilesChanged(bool value)
    {
        if (value && !ShowHiddenFiles)
            ShowHiddenFiles = true;
        if (_settings.ShowSystemFiles != value)
            _settings.ShowSystemFiles = value;
    }
    
    partial void OnQuickFilterChanged(string value)
    {
        RefreshItems();
    }

    partial void OnQuickFilterCaseSensitiveChanged(bool value)
    {
        RefreshItems();
    }

    partial void OnQuickFilterUseRegexChanged(bool value)
    {
        RefreshItems();
    }

    partial void OnQuickFilterIncludeDirectoriesChanged(bool value)
    {
        RefreshItems();
    }

    partial void OnShowDescriptionColumnChanged(bool value)
    {
        UpdateColumnVisibility();
        if (value)
        {
            _ = LoadDescriptionsAsync();
        }
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

    private static FilePanelViewMode ResolveViewMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return FilePanelViewMode.Details;

        if (string.Equals(mode, "Details", StringComparison.OrdinalIgnoreCase))
            return FilePanelViewMode.Details;
        if (string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase))
            return FilePanelViewMode.List;
        if (string.Equals(mode, "Thumbnails", StringComparison.OrdinalIgnoreCase))
            return FilePanelViewMode.Thumbnails;

        return FilePanelViewMode.Details;
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
    public void ToggleSystemFiles()
    {
        ShowSystemFiles = !ShowSystemFiles;
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
            OpenFileWithDefaultAction(item.FullPath);
        }
    }

    private void OpenFileWithDefaultAction(string filePath)
    {
        var openCommand = _fileAssociationService?.GetOpenCommand(filePath);
        if (!string.IsNullOrWhiteSpace(openCommand))
        {
            if (IsInternalArchiveCommand(openCommand))
            {
                ArchiveOpenRequested?.Invoke(this, new ArchiveRequestEventArgs
                {
                    ArchivePath = filePath,
                    ExtractPath = CurrentPath
                });
                return;
            }

            LaunchExternalTool(openCommand, filePath);
            return;
        }

        var action = _settings.FileAssociationDefaultAction;
        if (string.Equals(action, "Viewer", StringComparison.OrdinalIgnoreCase))
        {
            if (TryLaunchAssociatedCommand(_fileAssociationService?.GetViewerCommand(filePath), filePath))
                return;

            if (!string.IsNullOrWhiteSpace(_settings.ExternalViewer))
            {
                LaunchExternalTool(_settings.ExternalViewer, filePath);
                return;
            }
        }

        if (string.Equals(action, "Editor", StringComparison.OrdinalIgnoreCase))
        {
            if (TryLaunchAssociatedCommand(_fileAssociationService?.GetEditorCommand(filePath), filePath))
                return;

            if (!string.IsNullOrWhiteSpace(_settings.ExternalEditor))
            {
                LaunchExternalTool(_settings.ExternalEditor, filePath);
                return;
            }
        }

        // Open file with default application
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening file: {ex.Message}");
        }
    }

    private static bool IsInternalArchiveCommand(string command)
    {
        return string.Equals(command.Trim(), "internal:archive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryLaunchAssociatedCommand(string? command, string filePath)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        LaunchExternalTool(command, filePath);
        return true;
    }

    private static void LaunchExternalTool(string command, string filePath)
    {
        var quotedPath = QuoteForShell(filePath);
        var commandLine = command.Contains("{file}", StringComparison.OrdinalIgnoreCase)
            ? command.Replace("{file}", quotedPath, StringComparison.OrdinalIgnoreCase)
            : command.Contains("%s", StringComparison.OrdinalIgnoreCase)
                ? command.Replace("%s", quotedPath, StringComparison.OrdinalIgnoreCase)
                : $"{command} {quotedPath}";

        try
        {
            RunShellCommand(commandLine, Path.GetDirectoryName(filePath));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching external tool: {ex.Message}");
        }
    }

    private static void RunShellCommand(string commandLine, string? workingDirectory)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows()
                ? $"/c {commandLine}"
                : $"-c \"{commandLine}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    private static string QuoteForShell(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"\"{path}\"";
        }

        var escaped = path.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
    
    [RelayCommand]
    public void SelectAll()
    {
        PushSelectionHistory();
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
        PushSelectionHistory();
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
        PushSelectionHistory();
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
    public void ApplyQuickFilter()
    {
        var filter = QuickFilter?.Trim();
        if (string.IsNullOrEmpty(filter))
            return;

        if (string.Equals(filter, "All files", StringComparison.OrdinalIgnoreCase))
        {
            QuickFilter = string.Empty;
            return;
        }

        var history = _settings.QuickFilterHistory.Count == 0
            ? new List<string>()
            : new List<string>(_settings.QuickFilterHistory);

        history.RemoveAll(item => string.Equals(item, filter, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, filter);

        var limit = Math.Max(1, _settings.CommandHistoryLimit);
        if (history.Count > limit)
            history.RemoveRange(limit, history.Count - limit);

        _settings.QuickFilterHistory = history;
        UpdateQuickFilterPresets();
    }
    
    [RelayCommand]
    public void ClearQuickFilter()
    {
        QuickFilter = string.Empty;
    }

    private void InitializeQuickFilterPresets()
    {
        UpdateQuickFilterPresets();
    }

    private void UpdateQuickFilterPresets()
    {
        QuickFilterPresets.Clear();

        foreach (var preset in DefaultQuickFilterPresets)
            QuickFilterPresets.Add(preset);

        foreach (var preset in _settings.QuickFilterHistory)
        {
            if (QuickFilterPresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
                continue;

            QuickFilterPresets.Add(preset);
        }
    }
    
    private void RefreshItems()
    {
        Items.Clear();
        SelectedItems.Clear();
        
        if (string.IsNullOrEmpty(CurrentPath))
            return;
            
        var items = _fileSystemService.GetDirectoryContents(CurrentPath, ShowHiddenFiles)
            .Select(i => new FileItemViewModel(i));

        if (!ShowSystemFiles)
        {
            items = items.Where(i => !i.Item.IsSystem);
        }
            
        // Apply quick filter
        if (!string.IsNullOrEmpty(QuickFilter))
        {
            var comparison = QuickFilterCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var hasWildcard = QuickFilter.Contains('*') || QuickFilter.Contains('?');
            Regex? regex = null;
            if (QuickFilterUseRegex)
            {
                try
                {
                    var options = QuickFilterCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regex = new Regex(QuickFilter, options);
                }
                catch
                {
                    regex = null;
                }
            }

            items = items.Where(i => MatchesQuickFilter(i, comparison, hasWildcard, regex));
        }
            
        // Sort items
        var sortedItems = SortItems(items);
        
        foreach (var item in sortedItems)
        {
            Items.Add(item);
        }
        
        UpdateStatusText();
        
        // Load descriptions asynchronously if enabled
        if (ShowDescriptionColumn && _descriptionService != null)
        {
            _ = LoadDescriptionsAsync();
        }
    }
    
    /// <summary>
    /// Loads file descriptions from descript.ion file asynchronously.
    /// </summary>
    private async Task LoadDescriptionsAsync()
    {
        if (_descriptionService == null || string.IsNullOrEmpty(CurrentPath))
            return;
            
        try
        {
            var descriptions = await _descriptionService.GetDirectoryDescriptionsAsync(CurrentPath);
            var descriptionDict = descriptions.ToDictionary(
                d => d.FileName, 
                d => d.Description, 
                StringComparer.OrdinalIgnoreCase);
            
            foreach (var item in Items)
            {
                if (item.ItemType != FileSystemItemType.ParentDirectory && 
                    descriptionDict.TryGetValue(item.Name, out var description))
                {
                    item.Description = description;
                    item.DescriptionLoaded = true;
                }
            }
        }
        catch
        {
            // Silently ignore description loading errors
        }
    }
    
    private IEnumerable<FileItemViewModel> SortItems(IEnumerable<FileItemViewModel> items)
    {
        // Parent directory always first
        var parent = items.Where(i => i.ItemType == FileSystemItemType.ParentDirectory);
        var directories = items.Where(i => i.ItemType == FileSystemItemType.Directory);
        var files = items.Where(i => i.ItemType == FileSystemItemType.File);

        if (_settings.SortDirectoriesFirst)
        {
            directories = SortCollection(directories);
            files = SortCollection(files);
            return parent.Concat(directories).Concat(files);
        }

        var combined = directories.Concat(files);
        combined = SortCollection(combined);
        return parent.Concat(combined);
    }
    
    private IEnumerable<FileItemViewModel> SortCollection(IEnumerable<FileItemViewModel> items)
    {
        var comparer = _settings.SortCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        return SortColumn switch
        {
            "Name" => SortAscending
                ? items.OrderBy(i => i.Name, comparer)
                : items.OrderByDescending(i => i.Name, comparer),
            "Extension" => SortAscending
                ? items.OrderBy(i => i.Extension, comparer)
                : items.OrderByDescending(i => i.Extension, comparer),
            "Size" => SortAscending ? items.OrderBy(i => i.Size) : items.OrderByDescending(i => i.Size),
            "Date" => SortAscending ? items.OrderBy(i => i.DateModified) : items.OrderByDescending(i => i.DateModified),
            _ => items.OrderBy(i => i.Name, comparer)
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

    partial void OnViewModeChanged(FilePanelViewMode value)
    {
        OnPropertyChanged(nameof(IsDetailsView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsThumbnailsView));
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(ShowExtensionColumn));
        OnPropertyChanged(nameof(ShowSizeColumn));
        OnPropertyChanged(nameof(ShowDateColumn));
        UpdateColumnVisibility();
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
    
    // Selection history undo/redo support
    
    /// <summary>
    /// Push current selection to history stack for undo support.
    /// Call this before making selection changes.
    /// </summary>
    private void PushSelectionHistory()
    {
        _selectionService?.PushSelectionHistory(Id.ToString(), SelectedItems.Select(i => i.FullPath));
    }
    
    /// <summary>
    /// Undo the last selection change (Ctrl+Z for selection).
    /// </summary>
    [RelayCommand]
    public void UndoSelection()
    {
        if (_selectionService == null) return;
        
        var previousSelection = _selectionService.UndoSelection(Id.ToString());
        if (previousSelection != null)
        {
            ApplySelectionFromPaths(previousSelection);
        }
    }
    
    /// <summary>
    /// Redo a previously undone selection change.
    /// </summary>
    [RelayCommand]
    public void RedoSelection()
    {
        if (_selectionService == null) return;
        
        var nextSelection = _selectionService.RedoSelection(Id.ToString());
        if (nextSelection != null)
        {
            ApplySelectionFromPaths(nextSelection);
        }
    }
    
    /// <summary>
    /// Check if selection undo is available.
    /// </summary>
    public bool CanUndoSelection => _selectionService?.CanUndo(Id.ToString()) ?? false;
    
    /// <summary>
    /// Check if selection redo is available.
    /// </summary>
    public bool CanRedoSelection => _selectionService?.CanRedo(Id.ToString()) ?? false;
    
    /// <summary>
    /// Apply selection from a list of file paths.
    /// </summary>
    private void ApplySelectionFromPaths(IEnumerable<string> paths)
    {
        var pathSet = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        
        SelectedItems.Clear();
        foreach (var item in Items)
        {
            item.IsSelected = pathSet.Contains(item.FullPath);
            if (item.IsSelected)
            {
                SelectedItems.Add(item);
            }
        }
        UpdateStatusText();
    }

    private bool MatchesQuickFilter(
        FileItemViewModel item,
        StringComparison comparison,
        bool hasWildcard,
        Regex? regex)
    {
        if (item.ItemType == FileSystemItemType.ParentDirectory)
            return true;

        if (!QuickFilterIncludeDirectories && item.IsDirectory)
            return false;

        var name = item.Name ?? string.Empty;

        if (QuickFilterUseRegex && regex != null)
        {
            return regex.IsMatch(name);
        }

        if (hasWildcard)
        {
            return WildcardMatch(name, QuickFilter, comparison);
        }

        return name.IndexOf(QuickFilter, comparison) >= 0;
    }

    private static bool WildcardMatch(string text, string pattern, StringComparison comparison)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var options = comparison == StringComparison.OrdinalIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.IsMatch(text, regexPattern, options);
    }

    public TabState CreateSessionState()
    {
        return new TabState
        {
            Id = Id.ToString(),
            Path = CurrentPath,
            Title = Title,
            IsActive = IsSelected,
            SortColumn = MapSortColumnToState(SortColumn),
            SortAscending = SortAscending,
            ViewMode = ViewMode.ToString(),
            SelectedItems = SelectedItems.Select(item => item.FullPath).ToList(),
            FocusedItem = SelectedItem?.FullPath,
            ShowHidden = ShowHiddenFiles,
            Filter = QuickFilter,
            History = _history.ToList(),
            HistoryPosition = _historyIndex
        };
    }

    public void ApplySessionState(TabState state, string fallbackPath)
    {
        var targetPath = ResolveSessionPath(state.Path, fallbackPath);

        ViewMode = ResolveViewMode(state.ViewMode);
        SortColumn = MapSortColumnFromState(state.SortColumn);
        SortAscending = state.SortAscending;
        ShowHiddenFiles = state.ShowHidden;
        QuickFilter = state.Filter ?? string.Empty;

        RestoreHistory(state.History, state.HistoryPosition, targetPath);

        CurrentPath = _history[_historyIndex];
        RefreshItems();
        UpdatePathSegments();
        RestoreSelection(state.SelectedItems, state.FocusedItem);

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    private static string ResolveSessionPath(string? path, string fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            return path;

        if (!string.IsNullOrWhiteSpace(fallbackPath) && Directory.Exists(fallbackPath))
            return fallbackPath;

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private void RestoreHistory(List<string>? history, int historyIndex, string fallbackPath)
    {
        _history.Clear();

        if (history != null)
        {
            foreach (var entry in history)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                    _history.Add(entry);
            }
        }

        if (_history.Count == 0)
            _history.Add(fallbackPath);

        _historyIndex = Math.Clamp(historyIndex, 0, _history.Count - 1);
    }

    private void RestoreSelection(IReadOnlyList<string>? selectedItems, string? focusedItem)
    {
        if (Items.Count == 0)
            return;

        if (selectedItems != null)
        {
            foreach (var item in Items)
            {
                if (selectedItems.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase) ||
                    selectedItems.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    item.IsSelected = true;
                    if (!SelectedItems.Contains(item))
                        SelectedItems.Add(item);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(focusedItem))
        {
            var focused = Items.FirstOrDefault(item =>
                focusedItem.Equals(item.FullPath, StringComparison.OrdinalIgnoreCase) ||
                focusedItem.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (focused != null)
            {
                SelectedItem = focused;
                if (!SelectedItems.Contains(focused))
                    SelectedItems.Add(focused);
            }
        }

        if (SelectedItem == null && SelectedItems.Count > 0)
            SelectedItem = SelectedItems[0];
    }

    private static int MapSortColumnToState(string column)
    {
        return column switch
        {
            "Name" => 0,
            "Extension" => 1,
            "Size" => 2,
            "Date" => 3,
            _ => 0
        };
    }

    private static string MapSortColumnFromState(int column)
    {
        return column switch
        {
            1 => "Extension",
            2 => "Size",
            3 => "Date",
            _ => "Name"
        };
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.ShowHiddenFiles))
        {
            if (ShowHiddenFiles != _settings.ShowHiddenFiles)
            {
                ShowHiddenFiles = _settings.ShowHiddenFiles;
                RefreshItems();
            }
        }
        else if (e.PropertyName == nameof(AppSettings.ShowSystemFiles))
        {
            if (ShowSystemFiles != _settings.ShowSystemFiles)
            {
                ShowSystemFiles = _settings.ShowSystemFiles;
                RefreshItems();
            }
        }
        else if (e.PropertyName == nameof(AppSettings.ShowFileExtensions))
        {
            OnPropertyChanged(nameof(ShowExtensionColumn));
            UpdateColumnVisibility();
        }
        else if (e.PropertyName == nameof(AppSettings.ShowFileSizes))
        {
            OnPropertyChanged(nameof(ShowSizeColumn));
            UpdateColumnVisibility();
        }
        else if (e.PropertyName == nameof(AppSettings.ShowFileDates))
        {
            OnPropertyChanged(nameof(ShowDateColumn));
            UpdateColumnVisibility();
        }
        else if (e.PropertyName == nameof(AppSettings.SortDirectoriesFirst) ||
                 e.PropertyName == nameof(AppSettings.SortCaseSensitive))
        {
            RefreshItems();
        }
        else if (e.PropertyName == nameof(AppSettings.ShowDescriptionColumn))
        {
            if (ShowDescriptionColumn != _settings.ShowDescriptionColumn)
            {
                ShowDescriptionColumn = _settings.ShowDescriptionColumn;
            }
        }
        else if (e.PropertyName == nameof(AppSettings.QuickFilterCaseSensitive))
        {
            if (QuickFilterCaseSensitive != _settings.QuickFilterCaseSensitive)
                QuickFilterCaseSensitive = _settings.QuickFilterCaseSensitive;
        }
        else if (e.PropertyName == nameof(AppSettings.QuickFilterUseRegex))
        {
            if (QuickFilterUseRegex != _settings.QuickFilterUseRegex)
                QuickFilterUseRegex = _settings.QuickFilterUseRegex;
        }
        else if (e.PropertyName == nameof(AppSettings.QuickFilterIncludeDirectories))
        {
            if (QuickFilterIncludeDirectories != _settings.QuickFilterIncludeDirectories)
                QuickFilterIncludeDirectories = _settings.QuickFilterIncludeDirectories;
        }
        else if (e.PropertyName == nameof(AppSettings.QuickFilterHistory))
        {
            UpdateQuickFilterPresets();
        }
        else if (e.PropertyName == nameof(AppSettings.ShowTabCloseButton))
        {
            OnPropertyChanged(nameof(IsCloseButtonVisible));
        }
    }
}
