using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCommander.Models;

/// <summary>
/// Represents a toolbar button configuration.
/// </summary>
public class ToolbarButton
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "⚙️";
    public string CommandName { get; set; } = string.Empty;
    public string? CommandParameter { get; set; }
    public string? Tooltip { get; set; }
    public bool IsSeparator { get; set; }
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Represents the toolbar configuration.
/// </summary>
public class ToolbarConfiguration
{
    public List<ToolbarButton> Buttons { get; set; } = new();
    public bool ShowLabels { get; set; }
    public string Size { get; set; } = "Medium"; // Small, Medium, Large
    
    public static ToolbarConfiguration CreateDefault()
    {
        return new ToolbarConfiguration
        {
            Buttons = new List<ToolbarButton>
            {
                new() { Label = "Back", Icon = "⬅", CommandName = "GoBack", Tooltip = "Back (Alt+Left)", Order = 1 },
                new() { Label = "Forward", Icon = "➡", CommandName = "GoForward", Tooltip = "Forward (Alt+Right)", Order = 2 },
                new() { Label = "Parent", Icon = "⬆", CommandName = "GoToParent", Tooltip = "Parent Directory (Backspace)", Order = 3 },
                new() { Label = "Home", Icon = "🏠", CommandName = "GoHome", Tooltip = "Home", Order = 4 },
                new() { IsSeparator = true, Order = 5 },
                new() { Label = "Refresh", Icon = "⟳", CommandName = "Refresh", Tooltip = "Refresh (Ctrl+R)", Order = 6 },
                new() { IsSeparator = true, Order = 7 },
                new() { Label = "Copy", Icon = "📋", CommandName = "CopySelected", Tooltip = "Copy (F5)", Order = 8 },
                new() { Label = "Move", Icon = "✂️", CommandName = "MoveSelected", Tooltip = "Move (F6)", Order = 9 },
                new() { Label = "Delete", Icon = "🗑️", CommandName = "DeleteSelected", Tooltip = "Delete (F8)", Order = 10 },
                new() { IsSeparator = true, Order = 11 },
                new() { Label = "New Folder", Icon = "📁+", CommandName = "CreateNewFolder", Tooltip = "New Folder (F7)", Order = 12 },
                new() { Label = "New File", Icon = "📄+", CommandName = "CreateNewFile", Tooltip = "New File (Shift+F4)", Order = 13 },
                new() { IsSeparator = true, Order = 14 },
                new() { Label = "Search", Icon = "🔍", CommandName = "Search", Tooltip = "Search Files (Alt+F7)", Order = 15 },
                new() { IsSeparator = true, Order = 16 },
                new() { Label = "New Tab", Icon = "📑+", CommandName = "NewTab", Tooltip = "New Tab (Ctrl+T)", Order = 17 },
                new() { IsSeparator = true, Order = 18 },
                new() { Label = "Bookmarks", Icon = "⭐", CommandName = "ToggleBookmarks", Tooltip = "Bookmarks (Ctrl+B)", Order = 19 },
                new() { Label = "Add Bookmark", Icon = "⭐+", CommandName = "AddBookmark", Tooltip = "Add Current Folder to Bookmarks (Ctrl+Shift+D)", Order = 20 },
            }
        };
    }
    
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "toolbar.json");
    
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving toolbar configuration: {ex.Message}");
        }
    }
    
    public static ToolbarConfiguration Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<ToolbarConfiguration>(json);
                return config ?? CreateDefault();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading toolbar configuration: {ex.Message}");
        }
        
        return CreateDefault();
    }
}
