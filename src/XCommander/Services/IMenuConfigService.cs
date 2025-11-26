// IMenuConfigService.cs - TC-style customizable menu configuration
// Provides full menu customization like TC's Configuration > Options > Misc > Redefine hotkeys

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Represents a menu item in the customizable menu system
/// </summary>
public record MenuItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Icon { get; init; }
    public string? CommandName { get; init; }
    public string? CommandParameter { get; init; }
    public string? Shortcut { get; init; }
    public bool IsSeparator { get; init; }
    public bool IsSubmenu { get; init; }
    public string? SubmenuId { get; init; }
    public int Order { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsVisible { get; init; } = true;
    public string? Tooltip { get; init; }
    public ConfigMenuItemType Type { get; init; } = ConfigMenuItemType.Command;
    public List<MenuItem> Children { get; init; } = new();
}

/// <summary>
/// Types of menu items for configuration
/// </summary>
public enum ConfigMenuItemType
{
    Command,
    Separator,
    Submenu,
    InternalCommand,
    ExternalProgram,
    UserCommand,
    DirectoryHotlist,
    History,
    Plugin
}

/// <summary>
/// Represents a complete menu definition
/// </summary>
public record MenuDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MenuType Type { get; init; }
    public List<MenuItem> Items { get; init; } = new();
    public bool IsUserDefined { get; init; }
    public DateTime LastModified { get; init; } = DateTime.Now;
}

/// <summary>
/// Types of menus
/// </summary>
public enum MenuType
{
    MainMenu,
    ContextMenu,
    ButtonBar,
    StartMenu,
    DirectoryMenu,
    UserMenu,
    HistoryMenu,
    HotlistMenu,
    FtpMenu,
    PluginMenu
}

/// <summary>
/// Hotkey definition for commands
/// </summary>
public record HotkeyDefinition
{
    public string CommandName { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool WinKey { get; init; }
    public string? Parameter { get; init; }
    public bool IsUserDefined { get; init; }
    public string DisplayString => FormatKeyCombo();
    
    private string FormatKeyCombo()
    {
        var parts = new List<string>();
        if (WinKey) parts.Add("Win");
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}

/// <summary>
/// User-defined command
/// </summary>
public record UserCommand
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string CommandLine { get; init; } = string.Empty;
    public string? WorkingDirectory { get; init; }
    public string? Icon { get; init; }
    public bool RunMinimized { get; init; }
    public bool RunMaximized { get; init; }
    public bool CaptureOutput { get; init; }
    public bool WaitForCompletion { get; init; }
    public string? ShortcutKey { get; init; }
    public UserCommandParameters Parameters { get; init; } = new();
}

/// <summary>
/// Parameters that can be passed to user commands (TC-style placeholders)
/// </summary>
public record UserCommandParameters
{
    public bool UseSourcePath { get; init; }      // %P = source path
    public bool UseTargetPath { get; init; }      // %T = target path  
    public bool UseSourceFile { get; init; }      // %N = source filename
    public bool UseTargetFile { get; init; }      // %M = target filename
    public bool UseSourceFullPath { get; init; } // %F = source full path with filename
    public bool UseSelectedFiles { get; init; }   // %L = list of selected files
    public bool UseShortNames { get; init; }      // %S = use short (8.3) names
    public bool PromptForParams { get; init; }    // %? = prompt user for additional params
    public string? CustomPlaceholder { get; init; }
}

/// <summary>
/// Button bar definition (TC-style customizable toolbar)
/// </summary>
public record ButtonBarDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<ConfigButtonBarItem> Items { get; init; } = new();
    public bool ShowText { get; init; }
    public bool ShowIcons { get; init; } = true;
    public int IconSize { get; init; } = 24;
    public bool IsDefault { get; init; }
}

/// <summary>
/// Single button bar item for configuration
/// </summary>
public record ConfigButtonBarItem
{
    public string Id { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string? Text { get; init; }
    public string? Tooltip { get; init; }
    public string? Command { get; init; }
    public string? Parameter { get; init; }
    public bool IsSeparator { get; init; }
    public ConfigButtonBarItemType Type { get; init; } = ConfigButtonBarItemType.InternalCommand;
    public int Order { get; init; }
}

/// <summary>
/// Types of button bar items for configuration
/// </summary>
public enum ConfigButtonBarItemType
{
    InternalCommand,
    ExternalProgram,
    UserCommand,
    ChangeDirectory,
    Separator,
    Menu
}

/// <summary>
/// Service for managing TC-style customizable menus, hotkeys, and toolbars
/// </summary>
public interface IMenuConfigService
{
    // Menu management
    Task<IReadOnlyList<MenuDefinition>> GetAllMenusAsync(CancellationToken cancellationToken = default);
    Task<MenuDefinition?> GetMenuAsync(string menuId, CancellationToken cancellationToken = default);
    Task SaveMenuAsync(MenuDefinition menu, CancellationToken cancellationToken = default);
    Task DeleteMenuAsync(string menuId, CancellationToken cancellationToken = default);
    Task<MenuDefinition> CreateDefaultMenuAsync(MenuType type, CancellationToken cancellationToken = default);
    Task ResetMenuToDefaultAsync(string menuId, CancellationToken cancellationToken = default);
    
    // Menu item management  
    Task AddMenuItemAsync(string menuId, MenuItem item, int? position = null, CancellationToken cancellationToken = default);
    Task UpdateMenuItemAsync(string menuId, string itemId, MenuItem item, CancellationToken cancellationToken = default);
    Task RemoveMenuItemAsync(string menuId, string itemId, CancellationToken cancellationToken = default);
    Task MoveMenuItemAsync(string menuId, string itemId, int newPosition, CancellationToken cancellationToken = default);
    
    // Hotkey management
    Task<IReadOnlyList<HotkeyDefinition>> GetAllHotkeysAsync(CancellationToken cancellationToken = default);
    Task<HotkeyDefinition?> GetHotkeyForCommandAsync(string commandName, CancellationToken cancellationToken = default);
    Task<string?> GetCommandForHotkeyAsync(string hotkey, CancellationToken cancellationToken = default);
    Task SetHotkeyAsync(HotkeyDefinition hotkey, CancellationToken cancellationToken = default);
    Task RemoveHotkeyAsync(string commandName, CancellationToken cancellationToken = default);
    Task ResetHotkeysToDefaultAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HotkeyDefinition>> GetConflictingHotkeysAsync(HotkeyDefinition hotkey, CancellationToken cancellationToken = default);
    
    // User commands
    Task<IReadOnlyList<UserCommand>> GetAllUserCommandsAsync(CancellationToken cancellationToken = default);
    Task<UserCommand?> GetUserCommandAsync(string commandId, CancellationToken cancellationToken = default);
    Task SaveUserCommandAsync(UserCommand command, CancellationToken cancellationToken = default);
    Task DeleteUserCommandAsync(string commandId, CancellationToken cancellationToken = default);
    Task<string> ExpandUserCommandAsync(UserCommand command, string sourcePath, string targetPath, 
        IReadOnlyList<string> selectedFiles, CancellationToken cancellationToken = default);
    
    // Button bar management
    Task<IReadOnlyList<ButtonBarDefinition>> GetAllButtonBarsAsync(CancellationToken cancellationToken = default);
    Task<ButtonBarDefinition?> GetButtonBarAsync(string barId, CancellationToken cancellationToken = default);
    Task SaveButtonBarAsync(ButtonBarDefinition bar, CancellationToken cancellationToken = default);
    Task DeleteButtonBarAsync(string barId, CancellationToken cancellationToken = default);
    Task<ButtonBarDefinition> CreateDefaultButtonBarAsync(CancellationToken cancellationToken = default);
    
    // Import/Export
    Task ExportConfigurationAsync(string filePath, CancellationToken cancellationToken = default);
    Task ImportConfigurationAsync(string filePath, bool merge = false, CancellationToken cancellationToken = default);
    Task ImportTcMenuAsync(string filePath, CancellationToken cancellationToken = default); // Import TC menu file
    Task ImportTcUserCommandsAsync(string filePath, CancellationToken cancellationToken = default); // Import usercmd.ini
    
    // Events
    event EventHandler<MenuChangedEventArgs>? MenuChanged;
    event EventHandler<HotkeyChangedEventArgs>? HotkeyChanged;
    event EventHandler<ButtonBarChangedEventArgs>? ButtonBarChanged;
}

public class MenuChangedEventArgs : EventArgs
{
    public string MenuId { get; }
    public MenuChangeType ChangeType { get; }
    public MenuItem? Item { get; }
    
    public MenuChangedEventArgs(string menuId, MenuChangeType changeType, MenuItem? item = null)
    {
        MenuId = menuId;
        ChangeType = changeType;
        Item = item;
    }
}

public enum MenuChangeType
{
    Created,
    Updated,
    Deleted,
    ItemAdded,
    ItemRemoved,
    ItemMoved,
    Reset
}

public class HotkeyChangedEventArgs : EventArgs
{
    public string CommandName { get; }
    public HotkeyDefinition? OldHotkey { get; }
    public HotkeyDefinition? NewHotkey { get; }
    
    public HotkeyChangedEventArgs(string commandName, HotkeyDefinition? oldHotkey, HotkeyDefinition? newHotkey)
    {
        CommandName = commandName;
        OldHotkey = oldHotkey;
        NewHotkey = newHotkey;
    }
}

public class ButtonBarChangedEventArgs : EventArgs
{
    public string BarId { get; }
    public ButtonBarChangeType ChangeType { get; }
    
    public ButtonBarChangedEventArgs(string barId, ButtonBarChangeType changeType)
    {
        BarId = barId;
        ChangeType = changeType;
    }
}

public enum ButtonBarChangeType
{
    Created,
    Updated,
    Deleted,
    Reset
}
