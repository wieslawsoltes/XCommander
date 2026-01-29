using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Helpers;

namespace XCommander.ViewModels;

/// <summary>
/// Represents a customizable keyboard shortcut.
/// </summary>
public partial class KeyboardShortcut : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _category = string.Empty;
    
    [ObservableProperty]
    private string _defaultGesture = string.Empty;
    
    [ObservableProperty]
    private string _currentGesture = string.Empty;
    
    [ObservableProperty]
    private string _commandName = string.Empty;
    
    [ObservableProperty]
    private bool _isModified;
    
    partial void OnCurrentGestureChanged(string value)
    {
        IsModified = value != DefaultGesture;
    }
    
    public void Reset()
    {
        CurrentGesture = DefaultGesture;
    }
}

/// <summary>
/// Manages keyboard shortcuts with persistence and customization.
/// </summary>
public partial class KeyboardShortcutManager : ObservableObject
{
    private const string CategoryColumnKey = "category";
    private const string ActionColumnKey = "action";
    private const string DefaultColumnKey = "default";
    private const string CurrentColumnKey = "current";
    private const string StatusColumnKey = "status";

    private static readonly string ShortcutsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "shortcuts.json");
    
    public ObservableCollection<KeyboardShortcut> Shortcuts { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    
    public KeyboardShortcutManager()
    {
        InitializeDefaultShortcuts();
        LoadCustomShortcuts();
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<KeyboardShortcut>();

        IPropertyInfo categoryProperty = DataGridColumnHelper.CreateProperty(
            nameof(KeyboardShortcut.Category),
            (KeyboardShortcut item) => item.Category,
            (item, value) => item.Category = value);
        IPropertyInfo nameProperty = DataGridColumnHelper.CreateProperty(
            nameof(KeyboardShortcut.Name),
            (KeyboardShortcut item) => item.Name,
            (item, value) => item.Name = value);
        IPropertyInfo defaultGestureProperty = DataGridColumnHelper.CreateProperty(
            nameof(KeyboardShortcut.DefaultGesture),
            (KeyboardShortcut item) => item.DefaultGesture,
            (item, value) => item.DefaultGesture = value);

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Text(
                header: "Category",
                property: categoryProperty,
                getter: item => item.Category,
                configure: column =>
                {
                    column.ColumnKey = CategoryColumnKey;
                    column.Width = new DataGridLength(120);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Action",
                property: nameProperty,
                getter: item => item.Name,
                configure: column =>
                {
                    column.ColumnKey = ActionColumnKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Default",
                property: defaultGestureProperty,
                getter: item => item.DefaultGesture,
                configure: column =>
                {
                    column.ColumnKey = DefaultColumnKey;
                    column.Width = new DataGridLength(120);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Template(
                header: "Current Shortcut",
                cellTemplateKey: "KeyboardShortcutEditorTemplate",
                configure: column =>
                {
                    column.ColumnKey = CurrentColumnKey;
                    column.Width = new DataGridLength(160);
                    column.IsReadOnly = false;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<KeyboardShortcut, string>(
                        item => item.CurrentGesture,
                        (item, value) => item.CurrentGesture = value);
                    column.ValueType = typeof(string);
                }),
            builder.Template(
                header: "Status",
                cellTemplateKey: "KeyboardShortcutStatusTemplate",
                configure: column =>
                {
                    column.ColumnKey = StatusColumnKey;
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<KeyboardShortcut, string>(
                        item => item.IsModified ? "Modified" : string.Empty);
                    column.ValueType = typeof(string);
                })
        };
    }
    
    private void InitializeDefaultShortcuts()
    {
        var defaults = new List<KeyboardShortcut>
        {
            // Navigation
            new() { Id = "GoBack", Name = "Go Back", Category = "Navigation", DefaultGesture = "Alt+Left", CommandName = "GoBackCommand" },
            new() { Id = "GoForward", Name = "Go Forward", Category = "Navigation", DefaultGesture = "Alt+Right", CommandName = "GoForwardCommand" },
            new() { Id = "GoToParent", Name = "Go to Parent Folder", Category = "Navigation", DefaultGesture = "Back", CommandName = "GoToParentCommand" },
            
            // Selection
            new() { Id = "SelectAll", Name = "Select All", Category = "Selection", DefaultGesture = "Ctrl+A", CommandName = "SelectAllCommand" },
            new() { Id = "DeselectAll", Name = "Deselect All", Category = "Selection", DefaultGesture = "Ctrl+Shift+A", CommandName = "DeselectAllCommand" },
            new() { Id = "InvertSelection", Name = "Invert Selection", Category = "Selection", DefaultGesture = "Ctrl+I", CommandName = "InvertSelectionCommand" },
            
            // File Operations
            new() { Id = "Rename", Name = "Rename", Category = "File Operations", DefaultGesture = "F2", CommandName = "RenameSelectedCommand" },
            new() { Id = "View", Name = "View File", Category = "File Operations", DefaultGesture = "F3", CommandName = "ViewSelectedCommand" },
            new() { Id = "Edit", Name = "Edit File", Category = "File Operations", DefaultGesture = "F4", CommandName = "EditSelectedCommand" },
            new() { Id = "Copy", Name = "Copy", Category = "File Operations", DefaultGesture = "F5", CommandName = "CopySelectedCommand" },
            new() { Id = "Move", Name = "Move", Category = "File Operations", DefaultGesture = "F6", CommandName = "MoveSelectedCommand" },
            new() { Id = "NewFolder", Name = "Create Folder", Category = "File Operations", DefaultGesture = "F7", CommandName = "CreateNewFolderCommand" },
            new() { Id = "Delete", Name = "Delete", Category = "File Operations", DefaultGesture = "F8", CommandName = "DeleteSelectedCommand" },
            new() { Id = "NewFile", Name = "Create File", Category = "File Operations", DefaultGesture = "Shift+F4", CommandName = "CreateNewFileCommand" },
            
            // View
            new() { Id = "ToggleHidden", Name = "Toggle Hidden Files", Category = "View", DefaultGesture = "Ctrl+H", CommandName = "ToggleHiddenFilesCommand" },
            new() { Id = "Refresh", Name = "Refresh", Category = "View", DefaultGesture = "Ctrl+R", CommandName = "RefreshCommand" },
            new() { Id = "QuickView", Name = "Toggle Quick View", Category = "View", DefaultGesture = "Ctrl+Q", CommandName = "ToggleQuickViewCommand" },
            new() { Id = "DirectoryTree", Name = "Toggle Directory Tree", Category = "View", DefaultGesture = "Alt+F1", CommandName = "ToggleDirectoryTreeCommand" },
            
            // Bookmarks
            new() { Id = "ToggleBookmarks", Name = "Toggle Bookmarks", Category = "Bookmarks", DefaultGesture = "Ctrl+B", CommandName = "ToggleBookmarksPanelCommand" },
            new() { Id = "AddBookmark", Name = "Add to Bookmarks", Category = "Bookmarks", DefaultGesture = "Ctrl+Shift+D", CommandName = "AddCurrentFolderToBookmarksCommand" },
            
            // Tabs
            new() { Id = "NewTab", Name = "New Tab", Category = "Tabs", DefaultGesture = "Ctrl+T", CommandName = "AddNewTabCommand" },
            new() { Id = "CloseTab", Name = "Close Tab", Category = "Tabs", DefaultGesture = "Ctrl+W", CommandName = "CloseTabCommand" },
            new() { Id = "NextTab", Name = "Next Tab", Category = "Tabs", DefaultGesture = "Ctrl+Tab", CommandName = "NextTabCommand" },
            new() { Id = "PreviousTab", Name = "Previous Tab", Category = "Tabs", DefaultGesture = "Ctrl+Shift+Tab", CommandName = "PreviousTabCommand" },
            
            // Tools
            new() { Id = "MultiRename", Name = "Multi-Rename", Category = "Tools", DefaultGesture = "Ctrl+M", CommandName = "MultiRenameCommand" },
            new() { Id = "EncodingTool", Name = "Encoding Tool", Category = "Tools", DefaultGesture = "Ctrl+E", CommandName = "OpenEncodingToolCommand" },
            new() { Id = "CommandPalette", Name = "Command Palette", Category = "Tools", DefaultGesture = "Ctrl+Shift+P", CommandName = "OpenCommandPaletteCommand" },
            
            // Search
            new() { Id = "Search", Name = "Search Files", Category = "Search", DefaultGesture = "Ctrl+F", CommandName = "OpenSearchCommand" },
            new() { Id = "QuickSearch", Name = "Quick Filter", Category = "Search", DefaultGesture = "Ctrl+Shift+F", CommandName = "QuickSearchCommand" },
        };
        
        foreach (var shortcut in defaults)
        {
            shortcut.CurrentGesture = shortcut.DefaultGesture;
            Shortcuts.Add(shortcut);
        }
    }
    
    private void LoadCustomShortcuts()
    {
        try
        {
            if (File.Exists(ShortcutsFilePath))
            {
                var json = File.ReadAllText(ShortcutsFilePath);
                var customizations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (customizations != null)
                {
                    foreach (var (id, gesture) in customizations)
                    {
                        var shortcut = Shortcuts.FirstOrDefault(s => s.Id == id);
                        if (shortcut != null)
                        {
                            shortcut.CurrentGesture = gesture;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors, use defaults
        }
    }
    
    [RelayCommand]
    public void SaveShortcuts()
    {
        try
        {
            var directory = Path.GetDirectoryName(ShortcutsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var customizations = Shortcuts
                .Where(s => s.IsModified)
                .ToDictionary(s => s.Id, s => s.CurrentGesture);
            
            var json = JsonSerializer.Serialize(customizations, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ShortcutsFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    [RelayCommand]
    public void ResetAllShortcuts()
    {
        foreach (var shortcut in Shortcuts)
        {
            shortcut.Reset();
        }
        
        // Delete custom shortcuts file
        try
        {
            if (File.Exists(ShortcutsFilePath))
            {
                File.Delete(ShortcutsFilePath);
            }
        }
        catch { }
    }
    
    [RelayCommand]
    public void ResetShortcut(KeyboardShortcut? shortcut)
    {
        shortcut?.Reset();
    }
    
    [RelayCommand]
    public async Task ExportShortcutsAsync()
    {
        // This will be handled by the UI
        ExportRequested?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    public async Task ImportShortcutsAsync()
    {
        // This will be handled by the UI
        ImportRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? ExportRequested;
    public event EventHandler? ImportRequested;
    
    public async Task ExportToFileAsync(string filePath)
    {
        var exportData = Shortcuts.ToDictionary(s => s.Id, s => s.CurrentGesture);
        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task ImportFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importedData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        
        if (importedData != null)
        {
            foreach (var (id, gesture) in importedData)
            {
                var shortcut = Shortcuts.FirstOrDefault(s => s.Id == id);
                if (shortcut != null)
                {
                    shortcut.CurrentGesture = gesture;
                }
            }
            
            SaveShortcuts();
        }
    }
    
    public string? GetGesture(string shortcutId)
    {
        return Shortcuts.FirstOrDefault(s => s.Id == shortcutId)?.CurrentGesture;
    }
    
    public IEnumerable<string> Categories => Shortcuts.Select(s => s.Category).Distinct().OrderBy(c => c);
    
    public IEnumerable<KeyboardShortcut> GetShortcutsByCategory(string category)
    {
        return Shortcuts.Where(s => s.Category == category);
    }
    
    public bool HasConflict(string id, string gesture)
    {
        return Shortcuts.Any(s => s.Id != id && 
                                  s.CurrentGesture.Equals(gesture, StringComparison.OrdinalIgnoreCase));
    }
    
    public KeyboardShortcut? GetConflictingShortcut(string id, string gesture)
    {
        return Shortcuts.FirstOrDefault(s => s.Id != id && 
                                             s.CurrentGesture.Equals(gesture, StringComparison.OrdinalIgnoreCase));
    }
}
