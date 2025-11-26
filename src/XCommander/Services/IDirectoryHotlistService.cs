namespace XCommander.Services;

/// <summary>
/// Hotlist item type
/// </summary>
public enum HotlistItemType
{
    Directory,
    Category,
    Separator
}

/// <summary>
/// Hotlist item (directory or category)
/// </summary>
public record HotlistItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public HotlistItemType Type { get; init; } = HotlistItemType.Directory;
    public string Name { get; init; } = string.Empty;
    public string? Path { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? KeyboardShortcut { get; init; }
    public int Order { get; init; }
    public string? ParentCategoryId { get; init; }
    public bool IsExpanded { get; init; } = true;
    public DateTime Created { get; init; } = DateTime.Now;
    public DateTime LastAccessed { get; init; }
    public int AccessCount { get; init; }
}

/// <summary>
/// Category in the hotlist
/// </summary>
public record HotlistCategory
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int Order { get; init; }
    public string? ParentCategoryId { get; init; }
    public bool IsExpanded { get; init; } = true;
    public IReadOnlyList<HotlistItem> Items { get; init; } = Array.Empty<HotlistItem>();
    public IReadOnlyList<HotlistCategory> SubCategories { get; init; } = Array.Empty<HotlistCategory>();
}

/// <summary>
/// Recent folder entry
/// </summary>
public record RecentFolderEntry
{
    public string Path { get; init; } = string.Empty;
    public DateTime LastAccessed { get; init; }
    public int AccessCount { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>
/// Hotlist import/export format
/// </summary>
public class HotlistExportData
{
    public string Version { get; init; } = "1.0";
    public DateTime ExportedAt { get; init; } = DateTime.Now;
    public IReadOnlyList<HotlistItem> Items { get; init; } = Array.Empty<HotlistItem>();
    public IReadOnlyList<HotlistCategory> Categories { get; init; } = Array.Empty<HotlistCategory>();
}

/// <summary>
/// Service for managing directory hotlist and favorites
/// </summary>
public interface IDirectoryHotlistService
{
    /// <summary>
    /// Event raised when hotlist changes
    /// </summary>
    event EventHandler? HotlistChanged;
    
    // Hotlist Items
    
    /// <summary>
    /// Get all hotlist items
    /// </summary>
    Task<IReadOnlyList<HotlistItem>> GetItemsAsync();
    
    /// <summary>
    /// Get items in a specific category
    /// </summary>
    Task<IReadOnlyList<HotlistItem>> GetItemsInCategoryAsync(string? categoryId);
    
    /// <summary>
    /// Add a directory to hotlist
    /// </summary>
    Task<HotlistItem> AddDirectoryAsync(string path, string? name = null, string? categoryId = null);
    
    /// <summary>
    /// Update a hotlist item
    /// </summary>
    Task UpdateItemAsync(HotlistItem item);
    
    /// <summary>
    /// Remove item from hotlist
    /// </summary>
    Task RemoveItemAsync(string itemId);
    
    /// <summary>
    /// Move item to a different category
    /// </summary>
    Task MoveItemToCategoryAsync(string itemId, string? categoryId);
    
    /// <summary>
    /// Reorder items
    /// </summary>
    Task ReorderItemsAsync(IReadOnlyList<string> itemIds);
    
    /// <summary>
    /// Set keyboard shortcut for item
    /// </summary>
    Task SetItemShortcutAsync(string itemId, string? shortcut);
    
    /// <summary>
    /// Record access to hotlist item
    /// </summary>
    Task RecordAccessAsync(string itemId);
    
    // Categories
    
    /// <summary>
    /// Get all categories
    /// </summary>
    Task<IReadOnlyList<HotlistCategory>> GetCategoriesAsync();
    
    /// <summary>
    /// Get category tree (hierarchical)
    /// </summary>
    Task<IReadOnlyList<HotlistCategory>> GetCategoryTreeAsync();
    
    /// <summary>
    /// Create a new category
    /// </summary>
    Task<HotlistCategory> CreateCategoryAsync(string name, string? parentCategoryId = null);
    
    /// <summary>
    /// Update a category
    /// </summary>
    Task UpdateCategoryAsync(HotlistCategory category);
    
    /// <summary>
    /// Delete a category
    /// </summary>
    Task DeleteCategoryAsync(string categoryId, bool deleteContents = false);
    
    /// <summary>
    /// Rename a category
    /// </summary>
    Task RenameCategoryAsync(string categoryId, string newName);
    
    // Recent Folders
    
    /// <summary>
    /// Get recent folders
    /// </summary>
    Task<IReadOnlyList<RecentFolderEntry>> GetRecentFoldersAsync(int maxCount = 20);
    
    /// <summary>
    /// Add folder to recent list
    /// </summary>
    Task AddRecentFolderAsync(string path);
    
    /// <summary>
    /// Clear recent folders
    /// </summary>
    Task ClearRecentFoldersAsync();
    
    /// <summary>
    /// Remove a specific recent folder
    /// </summary>
    Task RemoveRecentFolderAsync(string path);
    
    // Favorites (quick access without category)
    
    /// <summary>
    /// Get favorite folders
    /// </summary>
    Task<IReadOnlyList<HotlistItem>> GetFavoritesAsync();
    
    /// <summary>
    /// Add to favorites
    /// </summary>
    Task AddToFavoritesAsync(string path);
    
    /// <summary>
    /// Remove from favorites
    /// </summary>
    Task RemoveFromFavoritesAsync(string path);
    
    /// <summary>
    /// Check if path is in favorites
    /// </summary>
    Task<bool> IsFavoriteAsync(string path);
    
    // Search
    
    /// <summary>
    /// Search hotlist items
    /// </summary>
    Task<IReadOnlyList<HotlistItem>> SearchAsync(string query);
    
    /// <summary>
    /// Get item by keyboard shortcut
    /// </summary>
    Task<HotlistItem?> GetItemByShortcutAsync(string shortcut);
    
    // Import/Export
    
    /// <summary>
    /// Export hotlist
    /// </summary>
    Task<HotlistExportData> ExportAsync();
    
    /// <summary>
    /// Import hotlist
    /// </summary>
    Task ImportAsync(HotlistExportData data, bool merge = false);
    
    /// <summary>
    /// Export to file
    /// </summary>
    Task ExportToFileAsync(string filePath);
    
    /// <summary>
    /// Import from file
    /// </summary>
    Task ImportFromFileAsync(string filePath, bool merge = false);
}
