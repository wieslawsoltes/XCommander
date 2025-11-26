using XCommander.ViewModels;

namespace XCommander.Plugins;

/// <summary>
/// Default implementation of IPluginContext.
/// </summary>
public class DefaultPluginContext : IPluginContext
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly PluginManager _pluginManager;
    private readonly Dictionary<string, object> _config = new();
    private readonly List<PluginMenuItem> _menuItems = new();
    private readonly List<PluginKeyboardShortcut> _shortcuts = new();
    
    private Func<string, string, Task>? _showMessageFunc;
    private Func<string, string, Task<bool>>? _showConfirmationFunc;
    private Func<string, string, string, Task<string?>>? _showInputFunc;

    public DefaultPluginContext(MainWindowViewModel mainViewModel, PluginManager pluginManager)
    {
        _mainViewModel = mainViewModel;
        _pluginManager = pluginManager;
    }

    public string LeftPanelPath => _mainViewModel.LeftPanel.CurrentPath;

    public string RightPanelPath => _mainViewModel.RightPanel.CurrentPath;

    public string ActivePanelPath => _mainViewModel.ActivePanel.CurrentPath;

    public IReadOnlyList<string> SelectedPaths => _mainViewModel.ActivePanel.GetSelectedPaths().ToList().AsReadOnly();

    public void NavigateTo(string path)
    {
        _mainViewModel.ActivePanel.NavigateTo(path);
    }

    public void NavigateLeftTo(string path)
    {
        _mainViewModel.LeftPanel.NavigateTo(path);
    }

    public void NavigateRightTo(string path)
    {
        _mainViewModel.RightPanel.NavigateTo(path);
    }

    public void RefreshActivePanel()
    {
        _mainViewModel.ActivePanel.Refresh();
    }

    public void RefreshAllPanels()
    {
        _mainViewModel.LeftPanel.Refresh();
        _mainViewModel.RightPanel.Refresh();
    }

    public Task ShowMessageAsync(string title, string message)
    {
        if (_showMessageFunc != null)
        {
            return _showMessageFunc(title, message);
        }
        // Fallback - log the message
        System.Diagnostics.Debug.WriteLine($"[{title}] {message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (_showConfirmationFunc != null)
        {
            return _showConfirmationFunc(title, message);
        }
        return Task.FromResult(false);
    }

    public Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "")
    {
        if (_showInputFunc != null)
        {
            return _showInputFunc(title, prompt, defaultValue);
        }
        return Task.FromResult<string?>(null);
    }

    public void Log(PluginLogLevel level, string message)
    {
        var prefix = level switch
        {
            PluginLogLevel.Debug => "[DEBUG]",
            PluginLogLevel.Info => "[INFO]",
            PluginLogLevel.Warning => "[WARNING]",
            PluginLogLevel.Error => "[ERROR]",
            _ => "[LOG]"
        };
        System.Diagnostics.Debug.WriteLine($"{prefix} {message}");
    }

    public T? GetConfig<T>(string key)
    {
        if (_config.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void SetConfig<T>(string key, T value)
    {
        if (value == null)
        {
            _config.Remove(key);
        }
        else
        {
            _config[key] = value;
        }
    }

    public void RegisterMenuItem(PluginMenuItem menuItem)
    {
        _menuItems.Add(menuItem);
        // Notify listeners about new menu item
        MenuItemRegistered?.Invoke(this, menuItem);
    }

    public void RegisterKeyboardShortcut(PluginKeyboardShortcut shortcut)
    {
        _shortcuts.Add(shortcut);
        // Notify listeners about new shortcut
        KeyboardShortcutRegistered?.Invoke(this, shortcut);
    }

    public string GetPluginDataDirectory(string pluginId)
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pluginDataDir = Path.Combine(baseDir, "XCommander", "Plugins", pluginId);
        
        if (!Directory.Exists(pluginDataDir))
        {
            Directory.CreateDirectory(pluginDataDir);
        }
        
        return pluginDataDir;
    }

    /// <summary>
    /// Get all registered menu items.
    /// </summary>
    public IReadOnlyList<PluginMenuItem> MenuItems => _menuItems.AsReadOnly();

    /// <summary>
    /// Get all registered keyboard shortcuts.
    /// </summary>
    public IReadOnlyList<PluginKeyboardShortcut> KeyboardShortcuts => _shortcuts.AsReadOnly();

    /// <summary>
    /// Event raised when a menu item is registered.
    /// </summary>
    public event EventHandler<PluginMenuItem>? MenuItemRegistered;

    /// <summary>
    /// Event raised when a keyboard shortcut is registered.
    /// </summary>
    public event EventHandler<PluginKeyboardShortcut>? KeyboardShortcutRegistered;

    /// <summary>
    /// Set the function to show messages.
    /// </summary>
    public void SetShowMessageHandler(Func<string, string, Task> handler)
    {
        _showMessageFunc = handler;
    }

    /// <summary>
    /// Set the function to show confirmations.
    /// </summary>
    public void SetShowConfirmationHandler(Func<string, string, Task<bool>> handler)
    {
        _showConfirmationFunc = handler;
    }

    /// <summary>
    /// Set the function to show input dialogs.
    /// </summary>
    public void SetShowInputHandler(Func<string, string, string, Task<string?>> handler)
    {
        _showInputFunc = handler;
    }
}
