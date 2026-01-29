using CommunityToolkit.Mvvm.ComponentModel;

namespace XCommander.Models;

public partial class AppSettings : ObservableObject
{
    public AppSettings()
    {
        DefaultLeftPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DefaultRightPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ExternalEditor = OperatingSystem.IsWindows() ? "notepad.exe" :
                         OperatingSystem.IsMacOS() ? "open -e" : "xdg-open";
        TerminalCommand = OperatingSystem.IsWindows() ? "cmd.exe" :
                          OperatingSystem.IsMacOS() ? "open -a Terminal" : "x-terminal-emulator";
    }

    // Appearance
    [ObservableProperty] private bool _useDarkTheme;
    [ObservableProperty] private int _fontSize = 13;
    [ObservableProperty] private string _fontFamily = "Segoe UI";
    [ObservableProperty] private bool _showGridLines = true;
    [ObservableProperty] private bool _showStatusBar = true;
    [ObservableProperty] private bool _showToolbar = true;
    [ObservableProperty] private bool _showToolbarLabels;
    [ObservableProperty] private bool _showCommandLine = true;
    [ObservableProperty] private bool _showFunctionKeyBar = true;
    [ObservableProperty] private bool _showDriveBar = true;
    [ObservableProperty] private bool _showPathBar = true;
    [ObservableProperty] private bool _showDirectoryTree;
    [ObservableProperty] private bool _showBookmarksPanel;
    [ObservableProperty] private bool _showQuickViewPanel;
    [ObservableProperty] private string _defaultViewMode = "Details";
    [ObservableProperty] private int _thumbnailSize = 120;

    // File Display
    [ObservableProperty] private bool _showHiddenFiles;
    [ObservableProperty] private bool _showSystemFiles;
    [ObservableProperty] private bool _showFileExtensions = true;
    [ObservableProperty] private bool _showFileSizes = true;
    [ObservableProperty] private bool _showFileDates = true;
    [ObservableProperty] private string _dateFormat = "yyyy-MM-dd HH:mm";
    [ObservableProperty] private bool _sortDirectoriesFirst = true;
    [ObservableProperty] private bool _sortCaseSensitive;
    [ObservableProperty] private bool _showDescriptionColumn;

    // Behavior
    [ObservableProperty] private bool _confirmDelete = true;
    [ObservableProperty] private bool _confirmOverwrite = true;
    [ObservableProperty] private bool _useRecycleBin = true;
    [ObservableProperty] private bool _singleClickOpen;
    [ObservableProperty] private bool _rememberLastPath = true;
    [ObservableProperty] private string _defaultLeftPath = string.Empty;
    [ObservableProperty] private string _defaultRightPath = string.Empty;
    [ObservableProperty] private bool _quickSearchEnabled = true;
    [ObservableProperty] private string _quickSearchMatchMode = "StartsWithThenContains";
    [ObservableProperty] private int _quickSearchTimeoutMs = 1000;
    [ObservableProperty] private bool _quickFilterCaseSensitive;
    [ObservableProperty] private bool _quickFilterUseRegex;
    [ObservableProperty] private bool _quickFilterIncludeDirectories = true;
    [ObservableProperty] private List<string> _quickFilterHistory = new();

    // Editor
    [ObservableProperty] private string _externalEditor = string.Empty;
    [ObservableProperty] private string _externalViewer = string.Empty;
    [ObservableProperty] private string _terminalCommand = string.Empty;
    [ObservableProperty] private string _fileAssociationDefaultAction = "SystemDefault";

    // Tabs
    [ObservableProperty] private bool _openNewTabOnDriveChange;
    [ObservableProperty] private bool _closeTabOnMiddleClick = true;
    [ObservableProperty] private bool _showTabCloseButton = true;

    // Quick View
    [ObservableProperty] private int _quickViewMaxFileSizeKb = 1024;
    [ObservableProperty] private bool _quickViewAutoUpdate = true;

    // File Operations
    [ObservableProperty] private int _copyBufferSizeKb = 64;
    [ObservableProperty] private bool _verifyAfterCopy;
    [ObservableProperty] private bool _preserveTimestamps = true;

    // Search
    [ObservableProperty] private int _searchMaxResults = 10000;
    [ObservableProperty] private bool _searchIncludeHidden;

    // Localization
    [ObservableProperty] private string _languageCode = string.Empty;

    // Touch Mode
    [ObservableProperty] private bool _touchModeEnabled;
    [ObservableProperty] private double _touchModeItemHeight = 48;
    [ObservableProperty] private double _touchModeSwipeThreshold = 50;
    [ObservableProperty] private int _touchModeLongPressDuration = 500;
    [ObservableProperty] private double _touchModePadding = 8;
    [ObservableProperty] private bool _touchModeEnableSwipeNavigation = true;
    [ObservableProperty] private bool _touchModeEnablePinchZoom = true;

    // Accessibility
    [ObservableProperty] private bool _accessibilityScreenReaderEnabled = true;
    [ObservableProperty] private bool _accessibilityKeyboardIndicatorsEnabled = true;
    [ObservableProperty] private bool _accessibilityHighContrastEnabled;
    [ObservableProperty] private bool _accessibilityReducedMotion;
    [ObservableProperty] private double _accessibilityScaleFactor = 1.0;
    [ObservableProperty] private double _accessibilityMinimumFontSize = 12;
    [ObservableProperty] private bool _accessibilityVerboseAnnouncements;
    [ObservableProperty] private int _accessibilityHoverAnnouncementDelay = 500;
    [ObservableProperty] private bool _accessibilityHighContrastIcons;
    [ObservableProperty] private double _accessibilityFocusOutlineWidth = 2;

    // Session
    [ObservableProperty] private string _lastLeftPath = string.Empty;
    [ObservableProperty] private string _lastRightPath = string.Empty;
    [ObservableProperty] private string _dockLayout = string.Empty;
    [ObservableProperty] private bool _saveSessionOnExit = true;
    [ObservableProperty] private bool _restoreSessionOnStartup = true;
    [ObservableProperty] private int _recentPathsLimit = 20;
    [ObservableProperty] private int _commandHistoryLimit = 100;

    public void ApplyFrom(AppSettings source)
    {
        UseDarkTheme = source.UseDarkTheme;
        FontSize = source.FontSize;
        FontFamily = source.FontFamily;
        ShowGridLines = source.ShowGridLines;
        ShowStatusBar = source.ShowStatusBar;
        ShowToolbar = source.ShowToolbar;
        ShowToolbarLabels = source.ShowToolbarLabels;
        ShowCommandLine = source.ShowCommandLine;
        ShowFunctionKeyBar = source.ShowFunctionKeyBar;
        ShowDriveBar = source.ShowDriveBar;
        ShowPathBar = source.ShowPathBar;
        ShowDirectoryTree = source.ShowDirectoryTree;
        ShowBookmarksPanel = source.ShowBookmarksPanel;
        ShowQuickViewPanel = source.ShowQuickViewPanel;
        DefaultViewMode = source.DefaultViewMode;
        ThumbnailSize = source.ThumbnailSize;
        ShowHiddenFiles = source.ShowHiddenFiles;
        ShowSystemFiles = source.ShowSystemFiles;
        ShowFileExtensions = source.ShowFileExtensions;
        ShowFileSizes = source.ShowFileSizes;
        ShowFileDates = source.ShowFileDates;
        DateFormat = source.DateFormat;
        SortDirectoriesFirst = source.SortDirectoriesFirst;
        SortCaseSensitive = source.SortCaseSensitive;
        ShowDescriptionColumn = source.ShowDescriptionColumn;
        ConfirmDelete = source.ConfirmDelete;
        ConfirmOverwrite = source.ConfirmOverwrite;
        UseRecycleBin = source.UseRecycleBin;
        SingleClickOpen = source.SingleClickOpen;
        RememberLastPath = source.RememberLastPath;
        DefaultLeftPath = source.DefaultLeftPath;
        DefaultRightPath = source.DefaultRightPath;
        QuickSearchEnabled = source.QuickSearchEnabled;
        QuickSearchMatchMode = source.QuickSearchMatchMode;
        QuickSearchTimeoutMs = source.QuickSearchTimeoutMs;
        QuickFilterCaseSensitive = source.QuickFilterCaseSensitive;
        QuickFilterUseRegex = source.QuickFilterUseRegex;
        QuickFilterIncludeDirectories = source.QuickFilterIncludeDirectories;
        QuickFilterHistory = source.QuickFilterHistory.Count == 0
            ? new List<string>()
            : new List<string>(source.QuickFilterHistory);
        ExternalEditor = source.ExternalEditor;
        ExternalViewer = source.ExternalViewer;
        TerminalCommand = source.TerminalCommand;
        FileAssociationDefaultAction = source.FileAssociationDefaultAction;
        OpenNewTabOnDriveChange = source.OpenNewTabOnDriveChange;
        CloseTabOnMiddleClick = source.CloseTabOnMiddleClick;
        ShowTabCloseButton = source.ShowTabCloseButton;
        QuickViewMaxFileSizeKb = source.QuickViewMaxFileSizeKb;
        QuickViewAutoUpdate = source.QuickViewAutoUpdate;
        CopyBufferSizeKb = source.CopyBufferSizeKb;
        VerifyAfterCopy = source.VerifyAfterCopy;
        PreserveTimestamps = source.PreserveTimestamps;
        SearchMaxResults = source.SearchMaxResults;
        SearchIncludeHidden = source.SearchIncludeHidden;
        LanguageCode = source.LanguageCode;
        TouchModeEnabled = source.TouchModeEnabled;
        TouchModeItemHeight = source.TouchModeItemHeight;
        TouchModeSwipeThreshold = source.TouchModeSwipeThreshold;
        TouchModeLongPressDuration = source.TouchModeLongPressDuration;
        TouchModePadding = source.TouchModePadding;
        TouchModeEnableSwipeNavigation = source.TouchModeEnableSwipeNavigation;
        TouchModeEnablePinchZoom = source.TouchModeEnablePinchZoom;
        AccessibilityScreenReaderEnabled = source.AccessibilityScreenReaderEnabled;
        AccessibilityKeyboardIndicatorsEnabled = source.AccessibilityKeyboardIndicatorsEnabled;
        AccessibilityHighContrastEnabled = source.AccessibilityHighContrastEnabled;
        AccessibilityReducedMotion = source.AccessibilityReducedMotion;
        AccessibilityScaleFactor = source.AccessibilityScaleFactor;
        AccessibilityMinimumFontSize = source.AccessibilityMinimumFontSize;
        AccessibilityVerboseAnnouncements = source.AccessibilityVerboseAnnouncements;
        AccessibilityHoverAnnouncementDelay = source.AccessibilityHoverAnnouncementDelay;
        AccessibilityHighContrastIcons = source.AccessibilityHighContrastIcons;
        AccessibilityFocusOutlineWidth = source.AccessibilityFocusOutlineWidth;
        LastLeftPath = source.LastLeftPath;
        LastRightPath = source.LastRightPath;
        DockLayout = source.DockLayout;
        SaveSessionOnExit = source.SaveSessionOnExit;
        RestoreSessionOnStartup = source.RestoreSessionOnStartup;
        RecentPathsLimit = source.RecentPathsLimit;
        CommandHistoryLimit = source.CommandHistoryLimit;
    }

    public AppSettings Clone()
    {
        var copy = new AppSettings();
        copy.ApplyFrom(this);
        return copy;
    }
}
