using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class BookmarksViewModelTests
{
    private readonly BookmarksViewModel _viewModel;
    
    public BookmarksViewModelTests()
    {
        _viewModel = new BookmarksViewModel();
        // Clear defaults for clean tests
        _viewModel.Bookmarks.Clear();
    }
    
    [Fact]
    public void AddBookmark_AddsToCollection()
    {
        // Arrange
        var initialCount = _viewModel.Bookmarks.Count;
        
        // Act
        _viewModel.AddBookmark("/test/path", "Test Bookmark");
        
        // Assert
        Assert.Equal(initialCount + 1, _viewModel.Bookmarks.Count);
    }
    
    [Fact]
    public void AddBookmark_SetsCorrectProperties()
    {
        // Act
        _viewModel.AddBookmark("/test/path", "Test Bookmark");
        
        // Assert
        var bookmark = _viewModel.Bookmarks.First();
        Assert.Equal("/test/path", bookmark.Path);
        Assert.Equal("Test Bookmark", bookmark.Name);
    }
    
    [Fact]
    public void RemoveBookmark_RemovesFromCollection()
    {
        // Arrange
        _viewModel.AddBookmark("/test/path", "Test Bookmark");
        var bookmark = _viewModel.Bookmarks.First();
        var initialCount = _viewModel.Bookmarks.Count;
        
        // Act
        _viewModel.RemoveBookmark(bookmark);
        
        // Assert
        Assert.Equal(initialCount - 1, _viewModel.Bookmarks.Count);
    }
    
    [Fact]
    public void AddRecentLocation_AddsToCollection()
    {
        // Arrange
        _viewModel.RecentLocations.Clear();
        
        // Act
        _viewModel.AddRecentLocation("/recent/path");
        
        // Assert
        Assert.Single(_viewModel.RecentLocations);
    }
    
    [Fact]
    public void AddRecentLocation_DoesNotDuplicatePath()
    {
        // Arrange
        _viewModel.RecentLocations.Clear();
        _viewModel.AddRecentLocation("/recent/path");
        
        // Act
        _viewModel.AddRecentLocation("/recent/path");
        
        // Assert
        Assert.Single(_viewModel.RecentLocations);
    }
    
    [Fact]
    public void AddRecentLocation_MovesExistingPathToTop()
    {
        // Arrange
        _viewModel.RecentLocations.Clear();
        _viewModel.AddRecentLocation("/path1");
        _viewModel.AddRecentLocation("/path2");
        _viewModel.AddRecentLocation("/path3");
        
        // Act
        _viewModel.AddRecentLocation("/path1");
        
        // Assert
        Assert.Equal("/path1", _viewModel.RecentLocations.First());
    }
    
    [Fact]
    public void AddRecentLocation_LimitsCollectionSize()
    {
        // Arrange
        _viewModel.RecentLocations.Clear();
        
        // Act - Add more than the limit (20)
        for (int i = 0; i < 25; i++)
        {
            _viewModel.AddRecentLocation($"/path{i}");
        }
        
        // Assert
        Assert.True(_viewModel.RecentLocations.Count <= 20);
    }
    
    [Fact]
    public void ClearRecentLocations_ClearsCollection()
    {
        // Arrange
        _viewModel.AddRecentLocation("/path1");
        _viewModel.AddRecentLocation("/path2");
        
        // Act
        _viewModel.ClearRecentLocations();
        
        // Assert
        Assert.Empty(_viewModel.RecentLocations);
    }
    
    [Fact]
    public void AddBookmark_DoesNotAddDuplicate()
    {
        // Arrange
        _viewModel.AddBookmark("/test/path", "Test1");
        var initialCount = _viewModel.Bookmarks.Count;
        
        // Act
        _viewModel.AddBookmark("/test/path", "Test2");
        
        // Assert
        Assert.Equal(initialCount, _viewModel.Bookmarks.Count);
    }
    
    [Fact]
    public void MoveBookmarkUp_MovesBookmarkUp()
    {
        // Arrange
        _viewModel.AddBookmark("/path1", "First");
        _viewModel.AddBookmark("/path2", "Second");
        var secondBookmark = _viewModel.Bookmarks.Last();
        
        // Act
        _viewModel.MoveBookmarkUp(secondBookmark);
        
        // Assert
        Assert.Same(secondBookmark, _viewModel.Bookmarks.First());
    }
    
    [Fact]
    public void MoveBookmarkDown_MovesBookmarkDown()
    {
        // Arrange
        _viewModel.AddBookmark("/path1", "First");
        _viewModel.AddBookmark("/path2", "Second");
        var firstBookmark = _viewModel.Bookmarks.First();
        
        // Act
        _viewModel.MoveBookmarkDown(firstBookmark);
        
        // Assert
        Assert.Same(firstBookmark, _viewModel.Bookmarks.Last());
    }
    
    [Fact]
    public void NavigateToBookmark_RaisesNavigateRequestedEvent()
    {
        // Arrange
        _viewModel.AddBookmark("/test/path", "Test");
        var bookmark = _viewModel.Bookmarks.First();
        string? navigatedPath = null;
        _viewModel.NavigateRequested += (_, path) => navigatedPath = path;
        
        // Act
        _viewModel.NavigateToBookmark(bookmark);
        
        // Assert
        Assert.Equal("/test/path", navigatedPath);
    }
}
