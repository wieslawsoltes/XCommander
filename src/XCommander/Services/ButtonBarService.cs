using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Button bar service for managing customizable toolbar buttons.
/// </summary>
public class ButtonBarService : IButtonBarService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "ButtonBars");
    
    private static readonly string ConfigFile = Path.Combine(ConfigDirectory, "buttonbars.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    private List<ButtonBar> _buttonBars = new();
    private bool _isLoaded;
    private readonly object _lock = new();
    
    private static readonly List<BuiltInCommand> BuiltInCommands = new()
    {
        // File Operations
        new BuiltInCommand { Id = "file.copy", Name = "Copy", Category = "File", Description = "Copy selected files", DefaultIcon = "📋", DefaultShortcut = "F5" },
        new BuiltInCommand { Id = "file.move", Name = "Move", Category = "File", Description = "Move selected files", DefaultIcon = "📦", DefaultShortcut = "F6" },
        new BuiltInCommand { Id = "file.delete", Name = "Delete", Category = "File", Description = "Delete selected files", DefaultIcon = "🗑️", DefaultShortcut = "F8" },
        new BuiltInCommand { Id = "file.rename", Name = "Rename", Category = "File", Description = "Rename selected file", DefaultIcon = "✏️", DefaultShortcut = "F2" },
        new BuiltInCommand { Id = "file.newFolder", Name = "New Folder", Category = "File", Description = "Create new folder", DefaultIcon = "📁", DefaultShortcut = "F7" },
        new BuiltInCommand { Id = "file.newFile", Name = "New File", Category = "File", Description = "Create new file", DefaultIcon = "📄", DefaultShortcut = "Shift+F4" },
        new BuiltInCommand { Id = "file.edit", Name = "Edit", Category = "File", Description = "Edit selected file", DefaultIcon = "📝", DefaultShortcut = "F4" },
        new BuiltInCommand { Id = "file.view", Name = "View", Category = "File", Description = "View selected file", DefaultIcon = "👁️", DefaultShortcut = "F3" },
        
        // Navigation
        new BuiltInCommand { Id = "nav.parent", Name = "Parent Directory", Category = "Navigation", Description = "Go to parent directory", DefaultIcon = "⬆️", DefaultShortcut = "Backspace" },
        new BuiltInCommand { Id = "nav.root", Name = "Root Directory", Category = "Navigation", Description = "Go to root directory", DefaultIcon = "🏠", DefaultShortcut = "Ctrl+\\" },
        new BuiltInCommand { Id = "nav.home", Name = "Home Directory", Category = "Navigation", Description = "Go to home directory", DefaultIcon = "🏡", DefaultShortcut = "Ctrl+Home" },
        new BuiltInCommand { Id = "nav.back", Name = "Back", Category = "Navigation", Description = "Go back in history", DefaultIcon = "⬅️", DefaultShortcut = "Alt+Left" },
        new BuiltInCommand { Id = "nav.forward", Name = "Forward", Category = "Navigation", Description = "Go forward in history", DefaultIcon = "➡️", DefaultShortcut = "Alt+Right" },
        new BuiltInCommand { Id = "nav.refresh", Name = "Refresh", Category = "Navigation", Description = "Refresh current view", DefaultIcon = "🔄", DefaultShortcut = "Ctrl+R" },
        
        // Selection
        new BuiltInCommand { Id = "select.all", Name = "Select All", Category = "Selection", Description = "Select all files", DefaultIcon = "☑️", DefaultShortcut = "Ctrl+A" },
        new BuiltInCommand { Id = "select.none", Name = "Deselect All", Category = "Selection", Description = "Deselect all files", DefaultIcon = "⬜", DefaultShortcut = "Ctrl+D" },
        new BuiltInCommand { Id = "select.invert", Name = "Invert Selection", Category = "Selection", Description = "Invert current selection", DefaultIcon = "🔀", DefaultShortcut = "Ctrl+I" },
        new BuiltInCommand { Id = "select.byPattern", Name = "Select by Pattern", Category = "Selection", Description = "Select files by pattern", DefaultIcon = "🔍", DefaultShortcut = "Num+" },
        
        // View
        new BuiltInCommand { Id = "view.details", Name = "Details View", Category = "View", Description = "Show detailed file list", DefaultIcon = "📋" },
        new BuiltInCommand { Id = "view.thumbnails", Name = "Thumbnail View", Category = "View", Description = "Show file thumbnails", DefaultIcon = "🖼️" },
        new BuiltInCommand { Id = "view.hidden", Name = "Show Hidden", Category = "View", Description = "Toggle hidden files", DefaultIcon = "👻", DefaultShortcut = "Ctrl+H" },
        new BuiltInCommand { Id = "view.tree", Name = "Tree View", Category = "View", Description = "Show folder tree", DefaultIcon = "🌳" },
        
        // Tools
        new BuiltInCommand { Id = "tools.search", Name = "Search", Category = "Tools", Description = "Search for files", DefaultIcon = "🔎", DefaultShortcut = "Ctrl+F" },
        new BuiltInCommand { Id = "tools.compare", Name = "Compare", Category = "Tools", Description = "Compare files/folders", DefaultIcon = "⚖️" },
        new BuiltInCommand { Id = "tools.sync", Name = "Synchronize", Category = "Tools", Description = "Synchronize folders", DefaultIcon = "🔄" },
        new BuiltInCommand { Id = "tools.terminal", Name = "Terminal", Category = "Tools", Description = "Open terminal here", DefaultIcon = "💻", DefaultShortcut = "Ctrl+T" },
        new BuiltInCommand { Id = "tools.pack", Name = "Pack", Category = "Tools", Description = "Create archive", DefaultIcon = "📦", DefaultShortcut = "Alt+F5" },
        new BuiltInCommand { Id = "tools.unpack", Name = "Unpack", Category = "Tools", Description = "Extract archive", DefaultIcon = "📂", DefaultShortcut = "Alt+F9" },
        
        // Panel
        new BuiltInCommand { Id = "panel.swap", Name = "Swap Panels", Category = "Panel", Description = "Swap left and right panels", DefaultIcon = "🔄", DefaultShortcut = "Ctrl+U" },
        new BuiltInCommand { Id = "panel.equalPaths", Name = "Equal Paths", Category = "Panel", Description = "Set both panels to same path", DefaultIcon = "=" },
        new BuiltInCommand { Id = "panel.targetToSource", Name = "Target to Source", Category = "Panel", Description = "Copy target path to source", DefaultIcon = "⬅️" },
        new BuiltInCommand { Id = "panel.sourceToTarget", Name = "Source to Target", Category = "Panel", Description = "Copy source path to target", DefaultIcon = "➡️" }
    };
    
    public async Task<IReadOnlyList<ButtonBar>> GetButtonBarsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        lock (_lock)
        {
            return _buttonBars.ToList();
        }
    }
    
    public async Task<ButtonBar?> GetDefaultButtonBarAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        lock (_lock)
        {
            return _buttonBars.FirstOrDefault(b => b.IsDefault) ?? _buttonBars.FirstOrDefault();
        }
    }
    
    public async Task<ButtonBar> CreateButtonBarAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var bar = new ButtonBar
        {
            Name = name,
            IsDefault = !_buttonBars.Any()
        };
        
        lock (_lock)
        {
            _buttonBars.Add(bar);
        }
        
        await SaveAsync(cancellationToken);
        return bar;
    }
    
    public async Task UpdateButtonBarAsync(ButtonBar buttonBar, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var index = _buttonBars.FindIndex(b => b.Id == buttonBar.Id);
            if (index >= 0)
            {
                buttonBar.ModifiedAt = DateTime.Now;
                _buttonBars[index] = buttonBar;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task DeleteButtonBarAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            _buttonBars.RemoveAll(b => b.Id == id);
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task SetDefaultButtonBarAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            foreach (var bar in _buttonBars)
            {
                bar.IsDefault = bar.Id == id;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task<ButtonBarItem> AddButtonAsync(string barId, ButtonBarItem button, int? position = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var bar = _buttonBars.FirstOrDefault(b => b.Id == barId);
            if (bar != null)
            {
                if (position.HasValue && position.Value >= 0 && position.Value < bar.Buttons.Count)
                {
                    bar.Buttons.Insert(position.Value, button);
                }
                else
                {
                    bar.Buttons.Add(button);
                }
                
                // Update order
                for (int i = 0; i < bar.Buttons.Count; i++)
                {
                    bar.Buttons[i].Order = i;
                }
                
                bar.ModifiedAt = DateTime.Now;
            }
        }
        
        await SaveAsync(cancellationToken);
        return button;
    }
    
    public async Task UpdateButtonAsync(string barId, ButtonBarItem button, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var bar = _buttonBars.FirstOrDefault(b => b.Id == barId);
            if (bar != null)
            {
                var index = bar.Buttons.FindIndex(b => b.Id == button.Id);
                if (index >= 0)
                {
                    bar.Buttons[index] = button;
                    bar.ModifiedAt = DateTime.Now;
                }
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task RemoveButtonAsync(string barId, string buttonId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var bar = _buttonBars.FirstOrDefault(b => b.Id == barId);
            if (bar != null)
            {
                bar.Buttons.RemoveAll(b => b.Id == buttonId);
                bar.ModifiedAt = DateTime.Now;
                
                // Update order
                for (int i = 0; i < bar.Buttons.Count; i++)
                {
                    bar.Buttons[i].Order = i;
                }
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task MoveButtonAsync(string barId, string buttonId, int newPosition, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var bar = _buttonBars.FirstOrDefault(b => b.Id == barId);
            if (bar != null)
            {
                var button = bar.Buttons.FirstOrDefault(b => b.Id == buttonId);
                if (button != null)
                {
                    bar.Buttons.Remove(button);
                    newPosition = Math.Clamp(newPosition, 0, bar.Buttons.Count);
                    bar.Buttons.Insert(newPosition, button);
                    
                    // Update order
                    for (int i = 0; i < bar.Buttons.Count; i++)
                    {
                        bar.Buttons[i].Order = i;
                    }
                    
                    bar.ModifiedAt = DateTime.Now;
                }
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task<ButtonBar> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var imported = JsonSerializer.Deserialize<ButtonBar>(json, JsonOptions)
                  ?? throw new InvalidDataException("Invalid button bar file");
        
        // Create new bar with new ID to avoid conflicts
        var bar = new ButtonBar
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = imported.Name,
            Description = imported.Description,
            IsDefault = false,
            IsVisible = imported.IsVisible,
            Position = imported.Position,
            Buttons = imported.Buttons,
            ModifiedAt = DateTime.Now
        };
        
        lock (_lock)
        {
            _buttonBars.Add(bar);
        }
        
        await SaveAsync(cancellationToken);
        return bar;
    }
    
    public async Task ExportAsync(string barId, string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        ButtonBar? bar;
        lock (_lock)
        {
            bar = _buttonBars.FirstOrDefault(b => b.Id == barId);
        }
        
        if (bar == null)
        {
            throw new InvalidOperationException($"Button bar not found: {barId}");
        }
        
        var json = JsonSerializer.Serialize(bar, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public async Task ExecuteButtonAsync(ButtonBarItem button, ButtonExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (button.Command == null) return;
        
        await Task.Run(() =>
        {
            switch (button.Command.Type)
            {
                case ButtonCommandType.BuiltIn:
                    ExecuteBuiltInCommand(button.Command, context);
                    break;
                    
                case ButtonCommandType.External:
                    ExecuteExternalCommand(button.Command, context);
                    break;
                    
                case ButtonCommandType.Script:
                    ExecuteScript(button.Command, context);
                    break;
            }
        }, cancellationToken);
    }
    
    public IReadOnlyList<BuiltInCommand> GetBuiltInCommands()
    {
        return BuiltInCommands;
    }
    
    #region Private Helpers
    
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded) return;
        
        lock (_lock)
        {
            if (_isLoaded) return;
        }
        
        Directory.CreateDirectory(ConfigDirectory);
        
        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(ConfigFile, cancellationToken);
                var loaded = JsonSerializer.Deserialize<List<ButtonBar>>(json, JsonOptions);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        _buttonBars = loaded;
                    }
                }
            }
            catch
            {
                // Ignore load errors, use empty list
            }
        }
        
        // Create default button bar if none exist
        lock (_lock)
        {
            if (_buttonBars.Count == 0)
            {
                _buttonBars.Add(CreateDefaultButtonBar());
            }
            _isLoaded = true;
        }
    }
    
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        List<ButtonBar> toSave;
        lock (_lock)
        {
            toSave = _buttonBars.ToList();
        }
        
        var json = JsonSerializer.Serialize(toSave, JsonOptions);
        await File.WriteAllTextAsync(ConfigFile, json, cancellationToken);
    }
    
    private static ButtonBar CreateDefaultButtonBar()
    {
        return new ButtonBar
        {
            Name = "Default",
            IsDefault = true,
            Buttons = new List<ButtonBarItem>
            {
                CreateButton("nav.parent", "⬆️", "Parent", 0),
                CreateButton("nav.home", "🏡", "Home", 1),
                CreateSeparator(2),
                CreateButton("file.view", "👁️", "View", 3),
                CreateButton("file.edit", "📝", "Edit", 4),
                CreateButton("file.copy", "📋", "Copy", 5),
                CreateButton("file.move", "📦", "Move", 6),
                CreateButton("file.newFolder", "📁", "New Folder", 7),
                CreateButton("file.delete", "🗑️", "Delete", 8),
                CreateSeparator(9),
                CreateButton("tools.search", "🔎", "Search", 10),
                CreateButton("tools.terminal", "💻", "Terminal", 11),
                CreateSeparator(12),
                CreateButton("tools.pack", "📦", "Pack", 13),
                CreateButton("tools.unpack", "📂", "Unpack", 14),
            }
        };
    }
    
    private static ButtonBarItem CreateButton(string commandId, string icon, string label, int order)
    {
        return new ButtonBarItem
        {
            Label = label,
            Icon = icon,
            Order = order,
            Type = ButtonBarItemType.Command,
            Command = new ButtonCommand
            {
                Type = ButtonCommandType.BuiltIn,
                CommandId = commandId
            }
        };
    }
    
    private static ButtonBarItem CreateSeparator(int order)
    {
        return new ButtonBarItem
        {
            Type = ButtonBarItemType.Separator,
            Order = order
        };
    }
    
    private void ExecuteBuiltInCommand(ButtonCommand command, ButtonExecutionContext context)
    {
        // Built-in commands would be executed by the main application
        // This service just provides the infrastructure
        // The actual command execution would be handled by a command dispatcher
    }
    
    private void ExecuteExternalCommand(ButtonCommand command, ButtonExecutionContext context)
    {
        if (string.IsNullOrEmpty(command.ExecutablePath)) return;
        
        var arguments = SubstituteVariables(command.Arguments ?? "", context);
        var workingDir = SubstituteVariables(command.WorkingDirectory ?? context.CurrentPath ?? "", context);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = !command.RunInTerminal
        };
        
        var process = Process.Start(processInfo);
        
        if (command.WaitForExit && process != null)
        {
            process.WaitForExit();
        }
    }
    
    private void ExecuteScript(ButtonCommand command, ButtonExecutionContext context)
    {
        if (string.IsNullOrEmpty(command.ExecutablePath)) return;
        
        var scriptPath = command.ExecutablePath;
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        
        string interpreter;
        string arguments;
        
        switch (extension)
        {
            case ".ps1":
                interpreter = "pwsh";
                arguments = $"-File \"{scriptPath}\"";
                break;
            case ".py":
                interpreter = "python";
                arguments = $"\"{scriptPath}\"";
                break;
            case ".sh":
                interpreter = "/bin/bash";
                arguments = $"\"{scriptPath}\"";
                break;
            case ".bat":
            case ".cmd":
                interpreter = "cmd.exe";
                arguments = $"/c \"{scriptPath}\"";
                break;
            default:
                interpreter = scriptPath;
                arguments = command.Arguments ?? "";
                break;
        }
        
        arguments = SubstituteVariables(arguments + " " + (command.Arguments ?? ""), context);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = interpreter,
            Arguments = arguments,
            WorkingDirectory = context.CurrentPath ?? Environment.CurrentDirectory,
            UseShellExecute = false
        };
        
        var process = Process.Start(processInfo);
        
        if (command.WaitForExit && process != null)
        {
            process.WaitForExit();
        }
    }
    
    private static string SubstituteVariables(string input, ButtonExecutionContext context)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Total Commander style variable substitution
        var result = input
            .Replace("%P", context.CurrentPath ?? "")               // Current path
            .Replace("%T", context.TargetPath ?? "")                // Target path
            .Replace("%N", context.CurrentFile ?? "")               // Current filename
            .Replace("%O", Path.GetFileNameWithoutExtension(context.CurrentFile ?? ""))  // Name without extension
            .Replace("%E", Path.GetExtension(context.CurrentFile ?? ""))  // Extension only
            .Replace("%S", string.Join(" ", context.SelectedFiles?.Select(f => $"\"{f}\"") ?? Array.Empty<string>()));  // Selected files
        
        // Additional variables
        if (context.Variables != null)
        {
            foreach (var (key, value) in context.Variables)
            {
                result = result.Replace($"%{key}%", value);
            }
        }
        
        return result;
    }
    
    #endregion
}
