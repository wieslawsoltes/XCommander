using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Serializer.SystemTextJson;
using XCommander.Models;
using XCommander.Plugins;
using XCommander.Services;
using XCommander.ViewModels.Docking;
using ServicesViewMode = XCommander.Services.ViewMode;
using ServicesViewStyle = XCommander.Services.ViewStyle;
using SessionStateModel = XCommander.Services.SessionState;
using SessionWindowState = XCommander.Services.WindowState;

namespace XCommander.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IAppSettingsService _settingsService;
    private readonly IFileAssociationService? _fileAssociationService;
    private readonly IBackgroundTransferService? _backgroundTransferService;
    private readonly ISessionStateService? _sessionStateService;
    private readonly IUserMenuService? _userMenuService;
    private readonly ICustomViewModesService? _customViewModesService;
    private readonly DockSerializer _dockSerializer = new();
    private TabViewModel? _leftActiveTab;
    private TabViewModel? _rightActiveTab;
    private readonly Dictionary<string, Action> _transferRefreshActions = new();
    private readonly SynchronizationContext? _syncContext;
    private string? _activeTransferId;
    
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
    private BookmarksViewModel _bookmarks;
    
    [ObservableProperty]
    private DirectoryTreeViewModel? _directoryTree;
    
    [ObservableProperty]
    private bool _isDirectoryTreeVisible;
    
    [ObservableProperty]
    private ObservableCollection<ToolbarButton> _toolbarButtons = new();
    
    [ObservableProperty]
    private bool _showToolbarLabels;

    [ObservableProperty]
    private ObservableCollection<UserMenuEntryViewModel> _userMenuItems = new();

    [ObservableProperty]
    private ObservableCollection<ViewModeItemViewModel> _viewModes = new();

    [ObservableProperty]
    private ViewModeItemViewModel? _selectedViewMode;

    [ObservableProperty]
    private MainDockFactory? _dockFactory;

    [ObservableProperty]
    private IRootDock? _dockLayout;

    [ObservableProperty]
    private SessionWindowState? _windowSessionState;

    public AppSettings Settings => _settingsService.Settings;
    
    // Command history
    private readonly List<string> _commandHistory = new();
    private int _commandHistoryIndex = -1;
    
    public ObservableCollection<string> CommandHistory { get; } = new();
    
    public TabbedPanelViewModel InactivePanel => ActivePanel == LeftPanel ? RightPanel : LeftPanel;
    
    private readonly IDescriptionFileService? _descriptionService;
    private readonly ISelectionService? _selectionService;
    private readonly IAdvancedSearchService? _advancedSearchService;
    private readonly IArchiveService? _archiveService;
    
    public MainWindowViewModel(
        IFileSystemService fileSystemService,
        IAppSettingsService settingsService,
        IFileAssociationService? fileAssociationService = null,
        IBackgroundTransferService? backgroundTransferService = null,
        IAdvancedSearchService? advancedSearchService = null,
        IArchiveService? archiveService = null,
        ISessionStateService? sessionStateService = null,
        IDescriptionFileService? descriptionService = null,
        ISelectionService? selectionService = null,
        IUserMenuService? userMenuService = null,
        ICustomViewModesService? customViewModesService = null)
    {
        _fileSystemService = fileSystemService;
        _settingsService = settingsService;
        _fileAssociationService = fileAssociationService;
        _backgroundTransferService = backgroundTransferService;
        _advancedSearchService = advancedSearchService;
        _archiveService = archiveService;
        _sessionStateService = sessionStateService;
        _descriptionService = descriptionService;
        _selectionService = selectionService;
        _userMenuService = userMenuService;
        _customViewModesService = customViewModesService;
        _syncContext = SynchronizationContext.Current;

        if (_backgroundTransferService != null)
        {
            _backgroundTransferService.OperationCompleted += OnBackgroundTransferCompleted;
            _backgroundTransferService.OperationFailed += OnBackgroundTransferFailed;
            _backgroundTransferService.ProgressChanged += OnBackgroundTransferProgress;
        }
        
        _leftPanel = new TabbedPanelViewModel(fileSystemService, settingsService.Settings, descriptionService, selectionService, fileAssociationService) { IsActive = true };
        _rightPanel = new TabbedPanelViewModel(fileSystemService, settingsService.Settings, descriptionService, selectionService, fileAssociationService) { IsActive = false };
        _activePanel = _leftPanel;
        _bookmarks = new BookmarksViewModel(settingsService.Settings);
        _isDirectoryTreeVisible = settingsService.Settings.ShowDirectoryTree;
        _isBookmarksPanelVisible = settingsService.Settings.ShowBookmarksPanel;
        _isQuickViewVisible = settingsService.Settings.ShowQuickViewPanel;
        _showToolbarLabels = settingsService.Settings.ShowToolbarLabels;
        
        // Initialize directory tree
        _directoryTree = new DirectoryTreeViewModel(fileSystemService);
        _directoryTree.NavigationRequested += OnDirectoryTreeNavigation;
        
        // Subscribe to navigation events for recent locations
        _leftPanel.Navigated += OnPanelNavigated;
        _rightPanel.Navigated += OnPanelNavigated;
        _leftPanel.ArchiveOpenRequested += OnArchiveOpenRequested;
        _rightPanel.ArchiveOpenRequested += OnArchiveOpenRequested;
        
        // Initialize to home directory
        var leftPath = ResolveInitialPath(isLeft: true);
        var rightPath = ResolveInitialPath(isLeft: false);
        _leftPanel.Initialize(leftPath);
        _rightPanel.Initialize(rightPath);

        _leftPanel.ActiveTabChanged += (_, _) => AttachActiveTabHandlers(_leftPanel, ref _leftActiveTab);
        _rightPanel.ActiveTabChanged += (_, _) => AttachActiveTabHandlers(_rightPanel, ref _rightActiveTab);
        AttachActiveTabHandlers(_leftPanel, ref _leftActiveTab);
        AttachActiveTabHandlers(_rightPanel, ref _rightActiveTab);
        
        // Load toolbar configuration
        LoadToolbarConfiguration();
        
        // Load command history
        LoadCommandHistory();

        BindCommandPaletteCommands();

        if (_customViewModesService != null)
        {
            _customViewModesService.ViewModeChanged += (_, args) =>
            {
                if (args.NewViewMode != null)
                    UpdateActiveViewMode(args.NewViewMode.Id);
            };
        }

        _ = LoadUserMenuAsync();
        _ = LoadViewModesAsync();

        _settingsService.DockLayoutProvider = SerializeDockLayout;
        InitializeDocking();

        _settingsService.SettingsChanged += (_, _) => ApplySettingsToPanels();
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
        if (sender is TabbedPanelViewModel panel)
        {
            _ = ApplyAutoSelectViewModeAsync(panel, path);
        }

        if (!Settings.RememberLastPath || !Settings.SaveSessionOnExit)
            return;

        if (ReferenceEquals(sender, LeftPanel))
        {
            Settings.LastLeftPath = path;
        }
        else if (ReferenceEquals(sender, RightPanel))
        {
            Settings.LastRightPath = path;
        }
    }

    private void OnArchiveOpenRequested(object? sender, TabViewModel.ArchiveRequestEventArgs e)
    {
        OpenArchiveRequested?.Invoke(this, new ArchiveEventArgs
        {
            ArchivePath = e.ArchivePath,
            ExtractPath = e.ExtractPath
        });
    }

    private string ResolveInitialPath(bool isLeft)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (Settings.RememberLastPath && Settings.RestoreSessionOnStartup)
        {
            var lastPath = isLeft ? Settings.LastLeftPath : Settings.LastRightPath;
            if (!string.IsNullOrWhiteSpace(lastPath) && Directory.Exists(lastPath))
                return lastPath;
        }

        var defaultPath = isLeft ? Settings.DefaultLeftPath : Settings.DefaultRightPath;
        if (!string.IsNullOrWhiteSpace(defaultPath) && Directory.Exists(defaultPath))
            return defaultPath;

        return home;
    }

    private void ApplySettingsToPanels()
    {
        foreach (var tab in LeftPanel.Tabs)
        {
            tab.ShowHiddenFiles = Settings.ShowHiddenFiles;
            tab.ShowSystemFiles = Settings.ShowSystemFiles;
        }

        foreach (var tab in RightPanel.Tabs)
        {
            tab.ShowHiddenFiles = Settings.ShowHiddenFiles;
            tab.ShowSystemFiles = Settings.ShowSystemFiles;
        }

        if (ShowToolbarLabels != Settings.ShowToolbarLabels)
        {
            ShowToolbarLabels = Settings.ShowToolbarLabels;
        }
        UpdateToolbarLabelSetting(Settings.ShowToolbarLabels);

        if (IsDirectoryTreeVisible != Settings.ShowDirectoryTree)
        {
            IsDirectoryTreeVisible = Settings.ShowDirectoryTree;
        }

        if (IsBookmarksPanelVisible != Settings.ShowBookmarksPanel)
        {
            IsBookmarksPanelVisible = Settings.ShowBookmarksPanel;
        }

        if (IsQuickViewVisible != Settings.ShowQuickViewPanel)
        {
            IsQuickViewVisible = Settings.ShowQuickViewPanel;
        }

        TrimCommandHistory();
    }

    private static void UpdateToolbarLabelSetting(bool showLabels)
    {
        var config = ToolbarConfiguration.Load();
        if (config.ShowLabels != showLabels)
        {
            config.ShowLabels = showLabels;
            config.Save();
        }
    }

    private void AttachActiveTabHandlers(TabbedPanelViewModel panel, ref TabViewModel? currentTab)
    {
        if (currentTab != null)
            currentTab.SelectedItemChanged -= OnTabSelectedItemChanged;

        currentTab = panel.ActiveTab;

        if (currentTab != null)
            currentTab.SelectedItemChanged += OnTabSelectedItemChanged;
    }

    private void OnTabSelectedItemChanged(object? sender, EventArgs e)
    {
        if (!Settings.QuickViewAutoUpdate || !IsQuickViewVisible)
            return;

        if (ActivePanel.ActiveTab == sender)
        {
            UpdateQuickView(force: false);
        }
    }

    partial void OnIsDirectoryTreeVisibleChanged(bool value)
    {
        Settings.ShowDirectoryTree = value;
        if (DockFactory?.LeftSplitDock is { } leftSplit)
        {
            leftSplit.IsPaneOpen = value;
        }
    }

    partial void OnIsBookmarksPanelVisibleChanged(bool value)
    {
        Settings.ShowBookmarksPanel = value;
        if (DockFactory?.BookmarksSplitDock is { } bookmarksSplit)
        {
            bookmarksSplit.IsPaneOpen = value;
        }
    }

    partial void OnIsQuickViewVisibleChanged(bool value)
    {
        Settings.ShowQuickViewPanel = value;
        if (DockFactory?.RightSplitDock is { } rightSplit)
        {
            rightSplit.IsPaneOpen = value;
        }

        if (value)
        {
            UpdateQuickView(force: true);
        }
        else
        {
            QuickView.Clear();
        }
    }
    
    [RelayCommand]
    public void ToggleDirectoryTree()
    {
        IsDirectoryTreeVisible = !IsDirectoryTreeVisible;
    }

    [RelayCommand]
    public void ToggleSortDirection()
    {
        var tab = ActivePanel?.ActiveTab;
        if (tab == null)
            return;

        tab.SortByCommand.Execute(tab.SortColumn);
    }
    
    public void LoadToolbarConfiguration()
    {
        var config = ToolbarConfiguration.Load();
        if (Settings.ShowToolbarLabels != config.ShowLabels)
        {
            Settings.ShowToolbarLabels = config.ShowLabels;
        }
        ShowToolbarLabels = config.ShowLabels;
        
        ToolbarButtons.Clear();
        foreach (var button in config.Buttons.Where(b => b.IsVisible).OrderBy(b => b.Order))
        {
            ToolbarButtons.Add(button);
        }
    }

    private void BindCommandPaletteCommands()
    {
        CommandPalette.BindCommands(
            copy: () => CopySelectedCommand.Execute(null),
            move: () => MoveSelectedCommand.Execute(null),
            delete: () => DeleteSelectedCommand.Execute(null),
            rename: () => RenameSelectedCommand.Execute(null),
            newFolder: () => CreateNewFolderCommand.Execute(null),
            newFile: () => CreateNewFileCommand.Execute(null),
            view: () => ViewSelectedCommand.Execute(null),
            edit: () => EditSelectedCommand.Execute(null),
            refresh: () => ActivePanel?.ActiveTab?.RefreshCommand.Execute(null),
            goToParent: () => ActivePanel?.ActiveTab?.GoToParentCommand.Execute(null),
            switchPanel: () => SwitchPanelCommand.Execute(null),
            goBack: () => ActivePanel?.ActiveTab?.GoBackCommand.Execute(null),
            goForward: () => ActivePanel?.ActiveTab?.GoForwardCommand.Execute(null),
            search: () => OpenSearchCommand.Execute(null),
            multiRename: () => MultiRenameCommand.Execute(null),
            compare: () => CompareDirectoriesCommand.Execute(null),
            compareFiles: () => CompareFilesCommand.Execute(null),
            sync: () => SyncDirectoriesCommand.Execute(null),
            checksum: () => CalculateChecksumCommand.Execute(null),
            split: () => SplitFileCommand.Execute(null),
            combine: () => CombineFilesCommand.Execute(null),
            openArchive: () => OpenArchiveCommand.Execute(null),
            createArchive: () => CreateArchiveCommand.Execute(null),
            extractArchive: () => ExtractArchiveCommand.Execute(null),
            ftp: () => OpenFtpConnectionCommand.Execute(null),
            sftp: () => OpenSftpConnectionCommand.Execute(null),
            settings: () => OpenSettingsCommand.Execute(null),
            plugins: () => ManagePluginsCommand.Execute(null),
            columns: () => CustomizeColumnsCommand.Execute(null),
            quickView: () => ToggleQuickViewCommand.Execute(null),
            detailView: () => ActivePanel.SetViewMode(FilePanelViewMode.Details),
            thumbnailView: () => ActivePanel.SetViewMode(FilePanelViewMode.Thumbnails),
            showHidden: () => ActivePanel.ToggleHiddenFiles(),
            commandPalette: () => OpenCommandPaletteCommand.Execute(null),
            about: () => ShowAboutCommand.Execute(null),
            exit: () => ExitCommand.Execute(null));
    }

    private void InitializeDocking()
    {
        DockFactory = new MainDockFactory(LeftPanel, RightPanel, DirectoryTree, Bookmarks, QuickView);
        var restored = TryRestoreDockLayout(out var layout);
        if (layout == null)
        {
            layout = DockFactory.CreateLayout();
        }

        DockFactory.AttachLayout(layout);
        DockFactory.InitLayout(layout);
        DockLayout = layout;

        if (restored)
        {
            SyncPaneVisibilityFromLayout();
        }
        else
        {
            ApplyPaneVisibilityToLayout();
        }
    }

    private bool TryRestoreDockLayout(out IRootDock? layout)
    {
        layout = null;
        if (!Settings.RestoreSessionOnStartup)
        {
            return false;
        }
        var payload = Settings.DockLayout;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            layout = _dockSerializer.Deserialize<IRootDock>(payload);
            return layout != null;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyPaneVisibilityToLayout()
    {
        if (DockFactory?.LeftSplitDock is { } leftSplit)
        {
            leftSplit.IsPaneOpen = IsDirectoryTreeVisible;
        }

        if (DockFactory?.BookmarksSplitDock is { } bookmarksSplit)
        {
            bookmarksSplit.IsPaneOpen = IsBookmarksPanelVisible;
        }

        if (DockFactory?.RightSplitDock is { } rightSplit)
        {
            rightSplit.IsPaneOpen = IsQuickViewVisible;
        }
    }

    private void SyncPaneVisibilityFromLayout()
    {
        if (DockFactory?.LeftSplitDock is { } leftSplit)
        {
            IsDirectoryTreeVisible = leftSplit.IsPaneOpen;
        }

        if (DockFactory?.BookmarksSplitDock is { } bookmarksSplit)
        {
            IsBookmarksPanelVisible = bookmarksSplit.IsPaneOpen;
        }

        if (DockFactory?.RightSplitDock is { } rightSplit)
        {
            IsQuickViewVisible = rightSplit.IsPaneOpen;
        }
    }

    private string? SerializeDockLayout()
    {
        if (DockLayout == null)
        {
            return null;
        }

        try
        {
            var payload = _dockSerializer.Serialize(DockLayout);
            return payload.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public void RestoreSession(SessionStateModel sessionState)
    {
        if (sessionState == null)
            return;

        WindowSessionState = sessionState.Window;

        var leftFallback = ResolveInitialPath(isLeft: true);
        var rightFallback = ResolveInitialPath(isLeft: false);

        LeftPanel.RestoreFromSession(sessionState.LeftPanel, leftFallback);
        RightPanel.RestoreFromSession(sessionState.RightPanel, rightFallback);

        if (string.Equals(sessionState.ActivePanel, "Right", StringComparison.OrdinalIgnoreCase))
        {
            SetActivePanel(RightPanel);
        }
        else
        {
            SetActivePanel(LeftPanel);
        }

        DirectoryTree?.NavigateToPath(ActivePanel.CurrentPath);
    }

    private void CaptureSessionState()
    {
        if (_sessionStateService == null)
            return;

        var leftState = LeftPanel.CreateSessionState("Left");
        var rightState = RightPanel.CreateSessionState("Right");

        _sessionStateService.UpdatePanelState(leftState);
        _sessionStateService.UpdatePanelState(rightState);
        _sessionStateService.SetActivePanel(ReferenceEquals(ActivePanel, RightPanel) ? "Right" : "Left");

        if (WindowSessionState != null)
            _sessionStateService.UpdateWindowState(WindowSessionState);

        UpdateSessionPreferences();
        UpdateSessionHistory();
    }

    private void UpdateSessionPreferences()
    {
        if (_sessionStateService == null)
            return;

        _sessionStateService.UpdatePreferences(preferences =>
        {
            preferences.Theme = Settings.UseDarkTheme ? "Dark" : "Light";
            preferences.FontFamily = Settings.FontFamily;
            preferences.FontSize = Settings.FontSize;
            preferences.ConfirmDelete = Settings.ConfirmDelete;
            preferences.ConfirmOverwrite = Settings.ConfirmOverwrite;
            preferences.ShowToolbar = Settings.ShowToolbar;
            preferences.ShowStatusBar = Settings.ShowStatusBar;
            preferences.ShowCommandLine = Settings.ShowCommandLine;
            preferences.ShowButtonBar = Settings.ShowToolbar;
            preferences.ShowDriveButtons = Settings.ShowDriveBar;
            preferences.MaxRecentItems = Math.Max(1, Settings.RecentPathsLimit);
            preferences.MaxHistoryItems = Math.Max(1, Settings.CommandHistoryLimit);
            preferences.SaveOnExit = Settings.SaveSessionOnExit;
            preferences.RestoreOnStartup = Settings.RestoreSessionOnStartup;
            preferences.DefaultEditor = Settings.ExternalEditor;
            preferences.DefaultViewer = Settings.ExternalViewer;
            preferences.TerminalCommand = Settings.TerminalCommand;
        });
    }

    private void UpdateSessionHistory()
    {
        if (_sessionStateService == null)
            return;

        _sessionStateService.ClearRecentPaths();
        for (var i = Bookmarks.RecentLocations.Count - 1; i >= 0; i--)
        {
            _sessionStateService.AddRecentPath(Bookmarks.RecentLocations[i]);
        }

        _sessionStateService.ClearCommandHistory();
        for (var i = _commandHistory.Count - 1; i >= 0; i--)
        {
            _sessionStateService.AddCommandHistory(_commandHistory[i]);
        }
    }

    [RelayCommand]
    public void SaveSession()
    {
        if (_sessionStateService == null || !Settings.SaveSessionOnExit)
            return;

        CaptureSessionState();
        _sessionStateService.SaveImmediate();
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
            case "RenameSelected":
                RenameSelectedCommand.Execute(null);
                break;
            case "ViewSelected":
                ViewSelectedCommand.Execute(null);
                break;
            case "EditSelected":
                EditSelectedCommand.Execute(null);
                break;
            case "CreateNewFolder":
                CreateNewFolderCommand.Execute(null);
                break;
            case "CreateNewFile":
                CreateNewFileCommand.Execute(null);
                break;
            case "Search":
                OpenSearchCommand.Execute(null);
                break;
            case "NewTab":
                ActivePanel.AddNewTabCommand.Execute(null);
                break;
            case "CloseTab":
                ActivePanel.CloseTabCommand.Execute(ActivePanel.ActiveTab);
                break;
            case "SwitchPanel":
                SwitchPanelCommand.Execute(null);
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
    public async Task ExecuteUserMenuItemAsync(UserMenuItemViewModel? item)
    {
        if (item == null || item.IsSeparator || item.Model.Type == MenuItemType.SubMenu)
            return;
        if (_userMenuService == null)
            return;

        var context = BuildMenuExecutionContext();
        await _userMenuService.ExecuteMenuItemAsync(item.Model, context);
    }

    [RelayCommand]
    public async Task ActivateViewModeAsync(ViewModeItemViewModel? viewMode)
    {
        if (viewMode == null || _customViewModesService == null)
            return;

        await _customViewModesService.ActivateAsync(viewMode.ViewMode.Id);
        ApplyViewMode(viewMode.ViewMode);
        UpdateActiveViewMode(viewMode.ViewMode.Id);
    }

    private async Task LoadUserMenuAsync()
    {
        if (_userMenuService == null)
            return;

        var menu = await _userMenuService.GetMainMenuAsync();
        if (menu == null)
            return;

        var items = BuildUserMenuViewModels(menu.Items);
        if (_syncContext != null)
        {
            _syncContext.Post(_ =>
            {
                UserMenuItems = new ObservableCollection<UserMenuEntryViewModel>(items);
            }, null);
        }
        else
        {
            UserMenuItems = new ObservableCollection<UserMenuEntryViewModel>(items);
        }
    }

    private static List<UserMenuEntryViewModel> BuildUserMenuViewModels(IReadOnlyList<UserMenuItem> items)
    {
        return items
            .OrderBy(i => i.Order)
            .Select(UserMenuViewModelFactory.Create)
            .ToList();
    }

    private async Task LoadViewModesAsync()
    {
        if (_customViewModesService == null)
            return;

        await _customViewModesService.GetSuggestedViewModeAsync(ActivePanel.CurrentPath);
        if (_customViewModesService.ActiveViewMode != null)
        {
            ApplyViewMode(_customViewModesService.ActiveViewMode);
        }
        var viewModes = _customViewModesService.ViewModes
            .OrderBy(mode => mode.Name)
            .Select(mode => new ViewModeItemViewModel(mode)
            {
                IsActive = _customViewModesService.ActiveViewMode?.Id == mode.Id
            })
            .ToList();

        var selected = viewModes.FirstOrDefault(mode => mode.IsActive);
        if (_syncContext != null)
        {
            _syncContext.Post(_ =>
            {
                ViewModes = new ObservableCollection<ViewModeItemViewModel>(viewModes);
                SelectedViewMode = selected;
            }, null);
        }
        else
        {
            ViewModes = new ObservableCollection<ViewModeItemViewModel>(viewModes);
            SelectedViewMode = selected;
        }
    }

    private void UpdateActiveViewMode(string viewModeId)
    {
        foreach (var viewMode in ViewModes)
        {
            viewMode.IsActive = string.Equals(viewMode.ViewMode.Id, viewModeId, StringComparison.OrdinalIgnoreCase);
        }
        SelectedViewMode = ViewModes.FirstOrDefault(mode => mode.IsActive);
    }

    private void ApplyViewMode(ServicesViewMode viewMode)
    {
        var panel = ActivePanel;
        if (panel == null)
            return;

        ApplyViewMode(viewMode, panel);
    }

    private void ApplyViewMode(ServicesViewMode viewMode, TabbedPanelViewModel panel)
    {
        var tab = panel.ActiveTab;
        if (tab == null)
            return;

        panel.SetViewMode(ResolvePanelViewMode(viewMode));
        tab.ShowHiddenFiles = viewMode.ShowHiddenFiles;
        tab.ShowSystemFiles = viewMode.ShowSystemFiles;
    }

    private async Task ApplyAutoSelectViewModeAsync(TabbedPanelViewModel panel, string path)
    {
        if (_customViewModesService == null || panel.ActiveTab == null)
            return;

        ServicesViewMode? suggested = null;
        try
        {
            suggested = await _customViewModesService.GetSuggestedViewModeAsync(path);
        }
        catch
        {
            // Ignore auto-select failures to avoid blocking navigation.
        }

        if (suggested == null)
            return;

        void ApplySuggested()
        {
            var tab = panel.ActiveTab;
            if (tab == null || IsViewModeApplied(tab, suggested))
                return;

            ApplyViewMode(suggested, panel);
            if (ReferenceEquals(panel, ActivePanel))
            {
                _ = _customViewModesService.ActivateAsync(suggested.Id);
            }
        }

        if (_syncContext != null)
        {
            _syncContext.Post(_ => ApplySuggested(), null);
        }
        else
        {
            ApplySuggested();
        }
    }

    private static bool IsViewModeApplied(TabViewModel tab, ServicesViewMode viewMode)
    {
        var panelMode = ResolvePanelViewMode(viewMode);
        return tab.ViewMode == panelMode
               && tab.ShowHiddenFiles == viewMode.ShowHiddenFiles
               && tab.ShowSystemFiles == viewMode.ShowSystemFiles;
    }

    private static FilePanelViewMode ResolvePanelViewMode(ServicesViewMode viewMode)
    {
        return viewMode.Style switch
        {
            ServicesViewStyle.List => FilePanelViewMode.List,
            ServicesViewStyle.Thumbnails => FilePanelViewMode.Thumbnails,
            _ => FilePanelViewMode.Details
        };
    }

    private MenuExecutionContext BuildMenuExecutionContext()
    {
        var sourcePanel = ActivePanel;
        var targetPanel = InactivePanel;
        var selectedItem = sourcePanel.SelectedItem;
        var selectedPaths = sourcePanel.GetSelectedPaths().ToList();
        var selectedTargetPaths = targetPanel.GetSelectedPaths().ToList();
        var currentName = selectedItem?.Name;

        return new MenuExecutionContext
        {
            SourcePath = sourcePanel.CurrentPath,
            TargetPath = targetPanel.CurrentPath,
            CurrentFileName = currentName,
            CurrentFileNameNoExt = currentName != null ? Path.GetFileNameWithoutExtension(currentName) : null,
            CurrentExtension = currentName != null ? Path.GetExtension(currentName) : null,
            SelectedFiles = selectedPaths,
            SelectedFilesSource = selectedPaths,
            SelectedFilesTarget = selectedTargetPaths,
            ChangeSourcePathAsync = path =>
            {
                sourcePanel.NavigateTo(path);
                return Task.CompletedTask;
            },
            ChangeTargetPathAsync = path =>
            {
                targetPanel.NavigateTo(path);
                return Task.CompletedTask;
            },
            ChangeActivePathAsync = path =>
            {
                sourcePanel.NavigateTo(path);
                return Task.CompletedTask;
            },
            InternalCommandHandlerAsync = HandleInternalMenuCommandAsync
        };
    }

    private Task HandleInternalMenuCommandAsync(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return Task.CompletedTask;

        var normalized = action.Trim();
        if (normalized.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("internal:".Length).Trim();
        }

        if (TcCommandMapper.TryMapToToolbarCommand(normalized, out var mapped))
        {
            ExecuteToolbarCommand(mapped);
            return Task.CompletedTask;
        }

        ExecuteToolbarCommand(normalized);
        return Task.CompletedTask;
    }
    
    [RelayCommand]
    public void ToggleQuickView()
    {
        IsQuickViewVisible = !IsQuickViewVisible;
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
    
    public async void UpdateQuickView(bool force = false)
    {
        if (!IsQuickViewVisible)
            return;

        if (!force && !Settings.QuickViewAutoUpdate)
            return;
            
        var selectedItem = ActivePanel.SelectedItem;
        if (selectedItem != null && !selectedItem.IsDirectory && selectedItem.ItemType != FileSystemItemType.ParentDirectory)
        {
            var maxBytes = Settings.QuickViewMaxFileSizeKb > 0
                ? Settings.QuickViewMaxFileSizeKb * 1024L
                : long.MaxValue;
            if (selectedItem.Size > maxBytes)
            {
                QuickView.ShowMessage($"File too large for quick view ({selectedItem.DisplaySize}).");
                return;
            }

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

        if (dialogResult.UseBackgroundTransfer && _backgroundTransferService != null)
        {
            var priority = dialogResult.LowPriority ? TransferPriority.Low : TransferPriority.Normal;
            var options = BuildTransferOptions(dialogResult, isMove: false);
            var operation = await _backgroundTransferService.QueueOperationAsync(
                TransferOperationType.Copy,
                ActivePanel.CurrentPath,
                dialogResult.DestinationPath,
                sourcePaths,
                options,
                priority);

            RegisterTransferRefresh(operation.Id, () => InactivePanel.Refresh());
            OperationStatus = "Copy queued";
            OperationProgress = 0;
            IsOperationInProgress = false;
            return;
        }
            
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

        if (dialogResult.UseBackgroundTransfer && _backgroundTransferService != null)
        {
            var priority = dialogResult.LowPriority ? TransferPriority.Low : TransferPriority.Normal;
            var options = BuildTransferOptions(dialogResult, isMove: true);
            var operation = await _backgroundTransferService.QueueOperationAsync(
                TransferOperationType.Move,
                ActivePanel.CurrentPath,
                dialogResult.DestinationPath,
                sourcePaths,
                options,
                priority);

            RegisterTransferRefresh(operation.Id, () =>
            {
                ActivePanel.Refresh();
                InactivePanel.Refresh();
            });
            OperationStatus = "Move queued";
            OperationProgress = 0;
            IsOperationInProgress = false;
            return;
        }
            
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
            
        DeleteConfirmationResult? dialogResult;
        if (Settings.ConfirmDelete)
        {
            // Request TC-style delete confirmation dialog
            dialogResult = await ShowDeleteConfirmationDialogAsync(sourcePaths);
            if (dialogResult == null || !dialogResult.Confirmed)
                return;
        }
        else
        {
            dialogResult = new DeleteConfirmationResult
            {
                Confirmed = true,
                DeleteMode = Settings.UseRecycleBin ? DeleteMode.RecycleBin : DeleteMode.Permanent
            };
        }

        if (_backgroundTransferService != null)
        {
            var options = new TransferOptions
            {
                UseRecycleBin = dialogResult.DeleteMode == DeleteMode.RecycleBin
            };
            var operation = await _backgroundTransferService.QueueOperationAsync(
                TransferOperationType.Delete,
                ActivePanel.CurrentPath,
                null,
                sourcePaths,
                options);

            RegisterTransferRefresh(operation.Id, () => ActivePanel.Refresh());
            OperationStatus = "Delete queued";
            OperationProgress = 0;
            IsOperationInProgress = false;
            return;
        }
        
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
            Callback = result => tcs.SetResult(result
                ? new DeleteConfirmationResult
                {
                    Confirmed = true,
                    DeleteMode = Settings.UseRecycleBin ? DeleteMode.RecycleBin : DeleteMode.Permanent
                }
                : null)
        });
        return tcs.Task;
    }

    private static TransferOptions BuildTransferOptions(CopyMoveDialogResult dialogResult, bool isMove)
    {
        return new TransferOptions
        {
            PreserveTimestamps = dialogResult.PreserveDateTime,
            PreserveAttributes = dialogResult.PreserveAttributes,
            VerifyAfterTransfer = dialogResult.VerifyAfterCopy,
            DeleteAfterTransfer = isMove,
            OverwriteExisting = dialogResult.OverwriteMode == OverwriteMode.OverwriteAll
                || dialogResult.OverwriteMode == OverwriteMode.OverwriteOlder,
            SkipExisting = dialogResult.OverwriteMode == OverwriteMode.SkipExisting
        };
    }

    private void RegisterTransferRefresh(string operationId, Action refreshAction)
    {
        _transferRefreshActions[operationId] = refreshAction;
        _activeTransferId = operationId;
    }

    private void OnBackgroundTransferCompleted(object? sender, TransferEventArgs e)
    {
        if (!_transferRefreshActions.TryGetValue(e.Operation.Id, out var refresh))
            return;

        _transferRefreshActions.Remove(e.Operation.Id);
        _activeTransferId = null;

        PostToUi(() =>
        {
            refresh();
            OperationProgress = 0;
            OperationStatus = string.Empty;
            IsOperationInProgress = false;
        });
    }

    private void OnBackgroundTransferFailed(object? sender, TransferEventArgs e)
    {
        if (_activeTransferId != null && _activeTransferId.Equals(e.Operation.Id, StringComparison.Ordinal))
        {
            PostToUi(() =>
            {
                OperationStatus = $"Error: {e.Operation.ErrorMessage ?? "Background transfer failed"}";
                IsOperationInProgress = false;
            });
        }

        _transferRefreshActions.Remove(e.Operation.Id);
    }

    private void OnBackgroundTransferProgress(object? sender, TransferProgressEventArgs e)
    {
        if (_activeTransferId == null || !_activeTransferId.Equals(e.Operation.Id, StringComparison.Ordinal))
            return;

        var totalBytes = e.Operation.TotalBytes;
        var processedBytes = e.Operation.ProcessedBytes;
        var percent = totalBytes > 0 ? processedBytes * 100.0 / totalBytes : 0;
        var currentFile = e.CurrentFile ?? e.Operation.CurrentFile ?? string.Empty;

        PostToUi(() =>
        {
            IsOperationInProgress = true;
            OperationProgress = percent;
            OperationStatus = string.IsNullOrWhiteSpace(currentFile)
                ? $"{e.Operation.Type} in progress"
                : $"{e.Operation.Type}: {Path.GetFileName(currentFile)}";
        });
    }

    private void PostToUi(Action action)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
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

        if (TryLaunchAssociatedCommand(_fileAssociationService?.GetViewerCommand(selectedItem.FullPath), selectedItem.FullPath))
            return;

        if (!string.IsNullOrWhiteSpace(Settings.ExternalViewer))
        {
            LaunchExternalTool(Settings.ExternalViewer, selectedItem.FullPath);
            return;
        }

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

        if (TryLaunchAssociatedCommand(_fileAssociationService?.GetEditorCommand(selectedItem.FullPath), selectedItem.FullPath))
            return;

        if (!string.IsNullOrWhiteSpace(Settings.ExternalEditor))
        {
            LaunchExternalTool(Settings.ExternalEditor, selectedItem.FullPath);
            return;
        }
            
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

    private void LaunchExternalTool(string command, string filePath)
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

    private void RunShellCommand(string commandLine, string? workingDirectory)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows()
                ? $"/c {commandLine}"
                : $"-c \"{commandLine}\"",
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? ActivePanel.CurrentPath
                : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        System.Diagnostics.Process.Start(psi);
    }

    private static string QuoteForShell(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private bool TryLaunchAssociatedCommand(string? command, string filePath)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

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
            return false;
        }

        return true;
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
        var limit = GetCommandHistoryLimit();
        while (_commandHistory.Count > limit)
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
                    var limit = GetCommandHistoryLimit();
                    foreach (var cmd in history.Take(limit))
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

    private int GetCommandHistoryLimit()
    {
        var limit = Settings.CommandHistoryLimit;
        return limit <= 0 ? 1 : limit;
    }

    private void TrimCommandHistory()
    {
        var limit = GetCommandHistoryLimit();
        while (_commandHistory.Count > limit)
        {
            _commandHistory.RemoveAt(_commandHistory.Count - 1);
            CommandHistory.RemoveAt(CommandHistory.Count - 1);
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
        var vm = new SearchViewModel(_fileSystemService, Settings, _advancedSearchService, _archiveService);
        vm.Initialize(ActivePanel.CurrentPath);
        return vm;
    }

    [RelayCommand]
    public void OpenSearch()
    {
        SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SearchRequested;
    
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
        if (!IsCommandPaletteOpen && !CommandPalette.IsOpen)
            return;

        IsCommandPaletteOpen = false;
        // Avoid triggering RequestClose -> CloseCommandPalette recursion.
        CommandPalette.IsOpen = false;
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
