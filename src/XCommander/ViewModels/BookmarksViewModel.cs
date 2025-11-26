using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;

namespace XCommander.ViewModels;

public partial class BookmarkItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _path = string.Empty;
    
    [ObservableProperty]
    private string _icon = "📁";
    
    [ObservableProperty]
    private string? _category;
    
    [ObservableProperty]
    private string? _shortcut;
    
    public Bookmark ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Path = Path,
        Icon = Icon,
        Category = Category,
        Shortcut = Shortcut
    };
    
    public static BookmarkItemViewModel FromModel(Bookmark model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Path = model.Path,
        Icon = model.Icon ?? "📁",
        Category = model.Category,
        Shortcut = model.Shortcut
    };
}

public partial class BookmarksViewModel : ViewModelBase
{
    private static readonly string BookmarksFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "bookmarks.json");
    
    [ObservableProperty]
    private ObservableCollection<BookmarkItemViewModel> _bookmarks = new();
    
    [ObservableProperty]
    private ObservableCollection<string> _recentLocations = new();
    
    [ObservableProperty]
    private BookmarkItemViewModel? _selectedBookmark;
    
    [ObservableProperty]
    private string _newBookmarkName = string.Empty;
    
    [ObservableProperty]
    private string _newBookmarkPath = string.Empty;
    
    public event EventHandler<string>? NavigateRequested;

    public BookmarksViewModel()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(BookmarksFilePath))
            {
                var json = File.ReadAllText(BookmarksFilePath);
                var data = JsonSerializer.Deserialize<BookmarksData>(json);
                
                if (data != null)
                {
                    Bookmarks.Clear();
                    foreach (var bookmark in data.UncategorizedBookmarks)
                    {
                        Bookmarks.Add(BookmarkItemViewModel.FromModel(bookmark));
                    }
                    
                    RecentLocations.Clear();
                    foreach (var location in data.RecentLocations)
                    {
                        RecentLocations.Add(location);
                    }
                }
            }
            else
            {
                // Add some default bookmarks
                AddDefaultBookmarks();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading bookmarks: {ex.Message}");
            AddDefaultBookmarks();
        }
    }

    private void AddDefaultBookmarks()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloadsDir = Path.Combine(homeDir, "Downloads");
        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        
        Bookmarks.Add(new BookmarkItemViewModel { Id = "home", Name = "Home", Path = homeDir, Icon = "🏠" });
        Bookmarks.Add(new BookmarkItemViewModel { Id = "documents", Name = "Documents", Path = documentsDir, Icon = "📄" });
        Bookmarks.Add(new BookmarkItemViewModel { Id = "downloads", Name = "Downloads", Path = downloadsDir, Icon = "⬇️" });
        Bookmarks.Add(new BookmarkItemViewModel { Id = "desktop", Name = "Desktop", Path = desktopDir, Icon = "🖥️" });
        
        if (OperatingSystem.IsMacOS())
        {
            Bookmarks.Add(new BookmarkItemViewModel { Id = "applications", Name = "Applications", Path = "/Applications", Icon = "📦" });
        }
        else if (OperatingSystem.IsLinux())
        {
            Bookmarks.Add(new BookmarkItemViewModel { Id = "root", Name = "Root", Path = "/", Icon = "💾" });
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(BookmarksFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var data = new BookmarksData
            {
                UncategorizedBookmarks = Bookmarks.Select(b => b.ToModel()).ToList(),
                RecentLocations = RecentLocations.ToList()
            };
            
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BookmarksFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving bookmarks: {ex.Message}");
        }
    }

    [RelayCommand]
    public void AddBookmark()
    {
        if (string.IsNullOrWhiteSpace(NewBookmarkPath))
            return;
            
        var name = string.IsNullOrWhiteSpace(NewBookmarkName) 
            ? Path.GetFileName(NewBookmarkPath) ?? NewBookmarkPath
            : NewBookmarkName;
            
        var bookmark = new BookmarkItemViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Path = NewBookmarkPath,
            Icon = Directory.Exists(NewBookmarkPath) ? "📁" : "📄"
        };
        
        Bookmarks.Add(bookmark);
        Save();
        
        NewBookmarkName = string.Empty;
        NewBookmarkPath = string.Empty;
    }

    public void AddBookmark(string path, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
            
        // Check if already exists
        if (Bookmarks.Any(b => b.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;
            
        var bookmarkName = name ?? Path.GetFileName(path) ?? path;
        
        var bookmark = new BookmarkItemViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = bookmarkName,
            Path = path,
            Icon = Directory.Exists(path) ? "📁" : "📄"
        };
        
        Bookmarks.Add(bookmark);
        Save();
    }

    [RelayCommand]
    public void RemoveBookmark(BookmarkItemViewModel? bookmark)
    {
        if (bookmark == null)
            return;
            
        Bookmarks.Remove(bookmark);
        Save();
    }

    [RelayCommand]
    public void NavigateToBookmark(BookmarkItemViewModel? bookmark)
    {
        if (bookmark == null || string.IsNullOrEmpty(bookmark.Path))
            return;
            
        NavigateRequested?.Invoke(this, bookmark.Path);
    }

    [RelayCommand]
    public void MoveBookmarkUp(BookmarkItemViewModel? bookmark)
    {
        if (bookmark == null)
            return;
            
        var index = Bookmarks.IndexOf(bookmark);
        if (index > 0)
        {
            Bookmarks.Move(index, index - 1);
            Save();
        }
    }

    [RelayCommand]
    public void MoveBookmarkDown(BookmarkItemViewModel? bookmark)
    {
        if (bookmark == null)
            return;
            
        var index = Bookmarks.IndexOf(bookmark);
        if (index < Bookmarks.Count - 1)
        {
            Bookmarks.Move(index, index + 1);
            Save();
        }
    }

    public void AddRecentLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
            
        // Remove if already exists
        var existing = RecentLocations.FirstOrDefault(l => l.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            RecentLocations.Remove(existing);
        }
        
        // Add at the beginning
        RecentLocations.Insert(0, path);
        
        // Limit to max
        while (RecentLocations.Count > 20)
        {
            RecentLocations.RemoveAt(RecentLocations.Count - 1);
        }
        
        Save();
    }

    [RelayCommand]
    public void ClearRecentLocations()
    {
        RecentLocations.Clear();
        Save();
    }

    [RelayCommand]
    public void NavigateToRecent(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
            
        NavigateRequested?.Invoke(this, path);
    }
}
