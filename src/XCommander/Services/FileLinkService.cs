using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XCommander.Services;

/// <summary>
/// Type of file system link
/// </summary>
public enum LinkType
{
    /// <summary>
    /// Symbolic link (soft link)
    /// </summary>
    Symbolic,
    
    /// <summary>
    /// Hard link (file only)
    /// </summary>
    Hard,
    
    /// <summary>
    /// Directory junction (Windows only)
    /// </summary>
    Junction
}

/// <summary>
/// Information about a file system link
/// </summary>
public class LinkInfo
{
    public string LinkPath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public LinkType Type { get; init; }
    public bool IsValid { get; init; }
    public bool TargetExists { get; init; }
}

/// <summary>
/// Result of a link creation operation
/// </summary>
public class LinkCreationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string LinkPath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public LinkType Type { get; init; }
}

/// <summary>
/// Service for managing file system links (symbolic links, hard links, junctions)
/// </summary>
public interface IFileLinkService
{
    /// <summary>
    /// Check if a path is a symbolic link
    /// </summary>
    bool IsSymbolicLink(string path);
    
    /// <summary>
    /// Check if a path is a hard link (has multiple links)
    /// </summary>
    bool IsHardLink(string path);
    
    /// <summary>
    /// Check if a path is a junction point (Windows only)
    /// </summary>
    bool IsJunction(string path);
    
    /// <summary>
    /// Get the target of a symbolic link or junction
    /// </summary>
    string? GetLinkTarget(string linkPath);
    
    /// <summary>
    /// Get detailed link information
    /// </summary>
    LinkInfo GetLinkInfo(string path);
    
    /// <summary>
    /// Create a symbolic link
    /// </summary>
    LinkCreationResult CreateSymbolicLink(string linkPath, string targetPath, bool isDirectory);
    
    /// <summary>
    /// Create a hard link (files only)
    /// </summary>
    LinkCreationResult CreateHardLink(string linkPath, string targetPath);
    
    /// <summary>
    /// Create a directory junction (Windows only)
    /// </summary>
    LinkCreationResult CreateJunction(string junctionPath, string targetPath);
    
    /// <summary>
    /// Get the number of hard links to a file
    /// </summary>
    int GetHardLinkCount(string filePath);
    
    /// <summary>
    /// Get all hard links to a file
    /// </summary>
    IEnumerable<string> GetHardLinks(string filePath);
    
    /// <summary>
    /// Resolve a link to its final target (follows chain of links)
    /// </summary>
    string? ResolveLink(string linkPath, int maxDepth = 10);
    
    /// <summary>
    /// Check if symbolic/junction link creation is supported on the current platform
    /// </summary>
    bool IsSymbolicLinkSupported { get; }
    
    /// <summary>
    /// Check if hard link creation is supported on the current platform
    /// </summary>
    bool IsHardLinkSupported { get; }
    
    /// <summary>
    /// Check if junction creation is supported (Windows only)
    /// </summary>
    bool IsJunctionSupported { get; }
}

public class FileLinkService : IFileLinkService
{
    public bool IsSymbolicLinkSupported => true; // Supported on all modern OS
    
    public bool IsHardLinkSupported => true; // Supported on all modern OS
    
    public bool IsJunctionSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    
    public bool IsSymbolicLink(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) && 
                   fileInfo.LinkTarget != null;
        }
        catch
        {
            return false;
        }
    }
    
    public bool IsHardLink(string path)
    {
        return GetHardLinkCount(path) > 1;
    }
    
    public bool IsJunction(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
            
        try
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.Exists && 
                   dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                   dirInfo.LinkTarget != null;
        }
        catch
        {
            return false;
        }
    }
    
    public string? GetLinkTarget(string linkPath)
    {
        try
        {
            if (File.Exists(linkPath))
            {
                var fileInfo = new FileInfo(linkPath);
                return fileInfo.LinkTarget;
            }
            
            if (Directory.Exists(linkPath))
            {
                var dirInfo = new DirectoryInfo(linkPath);
                return dirInfo.LinkTarget;
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    public LinkInfo GetLinkInfo(string path)
    {
        var target = GetLinkTarget(path);
        var isSymLink = IsSymbolicLink(path);
        var isJunction = IsJunction(path);
        var isHardLink = !isSymLink && !isJunction && IsHardLink(path);
        
        var linkType = isJunction ? LinkType.Junction : 
                       isSymLink ? LinkType.Symbolic : 
                       LinkType.Hard;
        
        var targetExists = false;
        if (target != null)
        {
            targetExists = File.Exists(target) || Directory.Exists(target);
        }
        
        return new LinkInfo
        {
            LinkPath = path,
            TargetPath = target,
            Type = linkType,
            IsValid = isSymLink || isJunction || isHardLink,
            TargetExists = targetExists
        };
    }
    
    public LinkCreationResult CreateSymbolicLink(string linkPath, string targetPath, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
            else
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            
            return new LinkCreationResult
            {
                Success = true,
                LinkPath = linkPath,
                TargetPath = targetPath,
                Type = LinkType.Symbolic
            };
        }
        catch (Exception ex)
        {
            return new LinkCreationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                LinkPath = linkPath,
                TargetPath = targetPath,
                Type = LinkType.Symbolic
            };
        }
    }
    
    public LinkCreationResult CreateHardLink(string linkPath, string targetPath)
    {
        try
        {
            if (!File.Exists(targetPath))
            {
                return new LinkCreationResult
                {
                    Success = false,
                    ErrorMessage = "Target file does not exist",
                    LinkPath = linkPath,
                    TargetPath = targetPath,
                    Type = LinkType.Hard
                };
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use Windows API via P/Invoke or process
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /H \"{linkPath}\" \"{targetPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                
                if (process?.ExitCode == 0)
                {
                    return new LinkCreationResult
                    {
                        Success = true,
                        LinkPath = linkPath,
                        TargetPath = targetPath,
                        Type = LinkType.Hard
                    };
                }
                else
                {
                    var error = process?.StandardError.ReadToEnd();
                    return new LinkCreationResult
                    {
                        Success = false,
                        ErrorMessage = error ?? "Unknown error creating hard link",
                        LinkPath = linkPath,
                        TargetPath = targetPath,
                        Type = LinkType.Hard
                    };
                }
            }
            else
            {
                // Use ln command on Unix-like systems
                var psi = new ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"\"{targetPath}\" \"{linkPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                
                if (process?.ExitCode == 0)
                {
                    return new LinkCreationResult
                    {
                        Success = true,
                        LinkPath = linkPath,
                        TargetPath = targetPath,
                        Type = LinkType.Hard
                    };
                }
                else
                {
                    var error = process?.StandardError.ReadToEnd();
                    return new LinkCreationResult
                    {
                        Success = false,
                        ErrorMessage = error ?? "Unknown error creating hard link",
                        LinkPath = linkPath,
                        TargetPath = targetPath,
                        Type = LinkType.Hard
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return new LinkCreationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                LinkPath = linkPath,
                TargetPath = targetPath,
                Type = LinkType.Hard
            };
        }
    }
    
    public LinkCreationResult CreateJunction(string junctionPath, string targetPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new LinkCreationResult
            {
                Success = false,
                ErrorMessage = "Junctions are only supported on Windows",
                LinkPath = junctionPath,
                TargetPath = targetPath,
                Type = LinkType.Junction
            };
        }
        
        try
        {
            if (!Directory.Exists(targetPath))
            {
                return new LinkCreationResult
                {
                    Success = false,
                    ErrorMessage = "Target directory does not exist",
                    LinkPath = junctionPath,
                    TargetPath = targetPath,
                    Type = LinkType.Junction
                };
            }
            
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(psi);
            process?.WaitForExit();
            
            if (process?.ExitCode == 0)
            {
                return new LinkCreationResult
                {
                    Success = true,
                    LinkPath = junctionPath,
                    TargetPath = targetPath,
                    Type = LinkType.Junction
                };
            }
            else
            {
                var error = process?.StandardError.ReadToEnd();
                return new LinkCreationResult
                {
                    Success = false,
                    ErrorMessage = error ?? "Unknown error creating junction",
                    LinkPath = junctionPath,
                    TargetPath = targetPath,
                    Type = LinkType.Junction
                };
            }
        }
        catch (Exception ex)
        {
            return new LinkCreationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                LinkPath = junctionPath,
                TargetPath = targetPath,
                Type = LinkType.Junction
            };
        }
    }
    
    public int GetHardLinkCount(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return 0;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: use fsutil
                var psi = new ProcessStartInfo
                {
                    FileName = "fsutil",
                    Arguments = $"hardlink list \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                
                using var process = Process.Start(psi);
                var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                process?.WaitForExit();
                
                return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            }
            else
            {
                // Unix: use stat
                var psi = new ProcessStartInfo
                {
                    FileName = "stat",
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) 
                        ? $"-f %l \"{filePath}\"" 
                        : $"-c %h \"{filePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                
                using var process = Process.Start(psi);
                var output = process?.StandardOutput.ReadToEnd().Trim() ?? "1";
                process?.WaitForExit();
                
                return int.TryParse(output, out var count) ? count : 1;
            }
        }
        catch
        {
            return 1;
        }
    }
    
    public IEnumerable<string> GetHardLinks(string filePath)
    {
        if (!File.Exists(filePath))
            yield break;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "fsutil",
                Arguments = $"hardlink list \"{filePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
            process?.WaitForExit();
            
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    yield return trimmed;
            }
        }
        else
        {
            // On Unix, finding all hard links requires scanning the filesystem
            // which is expensive, so we just return the original path
            yield return filePath;
        }
    }
    
    public string? ResolveLink(string linkPath, int maxDepth = 10)
    {
        var current = linkPath;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        for (var i = 0; i < maxDepth; i++)
        {
            if (visited.Contains(current))
                return null; // Circular link detected
            
            visited.Add(current);
            
            var target = GetLinkTarget(current);
            if (target == null)
                return current; // Not a link, return as-is
            
            // Resolve relative paths
            if (!Path.IsPathRooted(target))
            {
                var linkDir = Path.GetDirectoryName(current);
                if (linkDir != null)
                    target = Path.GetFullPath(Path.Combine(linkDir, target));
            }
            
            current = target;
        }
        
        return current;
    }
}
