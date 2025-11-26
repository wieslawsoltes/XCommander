using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Interface for customizable button bar management.
/// </summary>
public interface IButtonBarService
{
    /// <summary>
    /// Gets all button bars.
    /// </summary>
    Task<IReadOnlyList<ButtonBar>> GetButtonBarsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the default button bar.
    /// </summary>
    Task<ButtonBar?> GetDefaultButtonBarAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new button bar.
    /// </summary>
    Task<ButtonBar> CreateButtonBarAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a button bar.
    /// </summary>
    Task UpdateButtonBarAsync(ButtonBar buttonBar, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a button bar.
    /// </summary>
    Task DeleteButtonBarAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the default button bar.
    /// </summary>
    Task SetDefaultButtonBarAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a button to a button bar.
    /// </summary>
    Task<ButtonBarItem> AddButtonAsync(string barId, ButtonBarItem button, int? position = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a button in a button bar.
    /// </summary>
    Task UpdateButtonAsync(string barId, ButtonBarItem button, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a button from a button bar.
    /// </summary>
    Task RemoveButtonAsync(string barId, string buttonId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a button within a button bar.
    /// </summary>
    Task MoveButtonAsync(string barId, string buttonId, int newPosition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports a button bar from file.
    /// </summary>
    Task<ButtonBar> ImportAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a button bar to file.
    /// </summary>
    Task ExportAsync(string barId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a button command.
    /// </summary>
    Task ExecuteButtonAsync(ButtonBarItem button, ButtonExecutionContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets available built-in commands.
    /// </summary>
    IReadOnlyList<BuiltInCommand> GetBuiltInCommands();
}

/// <summary>
/// A customizable button bar.
/// </summary>
public class ButtonBar
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsVisible { get; set; } = true;
    public ButtonBarPosition Position { get; set; } = ButtonBarPosition.Top;
    public List<ButtonBarItem> Buttons { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Position of the button bar.
/// </summary>
public enum ButtonBarPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Floating
}

/// <summary>
/// A button in the button bar.
/// </summary>
public class ButtonBarItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string? Tooltip { get; set; }
    public string? Icon { get; set; }
    public string? IconPath { get; set; }
    public ButtonBarItemType Type { get; set; } = ButtonBarItemType.Command;
    public ButtonCommand? Command { get; set; }
    public List<ButtonBarItem>? SubMenu { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public string? KeyboardShortcut { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// Type of button bar item.
/// </summary>
public enum ButtonBarItemType
{
    Command,        // Executes a command
    Separator,      // Visual separator
    SubMenu,        // Dropdown menu
    Toggle,         // Toggle button
    Spacer          // Flexible space
}

/// <summary>
/// Command associated with a button.
/// </summary>
public class ButtonCommand
{
    public ButtonCommandType Type { get; set; }
    public string? CommandId { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public bool RunInTerminal { get; set; }
    public bool WaitForExit { get; set; }
}

/// <summary>
/// Type of button command.
/// </summary>
public enum ButtonCommandType
{
    BuiltIn,            // Built-in XCommander command
    External,           // External program
    Script,             // Script execution
    InternalAction,     // Internal navigation/action
    BatchJob            // Execute a batch job
}

/// <summary>
/// Context for button execution.
/// </summary>
public class ButtonExecutionContext
{
    public string? CurrentPath { get; init; }
    public string? TargetPath { get; init; }
    public IReadOnlyList<string>? SelectedFiles { get; init; }
    public string? CurrentFile { get; init; }
    public Dictionary<string, string>? Variables { get; init; }
}

/// <summary>
/// Built-in command definition.
/// </summary>
public class BuiltInCommand
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? DefaultIcon { get; init; }
    public string? DefaultShortcut { get; init; }
}
