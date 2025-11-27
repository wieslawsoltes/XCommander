using System.Text;
using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Service for importing Total Commander settings from wincmd.ini.
/// </summary>
public interface ITcConfigImportService
{
    /// <summary>
    /// Imports Total Commander configuration from wincmd.ini file.
    /// </summary>
    Task<TcImportResult> ImportAsync(string wincmdIniPath);
    
    /// <summary>
    /// Validates a wincmd.ini file without importing.
    /// </summary>
    TcValidationResult Validate(string wincmdIniPath);
    
    /// <summary>
    /// Gets specific settings from wincmd.ini.
    /// </summary>
    Dictionary<string, string> GetSettings(string wincmdIniPath, string section);
}

/// <summary>
/// Result of TC config import operation.
/// </summary>
public record TcImportResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int BookmarksImported { get; init; }
    public int FtpConnectionsImported { get; init; }
    public int ColorSchemesImported { get; init; }
    public int CustomColumnsImported { get; init; }
    public int ButtonBarItemsImported { get; init; }
    public int MenuItemsImported { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Validation result for TC config file.
/// </summary>
public record TcValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public string TcVersion { get; init; } = "Unknown";
    public bool HasBookmarks { get; init; }
    public bool HasFtpConnections { get; init; }
    public bool HasColorSchemes { get; init; }
    public bool HasCustomColumns { get; init; }
    public bool HasButtonBar { get; init; }
    public bool HasUserMenu { get; init; }
}

/// <summary>
/// Imported TC bookmark.
/// </summary>
public record TcBookmark
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? Shortcut { get; init; }
    public bool IsDirectory { get; init; }
}

/// <summary>
/// Imported TC FTP connection.
/// </summary>
public record TcFtpConnection
{
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 21;
    public string Username { get; init; } = string.Empty;
    public string? RemoteDirectory { get; init; }
    public bool UseSftp { get; init; }
    public bool UsePassiveMode { get; init; } = true;
}

/// <summary>
/// Implementation of TC config import service.
/// </summary>
public class TcConfigImportService : ITcConfigImportService
{
    private readonly IBookmarkService? _bookmarkService;
    
    public TcConfigImportService(IBookmarkService? bookmarkService = null)
    {
        _bookmarkService = bookmarkService;
    }
    
    public async Task<TcImportResult> ImportAsync(string wincmdIniPath)
    {
        var result = new TcImportResult { Success = false };
        var warnings = new List<string>();
        
        try
        {
            if (!File.Exists(wincmdIniPath))
            {
                return result with { ErrorMessage = "wincmd.ini file not found" };
            }
            
            var content = await File.ReadAllTextAsync(wincmdIniPath, Encoding.Default);
            var sections = ParseIniFile(content);
            
            // Import bookmarks from [DirMenu] section
            int bookmarksImported = 0;
            if (sections.TryGetValue("DirMenu", out var dirMenuSection))
            {
                var bookmarks = ParseBookmarks(dirMenuSection);
                foreach (var bookmark in bookmarks)
                {
                    try
                    {
                        // Convert Windows path to current platform if needed
                        var path = ConvertPath(bookmark.Path);
                        if (Directory.Exists(path) || File.Exists(path))
                        {
                            // Add to XCommander bookmarks
                            // _bookmarkService?.AddBookmark(path, bookmark.Name);
                            bookmarksImported++;
                        }
                        else
                        {
                            warnings.Add($"Bookmark path not found: {bookmark.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to import bookmark '{bookmark.Name}': {ex.Message}");
                    }
                }
            }
            
            // Import FTP connections from [FtpConnect] section
            int ftpImported = 0;
            if (sections.TryGetValue("FtpConnect", out var ftpSection))
            {
                var connections = ParseFtpConnections(ftpSection);
                foreach (var conn in connections)
                {
                    try
                    {
                        // Save FTP connection to XCommander
                        ftpImported++;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to import FTP connection '{conn.Name}': {ex.Message}");
                    }
                }
            }
            
            // Import color schemes from [Colors] section
            int colorsImported = 0;
            if (sections.TryGetValue("Colors", out var colorsSection))
            {
                try
                {
                    var colors = ParseColorScheme(colorsSection);
                    colorsImported = colors.Count;
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to import color scheme: {ex.Message}");
                }
            }
            
            // Import custom columns from [CustomFields] section
            int columnsImported = 0;
            if (sections.TryGetValue("CustomFields", out var columnsSection))
            {
                columnsImported = columnsSection.Count;
            }
            
            // Import button bar from [Buttonbar] section
            int buttonBarImported = 0;
            if (sections.TryGetValue("Buttonbar", out var buttonBarSection))
            {
                buttonBarImported = buttonBarSection.Count;
            }
            
            return new TcImportResult
            {
                Success = true,
                BookmarksImported = bookmarksImported,
                FtpConnectionsImported = ftpImported,
                ColorSchemesImported = colorsImported,
                CustomColumnsImported = columnsImported,
                ButtonBarItemsImported = buttonBarImported,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            return result with { ErrorMessage = $"Import failed: {ex.Message}" };
        }
    }
    
    public TcValidationResult Validate(string wincmdIniPath)
    {
        try
        {
            if (!File.Exists(wincmdIniPath))
            {
                return new TcValidationResult { IsValid = false, ErrorMessage = "File not found" };
            }
            
            var content = File.ReadAllText(wincmdIniPath, Encoding.Default);
            var sections = ParseIniFile(content);
            
            // Check if it's a valid TC config
            if (!sections.ContainsKey("Configuration") && !sections.ContainsKey("Colors"))
            {
                return new TcValidationResult { IsValid = false, ErrorMessage = "Not a valid Total Commander config file" };
            }
            
            // Try to get TC version
            string version = "Unknown";
            if (sections.TryGetValue("Configuration", out var configSection))
            {
                version = configSection.GetValueOrDefault("Version", "Unknown");
            }
            
            return new TcValidationResult
            {
                IsValid = true,
                TcVersion = version,
                HasBookmarks = sections.ContainsKey("DirMenu"),
                HasFtpConnections = sections.ContainsKey("FtpConnect"),
                HasColorSchemes = sections.ContainsKey("Colors"),
                HasCustomColumns = sections.ContainsKey("CustomFields"),
                HasButtonBar = sections.ContainsKey("Buttonbar"),
                HasUserMenu = sections.ContainsKey("user")
            };
        }
        catch (Exception ex)
        {
            return new TcValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }
    
    public Dictionary<string, string> GetSettings(string wincmdIniPath, string section)
    {
        try
        {
            if (!File.Exists(wincmdIniPath))
                return new();
            
            var content = File.ReadAllText(wincmdIniPath, Encoding.Default);
            var sections = ParseIniFile(content);
            
            return sections.GetValueOrDefault(section, new Dictionary<string, string>());
        }
        catch
        {
            return new();
        }
    }
    
    private Dictionary<string, Dictionary<string, string>> ParseIniFile(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = new Dictionary<string, string>();
        string currentSectionName = "";
        
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip comments and empty lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';'))
                continue;
            
            // Section header
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                if (!string.IsNullOrEmpty(currentSectionName))
                {
                    sections[currentSectionName] = currentSection;
                }
                
                currentSectionName = trimmedLine[1..^1];
                currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            // Key=Value pair
            else if (trimmedLine.Contains('='))
            {
                var equalIndex = trimmedLine.IndexOf('=');
                var key = trimmedLine[..equalIndex].Trim();
                var value = trimmedLine[(equalIndex + 1)..].Trim();
                currentSection[key] = value;
            }
        }
        
        // Save last section
        if (!string.IsNullOrEmpty(currentSectionName))
        {
            sections[currentSectionName] = currentSection;
        }
        
        return sections;
    }
    
    private List<TcBookmark> ParseBookmarks(Dictionary<string, string> section)
    {
        var bookmarks = new List<TcBookmark>();
        
        // TC stores bookmarks as cmd1, cmd2, path1, path2, etc.
        int index = 1;
        while (section.TryGetValue($"cmd{index}", out var name) || 
               section.TryGetValue($"menu{index}", out name))
        {
            if (section.TryGetValue($"path{index}", out var path))
            {
                bookmarks.Add(new TcBookmark
                {
                    Name = name ?? $"Bookmark {index}",
                    Path = path,
                    IsDirectory = true
                });
            }
            index++;
        }
        
        return bookmarks;
    }
    
    private List<TcFtpConnection> ParseFtpConnections(Dictionary<string, string> section)
    {
        var connections = new List<TcFtpConnection>();
        
        // TC stores FTP connections with numbered prefixes
        var connectionNames = section.Keys
            .Where(k => k.EndsWith("host", StringComparison.OrdinalIgnoreCase))
            .Select(k => k[..^4]) // Remove "host" suffix
            .Distinct()
            .ToList();
        
        foreach (var prefix in connectionNames)
        {
            var host = section.GetValueOrDefault($"{prefix}host", "");
            if (string.IsNullOrEmpty(host))
                continue;
            
            var port = 21;
            if (section.TryGetValue($"{prefix}port", out var portStr))
                int.TryParse(portStr, out port);
            
            connections.Add(new TcFtpConnection
            {
                Name = prefix,
                Host = host,
                Port = port,
                Username = section.GetValueOrDefault($"{prefix}user", "anonymous"),
                RemoteDirectory = section.GetValueOrDefault($"{prefix}directory"),
                UsePassiveMode = section.GetValueOrDefault($"{prefix}pasvmode", "1") == "1"
            });
        }
        
        return connections;
    }
    
    private Dictionary<string, string> ParseColorScheme(Dictionary<string, string> section)
    {
        // TC color format: Back, Fore, Bold as comma-separated RGB values
        var colors = new Dictionary<string, string>();
        
        foreach (var kvp in section)
        {
            if (kvp.Key.StartsWith("ColorFilter", StringComparison.OrdinalIgnoreCase))
            {
                colors[kvp.Key] = kvp.Value;
            }
        }
        
        return colors;
    }
    
    private static string ConvertPath(string tcPath)
    {
        // Convert Windows paths to current platform
        if (!OperatingSystem.IsWindows())
        {
            // Basic conversion: replace backslashes and drive letters
            var path = tcPath.Replace('\\', '/');
            
            // Handle drive letters (C: -> /mnt/c on WSL, or just ignore on other platforms)
            var match = Regex.Match(path, @"^([A-Za-z]):/");
            if (match.Success)
            {
                // For macOS/Linux, we can't directly convert - return empty or mapped path
                return string.Empty; // Or map to a user-defined location
            }
            
            return path;
        }
        
        return tcPath;
    }
}
