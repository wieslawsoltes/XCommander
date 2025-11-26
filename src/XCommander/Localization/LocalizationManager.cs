using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace XCommander.Localization;

/// <summary>
/// Provides localized strings for the application.
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    private static LocalizationManager? _instance;
    private Dictionary<string, string> _strings = new();
    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
    private string _currentLanguage = "en";

    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Available languages.
    /// </summary>
    public static readonly Dictionary<string, string> AvailableLanguages = new()
    {
        { "en", "English" },
        { "de", "Deutsch" },
        { "fr", "Français" },
        { "es", "Español" },
        { "pl", "Polski" },
        { "ru", "Русский" },
        { "zh", "中文" },
        { "ja", "日本語" }
    };

    /// <summary>
    /// Current language code.
    /// </summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                LoadLanguage(value);
                OnPropertyChanged();
                // Notify that all strings have changed via indexer
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }
    }

    /// <summary>
    /// Current culture.
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (!Equals(_currentCulture, value))
            {
                _currentCulture = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Get a localized string by key.
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }
            
            // Return the key itself if not found (useful for debugging)
            System.Diagnostics.Debug.WriteLine($"[Localization] Missing key: {key}");
            return key;
        }
    }

    /// <summary>
    /// Get a formatted localized string.
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var format = this[key];
        try
        {
            return string.Format(CurrentCulture, format, args);
        }
        catch
        {
            return format;
        }
    }

    private LocalizationManager()
    {
        // Try to load the system language, fall back to English
        var systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (AvailableLanguages.ContainsKey(systemLang))
        {
            LoadLanguage(systemLang);
        }
        else
        {
            LoadLanguage("en");
        }
    }

    /// <summary>
    /// Load a language by code.
    /// </summary>
    public void LoadLanguage(string languageCode)
    {
        _strings.Clear();
        
        // First load English as fallback
        LoadLanguageFile("en");
        
        // Then overlay the requested language
        if (languageCode != "en")
        {
            LoadLanguageFile(languageCode);
        }
        
        _currentLanguage = languageCode;
        CurrentCulture = new CultureInfo(languageCode);
        
        // Notify all bindings that strings changed
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void LoadLanguageFile(string languageCode)
    {
        try
        {
            // Try to load from embedded resource
            var assembly = typeof(LocalizationManager).Assembly;
            var resourceName = $"XCommander.Localization.Strings.{languageCode}.json";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (strings != null)
                {
                    foreach (var kvp in strings)
                    {
                        _strings[kvp.Key] = kvp.Value;
                    }
                }
                return;
            }
            
            // Try to load from file system
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(appDir, "Localization", $"{languageCode}.json");
            
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (strings != null)
                {
                    foreach (var kvp in strings)
                    {
                        _strings[kvp.Key] = kvp.Value;
                    }
                }
                return;
            }
            
            // If no file found, load default English strings
            if (languageCode == "en")
            {
                LoadDefaultStrings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Localization] Error loading {languageCode}: {ex.Message}");
            
            if (languageCode == "en")
            {
                LoadDefaultStrings();
            }
        }
    }

    private void LoadDefaultStrings()
    {
        // Default English strings
        _strings = new Dictionary<string, string>
        {
            // Menu - File
            { "Menu.File", "File" },
            { "Menu.File.New", "New" },
            { "Menu.File.NewFolder", "New Folder" },
            { "Menu.File.NewFile", "New File" },
            { "Menu.File.Open", "Open" },
            { "Menu.File.View", "View" },
            { "Menu.File.Edit", "Edit" },
            { "Menu.File.Copy", "Copy" },
            { "Menu.File.Move", "Move" },
            { "Menu.File.Delete", "Delete" },
            { "Menu.File.Rename", "Rename" },
            { "Menu.File.Properties", "Properties" },
            { "Menu.File.Exit", "Exit" },
            
            // Menu - Edit
            { "Menu.Edit", "Edit" },
            { "Menu.Edit.SelectAll", "Select All" },
            { "Menu.Edit.InvertSelection", "Invert Selection" },
            { "Menu.Edit.DeselectAll", "Deselect All" },
            
            // Menu - View
            { "Menu.View", "View" },
            { "Menu.View.Refresh", "Refresh" },
            { "Menu.View.ShowHiddenFiles", "Show Hidden Files" },
            { "Menu.View.DetailView", "Detail View" },
            { "Menu.View.ThumbnailView", "Thumbnail View" },
            { "Menu.View.QuickView", "Quick View" },
            { "Menu.View.CustomColumns", "Custom Columns..." },
            
            // Menu - Tools
            { "Menu.Tools", "Tools" },
            { "Menu.Tools.Search", "Search" },
            { "Menu.Tools.MultiRename", "Multi-Rename Tool" },
            { "Menu.Tools.SyncDirectories", "Sync Directories" },
            { "Menu.Tools.CompareDirectories", "Compare Directories" },
            { "Menu.Tools.CompareFiles", "Compare Files" },
            { "Menu.Tools.Checksum", "Calculate Checksum" },
            { "Menu.Tools.SplitFile", "Split File" },
            { "Menu.Tools.CombineFiles", "Combine Files" },
            
            // Menu - Network
            { "Menu.Network", "Network" },
            { "Menu.Network.FTP", "FTP Connection" },
            { "Menu.Network.SFTP", "SFTP Connection" },
            
            // Menu - Archive
            { "Menu.Archive", "Archive" },
            { "Menu.Archive.Open", "Open Archive" },
            { "Menu.Archive.Create", "Create Archive" },
            { "Menu.Archive.Extract", "Extract" },
            
            // Menu - Configuration
            { "Menu.Configuration", "Configuration" },
            { "Menu.Configuration.Settings", "Settings" },
            { "Menu.Configuration.Plugins", "Plugins" },
            
            // Menu - Help
            { "Menu.Help", "Help" },
            { "Menu.Help.About", "About" },
            { "Menu.Help.Keyboard", "Keyboard Shortcuts" },
            
            // Buttons
            { "Button.OK", "OK" },
            { "Button.Cancel", "Cancel" },
            { "Button.Apply", "Apply" },
            { "Button.Close", "Close" },
            { "Button.Yes", "Yes" },
            { "Button.No", "No" },
            { "Button.Browse", "Browse..." },
            { "Button.Add", "Add" },
            { "Button.Remove", "Remove" },
            { "Button.Refresh", "Refresh" },
            
            // File Panel
            { "FilePanel.Name", "Name" },
            { "FilePanel.Extension", "Ext" },
            { "FilePanel.Size", "Size" },
            { "FilePanel.DateModified", "Date Modified" },
            { "FilePanel.DateCreated", "Date Created" },
            { "FilePanel.Attributes", "Attributes" },
            { "FilePanel.SelectedCount", "{0} selected" },
            { "FilePanel.TotalItems", "{0} items" },
            { "FilePanel.FreeSpace", "{0} free" },
            
            // Dialogs
            { "Dialog.Confirm", "Confirm" },
            { "Dialog.Error", "Error" },
            { "Dialog.Warning", "Warning" },
            { "Dialog.Information", "Information" },
            
            // Delete Confirmation
            { "Delete.Confirm.Single", "Delete '{0}'?" },
            { "Delete.Confirm.Multiple", "Delete {0} items?" },
            
            // Search Dialog
            { "Search.Title", "Search" },
            { "Search.SearchIn", "Search in:" },
            { "Search.SearchFor", "Search for:" },
            { "Search.IncludeSubfolders", "Include subfolders" },
            { "Search.SearchButton", "Search" },
            { "Search.Results", "Results" },
            { "Search.NoResults", "No results found" },
            
            // Settings Dialog
            { "Settings.Title", "Settings" },
            { "Settings.General", "General" },
            { "Settings.Appearance", "Appearance" },
            { "Settings.Language", "Language" },
            { "Settings.Theme", "Theme" },
            { "Settings.KeyboardShortcuts", "Keyboard Shortcuts" },
            
            // Status Messages
            { "Status.Ready", "Ready" },
            { "Status.Copying", "Copying: {0}" },
            { "Status.Moving", "Moving: {0}" },
            { "Status.Deleting", "Deleting: {0}" },
            { "Status.Searching", "Searching..." },
            { "Status.Loading", "Loading..." },
            { "Status.Complete", "Complete" },
            { "Status.Error", "Error: {0}" },
            
            // FTP/SFTP
            { "FTP.Title", "FTP Connection" },
            { "SFTP.Title", "SFTP Connection" },
            { "Connection.Host", "Host:" },
            { "Connection.Port", "Port:" },
            { "Connection.Username", "Username:" },
            { "Connection.Password", "Password:" },
            { "Connection.Connect", "Connect" },
            { "Connection.Disconnect", "Disconnect" },
            { "Connection.Connected", "Connected" },
            { "Connection.Disconnected", "Disconnected" },
            
            // Archive
            { "Archive.CreateTitle", "Create Archive" },
            { "Archive.ExtractTitle", "Extract Archive" },
            { "Archive.Format", "Format:" },
            { "Archive.Destination", "Destination:" },
            { "Archive.CompressionLevel", "Compression Level:" },
            
            // Plugins
            { "Plugins.Title", "Plugin Manager" },
            { "Plugins.Enabled", "Enabled" },
            { "Plugins.Disabled", "Disabled" },
            { "Plugins.Enable", "Enable" },
            { "Plugins.Disable", "Disable" },
            
            // About
            { "About.Title", "About XCommander" },
            { "About.Version", "Version {0}" },
            { "About.Description", "A Total Commander-like file manager built with Avalonia UI" },
        };
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
