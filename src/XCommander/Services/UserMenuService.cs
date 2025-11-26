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
        
        await Task.Run(() =>
        {
            switch (item.Command.Type)
            {
                case MenuCommandType.External:
                    ExecuteExternalCommand(item.Command, context);
                    break;
                    
                case MenuCommandType.InternalCommand:
                    ExecuteInternalCommand(item.Command, context);
                    break;
                    
                case MenuCommandType.ChangePath:
                    ExecuteChangePath(item.Command, context);
                    break;
                    
                case MenuCommandType.OpenWithDefault:
                    ExecuteOpenWithDefault(item.Command, context);
                    break;
                    
                case MenuCommandType.CommandSequence:
                    ExecuteCommandSequence(item.Command, context);
                    break;
            }
        }, cancellationToken);
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
    
    private void ExecuteExternalCommand(UserMenuCommand command, MenuExecutionContext context)
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
            process.WaitForExit();
        }
    }
    
    private void ExecuteInternalCommand(UserMenuCommand command, MenuExecutionContext context)
    {
        // Internal commands would be handled by a command dispatcher
        // This is a placeholder for the actual implementation
    }
    
    private void ExecuteChangePath(UserMenuCommand command, MenuExecutionContext context)
    {
        // Path changes would be handled by the panel view model
        // This is a placeholder for the actual implementation
    }
    
    private void ExecuteOpenWithDefault(UserMenuCommand command, MenuExecutionContext context)
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
    }
    
    private void ExecuteCommandSequence(UserMenuCommand command, MenuExecutionContext context)
    {
        // Command sequences would parse and execute multiple commands
        // This is a placeholder for the actual implementation
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
        
        return result;
    }
    
    #endregion
}
