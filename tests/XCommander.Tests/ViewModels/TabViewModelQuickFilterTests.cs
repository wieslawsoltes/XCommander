using System;
using System.Collections.Generic;
using Moq;
using XCommander.Models;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class TabViewModelQuickFilterTests
{
    [Fact]
    public void ApplyQuickFilter_AddsFilterToHistoryAndPresets()
    {
        var fileSystem = CreateFileSystemMock();
        var settings = new AppSettings { CommandHistoryLimit = 10 };
        var viewModel = new TabViewModel(fileSystem.Object, settings);

        viewModel.QuickFilter = "*.log";
        viewModel.ApplyQuickFilter();

        Assert.Contains("*.log", settings.QuickFilterHistory);
        Assert.Contains("*.log", viewModel.QuickFilterPresets);
    }

    [Fact]
    public void ApplyQuickFilter_TrimsHistoryToLimit()
    {
        var fileSystem = CreateFileSystemMock();
        var settings = new AppSettings { CommandHistoryLimit = 2 };
        var viewModel = new TabViewModel(fileSystem.Object, settings);

        viewModel.QuickFilter = "*.a";
        viewModel.ApplyQuickFilter();
        viewModel.QuickFilter = "*.b";
        viewModel.ApplyQuickFilter();
        viewModel.QuickFilter = "*.c";
        viewModel.ApplyQuickFilter();

        Assert.Equal(2, settings.QuickFilterHistory.Count);
        Assert.Equal("*.c", settings.QuickFilterHistory[0]);
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
