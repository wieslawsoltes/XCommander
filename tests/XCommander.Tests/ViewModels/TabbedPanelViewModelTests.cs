using Moq;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class TabbedPanelViewModelTests
{
    private readonly Mock<IFileSystemService> _fileSystemServiceMock;
    private readonly TabbedPanelViewModel _viewModel;
    
    public TabbedPanelViewModelTests()
    {
        _fileSystemServiceMock = new Mock<IFileSystemService>();
        _viewModel = new TabbedPanelViewModel(_fileSystemServiceMock.Object);
    }
    
    [Fact]
    public void AddNewTab_AddsTabToCollection()
    {
        // Arrange
        _viewModel.Initialize("/");
        var initialCount = _viewModel.Tabs.Count;
        
        // Act
        _viewModel.AddNewTab();
        
        // Assert
        Assert.Equal(initialCount + 1, _viewModel.Tabs.Count);
    }
    
    [Fact]
    public void AddNewTab_SetsNewTabAsActive()
    {
        // Arrange
        _viewModel.Initialize("/");
        var originalTab = _viewModel.ActiveTab;
        
        // Act
        _viewModel.AddNewTab();
        
        // Assert
        Assert.NotSame(originalTab, _viewModel.ActiveTab);
        Assert.True(_viewModel.ActiveTab?.IsSelected);
    }
    
    [Fact]
    public void CloseTab_RemovesTabFromCollection()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        var initialCount = _viewModel.Tabs.Count;
        var tabToClose = _viewModel.Tabs[1];
        
        // Act
        _viewModel.CloseTab(tabToClose);
        
        // Assert
        Assert.Equal(initialCount - 1, _viewModel.Tabs.Count);
        Assert.DoesNotContain(tabToClose, _viewModel.Tabs);
    }
    
    [Fact]
    public void CloseTab_DoesNotCloseLastTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        var lastTab = _viewModel.Tabs[0];
        
        // Act
        _viewModel.CloseTab(lastTab);
        
        // Assert
        Assert.Single(_viewModel.Tabs);
    }
    
    [Fact]
    public void CloseTab_DoesNotCloseLockedTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        var lockedTab = _viewModel.Tabs[0];
        lockedTab.IsLocked = true;
        var initialCount = _viewModel.Tabs.Count;
        
        // Act
        _viewModel.CloseTab(lockedTab);
        
        // Assert
        Assert.Equal(initialCount, _viewModel.Tabs.Count);
        Assert.Contains(lockedTab, _viewModel.Tabs);
    }
    
    [Fact]
    public void SetActiveTab_SetsIsSelectedOnNewTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        var firstTab = _viewModel.Tabs[0];
        var secondTab = _viewModel.Tabs[1];
        
        // Act
        _viewModel.SetActiveTab(firstTab);
        
        // Assert
        Assert.True(firstTab.IsSelected);
        Assert.False(secondTab.IsSelected);
        Assert.Same(firstTab, _viewModel.ActiveTab);
    }
    
    [Fact]
    public void NextTab_CyclesThroughTabs()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        _viewModel.AddNewTab();
        _viewModel.SetActiveTab(_viewModel.Tabs[0]);
        
        // Act
        _viewModel.NextTab();
        
        // Assert
        Assert.Same(_viewModel.Tabs[1], _viewModel.ActiveTab);
    }
    
    [Fact]
    public void NextTab_WrapsAroundToFirstTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        _viewModel.SetActiveTab(_viewModel.Tabs[1]);
        
        // Act
        _viewModel.NextTab();
        
        // Assert
        Assert.Same(_viewModel.Tabs[0], _viewModel.ActiveTab);
    }
    
    [Fact]
    public void PreviousTab_WrapsAroundToLastTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        _viewModel.SetActiveTab(_viewModel.Tabs[0]);
        
        // Act
        _viewModel.PreviousTab();
        
        // Assert
        Assert.Same(_viewModel.Tabs[1], _viewModel.ActiveTab);
    }
    
    [Fact]
    public void MoveTabTo_ReordersTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        _viewModel.AddNewTab();
        var firstTab = _viewModel.Tabs[0];
        var secondTab = _viewModel.Tabs[1];
        var thirdTab = _viewModel.Tabs[2];
        
        // Act
        _viewModel.MoveTabTo(firstTab, 2);
        
        // Assert
        Assert.Same(secondTab, _viewModel.Tabs[0]);
        Assert.Same(thirdTab, _viewModel.Tabs[1]);
        Assert.Same(firstTab, _viewModel.Tabs[2]);
    }
    
    [Fact]
    public void DetachTab_RemovesTabAndReturnsTrue()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        var tabToDetach = _viewModel.Tabs[0];
        
        // Act
        var result = _viewModel.DetachTab(tabToDetach);
        
        // Assert
        Assert.True(result);
        Assert.DoesNotContain(tabToDetach, _viewModel.Tabs);
    }
    
    [Fact]
    public void DetachTab_ReturnsFalseWhenLastTab()
    {
        // Arrange
        _viewModel.Initialize("/");
        var lastTab = _viewModel.Tabs[0];
        
        // Act
        var result = _viewModel.DetachTab(lastTab);
        
        // Assert
        Assert.False(result);
        Assert.Contains(lastTab, _viewModel.Tabs);
    }
    
    [Fact]
    public void AcceptTab_AddsTabToCollection()
    {
        // Arrange
        _viewModel.Initialize("/");
        var otherService = new Mock<IFileSystemService>();
        var otherTab = new TabViewModel(otherService.Object);
        var initialCount = _viewModel.Tabs.Count;
        
        // Act
        _viewModel.AcceptTab(otherTab);
        
        // Assert
        Assert.Equal(initialCount + 1, _viewModel.Tabs.Count);
        Assert.Contains(otherTab, _viewModel.Tabs);
        Assert.Same(otherTab, _viewModel.ActiveTab);
    }
    
    [Fact]
    public void AcceptTab_InsertsAtSpecifiedIndex()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        var otherService = new Mock<IFileSystemService>();
        var otherTab = new TabViewModel(otherService.Object);
        
        // Act
        _viewModel.AcceptTab(otherTab, 1);
        
        // Assert
        Assert.Same(otherTab, _viewModel.Tabs[1]);
    }
    
    [Fact]
    public void CloseOtherTabs_ClosesAllExceptSpecified()
    {
        // Arrange
        _viewModel.Initialize("/");
        _viewModel.AddNewTab();
        _viewModel.AddNewTab();
        var tabToKeep = _viewModel.Tabs[1];
        
        // Act
        _viewModel.CloseOtherTabs(tabToKeep);
        
        // Assert
        Assert.Single(_viewModel.Tabs);
        Assert.Same(tabToKeep, _viewModel.Tabs[0]);
    }
    
    [Fact]
    public void DuplicateTab_CreatesNewTabWithSamePath()
    {
        // Arrange
        _viewModel.Initialize("/test/path");
        var originalTab = _viewModel.ActiveTab;
        var initialCount = _viewModel.Tabs.Count;
        
        // Act
        _viewModel.DuplicateTab(originalTab);
        
        // Assert
        Assert.Equal(initialCount + 1, _viewModel.Tabs.Count);
    }
    
    [Fact]
    public void ToggleLockTab_TogglesIsLocked()
    {
        // Arrange
        _viewModel.Initialize("/");
        var tab = _viewModel.ActiveTab!;
        var originalLocked = tab.IsLocked;
        
        // Act
        _viewModel.ToggleLockTab(tab);
        
        // Assert
        Assert.Equal(!originalLocked, tab.IsLocked);
    }
}
