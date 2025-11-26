// MenuConfigService.cs - TC-style customizable menu configuration implementation
// Full menu, hotkey, user command, and button bar customization

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public sealed class MenuConfigService : IMenuConfigService
{
    private readonly ConcurrentDictionary<string, MenuDefinition> _menus = new();
    private readonly ConcurrentDictionary<string, HotkeyDefinition> _hotkeys = new();
    private readonly ConcurrentDictionary<string, UserCommand> _userCommands = new();
    private readonly ConcurrentDictionary<string, ButtonBarDefinition> _buttonBars = new();
    private readonly string _configDir;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _initialized;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public event EventHandler<MenuChangedEventArgs>? MenuChanged;
    public event EventHandler<HotkeyChangedEventArgs>? HotkeyChanged;
    public event EventHandler<ButtonBarChangedEventArgs>? ButtonBarChanged;
    
    public MenuConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander",
            "config");
    }
    
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            
            Directory.CreateDirectory(_configDir);
            await LoadConfigurationAsync(cancellationToken);
            await EnsureDefaultsAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _saveLock.Release();
        }
    }
    
    private async Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        var menusFile = Path.Combine(_configDir, "menus.json");
        if (File.Exists(menusFile))
        {
            var json = await File.ReadAllTextAsync(menusFile, cancellationToken);
            var menus = JsonSerializer.Deserialize<List<MenuDefinition>>(json, JsonOptions);
            if (menus != null)
            {
                foreach (var menu in menus)
                {
                    _menus[menu.Id] = menu;
                }
            }
        }
        
        var hotkeysFile = Path.Combine(_configDir, "hotkeys.json");
        if (File.Exists(hotkeysFile))
        {
            var json = await File.ReadAllTextAsync(hotkeysFile, cancellationToken);
            var hotkeys = JsonSerializer.Deserialize<List<HotkeyDefinition>>(json, JsonOptions);
            if (hotkeys != null)
            {
                foreach (var hk in hotkeys)
                {
                    _hotkeys[hk.CommandName] = hk;
                }
            }
        }
        
        var userCmdsFile = Path.Combine(_configDir, "usercommands.json");
        if (File.Exists(userCmdsFile))
        {
            var json = await File.ReadAllTextAsync(userCmdsFile, cancellationToken);
            var commands = JsonSerializer.Deserialize<List<UserCommand>>(json, JsonOptions);
            if (commands != null)
            {
                foreach (var cmd in commands)
                {
                    _userCommands[cmd.Id] = cmd;
                }
            }
        }
        
        var buttonBarsFile = Path.Combine(_configDir, "buttonbars.json");
        if (File.Exists(buttonBarsFile))
        {
            var json = await File.ReadAllTextAsync(buttonBarsFile, cancellationToken);
            var bars = JsonSerializer.Deserialize<List<ButtonBarDefinition>>(json, JsonOptions);
            if (bars != null)
            {
                foreach (var bar in bars)
                {
                    _buttonBars[bar.Id] = bar;
                }
            }
        }
    }
    
    private async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            var menusFile = Path.Combine(_configDir, "menus.json");
            var menusJson = JsonSerializer.Serialize(_menus.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(menusFile, menusJson, cancellationToken);
            
            var hotkeysFile = Path.Combine(_configDir, "hotkeys.json");
            var hotkeysJson = JsonSerializer.Serialize(_hotkeys.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(hotkeysFile, hotkeysJson, cancellationToken);
            
            var userCmdsFile = Path.Combine(_configDir, "usercommands.json");
            var userCmdsJson = JsonSerializer.Serialize(_userCommands.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(userCmdsFile, userCmdsJson, cancellationToken);
            
            var buttonBarsFile = Path.Combine(_configDir, "buttonbars.json");
            var buttonBarsJson = JsonSerializer.Serialize(_buttonBars.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(buttonBarsFile, buttonBarsJson, cancellationToken);
        }
        finally
        {
            _saveLock.Release();
        }
    }
    
    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        // Ensure default main menu exists
        if (!_menus.ContainsKey("main"))
        {
            var mainMenu = await CreateDefaultMenuAsync(MenuType.MainMenu, cancellationToken);
            _menus[mainMenu.Id] = mainMenu;
        }
        
        // Ensure default context menu exists
        if (!_menus.ContainsKey("context"))
        {
            var contextMenu = await CreateDefaultMenuAsync(MenuType.ContextMenu, cancellationToken);
            _menus[contextMenu.Id] = contextMenu;
        }
        
        // Ensure default button bar exists
        if (!_buttonBars.Any())
        {
            var defaultBar = await CreateDefaultButtonBarAsync(cancellationToken);
            _buttonBars[defaultBar.Id] = defaultBar;
        }
        
        // Ensure default hotkeys exist
        if (!_hotkeys.Any())
        {
            await ResetHotkeysToDefaultAsync(cancellationToken);
        }
    }
    
    #region Menu Management
    
    public async Task<IReadOnlyList<MenuDefinition>> GetAllMenusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _menus.Values.ToList();
    }
    
    public async Task<MenuDefinition?> GetMenuAsync(string menuId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _menus.TryGetValue(menuId, out var menu) ? menu : null;
    }
    
    public async Task SaveMenuAsync(MenuDefinition menu, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var isNew = !_menus.ContainsKey(menu.Id);
        _menus[menu.Id] = menu with { LastModified = DateTime.Now };
        await SaveConfigurationAsync(cancellationToken);
        MenuChanged?.Invoke(this, new MenuChangedEventArgs(menu.Id, isNew ? MenuChangeType.Created : MenuChangeType.Updated));
    }
    
    public async Task DeleteMenuAsync(string menuId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_menus.TryRemove(menuId, out _))
        {
            await SaveConfigurationAsync(cancellationToken);
            MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.Deleted));
        }
    }
    
    public Task<MenuDefinition> CreateDefaultMenuAsync(MenuType type, CancellationToken cancellationToken = default)
    {
        var menu = type switch
        {
            MenuType.MainMenu => CreateDefaultMainMenu(),
            MenuType.ContextMenu => CreateDefaultContextMenu(),
            MenuType.HotlistMenu => CreateDefaultHotlistMenu(),
            MenuType.HistoryMenu => CreateDefaultHistoryMenu(),
            _ => new MenuDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{type} Menu",
                Type = type,
                Items = new List<MenuItem>()
            }
        };
        return Task.FromResult(menu);
    }
    
    private static MenuDefinition CreateDefaultMainMenu()
    {
        return new MenuDefinition
        {
            Id = "main",
            Name = "Main Menu",
            Type = MenuType.MainMenu,
            Items = new List<MenuItem>
            {
                new() { Id = "file", Name = "File", IsSubmenu = true, Order = 0, Children = new List<MenuItem>
                {
                    new() { Id = "file_open", Name = "Open", CommandName = "cm_Open", Shortcut = "Enter", Order = 0 },
                    new() { Id = "file_view", Name = "View", CommandName = "cm_View", Shortcut = "F3", Order = 1 },
                    new() { Id = "file_edit", Name = "Edit", CommandName = "cm_Edit", Shortcut = "F4", Order = 2 },
                    new() { Id = "file_copy", Name = "Copy", CommandName = "cm_Copy", Shortcut = "F5", Order = 3 },
                    new() { Id = "file_move", Name = "Move", CommandName = "cm_Move", Shortcut = "F6", Order = 4 },
                    new() { Id = "file_mkdir", Name = "New Folder", CommandName = "cm_MkDir", Shortcut = "F7", Order = 5 },
                    new() { Id = "file_delete", Name = "Delete", CommandName = "cm_Delete", Shortcut = "F8", Order = 6 },
                    new() { Id = "file_sep1", Name = "", IsSeparator = true, Order = 7 },
                    new() { Id = "file_quit", Name = "Quit", CommandName = "cm_Exit", Shortcut = "Alt+F4", Order = 8 }
                }},
                new() { Id = "mark", Name = "Mark", IsSubmenu = true, Order = 1, Children = new List<MenuItem>
                {
                    new() { Id = "mark_group", Name = "Select Group", CommandName = "cm_SelectGroup", Shortcut = "Num+", Order = 0 },
                    new() { Id = "mark_ungroup", Name = "Unselect Group", CommandName = "cm_UnselectGroup", Shortcut = "Num-", Order = 1 },
                    new() { Id = "mark_all", Name = "Select All", CommandName = "cm_SelectAll", Shortcut = "Ctrl+A", Order = 2 },
                    new() { Id = "mark_none", Name = "Unselect All", CommandName = "cm_UnselectAll", Shortcut = "Ctrl+Shift+A", Order = 3 },
                    new() { Id = "mark_invert", Name = "Invert Selection", CommandName = "cm_InvertSelection", Shortcut = "Num*", Order = 4 },
                    new() { Id = "mark_sep1", Name = "", IsSeparator = true, Order = 5 },
                    new() { Id = "mark_compare", Name = "Compare Directories", CommandName = "cm_CompareDirs", Order = 6 },
                    new() { Id = "mark_same", Name = "Mark Same", CommandName = "cm_MarkSame", Order = 7 },
                    new() { Id = "mark_newer", Name = "Mark Newer", CommandName = "cm_MarkNewer", Order = 8 }
                }},
                new() { Id = "commands", Name = "Commands", IsSubmenu = true, Order = 2, Children = new List<MenuItem>
                {
                    new() { Id = "cmd_search", Name = "Search", CommandName = "cm_SearchFiles", Shortcut = "Alt+F7", Order = 0 },
                    new() { Id = "cmd_multirename", Name = "Multi-Rename Tool", CommandName = "cm_MultiRename", Shortcut = "Ctrl+M", Order = 1 },
                    new() { Id = "cmd_syncdir", Name = "Synchronize Directories", CommandName = "cm_SyncDirs", Order = 2 },
                    new() { Id = "cmd_sep1", Name = "", IsSeparator = true, Order = 3 },
                    new() { Id = "cmd_ftp", Name = "FTP Connect", CommandName = "cm_FtpConnect", Shortcut = "Ctrl+F", Order = 4 },
                    new() { Id = "cmd_disconnect", Name = "Disconnect", CommandName = "cm_FtpDisconnect", Shortcut = "Ctrl+Shift+F", Order = 5 },
                    new() { Id = "cmd_sep2", Name = "", IsSeparator = true, Order = 6 },
                    new() { Id = "cmd_terminal", Name = "Open Terminal", CommandName = "cm_OpenTerminal", Order = 7 }
                }},
                new() { Id = "show", Name = "Show", IsSubmenu = true, Order = 3, Children = new List<MenuItem>
                {
                    new() { Id = "show_brief", Name = "Brief", CommandName = "cm_Brief", Shortcut = "Ctrl+F1", Order = 0 },
                    new() { Id = "show_full", Name = "Full", CommandName = "cm_Full", Shortcut = "Ctrl+F2", Order = 1 },
                    new() { Id = "show_tree", Name = "Tree", CommandName = "cm_Tree", Order = 2 },
                    new() { Id = "show_sep1", Name = "", IsSeparator = true, Order = 3 },
                    new() { Id = "show_hidden", Name = "Show Hidden Files", CommandName = "cm_ShowHidden", Shortcut = "Ctrl+H", Order = 4 },
                    new() { Id = "show_sort", Name = "Sort By...", CommandName = "cm_SortBy", Order = 5 },
                    new() { Id = "show_sep2", Name = "", IsSeparator = true, Order = 6 },
                    new() { Id = "show_refresh", Name = "Refresh", CommandName = "cm_Refresh", Shortcut = "Ctrl+R", Order = 7 }
                }},
                new() { Id = "config", Name = "Configuration", IsSubmenu = true, Order = 4, Children = new List<MenuItem>
                {
                    new() { Id = "cfg_options", Name = "Options", CommandName = "cm_Options", Order = 0 },
                    new() { Id = "cfg_layout", Name = "Layout", CommandName = "cm_Layout", Order = 1 },
                    new() { Id = "cfg_colors", Name = "Colors", CommandName = "cm_Colors", Order = 2 },
                    new() { Id = "cfg_sep1", Name = "", IsSeparator = true, Order = 3 },
                    new() { Id = "cfg_hotkeys", Name = "Hotkeys", CommandName = "cm_ConfigHotkeys", Order = 4 },
                    new() { Id = "cfg_toolbar", Name = "Button Bar", CommandName = "cm_ConfigButtonBar", Order = 5 }
                }},
                new() { Id = "help", Name = "Help", IsSubmenu = true, Order = 5, Children = new List<MenuItem>
                {
                    new() { Id = "help_contents", Name = "Help Contents", CommandName = "cm_Help", Shortcut = "F1", Order = 0 },
                    new() { Id = "help_sep1", Name = "", IsSeparator = true, Order = 1 },
                    new() { Id = "help_about", Name = "About", CommandName = "cm_About", Order = 2 }
                }}
            }
        };
    }
    
    private static MenuDefinition CreateDefaultContextMenu()
    {
        return new MenuDefinition
        {
            Id = "context",
            Name = "Context Menu",
            Type = MenuType.ContextMenu,
            Items = new List<MenuItem>
            {
                new() { Id = "ctx_open", Name = "Open", CommandName = "cm_Open", Order = 0 },
                new() { Id = "ctx_openwith", Name = "Open With...", CommandName = "cm_OpenWith", Order = 1 },
                new() { Id = "ctx_sep1", Name = "", IsSeparator = true, Order = 2 },
                new() { Id = "ctx_view", Name = "View", CommandName = "cm_View", Shortcut = "F3", Order = 3 },
                new() { Id = "ctx_edit", Name = "Edit", CommandName = "cm_Edit", Shortcut = "F4", Order = 4 },
                new() { Id = "ctx_sep2", Name = "", IsSeparator = true, Order = 5 },
                new() { Id = "ctx_copy", Name = "Copy", CommandName = "cm_Copy", Shortcut = "F5", Order = 6 },
                new() { Id = "ctx_move", Name = "Move", CommandName = "cm_Move", Shortcut = "F6", Order = 7 },
                new() { Id = "ctx_rename", Name = "Rename", CommandName = "cm_Rename", Shortcut = "Shift+F6", Order = 8 },
                new() { Id = "ctx_delete", Name = "Delete", CommandName = "cm_Delete", Shortcut = "F8", Order = 9 },
                new() { Id = "ctx_sep3", Name = "", IsSeparator = true, Order = 10 },
                new() { Id = "ctx_properties", Name = "Properties", CommandName = "cm_Properties", Shortcut = "Alt+Enter", Order = 11 },
                new() { Id = "ctx_sep4", Name = "", IsSeparator = true, Order = 12 },
                new() { Id = "ctx_copypath", Name = "Copy Path to Clipboard", CommandName = "cm_CopyPathToClipboard", Order = 13 },
                new() { Id = "ctx_copyname", Name = "Copy Filename to Clipboard", CommandName = "cm_CopyNameToClipboard", Order = 14 }
            }
        };
    }
    
    private static MenuDefinition CreateDefaultHotlistMenu()
    {
        return new MenuDefinition
        {
            Id = "hotlist",
            Name = "Directory Hotlist",
            Type = MenuType.HotlistMenu,
            Items = new List<MenuItem>
            {
                new() { Id = "hl_add", Name = "Add Current Directory", CommandName = "cm_AddToHotlist", Order = 0 },
                new() { Id = "hl_config", Name = "Configure Hotlist", CommandName = "cm_ConfigHotlist", Order = 1 },
                new() { Id = "hl_sep1", Name = "", IsSeparator = true, Order = 2 }
                // User-defined entries will be added here
            }
        };
    }
    
    private static MenuDefinition CreateDefaultHistoryMenu()
    {
        return new MenuDefinition
        {
            Id = "history",
            Name = "History",
            Type = MenuType.HistoryMenu,
            Items = new List<MenuItem>() // Populated dynamically from history
        };
    }
    
    public async Task ResetMenuToDefaultAsync(string menuId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_menus.TryGetValue(menuId, out var existingMenu))
        {
            var defaultMenu = await CreateDefaultMenuAsync(existingMenu.Type, cancellationToken);
            _menus[menuId] = defaultMenu with { Id = menuId };
            await SaveConfigurationAsync(cancellationToken);
            MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.Reset));
        }
    }
    
    #endregion
    
    #region Menu Item Management
    
    public async Task AddMenuItemAsync(string menuId, MenuItem item, int? position = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_menus.TryGetValue(menuId, out var menu))
        {
            var items = new List<MenuItem>(menu.Items);
            if (position.HasValue && position.Value >= 0 && position.Value < items.Count)
            {
                items.Insert(position.Value, item);
            }
            else
            {
                items.Add(item);
            }
            
            // Reorder
            for (int i = 0; i < items.Count; i++)
            {
                items[i] = items[i] with { Order = i };
            }
            
            _menus[menuId] = menu with { Items = items, LastModified = DateTime.Now };
            await SaveConfigurationAsync(cancellationToken);
            MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.ItemAdded, item));
        }
    }
    
    public async Task UpdateMenuItemAsync(string menuId, string itemId, MenuItem item, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_menus.TryGetValue(menuId, out var menu))
        {
            var items = new List<MenuItem>(menu.Items);
            var index = items.FindIndex(i => i.Id == itemId);
            if (index >= 0)
            {
                items[index] = item with { Id = itemId, Order = items[index].Order };
                _menus[menuId] = menu with { Items = items, LastModified = DateTime.Now };
                await SaveConfigurationAsync(cancellationToken);
                MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.Updated, item));
            }
        }
    }
    
    public async Task RemoveMenuItemAsync(string menuId, string itemId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_menus.TryGetValue(menuId, out var menu))
        {
            var items = menu.Items.Where(i => i.Id != itemId).ToList();
            // Reorder
            for (int i = 0; i < items.Count; i++)
            {
                items[i] = items[i] with { Order = i };
            }
            
            _menus[menuId] = menu with { Items = items, LastModified = DateTime.Now };
            await SaveConfigurationAsync(cancellationToken);
            MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.ItemRemoved));
        }
    }
    
    public async Task MoveMenuItemAsync(string menuId, string itemId, int newPosition, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_menus.TryGetValue(menuId, out var menu))
        {
            var items = new List<MenuItem>(menu.Items);
            var item = items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                items.Remove(item);
                if (newPosition < 0) newPosition = 0;
                if (newPosition > items.Count) newPosition = items.Count;
                items.Insert(newPosition, item);
                
                // Reorder
                for (int i = 0; i < items.Count; i++)
                {
                    items[i] = items[i] with { Order = i };
                }
                
                _menus[menuId] = menu with { Items = items, LastModified = DateTime.Now };
                await SaveConfigurationAsync(cancellationToken);
                MenuChanged?.Invoke(this, new MenuChangedEventArgs(menuId, MenuChangeType.ItemMoved, item));
            }
        }
    }
    
    #endregion
    
    #region Hotkey Management
    
    public async Task<IReadOnlyList<HotkeyDefinition>> GetAllHotkeysAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _hotkeys.Values.ToList();
    }
    
    public async Task<HotkeyDefinition?> GetHotkeyForCommandAsync(string commandName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _hotkeys.TryGetValue(commandName, out var hotkey) ? hotkey : null;
    }
    
    public async Task<string?> GetCommandForHotkeyAsync(string hotkey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _hotkeys.Values.FirstOrDefault(h => h.DisplayString == hotkey)?.CommandName;
    }
    
    public async Task SetHotkeyAsync(HotkeyDefinition hotkey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var oldHotkey = _hotkeys.TryGetValue(hotkey.CommandName, out var existing) ? existing : null;
        _hotkeys[hotkey.CommandName] = hotkey with { IsUserDefined = true };
        await SaveConfigurationAsync(cancellationToken);
        HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(hotkey.CommandName, oldHotkey, hotkey));
    }
    
    public async Task RemoveHotkeyAsync(string commandName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        if (_hotkeys.TryRemove(commandName, out var oldHotkey))
        {
            await SaveConfigurationAsync(cancellationToken);
            HotkeyChanged?.Invoke(this, new HotkeyChangedEventArgs(commandName, oldHotkey, null));
        }
    }
    
    public Task ResetHotkeysToDefaultAsync(CancellationToken cancellationToken = default)
    {
        _hotkeys.Clear();
        
        // TC-style default hotkeys
        var defaults = new[]
        {
            new HotkeyDefinition { CommandName = "cm_View", Key = "F3" },
            new HotkeyDefinition { CommandName = "cm_Edit", Key = "F4" },
            new HotkeyDefinition { CommandName = "cm_Copy", Key = "F5" },
            new HotkeyDefinition { CommandName = "cm_Move", Key = "F6" },
            new HotkeyDefinition { CommandName = "cm_MkDir", Key = "F7" },
            new HotkeyDefinition { CommandName = "cm_Delete", Key = "F8" },
            new HotkeyDefinition { CommandName = "cm_Help", Key = "F1" },
            new HotkeyDefinition { CommandName = "cm_Rename", Key = "F6", Shift = true },
            new HotkeyDefinition { CommandName = "cm_SearchFiles", Key = "F7", Alt = true },
            new HotkeyDefinition { CommandName = "cm_Exit", Key = "F4", Alt = true },
            new HotkeyDefinition { CommandName = "cm_SelectAll", Key = "A", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_UnselectAll", Key = "A", Ctrl = true, Shift = true },
            new HotkeyDefinition { CommandName = "cm_Refresh", Key = "R", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_ShowHidden", Key = "H", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_FtpConnect", Key = "F", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_FtpDisconnect", Key = "F", Ctrl = true, Shift = true },
            new HotkeyDefinition { CommandName = "cm_MultiRename", Key = "M", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_CopyPathToClipboard", Key = "P", Ctrl = true, Shift = true },
            new HotkeyDefinition { CommandName = "cm_GotoParent", Key = "Backspace" },
            new HotkeyDefinition { CommandName = "cm_GotoRoot", Key = "Backspace", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_GoBack", Key = "Left", Alt = true },
            new HotkeyDefinition { CommandName = "cm_GoForward", Key = "Right", Alt = true },
            new HotkeyDefinition { CommandName = "cm_SelectGroup", Key = "Add" },  // Num+
            new HotkeyDefinition { CommandName = "cm_UnselectGroup", Key = "Subtract" }, // Num-
            new HotkeyDefinition { CommandName = "cm_InvertSelection", Key = "Multiply" }, // Num*
            new HotkeyDefinition { CommandName = "cm_Properties", Key = "Enter", Alt = true },
            new HotkeyDefinition { CommandName = "cm_Brief", Key = "F1", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_Full", Key = "F2", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_SwitchPanel", Key = "Tab" },
            new HotkeyDefinition { CommandName = "cm_SwapPanels", Key = "U", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_PackFiles", Key = "P", Alt = true },
            new HotkeyDefinition { CommandName = "cm_UnpackFiles", Key = "U", Alt = true },
            new HotkeyDefinition { CommandName = "cm_QuickFilter", Key = "S", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_Hotlist", Key = "D", Ctrl = true },
            new HotkeyDefinition { CommandName = "cm_CommandLine", Key = "Down", Ctrl = true }
        };
        
        foreach (var hk in defaults)
        {
            _hotkeys[hk.CommandName] = hk;
        }
        
        return Task.CompletedTask;
    }
    
    public async Task<IReadOnlyList<HotkeyDefinition>> GetConflictingHotkeysAsync(HotkeyDefinition hotkey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        return _hotkeys.Values
            .Where(h => h.CommandName != hotkey.CommandName &&
                       h.Key == hotkey.Key &&
                       h.Ctrl == hotkey.Ctrl &&
                       h.Alt == hotkey.Alt &&
                       h.Shift == hotkey.Shift &&
                       h.WinKey == hotkey.WinKey)
            .ToList();
    }
    
    #endregion
    
    #region User Commands
    
    public async Task<IReadOnlyList<UserCommand>> GetAllUserCommandsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _userCommands.Values.ToList();
    }
    
    public async Task<UserCommand?> GetUserCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _userCommands.TryGetValue(commandId, out var command) ? command : null;
    }
    
    public async Task SaveUserCommandAsync(UserCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        _userCommands[command.Id] = command;
        await SaveConfigurationAsync(cancellationToken);
    }
    
    public async Task DeleteUserCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_userCommands.TryRemove(commandId, out _))
        {
            await SaveConfigurationAsync(cancellationToken);
        }
    }
    
    public Task<string> ExpandUserCommandAsync(
        UserCommand command, 
        string sourcePath, 
        string targetPath,
        IReadOnlyList<string> selectedFiles, 
        CancellationToken cancellationToken = default)
    {
        var result = command.CommandLine;
        var parameters = command.Parameters;
        
        // TC-style parameter expansion
        if (parameters.UseSourcePath || result.Contains("%P"))
        {
            result = result.Replace("%P", $"\"{sourcePath}\"");
        }
        
        if (parameters.UseTargetPath || result.Contains("%T"))
        {
            result = result.Replace("%T", $"\"{targetPath}\"");
        }
        
        if (parameters.UseSourceFile || result.Contains("%N"))
        {
            var firstFile = selectedFiles.FirstOrDefault() ?? "";
            var fileName = Path.GetFileName(firstFile);
            result = result.Replace("%N", $"\"{fileName}\"");
        }
        
        if (parameters.UseTargetFile || result.Contains("%M"))
        {
            var firstFile = selectedFiles.FirstOrDefault() ?? "";
            var targetFile = Path.Combine(targetPath, Path.GetFileName(firstFile));
            result = result.Replace("%M", $"\"{targetFile}\"");
        }
        
        if (parameters.UseSourceFullPath || result.Contains("%F"))
        {
            var firstFile = selectedFiles.FirstOrDefault() ?? "";
            result = result.Replace("%F", $"\"{firstFile}\"");
        }
        
        if (parameters.UseSelectedFiles || result.Contains("%L"))
        {
            // Create temp file with list of selected files
            var listContent = string.Join(Environment.NewLine, selectedFiles);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, listContent);
            result = result.Replace("%L", $"\"{tempFile}\"");
        }
        
        // Handle short names (Windows 8.3 format - simplified)
        if (parameters.UseShortNames)
        {
            // This would need platform-specific implementation
            // For now, just use regular names
        }
        
        return Task.FromResult(result);
    }
    
    #endregion
    
    #region Button Bar Management
    
    public async Task<IReadOnlyList<ButtonBarDefinition>> GetAllButtonBarsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _buttonBars.Values.ToList();
    }
    
    public async Task<ButtonBarDefinition?> GetButtonBarAsync(string barId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _buttonBars.TryGetValue(barId, out var bar) ? bar : null;
    }
    
    public async Task SaveButtonBarAsync(ButtonBarDefinition bar, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var isNew = !_buttonBars.ContainsKey(bar.Id);
        _buttonBars[bar.Id] = bar;
        await SaveConfigurationAsync(cancellationToken);
        ButtonBarChanged?.Invoke(this, new ButtonBarChangedEventArgs(bar.Id, isNew ? ButtonBarChangeType.Created : ButtonBarChangeType.Updated));
    }
    
    public async Task DeleteButtonBarAsync(string barId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_buttonBars.TryRemove(barId, out _))
        {
            await SaveConfigurationAsync(cancellationToken);
            ButtonBarChanged?.Invoke(this, new ButtonBarChangedEventArgs(barId, ButtonBarChangeType.Deleted));
        }
    }
    
    public Task<ButtonBarDefinition> CreateDefaultButtonBarAsync(CancellationToken cancellationToken = default)
    {
        var bar = new ButtonBarDefinition
        {
            Id = "default",
            Name = "Default Button Bar",
            ShowIcons = true,
            ShowText = false,
            IconSize = 24,
            IsDefault = true,
            Items = new List<ConfigButtonBarItem>
            {
                new() { Id = "bb_refresh", Icon = "refresh", Tooltip = "Refresh", Command = "cm_Refresh", Order = 0 },
                new() { Id = "bb_sep1", IsSeparator = true, Order = 1 },
                new() { Id = "bb_view", Icon = "view", Tooltip = "View (F3)", Command = "cm_View", Order = 2 },
                new() { Id = "bb_edit", Icon = "edit", Tooltip = "Edit (F4)", Command = "cm_Edit", Order = 3 },
                new() { Id = "bb_copy", Icon = "copy", Tooltip = "Copy (F5)", Command = "cm_Copy", Order = 4 },
                new() { Id = "bb_move", Icon = "move", Tooltip = "Move (F6)", Command = "cm_Move", Order = 5 },
                new() { Id = "bb_mkdir", Icon = "folder_add", Tooltip = "New Folder (F7)", Command = "cm_MkDir", Order = 6 },
                new() { Id = "bb_delete", Icon = "delete", Tooltip = "Delete (F8)", Command = "cm_Delete", Order = 7 },
                new() { Id = "bb_sep2", IsSeparator = true, Order = 8 },
                new() { Id = "bb_pack", Icon = "archive", Tooltip = "Pack Files", Command = "cm_PackFiles", Order = 9 },
                new() { Id = "bb_unpack", Icon = "unarchive", Tooltip = "Unpack Files", Command = "cm_UnpackFiles", Order = 10 },
                new() { Id = "bb_sep3", IsSeparator = true, Order = 11 },
                new() { Id = "bb_search", Icon = "search", Tooltip = "Search (Alt+F7)", Command = "cm_SearchFiles", Order = 12 },
                new() { Id = "bb_multirename", Icon = "rename", Tooltip = "Multi-Rename (Ctrl+M)", Command = "cm_MultiRename", Order = 13 },
                new() { Id = "bb_syncdir", Icon = "sync", Tooltip = "Synchronize Directories", Command = "cm_SyncDirs", Order = 14 },
                new() { Id = "bb_sep4", IsSeparator = true, Order = 15 },
                new() { Id = "bb_ftp", Icon = "ftp", Tooltip = "FTP Connect (Ctrl+F)", Command = "cm_FtpConnect", Order = 16 },
                new() { Id = "bb_terminal", Icon = "terminal", Tooltip = "Open Terminal", Command = "cm_OpenTerminal", Order = 17 }
            }
        };
        
        return Task.FromResult(bar);
    }
    
    #endregion
    
    #region Import/Export
    
    public async Task ExportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var config = new
        {
            Menus = _menus.Values.ToList(),
            Hotkeys = _hotkeys.Values.ToList(),
            UserCommands = _userCommands.Values.ToList(),
            ButtonBars = _buttonBars.Values.ToList(),
            ExportDate = DateTime.Now,
            Version = "1.0"
        };
        
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public async Task ImportConfigurationAsync(string filePath, bool merge = false, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (!merge)
        {
            _menus.Clear();
            _hotkeys.Clear();
            _userCommands.Clear();
            _buttonBars.Clear();
        }
        
        if (root.TryGetProperty("menus", out var menusElement))
        {
            var menus = JsonSerializer.Deserialize<List<MenuDefinition>>(menusElement.GetRawText(), JsonOptions);
            if (menus != null)
            {
                foreach (var menu in menus)
                {
                    _menus[menu.Id] = menu;
                }
            }
        }
        
        if (root.TryGetProperty("hotkeys", out var hotkeysElement))
        {
            var hotkeys = JsonSerializer.Deserialize<List<HotkeyDefinition>>(hotkeysElement.GetRawText(), JsonOptions);
            if (hotkeys != null)
            {
                foreach (var hk in hotkeys)
                {
                    _hotkeys[hk.CommandName] = hk;
                }
            }
        }
        
        if (root.TryGetProperty("userCommands", out var userCmdsElement))
        {
            var commands = JsonSerializer.Deserialize<List<UserCommand>>(userCmdsElement.GetRawText(), JsonOptions);
            if (commands != null)
            {
                foreach (var cmd in commands)
                {
                    _userCommands[cmd.Id] = cmd;
                }
            }
        }
        
        if (root.TryGetProperty("buttonBars", out var buttonBarsElement))
        {
            var bars = JsonSerializer.Deserialize<List<ButtonBarDefinition>>(buttonBarsElement.GetRawText(), JsonOptions);
            if (bars != null)
            {
                foreach (var bar in bars)
                {
                    _buttonBars[bar.Id] = bar;
                }
            }
        }
        
        await SaveConfigurationAsync(cancellationToken);
    }
    
    public async Task ImportTcMenuAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Import Total Commander menu file format (MainMenu.mnu, etc.)
        await EnsureInitializedAsync(cancellationToken);
        
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var items = new List<MenuItem>();
        MenuItem? currentSubmenu = null;
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";")) continue;
            
            // Parse TC menu format:
            // MENU="Name"
            // cmd_Command
            // -separator-
            // POPUP="Submenu Name"
            // END_POPUP
            
            if (line.StartsWith("POPUP=\""))
            {
                var name = line.Substring(7, line.Length - 8);
                currentSubmenu = new MenuItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    IsSubmenu = true,
                    Children = new List<MenuItem>()
                };
            }
            else if (line == "END_POPUP" && currentSubmenu != null)
            {
                items.Add(currentSubmenu);
                currentSubmenu = null;
            }
            else if (line == "-separator-" || line.StartsWith("--"))
            {
                var sep = new MenuItem
                {
                    Id = Guid.NewGuid().ToString(),
                    IsSeparator = true
                };
                
                if (currentSubmenu != null)
                {
                    currentSubmenu.Children.Add(sep);
                }
                else
                {
                    items.Add(sep);
                }
            }
            else if (line.StartsWith("cm_"))
            {
                var parts = line.Split('=', 2);
                var commandName = parts[0].Trim();
                var displayName = parts.Length > 1 ? parts[1].Trim() : commandName.Replace("cm_", "");
                
                var item = new MenuItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = displayName,
                    CommandName = commandName,
                    Type = ConfigMenuItemType.InternalCommand
                };
                
                if (currentSubmenu != null)
                {
                    currentSubmenu.Children.Add(item);
                }
                else
                {
                    items.Add(item);
                }
            }
        }
        
        // Create imported menu
        var menu = new MenuDefinition
        {
            Id = "imported_" + Path.GetFileNameWithoutExtension(filePath),
            Name = "Imported Menu",
            Type = MenuType.UserMenu,
            Items = items,
            IsUserDefined = true
        };
        
        _menus[menu.Id] = menu;
        await SaveConfigurationAsync(cancellationToken);
    }
    
    public async Task ImportTcUserCommandsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Import Total Commander usercmd.ini format
        await EnsureInitializedAsync(cancellationToken);
        
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        string? currentSection = null;
        var commandData = new Dictionary<string, string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
            
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                // Save previous command if exists
                if (currentSection != null && commandData.Count > 0)
                {
                    SaveTcUserCommand(currentSection, commandData);
                }
                
                currentSection = trimmed.Substring(1, trimmed.Length - 2);
                commandData.Clear();
            }
            else if (currentSection != null)
            {
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed.Substring(0, eqIndex).Trim();
                    var value = trimmed.Substring(eqIndex + 1).Trim();
                    commandData[key] = value;
                }
            }
        }
        
        // Save last command
        if (currentSection != null && commandData.Count > 0)
        {
            SaveTcUserCommand(currentSection, commandData);
        }
        
        await SaveConfigurationAsync(cancellationToken);
    }
    
    private void SaveTcUserCommand(string name, Dictionary<string, string> data)
    {
        var command = new UserCommand
        {
            Id = "tc_" + name.Replace(" ", "_"),
            Name = name,
            CommandLine = data.GetValueOrDefault("cmd", ""),
            WorkingDirectory = data.GetValueOrDefault("path", ""),
            Icon = data.GetValueOrDefault("iconic", ""),
            Description = data.GetValueOrDefault("menu", name)
        };
        
        _userCommands[command.Id] = command;
    }
    
    #endregion
}
