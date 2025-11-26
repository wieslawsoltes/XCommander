// IInternalCommandService.cs - TC-style internal command system
// Define custom executable commands with keyboard shortcuts

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Parameter type for commands
/// </summary>
public enum CommandParamType
{
    None,           // No parameter
    SourceFile,     // Current file in source panel
    TargetFile,     // Current file in target panel
    SourceDir,      // Current directory in source panel
    TargetDir,      // Current directory in target panel
    SelectedFiles,  // Selected files in source panel
    InputBox,       // Show input dialog
    FileSelect,     // Show file selection dialog
    DirSelect,      // Show directory selection dialog
    Custom          // Custom value
}

/// <summary>
/// Command execution context
/// </summary>
public enum CommandScope
{
    Global,         // Available everywhere
    FilePanel,      // Only in file panels
    Viewer,         // Only in viewer
    Editor,         // Only in editor
    Dialog          // Only in dialogs
}

/// <summary>
/// Command definition
/// </summary>
public record InternalCommand
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string Executable { get; init; } = string.Empty;
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<CommandParam> Parameters { get; init; } = Array.Empty<CommandParam>();
    public string? KeyboardShortcut { get; init; }
    public CommandScope Scope { get; init; } = CommandScope.Global;
    public bool RunMinimized { get; init; }
    public bool RunMaximized { get; init; }
    public bool WaitForExit { get; init; }
    public bool CaptureOutput { get; init; }
    public bool ConfirmExecution { get; init; }
    public string? ConfirmMessage { get; init; }
    public string? IconPath { get; init; }
    public int? MenuOrder { get; init; }
    public bool ShowInMenu { get; init; } = true;
    public bool ShowInToolbar { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// Command parameter
/// </summary>
public record CommandParam
{
    public string Name { get; init; } = string.Empty;
    public CommandParamType Type { get; init; }
    public string? Placeholder { get; init; }
    public string? DefaultValue { get; init; }
    public string? PromptText { get; init; }
    public bool Required { get; init; }
    public string? ValidationRegex { get; init; }
}

/// <summary>
/// Command execution result
/// </summary>
public record InternalCommandResult
{
    public bool Success { get; init; }
    public int? ExitCode { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Command execution context data
/// </summary>
public record CommandExecutionContext
{
    public string? SourcePath { get; init; }
    public string? TargetPath { get; init; }
    public string? CurrentFileName { get; init; }
    public string? CurrentFileExtension { get; init; }
    public IReadOnlyList<string> SelectedFiles { get; init; } = Array.Empty<string>();
    public string? ClipboardText { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}

/// <summary>
/// Command category
/// </summary>
public record InternalCommandCategory
{
    public string Name { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public int Order { get; init; }
}

/// <summary>
/// TC placeholder definitions
/// </summary>
public static class TCCommandPlaceholders
{
    public const string SourcePath = "%P";              // Source panel path
    public const string TargetPath = "%T";              // Target panel path
    public const string CurrentFileName = "%N";         // Current filename without extension
    public const string CurrentFileExt = "%E";          // Current file extension
    public const string CurrentFullName = "%F";         // Current filename with extension
    public const string CurrentFullPath = "%S";         // Current file full path
    public const string SelectedFiles = "%L";           // Selected file list
    public const string SelectedFilesQuoted = "%Q";     // Selected files quoted
    public const string ShortSourcePath = "%p";         // Source path short (8.3)
    public const string ShortTargetPath = "%t";         // Target path short
    public const string ShortFileName = "%n";           // Filename short
    public const string DriveSource = "%D";             // Source drive letter
    public const string DriveTarget = "%d";             // Target drive letter
    public const string PanelWidth = "%W";              // Panel width
    public const string PanelHeight = "%H";             // Panel height
    public const string SelectedCount = "%C";           // Number of selected files
    public const string TotalSize = "%Z";               // Total size of selected files
    public const string CurrentDate = "%DATE";          // Current date
    public const string CurrentTime = "%TIME";          // Current time
}

/// <summary>
/// Service for managing internal commands
/// </summary>
public interface IInternalCommandService
{
    // ======= Command Management =======
    
    IReadOnlyList<InternalCommand> GetCommands();
    IReadOnlyList<InternalCommand> GetCommandsByCategory(string category);
    InternalCommand? GetCommand(string commandId);
    InternalCommand? GetCommandByShortcut(string shortcut);
    Task<InternalCommand> CreateCommandAsync(InternalCommand command, CancellationToken cancellationToken = default);
    Task<InternalCommand> UpdateCommandAsync(InternalCommand command, CancellationToken cancellationToken = default);
    Task<bool> DeleteCommandAsync(string commandId, CancellationToken cancellationToken = default);
    Task<InternalCommand> DuplicateCommandAsync(string commandId, CancellationToken cancellationToken = default);
    
    // ======= Category Management =======
    
    IReadOnlyList<InternalCommandCategory> GetCategories();
    Task<InternalCommandCategory> CreateCategoryAsync(InternalCommandCategory category, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(string categoryName, CancellationToken cancellationToken = default);
    
    // ======= Execution =======
    
    Task<InternalCommandResult> ExecuteAsync(
        string commandId,
        CommandExecutionContext context,
        Dictionary<string, string>? additionalParams = null,
        CancellationToken cancellationToken = default);
    
    Task<InternalCommandResult?> ExecuteByShortcutAsync(
        string shortcut,
        CommandExecutionContext context,
        CancellationToken cancellationToken = default);
    
    string PreviewCommand(InternalCommand command, CommandExecutionContext context);
    string ExpandPlaceholders(string text, CommandExecutionContext context);
    
    // ======= Shortcuts =======
    
    IReadOnlyDictionary<string, string> GetShortcuts();
    bool IsShortcutAvailable(string shortcut, string? excludeCommandId = null);
    (bool Ctrl, bool Alt, bool Shift, string Key) ParseShortcut(string shortcut);
    
    // ======= Import/Export =======
    
    Task<IReadOnlyList<InternalCommand>> ImportCommandsAsync(string filePath, CancellationToken cancellationToken = default);
    Task ExportCommandsAsync(IEnumerable<string> commandIds, string filePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InternalCommand>> ImportFromTCAsync(string filePath, CancellationToken cancellationToken = default);
    Task ExportToTCAsync(IEnumerable<string> commandIds, string filePath, CancellationToken cancellationToken = default);
    
    // ======= Built-in Commands =======
    
    IReadOnlyList<InternalCommand> GetBuiltInCommands();
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
    
    // ======= Events =======
    
    event EventHandler<InternalCommandExecutedEventArgs>? CommandExecuted;
    event EventHandler<EventArgs>? CommandsChanged;
}

/// <summary>
/// Command executed event args
/// </summary>
public class InternalCommandExecutedEventArgs : EventArgs
{
    public InternalCommand Command { get; }
    public InternalCommandResult Result { get; }
    
    public InternalCommandExecutedEventArgs(InternalCommand command, InternalCommandResult result)
    {
        Command = command;
        Result = result;
    }
}
