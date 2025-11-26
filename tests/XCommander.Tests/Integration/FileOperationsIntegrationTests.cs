using XCommander.Models;
using XCommander.Services;

namespace XCommander.Tests.Integration;

/// <summary>
/// Integration tests for file system operations.
/// These tests use actual file system operations with temp directories.
/// </summary>
public class FileOperationsIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileSystemService _fileService;
    
    public FileOperationsIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"XCommander_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _fileService = new FileSystemService();
    }
    
    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
    
    #region File Copy Tests
    
    [Fact]
    public async Task CopyFile_SingleFile_CopiesSuccessfully()
    {
        // Arrange
        var sourceFile = Path.Combine(_testRoot, "source.txt");
        var destDir = Path.Combine(_testRoot, "dest");
        Directory.CreateDirectory(destDir);
        await File.WriteAllTextAsync(sourceFile, "Test content");
        
        // Act
        await _fileService.CopyAsync(
            new[] { sourceFile }, 
            destDir,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.True(File.Exists(Path.Combine(destDir, "source.txt")));
        Assert.Equal("Test content", await File.ReadAllTextAsync(Path.Combine(destDir, "source.txt")));
    }
    
    [Fact]
    public async Task CopyFile_MultipleFiles_CopiesAllSuccessfully()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "source");
        var destDir = Path.Combine(_testRoot, "dest");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);
        
        var files = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(sourceDir, $"file{i}.txt");
            await File.WriteAllTextAsync(file, $"Content {i}");
            files.Add(file);
        }
        
        // Act
        await _fileService.CopyAsync(
            files.ToArray(), 
            destDir,
            null,
            CancellationToken.None);
        
        // Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.True(File.Exists(Path.Combine(destDir, $"file{i}.txt")));
        }
    }
    
    [Fact]
    public async Task CopyDirectory_WithSubdirectories_CopiesRecursively()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "source");
        var subDir = Path.Combine(sourceDir, "subdir");
        var destDir = Path.Combine(_testRoot, "dest");
        
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "root.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(subDir, "sub.txt"), "sub");
        
        // Act
        await _fileService.CopyAsync(
            new[] { sourceDir }, 
            destDir,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.True(Directory.Exists(Path.Combine(destDir, "source")));
        Assert.True(File.Exists(Path.Combine(destDir, "source", "root.txt")));
        Assert.True(Directory.Exists(Path.Combine(destDir, "source", "subdir")));
        Assert.True(File.Exists(Path.Combine(destDir, "source", "subdir", "sub.txt")));
    }
    
    #endregion
    
    #region File Move Tests
    
    [Fact]
    public async Task MoveFile_SingleFile_MovesSuccessfully()
    {
        // Arrange
        var sourceFile = Path.Combine(_testRoot, "source.txt");
        var destDir = Path.Combine(_testRoot, "dest");
        Directory.CreateDirectory(destDir);
        await File.WriteAllTextAsync(sourceFile, "Test content");
        
        // Act
        await _fileService.MoveAsync(
            new[] { sourceFile }, 
            destDir,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.False(File.Exists(sourceFile)); // Source should not exist
        Assert.True(File.Exists(Path.Combine(destDir, "source.txt")));
    }
    
    [Fact]
    public async Task MoveDirectory_WithContents_MovesSuccessfully()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "source_move");
        var destDir = Path.Combine(_testRoot, "dest_move");
        
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file.txt"), "content");
        
        // Act
        await _fileService.MoveAsync(
            new[] { sourceDir }, 
            destDir,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.False(Directory.Exists(sourceDir)); // Source should not exist
        Assert.True(Directory.Exists(Path.Combine(destDir, "source_move")));
        Assert.True(File.Exists(Path.Combine(destDir, "source_move", "file.txt")));
    }
    
    #endregion
    
    #region File Delete Tests
    
    [Fact]
    public async Task DeleteFile_SingleFile_DeletesSuccessfully()
    {
        // Arrange
        var file = Path.Combine(_testRoot, "todelete.txt");
        await File.WriteAllTextAsync(file, "delete me");
        
        // Act
        await _fileService.DeleteAsync(
            new[] { file },
            permanent: true,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.False(File.Exists(file));
    }
    
    [Fact]
    public async Task DeleteDirectory_WithContents_DeletesRecursively()
    {
        // Arrange
        var dir = Path.Combine(_testRoot, "todelete");
        var subDir = Path.Combine(dir, "subdir");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(dir, "file1.txt"), "content");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.txt"), "content");
        
        // Act
        await _fileService.DeleteAsync(
            new[] { dir },
            permanent: true,
            null,
            CancellationToken.None);
        
        // Assert
        Assert.False(Directory.Exists(dir));
    }
    
    #endregion
    
    #region Create Directory Tests
    
    [Fact]
    public void CreateDirectory_NewDirectory_CreatesSuccessfully()
    {
        // Arrange
        var newDir = Path.Combine(_testRoot, "newdir");
        
        // Act
        _fileService.CreateDirectory(newDir);
        
        // Assert
        Assert.True(Directory.Exists(newDir));
    }
    
    [Fact]
    public void CreateDirectory_NestedPath_CreatesAllLevels()
    {
        // Arrange
        var nestedDir = Path.Combine(_testRoot, "level1", "level2", "level3");
        
        // Act
        _fileService.CreateDirectory(nestedDir);
        
        // Assert
        Assert.True(Directory.Exists(nestedDir));
    }
    
    #endregion
    
    #region Rename Tests
    
    [Fact]
    public void Rename_File_RenamesSuccessfully()
    {
        // Arrange
        var originalFile = Path.Combine(_testRoot, "original.txt");
        var newPath = Path.Combine(_testRoot, "renamed.txt");
        File.WriteAllText(originalFile, "content");
        
        // Act
        _fileService.Rename(originalFile, newPath);
        
        // Assert
        Assert.False(File.Exists(originalFile));
        Assert.True(File.Exists(newPath));
    }
    
    [Fact]
    public void Rename_Directory_RenamesSuccessfully()
    {
        // Arrange
        var originalDir = Path.Combine(_testRoot, "originaldir");
        var newPath = Path.Combine(_testRoot, "renameddir");
        Directory.CreateDirectory(originalDir);
        File.WriteAllText(Path.Combine(originalDir, "file.txt"), "content");
        
        // Act
        _fileService.Rename(originalDir, newPath);
        
        // Assert
        Assert.False(Directory.Exists(originalDir));
        Assert.True(Directory.Exists(newPath));
        Assert.True(File.Exists(Path.Combine(newPath, "file.txt")));
    }
    
    #endregion
    
    #region List Directory Tests
    
    [Fact]
    public async Task GetDirectoryContents_ReturnsFilesAndFolders()
    {
        // Arrange
        var testDir = Path.Combine(_testRoot, "listtest");
        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(Path.Combine(testDir, "folder1"));
        Directory.CreateDirectory(Path.Combine(testDir, "folder2"));
        await File.WriteAllTextAsync(Path.Combine(testDir, "file1.txt"), "content");
        await File.WriteAllTextAsync(Path.Combine(testDir, "file2.txt"), "content");
        
        // Act
        var items = _fileService.GetDirectoryContents(testDir, showHidden: true);
        
        // Assert - may include parent directory entry
        var regularItems = items.Where(i => i.Name != "..").ToList();
        Assert.Equal(4, regularItems.Count); // 2 folders + 2 files
        Assert.Equal(2, regularItems.Count(i => i.IsDirectory));
        Assert.Equal(2, regularItems.Count(i => !i.IsDirectory));
    }
    
    [Fact]
    public void GetDrives_ReturnsAtLeastOneDrive()
    {
        // Act
        var drives = _fileService.GetDrives();
        
        // Assert
        Assert.NotEmpty(drives);
    }
    
    #endregion
}
