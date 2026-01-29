using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Media;
using XCommander.Localization;
using XCommander.Models;

namespace XCommander.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }
    string SettingsPath { get; }
    Func<string?>? DockLayoutProvider { get; set; }
    event EventHandler? SettingsChanged;
    void Load();
    void Save();
    void ApplySettings(AppSettings settings, bool save);
    void ApplyToApplication();
}

public sealed class AppSettingsService : IAppSettingsService
{
    private const string ConfigurationSection = "Configuration";
    private const string LayoutSection = "Layout";
    private const string XCommanderSection = "XCommander";
    private const string MonoFontFallback = "Consolas, Menlo, Monaco, monospace";
    private static readonly string LegacyJsonPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "settings.json");

    public AppSettings Settings { get; } = new();

    public string SettingsPath { get; }

    public Func<string?>? DockLayoutProvider { get; set; }

    public event EventHandler? SettingsChanged;

    private readonly ITouchModeService? _touchModeService;
    private readonly IAccessibilityService? _accessibilityService;

    public AppSettingsService(ITouchModeService? touchModeService, IAccessibilityService? accessibilityService)
        : this(null, touchModeService, accessibilityService)
    {
    }

    public AppSettingsService(string? settingsPath = null)
        : this(settingsPath, null, null)
    {
    }

    private AppSettingsService(string? settingsPath, ITouchModeService? touchModeService, IAccessibilityService? accessibilityService)
    {
        _touchModeService = touchModeService;
        _accessibilityService = accessibilityService;
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander",
            "wincmd.ini");
    }

    public void Load()
    {
        AppSettings loaded;
        if (File.Exists(SettingsPath))
        {
            var ini = IniFile.Load(SettingsPath);
            loaded = LoadFromIni(ini);
        }
        else if (File.Exists(LegacyJsonPath))
        {
            var json = File.ReadAllText(LegacyJsonPath);
            loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            var ini = IniFile.Load(SettingsPath);
            SaveToIni(ini, loaded);
            ini.Save(SettingsPath);
        }
        else
        {
            loaded = new AppSettings();
        }

        Settings.ApplyFrom(loaded);
        ApplyToApplication();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        if (Settings.SaveSessionOnExit)
        {
            var dockLayout = DockLayoutProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(dockLayout))
            {
                Settings.DockLayout = dockLayout;
            }
        }
        else
        {
            Settings.DockLayout = string.Empty;
            Settings.LastLeftPath = string.Empty;
            Settings.LastRightPath = string.Empty;
        }

        var ini = IniFile.Load(SettingsPath);
        SaveToIni(ini, Settings);
        ini.Save(SettingsPath);
    }

    public void ApplySettings(AppSettings settings, bool save)
    {
        Settings.ApplyFrom(settings);
        ApplyToApplication();
        if (save)
        {
            Save();
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyToApplication()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var fontFamily = ResolveFontFamily(Settings.FontFamily);

        app.RequestedThemeVariant = Settings.UseDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

        app.Resources["ContentControlThemeFontFamily"] = fontFamily;
        app.Resources["ControlContentThemeFontSize"] = (double)Settings.FontSize;
        app.Resources["TextElement.FontFamily"] = fontFamily;
        app.Resources["TextElement.FontSize"] = (double)Settings.FontSize;
        app.Resources["DateFormatString"] = Settings.DateFormat;

        // Force MonoFontFamily to a FontFamily instance to avoid resource type mismatches.
        app.Resources["MonoFontFamily"] = ResolveFontFamily(MonoFontFallback);

        ApplyLocalization();
        ApplyTouchModeSettings();
        ApplyAccessibilitySettings();
    }

    private void ApplyLocalization()
    {
        var language = Settings.LanguageCode;
        if (string.IsNullOrWhiteSpace(language))
        {
            var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (LocalizationManager.AvailableLanguages.ContainsKey(systemLanguage))
            {
                LocalizationManager.Instance.CurrentLanguage = systemLanguage;
            }
            else
            {
                LocalizationManager.Instance.CurrentLanguage = "en";
            }
            return;
        }

        LocalizationManager.Instance.CurrentLanguage = language;
    }

    private void ApplyTouchModeSettings()
    {
        if (_touchModeService == null)
            return;

        _touchModeService.Settings.ItemHeight = Settings.TouchModeItemHeight;
        _touchModeService.Settings.SwipeThreshold = Settings.TouchModeSwipeThreshold;
        _touchModeService.Settings.LongPressDuration = Settings.TouchModeLongPressDuration;
        _touchModeService.Settings.TouchPadding = Settings.TouchModePadding;
        _touchModeService.Settings.EnableSwipeNavigation = Settings.TouchModeEnableSwipeNavigation;
        _touchModeService.Settings.EnablePinchZoom = Settings.TouchModeEnablePinchZoom;

        if (Settings.TouchModeEnabled)
        {
            _touchModeService.Enable();
        }
        else
        {
            _touchModeService.Disable();
        }
    }

    private void ApplyAccessibilitySettings()
    {
        if (_accessibilityService == null)
            return;

        var settings = new AccessibilitySettings
        {
            ScreenReaderEnabled = Settings.AccessibilityScreenReaderEnabled,
            KeyboardIndicatorsEnabled = Settings.AccessibilityKeyboardIndicatorsEnabled,
            HighContrastEnabled = Settings.AccessibilityHighContrastEnabled,
            ReducedMotion = Settings.AccessibilityReducedMotion,
            ScaleFactor = Settings.AccessibilityScaleFactor,
            MinimumFontSize = Settings.AccessibilityMinimumFontSize,
            VerboseAnnouncements = Settings.AccessibilityVerboseAnnouncements,
            HoverAnnouncementDelay = Settings.AccessibilityHoverAnnouncementDelay,
            HighContrastIcons = Settings.AccessibilityHighContrastIcons,
            FocusOutlineWidth = Settings.AccessibilityFocusOutlineWidth
        };

        _accessibilityService.UpdateSettings(settings);
    }

    private static FontFamily ResolveFontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FontFamily.Default;

        try
        {
            return new FontFamily(value);
        }
        catch
        {
            return FontFamily.Default;
        }
    }

    private static void EnsureFontFamilyResource(Application app, string key, string fallback)
    {
        if (app.Resources.TryGetValue(key, out var existing))
        {
            if (existing is string existingString)
            {
                app.Resources[key] = ResolveFontFamily(existingString);
            }
            return;
        }

        app.Resources[key] = ResolveFontFamily(fallback);
    }

    private static AppSettings LoadFromIni(IniFile ini)
    {
        var settings = new AppSettings();
        var config = ini.GetSection(ConfigurationSection, false);
        var layout = ini.GetSection(LayoutSection, false);
        var xcmd = ini.GetSection(XCommanderSection, false);

        var darkMode = ReadIntFromKeys(-1, (config, "DarkMode"), (config, "DarkTheme"), (xcmd, "DarkMode"));
        settings.UseDarkTheme = darkMode >= 0
            ? darkMode != 0
            : ReadBoolFromKeys(settings.UseDarkTheme, (config, "UseDarkTheme"), (xcmd, "UseDarkTheme"));

        settings.FontSize = ReadIntFromKeys(settings.FontSize, (config, "FontSize"), (xcmd, "FontSize"));
        settings.FontFamily = ReadStringFromKeys(settings.FontFamily,
            (config, "FontName"), (config, "FontFamily"), (xcmd, "FontFamily"));

        var showGrid = ReadIntFromKeys(-1, (config, "ShowGrid"), (xcmd, "ShowGrid"));
        settings.ShowGridLines = showGrid >= 0
            ? showGrid != 0
            : ReadBoolFromKeys(settings.ShowGridLines, (config, "ShowGridLines"), (xcmd, "ShowGridLines"));

        settings.ShowStatusBar = ReadBoolFromKeys(settings.ShowStatusBar,
            (layout, "ShowStatusBar"), (config, "ShowStatusBar"), (xcmd, "ShowStatusBar"));
        settings.ShowToolbar = ReadBoolFromKeys(settings.ShowToolbar,
            (layout, "ButtonBar"), (layout, "ShowToolbar"), (config, "ShowToolbar"), (xcmd, "ShowToolbar"));
        settings.ShowCommandLine = ReadBoolFromKeys(settings.ShowCommandLine,
            (layout, "ShowCmdLine"), (layout, "ShowCommandLine"), (xcmd, "ShowCommandLine"));
        settings.ShowFunctionKeyBar = ReadBoolFromKeys(settings.ShowFunctionKeyBar,
            (layout, "ShowKeyBar"), (layout, "ShowFunctionKeyBar"), (xcmd, "ShowFunctionKeyBar"));
        settings.ShowDriveBar = ReadBoolFromKeys(settings.ShowDriveBar,
            (layout, "DriveBar1"), (layout, "ShowDriveBar"), (xcmd, "ShowDriveBar"));
        settings.ShowPathBar = ReadBoolFromKeys(settings.ShowPathBar,
            (layout, "ShowPathBar"), (layout, "ShowPathLine"), (xcmd, "ShowPathBar"));
        settings.ShowToolbarLabels = ReadBoolFromKeys(settings.ShowToolbarLabels,
            (layout, "ShowToolbarLabels"), (xcmd, "ShowToolbarLabels"));
        settings.ShowDirectoryTree = ReadBoolFromKeys(settings.ShowDirectoryTree,
            (layout, "ShowDirectoryTree"), (xcmd, "ShowDirectoryTree"));
        settings.ShowBookmarksPanel = ReadBoolFromKeys(settings.ShowBookmarksPanel,
            (layout, "ShowBookmarksPanel"), (xcmd, "ShowBookmarksPanel"));
        settings.ShowQuickViewPanel = ReadBoolFromKeys(settings.ShowQuickViewPanel,
            (layout, "ShowQuickViewPanel"), (xcmd, "ShowQuickViewPanel"));
        settings.DefaultViewMode = ReadStringFromKeys(settings.DefaultViewMode,
            (xcmd, "DefaultViewMode"));
        settings.ThumbnailSize = ReadIntFromKeys(settings.ThumbnailSize,
            (xcmd, "ThumbnailSize"));

        var showHiddenSystem = ReadIntFromKeys(-1, (config, "ShowHiddenSystem"), (xcmd, "ShowHiddenSystem"));
        if (showHiddenSystem >= 0)
        {
            settings.ShowHiddenFiles = showHiddenSystem >= 1;
            settings.ShowSystemFiles = showHiddenSystem >= 2;
        }
        else
        {
            settings.ShowHiddenFiles = ReadBoolFromKeys(settings.ShowHiddenFiles,
                (config, "ShowHiddenFiles"), (config, "ShowHidden"), (xcmd, "ShowHiddenFiles"));
            settings.ShowSystemFiles = ReadBoolFromKeys(settings.ShowSystemFiles,
                (config, "ShowSystemFiles"), (config, "ShowSystem"), (xcmd, "ShowSystemFiles"));
        }

        var showExt = ReadIntFromKeys(-1, (config, "ShowFileExt"), (xcmd, "ShowFileExt"));
        settings.ShowFileExtensions = showExt >= 0
            ? showExt != 0
            : ReadBoolFromKeys(settings.ShowFileExtensions,
                (config, "ShowFileExtensions"), (xcmd, "ShowFileExtensions"));

        var showSize = ReadIntFromKeys(-1, (config, "ShowFileSize"), (config, "ShowSize"), (xcmd, "ShowFileSize"));
        settings.ShowFileSizes = showSize >= 0
            ? showSize != 0
            : ReadBoolFromKeys(settings.ShowFileSizes,
                (config, "ShowFileSizes"), (xcmd, "ShowFileSizes"));

        var showDate = ReadIntFromKeys(-1, (config, "ShowDate"), (config, "ShowFileDate"), (xcmd, "ShowDate"));
        settings.ShowFileDates = showDate >= 0
            ? showDate != 0
            : ReadBoolFromKeys(settings.ShowFileDates,
                (config, "ShowFileDates"), (xcmd, "ShowFileDates"));

        settings.DateFormat = ReadStringFromKeys(settings.DateFormat,
            (xcmd, "DateFormat"), (config, "DateFormat"), (config, "DateTimeFormat"));
        settings.SortDirectoriesFirst = ReadBoolFromKeys(settings.SortDirectoriesFirst,
            (xcmd, "SortDirectoriesFirst"));
        settings.SortCaseSensitive = ReadBoolFromKeys(settings.SortCaseSensitive,
            (xcmd, "SortCaseSensitive"));
        settings.ShowDescriptionColumn = ReadBoolFromKeys(settings.ShowDescriptionColumn,
            (xcmd, "ShowDescriptionColumn"));

        settings.ConfirmDelete = ReadBoolFromKeys(settings.ConfirmDelete,
            (xcmd, "ConfirmDelete"), (config, "ConfirmDelete"));
        settings.ConfirmOverwrite = ReadBoolFromKeys(settings.ConfirmOverwrite,
            (xcmd, "ConfirmOverwrite"), (config, "ConfirmOverwrite"));
        settings.UseRecycleBin = ReadBoolFromKeys(settings.UseRecycleBin,
            (config, "UseTrash"), (config, "DeleteToTrash"), (xcmd, "UseRecycleBin"));
        settings.SingleClickOpen = ReadBoolFromKeys(settings.SingleClickOpen,
            (config, "SingleClickStart"), (config, "SingleClickOpen"), (xcmd, "SingleClickOpen"));
        settings.RememberLastPath = ReadBoolFromKeys(settings.RememberLastPath,
            (config, "SaveLastPath"), (config, "RememberLastPath"), (xcmd, "RememberLastPath"));
        settings.DefaultLeftPath = ReadStringFromKeys(settings.DefaultLeftPath,
            (xcmd, "DefaultLeftPath"), (config, "LeftPath"));
        settings.DefaultRightPath = ReadStringFromKeys(settings.DefaultRightPath,
            (xcmd, "DefaultRightPath"), (config, "RightPath"));
        settings.QuickSearchEnabled = ReadBoolFromKeys(settings.QuickSearchEnabled,
            (xcmd, "QuickSearchEnabled"));
        settings.QuickSearchMatchMode = ReadStringFromKeys(settings.QuickSearchMatchMode,
            (xcmd, "QuickSearchMatchMode"));
        settings.QuickSearchTimeoutMs = ReadIntFromKeys(settings.QuickSearchTimeoutMs,
            (xcmd, "QuickSearchTimeoutMs"));
        settings.QuickFilterCaseSensitive = ReadBoolFromKeys(settings.QuickFilterCaseSensitive,
            (xcmd, "QuickFilterCaseSensitive"));
        settings.QuickFilterUseRegex = ReadBoolFromKeys(settings.QuickFilterUseRegex,
            (xcmd, "QuickFilterUseRegex"));
        settings.QuickFilterIncludeDirectories = ReadBoolFromKeys(settings.QuickFilterIncludeDirectories,
            (xcmd, "QuickFilterIncludeDirectories"));
        settings.QuickFilterHistory = ReadListFromKeyPrefix(xcmd, "QuickFilterHistory");

        settings.ExternalEditor = ReadStringFromKeys(settings.ExternalEditor,
            (config, "Editor"), (xcmd, "ExternalEditor"));
        settings.ExternalViewer = ReadStringFromKeys(settings.ExternalViewer,
            (config, "Viewer"), (xcmd, "ExternalViewer"));
        settings.TerminalCommand = ReadStringFromKeys(settings.TerminalCommand,
            (config, "Terminal"), (xcmd, "TerminalCommand"));
        settings.FileAssociationDefaultAction = ReadStringFromKeys(settings.FileAssociationDefaultAction,
            (xcmd, "FileAssociationDefaultAction"));

        settings.OpenNewTabOnDriveChange = ReadBoolFromKeys(settings.OpenNewTabOnDriveChange,
            (xcmd, "OpenNewTabOnDriveChange"));
        settings.CloseTabOnMiddleClick = ReadBoolFromKeys(settings.CloseTabOnMiddleClick,
            (xcmd, "CloseTabOnMiddleClick"));
        settings.ShowTabCloseButton = ReadBoolFromKeys(settings.ShowTabCloseButton,
            (xcmd, "ShowTabCloseButton"));

        settings.QuickViewMaxFileSizeKb = ReadIntFromKeys(settings.QuickViewMaxFileSizeKb,
            (xcmd, "QuickViewMaxFileSizeKb"));
        settings.QuickViewAutoUpdate = ReadBoolFromKeys(settings.QuickViewAutoUpdate,
            (xcmd, "QuickViewAutoUpdate"));

        settings.CopyBufferSizeKb = ReadIntFromKeys(settings.CopyBufferSizeKb,
            (xcmd, "CopyBufferSizeKb"));
        settings.VerifyAfterCopy = ReadBoolFromKeys(settings.VerifyAfterCopy,
            (xcmd, "VerifyAfterCopy"));
        settings.PreserveTimestamps = ReadBoolFromKeys(settings.PreserveTimestamps,
            (xcmd, "PreserveTimestamps"));

        settings.SearchMaxResults = ReadIntFromKeys(settings.SearchMaxResults,
            (xcmd, "SearchMaxResults"));
        settings.SearchIncludeHidden = ReadBoolFromKeys(settings.SearchIncludeHidden,
            (xcmd, "SearchIncludeHidden"));

        settings.LanguageCode = ReadStringFromKeys(settings.LanguageCode,
            (xcmd, "LanguageCode"), (xcmd, "Language"));

        settings.TouchModeEnabled = ReadBoolFromKeys(settings.TouchModeEnabled,
            (xcmd, "TouchModeEnabled"));
        settings.TouchModeItemHeight = ReadDoubleFromKeys(settings.TouchModeItemHeight,
            (xcmd, "TouchModeItemHeight"));
        settings.TouchModeSwipeThreshold = ReadDoubleFromKeys(settings.TouchModeSwipeThreshold,
            (xcmd, "TouchModeSwipeThreshold"));
        settings.TouchModeLongPressDuration = ReadIntFromKeys(settings.TouchModeLongPressDuration,
            (xcmd, "TouchModeLongPressDuration"));
        settings.TouchModePadding = ReadDoubleFromKeys(settings.TouchModePadding,
            (xcmd, "TouchModePadding"));
        settings.TouchModeEnableSwipeNavigation = ReadBoolFromKeys(settings.TouchModeEnableSwipeNavigation,
            (xcmd, "TouchModeEnableSwipeNavigation"));
        settings.TouchModeEnablePinchZoom = ReadBoolFromKeys(settings.TouchModeEnablePinchZoom,
            (xcmd, "TouchModeEnablePinchZoom"));

        settings.AccessibilityScreenReaderEnabled = ReadBoolFromKeys(settings.AccessibilityScreenReaderEnabled,
            (xcmd, "AccessibilityScreenReaderEnabled"));
        settings.AccessibilityKeyboardIndicatorsEnabled = ReadBoolFromKeys(settings.AccessibilityKeyboardIndicatorsEnabled,
            (xcmd, "AccessibilityKeyboardIndicatorsEnabled"));
        settings.AccessibilityHighContrastEnabled = ReadBoolFromKeys(settings.AccessibilityHighContrastEnabled,
            (xcmd, "AccessibilityHighContrastEnabled"));
        settings.AccessibilityReducedMotion = ReadBoolFromKeys(settings.AccessibilityReducedMotion,
            (xcmd, "AccessibilityReducedMotion"));
        settings.AccessibilityScaleFactor = ReadDoubleFromKeys(settings.AccessibilityScaleFactor,
            (xcmd, "AccessibilityScaleFactor"));
        settings.AccessibilityMinimumFontSize = ReadDoubleFromKeys(settings.AccessibilityMinimumFontSize,
            (xcmd, "AccessibilityMinimumFontSize"));
        settings.AccessibilityVerboseAnnouncements = ReadBoolFromKeys(settings.AccessibilityVerboseAnnouncements,
            (xcmd, "AccessibilityVerboseAnnouncements"));
        settings.AccessibilityHoverAnnouncementDelay = ReadIntFromKeys(settings.AccessibilityHoverAnnouncementDelay,
            (xcmd, "AccessibilityHoverAnnouncementDelay"));
        settings.AccessibilityHighContrastIcons = ReadBoolFromKeys(settings.AccessibilityHighContrastIcons,
            (xcmd, "AccessibilityHighContrastIcons"));
        settings.AccessibilityFocusOutlineWidth = ReadDoubleFromKeys(settings.AccessibilityFocusOutlineWidth,
            (xcmd, "AccessibilityFocusOutlineWidth"));

        settings.LastLeftPath = ReadStringFromKeys(settings.LastLeftPath,
            (xcmd, "LastLeftPath"), (config, "LeftPath"));
        settings.LastRightPath = ReadStringFromKeys(settings.LastRightPath,
            (xcmd, "LastRightPath"), (config, "RightPath"));
        settings.DockLayout = ReadStringFromKeys(settings.DockLayout,
            (layout, "DockLayout"), (xcmd, "DockLayout"));
        settings.SaveSessionOnExit = ReadBoolFromKeys(settings.SaveSessionOnExit,
            (xcmd, "SaveSessionOnExit"));
        settings.RestoreSessionOnStartup = ReadBoolFromKeys(settings.RestoreSessionOnStartup,
            (xcmd, "RestoreSessionOnStartup"));
        settings.RecentPathsLimit = ReadIntFromKeys(settings.RecentPathsLimit,
            (xcmd, "RecentPathsLimit"));
        settings.CommandHistoryLimit = ReadIntFromKeys(settings.CommandHistoryLimit,
            (xcmd, "CommandHistoryLimit"));

        return settings;
    }

    private static void SaveToIni(IniFile ini, AppSettings settings)
    {
        ini.SetValue(ConfigurationSection, "DarkMode", settings.UseDarkTheme ? "1" : "0");
        ini.SetValue(ConfigurationSection, "FontSize", settings.FontSize.ToString());
        ini.SetValue(ConfigurationSection, "FontName", settings.FontFamily);
        ini.SetValue(ConfigurationSection, "ShowGrid", settings.ShowGridLines ? "1" : "0");

        var hiddenSystem = settings.ShowSystemFiles ? 2 : settings.ShowHiddenFiles ? 1 : 0;
        ini.SetValue(ConfigurationSection, "ShowHiddenSystem", hiddenSystem.ToString());
        ini.SetValue(ConfigurationSection, "ShowFileExt", settings.ShowFileExtensions ? "1" : "0");
        ini.SetValue(ConfigurationSection, "ShowFileSize", settings.ShowFileSizes ? "1" : "0");
        ini.SetValue(ConfigurationSection, "ShowDate", settings.ShowFileDates ? "1" : "0");
        ini.SetValue(ConfigurationSection, "Editor", settings.ExternalEditor);
        ini.SetValue(ConfigurationSection, "Viewer", settings.ExternalViewer);
        ini.SetValue(ConfigurationSection, "UseTrash", settings.UseRecycleBin ? "1" : "0");
        ini.SetValue(ConfigurationSection, "SingleClickStart", settings.SingleClickOpen ? "1" : "0");
        ini.SetValue(ConfigurationSection, "SaveLastPath", settings.RememberLastPath ? "1" : "0");
        if (!string.IsNullOrWhiteSpace(settings.LastLeftPath))
            ini.SetValue(ConfigurationSection, "LeftPath", settings.LastLeftPath);
        if (!string.IsNullOrWhiteSpace(settings.LastRightPath))
            ini.SetValue(ConfigurationSection, "RightPath", settings.LastRightPath);

        ini.SetValue(LayoutSection, "ShowStatusBar", WriteBool(settings.ShowStatusBar));
        ini.SetValue(LayoutSection, "ButtonBar", WriteBool(settings.ShowToolbar));
        ini.SetValue(LayoutSection, "ShowCmdLine", WriteBool(settings.ShowCommandLine));
        ini.SetValue(LayoutSection, "ShowKeyBar", WriteBool(settings.ShowFunctionKeyBar));
        ini.SetValue(LayoutSection, "DriveBar1", WriteBool(settings.ShowDriveBar));
        ini.SetValue(LayoutSection, "ShowPathBar", WriteBool(settings.ShowPathBar));
        ini.SetValue(LayoutSection, "ShowToolbarLabels", WriteBool(settings.ShowToolbarLabels));
        ini.SetValue(LayoutSection, "ShowDirectoryTree", WriteBool(settings.ShowDirectoryTree));
        ini.SetValue(LayoutSection, "ShowBookmarksPanel", WriteBool(settings.ShowBookmarksPanel));
        ini.SetValue(LayoutSection, "ShowQuickViewPanel", WriteBool(settings.ShowQuickViewPanel));

        ini.SetValue(XCommanderSection, "UseDarkTheme", WriteBool(settings.UseDarkTheme));
        ini.SetValue(XCommanderSection, "FontSize", settings.FontSize.ToString());
        ini.SetValue(XCommanderSection, "FontFamily", settings.FontFamily);
        ini.SetValue(XCommanderSection, "ShowGridLines", WriteBool(settings.ShowGridLines));
        ini.SetValue(XCommanderSection, "ShowStatusBar", WriteBool(settings.ShowStatusBar));
        ini.SetValue(XCommanderSection, "ShowToolbar", WriteBool(settings.ShowToolbar));
        ini.SetValue(XCommanderSection, "ShowToolbarLabels", WriteBool(settings.ShowToolbarLabels));
        ini.SetValue(XCommanderSection, "ShowCommandLine", WriteBool(settings.ShowCommandLine));
        ini.SetValue(XCommanderSection, "ShowFunctionKeyBar", WriteBool(settings.ShowFunctionKeyBar));
        ini.SetValue(XCommanderSection, "ShowDriveBar", WriteBool(settings.ShowDriveBar));
        ini.SetValue(XCommanderSection, "ShowPathBar", WriteBool(settings.ShowPathBar));
        ini.SetValue(XCommanderSection, "ShowDirectoryTree", WriteBool(settings.ShowDirectoryTree));
        ini.SetValue(XCommanderSection, "ShowBookmarksPanel", WriteBool(settings.ShowBookmarksPanel));
        ini.SetValue(XCommanderSection, "ShowQuickViewPanel", WriteBool(settings.ShowQuickViewPanel));
        ini.SetValue(XCommanderSection, "DefaultViewMode", settings.DefaultViewMode);
        ini.SetValue(XCommanderSection, "ThumbnailSize", settings.ThumbnailSize.ToString());

        ini.SetValue(XCommanderSection, "ShowHiddenFiles", WriteBool(settings.ShowHiddenFiles));
        ini.SetValue(XCommanderSection, "ShowSystemFiles", WriteBool(settings.ShowSystemFiles));
        ini.SetValue(XCommanderSection, "ShowFileExtensions", WriteBool(settings.ShowFileExtensions));
        ini.SetValue(XCommanderSection, "ShowFileSizes", WriteBool(settings.ShowFileSizes));
        ini.SetValue(XCommanderSection, "ShowFileDates", WriteBool(settings.ShowFileDates));
        ini.SetValue(XCommanderSection, "DateFormat", settings.DateFormat);
        ini.SetValue(XCommanderSection, "SortDirectoriesFirst", WriteBool(settings.SortDirectoriesFirst));
        ini.SetValue(XCommanderSection, "SortCaseSensitive", WriteBool(settings.SortCaseSensitive));
        ini.SetValue(XCommanderSection, "ShowDescriptionColumn", WriteBool(settings.ShowDescriptionColumn));

        ini.SetValue(XCommanderSection, "ConfirmDelete", WriteBool(settings.ConfirmDelete));
        ini.SetValue(XCommanderSection, "ConfirmOverwrite", WriteBool(settings.ConfirmOverwrite));
        ini.SetValue(XCommanderSection, "UseRecycleBin", WriteBool(settings.UseRecycleBin));
        ini.SetValue(XCommanderSection, "SingleClickOpen", WriteBool(settings.SingleClickOpen));
        ini.SetValue(XCommanderSection, "RememberLastPath", WriteBool(settings.RememberLastPath));
        ini.SetValue(XCommanderSection, "DefaultLeftPath", settings.DefaultLeftPath);
        ini.SetValue(XCommanderSection, "DefaultRightPath", settings.DefaultRightPath);
        ini.SetValue(XCommanderSection, "QuickSearchEnabled", WriteBool(settings.QuickSearchEnabled));
        ini.SetValue(XCommanderSection, "QuickSearchMatchMode", settings.QuickSearchMatchMode);
        ini.SetValue(XCommanderSection, "QuickSearchTimeoutMs", settings.QuickSearchTimeoutMs.ToString());
        ini.SetValue(XCommanderSection, "QuickFilterCaseSensitive", WriteBool(settings.QuickFilterCaseSensitive));
        ini.SetValue(XCommanderSection, "QuickFilterUseRegex", WriteBool(settings.QuickFilterUseRegex));
        ini.SetValue(XCommanderSection, "QuickFilterIncludeDirectories", WriteBool(settings.QuickFilterIncludeDirectories));

        WriteListWithPrefix(ini, XCommanderSection, "QuickFilterHistory", settings.QuickFilterHistory);

        ini.SetValue(XCommanderSection, "ExternalEditor", settings.ExternalEditor);
        ini.SetValue(XCommanderSection, "ExternalViewer", settings.ExternalViewer);
        ini.SetValue(XCommanderSection, "TerminalCommand", settings.TerminalCommand);
        ini.SetValue(XCommanderSection, "FileAssociationDefaultAction", settings.FileAssociationDefaultAction);

        ini.SetValue(XCommanderSection, "OpenNewTabOnDriveChange", WriteBool(settings.OpenNewTabOnDriveChange));
        ini.SetValue(XCommanderSection, "CloseTabOnMiddleClick", WriteBool(settings.CloseTabOnMiddleClick));
        ini.SetValue(XCommanderSection, "ShowTabCloseButton", WriteBool(settings.ShowTabCloseButton));

        ini.SetValue(XCommanderSection, "QuickViewMaxFileSizeKb", settings.QuickViewMaxFileSizeKb.ToString());
        ini.SetValue(XCommanderSection, "QuickViewAutoUpdate", WriteBool(settings.QuickViewAutoUpdate));

        ini.SetValue(XCommanderSection, "CopyBufferSizeKb", settings.CopyBufferSizeKb.ToString());
        ini.SetValue(XCommanderSection, "VerifyAfterCopy", WriteBool(settings.VerifyAfterCopy));
        ini.SetValue(XCommanderSection, "PreserveTimestamps", WriteBool(settings.PreserveTimestamps));

        ini.SetValue(XCommanderSection, "SearchMaxResults", settings.SearchMaxResults.ToString());
        ini.SetValue(XCommanderSection, "SearchIncludeHidden", WriteBool(settings.SearchIncludeHidden));
        ini.SetValue(XCommanderSection, "LanguageCode", settings.LanguageCode);
        ini.SetValue(XCommanderSection, "TouchModeEnabled", WriteBool(settings.TouchModeEnabled));
        ini.SetValue(XCommanderSection, "TouchModeItemHeight", settings.TouchModeItemHeight.ToString(CultureInfo.InvariantCulture));
        ini.SetValue(XCommanderSection, "TouchModeSwipeThreshold", settings.TouchModeSwipeThreshold.ToString(CultureInfo.InvariantCulture));
        ini.SetValue(XCommanderSection, "TouchModeLongPressDuration", settings.TouchModeLongPressDuration.ToString());
        ini.SetValue(XCommanderSection, "TouchModePadding", settings.TouchModePadding.ToString(CultureInfo.InvariantCulture));
        ini.SetValue(XCommanderSection, "TouchModeEnableSwipeNavigation", WriteBool(settings.TouchModeEnableSwipeNavigation));
        ini.SetValue(XCommanderSection, "TouchModeEnablePinchZoom", WriteBool(settings.TouchModeEnablePinchZoom));
        ini.SetValue(XCommanderSection, "AccessibilityScreenReaderEnabled", WriteBool(settings.AccessibilityScreenReaderEnabled));
        ini.SetValue(XCommanderSection, "AccessibilityKeyboardIndicatorsEnabled", WriteBool(settings.AccessibilityKeyboardIndicatorsEnabled));
        ini.SetValue(XCommanderSection, "AccessibilityHighContrastEnabled", WriteBool(settings.AccessibilityHighContrastEnabled));
        ini.SetValue(XCommanderSection, "AccessibilityReducedMotion", WriteBool(settings.AccessibilityReducedMotion));
        ini.SetValue(XCommanderSection, "AccessibilityScaleFactor", settings.AccessibilityScaleFactor.ToString(CultureInfo.InvariantCulture));
        ini.SetValue(XCommanderSection, "AccessibilityMinimumFontSize", settings.AccessibilityMinimumFontSize.ToString(CultureInfo.InvariantCulture));
        ini.SetValue(XCommanderSection, "AccessibilityVerboseAnnouncements", WriteBool(settings.AccessibilityVerboseAnnouncements));
        ini.SetValue(XCommanderSection, "AccessibilityHoverAnnouncementDelay", settings.AccessibilityHoverAnnouncementDelay.ToString());
        ini.SetValue(XCommanderSection, "AccessibilityHighContrastIcons", WriteBool(settings.AccessibilityHighContrastIcons));
        ini.SetValue(XCommanderSection, "AccessibilityFocusOutlineWidth", settings.AccessibilityFocusOutlineWidth.ToString(CultureInfo.InvariantCulture));

        ini.SetValue(XCommanderSection, "LastLeftPath", settings.LastLeftPath);
        ini.SetValue(XCommanderSection, "LastRightPath", settings.LastRightPath);
        ini.SetValue(LayoutSection, "DockLayout", settings.DockLayout ?? string.Empty);
        ini.SetValue(XCommanderSection, "SaveSessionOnExit", WriteBool(settings.SaveSessionOnExit));
        ini.SetValue(XCommanderSection, "RestoreSessionOnStartup", WriteBool(settings.RestoreSessionOnStartup));
        ini.SetValue(XCommanderSection, "RecentPathsLimit", settings.RecentPathsLimit.ToString());
        ini.SetValue(XCommanderSection, "CommandHistoryLimit", settings.CommandHistoryLimit.ToString());
    }

    private static bool TryParseBool(string value, out bool result)
    {
        if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }
        if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    private static bool TryReadBool(Dictionary<string, string> section, string key, out bool result)
    {
        if (section.TryGetValue(key, out var value) && TryParseBool(value, out var parsed))
        {
            result = parsed;
            return true;
        }
        result = false;
        return false;
    }

    private static bool TryReadInt(Dictionary<string, string> section, string key, out int result)
    {
        if (section.TryGetValue(key, out var value) && int.TryParse(value, out var parsed))
        {
            result = parsed;
            return true;
        }
        result = 0;
        return false;
    }

    private static bool TryReadDouble(Dictionary<string, string> section, string key, out double result)
    {
        if (section.TryGetValue(key, out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }
        result = 0;
        return false;
    }

    private static bool ReadBoolFromKeys(bool defaultValue,
        params (Dictionary<string, string> Section, string Key)[] candidates)
    {
        foreach (var (section, key) in candidates)
        {
            if (TryReadBool(section, key, out var value))
                return value;
        }
        return defaultValue;
    }

    private static int ReadIntFromKeys(int defaultValue,
        params (Dictionary<string, string> Section, string Key)[] candidates)
    {
        foreach (var (section, key) in candidates)
        {
            if (TryReadInt(section, key, out var value))
                return value;
        }
        return defaultValue;
    }

    private static double ReadDoubleFromKeys(double defaultValue,
        params (Dictionary<string, string> Section, string Key)[] candidates)
    {
        foreach (var (section, key) in candidates)
        {
            if (TryReadDouble(section, key, out var value))
                return value;
        }
        return defaultValue;
    }

    private static string ReadStringFromKeys(string defaultValue,
        params (Dictionary<string, string> Section, string Key)[] candidates)
    {
        foreach (var (section, key) in candidates)
        {
            if (section.TryGetValue(key, out var value))
                return value;
        }
        return defaultValue;
    }

    private static string WriteBool(bool value) => value ? "1" : "0";

    private static List<string> ReadListFromKeyPrefix(Dictionary<string, string> section, string prefix)
    {
        var items = new List<(int Index, string Value)>();
        foreach (var kvp in section)
        {
            if (!kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var indexText = kvp.Key[prefix.Length..];
            if (!int.TryParse(indexText, out var index))
                continue;

            if (string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            items.Add((index, kvp.Value));
        }

        items.Sort((left, right) => left.Index.CompareTo(right.Index));

        var results = new List<string>(items.Count);
        foreach (var item in items)
            results.Add(item.Value);

        return results;
    }

    private static void WriteListWithPrefix(IniFile ini, string sectionName, string prefix, IReadOnlyList<string> items)
    {
        var section = ini.GetSection(sectionName, true);
        var keysToRemove = new List<string>();

        foreach (var key in section.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                keysToRemove.Add(key);
        }

        foreach (var key in keysToRemove)
            section.Remove(key);

        for (var i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i]))
                ini.SetValue(sectionName, $"{prefix}{i}", items[i]);
        }
    }
}
