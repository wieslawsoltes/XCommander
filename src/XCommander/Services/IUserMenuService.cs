using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Interface for custom user menu management.
/// </summary>
public interface IUserMenuService
{
    /// <summary>
    /// Gets all user menus.
    /// </summary>
    Task<IReadOnlyList<UserMenu>> GetMenusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the main user menu.
    /// </summary>
    Task<UserMenu?> GetMainMenuAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new user menu.
    /// </summary>
    Task<UserMenu> CreateMenuAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a user menu.
    /// </summary>
    Task UpdateMenuAsync(UserMenu menu, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a user menu.
    /// </summary>
    Task DeleteMenuAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a menu item.
    /// </summary>
    Task<UserMenuItem> AddMenuItemAsync(string menuId, UserMenuItem item, string? parentId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a menu item.
    /// </summary>
    Task UpdateMenuItemAsync(string menuId, UserMenuItem item, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a menu item.
    /// </summary>
    Task RemoveMenuItemAsync(string menuId, string itemId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a menu item.
    /// </summary>
    Task MoveMenuItemAsync(string menuId, string itemId, string? newParentId, int position,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a menu item command.
    /// </summary>
    Task ExecuteMenuItemAsync(UserMenuItem item, MenuExecutionContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports a menu from file.
    /// </summary>
    Task<UserMenu> ImportAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a menu to file.
    /// </summary>
    Task ExportAsync(string menuId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets available parameter placeholders.
    /// </summary>
    IReadOnlyList<ParameterPlaceholder> GetParameterPlaceholders();
}

/// <summary>
/// A custom user menu.
/// </summary>
public class UserMenu
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsMain { get; set; }
    public string? KeyboardShortcut { get; set; }
    public List<UserMenuItem> Items { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// An item in a user menu.
/// </summary>
public class UserMenuItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public MenuItemType Type { get; set; } = MenuItemType.Command;
    public UserMenuCommand? Command { get; set; }
    public List<UserMenuItem>? SubItems { get; set; }
    public string? KeyboardShortcut { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public string? Condition { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// Type of menu item.
/// </summary>
public enum MenuItemType
{
    Command,        // Executes a command
    Separator,      // Visual separator
    SubMenu,        // Sub-menu
    Internal        // Internal action
}

/// <summary>
/// Command associated with a menu item.
/// </summary>
public class UserMenuCommand
{
    public MenuCommandType Type { get; set; }
    public string? CommandLine { get; set; }
    public string? Parameters { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? InternalAction { get; set; }
    public bool MinimizeOnExecute { get; set; }
    public bool WaitForFinish { get; set; }
    public bool RunAsAdmin { get; set; }
    public bool CloseOnFinish { get; set; }
}

/// <summary>
/// Type of menu command.
/// </summary>
public enum MenuCommandType
{
    External,           // External program
    InternalCommand,    // Built-in XCommander command
    ChangePath,         // Change current directory
    OpenWithDefault,    // Open with default application
    CommandSequence,    // Multiple commands in sequence
    Custom              // Custom command handler
}

/// <summary>
/// Context for menu item execution.
/// </summary>
public class MenuExecutionContext
{
    public string? SourcePath { get; init; }
    public string? TargetPath { get; init; }
    public string? CurrentFileName { get; init; }
    public string? CurrentFileNameNoExt { get; init; }
    public string? CurrentExtension { get; init; }
    public IReadOnlyList<string>? SelectedFiles { get; init; }
    public IReadOnlyList<string>? SelectedFilesSource { get; init; }
    public IReadOnlyList<string>? SelectedFilesTarget { get; init; }
    public string? SourceListFile { get; init; }
    public string? TargetListFile { get; init; }
}

/// <summary>
/// Parameter placeholder for command substitution.
/// </summary>
public class ParameterPlaceholder
{
    public string Placeholder { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Example { get; init; }
    public string Category { get; init; } = string.Empty;
}
