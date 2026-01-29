using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace XCommander.Services;

/// <summary>
/// User menu service for managing custom menus and commands.
/// </summary>
public class UserMenuService : IUserMenuService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "UserMenus");
    
    private static readonly string ConfigFile = Path.Combine(ConfigDirectory, "usermenus.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    private List<UserMenu> _menus = new();
    private bool _isLoaded;
    private readonly object _lock = new();
    private readonly IInternalCommandService? _internalCommandService;
    private readonly IFileChecksumService? _fileChecksumService;
    private readonly INotificationService? _notificationService;

    public UserMenuService(
        IInternalCommandService? internalCommandService = null,
        IFileChecksumService? fileChecksumService = null,
        INotificationService? notificationService = null)
    {
        _internalCommandService = internalCommandService;
        _fileChecksumService = fileChecksumService;
        _notificationService = notificationService;
    }
    
    private static readonly List<ParameterPlaceholder> Placeholders = new()
    {
        // Path placeholders
        new ParameterPlaceholder { Placeholder = "%P", Description = "Source path (current directory)", Category = "Paths", Example = "C:\\Users\\Name\\Documents" },
        new ParameterPlaceholder { Placeholder = "%T", Description = "Target path (other panel)", Category = "Paths", Example = "D:\\Backup" },
        new ParameterPlaceholder { Placeholder = "%p", Description = "Source path in short format", Category = "Paths", Example = "C:\\Users\\Name\\Docume~1" },
        new ParameterPlaceholder { Placeholder = "%t", Description = "Target path in short format", Category = "Paths", Example = "D:\\Backup" },
        
        // File placeholders
        new ParameterPlaceholder { Placeholder = "%N", Description = "Current filename (with extension)", Category = "Files", Example = "document.txt" },
        new ParameterPlaceholder { Placeholder = "%n", Description = "Current filename in short format", Category = "Files", Example = "docume~1.txt" },
        new ParameterPlaceholder { Placeholder = "%O", Description = "Current filename without extension", Category = "Files", Example = "document" },
        new ParameterPlaceholder { Placeholder = "%E", Description = "Extension of current file", Category = "Files", Example = ".txt" },
        new ParameterPlaceholder { Placeholder = "%M", Description = "Current filename in target panel", Category = "Files", Example = "backup.txt" },
        
        // Selection placeholders
        new ParameterPlaceholder { Placeholder = "%S", Description = "Selected filenames (space-separated)", Category = "Selection", Example = "file1.txt file2.txt" },
        new ParameterPlaceholder { Placeholder = "%s", Description = "Selected filenames (short format)", Category = "Selection", Example = "file1~1.txt file2~1.txt" },
        new ParameterPlaceholder { Placeholder = "%R", Description = "Selected filenames with full path", Category = "Selection", Example = "C:\\path\\file1.txt C:\\path\\file2.txt" },
        
        // List file placeholders
        new ParameterPlaceholder { Placeholder = "%L", Description = "Long filenames list file (source)", Category = "List Files", Example = "Path to temp file with list" },
        new ParameterPlaceholder { Placeholder = "%l", Description = "Short filenames list file (source)", Category = "List Files", Example = "Path to temp file with list" },
        new ParameterPlaceholder { Placeholder = "%F", Description = "Long filenames list file (target)", Category = "List Files", Example = "Path to temp file with list" },
        new ParameterPlaceholder { Placeholder = "%f", Description = "Short filenames list file (target)", Category = "List Files", Example = "Path to temp file with list" },
        new ParameterPlaceholder { Placeholder = "%WL", Description = "Long filenames list (source) with paths", Category = "List Files", Example = "Path to temp file with full paths" },
        new ParameterPlaceholder { Placeholder = "%WF", Description = "Long filenames list (target) with paths", Category = "List Files", Example = "Path to temp file with full paths" },
        
        // Special placeholders
        new ParameterPlaceholder { Placeholder = "%?", Description = "Prompt for parameters at runtime", Category = "Special", Example = "Opens dialog for input" },
        new ParameterPlaceholder { Placeholder = "%%", Description = "Literal percent sign", Category = "Special", Example = "%" },
        new ParameterPlaceholder { Placeholder = "%X", Description = "XCommander installation directory", Category = "Special", Example = "C:\\Program Files\\XCommander" },
        new ParameterPlaceholder { Placeholder = "%D", Description = "Current date (YYYY-MM-DD)", Category = "Special", Example = "2024-01-15" },
        new ParameterPlaceholder { Placeholder = "%H", Description = "Current time (HH-MM-SS)", Category = "Special", Example = "14-30-45" },
        
        // Conditional placeholders
        new ParameterPlaceholder { Placeholder = "%Z", Description = "Selected file(s) or file under cursor", Category = "Conditional", Example = "Uses selection or current file" }
    };
    
    public async Task<IReadOnlyList<UserMenu>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        lock (_lock)
        {
            return _menus.ToList();
        }
    }
    
    public async Task<UserMenu?> GetMainMenuAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        lock (_lock)
        {
            return _menus.FirstOrDefault(m => m.IsMain) ?? _menus.FirstOrDefault();
        }
    }
    
    public async Task<UserMenu> CreateMenuAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var menu = new UserMenu
        {
            Name = name,
            IsMain = !_menus.Any()
        };
        
        lock (_lock)
        {
            _menus.Add(menu);
        }
        
        await SaveAsync(cancellationToken);
        return menu;
    }
    
    public async Task UpdateMenuAsync(UserMenu menu, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var index = _menus.FindIndex(m => m.Id == menu.Id);
            if (index >= 0)
            {
                menu.ModifiedAt = DateTime.Now;
                _menus[index] = menu;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task DeleteMenuAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            _menus.RemoveAll(m => m.Id == id);
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task<UserMenuItem> AddMenuItemAsync(string menuId, UserMenuItem item, string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var menu = _menus.FirstOrDefault(m => m.Id == menuId);
            if (menu != null)
            {
                if (parentId == null)
                {
                    menu.Items.Add(item);
                    UpdateItemOrders(menu.Items);
                }
                else
                {
                    var parent = FindMenuItem(menu.Items, parentId);
                    if (parent != null)
                    {
                        parent.SubItems ??= new List<UserMenuItem>();
                        parent.SubItems.Add(item);
                        UpdateItemOrders(parent.SubItems);
                    }
                }
                
                menu.ModifiedAt = DateTime.Now;
            }
        }
        
        await SaveAsync(cancellationToken);
        return item;
    }
    
    public async Task UpdateMenuItemAsync(string menuId, UserMenuItem item, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var menu = _menus.FirstOrDefault(m => m.Id == menuId);
            if (menu != null)
            {
                UpdateMenuItemRecursive(menu.Items, item);
                menu.ModifiedAt = DateTime.Now;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task RemoveMenuItemAsync(string menuId, string itemId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var menu = _menus.FirstOrDefault(m => m.Id == menuId);
            if (menu != null)
            {
                RemoveMenuItemRecursive(menu.Items, itemId);
                menu.ModifiedAt = DateTime.Now;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task MoveMenuItemAsync(string menuId, string itemId, string? newParentId, int position,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var menu = _menus.FirstOrDefault(m => m.Id == menuId);
            if (menu == null) return;
            
            // Find and remove the item
            var item = FindMenuItem(menu.Items, itemId);
            if (item == null) return;
            
            RemoveMenuItemRecursive(menu.Items, itemId);
            
            // Add to new location
            if (newParentId == null)
            {
                position = Math.Clamp(position, 0, menu.Items.Count);
                menu.Items.Insert(position, item);
                UpdateItemOrders(menu.Items);
            }
            else
            {
                var newParent = FindMenuItem(menu.Items, newParentId);
                if (newParent != null)
                {
                    newParent.SubItems ??= new List<UserMenuItem>();
                    position = Math.Clamp(position, 0, newParent.SubItems.Count);
                    newParent.SubItems.Insert(position, item);
                    UpdateItemOrders(newParent.SubItems);
                }
            }
            
            menu.ModifiedAt = DateTime.Now;
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task ExecuteMenuItemAsync(UserMenuItem item, MenuExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (item.Command == null) return;

        switch (item.Command.Type)
        {
            case MenuCommandType.External:
                await ExecuteExternalCommandAsync(item.Command, context, cancellationToken);
                break;

            case MenuCommandType.InternalCommand:
                await ExecuteInternalCommandAsync(item.Command, context, cancellationToken);
                break;

            case MenuCommandType.ChangePath:
                await ExecuteChangePathAsync(item.Command, context, cancellationToken);
                break;

            case MenuCommandType.OpenWithDefault:
                await ExecuteOpenWithDefaultAsync(item.Command, context, cancellationToken);
                break;

            case MenuCommandType.CommandSequence:
                await ExecuteCommandSequenceAsync(item.Command, context, cancellationToken);
                break;
        }
    }
    
    public async Task<UserMenu> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var imported = JsonSerializer.Deserialize<UserMenu>(json, JsonOptions)
                   ?? throw new InvalidDataException("Invalid user menu file");
        
        // Create new menu with new ID to avoid conflicts
        var menu = new UserMenu
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = imported.Name,
            Description = imported.Description,
            IsMain = false,
            KeyboardShortcut = imported.KeyboardShortcut,
            Items = imported.Items,
            ModifiedAt = DateTime.Now
        };
        
        lock (_lock)
        {
            _menus.Add(menu);
        }
        
        await SaveAsync(cancellationToken);
        return menu;
    }
    
    public async Task ExportAsync(string menuId, string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        UserMenu? menu;
        lock (_lock)
        {
            menu = _menus.FirstOrDefault(m => m.Id == menuId);
        }
        
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu not found: {menuId}");
        }
        
        var json = JsonSerializer.Serialize(menu, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public IReadOnlyList<ParameterPlaceholder> GetParameterPlaceholders()
    {
        return Placeholders;
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
                var loaded = JsonSerializer.Deserialize<List<UserMenu>>(json, JsonOptions);
                if (loaded != null)
                {
                    lock (_lock)
                    {
                        _menus = loaded;
                    }
                }
            }
            catch
            {
                // Ignore load errors, use empty list
            }
        }
        
        // Create default menu if none exist
        lock (_lock)
        {
            if (_menus.Count == 0)
            {
                _menus.Add(CreateDefaultMenu());
            }
            _isLoaded = true;
        }
    }
    
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        List<UserMenu> toSave;
        lock (_lock)
        {
            toSave = _menus.ToList();
        }
        
        var json = JsonSerializer.Serialize(toSave, JsonOptions);
        await File.WriteAllTextAsync(ConfigFile, json, cancellationToken);
    }
    
    private static UserMenu CreateDefaultMenu()
    {
        return new UserMenu
        {
            Name = "User Menu",
            IsMain = true,
            KeyboardShortcut = "F2",
            Items = new List<UserMenuItem>
            {
                new UserMenuItem
                {
                    Label = "Open Terminal Here",
                    Icon = "💻",
                    Order = 0,
                    Command = new UserMenuCommand
                    {
                        Type = MenuCommandType.External,
                        CommandLine = OperatingSystem.IsWindows() ? "cmd.exe" : 
                                      OperatingSystem.IsMacOS() ? "/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal" : 
                                      "x-terminal-emulator",
                        WorkingDirectory = "%P"
                    }
                },
                new UserMenuItem { Type = MenuItemType.Separator, Order = 1 },
                new UserMenuItem
                {
                    Label = "Open with Code",
                    Icon = "📝",
                    Order = 2,
                    Command = new UserMenuCommand
                    {
                        Type = MenuCommandType.External,
                        CommandLine = "code",
                        Parameters = "%P"
                    }
                },
                new UserMenuItem
                {
                    Label = "Copy Path to Clipboard",
                    Icon = "📋",
                    Order = 3,
                    Command = new UserMenuCommand
                    {
                        Type = MenuCommandType.InternalCommand,
                        InternalAction = "clipboard.copyPath"
                    }
                },
                new UserMenuItem { Type = MenuItemType.Separator, Order = 4 },
                new UserMenuItem
                {
                    Label = "Git",
                    Icon = "🔀",
                    Type = MenuItemType.SubMenu,
                    Order = 5,
                    SubItems = new List<UserMenuItem>
                    {
                        new UserMenuItem
                        {
                            Label = "Git Status",
                            Order = 0,
                            Command = new UserMenuCommand
                            {
                                Type = MenuCommandType.External,
                                CommandLine = "git",
                                Parameters = "status",
                                WorkingDirectory = "%P",
                                WaitForFinish = true
                            }
                        },
                        new UserMenuItem
                        {
                            Label = "Git Pull",
                            Order = 1,
                            Command = new UserMenuCommand
                            {
                                Type = MenuCommandType.External,
                                CommandLine = "git",
                                Parameters = "pull",
                                WorkingDirectory = "%P",
                                WaitForFinish = true
                            }
                        },
                        new UserMenuItem
                        {
                            Label = "Git Log",
                            Order = 2,
                            Command = new UserMenuCommand
                            {
                                Type = MenuCommandType.External,
                                CommandLine = "git",
                                Parameters = "log --oneline -20",
                                WorkingDirectory = "%P",
                                WaitForFinish = true
                            }
                        }
                    }
                },
                new UserMenuItem
                {
                    Label = "File Operations",
                    Icon = "📁",
                    Type = MenuItemType.SubMenu,
                    Order = 6,
                    SubItems = new List<UserMenuItem>
                    {
                        new UserMenuItem
                        {
                            Label = "Calculate MD5",
                            Order = 0,
                            Command = new UserMenuCommand
                            {
                                Type = MenuCommandType.InternalCommand,
                                InternalAction = "file.calculateMd5"
                            }
                        },
                        new UserMenuItem
                        {
                            Label = "Compare Files",
                            Order = 1,
                            Command = new UserMenuCommand
                            {
                                Type = MenuCommandType.InternalCommand,
                                InternalAction = "file.compare"
                            }
                        }
                    }
                }
            }
        };
    }
    
    private static UserMenuItem? FindMenuItem(List<UserMenuItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            
            if (item.SubItems != null)
            {
                var found = FindMenuItem(item.SubItems, id);
                if (found != null) return found;
            }
        }
        
        return null;
    }
    
    private static void UpdateMenuItemRecursive(List<UserMenuItem> items, UserMenuItem updated)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id == updated.Id)
            {
                items[i] = updated;
                return;
            }
            
            if (items[i].SubItems != null)
            {
                UpdateMenuItemRecursive(items[i].SubItems!, updated);
            }
        }
    }
    
    private static bool RemoveMenuItemRecursive(List<UserMenuItem> items, string id)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id == id)
            {
                items.RemoveAt(i);
                UpdateItemOrders(items);
                return true;
            }
            
            if (items[i].SubItems != null && RemoveMenuItemRecursive(items[i].SubItems!, id))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static void UpdateItemOrders(List<UserMenuItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
    }
    
    private async Task ExecuteExternalCommandAsync(
        UserMenuCommand command,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.CommandLine)) return;

        var commandLine = SubstituteParameters(command.CommandLine, context);
        var parameters = SubstituteParameters(command.Parameters ?? "", context);
        var workingDir = SubstituteParameters(command.WorkingDirectory ?? context.SourcePath ?? "", context);

        var processInfo = new ProcessStartInfo
        {
            FileName = commandLine,
            Arguments = parameters,
            WorkingDirectory = workingDir,
            UseShellExecute = true
        };

        if (command.RunAsAdmin && OperatingSystem.IsWindows())
        {
            processInfo.Verb = "runas";
        }

        var process = Process.Start(processInfo);

        if (command.WaitForFinish && process != null)
        {
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    private async Task ExecuteInternalCommandAsync(
        UserMenuCommand command,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        var action = command.InternalAction?.Trim();
        if (string.IsNullOrEmpty(action)) return;

        if (context.InternalCommandHandlerAsync != null)
        {
            await context.InternalCommandHandlerAsync(action);
            return;
        }

        if (_internalCommandService != null)
        {
            var resolved = action.StartsWith("internal:", StringComparison.OrdinalIgnoreCase)
                ? action.Substring("internal:".Length)
                : action;
            var internalCommand = _internalCommandService.GetCommand(resolved);
            if (internalCommand != null)
            {
                var execContext = BuildCommandExecutionContext(context);
                await _internalCommandService.ExecuteAsync(internalCommand.Id, execContext, cancellationToken: cancellationToken);
                return;
            }
        }

        await ExecuteBuiltInInternalActionAsync(action, context, cancellationToken);
    }

    private async Task ExecuteChangePathAsync(
        UserMenuCommand command,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = SubstituteParameters(command.CommandLine ?? command.Parameters ?? "", context);
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!Path.IsPathRooted(path) && !path.Contains("://", StringComparison.Ordinal))
        {
            var basePath = context.SourcePath ?? Environment.CurrentDirectory;
            path = Path.GetFullPath(Path.Combine(basePath, path));
        }

        var target = command.InternalAction?.Trim().ToLowerInvariant();
        if (target == "source" || target == "left")
        {
            if (context.ChangeSourcePathAsync != null)
                await context.ChangeSourcePathAsync(path);
            return;
        }

        if (target == "target" || target == "right")
        {
            if (context.ChangeTargetPathAsync != null)
                await context.ChangeTargetPathAsync(path);
            return;
        }

        if (context.ChangeActivePathAsync != null)
        {
            await context.ChangeActivePathAsync(path);
            return;
        }

        if (context.ChangeSourcePathAsync != null)
        {
            await context.ChangeSourcePathAsync(path);
            return;
        }

        if (context.ChangeTargetPathAsync != null)
        {
            await context.ChangeTargetPathAsync(path);
        }
    }

    private Task ExecuteOpenWithDefaultAsync(
        UserMenuCommand command,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        var filePath = context.CurrentFileName != null
            ? Path.Combine(context.SourcePath ?? "", context.CurrentFileName)
            : null;

        if (filePath != null && File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteCommandSequenceAsync(
        UserMenuCommand command,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sequence = command.CommandLine ?? command.Parameters ?? "";
        if (string.IsNullOrWhiteSpace(sequence)) return;

        var steps = SplitCommandSequence(sequence);
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolved = SubstituteParameters(step, context).Trim();
            if (string.IsNullOrWhiteSpace(resolved))
                continue;

            if (resolved.StartsWith("internal:", StringComparison.OrdinalIgnoreCase))
            {
                var action = resolved.Substring("internal:".Length).Trim();
                await ExecuteInternalCommandAsync(new UserMenuCommand
                {
                    Type = MenuCommandType.InternalCommand,
                    InternalAction = action
                }, context, cancellationToken);
                continue;
            }

            if (resolved.StartsWith("cd:", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
            {
                var pathValue = resolved.Contains(':') ? resolved.Substring(resolved.IndexOf(':') + 1).Trim() : "";
                await ExecuteChangePathAsync(new UserMenuCommand
                {
                    Type = MenuCommandType.ChangePath,
                    CommandLine = pathValue,
                    InternalAction = command.InternalAction
                }, context, cancellationToken);
                continue;
            }

            if (resolved.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
            {
                var pathValue = resolved.Substring("open:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(pathValue))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pathValue,
                        UseShellExecute = true
                    });
                }
                else
                {
                    await ExecuteOpenWithDefaultAsync(command, context, cancellationToken);
                }
                continue;
            }

            var (fileName, args) = SplitCommandLine(resolved);
            await ExecuteExternalCommandAsync(new UserMenuCommand
            {
                Type = MenuCommandType.External,
                CommandLine = fileName,
                Parameters = args,
                WorkingDirectory = command.WorkingDirectory,
                RunAsAdmin = command.RunAsAdmin,
                WaitForFinish = command.WaitForFinish
            }, context, cancellationToken);
        }
    }

    private static CommandExecutionContext BuildCommandExecutionContext(MenuExecutionContext context)
    {
        var selected = ResolveSelectedPaths(context);
        var currentExtension = context.CurrentExtension;
        if (string.IsNullOrEmpty(currentExtension) && !string.IsNullOrEmpty(context.CurrentFileName))
            currentExtension = Path.GetExtension(context.CurrentFileName);

        return new CommandExecutionContext
        {
            SourcePath = context.SourcePath,
            TargetPath = context.TargetPath,
            CurrentFileName = context.CurrentFileName,
            CurrentFileExtension = currentExtension,
            SelectedFiles = selected,
            Variables = context.Variables != null
                ? new Dictionary<string, string>(context.Variables)
                : new Dictionary<string, string>()
        };
    }

    private async Task ExecuteBuiltInInternalActionAsync(
        string action,
        MenuExecutionContext context,
        CancellationToken cancellationToken)
    {
        var normalized = action.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "clipboard.copypath":
                await CopyPathToClipboardAsync(context);
                break;

            case "file.calculatemd5":
                await CalculateMd5Async(context, cancellationToken);
                break;

            case "file.compare":
                await CompareFilesAsync(context, cancellationToken);
                break;
        }
    }

    private async Task CopyPathToClipboardAsync(MenuExecutionContext context)
    {
        try
        {
            var selected = ResolveSelectedPaths(context);
            var text = selected.Count > 0
                ? string.Join(Environment.NewLine, selected)
                : (context.SourcePath ?? string.Empty);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                    return;
                }
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private async Task CalculateMd5Async(MenuExecutionContext context, CancellationToken cancellationToken)
    {
        if (_fileChecksumService == null) return;

        var target = ResolvePrimaryFilePath(context);
        if (string.IsNullOrEmpty(target))
        {
            _notificationService?.ShowWarning("No file selected.", "MD5");
            return;
        }

        var result = await _fileChecksumService.CalculateChecksumAsync(target, ChecksumAlgorithm.MD5, cancellationToken);
        if (result.Success)
        {
            _notificationService?.ShowInfo($"{Path.GetFileName(target)}: {result.Hash}", "MD5");
        }
        else
        {
            _notificationService?.ShowError(result.ErrorMessage ?? "Failed to calculate MD5.", "MD5");
        }
    }

    private async Task CompareFilesAsync(MenuExecutionContext context, CancellationToken cancellationToken)
    {
        if (_fileChecksumService == null) return;

        var selected = ResolveSelectedPaths(context);
        if (selected.Count < 2)
        {
            _notificationService?.ShowWarning("Select two files to compare.", "Compare");
            return;
        }

        var match = await _fileChecksumService.CompareFilesAsync(selected[0], selected[1], ChecksumAlgorithm.SHA256, cancellationToken);
        var message = match ? "Files match." : "Files differ.";
        _notificationService?.ShowInfo(message, "Compare");
    }

    private static IReadOnlyList<string> ResolveSelectedPaths(MenuExecutionContext context)
    {
        if (context.SelectedFilesSource != null && context.SelectedFilesSource.Count > 0)
            return NormalizePaths(context.SelectedFilesSource, context.SourcePath);
        if (context.SelectedFiles != null && context.SelectedFiles.Count > 0)
            return NormalizePaths(context.SelectedFiles, context.SourcePath);

        if (!string.IsNullOrEmpty(context.CurrentFileName) && !string.IsNullOrEmpty(context.SourcePath))
        {
            return new[] { Path.Combine(context.SourcePath, context.CurrentFileName) };
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> NormalizePaths(IReadOnlyList<string> paths, string? basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return paths.ToList();

        return paths.Select(p => Path.IsPathRooted(p) ? p : Path.Combine(basePath, p)).ToList();
    }

    private static string? ResolvePrimaryFilePath(MenuExecutionContext context)
    {
        var selected = ResolveSelectedPaths(context);
        return selected.Count > 0 ? selected[0] : null;
    }

    private static IReadOnlyList<string> SplitCommandSequence(string sequence)
    {
        var steps = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in sequence)
        {
            if (ch == '"' && (current.Length == 0 || current[^1] != '\\'))
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes && (ch == ';' || ch == '\n' || ch == '\r'))
            {
                var entry = current.ToString().Trim();
                if (!string.IsNullOrEmpty(entry))
                    steps.Add(entry);
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            steps.Add(last);

        return steps;
    }

    private static (string FileName, string Arguments) SplitCommandLine(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return (string.Empty, string.Empty);

        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                var fileName = trimmed.Substring(1, endQuote - 1);
                var args = trimmed.Substring(endQuote + 1).TrimStart();
                return (fileName, args);
            }
        }

        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
            return (trimmed, string.Empty);

        return (trimmed.Substring(0, firstSpace), trimmed.Substring(firstSpace + 1).TrimStart());
    }
    
    private static string SubstituteParameters(string input, MenuExecutionContext context)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = input
            // Path placeholders
            .Replace("%P", context.SourcePath ?? "")
            .Replace("%T", context.TargetPath ?? "")
            
            // File placeholders
            .Replace("%N", context.CurrentFileName ?? "")
            .Replace("%O", context.CurrentFileNameNoExt ?? "")
            .Replace("%E", context.CurrentExtension ?? "")
            
            // Selection placeholders
            .Replace("%S", context.SelectedFiles != null 
                ? string.Join(" ", context.SelectedFiles.Select(f => $"\"{f}\""))
                : "")
            .Replace("%R", context.SelectedFilesSource != null
                ? string.Join(" ", context.SelectedFilesSource.Select(f => $"\"{f}\""))
                : "")
            
            // List file placeholders
            .Replace("%L", context.SourceListFile ?? "")
            .Replace("%F", context.TargetListFile ?? "")
            
            // Special placeholders
            .Replace("%%", "%")
            .Replace("%D", DateTime.Now.ToString("yyyy-MM-dd"))
            .Replace("%H", DateTime.Now.ToString("HH-mm-ss"))
            .Replace("%X", AppContext.BaseDirectory);
        
        // Conditional placeholder %Z - use selection or current file
        if (context.SelectedFiles?.Count > 0)
        {
            result = result.Replace("%Z", string.Join(" ", context.SelectedFiles.Select(f => $"\"{f}\"")));
        }
        else
        {
            result = result.Replace("%Z", context.CurrentFileName != null ? $"\"{context.CurrentFileName}\"" : "");
        }

        if (context.Variables != null)
        {
            foreach (var variable in context.Variables)
            {
                result = result.Replace($"%{variable.Key}%", variable.Value);
            }
        }
        
        return result;
    }
    
    #endregion
}
