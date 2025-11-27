using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Plugins;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    
    [ObservableProperty]
    private TabbedPanelViewModel _leftPanel;
    
    [ObservableProperty]
    private TabbedPanelViewModel _rightPanel;
    
    [ObservableProperty]
    private TabbedPanelViewModel _activePanel;
    
    [ObservableProperty]
    private string _commandLine = string.Empty;
    
    [ObservableProperty]
    private bool _isOperationInProgress;
    
    [ObservableProperty]
    private string _operationStatus = string.Empty;
    
    [ObservableProperty]
    private double _operationProgress;
    
    [ObservableProperty]
    private bool _isQuickViewVisible;
    
    [ObservableProperty]
    private QuickViewViewModel _quickView = new();
    
    [ObservableProperty]
    private bool _isCommandPaletteOpen;
    
    [ObservableProperty]
    private CommandPaletteViewModel _commandPalette = new();
    
    [ObservableProperty]
    private PluginManager? _pluginManager;
    
    [ObservableProperty]
    private bool _isBookmarksPanelVisible;
    
    [ObservableProperty]
    private BookmarksViewModel _bookmarks = new();
    
    [ObservableProperty]
    private DirectoryTreeViewModel? _directoryTree;
    
    [ObservableProperty]
    private bool _isDirectoryTreeVisible;
    
    [ObservableProperty]
    private ObservableCollection<ToolbarButton> _toolbarButtons = new();
    
    [ObservableProperty]
    private bool _showToolbarLabels;
    
    // Command history
    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;
    private const int MaxCommandHistory = 100;
    
    public ObservableCollection<string> CommandHistory { get; } = new();
    
    public TabbedPanelViewModel InactivePanel => ActivePanel == LeftPanel ? RightPanel : LeftPanel;
    
    private readonly IDescriptionFileService? _descriptionService;
    private readonly ISelectionService? _selectionService;
    
    public MainWindowViewModel(IFileSystemService fileSystemService, IDescriptionFileService? descriptionService = null, ISelectionService? selectionService = null)
    {
        _fileSystemService = fileSystemService;
        _descriptionService = descriptionService;
        _selectionService = selectionService;
        
        _leftPanel = new TabbedPanelViewModel(fileSystemService, descriptionService, selectionService) { IsActive = true };
        _rightPanel = new TabbedPanelViewModel(fileSystemService, descriptionService, selectionService) { IsActive = false };
        _activePanel = _leftPanel;
        
        // Initialize directory tree
        _directoryTree = new DirectoryTreeViewModel(fileSystemService);
        _directoryTree.NavigationRequested += OnDirectoryTreeNavigation;
        
        // Subscribe to navigation events for recent locations
        _leftPanel.Navigated += OnPanelNavigated;
        _rightPanel.Navigated += OnPanelNavigated;
        
        // Initialize to home directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _leftPanel.Initialize(homeDir);
        _rightPanel.Initialize(homeDir);
        
        // Load toolbar configuration
        LoadToolbarConfiguration();
        
        // Load command history
        LoadCommandHistory();
    }
    
    private void OnDirectoryTreeNavigation(object? sender, string path)
    {
        ActivePanel.ActiveTab?.NavigateTo(path);
    }
    
    private void OnPanelNavigated(object? sender, string path)
    {
        // Add to recent locations
        Bookmarks.AddRecentLocation(path);
        
        // Sync directory tree selection
        DirectoryTree?.NavigateToPath(path);
    }
    
    [RelayCommand]
    public void ToggleDirectoryTree()
    {
        IsDirectoryTreeVisible = !IsDirectoryTreeVisible;
    }
    
    public void LoadToolbarConfiguration()
    {
        var config = ToolbarConfiguration.Load();
        ShowToolbarLabels = config.ShowLabels;
        
        ToolbarButtons.Clear();
        foreach (var button in config.Buttons.Where(b => b.IsVisible).OrderBy(b => b.Order))
        {
            ToolbarButtons.Add(button);
        }
    }
    
    [RelayCommand]
    public void ExecuteToolbarCommand(string? commandName)
    {
        if (string.IsNullOrEmpty(commandName))
            return;
            
        switch (commandName)
        {
            case "GoBack":
                ActivePanel?.ActiveTab?.GoBackCommand.Execute(null);
                break;
            case "GoForward":
                ActivePanel?.ActiveTab?.GoForwardCommand.Execute(null);
                break;
            case "GoToParent":
                ActivePanel?.ActiveTab?.GoToParentCommand.Execute(null);
                break;
            case "GoHome":
                ActivePanel?.NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                break;
            case "Refresh":
                ActivePanel?.ActiveTab?.RefreshCommand.Execute(null);
                break;
            case "CopySelected":
                CopySelectedCommand.Execute(null);
                break;
            case "MoveSelected":
                MoveSelectedCommand.Execute(null);
                break;
            case "DeleteSelected":
                DeleteSelectedCommand.Execute(null);
                break;
            case "CreateNewFolder":
                CreateNewFolderCommand.Execute(null);
                break;
            case "CreateNewFile":
                CreateNewFileCommand.Execute(null);
                break;
            case "Search":
                // Will be handled by UI event
                break;
            case "NewTab":
                ActivePanel.AddNewTabCommand.Execute(null);
                break;
            case "ToggleBookmarks":
                ToggleBookmarksPanelCommand.Execute(null);
                break;
            case "AddBookmark":
                AddCurrentFolderToBookmarksCommand.Execute(null);
                break;
            case "ToggleQuickView":
                ToggleQuickViewCommand.Execute(null);
                break;
            case "MultiRename":
                MultiRenameCommand.Execute(null);
                break;
            case "CompareDirectories":
                CompareDirectoriesCommand.Execute(null);
                break;
            case "CompareFiles":
                CompareFilesCommand.Execute(null);
                break;
            case "SyncDirectories":
                SyncDirectoriesCommand.Execute(null);
                break;
            case "BranchView":
                ToggleBranchViewCommand.Execute(null);
                break;
            case "CalculateChecksum":
                CalculateChecksumCommand.Execute(null);
                break;
            case "EncodingTool":
                OpenEncodingToolCommand.Execute(null);
                break;
            case "SplitFile":
                SplitFileCommand.Execute(null);
                break;
            case "CombineFiles":
                CombineFilesCommand.Execute(null);
                break;
            case "OpenArchive":
                OpenArchiveCommand.Execute(null);
                break;
            case "CreateArchive":
                CreateArchiveCommand.Execute(null);
                break;
            case "ExtractArchive":
                ExtractArchiveCommand.Execute(null);
                break;
            case "FtpConnect":
                OpenFtpConnectionCommand.Execute(null);
                break;
            case "SftpConnect":
                OpenSftpConnectionCommand.Execute(null);
                break;
            case "Settings":
                OpenSettingsCommand.Execute(null);
                break;
            case "Help":
                ShowHelpCommand.Execute(null);
                break;
            case "CommandPalette":
                OpenCommandPaletteCommand.Execute(null);
                break;
        }
    }
    
    [RelayCommand]
    public void ToggleQuickView()
    {
        IsQuickViewVisible = !IsQuickViewVisible;
        if (IsQuickViewVisible)
        {
            UpdateQuickView();
        }
        else
        {
            QuickView.Clear();
        }
    }
    
    [RelayCommand]
    public void ToggleBookmarksPanel()
    {
        IsBookmarksPanelVisible = !IsBookmarksPanelVisible;
    }
    
    [RelayCommand]
    public void AddCurrentFolderToBookmarks()
    {
        var currentPath = ActivePanel.CurrentPath;
        if (!string.IsNullOrEmpty(currentPath))
        {
            Bookmarks.AddBookmark(currentPath);
        }
    }
    
    public void NavigateToBookmark(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            ActivePanel.NavigateTo(path);
        }
    }
    
    public async void UpdateQuickView()
    {
        if (!IsQuickViewVisible)
            return;
            
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem != null && !selectedItem.IsDirectory && selectedItem.ItemType != FileSystemItemType.ParentDirectory)
        {
            await QuickView.LoadPreviewAsync(selectedItem.FullPath);
        }
        else
        {
            QuickView.Clear();
        }
    }
    
    [RelayCommand]
    public void SwitchPanel()
    {
        ActivePanel.IsActive = false;
        ActivePanel = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
        ActivePanel.IsActive = true;
        OnPropertyChanged(nameof(InactivePanel));
    }
    
    /// <summary>
    /// Swaps the paths of both panels (Ctrl+U in Total Commander).
    /// </summary>
    [RelayCommand]
    public void SwapPanels()
    {
        var leftPath = LeftPanel.CurrentPath;
        var rightPath = RightPanel.CurrentPath;
        
        LeftPanel.NavigateTo(rightPath);
        RightPanel.NavigateTo(leftPath);
    }
    
    [RelayCommand]
    public void SetActivePanel(TabbedPanelViewModel panel)
    {
        if (ActivePanel != panel)
        {
            ActivePanel.IsActive = false;
            ActivePanel = panel;
            ActivePanel.IsActive = true;
            OnPropertyChanged(nameof(InactivePanel));
        }
    }
    
    /// <summary>
    /// Event raised when copy/move dialog is requested.
    /// </summary>
    public event EventHandler<CopyMoveDialogEventArgs>? CopyMoveDialogRequested;
    
    /// <summary>
    /// Event raised when delete confirmation dialog is requested.
    /// </summary>
    public event EventHandler<DeleteConfirmationEventArgs>? DeleteConfirmationRequested;
    
    [RelayCommand]
    public async Task CopySelectedAsync()
    {
        var sourcePaths = ActivePanel.GetSelectedPaths().ToList();
        if (sourcePaths.Count == 0)
            return;
            
        var destinationFolder = InactivePanel.CurrentPath;
        if (string.IsNullOrEmpty(destinationFolder))
            return;
        
        // Request TC-style copy dialog
        var dialogResult = await ShowCopyMoveDialogAsync(sourcePaths, destinationFolder, isCopy: true);
        if (dialogResult == null || !dialogResult.Confirmed)
            return;
            
        IsOperationInProgress = true;
        OperationStatus = "Copying files...";
        
        try
        {
            var progress = new Progress<FileOperationProgress>(p =>
            {
                OperationProgress = p.Percentage;
                OperationStatus = $"Copying: {p.CurrentItem}";
            });
            
            await _fileSystemService.CopyAsync(sourcePaths, dialogResult.DestinationPath, progress);
            InactivePanel.Refresh();
        }
        catch (Exception ex)
        {
            OperationStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
            OperationProgress = 0;
            OperationStatus = string.Empty;
        }
    }
    
    [RelayCommand]
    public async Task MoveSelectedAsync()
    {
        var sourcePaths = ActivePanel.GetSelectedPaths().ToList();
        if (sourcePaths.Count == 0)
            return;
            
        var destinationFolder = InactivePanel.CurrentPath;
        if (string.IsNullOrEmpty(destinationFolder))
            return;
        
        // Request TC-style move dialog
        var dialogResult = await ShowCopyMoveDialogAsync(sourcePaths, destinationFolder, isCopy: false);
        if (dialogResult == null || !dialogResult.Confirmed)
            return;
            
        IsOperationInProgress = true;
        OperationStatus = "Moving files...";
        
        try
        {
            var progress = new Progress<FileOperationProgress>(p =>
            {
                OperationProgress = p.Percentage;
                OperationStatus = $"Moving: {p.CurrentItem}";
            });
            
            await _fileSystemService.MoveAsync(sourcePaths, dialogResult.DestinationPath, progress);
            ActivePanel.Refresh();
            InactivePanel.Refresh();
        }
        catch (Exception ex)
        {
            OperationStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
            OperationProgress = 0;
            OperationStatus = string.Empty;
        }
    }
    
    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var sourcePaths = ActivePanel.GetSelectedPaths().ToList();
        if (sourcePaths.Count == 0)
            return;
            
        // Request TC-style delete confirmation dialog
        var dialogResult = await ShowDeleteConfirmationDialogAsync(sourcePaths);
        if (dialogResult == null || !dialogResult.Confirmed)
            return;
        
        IsOperationInProgress = true;
        OperationStatus = "Deleting files...";
        
        try
        {
            var progress = new Progress<FileOperationProgress>(p =>
            {
                OperationProgress = p.Percentage;
                OperationStatus = $"Deleting: {p.CurrentItem}";
            });
            
            var useRecycleBin = dialogResult.DeleteMode == DeleteMode.RecycleBin;
            await _fileSystemService.DeleteAsync(sourcePaths, useRecycleBin, progress);
            ActivePanel.Refresh();
        }
        catch (Exception ex)
        {
            OperationStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
            OperationProgress = 0;
            OperationStatus = string.Empty;
        }
    }
    
    private Task<CopyMoveDialogResult?> ShowCopyMoveDialogAsync(List<string> sourcePaths, string destinationFolder, bool isCopy)
    {
        var tcs = new TaskCompletionSource<CopyMoveDialogResult?>();
        CopyMoveDialogRequested?.Invoke(this, new CopyMoveDialogEventArgs
        {
            SourcePaths = sourcePaths,
            DestinationFolder = destinationFolder,
            IsCopy = isCopy,
            Callback = result => tcs.SetResult(result)
        });
        
        // If no handler, return null
        if (CopyMoveDialogRequested == null)
        {
            tcs.SetResult(null);
        }
        
        return tcs.Task;
    }
    
    private Task<DeleteConfirmationResult?> ShowDeleteConfirmationDialogAsync(List<string> sourcePaths)
    {
        var tcs = new TaskCompletionSource<DeleteConfirmationResult?>();
        DeleteConfirmationRequested?.Invoke(this, new DeleteConfirmationEventArgs
        {
            SourcePaths = sourcePaths,
            Callback = result => tcs.SetResult(result)
        });
        
        // If no handler, use simple confirmation
        if (DeleteConfirmationRequested == null)
        {
            return ConfirmDeleteLegacyAsync(sourcePaths);
        }
        
        return tcs.Task;
    }
    
    private Task<DeleteConfirmationResult?> ConfirmDeleteLegacyAsync(List<string> paths)
    {
        var tcs = new TaskCompletionSource<DeleteConfirmationResult?>();
        ConfirmationRequested?.Invoke(this, new ConfirmationEventArgs
        {
            Title = "Delete",
            Message = paths.Count == 1 
                ? $"Delete '{Path.GetFileName(paths[0])}'?"
                : $"Delete {paths.Count} items?",
            Callback = result => tcs.SetResult(result ? new DeleteConfirmationResult { Confirmed = true, DeleteMode = DeleteMode.RecycleBin } : null)
        });
        return tcs.Task;
    }

    public event EventHandler<ConfirmationEventArgs>? ConfirmationRequested;
    
    public class ConfirmationEventArgs : EventArgs
    {
        public required string Title { get; init; }
        public required string Message { get; init; }
        public required Action<bool> Callback { get; init; }
    }
    
    [RelayCommand]
    public async Task CreateNewFolderAsync()
    {
        var basePath = ActivePanel.CurrentPath;
        var defaultName = "New Folder";
        
        // Generate a unique default name
        var suggestedName = defaultName;
        var counter = 1;
        while (Directory.Exists(Path.Combine(basePath, suggestedName)))
        {
            suggestedName = $"{defaultName} ({counter})";
            counter++;
        }
        
        var newFolderName = await RequestInputAsync("New Folder", "Enter folder name:", suggestedName);
        if (string.IsNullOrWhiteSpace(newFolderName))
            return;
            
        var newPath = Path.Combine(basePath, newFolderName);
        
        try
        {
            _fileSystemService.CreateDirectory(newPath);
            ActivePanel.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating folder: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public async Task CreateNewFileAsync()
    {
        var basePath = ActivePanel.CurrentPath;
        var defaultName = "New File.txt";
        
        // Generate a unique default name
        var suggestedName = defaultName;
        var counter = 1;
        while (File.Exists(Path.Combine(basePath, suggestedName)))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(defaultName);
            var ext = Path.GetExtension(defaultName);
            suggestedName = $"{nameWithoutExt} ({counter}){ext}";
            counter++;
        }
        
        var newFileName = await RequestInputAsync("New File", "Enter file name:", suggestedName);
        if (string.IsNullOrWhiteSpace(newFileName))
            return;
            
        var newPath = Path.Combine(basePath, newFileName);
        
        try
        {
            _fileSystemService.CreateFile(newPath);
            ActivePanel.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating file: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public async Task RenameSelectedAsync()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.ItemType == FileSystemItemType.ParentDirectory)
            return;
            
        // Request new name via event
        var newName = await RequestInputAsync("Rename", "Enter new name:", selectedItem.Name);
        if (string.IsNullOrEmpty(newName) || newName == selectedItem.Name)
            return;
            
        try
        {
            var directory = Path.GetDirectoryName(selectedItem.FullPath) ?? string.Empty;
            var newPath = Path.Combine(directory, newName);
            
            if (selectedItem.IsDirectory)
            {
                Directory.Move(selectedItem.FullPath, newPath);
            }
            else
            {
                File.Move(selectedItem.FullPath, newPath);
            }
            
            ActivePanel.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error renaming: {ex.Message}");
        }
    }
    
    private Task<string?> RequestInputAsync(string title, string prompt, string defaultValue = "")
    {
        var tcs = new TaskCompletionSource<string?>();
        InputRequested?.Invoke(this, new InputEventArgs
        {
            Title = title,
            Prompt = prompt,
            DefaultValue = defaultValue,
            Callback = result => tcs.SetResult(result)
        });
        return tcs.Task;
    }
    
    public event EventHandler<InputEventArgs>? InputRequested;
    
    public class InputEventArgs : EventArgs
    {
        public required string Title { get; init; }
        public required string Prompt { get; init; }
        public string DefaultValue { get; init; } = string.Empty;
        public required Action<string?> Callback { get; init; }
    }
    
    [RelayCommand]
    public void ViewSelected()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        // View will be handled via event, raised to the view layer
        ViewFileRequested?.Invoke(this, selectedItem.FullPath);
    }
    
    public event EventHandler<string>? ViewFileRequested;
    
    [RelayCommand]
    public void MultiRename()
    {
        var selectedPaths = ActivePanel.GetSelectedPaths().ToList();
        if (selectedPaths.Count == 0)
            return;
            
        MultiRenameRequested?.Invoke(this, selectedPaths);
    }
    
    public event EventHandler<List<string>>? MultiRenameRequested;
    
    [RelayCommand]
    public void OpenFtpConnection()
    {
        FtpConnectionRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? FtpConnectionRequested;
    
    [RelayCommand]
    public void OpenSftpConnection()
    {
        SftpConnectionRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? SftpConnectionRequested;
    
    [RelayCommand]
    public void CompareDirectories()
    {
        DirectoryCompareRequested?.Invoke(this, new DirectoryCompareEventArgs
        {
            LeftPath = LeftPanel.CurrentPath,
            RightPath = RightPanel.CurrentPath
        });
    }
    
    public event EventHandler<DirectoryCompareEventArgs>? DirectoryCompareRequested;
    
    public class DirectoryCompareEventArgs : EventArgs
    {
        public string LeftPath { get; init; } = string.Empty;
        public string RightPath { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Toggle branch/flat view for the active panel showing all files from subdirectories.
    /// </summary>
    [RelayCommand]
    public void ToggleBranchView()
    {
        var currentPath = ActivePanel.CurrentPath;
        if (string.IsNullOrEmpty(currentPath))
            return;
            
        BranchViewRequested?.Invoke(this, new BranchViewEventArgs
        {
            RootPath = currentPath,
            Panel = ActivePanel
        });
    }
    
    public event EventHandler<BranchViewEventArgs>? BranchViewRequested;
    
    public class BranchViewEventArgs : EventArgs
    {
        public string RootPath { get; init; } = string.Empty;
        public TabbedPanelViewModel? Panel { get; init; }
    }
    
    [RelayCommand]
    public void CompareFiles()
    {
        var leftItem = LeftPanel.SelectedItem;
        var rightItem = RightPanel.SelectedItem;
        
        FileCompareRequested?.Invoke(this, new FileCompareEventArgs
        {
            LeftPath = leftItem != null && !leftItem.IsDirectory ? leftItem.FullPath : null,
            RightPath = rightItem != null && !rightItem.IsDirectory ? rightItem.FullPath : null
        });
    }
    
    public event EventHandler<FileCompareEventArgs>? FileCompareRequested;
    
    public class FileCompareEventArgs : EventArgs
    {
        public string? LeftPath { get; init; }
        public string? RightPath { get; init; }
    }
    
    [RelayCommand]
    public void CalculateChecksum()
    {
        var selectedPaths = ActivePanel.GetSelectedPaths()
            .Where(p => !Directory.Exists(p))
            .ToList();
        
        if (selectedPaths.Count == 0)
        {
            // If no files selected, try single selected item
            var selected = ActivePanel.SelectedItem;
            if (selected != null && !selected.IsDirectory)
            {
                selectedPaths.Add(selected.FullPath);
            }
        }
        
        if (selectedPaths.Count > 0)
        {
            ChecksumRequested?.Invoke(this, selectedPaths);
        }
    }
    
    public event EventHandler<List<string>>? ChecksumRequested;
    
    [RelayCommand]
    public void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? SettingsRequested;
    
    [RelayCommand]
    public void OpenKeyboardShortcuts()
    {
        KeyboardShortcutsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? KeyboardShortcutsRequested;
    
    [RelayCommand]
    public void OpenFileAssociations()
    {
        FileAssociationsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? FileAssociationsRequested;
    
    [RelayCommand]
    public void OpenArchive()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        var extension = Path.GetExtension(selectedItem.FullPath).ToLowerInvariant();
        var supportedExtensions = new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".tgz" };
        
        if (supportedExtensions.Contains(extension))
        {
            OpenArchiveRequested?.Invoke(this, new ArchiveEventArgs
            {
                ArchivePath = selectedItem.FullPath,
                ExtractPath = ActivePanel.CurrentPath
            });
        }
    }
    
    [RelayCommand]
    public void CreateArchive()
    {
        var selectedPaths = ActivePanel.GetSelectedPaths().ToList();
        CreateArchiveRequested?.Invoke(this, new CreateArchiveEventArgs
        {
            SourcePaths = selectedPaths,
            DestinationPath = ActivePanel.CurrentPath
        });
    }
    
    [RelayCommand]
    public void ExtractArchive()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        var extension = Path.GetExtension(selectedItem.FullPath).ToLowerInvariant();
        var supportedExtensions = new[] { ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".tgz" };
        
        if (supportedExtensions.Contains(extension))
        {
            ExtractArchiveRequested?.Invoke(this, new ArchiveEventArgs
            {
                ArchivePath = selectedItem.FullPath,
                ExtractPath = InactivePanel.CurrentPath
            });
        }
    }
    
    public event EventHandler<ArchiveEventArgs>? OpenArchiveRequested;
    public event EventHandler<CreateArchiveEventArgs>? CreateArchiveRequested;
    public event EventHandler<ArchiveEventArgs>? ExtractArchiveRequested;
    
    public class ArchiveEventArgs : EventArgs
    {
        public string ArchivePath { get; init; } = string.Empty;
        public string ExtractPath { get; init; } = string.Empty;
    }
    
    public class CreateArchiveEventArgs : EventArgs
    {
        public List<string> SourcePaths { get; init; } = new();
        public string DestinationPath { get; init; } = string.Empty;
    }
    
    [RelayCommand]
    public void SyncDirectories()
    {
        DirectorySyncRequested?.Invoke(this, new DirectoryCompareEventArgs
        {
            LeftPath = LeftPanel.CurrentPath,
            RightPath = RightPanel.CurrentPath
        });
    }
    
    public event EventHandler<DirectoryCompareEventArgs>? DirectorySyncRequested;
    
    [RelayCommand]
    public void SplitFile()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        SplitFileRequested?.Invoke(this, new SplitCombineEventArgs
        {
            FilePath = selectedItem.FullPath,
            DestinationPath = InactivePanel.CurrentPath
        });
    }
    
    [RelayCommand]
    public void CombineFiles()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        // Check if it's a split file (.001, .002, etc.)
        var ext = Path.GetExtension(selectedItem.FullPath).ToLowerInvariant();
        if (ext.Length == 4 && ext.StartsWith(".") && int.TryParse(ext.Substring(1), out _))
        {
            CombineFilesRequested?.Invoke(this, new SplitCombineEventArgs
            {
                FilePath = selectedItem.FullPath,
                DestinationPath = InactivePanel.CurrentPath
            });
        }
    }
    
    public event EventHandler<SplitCombineEventArgs>? SplitFileRequested;
    public event EventHandler<SplitCombineEventArgs>? CombineFilesRequested;
    
    public class SplitCombineEventArgs : EventArgs
    {
        public string FilePath { get; init; } = string.Empty;
        public string DestinationPath { get; init; } = string.Empty;
    }

    [RelayCommand]
    public void OpenEncodingTool()
    {
        EncodingToolRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? EncodingToolRequested;

    [RelayCommand]
    public void EditSelected()
    {
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
            
        // Open file with system's default editor using shell execute
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = selectedItem.FullPath,
                UseShellExecute = true,
                Verb = "edit"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error editing file: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public void ExecuteCommand()
    {
        if (string.IsNullOrWhiteSpace(CommandLine))
            return;
        
        // Add to history
        AddToCommandHistory(CommandLine);
        
        // Replace placeholders for selected files
        var command = ReplaceCommandPlaceholders(CommandLine);
            
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                WorkingDirectory = ActivePanel.CurrentPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error executing command: {ex.Message}");
        }
        
        CommandLine = string.Empty;
        _commandHistoryIndex = -1;
    }
    
    private string ReplaceCommandPlaceholders(string command)
    {
        // %s - selected file(s) space separated
        // %S - selected file(s) with full path, space separated
        // %P - current path
        // %T - target path (other panel)
        
        var selectedPaths = ActivePanel.GetSelectedPaths().ToList();
        var selectedFiles = selectedPaths.Select(Path.GetFileName).ToList();
        
        command = command.Replace("%P", $"\"{ActivePanel.CurrentPath}\"");
        command = command.Replace("%T", $"\"{InactivePanel.CurrentPath}\"");
        
        if (selectedPaths.Count > 0)
        {
            command = command.Replace("%S", string.Join(" ", selectedPaths.Select(p => $"\"{p}\"")));
            command = command.Replace("%s", string.Join(" ", selectedFiles.Select(f => $"\"{f}\"")));
        }
        else
        {
            command = command.Replace("%S", "");
            command = command.Replace("%s", "");
        }
        
        return command;
    }
    
    private void AddToCommandHistory(string command)
    {
        // Don't add duplicates at the top
        if (_commandHistory.Count > 0 && _commandHistory[0] == command)
            return;
            
        _commandHistory.Insert(0, command);
        CommandHistory.Insert(0, command);
        
        // Limit history size
        while (_commandHistory.Count > MaxCommandHistory)
        {
            _commandHistory.RemoveAt(_commandHistory.Count - 1);
            CommandHistory.RemoveAt(CommandHistory.Count - 1);
        }
        
        SaveCommandHistory();
    }
    
    [RelayCommand]
    public void CommandHistoryUp()
    {
        if (_commandHistory.Count == 0)
            return;
            
        if (_commandHistoryIndex < _commandHistory.Count - 1)
        {
            _commandHistoryIndex++;
            CommandLine = _commandHistory[_commandHistoryIndex];
        }
    }
    
    [RelayCommand]
    public void CommandHistoryDown()
    {
        if (_commandHistoryIndex > 0)
        {
            _commandHistoryIndex--;
            CommandLine = _commandHistory[_commandHistoryIndex];
        }
        else if (_commandHistoryIndex == 0)
        {
            _commandHistoryIndex = -1;
            CommandLine = string.Empty;
        }
    }
    
    private void LoadCommandHistory()
    {
        try
        {
            var historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XCommander", "command_history.json");
                
            if (File.Exists(historyPath))
            {
                var json = File.ReadAllText(historyPath);
                var history = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (history != null)
                {
                    _commandHistory.Clear();
                    CommandHistory.Clear();
                    foreach (var cmd in history.Take(MaxCommandHistory))
                    {
                        _commandHistory.Add(cmd);
                        CommandHistory.Add(cmd);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors loading history
        }
    }
    
    private void SaveCommandHistory()
    {
        try
        {
            var historyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XCommander", "command_history.json");
                
            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(_commandHistory);
            File.WriteAllText(historyPath, json);
        }
        catch
        {
            // Ignore errors saving history
        }
    }
    
    [RelayCommand]
    public void ShowHelp()
    {
        HelpRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? HelpRequested;
    
    [RelayCommand]
    public void ShowAbout()
    {
        AboutRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? AboutRequested;
    
    [RelayCommand]
    public void ConfigureToolbar()
    {
        ToolbarConfigurationRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? ToolbarConfigurationRequested;
    
    // Search functionality
    public SearchViewModel CreateSearchViewModel()
    {
        var vm = new SearchViewModel(_fileSystemService);
        vm.Initialize(ActivePanel.CurrentPath);
        return vm;
    }
    
    public void NavigateToPath(string path)
    {
        ActivePanel.NavigateTo(path);
    }
    
    [RelayCommand]
    public void OpenCommandPalette()
    {
        IsCommandPaletteOpen = true;
        CommandPalette.Open();
    }
    
    [RelayCommand]
    public void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        CommandPalette.Close();
    }
    
    [RelayCommand]
    public void ManagePlugins()
    {
        PluginsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? PluginsRequested;
    
    [RelayCommand]
    public void CustomizeColumns()
    {
        CustomColumnsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? CustomColumnsRequested;
    
    public async Task InitializePluginsAsync()
    {
        PluginManager = new PluginManager();
        var context = new DefaultPluginContext(this, PluginManager);
        
        try
        {
            await PluginManager.DiscoverAndLoadPluginsAsync(context);
            System.Diagnostics.Debug.WriteLine($"Loaded {PluginManager.LoadedPlugins.Count} plugins");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading plugins: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public void Exit()
    {
        Environment.Exit(0);
    }
}

