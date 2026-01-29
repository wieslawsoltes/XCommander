using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using XCommander.Models;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class SessionStateViewModelTests
{
    [Fact]
    public void ApplySessionState_RestoresViewModeSortAndFilter()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        try
        {
            var fileSystem = CreateFileSystemMock();
            var settings = new AppSettings();
            var viewModel = new TabViewModel(fileSystem.Object, settings);

            var state = new TabState
            {
                Path = tempDir.FullName,
                ViewMode = "Thumbnails",
                SortColumn = 2,
                SortAscending = false,
                Filter = "*.cs",
                ShowHidden = true,
                History = new List<string> { tempDir.FullName },
                HistoryPosition = 0
            };

            viewModel.ApplySessionState(state, tempDir.FullName);

            Assert.Equal(FilePanelViewMode.Thumbnails, viewModel.ViewMode);
            Assert.Equal("Size", viewModel.SortColumn);
            Assert.False(viewModel.SortAscending);
            Assert.Equal("*.cs", viewModel.QuickFilter);
            Assert.True(viewModel.ShowHiddenFiles);
            Assert.Equal(tempDir.FullName, viewModel.CurrentPath);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
        }
    }

    [Fact]
    public void RestoreFromSession_SetsActiveTabFromPanelState()
    {
        var leftDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var rightDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        try
        {
            var fileSystem = CreateFileSystemMock();
            var settings = new AppSettings();
            var viewModel = new TabbedPanelViewModel(fileSystem.Object, settings);

            var panelState = new PanelState
            {
                Position = "Left",
                ActiveTabIndex = 1,
                Tabs = new List<TabState>
                {
                    new() { Path = leftDir.FullName },
                    new() { Path = rightDir.FullName }
                }
            };

            viewModel.RestoreFromSession(panelState, leftDir.FullName);

            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.Same(viewModel.Tabs[1], viewModel.ActiveTab);
            Assert.True(viewModel.ActiveTab?.IsSelected);
        }
        finally
        {
            Directory.Delete(leftDir.FullName, true);
            Directory.Delete(rightDir.FullName, true);
        }
    }

    private static Mock<IFileSystemService> CreateFileSystemMock()
    {
        var mock = new Mock<IFileSystemService>();
        mock.Setup(service => service.GetDirectoryContents(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Array.Empty<FileSystemItem>());
        mock.Setup(service => service.GetDrives())
            .Returns(Array.Empty<DriveItem>());
        mock.Setup(service => service.GetParentDirectory(It.IsAny<string>()))
            .Returns(string.Empty);
        return mock;
    }
}
