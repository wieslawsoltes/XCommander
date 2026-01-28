using System.Text.Json;

namespace XCommander.Services;

/// <summary>
/// Implementation of directory hotlist service
/// </summary>
public class DirectoryHotlistService : IDirectoryHotlistService
{
    private readonly string _dataPath;
    private readonly string _hotlistPath;
    private readonly string _recentPath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private List<HotlistItem> _items = new();
    private List<HotlistCategory> _categories = new();
    private List<RecentFolderEntry> _recentFolders = new();
    
    private const string FavoritesCategoryId = "__favorites__";
    
    public event EventHandler? HotlistChanged;
    
    public DirectoryHotlistService()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander");
        Directory.CreateDirectory(_dataPath);
        
        _hotlistPath = Path.Combine(_dataPath, "hotlist.json");
        _recentPath = Path.Combine(_dataPath, "recent_folders.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        LoadData();
    }
    
    private void LoadData()
    {
        try
        {
            if (File.Exists(_hotlistPath))
            {
                var json = File.ReadAllText(_hotlistPath);
                var data = JsonSerializer.Deserialize<HotlistData>(json, _jsonOptions);
                if (data != null)
                {
                    _items = data.Items?.ToList() ?? new();
                    _categories = data.Categories?.ToList() ?? new();
                }
            }
        }
        catch
        {
            _items = new();
            _categories = new();
        }
        
        try
        {
            if (File.Exists(_recentPath))
            {
                var json = File.ReadAllText(_recentPath);
                _recentFolders = JsonSerializer.Deserialize<List<RecentFolderEntry>>(json, _jsonOptions) ?? new();
            }
        }
        catch
        {
            _recentFolders = new();
        }
        
        // Ensure favorites category exists
        if (!_categories.Any(c => c.Id == FavoritesCategoryId))
        {
            _categories.Insert(0, new HotlistCategory
            {
                Id = FavoritesCategoryId,
                Name = "Favorites",
                Icon = "⭐",
                Order = -1
            });
        }
    }
    
    private async Task SaveHotlistAsync()
    {
        var data = new HotlistData
        {
            Items = _items,
            Categories = _categories
        };
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(_hotlistPath, json);
        HotlistChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private async Task SaveRecentAsync()
    {
        var json = JsonSerializer.Serialize(_recentFolders, _jsonOptions);
        await File.WriteAllTextAsync(_recentPath, json);
    }
    
    #region Hotlist Items
    
    public Task<IReadOnlyList<HotlistItem>> GetItemsAsync()
    {
        return Task.FromResult<IReadOnlyList<HotlistItem>>(
            _items.OrderBy(i => i.Order).ToList());
    }
    
    public Task<IReadOnlyList<HotlistItem>> GetItemsInCategoryAsync(string? categoryId)
    {
        return Task.FromResult<IReadOnlyList<HotlistItem>>(
            _items.Where(i => i.ParentCategoryId == categoryId)
                .OrderBy(i => i.Order)
                .ToList());
    }
    
    public async Task<HotlistItem> AddDirectoryAsync(string path, string? name = null, string? categoryId = null)
    {
        var displayName = name ?? Path.GetFileName(path);
        if (string.IsNullOrEmpty(displayName))
            displayName = path;
        
        var maxOrder = _items.Where(i => i.ParentCategoryId == categoryId)
            .Select(i => i.Order)
            .DefaultIfEmpty(-1)
            .Max();
        
        var item = new HotlistItem
        {
            Type = HotlistItemType.Directory,
            Name = displayName,
            Path = path,
            ParentCategoryId = categoryId,
            Order = maxOrder + 1,
            Created = DateTime.Now
        };
        
        _items.Add(item);
        await SaveHotlistAsync();
        
        return item;
    }

    public async Task<HotlistItem> AddSeparatorAsync(string? categoryId = null)
    {
        var maxOrder = _items.Where(i => i.ParentCategoryId == categoryId)
            .Select(i => i.Order)
            .DefaultIfEmpty(-1)
            .Max();

        var item = new HotlistItem
        {
            Type = HotlistItemType.Separator,
            Name = string.Empty,
            ParentCategoryId = categoryId,
            Order = maxOrder + 1,
            Created = DateTime.Now
        };

        _items.Add(item);
        await SaveHotlistAsync();

        return item;
    }
    
    public async Task UpdateItemAsync(HotlistItem item)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            _items[index] = item;
            await SaveHotlistAsync();
        }
    }
    
    public async Task RemoveItemAsync(string itemId)
    {
        _items.RemoveAll(i => i.Id == itemId);
        await SaveHotlistAsync();
    }
    
    public async Task MoveItemToCategoryAsync(string itemId, string? categoryId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var maxOrder = _items.Where(i => i.ParentCategoryId == categoryId)
                .Select(i => i.Order)
                .DefaultIfEmpty(-1)
                .Max();
            
            var index = _items.IndexOf(item);
            _items[index] = item with
            {
                ParentCategoryId = categoryId,
                Order = maxOrder + 1
            };
            await SaveHotlistAsync();
        }
    }
    
    public async Task ReorderItemsAsync(IReadOnlyList<string> itemIds)
    {
        for (var i = 0; i < itemIds.Count; i++)
        {
            var item = _items.FirstOrDefault(it => it.Id == itemIds[i]);
            if (item != null)
            {
                var index = _items.IndexOf(item);
                _items[index] = item with { Order = i };
            }
        }
        await SaveHotlistAsync();
    }
    
    public async Task SetItemShortcutAsync(string itemId, string? shortcut)
    {
        // Remove shortcut from any other item that has it
        if (!string.IsNullOrEmpty(shortcut))
        {
            foreach (var other in _items.Where(i => i.KeyboardShortcut == shortcut))
            {
                var idx = _items.IndexOf(other);
                _items[idx] = other with { KeyboardShortcut = null };
            }
        }
        
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var index = _items.IndexOf(item);
            _items[index] = item with { KeyboardShortcut = shortcut };
            await SaveHotlistAsync();
        }
    }
    
    public async Task RecordAccessAsync(string itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var index = _items.IndexOf(item);
            _items[index] = item with
            {
                LastAccessed = DateTime.Now,
                AccessCount = item.AccessCount + 1
            };
            await SaveHotlistAsync();
        }
    }
    
    #endregion
    
    #region Categories
    
    public Task<IReadOnlyList<HotlistCategory>> GetCategoriesAsync()
    {
        return Task.FromResult<IReadOnlyList<HotlistCategory>>(
            _categories.OrderBy(c => c.Order).ToList());
    }
    
    public Task<IReadOnlyList<HotlistCategory>> GetCategoryTreeAsync()
    {
        var rootCategories = _categories
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.Order)
            .Select(c => BuildCategoryTree(c))
            .ToList();
        
        return Task.FromResult<IReadOnlyList<HotlistCategory>>(rootCategories);
    }
    
    private HotlistCategory BuildCategoryTree(HotlistCategory category)
    {
        var items = _items.Where(i => i.ParentCategoryId == category.Id)
            .OrderBy(i => i.Order)
            .ToList();
        
        var subCategories = _categories
            .Where(c => c.ParentCategoryId == category.Id)
            .OrderBy(c => c.Order)
            .Select(c => BuildCategoryTree(c))
            .ToList();
        
        return category with
        {
            Items = items,
            SubCategories = subCategories
        };
    }
    
    public async Task<HotlistCategory> CreateCategoryAsync(string name, string? parentCategoryId = null)
    {
        var maxOrder = _categories.Where(c => c.ParentCategoryId == parentCategoryId)
            .Select(c => c.Order)
            .DefaultIfEmpty(-1)
            .Max();
        
        var category = new HotlistCategory
        {
            Name = name,
            ParentCategoryId = parentCategoryId,
            Order = maxOrder + 1
        };
        
        _categories.Add(category);
        await SaveHotlistAsync();
        
        return category;
    }
    
    public async Task UpdateCategoryAsync(HotlistCategory category)
    {
        var index = _categories.FindIndex(c => c.Id == category.Id);
        if (index >= 0)
        {
            _categories[index] = category;
            await SaveHotlistAsync();
        }
    }
    
    public async Task DeleteCategoryAsync(string categoryId, bool deleteContents = false)
    {
        if (categoryId == FavoritesCategoryId)
            return; // Cannot delete favorites
        
        if (deleteContents)
        {
            // Delete all items in the category
            _items.RemoveAll(i => i.ParentCategoryId == categoryId);
            
            // Recursively delete sub-categories
            var subCategories = _categories.Where(c => c.ParentCategoryId == categoryId).ToList();
            foreach (var sub in subCategories)
            {
                await DeleteCategoryAsync(sub.Id, true);
            }
        }
        else
        {
            // Move items to root
            foreach (var item in _items.Where(i => i.ParentCategoryId == categoryId))
            {
                var index = _items.IndexOf(item);
                _items[index] = item with { ParentCategoryId = null };
            }
            
            // Move sub-categories to parent
            var category = _categories.FirstOrDefault(c => c.Id == categoryId);
            foreach (var sub in _categories.Where(c => c.ParentCategoryId == categoryId))
            {
                var index = _categories.IndexOf(sub);
                _categories[index] = sub with { ParentCategoryId = category?.ParentCategoryId };
            }
        }
        
        _categories.RemoveAll(c => c.Id == categoryId);
        await SaveHotlistAsync();
    }
    
    public async Task RenameCategoryAsync(string categoryId, string newName)
    {
        var category = _categories.FirstOrDefault(c => c.Id == categoryId);
        if (category != null)
        {
            var index = _categories.IndexOf(category);
            _categories[index] = category with { Name = newName };
            await SaveHotlistAsync();
        }
    }
    
    #endregion
    
    #region Recent Folders
    
    public Task<IReadOnlyList<RecentFolderEntry>> GetRecentFoldersAsync(int maxCount = 20)
    {
        return Task.FromResult<IReadOnlyList<RecentFolderEntry>>(
            _recentFolders.OrderByDescending(r => r.LastAccessed)
                .Take(maxCount)
                .ToList());
    }
    
    public async Task AddRecentFolderAsync(string path)
    {
        var existing = _recentFolders.FirstOrDefault(r => 
            r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        
        if (existing != null)
        {
            var index = _recentFolders.IndexOf(existing);
            _recentFolders[index] = existing with
            {
                LastAccessed = DateTime.Now,
                AccessCount = existing.AccessCount + 1
            };
        }
        else
        {
            _recentFolders.Add(new RecentFolderEntry
            {
                Path = path,
                DisplayName = Path.GetFileName(path),
                LastAccessed = DateTime.Now,
                AccessCount = 1
            });
        }
        
        // Keep only last 100
        while (_recentFolders.Count > 100)
        {
            var oldest = _recentFolders.OrderBy(r => r.LastAccessed).First();
            _recentFolders.Remove(oldest);
        }
        
        await SaveRecentAsync();
    }
    
    public async Task ClearRecentFoldersAsync()
    {
        _recentFolders.Clear();
        await SaveRecentAsync();
    }
    
    public async Task RemoveRecentFolderAsync(string path)
    {
        _recentFolders.RemoveAll(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        await SaveRecentAsync();
    }
    
    #endregion
    
    #region Favorites
    
    public Task<IReadOnlyList<HotlistItem>> GetFavoritesAsync()
    {
        return GetItemsInCategoryAsync(FavoritesCategoryId);
    }
    
    public async Task AddToFavoritesAsync(string path)
    {
        // Check if already in favorites
        var existing = _items.FirstOrDefault(i => 
            i.ParentCategoryId == FavoritesCategoryId &&
            i.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
        
        if (existing == null)
        {
            await AddDirectoryAsync(path, null, FavoritesCategoryId);
        }
    }
    
    public async Task RemoveFromFavoritesAsync(string path)
    {
        var item = _items.FirstOrDefault(i => 
            i.ParentCategoryId == FavoritesCategoryId &&
            i.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
        
        if (item != null)
        {
            await RemoveItemAsync(item.Id);
        }
    }
    
    public Task<bool> IsFavoriteAsync(string path)
    {
        var isFavorite = _items.Any(i => 
            i.ParentCategoryId == FavoritesCategoryId &&
            i.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
        
        return Task.FromResult(isFavorite);
    }
    
    #endregion
    
    #region Search
    
    public Task<IReadOnlyList<HotlistItem>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<HotlistItem>>(Array.Empty<HotlistItem>());
        
        var results = _items.Where(i =>
            (i.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
            (i.Path?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
            (i.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .ToList();
        
        return Task.FromResult<IReadOnlyList<HotlistItem>>(results);
    }
    
    public Task<HotlistItem?> GetItemByShortcutAsync(string shortcut)
    {
        var item = _items.FirstOrDefault(i => 
            i.KeyboardShortcut?.Equals(shortcut, StringComparison.OrdinalIgnoreCase) == true);
        
        return Task.FromResult(item);
    }
    
    #endregion
    
    #region Import/Export
    
    public Task<HotlistExportData> ExportAsync()
    {
        return Task.FromResult(new HotlistExportData
        {
            Items = _items.ToList(),
            Categories = _categories.ToList(),
            ExportedAt = DateTime.Now
        });
    }
    
    public async Task ImportAsync(HotlistExportData data, bool merge = false)
    {
        if (!merge)
        {
            _items.Clear();
            _categories.Clear();
        }
        
        // Assign new IDs to avoid conflicts when merging
        var idMapping = new Dictionary<string, string>();
        
        foreach (var category in data.Categories ?? Array.Empty<HotlistCategory>())
        {
            var newId = merge ? Guid.NewGuid().ToString() : category.Id;
            idMapping[category.Id] = newId;
            
            var parentId = category.ParentCategoryId != null && idMapping.TryGetValue(category.ParentCategoryId, out var mappedParent)
                ? mappedParent
                : category.ParentCategoryId;
            
            _categories.Add(category with
            {
                Id = newId,
                ParentCategoryId = parentId
            });
        }
        
        foreach (var item in data.Items ?? Array.Empty<HotlistItem>())
        {
            var newId = merge ? Guid.NewGuid().ToString() : item.Id;
            var categoryId = item.ParentCategoryId != null && idMapping.TryGetValue(item.ParentCategoryId, out var mappedCat)
                ? mappedCat
                : item.ParentCategoryId;
            
            _items.Add(item with
            {
                Id = newId,
                ParentCategoryId = categoryId
            });
        }
        
        await SaveHotlistAsync();
    }
    
    public async Task ExportToFileAsync(string filePath)
    {
        var data = await ExportAsync();
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task ImportFromFileAsync(string filePath, bool merge = false)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<HotlistExportData>(json, _jsonOptions);
        
        if (data != null)
        {
            await ImportAsync(data, merge);
        }
    }
    
    #endregion
    
    private class HotlistData
    {
        public List<HotlistItem>? Items { get; init; }
        public List<HotlistCategory>? Categories { get; init; }
    }
}
