using System.Diagnostics;

namespace XCommander.Services;

/// <summary>
/// Git file status enumeration.
/// </summary>
public enum GitFileStatus
{
    None,
    Untracked,
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    Ignored,
    Unmerged
}

/// <summary>
/// Git repository information.
/// </summary>
public class GitRepositoryInfo
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? RemoteName { get; set; }
    public int AheadCount { get; set; }
    public int BehindCount { get; set; }
    public bool HasUncommittedChanges { get; set; }
    public bool HasUntrackedFiles { get; set; }
    public bool HasStashedChanges { get; set; }
}

/// <summary>
/// Git file status information.
/// </summary>
public class GitFileStatusInfo
{
    public string FilePath { get; set; } = string.Empty;
    public GitFileStatus IndexStatus { get; set; }
    public GitFileStatus WorkTreeStatus { get; set; }
    
    public string StatusIcon => GetStatusIcon();
    public string StatusText => GetStatusText();
    
    private string GetStatusIcon() => (IndexStatus, WorkTreeStatus) switch
    {
        (GitFileStatus.Added, _) => "➕",
        (_, GitFileStatus.Added) => "➕",
        (GitFileStatus.Modified, _) => "✏️",
        (_, GitFileStatus.Modified) => "✏️",
        (GitFileStatus.Deleted, _) => "➖",
        (_, GitFileStatus.Deleted) => "➖",
        (GitFileStatus.Renamed, _) => "➡️",
        (_, GitFileStatus.Renamed) => "➡️",
        (GitFileStatus.Untracked, _) => "❓",
        (_, GitFileStatus.Untracked) => "❓",
        (GitFileStatus.Ignored, _) => "🚫",
        (_, GitFileStatus.Ignored) => "🚫",
        (GitFileStatus.Unmerged, _) => "⚠️",
        (_, GitFileStatus.Unmerged) => "⚠️",
        _ => string.Empty
    };
    
    private string GetStatusText() => (IndexStatus, WorkTreeStatus) switch
    {
        (GitFileStatus.Added, _) => "Added (staged)",
        (_, GitFileStatus.Added) => "Added",
        (GitFileStatus.Modified, GitFileStatus.Modified) => "Modified (staged + unstaged)",
        (GitFileStatus.Modified, _) => "Modified (staged)",
        (_, GitFileStatus.Modified) => "Modified",
        (GitFileStatus.Deleted, _) => "Deleted (staged)",
        (_, GitFileStatus.Deleted) => "Deleted",
        (GitFileStatus.Renamed, _) => "Renamed",
        (_, GitFileStatus.Renamed) => "Renamed",
        (GitFileStatus.Untracked, _) => "Untracked",
        (_, GitFileStatus.Untracked) => "Untracked",
        (GitFileStatus.Ignored, _) => "Ignored",
        (_, GitFileStatus.Ignored) => "Ignored",
        (GitFileStatus.Unmerged, _) => "Merge conflict",
        (_, GitFileStatus.Unmerged) => "Merge conflict",
        _ => string.Empty
    };
}

/// <summary>
/// Service for Git integration.
/// </summary>
public class GitService
{
    private readonly Dictionary<string, GitRepositoryInfo> _repositoryCache = new();
    private readonly Dictionary<string, Dictionary<string, GitFileStatusInfo>> _statusCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();
    
    /// <summary>
    /// Check if git is available on the system.
    /// </summary>
    public bool IsGitAvailable()
    {
        try
        {
            var result = RunGitCommand(".", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Find the git repository root for a given path.
    /// </summary>
    public string? FindRepositoryRoot(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
            
        try
        {
            var result = RunGitCommand(path, "rev-parse --show-toplevel");
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                return result.Output.Trim();
            }
        }
        catch { }
        
        return null;
    }
    
    /// <summary>
    /// Check if a directory is inside a git repository.
    /// </summary>
    public bool IsInGitRepository(string path)
    {
        return FindRepositoryRoot(path) != null;
    }
    
    /// <summary>
    /// Get repository information for the given path.
    /// </summary>
    public GitRepositoryInfo? GetRepositoryInfo(string path)
    {
        var repoRoot = FindRepositoryRoot(path);
        if (string.IsNullOrEmpty(repoRoot))
            return null;
            
        // Check cache
        if (_repositoryCache.TryGetValue(repoRoot, out var cached) &&
            _cacheTimestamps.TryGetValue(repoRoot, out var timestamp) &&
            DateTime.Now - timestamp < _cacheExpiry)
        {
            return cached;
        }
        
        var info = new GitRepositoryInfo
        {
            RepositoryPath = repoRoot
        };
        
        // Get branch name
        var branchResult = RunGitCommand(repoRoot, "rev-parse --abbrev-ref HEAD");
        if (branchResult.ExitCode == 0)
        {
            info.BranchName = branchResult.Output.Trim();
        }
        
        // Get ahead/behind count
        var trackResult = RunGitCommand(repoRoot, "rev-list --left-right --count HEAD...@{upstream}");
        if (trackResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(trackResult.Output))
        {
            var parts = trackResult.Output.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                int.TryParse(parts[0], out var ahead);
                int.TryParse(parts[1], out var behind);
                info.AheadCount = ahead;
                info.BehindCount = behind;
            }
        }
        
        // Get status summary
        var statusResult = RunGitCommand(repoRoot, "status --porcelain");
        if (statusResult.ExitCode == 0)
        {
            var lines = statusResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            info.HasUncommittedChanges = lines.Any(l => l.Length > 0 && l[0] != '?');
            info.HasUntrackedFiles = lines.Any(l => l.StartsWith("??"));
        }
        
        // Check stash
        var stashResult = RunGitCommand(repoRoot, "stash list");
        if (stashResult.ExitCode == 0)
        {
            info.HasStashedChanges = !string.IsNullOrWhiteSpace(stashResult.Output);
        }
        
        // Cache the result
        _repositoryCache[repoRoot] = info;
        _cacheTimestamps[repoRoot] = DateTime.Now;
        
        return info;
    }
    
    /// <summary>
    /// Get file status for all files in a directory.
    /// </summary>
    public Dictionary<string, GitFileStatusInfo> GetDirectoryStatus(string directoryPath)
    {
        var result = new Dictionary<string, GitFileStatusInfo>(StringComparer.OrdinalIgnoreCase);
        
        var repoRoot = FindRepositoryRoot(directoryPath);
        if (string.IsNullOrEmpty(repoRoot))
            return result;
            
        var cacheKey = directoryPath;
        
        // Check cache
        if (_statusCache.TryGetValue(cacheKey, out var cached) &&
            _cacheTimestamps.TryGetValue(cacheKey + "_status", out var timestamp) &&
            DateTime.Now - timestamp < _cacheExpiry)
        {
            return cached;
        }
        
        // Get status for all files
        var statusResult = RunGitCommand(repoRoot, "status --porcelain -uall");
        if (statusResult.ExitCode != 0)
            return result;
            
        var lines = statusResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Length < 4)
                continue;
                
            var indexStatus = ParseStatusChar(line[0]);
            var workTreeStatus = ParseStatusChar(line[1]);
            var relativePath = line[3..].Trim();
            
            // Handle renamed files (format: "R  old -> new")
            if (relativePath.Contains(" -> "))
            {
                var parts = relativePath.Split(" -> ");
                if (parts.Length == 2)
                    relativePath = parts[1];
            }
            
            var fullPath = Path.Combine(repoRoot, relativePath);
            
            // Check if file is in the requested directory or its subdirectories
            var fileDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (!fileDir.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
                continue;
                
            result[fullPath] = new GitFileStatusInfo
            {
                FilePath = fullPath,
                IndexStatus = indexStatus,
                WorkTreeStatus = workTreeStatus
            };
            
            // Also add parent directories as modified if they contain modified files
            var parentDir = fileDir;
            while (!string.IsNullOrEmpty(parentDir) && parentDir.Length > directoryPath.Length)
            {
                if (!result.ContainsKey(parentDir))
                {
                    result[parentDir] = new GitFileStatusInfo
                    {
                        FilePath = parentDir,
                        IndexStatus = GitFileStatus.None,
                        WorkTreeStatus = GitFileStatus.Modified
                    };
                }
                parentDir = Path.GetDirectoryName(parentDir);
            }
        }
        
        // Cache the result
        _statusCache[cacheKey] = result;
        _cacheTimestamps[cacheKey + "_status"] = DateTime.Now;
        
        return result;
    }
    
    /// <summary>
    /// Get status for a specific file.
    /// </summary>
    public GitFileStatusInfo? GetFileStatus(string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directoryPath))
            return null;
            
        var statuses = GetDirectoryStatus(directoryPath);
        return statuses.TryGetValue(filePath, out var status) ? status : null;
    }
    
    /// <summary>
    /// Stage a file for commit.
    /// </summary>
    public bool StageFile(string filePath)
    {
        var repoRoot = FindRepositoryRoot(filePath);
        if (string.IsNullOrEmpty(repoRoot))
            return false;
            
        var result = RunGitCommand(repoRoot, $"add \"{filePath}\"");
        ClearCache(repoRoot);
        return result.ExitCode == 0;
    }
    
    /// <summary>
    /// Unstage a file.
    /// </summary>
    public bool UnstageFile(string filePath)
    {
        var repoRoot = FindRepositoryRoot(filePath);
        if (string.IsNullOrEmpty(repoRoot))
            return false;
            
        var result = RunGitCommand(repoRoot, $"reset HEAD \"{filePath}\"");
        ClearCache(repoRoot);
        return result.ExitCode == 0;
    }
    
    /// <summary>
    /// Discard changes to a file.
    /// </summary>
    public bool DiscardChanges(string filePath)
    {
        var repoRoot = FindRepositoryRoot(filePath);
        if (string.IsNullOrEmpty(repoRoot))
            return false;
            
        var result = RunGitCommand(repoRoot, $"checkout -- \"{filePath}\"");
        ClearCache(repoRoot);
        return result.ExitCode == 0;
    }
    
    /// <summary>
    /// Get diff for a file.
    /// </summary>
    public string GetFileDiff(string filePath, bool staged = false)
    {
        var repoRoot = FindRepositoryRoot(filePath);
        if (string.IsNullOrEmpty(repoRoot))
            return string.Empty;
            
        var stagedFlag = staged ? "--cached " : "";
        var result = RunGitCommand(repoRoot, $"diff {stagedFlag}\"{filePath}\"");
        return result.ExitCode == 0 ? result.Output : string.Empty;
    }
    
    /// <summary>
    /// Get log for a file.
    /// </summary>
    public string GetFileLog(string filePath, int maxEntries = 20)
    {
        var repoRoot = FindRepositoryRoot(filePath);
        if (string.IsNullOrEmpty(repoRoot))
            return string.Empty;
            
        var result = RunGitCommand(repoRoot, $"log --oneline -{maxEntries} -- \"{filePath}\"");
        return result.ExitCode == 0 ? result.Output : string.Empty;
    }
    
    /// <summary>
    /// Clear cache for a repository.
    /// </summary>
    public void ClearCache(string? repoRoot = null)
    {
        if (string.IsNullOrEmpty(repoRoot))
        {
            _repositoryCache.Clear();
            _statusCache.Clear();
            _cacheTimestamps.Clear();
        }
        else
        {
            _repositoryCache.Remove(repoRoot);
            var keysToRemove = _statusCache.Keys.Where(k => k.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in keysToRemove)
            {
                _statusCache.Remove(key);
                _cacheTimestamps.Remove(key + "_status");
            }
            _cacheTimestamps.Remove(repoRoot);
        }
    }
    
    private GitFileStatus ParseStatusChar(char c) => c switch
    {
        'M' => GitFileStatus.Modified,
        'A' => GitFileStatus.Added,
        'D' => GitFileStatus.Deleted,
        'R' => GitFileStatus.Renamed,
        'C' => GitFileStatus.Copied,
        '?' => GitFileStatus.Untracked,
        '!' => GitFileStatus.Ignored,
        'U' => GitFileStatus.Unmerged,
        _ => GitFileStatus.None
    };
    
    private (int ExitCode, string Output) RunGitCommand(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null)
                return (-1, string.Empty);
                
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000); // 5 second timeout
            
            return (process.ExitCode, output);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Git command failed: {ex.Message}");
            return (-1, string.Empty);
        }
    }
}
