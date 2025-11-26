// ILongPathService.cs - Long Path Support Service
// Provides support for paths longer than 259 characters

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for handling long paths (>259 characters).
/// </summary>
public interface ILongPathService
{
    /// <summary>
    /// Checks if the path exceeds MAX_PATH limit.
    /// </summary>
    bool IsLongPath(string path);
    
    /// <summary>
    /// Normalizes a path for long path support.
    /// </summary>
    string NormalizePath(string path);
    
    /// <summary>
    /// Gets the extended-length path prefix for Windows.
    /// </summary>
    string GetExtendedLengthPath(string path);
    
    /// <summary>
    /// Checks if the system supports long paths natively.
    /// </summary>
    bool SupportsLongPathsNatively();
    
    /// <summary>
    /// Copies a file with long path support.
    /// </summary>
    Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves a file with long path support.
    /// </summary>
    Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a file with long path support.
    /// </summary>
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a directory with long path support.
    /// </summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a directory with long path support.
    /// </summary>
    Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a path exists (file or directory) with long path support.
    /// </summary>
    bool Exists(string path);
    
    /// <summary>
    /// Gets file info with long path support.
    /// </summary>
    LongPathFileInfo? GetFileInfo(string path);
    
    /// <summary>
    /// Gets directory info with long path support.
    /// </summary>
    LongPathDirectoryInfo? GetDirectoryInfo(string path);
    
    /// <summary>
    /// Enumerates files in a directory with long path support.
    /// </summary>
    IEnumerable<LongPathFileInfo> EnumerateFiles(string path, string searchPattern = "*", bool recursive = false);
    
    /// <summary>
    /// Enumerates directories with long path support.
    /// </summary>
    IEnumerable<LongPathDirectoryInfo> EnumerateDirectories(string path, string searchPattern = "*", bool recursive = false);
}

/// <summary>
/// File information for long paths.
/// </summary>
public record LongPathFileInfo
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string? DirectoryPath { get; init; }
    public required long Length { get; init; }
    public required DateTime CreationTime { get; init; }
    public required DateTime LastWriteTime { get; init; }
    public required DateTime LastAccessTime { get; init; }
    public required FileAttributes Attributes { get; init; }
    public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;
    public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0;
    public bool IsSystem => (Attributes & FileAttributes.System) != 0;
}

/// <summary>
/// Directory information for long paths.
/// </summary>
public record LongPathDirectoryInfo
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string? ParentPath { get; init; }
    public required DateTime CreationTime { get; init; }
    public required DateTime LastWriteTime { get; init; }
    public required DateTime LastAccessTime { get; init; }
    public required FileAttributes Attributes { get; init; }
    public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0;
    public bool IsSystem => (Attributes & FileAttributes.System) != 0;
}
