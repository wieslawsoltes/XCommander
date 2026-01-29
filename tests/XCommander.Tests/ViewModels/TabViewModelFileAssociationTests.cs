using System;
using System.IO;
using Moq;
using XCommander.Models;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class TabViewModelFileAssociationTests
{
    [Fact]
    public void OpenItem_RaisesArchiveOpenRequested_WhenAssociationIsArchive()
    {
        var fileSystem = CreateFileSystemMock();
        var settings = new AppSettings();
        var associationService = new StubFileAssociationService("internal:archive");
        var viewModel = new TabViewModel(fileSystem.Object, settings, fileAssociationService: associationService)
        {
            CurrentPath = "/tmp"
        };

        var fileItem = new FileItemViewModel(new FileSystemItem
        {
            Name = "archive.zip",
            FullPath = "/tmp/archive.zip",
            ItemType = FileSystemItemType.File,
            Size = 12,
            DateModified = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            Extension = ".zip",
            Attributes = FileAttributes.Normal
        });

        TabViewModel.ArchiveRequestEventArgs? args = null;
        viewModel.ArchiveOpenRequested += (_, e) => args = e;

        viewModel.OpenItem(fileItem);

        Assert.NotNull(args);
        Assert.Equal(fileItem.FullPath, args!.ArchivePath);
        Assert.Equal(viewModel.CurrentPath, args.ExtractPath);
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

    private sealed class StubFileAssociationService : IFileAssociationService
    {
        private readonly string? _openCommand;

        public StubFileAssociationService(string? openCommand)
        {
            _openCommand = openCommand;
        }

        public string? GetOpenCommand(string filePath) => _openCommand;

        public string? GetViewerCommand(string filePath) => null;

        public string? GetEditorCommand(string filePath) => null;
    }
}
