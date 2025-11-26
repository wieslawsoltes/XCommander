namespace XCommander.Plugins.BuiltIn;

/// <summary>
/// Built-in column plugin that provides additional file metadata columns.
/// </summary>
public class FileMetadataColumnPlugin : IColumnPlugin
{
    public string Id => "xcommander.columns.metadata";
    public string Name => "File Metadata Columns";
    public string Description => "Provides additional file metadata columns such as owner, permissions, and encoding.";
    public Version Version => new(1, 0, 0);
    public string Author => "XCommander Team";

    private IPluginContext? _context;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _context.Log(PluginLogLevel.Info, $"{Name} initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(PluginLogLevel.Info, $"{Name} shutdown");
        return Task.CompletedTask;
    }

    public IEnumerable<PluginColumn> GetColumns()
    {
        yield return new PluginColumn
        {
            Id = "owner",
            Name = "Owner",
            Description = "File owner/user",
            DefaultWidth = 120
        };

        yield return new PluginColumn
        {
            Id = "permissions",
            Name = "Permissions",
            Description = "File permissions (Unix-style)",
            DefaultWidth = 100
        };

        yield return new PluginColumn
        {
            Id = "md5",
            Name = "MD5",
            Description = "MD5 checksum (calculated on demand)",
            DefaultWidth = 250
        };

        yield return new PluginColumn
        {
            Id = "accessTime",
            Name = "Last Access",
            Description = "Last access time",
            DefaultWidth = 150
        };

        yield return new PluginColumn
        {
            Id = "lineCount",
            Name = "Lines",
            Description = "Line count (for text files)",
            DefaultWidth = 80,
            Alignment = PluginColumnAlignment.Right
        };
    }

    public async Task<object?> GetValueAsync(string columnId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath) && !Directory.Exists(filePath))
            return null;

        return columnId switch
        {
            "owner" => GetOwner(filePath),
            "permissions" => GetPermissions(filePath),
            "md5" => await GetMd5Async(filePath, cancellationToken),
            "accessTime" => GetAccessTime(filePath),
            "lineCount" => await GetLineCountAsync(filePath, cancellationToken),
            _ => null
        };
    }

    private static string? GetOwner(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // On Windows, we'd need System.Security.AccessControl
                return null;
            }
            else
            {
                var info = new FileInfo(filePath);
                // On Unix, we can use UnixFileSystemInfo but it requires Mono.Posix
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? GetPermissions(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var attr = File.GetAttributes(filePath);
                var parts = new List<string>();
                
                if ((attr & FileAttributes.ReadOnly) != 0) parts.Add("R");
                if ((attr & FileAttributes.Hidden) != 0) parts.Add("H");
                if ((attr & FileAttributes.System) != 0) parts.Add("S");
                if ((attr & FileAttributes.Archive) != 0) parts.Add("A");
                
                return string.Join("", parts);
            }
            else
            {
                // On Unix, we'd format rwxrwxrwx style
                var info = new FileInfo(filePath);
                var mode = (int)(new UnixFileInfo(filePath)?.FileAccessPermissions ?? 0);
                return FormatUnixPermissions(mode);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string FormatUnixPermissions(int mode)
    {
        var chars = new char[9];
        chars[0] = (mode & 0x100) != 0 ? 'r' : '-';
        chars[1] = (mode & 0x080) != 0 ? 'w' : '-';
        chars[2] = (mode & 0x040) != 0 ? 'x' : '-';
        chars[3] = (mode & 0x020) != 0 ? 'r' : '-';
        chars[4] = (mode & 0x010) != 0 ? 'w' : '-';
        chars[5] = (mode & 0x008) != 0 ? 'x' : '-';
        chars[6] = (mode & 0x004) != 0 ? 'r' : '-';
        chars[7] = (mode & 0x002) != 0 ? 'w' : '-';
        chars[8] = (mode & 0x001) != 0 ? 'x' : '-';
        return new string(chars);
    }

    // Stub class for Unix file info (would need Mono.Posix.NETStandard)
    private class UnixFileInfo
    {
        public UnixFileInfo(string path) { }
        public int? FileAccessPermissions => null;
    }

    private static async Task<string?> GetMd5Async(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (Directory.Exists(filePath))
                return null;

            var info = new FileInfo(filePath);
            // Only calculate for small files (< 10MB) to avoid UI freezing
            if (info.Length > 10 * 1024 * 1024)
                return "<too large>";

            using var md5 = System.Security.Cryptography.MD5.Create();
            await using var stream = File.OpenRead(filePath);
            var hash = await md5.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? GetAccessTime(string filePath)
    {
        try
        {
            return File.GetLastAccessTime(filePath);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> GetLineCountAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (Directory.Exists(filePath))
                return null;

            var info = new FileInfo(filePath);
            // Only count for text files (< 5MB)
            if (info.Length > 5 * 1024 * 1024)
                return null;

            // Check if it's likely a text file
            var ext = info.Extension.ToLowerInvariant();
            var textExtensions = new[] { ".txt", ".md", ".cs", ".js", ".ts", ".json", ".xml", ".html", ".css", ".py", ".java", ".cpp", ".h", ".c", ".yaml", ".yml", ".ini", ".cfg", ".log" };
            
            if (!textExtensions.Contains(ext))
                return null;

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            return lines.Length;
        }
        catch
        {
            return null;
        }
    }
}
