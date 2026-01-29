using System.Text;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace XCommander.Tests.Integration;

/// <summary>
/// Integration tests for archive operations.
/// These tests use actual archive files with temp directories.
/// </summary>
public class ArchiveOperationsIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    
    public ArchiveOperationsIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"XCommander_Archive_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }
    
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
    
    #region ZIP Creation Tests
    
    [Fact]
    public async Task CreateZipArchive_SingleFile_CreatesSuccessfully()
    {
        // Arrange
        var sourceFile = Path.Combine(_testRoot, "test.txt");
        var archivePath = Path.Combine(_testRoot, "archive.zip");
        await File.WriteAllTextAsync(sourceFile, "Test content for ZIP");
        
        // Act
        using (var archive = CreateZipArchive())
        {
            using var stream = File.OpenRead(sourceFile);
            archive.AddEntry("test.txt", stream, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Assert
        Assert.True(File.Exists(archivePath));
        
        using var readArchive = OpenZipArchive(archivePath);
        var entries = readArchive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Single(entries);
        Assert.Equal("test.txt", entries[0].Key);
    }
    
    [Fact]
    public async Task CreateZipArchive_MultipleFiles_CreatesSuccessfully()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "multi.zip");
        var files = new List<string>();
        
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(_testRoot, $"file{i}.txt");
            await File.WriteAllTextAsync(file, $"Content {i}");
            files.Add(file);
        }
        
        // Act - Use memory streams with leaveOpen=false for all entries
        using (var archive = CreateZipArchive())
        {
            foreach (var file in files)
            {
                var bytes = await File.ReadAllBytesAsync(file);
                var stream = new MemoryStream(bytes);
                archive.AddEntry(Path.GetFileName(file), stream, true);
            }
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Assert
        Assert.True(File.Exists(archivePath));
        
        using var readArchive = OpenZipArchive(archivePath);
        Assert.Equal(5, readArchive.Entries.Count(e => !e.IsDirectory));
    }
    
    [Fact]
    public async Task CreateZipArchive_WithDirectory_PreservesStructure()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "source");
        var subDir = Path.Combine(sourceDir, "subdir");
        var archivePath = Path.Combine(_testRoot, "structure.zip");
        
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "root.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(subDir, "sub.txt"), "sub");
        
        // Act
        using (var archive = CreateZipArchive())
        {
            using var stream1 = File.OpenRead(Path.Combine(sourceDir, "root.txt"));
            archive.AddEntry("root.txt", stream1, true);
            using var stream2 = File.OpenRead(Path.Combine(subDir, "sub.txt"));
            archive.AddEntry("subdir/sub.txt", stream2, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Assert
        using var readArchive = OpenZipArchive(archivePath);
        var entries = readArchive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Key == "root.txt");
        Assert.Contains(entries, e => e.Key == "subdir/sub.txt");
    }
    
    #endregion
    
    #region ZIP Extraction Tests
    
    [Fact]
    public async Task ExtractZipArchive_AllFiles_ExtractsSuccessfully()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "toextract.zip");
        var extractDir = Path.Combine(_testRoot, "extracted");
        Directory.CreateDirectory(extractDir);
        
        // Create test archive
        using (var archive = CreateZipArchive())
        {
            using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content 1"));
            using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content 2"));
            archive.AddEntry("file1.txt", stream1, true);
            archive.AddEntry("file2.txt", stream2, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act
        using (var archive = OpenZipArchive(archivePath))
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var destPath = Path.Combine(extractDir, entry.Key ?? "unknown");
                using var entryStream = entry.OpenEntryStream();
                using var fileStream = File.Create(destPath);
                await entryStream.CopyToAsync(fileStream);
            }
        }
        
        // Assert
        Assert.True(File.Exists(Path.Combine(extractDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(extractDir, "file2.txt")));
        Assert.Equal("Content 1", await File.ReadAllTextAsync(Path.Combine(extractDir, "file1.txt")));
        Assert.Equal("Content 2", await File.ReadAllTextAsync(Path.Combine(extractDir, "file2.txt")));
    }
    
    [Fact]
    public async Task ExtractZipArchive_SingleFile_ExtractsCorrectly()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "selectextract.zip");
        var extractDir = Path.Combine(_testRoot, "selectextracted");
        Directory.CreateDirectory(extractDir);
        
        using (var archive = CreateZipArchive())
        {
            using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Extract me"));
            using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Skip me"));
            archive.AddEntry("extract.txt", stream1, true);
            archive.AddEntry("skip.txt", stream2, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act
        using (var archive = OpenZipArchive(archivePath))
        {
            var entry = archive.Entries.First(e => e.Key == "extract.txt");
            var destPath = Path.Combine(extractDir, "extract.txt");
            using var entryStream = entry.OpenEntryStream();
            using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream);
        }
        
        // Assert
        Assert.True(File.Exists(Path.Combine(extractDir, "extract.txt")));
        Assert.False(File.Exists(Path.Combine(extractDir, "skip.txt")));
    }
    
    #endregion
    
    #region ZIP Modification Tests
    
    [Fact]
    public async Task AddToZipArchive_ExistingArchive_AddsFile()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "addto.zip");
        var newFile = Path.Combine(_testRoot, "newfile.txt");
        await File.WriteAllTextAsync(newFile, "New content");
        
        // Create initial archive
        using (var archive = CreateZipArchive())
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Original"));
            archive.AddEntry("original.txt", stream, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act - Add new file
        var tempPath = archivePath + ".tmp";
        using (var archive = OpenZipArchive(archivePath))
        {
            using var newFileStream = File.OpenRead(newFile);
            archive.AddEntry("newfile.txt", newFileStream, true);
            using var outputStream = File.Create(tempPath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        File.Move(tempPath, archivePath, overwrite: true);
        
        // Assert
        using var readArchive = OpenZipArchive(archivePath);
        var entries = readArchive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Key == "original.txt");
        Assert.Contains(entries, e => e.Key == "newfile.txt");
    }
    
    [Fact]
    public void DeleteFromZipArchive_ExistingEntry_RemovesFile()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "deletefrom.zip");
        
        // Create archive with multiple files
        using (var archive = CreateZipArchive())
        {
            using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Keep"));
            using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Delete"));
            archive.AddEntry("keep.txt", stream1, true);
            archive.AddEntry("delete.txt", stream2, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act - Delete file
        var tempPath = archivePath + ".tmp";
        using (var archive = OpenZipArchive(archivePath))
        {
            var entryToDelete = archive.Entries.First(e => e.Key == "delete.txt");
            archive.RemoveEntry(entryToDelete);
            using var outputStream = File.Create(tempPath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        File.Move(tempPath, archivePath, overwrite: true);
        
        // Assert
        using var readArchive = OpenZipArchive(archivePath);
        var entries = readArchive.Entries.Where(e => !e.IsDirectory).ToList();
        Assert.Single(entries);
        Assert.Equal("keep.txt", entries[0].Key);
    }
    
    #endregion
    
    #region Archive Browsing Tests
    
    [Fact]
    public void BrowseZipArchive_ListsAllEntries()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "browse.zip");
        
        using (var archive = CreateZipArchive())
        {
            using var s1 = new MemoryStream(Encoding.UTF8.GetBytes("1"));
            using var s2 = new MemoryStream(Encoding.UTF8.GetBytes("2"));
            using var s3 = new MemoryStream(Encoding.UTF8.GetBytes("3"));
            archive.AddEntry("file1.txt", s1, true);
            archive.AddEntry("dir/file2.txt", s2, true);
            archive.AddEntry("dir/subdir/file3.txt", s3, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act
        List<string> entries;
        using (var archive = OpenZipArchive(archivePath))
        {
            entries = archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e => e.Key ?? "")
                .ToList();
        }
        
        // Assert
        Assert.Equal(3, entries.Count);
        Assert.Contains("file1.txt", entries);
        Assert.Contains("dir/file2.txt", entries);
        Assert.Contains("dir/subdir/file3.txt", entries);
    }
    
    [Fact]
    public void BrowseZipArchive_GetsEntrySizes()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "sizes.zip");
        var content = new string('A', 1000); // 1000 bytes
        
        using (var archive = CreateZipArchive())
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            archive.AddEntry("data.txt", stream, true);
            using var outputStream = File.Create(archivePath);
            archive.SaveTo(outputStream, new WriterOptions(CompressionType.Deflate));
        }
        
        // Act
        long size;
        long compressedSize;
        using (var archive = OpenZipArchive(archivePath))
        {
            var entry = archive.Entries.First(e => !e.IsDirectory);
            size = entry.Size;
            compressedSize = entry.CompressedSize;
        }
        
        // Assert
        Assert.Equal(1000, size);
        Assert.True(compressedSize < size); // Should be compressed
    }
    
    #endregion

    private static ZipArchive CreateZipArchive()
    {
        return (ZipArchive)ZipArchive.CreateArchive();
    }

    private static ZipArchive OpenZipArchive(string archivePath)
    {
        return (ZipArchive)ZipArchive.OpenArchive(archivePath, new ReaderOptions());
    }
}
