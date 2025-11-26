// InternalCommandService.cs - Implementation of internal command system
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Internal command management implementation
/// </summary>
public class InternalCommandService : IInternalCommandService
{
    private readonly ILongPathService _longPathService;
    private readonly List<InternalCommand> _commands = new();
    private readonly List<InternalCommandCategory> _categories = new();
    private readonly List<InternalCommand> _builtInCommands;
    private readonly string _commandsFilePath;
    private readonly object _lock = new();
    
    public event EventHandler<InternalCommandExecutedEventArgs>? CommandExecuted;
    public event EventHandler<EventArgs>? CommandsChanged;
    
    public InternalCommandService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
        _commandsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander", "internalcmds.json");
        
        _builtInCommands = CreateBuiltInCommands();
        LoadCommandsAsync().ConfigureAwait(false);
    }
    
    // ======= Command Management =======
    
    public IReadOnlyList<InternalCommand> GetCommands()
    {
        lock (_lock) { return _commands.ToList().AsReadOnly(); }
    }
    
    public IReadOnlyList<InternalCommand> GetCommandsByCategory(string category)
    {
        lock (_lock)
        {
            return _commands
                .Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.MenuOrder ?? int.MaxValue)
                .ThenBy(c => c.Name)
                .ToList().AsReadOnly();
        }
    }
    
    public InternalCommand? GetCommand(string commandId)
    {
        lock (_lock)
        {
            return _commands.FirstOrDefault(c => c.Id == commandId)
                ?? _builtInCommands.FirstOrDefault(c => c.Id == commandId);
        }
    }
    
    public InternalCommand? GetCommandByShortcut(string shortcut)
    {
        lock (_lock)
        {
            var normalized = NormalizeShortcut(shortcut);
            return _commands.FirstOrDefault(c => 
                c.KeyboardShortcut != null && NormalizeShortcut(c.KeyboardShortcut) == normalized)
                ?? _builtInCommands.FirstOrDefault(c => 
                    c.KeyboardShortcut != null && NormalizeShortcut(c.KeyboardShortcut) == normalized);
        }
    }
    
    public async Task<InternalCommand> CreateCommandAsync(InternalCommand command, CancellationToken cancellationToken = default)
    {
        var newCommand = command with { Id = Guid.NewGuid().ToString(), CreatedAt = DateTime.Now };
        lock (_lock) { _commands.Add(newCommand); }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return newCommand;
    }
    
    public async Task<InternalCommand> UpdateCommandAsync(InternalCommand command, CancellationToken cancellationToken = default)
    {
        var updated = command with { ModifiedAt = DateTime.Now };
        lock (_lock)
        {
            var index = _commands.FindIndex(c => c.Id == command.Id);
            if (index < 0) throw new InvalidOperationException($"Command not found: {command.Id}");
            _commands[index] = updated;
        }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return updated;
    }
    
    public async Task<bool> DeleteCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        bool removed;
        lock (_lock) { removed = _commands.RemoveAll(c => c.Id == commandId) > 0; }
        if (removed)
        {
            await SaveCommandsAsync(cancellationToken);
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }
        return removed;
    }
    
    public async Task<InternalCommand> DuplicateCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        var original = GetCommand(commandId) ?? throw new InvalidOperationException($"Command not found: {commandId}");
        var duplicate = original with
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{original.Name} (Copy)",
            KeyboardShortcut = null,
            CreatedAt = DateTime.Now,
            ModifiedAt = null
        };
        lock (_lock) { _commands.Add(duplicate); }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return duplicate;
    }
    
    // ======= Category Management =======
    
    public IReadOnlyList<InternalCommandCategory> GetCategories()
    {
        lock (_lock)
        {
            var categoriesFromCommands = _commands
                .Where(c => !string.IsNullOrEmpty(c.Category))
                .Select(c => c.Category!)
                .Distinct()
                .Where(c => !_categories.Any(cat => cat.Name == c))
                .Select(c => new InternalCommandCategory { Name = c });
            return _categories.Concat(categoriesFromCommands).OrderBy(c => c.Order).ThenBy(c => c.Name).ToList().AsReadOnly();
        }
    }
    
    public async Task<InternalCommandCategory> CreateCategoryAsync(InternalCommandCategory category, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_categories.Any(c => c.Name == category.Name))
                throw new InvalidOperationException($"Category already exists: {category.Name}");
            _categories.Add(category);
        }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return category;
    }
    
    public async Task<bool> DeleteCategoryAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var removed = _categories.RemoveAll(c => c.Name == categoryName) > 0;
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].Category == categoryName)
                    _commands[i] = _commands[i] with { Category = null };
            }
            if (removed)
            {
                _ = SaveCommandsAsync(cancellationToken);
                CommandsChanged?.Invoke(this, EventArgs.Empty);
            }
            return removed;
        }
    }
    
    // ======= Execution =======
    
    public async Task<InternalCommandResult> ExecuteAsync(
        string commandId,
        CommandExecutionContext context,
        Dictionary<string, string>? additionalParams = null,
        CancellationToken cancellationToken = default)
    {
        var command = GetCommand(commandId);
        if (command == null)
            return new InternalCommandResult { Success = false, ErrorMessage = $"Command not found: {commandId}" };
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var mergedContext = context;
            if (additionalParams != null)
            {
                var variables = new Dictionary<string, string>(context.Variables);
                foreach (var param in additionalParams) variables[param.Key] = param.Value;
                mergedContext = context with { Variables = variables };
            }
            
            var expandedCommand = ExpandPlaceholders(command.Executable, mergedContext);
            var expandedArgs = command.Arguments != null ? ExpandPlaceholders(command.Arguments, mergedContext) : null;
            var expandedWorkDir = command.WorkingDirectory != null
                ? ExpandPlaceholders(command.WorkingDirectory, mergedContext)
                : context.SourcePath;
            
            var psi = new ProcessStartInfo
            {
                FileName = expandedCommand,
                Arguments = expandedArgs ?? string.Empty,
                WorkingDirectory = expandedWorkDir ?? Environment.CurrentDirectory,
                UseShellExecute = !command.CaptureOutput,
                RedirectStandardOutput = command.CaptureOutput,
                RedirectStandardError = command.CaptureOutput,
                CreateNoWindow = command.RunMinimized
            };
            
            if (command.RunMinimized && psi.UseShellExecute)
                psi.WindowStyle = ProcessWindowStyle.Minimized;
            else if (command.RunMaximized && psi.UseShellExecute)
                psi.WindowStyle = ProcessWindowStyle.Maximized;
            
            string? output = null, error = null;
            int exitCode = 0;
            
            using var process = Process.Start(psi);
            if (process == null)
                return new InternalCommandResult { Success = false, ErrorMessage = "Failed to start process" };
            
            if (command.WaitForExit || command.CaptureOutput)
            {
                if (command.CaptureOutput)
                {
                    output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    error = await process.StandardError.ReadToEndAsync(cancellationToken);
                }
                await process.WaitForExitAsync(cancellationToken);
                exitCode = process.ExitCode;
            }
            
            stopwatch.Stop();
            var result = new InternalCommandResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                Output = output,
                Error = error,
                Duration = stopwatch.Elapsed
            };
            CommandExecuted?.Invoke(this, new InternalCommandExecutedEventArgs(command, result));
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new InternalCommandResult { Success = false, ErrorMessage = ex.Message, Duration = stopwatch.Elapsed };
            CommandExecuted?.Invoke(this, new InternalCommandExecutedEventArgs(command, result));
            return result;
        }
    }
    
    public async Task<InternalCommandResult?> ExecuteByShortcutAsync(
        string shortcut,
        CommandExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var command = GetCommandByShortcut(shortcut);
        if (command == null) return null;
        return await ExecuteAsync(command.Id, context, cancellationToken: cancellationToken);
    }
    
    public string PreviewCommand(InternalCommand command, CommandExecutionContext context)
    {
        var expandedCommand = ExpandPlaceholders(command.Executable, context);
        var expandedArgs = command.Arguments != null ? ExpandPlaceholders(command.Arguments, context) : string.Empty;
        return $"{expandedCommand} {expandedArgs}".Trim();
    }
    
    public string ExpandPlaceholders(string text, CommandExecutionContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var result = text;
        result = result.Replace(TCCommandPlaceholders.SourcePath, context.SourcePath ?? string.Empty);
        result = result.Replace(TCCommandPlaceholders.TargetPath, context.TargetPath ?? string.Empty);
        result = result.Replace(TCCommandPlaceholders.CurrentFileName, 
            context.CurrentFileName != null ? Path.GetFileNameWithoutExtension(context.CurrentFileName) : string.Empty);
        result = result.Replace(TCCommandPlaceholders.CurrentFileExt, context.CurrentFileExtension ?? string.Empty);
        result = result.Replace(TCCommandPlaceholders.CurrentFullName, context.CurrentFileName ?? string.Empty);
        result = result.Replace(TCCommandPlaceholders.CurrentFullPath,
            context.SourcePath != null && context.CurrentFileName != null
                ? Path.Combine(context.SourcePath, context.CurrentFileName) : string.Empty);
        
        if (context.SelectedFiles.Count > 0)
        {
            result = result.Replace(TCCommandPlaceholders.SelectedFiles, string.Join(" ", context.SelectedFiles));
            result = result.Replace(TCCommandPlaceholders.SelectedFilesQuoted, string.Join(" ", context.SelectedFiles.Select(f => $"\"{f}\"")));
            result = result.Replace(TCCommandPlaceholders.SelectedCount, context.SelectedFiles.Count.ToString());
        }
        else
        {
            result = result.Replace(TCCommandPlaceholders.SelectedFiles, string.Empty);
            result = result.Replace(TCCommandPlaceholders.SelectedFilesQuoted, string.Empty);
            result = result.Replace(TCCommandPlaceholders.SelectedCount, "0");
        }
        
        result = result.Replace(TCCommandPlaceholders.CurrentDate, DateTime.Now.ToString("yyyy-MM-dd"));
        result = result.Replace(TCCommandPlaceholders.CurrentTime, DateTime.Now.ToString("HH:mm:ss"));
        
        foreach (var variable in context.Variables)
            result = result.Replace($"%{variable.Key}%", variable.Value);
        
        return result;
    }
    
    // ======= Shortcuts =======
    
    public IReadOnlyDictionary<string, string> GetShortcuts()
    {
        lock (_lock)
        {
            var shortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cmd in _builtInCommands.Concat(_commands))
            {
                if (!string.IsNullOrEmpty(cmd.KeyboardShortcut))
                    shortcuts[NormalizeShortcut(cmd.KeyboardShortcut)] = cmd.Id;
            }
            return shortcuts;
        }
    }
    
    public bool IsShortcutAvailable(string shortcut, string? excludeCommandId = null)
    {
        var existing = GetCommandByShortcut(shortcut);
        return existing == null || existing.Id == excludeCommandId;
    }
    
    public (bool Ctrl, bool Alt, bool Shift, string Key) ParseShortcut(string shortcut)
    {
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries);
        bool ctrl = false, alt = false, shift = false;
        string key = string.Empty;
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.Equals(trimmed, "Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
            else if (string.Equals(trimmed, "Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else if (string.Equals(trimmed, "Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else key = trimmed;
        }
        return (ctrl, alt, shift, key);
    }
    
    private string NormalizeShortcut(string shortcut)
    {
        var (ctrl, alt, shift, key) = ParseShortcut(shortcut);
        var sb = new StringBuilder();
        if (ctrl) sb.Append("Ctrl+");
        if (alt) sb.Append("Alt+");
        if (shift) sb.Append("Shift+");
        sb.Append(key.ToUpperInvariant());
        return sb.ToString();
    }
    
    // ======= Import/Export =======
    
    public async Task<IReadOnlyList<InternalCommand>> ImportCommandsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var imported = JsonSerializer.Deserialize<List<InternalCommand>>(json) ?? new();
        var newCommands = imported.Select(c => c with { Id = Guid.NewGuid().ToString(), CreatedAt = DateTime.Now }).ToList();
        lock (_lock) { _commands.AddRange(newCommands); }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return newCommands.AsReadOnly();
    }
    
    public async Task ExportCommandsAsync(IEnumerable<string> commandIds, string filePath, CancellationToken cancellationToken = default)
    {
        List<InternalCommand> toExport;
        lock (_lock)
        {
            var ids = commandIds.ToHashSet();
            toExport = _commands.Where(c => ids.Contains(c.Id)).ToList();
        }
        var json = JsonSerializer.Serialize(toExport, new JsonSerializerOptions { WriteIndented = true });
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public async Task<IReadOnlyList<InternalCommand>> ImportFromTCAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var commands = new List<InternalCommand>();
        InternalCommand? current = null;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
            
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                if (current != null) commands.Add(current);
                var section = trimmed.Substring(1, trimmed.Length - 2);
                current = new InternalCommand { Id = Guid.NewGuid().ToString(), Name = section, CreatedAt = DateTime.Now };
                continue;
            }
            
            if (current == null) continue;
            
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = trimmed.Substring(0, eqIndex).Trim();
                var value = trimmed.Substring(eqIndex + 1).Trim();
                current = key.ToLowerInvariant() switch
                {
                    "cmd" or "command" => current with { Executable = value },
                    "param" or "parameters" => current with { Arguments = value },
                    "path" => current with { WorkingDirectory = value },
                    "menu" => current with { Name = value },
                    "shortcut" or "key" => current with { KeyboardShortcut = value },
                    "iconic" or "minimized" => current with { RunMinimized = value == "1" },
                    "wait" => current with { WaitForExit = value == "1" },
                    _ => current
                };
            }
        }
        
        if (current != null) commands.Add(current);
        lock (_lock) { _commands.AddRange(commands); }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
        return commands.AsReadOnly();
    }
    
    public async Task ExportToTCAsync(IEnumerable<string> commandIds, string filePath, CancellationToken cancellationToken = default)
    {
        List<InternalCommand> toExport;
        lock (_lock)
        {
            var ids = commandIds.ToHashSet();
            toExport = _commands.Where(c => ids.Contains(c.Id)).ToList();
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("; XCommander User Commands Export");
        sb.AppendLine($"; Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        
        int cmdNum = 1;
        foreach (var cmd in toExport)
        {
            sb.AppendLine($"[em_UserCmd{cmdNum:D4}]");
            sb.AppendLine($"menu={cmd.Name}");
            sb.AppendLine($"cmd={cmd.Executable}");
            if (!string.IsNullOrEmpty(cmd.Arguments)) sb.AppendLine($"param={cmd.Arguments}");
            if (!string.IsNullOrEmpty(cmd.WorkingDirectory)) sb.AppendLine($"path={cmd.WorkingDirectory}");
            if (!string.IsNullOrEmpty(cmd.KeyboardShortcut)) sb.AppendLine($"shortcut={cmd.KeyboardShortcut}");
            if (cmd.RunMinimized) sb.AppendLine("iconic=1");
            if (cmd.WaitForExit) sb.AppendLine("wait=1");
            sb.AppendLine();
            cmdNum++;
        }
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }
    
    // ======= Built-in Commands =======
    
    public IReadOnlyList<InternalCommand> GetBuiltInCommands() => _builtInCommands.AsReadOnly();
    
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock) { _commands.Clear(); _categories.Clear(); }
        await SaveCommandsAsync(cancellationToken);
        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    // ======= Private Methods =======
    
    private List<InternalCommand> CreateBuiltInCommands()
    {
        return new List<InternalCommand>
        {
            new InternalCommand
            {
                Id = "builtin_terminal",
                Name = "Open Terminal",
                Description = "Open terminal in current directory",
                Category = "System",
                Executable = OperatingSystem.IsWindows() ? "cmd" : OperatingSystem.IsMacOS() ? "open" : "xterm",
                Arguments = OperatingSystem.IsMacOS() ? "-a Terminal %P" : null,
                WorkingDirectory = "%P",
                KeyboardShortcut = "Ctrl+Shift+T",
                Scope = CommandScope.FilePanel
            },
            new InternalCommand
            {
                Id = "builtin_notepad",
                Name = "Open in Text Editor",
                Description = "Open selected file in text editor",
                Category = "Edit",
                Executable = OperatingSystem.IsWindows() ? "notepad" : OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                Arguments = OperatingSystem.IsMacOS() ? "-e %S" : "%S",
                Scope = CommandScope.FilePanel
            },
            new InternalCommand
            {
                Id = "builtin_explorer",
                Name = "Open in File Manager",
                Description = "Open current directory in system file manager",
                Category = "System",
                Executable = OperatingSystem.IsWindows() ? "explorer" : OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                Arguments = "%P",
                Scope = CommandScope.FilePanel
            },
            new InternalCommand
            {
                Id = "builtin_properties",
                Name = "File Properties",
                Description = "Show file properties",
                Category = "System",
                Executable = OperatingSystem.IsWindows() ? "powershell" : OperatingSystem.IsMacOS() ? "mdls" : "stat",
                Arguments = OperatingSystem.IsWindows() ? "-Command \"Get-ItemProperty '%S' | Format-List *\"" : "%S",
                CaptureOutput = true,
                WaitForExit = true,
                KeyboardShortcut = "Alt+Enter",
                Scope = CommandScope.FilePanel
            },
            new InternalCommand
            {
                Id = "builtin_copy_names",
                Name = "Copy File Names",
                Description = "Copy selected file names to clipboard",
                Category = "Clipboard",
                Executable = "internal",
                Arguments = "copy_names",
                Scope = CommandScope.FilePanel
            }
        };
    }
    
    private async Task LoadCommandsAsync()
    {
        try
        {
            if (File.Exists(_commandsFilePath))
            {
                var json = await File.ReadAllTextAsync(_commandsFilePath);
                var data = JsonSerializer.Deserialize<CommandsData>(json);
                if (data != null)
                {
                    lock (_lock)
                    {
                        _commands.Clear();
                        _commands.AddRange(data.Commands);
                        _categories.Clear();
                        _categories.AddRange(data.Categories);
                    }
                }
            }
        }
        catch { /* Use defaults */ }
    }
    
    private async Task SaveCommandsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_commandsFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            
            List<InternalCommand> commands;
            List<InternalCommandCategory> categories;
            lock (_lock)
            {
                commands = _commands.ToList();
                categories = _categories.ToList();
            }
            
            var data = new CommandsData { Commands = commands, Categories = categories };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_commandsFilePath, json, cancellationToken);
        }
        catch { /* Ignore */ }
    }
    
    private class CommandsData
    {
        public List<InternalCommand> Commands { get; set; } = new();
        public List<InternalCommandCategory> Categories { get; set; } = new();
    }
}
