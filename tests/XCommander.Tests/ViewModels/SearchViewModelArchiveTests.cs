using System;
using System.IO;
using Moq;
using XCommander.Models;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class SearchViewModelArchiveTests
{
    [Fact]
    public async Task StartSearchAsync_IncludesArchiveEntries_WhenEnabled()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var archivePath = Path.Combine(tempDir.FullName, "sample.zip");
        File.WriteAllText(archivePath, string.Empty);

        try
        {
            var fileSystem = CreateFileSystemMock();
            var settings = new AppSettings { SearchMaxResults = 50 };
            var archiveService = new StubArchiveService("doc.txt");
            var viewModel = new SearchViewModel(fileSystem.Object, settings, archiveService: archiveService)
            {
                SearchPath = tempDir.FullName,
                SearchPattern = "*.txt",
                SearchInArchives = true,
                SearchInContent = false,
                SearchInSubfolders = true
            };

            await viewModel.StartSearchAsync();

            Assert.Contains(viewModel.Results, r => r.Name.Contains("doc.txt", StringComparison.Ordinal));
            Assert.Contains(viewModel.Results, r => r.FullPath == archivePath);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
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

    private sealed class StubArchiveService : IArchiveService
    {
        private readonly string _entryName;

        public StubArchiveService(string entryName)
        {
            _entryName = entryName;
        }

        public IReadOnlyList<string> SupportedExtensions => new[] { ".zip" };

        public bool IsArchive(string path) => Path.GetExtension(path)
            .Equals(".zip", StringComparison.OrdinalIgnoreCase);

        public Task<List<ArchiveEntry>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ArchiveEntry>
            {
                new()
                {
                    Name = _entryName,
                    Path = _entryName,
                    IsDirectory = false,
                    Size = 12,
                    LastModified = DateTime.UtcNow
                }
            });
        }

        public Task ExtractAllAsync(string archivePath, string destinationPath, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExtractEntriesAsync(string archivePath, IEnumerable<string> entryPaths, string destinationPath, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task CreateArchiveAsync(string archivePath, IEnumerable<string> sourcePaths, ArchiveType type, CompressionLevel compressionLevel = CompressionLevel.Normal, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task AddToArchiveAsync(string archivePath, IEnumerable<string> sourcePaths, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteEntriesAsync(string archivePath, IEnumerable<string> entryPaths, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
