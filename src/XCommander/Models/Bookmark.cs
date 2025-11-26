namespace XCommander.Models;

/// <summary>
/// Represents a bookmarked/favorite location.
/// </summary>
public class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Category { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public int Order { get; set; }
    public string? Shortcut { get; set; }
}

/// <summary>
/// Bookmark category for organizing favorites.
/// </summary>
public class BookmarkCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int Order { get; set; }
    public List<Bookmark> Bookmarks { get; set; } = new();
}

/// <summary>
/// Container for all bookmarks data.
/// </summary>
public class BookmarksData
{
    public List<BookmarkCategory> Categories { get; set; } = new();
    public List<Bookmark> UncategorizedBookmarks { get; set; } = new();
    public List<string> RecentLocations { get; set; } = new();
    public int MaxRecentLocations { get; set; } = 20;
}
