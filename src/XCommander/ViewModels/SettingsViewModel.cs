using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "settings.json");

    // Appearance
    [ObservableProperty]
    private bool _useDarkTheme;

    [ObservableProperty]
    private int _fontSize = 13;

    [ObservableProperty]
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private bool _showGridLines;

    [ObservableProperty]
    private bool _showStatusBar = true;

    [ObservableProperty]
    private bool _showToolbar = true;

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

    // Editor
    [ObservableProperty]
    private string _externalEditor = string.Empty;

    [ObservableProperty]
    private string _externalViewer = string.Empty;

    [ObservableProperty]
    private string _terminalCommand = string.Empty;

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

    public event EventHandler? SettingsSaved;
    public event EventHandler? SettingsLoaded;

    [RelayCommand]
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new AppSettings
            {
                // Appearance
                UseDarkTheme = UseDarkTheme,
                FontSize = FontSize,
                FontFamily = FontFamily,
                ShowGridLines = ShowGridLines,
                ShowStatusBar = ShowStatusBar,
                ShowToolbar = ShowToolbar,

                // File Display
                ShowHiddenFiles = ShowHiddenFiles,
                ShowSystemFiles = ShowSystemFiles,
                ShowFileExtensions = ShowFileExtensions,
                ShowFileSizes = ShowFileSizes,
                ShowFileDates = ShowFileDates,
                DateFormat = DateFormat,

                // Behavior
                ConfirmDelete = ConfirmDelete,
                ConfirmOverwrite = ConfirmOverwrite,
                UseRecycleBin = UseRecycleBin,
                SingleClickOpen = SingleClickOpen,
                RememberLastPath = RememberLastPath,
                DefaultLeftPath = DefaultLeftPath,
                DefaultRightPath = DefaultRightPath,

                // Editor
                ExternalEditor = ExternalEditor,
                ExternalViewer = ExternalViewer,
                TerminalCommand = TerminalCommand,

                // Tabs
                OpenNewTabOnDriveChange = OpenNewTabOnDriveChange,
                CloseTabOnMiddleClick = CloseTabOnMiddleClick,
                ShowTabCloseButton = ShowTabCloseButton,

                // Quick View
                QuickViewMaxFileSizeKb = QuickViewMaxFileSizeKb,
                QuickViewAutoUpdate = QuickViewAutoUpdate,

                // File Operations
                CopyBufferSizeKb = CopyBufferSizeKb,
                VerifyAfterCopy = VerifyAfterCopy,
                PreserveTimestamps = PreserveTimestamps,

                // Search
                SearchMaxResults = SearchMaxResults,
                SearchIncludeHidden = SearchIncludeHidden
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);

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
            if (!File.Exists(SettingsPath))
            {
                SetDefaults();
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null)
            {
                // Appearance
                UseDarkTheme = settings.UseDarkTheme;
                FontSize = settings.FontSize;
                FontFamily = settings.FontFamily;
                ShowGridLines = settings.ShowGridLines;
                ShowStatusBar = settings.ShowStatusBar;
                ShowToolbar = settings.ShowToolbar;

                // File Display
                ShowHiddenFiles = settings.ShowHiddenFiles;
                ShowSystemFiles = settings.ShowSystemFiles;
                ShowFileExtensions = settings.ShowFileExtensions;
                ShowFileSizes = settings.ShowFileSizes;
                ShowFileDates = settings.ShowFileDates;
                DateFormat = settings.DateFormat;

                // Behavior
                ConfirmDelete = settings.ConfirmDelete;
                ConfirmOverwrite = settings.ConfirmOverwrite;
                UseRecycleBin = settings.UseRecycleBin;
                SingleClickOpen = settings.SingleClickOpen;
                RememberLastPath = settings.RememberLastPath;
                DefaultLeftPath = settings.DefaultLeftPath;
                DefaultRightPath = settings.DefaultRightPath;

                // Editor
                ExternalEditor = settings.ExternalEditor;
                ExternalViewer = settings.ExternalViewer;
                TerminalCommand = settings.TerminalCommand;

                // Tabs
                OpenNewTabOnDriveChange = settings.OpenNewTabOnDriveChange;
                CloseTabOnMiddleClick = settings.CloseTabOnMiddleClick;
                ShowTabCloseButton = settings.ShowTabCloseButton;

                // Quick View
                QuickViewMaxFileSizeKb = settings.QuickViewMaxFileSizeKb;
                QuickViewAutoUpdate = settings.QuickViewAutoUpdate;

                // File Operations
                CopyBufferSizeKb = settings.CopyBufferSizeKb;
                VerifyAfterCopy = settings.VerifyAfterCopy;
                PreserveTimestamps = settings.PreserveTimestamps;

                // Search
                SearchMaxResults = settings.SearchMaxResults;
                SearchIncludeHidden = settings.SearchIncludeHidden;
            }

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
        // Appearance
        UseDarkTheme = false;
        FontSize = 13;
        FontFamily = "Segoe UI";
        ShowGridLines = false;
        ShowStatusBar = true;
        ShowToolbar = true;

        // File Display
        ShowHiddenFiles = false;
        ShowSystemFiles = false;
        ShowFileExtensions = true;
        ShowFileSizes = true;
        ShowFileDates = true;
        DateFormat = "yyyy-MM-dd HH:mm";

        // Behavior
        ConfirmDelete = true;
        ConfirmOverwrite = true;
        UseRecycleBin = true;
        SingleClickOpen = false;
        RememberLastPath = true;
        DefaultLeftPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DefaultRightPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Editor
        ExternalEditor = OperatingSystem.IsWindows() ? "notepad.exe" : 
                         OperatingSystem.IsMacOS() ? "open -e" : "xdg-open";
        ExternalViewer = string.Empty;
        TerminalCommand = OperatingSystem.IsWindows() ? "cmd.exe" :
                          OperatingSystem.IsMacOS() ? "open -a Terminal" : "x-terminal-emulator";

        // Tabs
        OpenNewTabOnDriveChange = false;
        CloseTabOnMiddleClick = true;
        ShowTabCloseButton = true;

        // Quick View
        QuickViewMaxFileSizeKb = 1024;
        QuickViewAutoUpdate = true;

        // File Operations
        CopyBufferSizeKb = 64;
        VerifyAfterCopy = false;
        PreserveTimestamps = true;

        // Search
        SearchMaxResults = 10000;
        SearchIncludeHidden = false;
    }
}

public class AppSettings
{
    // Appearance
    public bool UseDarkTheme { get; set; }
    public int FontSize { get; set; } = 13;
    public string FontFamily { get; set; } = "Segoe UI";
    public bool ShowGridLines { get; set; }
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;

    // File Display
    public bool ShowHiddenFiles { get; set; }
    public bool ShowSystemFiles { get; set; }
    public bool ShowFileExtensions { get; set; } = true;
    public bool ShowFileSizes { get; set; } = true;
    public bool ShowFileDates { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm";

    // Behavior
    public bool ConfirmDelete { get; set; } = true;
    public bool ConfirmOverwrite { get; set; } = true;
    public bool UseRecycleBin { get; set; } = true;
    public bool SingleClickOpen { get; set; }
    public bool RememberLastPath { get; set; } = true;
    public string DefaultLeftPath { get; set; } = string.Empty;
    public string DefaultRightPath { get; set; } = string.Empty;

    // Editor
    public string ExternalEditor { get; set; } = string.Empty;
    public string ExternalViewer { get; set; } = string.Empty;
    public string TerminalCommand { get; set; } = string.Empty;

    // Tabs
    public bool OpenNewTabOnDriveChange { get; set; }
    public bool CloseTabOnMiddleClick { get; set; } = true;
    public bool ShowTabCloseButton { get; set; } = true;

    // Quick View
    public int QuickViewMaxFileSizeKb { get; set; } = 1024;
    public bool QuickViewAutoUpdate { get; set; } = true;

    // File Operations
    public int CopyBufferSizeKb { get; set; } = 64;
    public bool VerifyAfterCopy { get; set; }
    public bool PreserveTimestamps { get; set; } = true;

    // Search
    public int SearchMaxResults { get; set; } = 10000;
    public bool SearchIncludeHidden { get; set; }
}
