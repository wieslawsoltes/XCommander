// LongPathService.cs - Long Path Support Service Implementation

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of long path support.
/// </summary>
public class LongPathService : ILongPathService
{
    private const int MaxPathLegacy = 260;
    private const string LongPathPrefix = @"\\?\";
    private const string UncLongPathPrefix = @"\\?\UNC\";
    
    /// <summary>
    /// Gets whether we're running on Windows.
    /// </summary>
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    
    public bool IsLongPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        return path.Length >= MaxPathLegacy;
    }
    
    public string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
            
        // Remove any existing long path prefix
        if (path.StartsWith(UncLongPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = @"\\" + path.Substring(UncLongPathPrefix.Length);
        }
        else if (path.StartsWith(LongPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(LongPathPrefix.Length);
        }
        
        // Normalize separators
        path = path.Replace('/', '\\');
        
        // Remove trailing separator (except for root)
        if (path.Length > 1 && path.EndsWith('\\') && !path.EndsWith(":\\"))
        {
            path = path.TrimEnd('\\');
        }
        
        return path;
    }
    
    public string GetExtendedLengthPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
            
        // Already has long path prefix
        if (path.StartsWith(LongPathPrefix, StringComparison.OrdinalIgnoreCase))
            return path;
            
        path = NormalizePath(path);
        
        // UNC path
        if (path.StartsWith(@"\\"))
        {
            return UncLongPathPrefix + path.Substring(2);
        }
        
        // Regular path
        return LongPathPrefix + path;
    }
    
    public bool SupportsLongPathsNatively()
    {
        // .NET 6+ supports long paths on Windows 10 1607+
        // Linux/macOS don't have the 260 character limit
        if (!IsWindows)
            return true;
            
        // Check if LongPathsEnabled is set in Windows
        // .NET handles this automatically in most cases
        try
        {
            // Try to access a theoretical long path
            // If no exception, long paths are supported
            var testPath = new string('a', 300);
            var fullPath = Path.GetFullPath(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var src = PreparePathForOperation(sourcePath);
        var dst = PreparePathForOperation(destPath);
        
        // Ensure destination directory exists
        var destDir = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        await using var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destStream = new FileStream(dst, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        await sourceStream.CopyToAsync(destStream, cancellationToken);
        
        // Copy attributes
        try
        {
            var attrs = File.GetAttributes(src);
            File.SetAttributes(dst, attrs);
        }
        catch
        {
            // Ignore attribute copy errors
        }
    }
    
    public async Task MoveFileAsync(string sourcePath, string destPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var src = PreparePathForOperation(sourcePath);
        var dst = PreparePathForOperation(destPath);
        
        // Ensure destination directory exists
        var destDir = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        if (overwrite && File.Exists(dst))
        {
            File.Delete(dst);
        }
        
        // Try rename first (faster if on same volume)
        try
        {
            File.Move(src, dst);
            return;
        }
        catch (IOException)
        {
            // Cross-volume move, need to copy + delete
        }
        
        await CopyFileAsync(sourcePath, destPath, overwrite, cancellationToken);
        File.Delete(src);
    }
    
    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var p = PreparePathForOperation(path);
        
        // Remove read-only attribute if set
        var attrs = File.GetAttributes(p);
        if ((attrs & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(p, attrs & ~FileAttributes.ReadOnly);
        }
        
        File.Delete(p);
        return Task.CompletedTask;
    }
    
    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var p = PreparePathForOperation(path);
        Directory.CreateDirectory(p);
        return Task.CompletedTask;
    }
    
    public Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var p = PreparePathForOperation(path);
        
        if (recursive)
        {
            // Delete all contents first
            foreach (var file in EnumerateFiles(path, "*", true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(file.FullPath);
            }
            
            // Delete directories in reverse order (deepest first)
            var dirs = EnumerateDirectories(path, "*", true).ToList();
            dirs.Reverse();
            
            foreach (var dir in dirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(dir.FullPath);
            }
        }
        
        Directory.Delete(p);
        return Task.CompletedTask;
    }
    
    public bool Exists(string path)
    {
        var p = PreparePathForOperation(path);
        return File.Exists(p) || Directory.Exists(p);
    }
    
    public LongPathFileInfo? GetFileInfo(string path)
    {
        var p = PreparePathForOperation(path);
        
        if (!File.Exists(p))
            return null;
            
        var info = new FileInfo(p);
        
        return new LongPathFileInfo
        {
            FullPath = NormalizePath(path),
            Name = info.Name,
            DirectoryPath = info.DirectoryName != null ? NormalizePath(info.DirectoryName) : null,
            Length = info.Length,
            CreationTime = info.CreationTimeUtc,
            LastWriteTime = info.LastWriteTimeUtc,
            LastAccessTime = info.LastAccessTimeUtc,
            Attributes = info.Attributes
        };
    }
    
    public LongPathDirectoryInfo? GetDirectoryInfo(string path)
    {
        var p = PreparePathForOperation(path);
        
        if (!Directory.Exists(p))
            return null;
            
        var info = new DirectoryInfo(p);
        
        return new LongPathDirectoryInfo
        {
            FullPath = NormalizePath(path),
            Name = info.Name,
            ParentPath = info.Parent?.FullName != null ? NormalizePath(info.Parent.FullName) : null,
            CreationTime = info.CreationTimeUtc,
            LastWriteTime = info.LastWriteTimeUtc,
            LastAccessTime = info.LastAccessTimeUtc,
            Attributes = info.Attributes
        };
    }
    
    public IEnumerable<LongPathFileInfo> EnumerateFiles(string path, string searchPattern = "*", bool recursive = false)
    {
        var p = PreparePathForOperation(path);
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        foreach (var filePath in Directory.EnumerateFiles(p, searchPattern, options))
        {
            LongPathFileInfo? info = null;
            try
            {
                var fileInfo = new FileInfo(filePath);
                info = new LongPathFileInfo
                {
                    FullPath = NormalizePath(filePath),
                    Name = fileInfo.Name,
                    DirectoryPath = fileInfo.DirectoryName != null ? NormalizePath(fileInfo.DirectoryName) : null,
                    Length = fileInfo.Length,
                    CreationTime = fileInfo.CreationTimeUtc,
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    LastAccessTime = fileInfo.LastAccessTimeUtc,
                    Attributes = fileInfo.Attributes
                };
            }
            catch
            {
                // Skip files we can't access
            }
            
            if (info != null)
                yield return info;
        }
    }
    
    public IEnumerable<LongPathDirectoryInfo> EnumerateDirectories(string path, string searchPattern = "*", bool recursive = false)
    {
        var p = PreparePathForOperation(path);
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        foreach (var dirPath in Directory.EnumerateDirectories(p, searchPattern, options))
        {
            LongPathDirectoryInfo? info = null;
            try
            {
                var dirInfo = new DirectoryInfo(dirPath);
                info = new LongPathDirectoryInfo
                {
                    FullPath = NormalizePath(dirPath),
                    Name = dirInfo.Name,
                    ParentPath = dirInfo.Parent?.FullName != null ? NormalizePath(dirInfo.Parent.FullName) : null,
                    CreationTime = dirInfo.CreationTimeUtc,
                    LastWriteTime = dirInfo.LastWriteTimeUtc,
                    LastAccessTime = dirInfo.LastAccessTimeUtc,
                    Attributes = dirInfo.Attributes
                };
            }
            catch
            {
                // Skip directories we can't access
            }
            
            if (info != null)
                yield return info;
        }
    }
    
    private string PreparePathForOperation(string path)
    {
        if (IsWindows && IsLongPath(path) && !SupportsLongPathsNatively())
        {
            return GetExtendedLengthPath(path);
        }
        
        return path;
    }
}
