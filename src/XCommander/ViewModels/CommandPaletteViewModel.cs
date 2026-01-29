using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Represents a command in the command palette.
/// </summary>
public partial class CommandItem : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _description = string.Empty;
    
    [ObservableProperty]
    private string _category = string.Empty;
    
    [ObservableProperty]
    private string _shortcut = string.Empty;
    
    [ObservableProperty]
    private string _icon = string.Empty;
    
    [ObservableProperty]
    private bool _isEnabled = true;
    
    public Action? Execute { get; set; }
    public Func<Task>? ExecuteAsync { get; set; }
    public Func<bool>? CanExecute { get; set; }
}

/// <summary>
/// ViewModel for the command palette.
/// </summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly List<CommandItem> _allCommands = new();
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<CommandItem> _filteredCommands = new();
    
    [ObservableProperty]
    private CommandItem? _selectedCommand;
    
    [ObservableProperty]
    private int _selectedIndex;
    
    [ObservableProperty]
    private bool _isOpen;

    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    
    public event EventHandler? RequestClose;
    public event EventHandler<CommandItem>? CommandExecuted;

    public CommandPaletteViewModel()
    {
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
        RegisterDefaultCommands();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<CommandItem>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Command",
                cellTemplateKey: "CommandItemTemplate",
                configure: column =>
                {
                    column.ColumnKey = "command";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<CommandItem, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<CommandItem, string>(
                            item => item.Name),
                        SearchTextProvider = item =>
                        {
                            if (item is not CommandItem command)
                                return string.Empty;
                            return $"{command.Name} {command.Description} {command.Category} {command.Shortcut}";
                        }
                    };
                })
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterCommands();
    }

    private void FilterCommands()
    {
        FilteredCommands.Clear();
        
        var searchTerms = SearchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var filtered = _allCommands
            .Where(c => c.IsEnabled && (c.CanExecute?.Invoke() ?? true))
            .Where(c => searchTerms.Length == 0 || searchTerms.All(term =>
                c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name);
        
        foreach (var command in filtered)
        {
            FilteredCommands.Add(command);
        }
        
        SelectedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        SelectedCommand = FilteredCommands.FirstOrDefault();
    }

    public void RegisterCommand(CommandItem command)
    {
        _allCommands.Add(command);
        FilterCommands();
    }

    public void RegisterCommands(IEnumerable<CommandItem> commands)
    {
        foreach (var command in commands)
        {
            _allCommands.Add(command);
        }
        FilterCommands();
    }

    public void UnregisterCommand(string commandId)
    {
        _allCommands.RemoveAll(c => c.Id == commandId);
        FilterCommands();
    }

    [RelayCommand]
    public async Task ExecuteSelectedAsync()
    {
        if (SelectedCommand == null)
            return;

        var command = SelectedCommand;
        RequestClose?.Invoke(this, EventArgs.Empty);

        try
        {
            if (command.ExecuteAsync != null)
            {
                await command.ExecuteAsync();
            }
            else
            {
                command.Execute?.Invoke();
            }
            
            CommandExecuted?.Invoke(this, command);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error executing command {command.Id}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void MoveSelectionUp()
    {
        if (FilteredCommands.Count == 0)
            return;

        SelectedIndex = SelectedIndex > 0 
            ? SelectedIndex - 1 
            : FilteredCommands.Count - 1;
        
        SelectedCommand = FilteredCommands[SelectedIndex];
    }

    [RelayCommand]
    public void MoveSelectionDown()
    {
        if (FilteredCommands.Count == 0)
            return;

        SelectedIndex = SelectedIndex < FilteredCommands.Count - 1 
            ? SelectedIndex + 1 
            : 0;
        
        SelectedCommand = FilteredCommands[SelectedIndex];
    }

    [RelayCommand]
    public void Open()
    {
        SearchText = string.Empty;
        IsOpen = true;
        FilterCommands();
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void RegisterDefaultCommands()
    {
        var defaultCommands = new List<CommandItem>
        {
            // File Operations
            new() { Id = "file.copy", Name = "Copy", Description = "Copy selected files to inactive panel", Category = "File", Shortcut = "F5", Icon = "📋" },
            new() { Id = "file.move", Name = "Move", Description = "Move selected files to inactive panel", Category = "File", Shortcut = "F6", Icon = "📦" },
            new() { Id = "file.delete", Name = "Delete", Description = "Delete selected files", Category = "File", Shortcut = "F8", Icon = "🗑️" },
            new() { Id = "file.rename", Name = "Rename", Description = "Rename selected file", Category = "File", Shortcut = "F2", Icon = "✏️" },
            new() { Id = "file.newFolder", Name = "New Folder", Description = "Create new folder", Category = "File", Shortcut = "F7", Icon = "📁" },
            new() { Id = "file.newFile", Name = "New File", Description = "Create new file", Category = "File", Icon = "📄" },
            new() { Id = "file.view", Name = "View", Description = "View selected file", Category = "File", Shortcut = "F3", Icon = "👁️" },
            new() { Id = "file.edit", Name = "Edit", Description = "Edit selected file", Category = "File", Shortcut = "F4", Icon = "✍️" },
            
            // Navigation
            new() { Id = "nav.refresh", Name = "Refresh", Description = "Refresh current panel", Category = "Navigation", Shortcut = "Ctrl+R", Icon = "🔄" },
            new() { Id = "nav.goToParent", Name = "Go to Parent", Description = "Navigate to parent folder", Category = "Navigation", Shortcut = "Backspace", Icon = "⬆️" },
            new() { Id = "nav.switchPanel", Name = "Switch Panel", Description = "Switch between left and right panels", Category = "Navigation", Shortcut = "Tab", Icon = "↔️" },
            new() { Id = "nav.goBack", Name = "Go Back", Description = "Navigate to previous folder", Category = "Navigation", Shortcut = "Alt+Left", Icon = "◀️" },
            new() { Id = "nav.goForward", Name = "Go Forward", Description = "Navigate to next folder", Category = "Navigation", Shortcut = "Alt+Right", Icon = "▶️" },
            
            // View
            new() { Id = "view.quickView", Name = "Toggle Quick View", Description = "Toggle quick view panel", Category = "View", Shortcut = "Ctrl+Q", Icon = "🔍" },
            new() { Id = "view.detailView", Name = "Detail View", Description = "Switch to detail view", Category = "View", Icon = "📋" },
            new() { Id = "view.thumbnailView", Name = "Thumbnail View", Description = "Switch to thumbnail view", Category = "View", Icon = "🖼️" },
            new() { Id = "view.showHidden", Name = "Show Hidden Files", Description = "Toggle hidden files visibility", Category = "View", Shortcut = "Ctrl+H", Icon = "👁️" },
            
            // Tools
            new() { Id = "tools.search", Name = "Search", Description = "Search for files", Category = "Tools", Shortcut = "Alt+F7", Icon = "🔎" },
            new() { Id = "tools.multiRename", Name = "Multi-Rename", Description = "Rename multiple files", Category = "Tools", Shortcut = "Ctrl+M", Icon = "📝" },
            new() { Id = "tools.compare", Name = "Compare Directories", Description = "Compare left and right directories", Category = "Tools", Icon = "⚖️" },
            new() { Id = "tools.compareFiles", Name = "Compare Files", Description = "Compare selected files", Category = "Tools", Icon = "📊" },
            new() { Id = "tools.sync", Name = "Sync Directories", Description = "Synchronize directories", Category = "Tools", Icon = "🔄" },
            new() { Id = "tools.checksum", Name = "Calculate Checksum", Description = "Calculate file checksums", Category = "Tools", Icon = "🔢" },
            new() { Id = "tools.split", Name = "Split File", Description = "Split file into parts", Category = "Tools", Icon = "✂️" },
            new() { Id = "tools.combine", Name = "Combine Files", Description = "Combine split files", Category = "Tools", Icon = "🔗" },
            
            // Archive
            new() { Id = "archive.open", Name = "Open Archive", Description = "Open archive file", Category = "Archive", Icon = "📦" },
            new() { Id = "archive.create", Name = "Create Archive", Description = "Create new archive", Category = "Archive", Shortcut = "Alt+F5", Icon = "📦" },
            new() { Id = "archive.extract", Name = "Extract Archive", Description = "Extract archive to folder", Category = "Archive", Shortcut = "Alt+F9", Icon = "📂" },
            
            // Network
            new() { Id = "network.ftp", Name = "FTP Connection", Description = "Connect to FTP server", Category = "Network", Icon = "🌐" },
            new() { Id = "network.sftp", Name = "SFTP Connection", Description = "Connect to SFTP server", Category = "Network", Icon = "🔒" },
            
            // Configuration
            new() { Id = "config.settings", Name = "Settings", Description = "Open settings dialog", Category = "Configuration", Shortcut = "Alt+Enter", Icon = "⚙️" },
            new() { Id = "config.plugins", Name = "Plugins", Description = "Manage plugins", Category = "Configuration", Icon = "🔌" },
            new() { Id = "config.columns", Name = "Custom Columns", Description = "Configure panel columns", Category = "Configuration", Icon = "📊" },
            
            // Application
            new() { Id = "app.commandPalette", Name = "Command Palette", Description = "Open command palette", Category = "Application", Shortcut = "Ctrl+Shift+P", Icon = "⌨️" },
            new() { Id = "app.about", Name = "About", Description = "About XCommander", Category = "Application", Icon = "ℹ️" },
            new() { Id = "app.exit", Name = "Exit", Description = "Exit application", Category = "Application", Shortcut = "Alt+F4", Icon = "🚪" },
        };
        
        RegisterCommands(defaultCommands);
    }

    /// <summary>
    /// Bind commands to actions from the main view model.
    /// </summary>
    public void BindCommands(
        Action copy,
        Action move,
        Action delete,
        Action rename,
        Action newFolder,
        Action newFile,
        Action view,
        Action edit,
        Action refresh,
        Action goToParent,
        Action switchPanel,
        Action goBack,
        Action goForward,
        Action search,
        Action multiRename,
        Action compare,
        Action compareFiles,
        Action sync,
        Action checksum,
        Action split,
        Action combine,
        Action openArchive,
        Action createArchive,
        Action extractArchive,
        Action ftp,
        Action sftp,
        Action settings,
        Action plugins,
        Action columns,
        Action quickView,
        Action detailView,
        Action thumbnailView,
        Action showHidden,
        Action commandPalette,
        Action about,
        Action exit)
    {
        SetCommandAction("file.copy", copy);
        SetCommandAction("file.move", move);
        SetCommandAction("file.delete", delete);
        SetCommandAction("file.rename", rename);
        SetCommandAction("file.newFolder", newFolder);
        SetCommandAction("file.newFile", newFile);
        SetCommandAction("file.view", view);
        SetCommandAction("file.edit", edit);
        SetCommandAction("nav.refresh", refresh);
        SetCommandAction("nav.goToParent", goToParent);
        SetCommandAction("nav.switchPanel", switchPanel);
        SetCommandAction("nav.goBack", goBack);
        SetCommandAction("nav.goForward", goForward);
        SetCommandAction("view.quickView", quickView);
        SetCommandAction("view.detailView", detailView);
        SetCommandAction("view.thumbnailView", thumbnailView);
        SetCommandAction("view.showHidden", showHidden);
        SetCommandAction("tools.search", search);
        SetCommandAction("tools.multiRename", multiRename);
        SetCommandAction("tools.compare", compare);
        SetCommandAction("tools.compareFiles", compareFiles);
        SetCommandAction("tools.sync", sync);
        SetCommandAction("tools.checksum", checksum);
        SetCommandAction("tools.split", split);
        SetCommandAction("tools.combine", combine);
        SetCommandAction("archive.open", openArchive);
        SetCommandAction("archive.create", createArchive);
        SetCommandAction("archive.extract", extractArchive);
        SetCommandAction("network.ftp", ftp);
        SetCommandAction("network.sftp", sftp);
        SetCommandAction("config.settings", settings);
        SetCommandAction("config.plugins", plugins);
        SetCommandAction("config.columns", columns);
        SetCommandAction("app.commandPalette", commandPalette);
        SetCommandAction("app.about", about);
        SetCommandAction("app.exit", exit);
    }

    private void SetCommandAction(string commandId, Action action)
    {
        var command = _allCommands.FirstOrDefault(c => c.Id == commandId);
        if (command != null)
        {
            command.Execute = action;
        }
    }
}
