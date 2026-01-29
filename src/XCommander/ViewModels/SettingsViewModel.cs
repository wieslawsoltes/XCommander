using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Localization;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;
    private readonly ISettingsNavigationService? _navigationService;

    // Appearance
    [ObservableProperty]
    private bool _useDarkTheme;

    [ObservableProperty]
    private int _fontSize = 13;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private bool _showGridLines = true;

    [ObservableProperty]
    private bool _showStatusBar = true;

    [ObservableProperty]
    private bool _showToolbar = true;

    [ObservableProperty]
    private bool _showToolbarLabels;

    [ObservableProperty]
    private bool _showCommandLine = true;

    [ObservableProperty]
    private bool _showFunctionKeyBar = true;

    [ObservableProperty]
    private bool _showDriveBar = true;

    [ObservableProperty]
    private bool _showPathBar = true;

    [ObservableProperty]
    private bool _showDirectoryTree;

    [ObservableProperty]
    private bool _showBookmarksPanel;

    [ObservableProperty]
    private bool _showQuickViewPanel;

    [ObservableProperty]
    private string _defaultViewMode = "Details";

    [ObservableProperty]
    private int _thumbnailSize = 120;

    // File Display
    [ObservableProperty]
    private bool _showHiddenFiles;

    [ObservableProperty]
    private bool _showSystemFiles;

    [ObservableProperty]
    private bool _showFileExtensions = true;

    [ObservableProperty]
    private bool _showFileSizes = true;

    [ObservableProperty]
    private bool _showFileDates = true;

    [ObservableProperty]
    private string _dateFormat = "yyyy-MM-dd HH:mm";

    [ObservableProperty]
    private bool _sortDirectoriesFirst = true;

    [ObservableProperty]
    private bool _sortCaseSensitive;

    [ObservableProperty]
    private bool _showDescriptionColumn;

    // Behavior
    [ObservableProperty]
    private bool _confirmDelete = true;

    [ObservableProperty]
    private bool _confirmOverwrite = true;

    [ObservableProperty]
    private bool _useRecycleBin = true;

    [ObservableProperty]
    private bool _singleClickOpen;

    [ObservableProperty]
    private bool _rememberLastPath = true;

    [ObservableProperty]
    private string _defaultLeftPath = string.Empty;

    [ObservableProperty]
    private string _defaultRightPath = string.Empty;

    [ObservableProperty]
    private bool _quickSearchEnabled = true;

    [ObservableProperty]
    private string _quickSearchMatchMode = "StartsWithThenContains";

    [ObservableProperty]
    private int _quickSearchTimeoutMs = 1000;

    [ObservableProperty]
    private bool _quickFilterCaseSensitive;

    [ObservableProperty]
    private bool _quickFilterUseRegex;

    [ObservableProperty]
    private bool _quickFilterIncludeDirectories = true;

    // Editor
    [ObservableProperty]
    private string _externalEditor = string.Empty;

    [ObservableProperty]
    private string _externalViewer = string.Empty;

    [ObservableProperty]
    private string _terminalCommand = string.Empty;

    [ObservableProperty]
    private string _fileAssociationDefaultAction = "SystemDefault";

    [ObservableProperty]
    private SettingsOption? _selectedFileAssociationActionOption;

    // Tabs
    [ObservableProperty]
    private bool _openNewTabOnDriveChange;

    [ObservableProperty]
    private bool _closeTabOnMiddleClick = true;

    [ObservableProperty]
    private bool _showTabCloseButton = true;

    // Quick View
    [ObservableProperty]
    private int _quickViewMaxFileSizeKb = 1024;

    [ObservableProperty]
    private bool _quickViewAutoUpdate = true;

    // File Operations
    [ObservableProperty]
    private int _copyBufferSizeKb = 64;

    [ObservableProperty]
    private bool _verifyAfterCopy;

    [ObservableProperty]
    private bool _preserveTimestamps = true;

    // Search
    [ObservableProperty]
    private int _searchMaxResults = 10000;

    [ObservableProperty]
    private bool _searchIncludeHidden;

    // Localization
    [ObservableProperty]
    private string _languageCode = string.Empty;

    [ObservableProperty]
    private SettingsOption? _selectedLanguageOption;

    // Touch Mode
    [ObservableProperty]
    private bool _touchModeEnabled;

    [ObservableProperty]
    private double _touchModeItemHeight = 48;

    [ObservableProperty]
    private double _touchModeSwipeThreshold = 50;

    [ObservableProperty]
    private int _touchModeLongPressDuration = 500;

    [ObservableProperty]
    private double _touchModePadding = 8;

    [ObservableProperty]
    private bool _touchModeEnableSwipeNavigation = true;

    [ObservableProperty]
    private bool _touchModeEnablePinchZoom = true;

    // Accessibility
    [ObservableProperty]
    private bool _accessibilityScreenReaderEnabled = true;

    [ObservableProperty]
    private bool _accessibilityKeyboardIndicatorsEnabled = true;

    [ObservableProperty]
    private bool _accessibilityHighContrastEnabled;

    [ObservableProperty]
    private bool _accessibilityReducedMotion;

    [ObservableProperty]
    private double _accessibilityScaleFactor = 1.0;

    [ObservableProperty]
    private double _accessibilityMinimumFontSize = 12;

    [ObservableProperty]
    private bool _accessibilityVerboseAnnouncements;

    [ObservableProperty]
    private int _accessibilityHoverAnnouncementDelay = 500;

    [ObservableProperty]
    private bool _accessibilityHighContrastIcons;

    [ObservableProperty]
    private double _accessibilityFocusOutlineWidth = 2;

    // Session & History
    [ObservableProperty]
    private bool _saveSessionOnExit = true;

    [ObservableProperty]
    private bool _restoreSessionOnStartup = true;

    [ObservableProperty]
    private int _recentPathsLimit = 20;

    [ObservableProperty]
    private int _commandHistoryLimit = 100;

    public event EventHandler? SettingsSaved;
    public event EventHandler? SettingsLoaded;

    public IReadOnlyList<string> ViewModeOptions { get; } =
        new[] { "Details", "List", "Thumbnails" };

    public IReadOnlyList<string> QuickSearchMatchModeOptions { get; } =
        new[] { "StartsWith", "Contains", "StartsWithThenContains" };

    public IReadOnlyList<SettingsOption> LanguageOptions { get; }
    public IReadOnlyList<SettingsOption> FileAssociationActionOptions { get; } =
        new List<SettingsOption>
        {
            new("SystemDefault", "System Default"),
            new("Viewer", "Viewer"),
            new("Editor", "Editor")
        };

    public SettingsViewModel(IAppSettingsService settingsService, ISettingsNavigationService? navigationService = null)
    {
        _settingsService = settingsService;
        _navigationService = navigationService;
        LanguageOptions = BuildLanguageOptions();
    }

    private static IReadOnlyList<SettingsOption> BuildLanguageOptions()
    {
        var options = new List<SettingsOption>
        {
            new(string.Empty, "System Default")
        };

        foreach (var entry in LocalizationManager.AvailableLanguages)
            options.Add(new SettingsOption(entry.Key, entry.Value));
        return options;
    }

    private static SettingsOption? FindOption(IEnumerable<SettingsOption> options, string key)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        foreach (var option in options)
            return option;

        return null;
    }

    partial void OnSelectedLanguageOptionChanged(SettingsOption? value)
    {
        LanguageCode = value?.Key ?? string.Empty;
    }

    partial void OnSelectedFileAssociationActionOptionChanged(SettingsOption? value)
    {
        FileAssociationDefaultAction = value?.Key ?? "SystemDefault";
    }

    [RelayCommand]
    public void Save()
    {
        try
        {
            var settings = BuildSettingsSnapshot();
            _settingsService.ApplySettings(settings, save: true);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Load()
    {
        try
        {
            ApplyFromSettings(_settingsService.Settings);
            SettingsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            SetDefaults();
        }
    }

    [RelayCommand]
    public void SetDefaults()
    {
        var defaults = new AppSettings();
        ApplyFromSettings(defaults);
    }

    private void ApplyFromSettings(AppSettings settings)
    {
        UseDarkTheme = settings.UseDarkTheme;
        FontSize = settings.FontSize;
        FontFamily = settings.FontFamily;
        ShowGridLines = settings.ShowGridLines;
        ShowStatusBar = settings.ShowStatusBar;
        ShowToolbar = settings.ShowToolbar;
        ShowToolbarLabels = settings.ShowToolbarLabels;
        ShowCommandLine = settings.ShowCommandLine;
        ShowFunctionKeyBar = settings.ShowFunctionKeyBar;
        ShowDriveBar = settings.ShowDriveBar;
        ShowPathBar = settings.ShowPathBar;
        ShowDirectoryTree = settings.ShowDirectoryTree;
        ShowBookmarksPanel = settings.ShowBookmarksPanel;
        ShowQuickViewPanel = settings.ShowQuickViewPanel;
        DefaultViewMode = settings.DefaultViewMode;
        ThumbnailSize = settings.ThumbnailSize;

        ShowHiddenFiles = settings.ShowHiddenFiles;
        ShowSystemFiles = settings.ShowSystemFiles;
        ShowFileExtensions = settings.ShowFileExtensions;
        ShowFileSizes = settings.ShowFileSizes;
        ShowFileDates = settings.ShowFileDates;
        DateFormat = settings.DateFormat;
        SortDirectoriesFirst = settings.SortDirectoriesFirst;
        SortCaseSensitive = settings.SortCaseSensitive;
        ShowDescriptionColumn = settings.ShowDescriptionColumn;

        ConfirmDelete = settings.ConfirmDelete;
        ConfirmOverwrite = settings.ConfirmOverwrite;
        UseRecycleBin = settings.UseRecycleBin;
        SingleClickOpen = settings.SingleClickOpen;
        RememberLastPath = settings.RememberLastPath;
        DefaultLeftPath = settings.DefaultLeftPath;
        DefaultRightPath = settings.DefaultRightPath;
        QuickSearchEnabled = settings.QuickSearchEnabled;
        QuickSearchMatchMode = settings.QuickSearchMatchMode;
        QuickSearchTimeoutMs = settings.QuickSearchTimeoutMs;
        QuickFilterCaseSensitive = settings.QuickFilterCaseSensitive;
        QuickFilterUseRegex = settings.QuickFilterUseRegex;
        QuickFilterIncludeDirectories = settings.QuickFilterIncludeDirectories;

        ExternalEditor = settings.ExternalEditor;
        ExternalViewer = settings.ExternalViewer;
        TerminalCommand = settings.TerminalCommand;
        FileAssociationDefaultAction = settings.FileAssociationDefaultAction;
        SelectedFileAssociationActionOption = FindOption(FileAssociationActionOptions, FileAssociationDefaultAction);

        OpenNewTabOnDriveChange = settings.OpenNewTabOnDriveChange;
        CloseTabOnMiddleClick = settings.CloseTabOnMiddleClick;
        ShowTabCloseButton = settings.ShowTabCloseButton;

        QuickViewMaxFileSizeKb = settings.QuickViewMaxFileSizeKb;
        QuickViewAutoUpdate = settings.QuickViewAutoUpdate;

        CopyBufferSizeKb = settings.CopyBufferSizeKb;
        VerifyAfterCopy = settings.VerifyAfterCopy;
        PreserveTimestamps = settings.PreserveTimestamps;

        SearchMaxResults = settings.SearchMaxResults;
        SearchIncludeHidden = settings.SearchIncludeHidden;

        LanguageCode = settings.LanguageCode;
        SelectedLanguageOption = FindOption(LanguageOptions, LanguageCode);

        TouchModeEnabled = settings.TouchModeEnabled;
        TouchModeItemHeight = settings.TouchModeItemHeight;
        TouchModeSwipeThreshold = settings.TouchModeSwipeThreshold;
        TouchModeLongPressDuration = settings.TouchModeLongPressDuration;
        TouchModePadding = settings.TouchModePadding;
        TouchModeEnableSwipeNavigation = settings.TouchModeEnableSwipeNavigation;
        TouchModeEnablePinchZoom = settings.TouchModeEnablePinchZoom;

        AccessibilityScreenReaderEnabled = settings.AccessibilityScreenReaderEnabled;
        AccessibilityKeyboardIndicatorsEnabled = settings.AccessibilityKeyboardIndicatorsEnabled;
        AccessibilityHighContrastEnabled = settings.AccessibilityHighContrastEnabled;
        AccessibilityReducedMotion = settings.AccessibilityReducedMotion;
        AccessibilityScaleFactor = settings.AccessibilityScaleFactor;
        AccessibilityMinimumFontSize = settings.AccessibilityMinimumFontSize;
        AccessibilityVerboseAnnouncements = settings.AccessibilityVerboseAnnouncements;
        AccessibilityHoverAnnouncementDelay = settings.AccessibilityHoverAnnouncementDelay;
        AccessibilityHighContrastIcons = settings.AccessibilityHighContrastIcons;
        AccessibilityFocusOutlineWidth = settings.AccessibilityFocusOutlineWidth;

        SaveSessionOnExit = settings.SaveSessionOnExit;
        RestoreSessionOnStartup = settings.RestoreSessionOnStartup;
        RecentPathsLimit = settings.RecentPathsLimit;
        CommandHistoryLimit = settings.CommandHistoryLimit;
    }

    private AppSettings BuildSettingsSnapshot()
    {
        return new AppSettings
        {
            UseDarkTheme = UseDarkTheme,
            FontSize = FontSize,
            FontFamily = FontFamily,
            ShowGridLines = ShowGridLines,
            ShowStatusBar = ShowStatusBar,
            ShowToolbar = ShowToolbar,
            ShowToolbarLabels = ShowToolbarLabels,
            ShowCommandLine = ShowCommandLine,
            ShowFunctionKeyBar = ShowFunctionKeyBar,
            ShowDriveBar = ShowDriveBar,
            ShowPathBar = ShowPathBar,
            ShowDirectoryTree = ShowDirectoryTree,
            ShowBookmarksPanel = ShowBookmarksPanel,
            ShowQuickViewPanel = ShowQuickViewPanel,
            DefaultViewMode = DefaultViewMode,
            ThumbnailSize = ThumbnailSize,
            ShowHiddenFiles = ShowHiddenFiles,
            ShowSystemFiles = ShowSystemFiles,
            ShowFileExtensions = ShowFileExtensions,
            ShowFileSizes = ShowFileSizes,
            ShowFileDates = ShowFileDates,
            DateFormat = DateFormat,
            SortDirectoriesFirst = SortDirectoriesFirst,
            SortCaseSensitive = SortCaseSensitive,
            ShowDescriptionColumn = ShowDescriptionColumn,
            ConfirmDelete = ConfirmDelete,
            ConfirmOverwrite = ConfirmOverwrite,
            UseRecycleBin = UseRecycleBin,
            SingleClickOpen = SingleClickOpen,
            RememberLastPath = RememberLastPath,
            DefaultLeftPath = DefaultLeftPath,
            DefaultRightPath = DefaultRightPath,
            QuickSearchEnabled = QuickSearchEnabled,
            QuickSearchMatchMode = QuickSearchMatchMode,
            QuickSearchTimeoutMs = QuickSearchTimeoutMs,
            QuickFilterCaseSensitive = QuickFilterCaseSensitive,
            QuickFilterUseRegex = QuickFilterUseRegex,
            QuickFilterIncludeDirectories = QuickFilterIncludeDirectories,
            QuickFilterHistory = _settingsService.Settings.QuickFilterHistory.Count == 0
                ? new List<string>()
                : new List<string>(_settingsService.Settings.QuickFilterHistory),
            ExternalEditor = ExternalEditor,
            ExternalViewer = ExternalViewer,
            TerminalCommand = TerminalCommand,
            FileAssociationDefaultAction = FileAssociationDefaultAction,
            OpenNewTabOnDriveChange = OpenNewTabOnDriveChange,
            CloseTabOnMiddleClick = CloseTabOnMiddleClick,
            ShowTabCloseButton = ShowTabCloseButton,
            QuickViewMaxFileSizeKb = QuickViewMaxFileSizeKb,
            QuickViewAutoUpdate = QuickViewAutoUpdate,
            CopyBufferSizeKb = CopyBufferSizeKb,
            VerifyAfterCopy = VerifyAfterCopy,
            PreserveTimestamps = PreserveTimestamps,
            SearchMaxResults = SearchMaxResults,
            SearchIncludeHidden = SearchIncludeHidden,
            LanguageCode = LanguageCode,
            TouchModeEnabled = TouchModeEnabled,
            TouchModeItemHeight = TouchModeItemHeight,
            TouchModeSwipeThreshold = TouchModeSwipeThreshold,
            TouchModeLongPressDuration = TouchModeLongPressDuration,
            TouchModePadding = TouchModePadding,
            TouchModeEnableSwipeNavigation = TouchModeEnableSwipeNavigation,
            TouchModeEnablePinchZoom = TouchModeEnablePinchZoom,
            AccessibilityScreenReaderEnabled = AccessibilityScreenReaderEnabled,
            AccessibilityKeyboardIndicatorsEnabled = AccessibilityKeyboardIndicatorsEnabled,
            AccessibilityHighContrastEnabled = AccessibilityHighContrastEnabled,
            AccessibilityReducedMotion = AccessibilityReducedMotion,
            AccessibilityScaleFactor = AccessibilityScaleFactor,
            AccessibilityMinimumFontSize = AccessibilityMinimumFontSize,
            AccessibilityVerboseAnnouncements = AccessibilityVerboseAnnouncements,
            AccessibilityHoverAnnouncementDelay = AccessibilityHoverAnnouncementDelay,
            AccessibilityHighContrastIcons = AccessibilityHighContrastIcons,
            AccessibilityFocusOutlineWidth = AccessibilityFocusOutlineWidth,
            SaveSessionOnExit = SaveSessionOnExit,
            RestoreSessionOnStartup = RestoreSessionOnStartup,
            RecentPathsLimit = RecentPathsLimit,
            CommandHistoryLimit = CommandHistoryLimit
        };
    }

    [RelayCommand]
    private async Task OpenKeyboardShortcutsAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenKeyboardShortcutsAsync();
        }
    }

    [RelayCommand]
    private async Task OpenToolbarConfigurationAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenToolbarConfigurationAsync();
        }
    }

    [RelayCommand]
    private async Task OpenCustomColumnsAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenCustomColumnsAsync();
        }
    }

    [RelayCommand]
    private async Task OpenFileColoringAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenFileColoringAsync();
        }
    }

    [RelayCommand]
    private async Task OpenFileAssociationsAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenFileAssociationsAsync();
        }
    }

    [RelayCommand]
    private async Task OpenPluginsAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenPluginsAsync();
        }
    }

    [RelayCommand]
    private async Task OpenTcConfigImportAsync()
    {
        if (_navigationService != null)
        {
            await _navigationService.OpenTcConfigImportAsync();
        }
    }
}

public sealed record SettingsOption(string Key, string Label);
