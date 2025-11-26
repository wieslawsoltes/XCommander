using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCommander.Services;

/// <summary>
/// A bookmark to a folder or location
/// </summary>
public class FolderBookmark
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }
    public string? HotKey { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int AccessCount { get; set; }
    
    [JsonIgnore]
    public bool Exists => Directory.Exists(Path) || File.Exists(Path);
}

/// <summary>
/// A category/folder for organizing bookmarks
/// </summary>
public class BookmarkCategory
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }
    public int SortOrder { get; set; }
    public bool IsExpanded { get; set; } = true;
}

/// <summary>
/// Complete bookmark collection data
/// </summary>
public class BookmarkData
{
    public int Version { get; set; } = 1;
    public List<BookmarkCategory> Categories { get; set; } = new();
    public List<FolderBookmark> Bookmarks { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for managing folder bookmarks
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Event raised when bookmarks change
    /// </summary>
    event EventHandler? BookmarksChanged;
    
    /// <summary>
    /// Get all bookmarks
    /// </summary>
    IReadOnlyList<FolderBookmark> GetAllBookmarks();
    
    /// <summary>
    /// Get all categories
    /// </summary>
    IReadOnlyList<BookmarkCategory> GetAllCategories();
    
    /// <summary>
    /// Get bookmarks in a specific category
    /// </summary>
    IReadOnlyList<FolderBookmark> GetBookmarksByCategory(string? categoryId);
    
    /// <summary>
    /// Get bookmark by ID
    /// </summary>
    FolderBookmark? GetBookmark(string id);
    
    /// <summary>
    /// Get bookmark by hotkey
    /// </summary>
    FolderBookmark? GetBookmarkByHotKey(string hotKey);
    
    /// <summary>
    /// Add a new bookmark
    /// </summary>
    FolderBookmark AddBookmark(string path, string? name = null, string? categoryId = null);
    
    /// <summary>
    /// Add bookmark from current selection
    /// </summary>
    FolderBookmark AddBookmarkFromPath(string path, string? name = null, string? categoryId = null, string? hotKey = null);
    
    /// <summary>
    /// Update an existing bookmark
    /// </summary>
    bool UpdateBookmark(FolderBookmark bookmark);
    
    /// <summary>
    /// Remove a bookmark
    /// </summary>
    bool RemoveBookmark(string id);
    
    /// <summary>
    /// Add a new category
    /// </summary>
    BookmarkCategory AddCategory(string name, string? description = null);
    
    /// <summary>
    /// Update an existing category
    /// </summary>
    bool UpdateCategory(BookmarkCategory category);
    
    /// <summary>
    /// Remove a category (and optionally its bookmarks)
    /// </summary>
    bool RemoveCategory(string id, bool removeBookmarks = false);
    
    /// <summary>
    /// Record access to a bookmark (for "most used" features)
    /// </summary>
    void RecordAccess(string bookmarkId);
    
    /// <summary>
    /// Get most frequently used bookmarks
    /// </summary>
    IReadOnlyList<FolderBookmark> GetMostUsed(int count = 10);
    
    /// <summary>
    /// Get recently used bookmarks
    /// </summary>
    IReadOnlyList<FolderBookmark> GetRecent(int count = 10);
    
    /// <summary>
    /// Search bookmarks by name or path
    /// </summary>
    IReadOnlyList<FolderBookmark> Search(string query);
    
    /// <summary>
    /// Import bookmarks from file
    /// </summary>
    Task<int> ImportAsync(string filePath);
    
    /// <summary>
    /// Export bookmarks to file
    /// </summary>
    Task ExportAsync(string filePath);
    
    /// <summary>
    /// Move bookmark to a different category
    /// </summary>
    bool MoveToCategory(string bookmarkId, string? categoryId);
    
    /// <summary>
    /// Reorder bookmarks within a category
    /// </summary>
    void ReorderBookmarks(IEnumerable<string> bookmarkIds, string? categoryId);
    
    /// <summary>
    /// Validate all bookmarks (check if paths exist)
    /// </summary>
    IReadOnlyList<FolderBookmark> GetInvalidBookmarks();
    
    /// <summary>
    /// Remove all invalid bookmarks
    /// </summary>
    int RemoveInvalidBookmarks();
    
    /// <summary>
    /// Save bookmarks to persistent storage
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Load bookmarks from persistent storage
    /// </summary>
    Task LoadAsync();
}

public class BookmarkService : IBookmarkService
{
    private readonly string _bookmarkFilePath;
    private BookmarkData _data = new();
    private readonly object _lock = new();
    private bool _isDirty;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public event EventHandler? BookmarksChanged;
    
    public BookmarkService(string? bookmarkFilePath = null)
    {
        _bookmarkFilePath = bookmarkFilePath ?? GetDefaultBookmarkPath();
    }
    
    private static string GetDefaultBookmarkPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XCommander", "bookmarks.json");
    }
    
    public IReadOnlyList<FolderBookmark> GetAllBookmarks()
    {
        lock (_lock)
        {
            return _data.Bookmarks.OrderBy(b => b.SortOrder).ToList();
        }
    }
    
    public IReadOnlyList<BookmarkCategory> GetAllCategories()
    {
        lock (_lock)
        {
            return _data.Categories.OrderBy(c => c.SortOrder).ToList();
        }
    }
    
    public IReadOnlyList<FolderBookmark> GetBookmarksByCategory(string? categoryId)
    {
        lock (_lock)
        {
            return _data.Bookmarks
                .Where(b => b.Category == categoryId)
                .OrderBy(b => b.SortOrder)
                .ToList();
        }
    }
    
    public FolderBookmark? GetBookmark(string id)
    {
        lock (_lock)
        {
            return _data.Bookmarks.FirstOrDefault(b => b.Id == id);
        }
    }
    
    public FolderBookmark? GetBookmarkByHotKey(string hotKey)
    {
        lock (_lock)
        {
            return _data.Bookmarks.FirstOrDefault(b => 
                string.Equals(b.HotKey, hotKey, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    public FolderBookmark AddBookmark(string path, string? name = null, string? categoryId = null)
    {
        return AddBookmarkFromPath(path, name, categoryId, null);
    }
    
    public FolderBookmark AddBookmarkFromPath(string path, string? name = null, string? categoryId = null, string? hotKey = null)
    {
        var bookmark = new FolderBookmark
        {
            Path = path,
            Name = name ?? Path.GetFileName(path) ?? path,
            Category = categoryId,
            HotKey = hotKey,
            SortOrder = GetNextSortOrder(categoryId)
        };
        
        lock (_lock)
        {
            _data.Bookmarks.Add(bookmark);
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return bookmark;
    }
    
    public bool UpdateBookmark(FolderBookmark bookmark)
    {
        lock (_lock)
        {
            var existing = _data.Bookmarks.FirstOrDefault(b => b.Id == bookmark.Id);
            if (existing == null)
                return false;
            
            var index = _data.Bookmarks.IndexOf(existing);
            _data.Bookmarks[index] = bookmark;
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return true;
    }
    
    public bool RemoveBookmark(string id)
    {
        lock (_lock)
        {
            var bookmark = _data.Bookmarks.FirstOrDefault(b => b.Id == id);
            if (bookmark == null)
                return false;
            
            _data.Bookmarks.Remove(bookmark);
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return true;
    }
    
    public BookmarkCategory AddCategory(string name, string? description = null)
    {
        var category = new BookmarkCategory
        {
            Name = name,
            Description = description,
            SortOrder = GetNextCategorySortOrder()
        };
        
        lock (_lock)
        {
            _data.Categories.Add(category);
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return category;
    }
    
    public bool UpdateCategory(BookmarkCategory category)
    {
        lock (_lock)
        {
            var existing = _data.Categories.FirstOrDefault(c => c.Id == category.Id);
            if (existing == null)
                return false;
            
            var index = _data.Categories.IndexOf(existing);
            _data.Categories[index] = category;
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return true;
    }
    
    public bool RemoveCategory(string id, bool removeBookmarks = false)
    {
        lock (_lock)
        {
            var category = _data.Categories.FirstOrDefault(c => c.Id == id);
            if (category == null)
                return false;
            
            _data.Categories.Remove(category);
            
            if (removeBookmarks)
            {
                _data.Bookmarks.RemoveAll(b => b.Category == id);
            }
            else
            {
                // Move bookmarks to uncategorized
                foreach (var bookmark in _data.Bookmarks.Where(b => b.Category == id))
                {
                    bookmark.Category = null;
                }
            }
            
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return true;
    }
    
    public void RecordAccess(string bookmarkId)
    {
        lock (_lock)
        {
            var bookmark = _data.Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark != null)
            {
                bookmark.AccessCount++;
                bookmark.LastAccessedAt = DateTime.UtcNow;
                _isDirty = true;
            }
        }
    }
    
    public IReadOnlyList<FolderBookmark> GetMostUsed(int count = 10)
    {
        lock (_lock)
        {
            return _data.Bookmarks
                .OrderByDescending(b => b.AccessCount)
                .Take(count)
                .ToList();
        }
    }
    
    public IReadOnlyList<FolderBookmark> GetRecent(int count = 10)
    {
        lock (_lock)
        {
            return _data.Bookmarks
                .OrderByDescending(b => b.LastAccessedAt)
                .Take(count)
                .ToList();
        }
    }
    
    public IReadOnlyList<FolderBookmark> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllBookmarks();
        
        lock (_lock)
        {
            return _data.Bookmarks
                .Where(b => 
                    b.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    b.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (b.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(b => b.SortOrder)
                .ToList();
        }
    }
    
    public async Task<int> ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importData = JsonSerializer.Deserialize<BookmarkData>(json, JsonOptions);
        
        if (importData == null)
            return 0;
        
        var count = 0;
        
        lock (_lock)
        {
            // Import categories (skip existing)
            foreach (var category in importData.Categories)
            {
                if (!_data.Categories.Any(c => c.Name == category.Name))
                {
                    _data.Categories.Add(category);
                }
            }
            
            // Import bookmarks (skip duplicates by path)
            foreach (var bookmark in importData.Bookmarks)
            {
                if (!_data.Bookmarks.Any(b => b.Path == bookmark.Path))
                {
                    _data.Bookmarks.Add(bookmark);
                    count++;
                }
            }
            
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return count;
    }
    
    public async Task ExportAsync(string filePath)
    {
        BookmarkData exportData;
        
        lock (_lock)
        {
            exportData = new BookmarkData
            {
                Version = _data.Version,
                Categories = new List<BookmarkCategory>(_data.Categories),
                Bookmarks = new List<FolderBookmark>(_data.Bookmarks),
                LastModified = DateTime.UtcNow
            };
        }
        
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public bool MoveToCategory(string bookmarkId, string? categoryId)
    {
        lock (_lock)
        {
            var bookmark = _data.Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark == null)
                return false;
            
            bookmark.Category = categoryId;
            bookmark.SortOrder = GetNextSortOrder(categoryId);
            _isDirty = true;
        }
        
        OnBookmarksChanged();
        return true;
    }
    
    public void ReorderBookmarks(IEnumerable<string> bookmarkIds, string? categoryId)
    {
        lock (_lock)
        {
            var ids = bookmarkIds.ToList();
            for (var i = 0; i < ids.Count; i++)
            {
                var bookmark = _data.Bookmarks.FirstOrDefault(b => b.Id == ids[i]);
                if (bookmark != null && bookmark.Category == categoryId)
                {
                    bookmark.SortOrder = i;
                }
            }
            _isDirty = true;
        }
        
        OnBookmarksChanged();
    }
    
    public IReadOnlyList<FolderBookmark> GetInvalidBookmarks()
    {
        lock (_lock)
        {
            return _data.Bookmarks
                .Where(b => !b.Exists)
                .ToList();
        }
    }
    
    public int RemoveInvalidBookmarks()
    {
        int count;
        
        lock (_lock)
        {
            count = _data.Bookmarks.RemoveAll(b => !b.Exists);
            if (count > 0)
                _isDirty = true;
        }
        
        if (count > 0)
            OnBookmarksChanged();
        
        return count;
    }
    
    public async Task SaveAsync()
    {
        BookmarkData? dataToSave;
        
        lock (_lock)
        {
            if (!_isDirty)
                return;
            
            _data.LastModified = DateTime.UtcNow;
            dataToSave = new BookmarkData
            {
                Version = _data.Version,
                Categories = new List<BookmarkCategory>(_data.Categories),
                Bookmarks = new List<FolderBookmark>(_data.Bookmarks),
                LastModified = _data.LastModified
            };
            _isDirty = false;
        }
        
        var directory = Path.GetDirectoryName(_bookmarkFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        var json = JsonSerializer.Serialize(dataToSave, JsonOptions);
        await File.WriteAllTextAsync(_bookmarkFilePath, json);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(_bookmarkFilePath))
        {
            await InitializeDefaultBookmarksAsync();
            return;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_bookmarkFilePath);
            var data = JsonSerializer.Deserialize<BookmarkData>(json, JsonOptions);
            
            lock (_lock)
            {
                _data = data ?? new BookmarkData();
                _isDirty = false;
            }
        }
        catch
        {
            // If loading fails, start with defaults
            await InitializeDefaultBookmarksAsync();
        }
        
        OnBookmarksChanged();
    }
    
    private async Task InitializeDefaultBookmarksAsync()
    {
        lock (_lock)
        {
            _data = new BookmarkData();
            
            // Add default categories
            var systemCategory = new BookmarkCategory { Name = "System", SortOrder = 0 };
            var userCategory = new BookmarkCategory { Name = "User", SortOrder = 1 };
            
            _data.Categories.Add(systemCategory);
            _data.Categories.Add(userCategory);
            
            // Add some default bookmarks
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var downloadsDir = Path.Combine(homeDir, "Downloads");
            
            _data.Bookmarks.Add(new FolderBookmark
            {
                Name = "Home",
                Path = homeDir,
                Category = userCategory.Id,
                HotKey = "Ctrl+1",
                SortOrder = 0
            });
            
            _data.Bookmarks.Add(new FolderBookmark
            {
                Name = "Desktop",
                Path = desktopDir,
                Category = userCategory.Id,
                HotKey = "Ctrl+2",
                SortOrder = 1
            });
            
            _data.Bookmarks.Add(new FolderBookmark
            {
                Name = "Documents",
                Path = documentsDir,
                Category = userCategory.Id,
                HotKey = "Ctrl+3",
                SortOrder = 2
            });
            
            if (Directory.Exists(downloadsDir))
            {
                _data.Bookmarks.Add(new FolderBookmark
                {
                    Name = "Downloads",
                    Path = downloadsDir,
                    Category = userCategory.Id,
                    HotKey = "Ctrl+4",
                    SortOrder = 3
                });
            }
            
            // Add system locations
            var rootPath = Environment.OSVersion.Platform == PlatformID.Win32NT ? "C:\\" : "/";
            _data.Bookmarks.Add(new FolderBookmark
            {
                Name = "Root",
                Path = rootPath,
                Category = systemCategory.Id,
                SortOrder = 0
            });
            
            _isDirty = true;
        }
        
        await SaveAsync();
    }
    
    private int GetNextSortOrder(string? categoryId)
    {
        var bookmarks = _data.Bookmarks.Where(b => b.Category == categoryId).ToList();
        return bookmarks.Any() ? bookmarks.Max(b => b.SortOrder) + 1 : 0;
    }
    
    private int GetNextCategorySortOrder()
    {
        return _data.Categories.Any() ? _data.Categories.Max(c => c.SortOrder) + 1 : 0;
    }
    
    private void OnBookmarksChanged()
    {
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }
}
