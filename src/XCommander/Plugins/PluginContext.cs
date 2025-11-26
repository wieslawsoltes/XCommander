namespace XCommander.Plugins;

/// <summary>
/// Context provided to plugins for interacting with XCommander.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Get the path of the left panel.
    /// </summary>
    string LeftPanelPath { get; }
    
    /// <summary>
    /// Get the path of the right panel.
    /// </summary>
    string RightPanelPath { get; }
    
    /// <summary>
    /// Get the path of the active panel.
    /// </summary>
    string ActivePanelPath { get; }
    
    /// <summary>
    /// Get selected items in the active panel.
    /// </summary>
    IReadOnlyList<string> SelectedPaths { get; }
    
    /// <summary>
    /// Navigate the active panel to a path.
    /// </summary>
    void NavigateTo(string path);
    
    /// <summary>
    /// Navigate the left panel to a path.
    /// </summary>
    void NavigateLeftTo(string path);
    
    /// <summary>
    /// Navigate the right panel to a path.
    /// </summary>
    void NavigateRightTo(string path);
    
    /// <summary>
    /// Refresh the active panel.
    /// </summary>
    void RefreshActivePanel();
    
    /// <summary>
    /// Refresh both panels.
    /// </summary>
    void RefreshAllPanels();
    
    /// <summary>
    /// Show a message to the user.
    /// </summary>
    Task ShowMessageAsync(string title, string message);
    
    /// <summary>
    /// Show a confirmation dialog.
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message);
    
    /// <summary>
    /// Show an input dialog.
    /// </summary>
    Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "");
    
    /// <summary>
    /// Log a message.
    /// </summary>
    void Log(PluginLogLevel level, string message);
    
    /// <summary>
    /// Get a configuration value.
    /// </summary>
    T? GetConfig<T>(string key);
    
    /// <summary>
    /// Set a configuration value.
    /// </summary>
    void SetConfig<T>(string key, T value);
    
    /// <summary>
    /// Register a menu item.
    /// </summary>
    void RegisterMenuItem(PluginMenuItem menuItem);
    
    /// <summary>
    /// Register a keyboard shortcut.
    /// </summary>
    void RegisterKeyboardShortcut(PluginKeyboardShortcut shortcut);
    
    /// <summary>
    /// Get the plugin's data directory for storing configuration/cache.
    /// </summary>
    string GetPluginDataDirectory(string pluginId);
}

/// <summary>
/// Log levels for plugin logging.
/// </summary>
public enum PluginLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents a file item returned by a file system plugin.
/// </summary>
public class PluginFileItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime? LastModified { get; init; }
    public DateTime? Created { get; init; }
    public string? Extension { get; init; }
    public string? Attributes { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
}

/// <summary>
/// Represents a column provided by a column plugin.
/// </summary>
public class PluginColumn
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int DefaultWidth { get; init; } = 100;
    public PluginColumnAlignment Alignment { get; init; } = PluginColumnAlignment.Left;
    public bool Sortable { get; init; } = true;
}

/// <summary>
/// Column alignment options.
/// </summary>
public enum PluginColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Represents an entry in an archive.
/// </summary>
public class PluginArchiveEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public DateTime? LastModified { get; init; }
    public string? Method { get; init; }
    public uint? Crc32 { get; init; }
}

/// <summary>
/// Represents a command provided by a command plugin.
/// </summary>
public class PluginCommand
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? IconPath { get; init; }
    public string? Category { get; init; }
    public string? KeyboardShortcut { get; init; }
}

/// <summary>
/// Represents a menu item added by a plugin.
/// </summary>
public class PluginMenuItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public string? ParentMenuId { get; init; }
    public string? IconPath { get; init; }
    public int Order { get; init; }
    public Func<IPluginContext, Task>? Action { get; init; }
    public Func<IPluginContext, bool>? IsEnabled { get; init; }
    public Func<IPluginContext, bool>? IsVisible { get; init; }
    public List<PluginMenuItem>? SubItems { get; init; }
}

/// <summary>
/// Represents a keyboard shortcut registered by a plugin.
/// </summary>
public class PluginKeyboardShortcut
{
    public required string Key { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public required string CommandId { get; init; }
}
