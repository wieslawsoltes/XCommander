using System.Text;
using XCommander.Models;

namespace XCommander.Services;

/// <summary>
/// Service for importing Total Commander settings from wincmd.ini.
/// </summary>
public interface ITcConfigImportService
{
    /// <summary>
    /// Imports Total Commander configuration from wincmd.ini file.
    /// </summary>
    Task<TcImportResult> ImportAsync(string wincmdIniPath, TcImportOptions? options = null);
    
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
/// Options for importing TC configuration.
/// </summary>
public record TcImportOptions
{
    public bool ImportBookmarks { get; init; } = true;
    public bool ImportFtpConnections { get; init; } = true;
    public bool ImportColorSchemes { get; init; } = true;
    public bool ImportCustomColumns { get; init; } = true;
    public bool ImportButtonBar { get; init; } = true;
    public bool ImportUserMenu { get; init; } = true;
    public bool MergeWithExisting { get; init; } = true;
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
    public bool UseFtps { get; init; }
    public bool UsePassiveMode { get; init; } = true;
}

/// <summary>
/// Implementation of TC config import service.
/// </summary>
public class TcConfigImportService : ITcConfigImportService
{
    private readonly IBookmarkService? _bookmarkService;
    private readonly IDirectoryHotlistService? _hotlistService;
    private readonly ICustomColumnService? _customColumnService;
    private readonly IButtonBarService? _buttonBarService;
    private readonly IUserMenuService? _userMenuService;
    
    public TcConfigImportService(
        IBookmarkService? bookmarkService = null,
        IDirectoryHotlistService? hotlistService = null,
        ICustomColumnService? customColumnService = null,
        IButtonBarService? buttonBarService = null,
        IUserMenuService? userMenuService = null)
    {
        _bookmarkService = bookmarkService;
        _hotlistService = hotlistService;
        _customColumnService = customColumnService;
        _buttonBarService = buttonBarService;
        _userMenuService = userMenuService;
    }
    
    public async Task<TcImportResult> ImportAsync(string wincmdIniPath, TcImportOptions? options = null)
    {
        var result = new TcImportResult { Success = false };
        var warnings = new List<string>();
        options ??= new TcImportOptions();
        
        try
        {
            if (!File.Exists(wincmdIniPath))
            {
                return result with { ErrorMessage = "wincmd.ini file not found" };
            }
            
            var ini = IniFile.Load(wincmdIniPath, Encoding.Default);
            var sections = ini.Sections;
            var userCommands = LoadUserCommands(wincmdIniPath, sections, warnings);
            
            // Import bookmarks (Directory Hotlist) from [DirMenu] section
            int bookmarksImported = 0;
            if (options.ImportBookmarks && sections.TryGetValue("DirMenu", out var dirMenuSection))
            {
                if (_hotlistService == null)
                {
                    warnings.Add("Directory hotlist service not available; bookmarks were not imported.");
                }
                else
                {
                    try
                    {
                        var hotlistData = ParseHotlist(dirMenuSection, warnings);
                        bookmarksImported = hotlistData.Items.Count(i => i.Type == HotlistItemType.Directory);
                        await _hotlistService.ImportAsync(hotlistData, merge: options.MergeWithExisting);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to import directory hotlist: {ex.Message}");
                    }
                }
            }
            
            // Import FTP connections from [FtpConnect] section
            int ftpImported = 0;
            if (options.ImportFtpConnections && sections.TryGetValue("FtpConnect", out var ftpSection))
            {
                try
                {
                    ftpImported = ImportFtpConnections(ftpSection, warnings);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to import FTP connections: {ex.Message}");
                }
            }
            
            // Import color schemes from [Colors] section
            int colorsImported = 0;
            if (options.ImportColorSchemes && sections.TryGetValue("Colors", out var colorsSection))
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
            if (options.ImportCustomColumns && sections.TryGetValue("CustomFields", out var columnsSection))
            {
                if (_customColumnService == null)
                {
                    warnings.Add("Custom column service not available; custom columns were not imported.");
                }
                else
                {
                    columnsImported = await ImportCustomColumnsAsync(columnsSection, warnings, options.MergeWithExisting);
                }
            }
            
            // Import button bar from [Buttonbar] section
            int buttonBarImported = 0;
            if (options.ImportButtonBar && sections.TryGetValue("Buttonbar", out var buttonBarSection))
            {
                buttonBarImported = await ImportButtonBarAsync(buttonBarSection, warnings, userCommands);
            }
            
            // Import user menu from [user] section
            int menuItemsImported = 0;
            if (options.ImportUserMenu && sections.TryGetValue("user", out var userMenuSection))
            {
                if (_userMenuService == null)
                {
                    warnings.Add("User menu service not available; user menu was not imported.");
                }
                else
                {
                    var items = ParseUserMenuItems(userMenuSection, warnings, userCommands);
                    menuItemsImported = CountMenuItems(items);
                    await PersistUserMenuAsync(items, warnings);
                }
            }
            
            return new TcImportResult
            {
                Success = true,
                BookmarksImported = bookmarksImported,
                FtpConnectionsImported = ftpImported,
                ColorSchemesImported = colorsImported,
                CustomColumnsImported = columnsImported,
                ButtonBarItemsImported = buttonBarImported,
                MenuItemsImported = menuItemsImported,
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
            
            var ini = IniFile.Load(wincmdIniPath, Encoding.Default);
            var sections = ini.Sections;
            
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
            
            var ini = IniFile.Load(wincmdIniPath, Encoding.Default);
            return ini.GetSection(section);
        }
        catch
        {
            return new();
        }
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
            
            var protocolHint = section.GetValueOrDefault($"{prefix}protocol") ?? string.Empty;
            var useSftp = section.GetValueOrDefault($"{prefix}sftp", "0") == "1" ||
                          protocolHint.Contains("sftp", StringComparison.OrdinalIgnoreCase) ||
                          port == 22;
            var useFtps = section.GetValueOrDefault($"{prefix}ssl", "0") == "1" ||
                          section.GetValueOrDefault($"{prefix}tls", "0") == "1" ||
                          protocolHint.Contains("ftps", StringComparison.OrdinalIgnoreCase);
            
            var name = section.GetValueOrDefault($"{prefix}name", prefix);
            
            connections.Add(new TcFtpConnection
            {
                Name = name,
                Host = host,
                Port = port,
                Username = section.GetValueOrDefault($"{prefix}user", "anonymous"),
                RemoteDirectory = section.GetValueOrDefault($"{prefix}directory"),
                UseSftp = useSftp,
                UseFtps = useFtps,
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

    private int ImportFtpConnections(Dictionary<string, string> section, List<string> warnings)
    {
        var connections = ParseFtpConnections(section);
        if (connections.Count == 0)
            return 0;

        var data = FtpConnectionsData.Load();
        var imported = 0;

        foreach (var conn in connections)
        {
            var protocol = ResolveProtocol(conn);
            var existing = data.Connections.FirstOrDefault(c =>
                string.Equals(c.Host, conn.Host, StringComparison.OrdinalIgnoreCase) &&
                c.Port == conn.Port &&
                string.Equals(c.Username, conn.Username, StringComparison.OrdinalIgnoreCase) &&
                c.Protocol == protocol);

            if (existing != null)
            {
                existing.Name = conn.Name;
                existing.RemotePath = conn.RemoteDirectory ?? existing.RemotePath;
                existing.UsePassiveMode = conn.UsePassiveMode;
                existing.LastUsed = DateTime.Now;
                continue;
            }

            data.Connections.Add(new FtpConnection
            {
                Name = conn.Name,
                Host = conn.Host,
                Port = conn.Port,
                Username = conn.Username,
                RemotePath = conn.RemoteDirectory ?? "/",
                Protocol = protocol,
                UsePassiveMode = conn.UsePassiveMode,
                Order = data.Connections.Count,
                Created = DateTime.Now
            });
            imported++;
        }

        data.Save();
        return imported;
    }

    private static ConnectionProtocol ResolveProtocol(TcFtpConnection connection)
    {
        if (connection.UseSftp)
            return ConnectionProtocol.Sftp;
        if (connection.UseFtps)
            return ConnectionProtocol.Ftps;
        return ConnectionProtocol.Ftp;
    }

    private HotlistExportData ParseHotlist(Dictionary<string, string> section, List<string> warnings)
    {
        var items = new List<HotlistItem>();
        var categories = new List<HotlistCategory>();
        var categoryStack = new Stack<string?>();
        var orderByCategory = new Dictionary<string, int>();
        var categoryOrder = 0;
        categoryStack.Push(null);

        var maxIndex = GetMaxIndex(section, "menu", "cmd", "path");
        for (var index = 1; index <= maxIndex; index++)
        {
            section.TryGetValue($"menu{index}", out var label);
            section.TryGetValue($"cmd{index}", out var command);
            section.TryGetValue($"path{index}", out var path);

            if (string.IsNullOrWhiteSpace(label) &&
                string.IsNullOrWhiteSpace(command) &&
                string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (IsSubmenuEnd(command))
            {
                if (categoryStack.Count > 1)
                    categoryStack.Pop();
                continue;
            }

            if (IsSubmenuStart(command))
            {
                var name = string.IsNullOrWhiteSpace(label) ? $"Group {index}" : label.Trim();
                var categoryId = Guid.NewGuid().ToString();
                categories.Add(new HotlistCategory
                {
                    Id = categoryId,
                    Name = name,
                    ParentCategoryId = categoryStack.Peek(),
                    Order = categoryOrder++
                });
                categoryStack.Push(categoryId);
                continue;
            }

            if (IsSeparator(label, command))
            {
                items.Add(new HotlistItem
                {
                    Type = HotlistItemType.Separator,
                    ParentCategoryId = categoryStack.Peek(),
                    Order = NextOrder(orderByCategory, categoryStack.Peek())
                });
                continue;
            }

            var resolvedPath = ResolvePathFromCommand(command, path);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                warnings.Add($"Skipped hotlist item {index}: missing path.");
                continue;
            }

            var convertedPath = ConvertPath(resolvedPath);
            var displayName = string.IsNullOrWhiteSpace(label)
                ? Path.GetFileName(convertedPath) ?? convertedPath
                : label.Trim();

            items.Add(new HotlistItem
            {
                Type = HotlistItemType.Directory,
                Name = displayName,
                Path = convertedPath,
                ParentCategoryId = categoryStack.Peek(),
                Order = NextOrder(orderByCategory, categoryStack.Peek())
            });
        }

        return new HotlistExportData
        {
            Items = items,
            Categories = categories,
            ExportedAt = DateTime.Now
        };
    }

    private static int NextOrder(IDictionary<string, int> orderByCategory, string? categoryId)
    {
        var key = categoryId ?? string.Empty;
        if (!orderByCategory.TryGetValue(key, out var order))
            order = 0;
        orderByCategory[key] = order + 1;
        return order;
    }

    private static bool IsSeparator(string? label, string? command)
    {
        if (!string.IsNullOrWhiteSpace(label) && label.Trim('-', ' ').Length == 0)
            return true;
        return string.Equals(command, "-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubmenuStart(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;
        var normalized = command.Trim();
        return string.Equals(normalized, "menu", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "submenu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubmenuEnd(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;
        var normalized = command.Trim();
        return string.Equals(normalized, "endmenu", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "end", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolvePathFromCommand(string? command, string? path)
    {
        var resolved = path;
        if (string.IsNullOrWhiteSpace(resolved) && !string.IsNullOrWhiteSpace(command))
        {
            var trimmed = command.Trim();
            if (trimmed.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(2).Trim();
                if (trimmed.StartsWith("/d", StringComparison.OrdinalIgnoreCase))
                    trimmed = trimmed.Substring(2).Trim();
                if (trimmed.StartsWith("="))
                    trimmed = trimmed.Substring(1).Trim();
            }
            resolved = trimmed;
        }

        if (string.IsNullOrWhiteSpace(resolved))
            return null;

        return resolved.Trim().Trim('"');
    }

    private static int GetMaxIndex(Dictionary<string, string> section, params string[] prefixes)
    {
        var maxIndex = 0;
        foreach (var key in section.Keys)
        {
            foreach (var prefix in prefixes)
            {
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var suffix = key.Substring(prefix.Length);
                if (int.TryParse(suffix, out var index) && index > maxIndex)
                    maxIndex = index;
            }
        }
        return maxIndex;
    }

    private async Task<int> ImportCustomColumnsAsync(
        Dictionary<string, string> section,
        List<string> warnings,
        bool mergeExisting)
    {
        var sets = ParseCustomColumnSets(section);
        if (sets.Count == 0)
        {
            warnings.Add("No recognizable custom column sets found in [CustomFields].");
            return 0;
        }

        var imported = 0;
        var customColumnService = _customColumnService ?? throw new InvalidOperationException("Custom column service is not available.");
        var existingColumns = customColumnService.GetUserColumns();
        var existingSets = customColumnService.GetColumnSets();

        foreach (var set in sets)
        {
            var columnIds = new List<string>();
            for (var i = 0; i < set.Fields.Count; i++)
            {
                var field = set.Fields[i];
                var displayName = i < set.Names.Count && !string.IsNullOrWhiteSpace(set.Names[i])
                    ? set.Names[i]
                    : field;
                var width = i < set.Widths.Count ? set.Widths[i] : 120;
                var dataType = InferDataType(field);
                var alignment = i < set.Alignments.Count
                    ? ParseAlignment(set.Alignments[i], dataType)
                    : DefaultAlignment(dataType);

                if (TryMapFieldToBuiltInId(field, out var builtInId))
                {
                    columnIds.Add(builtInId);
                    continue;
                }

                var existing = existingColumns.FirstOrDefault(c =>
                    string.Equals(c.DisplayName, displayName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.SourceField, field, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    columnIds.Add(existing.Id);
                    continue;
                }

                var definition = new CustomColumnDefinition
                {
                    Name = displayName,
                    DisplayName = displayName,
                    Source = CustomColumnSource.Expression,
                    SourceField = field,
                    DataType = dataType,
                    Alignment = alignment,
                    Width = width,
                    DisplayOrder = i,
                    Visible = true
                };

                var created = await customColumnService.CreateColumnAsync(definition);
                existingColumns = customColumnService.GetUserColumns();
                columnIds.Add(created.Id);
                imported++;
            }

            if (columnIds.Count == 0)
                continue;

            var setName = string.IsNullOrWhiteSpace(set.Name) ? set.Id : set.Name;
            var existingSet = existingSets.FirstOrDefault(s =>
                string.Equals(s.Name, setName, StringComparison.OrdinalIgnoreCase));

            if (existingSet != null && mergeExisting && !existingSet.IsBuiltIn)
            {
                await customColumnService.UpdateColumnSetAsync(existingSet with
                {
                    ColumnIds = columnIds
                });
                continue;
            }

            await customColumnService.CreateColumnSetAsync(new CustomColumnSet
            {
                Name = setName,
                ColumnIds = columnIds,
                IsDefault = false,
                IsBuiltIn = false
            });
        }

        return imported;
    }

    private static List<TcCustomColumnSet> ParseCustomColumnSets(Dictionary<string, string> section)
    {
        var sets = new Dictionary<string, TcCustomColumnSet>(StringComparer.OrdinalIgnoreCase);
        var indexedFields = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var indexedNames = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var indexedWidths = new Dictionary<string, SortedDictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        var indexedAlignments = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var indexedColumns = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in section)
        {
            var key = kvp.Key.Trim();
            var value = kvp.Value;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var parts = key.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
            var prefix = parts[0];
            var suffix = parts.Length > 1 ? parts[1] : string.Empty;

            if (!sets.TryGetValue(prefix, out var set))
            {
                set = new TcCustomColumnSet(prefix);
                sets[prefix] = set;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                set.Name = value;
                continue;
            }

            if (TryParseIndexedSuffix(suffix, out var baseName, out var index))
            {
                switch (baseName.ToLowerInvariant())
                {
                    case "field":
                    case "fields":
                        AddIndexedValue(indexedFields, prefix, index, value);
                        break;
                    case "name":
                    case "title":
                    case "header":
                        AddIndexedValue(indexedNames, prefix, index, value);
                        break;
                    case "width":
                    case "widths":
                        if (int.TryParse(value.Trim(), out var width))
                            AddIndexedValue(indexedWidths, prefix, index, width);
                        break;
                    case "align":
                    case "alignment":
                        AddIndexedValue(indexedAlignments, prefix, index, value);
                        break;
                    case "col":
                    case "column":
                        AddIndexedValue(indexedColumns, prefix, index, value);
                        break;
                }
                continue;
            }

            switch (suffix.ToLowerInvariant())
            {
                case "name":
                case "title":
                case "caption":
                    set.Name = value;
                    break;
                case "fields":
                case "field":
                case "columns":
                case "column":
                    set.Fields = SplitList(value);
                    break;
                case "names":
                case "titles":
                case "headers":
                    set.Names = SplitList(value);
                    break;
                case "widths":
                case "width":
                    set.Widths = ParseIntList(value);
                    break;
                case "align":
                case "alignment":
                    set.Alignments = SplitList(value);
                    break;
            }
        }

        foreach (var set in sets.Values)
        {
            ApplyIndexedColumns(set, indexedColumns);
            ApplyIndexedValues(set.Fields, indexedFields, set.Id);
            ApplyIndexedValues(set.Names, indexedNames, set.Id);
            ApplyIndexedValues(set.Widths, indexedWidths, set.Id);
            ApplyIndexedValues(set.Alignments, indexedAlignments, set.Id);
        }

        return sets.Values.ToList();
    }

    private static void AddIndexedValue(
        Dictionary<string, SortedDictionary<int, string>> indexMap,
        string setId,
        int index,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value) || index <= 0)
            return;

        if (!indexMap.TryGetValue(setId, out var list))
        {
            list = new SortedDictionary<int, string>();
            indexMap[setId] = list;
        }

        list[index] = value.Trim();
    }

    private static void AddIndexedValue(
        Dictionary<string, SortedDictionary<int, int>> indexMap,
        string setId,
        int index,
        int value)
    {
        if (index <= 0)
            return;

        if (!indexMap.TryGetValue(setId, out var list))
        {
            list = new SortedDictionary<int, int>();
            indexMap[setId] = list;
        }

        list[index] = value;
    }

    private static void ApplyIndexedValues(
        List<string> target,
        Dictionary<string, SortedDictionary<int, string>> indexMap,
        string setId)
    {
        if (!indexMap.TryGetValue(setId, out var values))
            return;

        foreach (var entry in values)
        {
            var index = entry.Key;
            var value = entry.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            EnsureListSize(target, index);
            if (string.IsNullOrWhiteSpace(target[index - 1]))
                target[index - 1] = value.Trim();
        }
    }

    private static void ApplyIndexedValues(
        List<int> target,
        Dictionary<string, SortedDictionary<int, int>> indexMap,
        string setId)
    {
        if (!indexMap.TryGetValue(setId, out var values))
            return;

        foreach (var entry in values)
        {
            var index = entry.Key;
            if (index <= 0)
                continue;

            EnsureListSize(target, index);
            if (target[index - 1] == 0)
                target[index - 1] = entry.Value;
        }
    }

    private static void ApplyIndexedColumns(
        TcCustomColumnSet set,
        Dictionary<string, SortedDictionary<int, string>> indexMap)
    {
        if (!indexMap.TryGetValue(set.Id, out var columns))
            return;

        foreach (var entry in columns)
        {
            var index = entry.Key;
            var value = entry.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var parts = SplitColumnParts(value);
            if (parts.Count == 0)
                continue;

            string? field = null;
            string? name = null;
            string? alignment = null;
            int? width = null;

            foreach (var part in parts)
            {
                if (width == null && int.TryParse(part, out var parsedWidth))
                {
                    width = parsedWidth;
                    continue;
                }

                if (alignment == null && IsAlignmentToken(part))
                {
                    alignment = part;
                    continue;
                }

                if (field == null && LooksLikeFieldToken(part))
                {
                    field = part;
                    continue;
                }

                if (name == null)
                {
                    name = part;
                    continue;
                }

                if (field == null)
                {
                    field = part;
                }
            }

            field ??= parts[0];
            EnsureListSize(set.Fields, index);
            if (string.IsNullOrWhiteSpace(set.Fields[index - 1]))
                set.Fields[index - 1] = field;

            if (!string.IsNullOrWhiteSpace(name))
            {
                EnsureListSize(set.Names, index);
                if (string.IsNullOrWhiteSpace(set.Names[index - 1]))
                    set.Names[index - 1] = name;
            }

            if (width.HasValue)
            {
                EnsureListSize(set.Widths, index);
                if (set.Widths[index - 1] == 0)
                    set.Widths[index - 1] = width.Value;
            }

            if (!string.IsNullOrWhiteSpace(alignment))
            {
                EnsureListSize(set.Alignments, index);
                if (string.IsNullOrWhiteSpace(set.Alignments[index - 1]))
                    set.Alignments[index - 1] = alignment;
            }
        }
    }

    private static bool LooksLikeFieldToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("[=", StringComparison.OrdinalIgnoreCase))
            return true;
        if (TryMapFieldToBuiltInId(trimmed, out _))
            return true;
        if (trimmed.Contains('.', StringComparison.Ordinal) && !trimmed.Contains(' ', StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool IsAlignmentToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() is "l" or "left" or "0"
            or "c" or "center" or "1"
            or "r" or "right" or "2";
    }

    private static bool TryParseIndexedSuffix(string suffix, out string baseName, out int index)
    {
        baseName = string.Empty;
        index = 0;
        if (string.IsNullOrWhiteSpace(suffix))
            return false;

        var split = suffix.Trim();
        var pos = split.Length;
        while (pos > 0 && char.IsDigit(split[pos - 1]))
            pos--;

        if (pos == split.Length)
            return false;

        var digits = split.Substring(pos);
        if (!int.TryParse(digits, out index) || index <= 0)
            return false;

        baseName = split.Substring(0, pos);
        return !string.IsNullOrWhiteSpace(baseName);
    }

    private static List<string> SplitColumnParts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        var separator = value.Contains('|', StringComparison.Ordinal) ? '|' :
            value.Contains(';', StringComparison.Ordinal) ? ';' :
            value.Contains(',', StringComparison.Ordinal) ? ',' : '\0';

        var parts = separator == '\0'
            ? new[] { value }
            : value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        return parts.Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private static void EnsureListSize(List<string> list, int index)
    {
        while (list.Count < index)
            list.Add(string.Empty);
    }

    private static void EnsureListSize(List<int> list, int index)
    {
        while (list.Count < index)
            list.Add(0);
    }

    private static List<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        var separator = value.Contains(';', StringComparison.Ordinal) ? ';' :
            value.Contains(',', StringComparison.Ordinal) ? ',' :
            value.Contains('|', StringComparison.Ordinal) ? '|' : '\0';
        var parts = separator == '\0'
            ? new[] { value }
            : value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        return parts.Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private static List<int> ParseIntList(string? value)
    {
        var list = new List<int>();
        foreach (var part in SplitList(value))
        {
            if (int.TryParse(part, out var number))
                list.Add(number);
        }
        return list;
    }

    private static CustomColumnAlignment ParseAlignment(string? value, CustomColumnDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultAlignment(dataType);

        return value.Trim().ToLowerInvariant() switch
        {
            "r" or "right" or "2" => CustomColumnAlignment.Right,
            "c" or "center" or "1" => CustomColumnAlignment.Center,
            _ => CustomColumnAlignment.Left
        };
    }

    private static CustomColumnAlignment DefaultAlignment(CustomColumnDataType dataType)
    {
        return dataType switch
        {
            CustomColumnDataType.Number or CustomColumnDataType.Size => CustomColumnAlignment.Right,
            _ => CustomColumnAlignment.Left
        };
    }

    private static bool TryMapFieldToBuiltInId(string field, out string columnId)
    {
        var normalized = NormalizeFieldToken(field);
        switch (normalized)
        {
            case "name":
                columnId = BuiltInColumns.Name;
                return true;
            case "ext":
            case "extension":
                columnId = BuiltInColumns.Extension;
                return true;
            case "size":
                columnId = BuiltInColumns.Size;
                return true;
            case "date":
            case "modified":
            case "datemodified":
                columnId = BuiltInColumns.Date;
                return true;
            case "created":
                columnId = BuiltInColumns.Created;
                return true;
            case "accessed":
                columnId = BuiltInColumns.Accessed;
                return true;
            case "attr":
            case "attributes":
                columnId = BuiltInColumns.Attributes;
                return true;
            case "type":
                columnId = BuiltInColumns.Type;
                return true;
            case "path":
                columnId = BuiltInColumns.Path;
                return true;
            case "description":
            case "comment":
                columnId = BuiltInColumns.Description;
                return true;
            case "crc32":
                columnId = BuiltInColumns.CRC32;
                return true;
            case "md5":
                columnId = BuiltInColumns.MD5;
                return true;
            case "sha256":
                columnId = BuiltInColumns.SHA256;
                return true;
        }

        columnId = string.Empty;
        return false;
    }

    private static string NormalizeFieldToken(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return string.Empty;

        var trimmed = field.Trim();
        if (trimmed.StartsWith("[=", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(2, trimmed.Length - 3).Trim();
        }
        if (trimmed.StartsWith("tc.", StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(3).Trim().ToLowerInvariant();
        if (trimmed.StartsWith("shell.", StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(6).Trim().ToLowerInvariant();
        if (trimmed.StartsWith("content.", StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(8).Trim().ToLowerInvariant();

        return trimmed.ToLowerInvariant();
    }

    private static CustomColumnDataType InferDataType(string field)
    {
        var normalized = NormalizeFieldToken(field);
        return normalized switch
        {
            "size" or "length" or "compressedsize" => CustomColumnDataType.Size,
            "date" or "modified" or "datemodified" or "created" or "accessed" => CustomColumnDataType.DateTime,
            "crc32" or "md5" or "sha256" => CustomColumnDataType.String,
            _ => CustomColumnDataType.String
        };
    }

    private async Task<int> ImportButtonBarAsync(
        Dictionary<string, string> section,
        List<string> warnings,
        IReadOnlyDictionary<string, TcUserCommandDefinition> userCommands)
    {
        var items = ParseButtonBarItems(section, warnings);
        if (items.Count == 0)
            return 0;

        var toolbarButtons = new List<ToolbarButton>();
        var buttonBarItems = new List<ButtonBarItem>();
        var order = 0;

        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                toolbarButtons.Add(new ToolbarButton { IsSeparator = true, Order = order++ });
                buttonBarItems.Add(new ButtonBarItem
                {
                    Type = ButtonBarItemType.Separator,
                    Order = buttonBarItems.Count
                });
                continue;
            }

            if (TcCommandMapper.TryMapToToolbarCommand(item.Command, out var commandName))
            {
                toolbarButtons.Add(new ToolbarButton
                {
                    Label = item.Label,
                    CommandName = commandName,
                    Icon = ResolveToolbarIcon(commandName) ?? string.Empty,
                    Tooltip = item.Label,
                    Order = order++
                });
            }
            else if (!string.IsNullOrWhiteSpace(item.Command))
            {
                warnings.Add($"Button bar command '{item.Command}' not mapped; skipped for toolbar.");
            }

            if (_buttonBarService != null)
            {
                var buttonItem = MapToButtonBarItem(item, warnings, buttonBarItems.Count, userCommands);
                if (buttonItem != null)
                {
                    buttonBarItems.Add(buttonItem);
                }
            }
        }

        if (toolbarButtons.Count > 0)
        {
            var config = ToolbarConfiguration.Load();
            config.Buttons = toolbarButtons;
            config.Save();
        }

        if (_buttonBarService != null && buttonBarItems.Count > 0)
        {
            var bar = await _buttonBarService.GetDefaultButtonBarAsync() ??
                      await _buttonBarService.CreateButtonBarAsync("Imported Button Bar");
            bar.Buttons.Clear();
            foreach (var button in buttonBarItems)
            {
                bar.Buttons.Add(button);
            }
            await _buttonBarService.UpdateButtonBarAsync(bar);
        }

        return toolbarButtons.Count(b => !b.IsSeparator);
    }

    private static string? ResolveToolbarIcon(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return null;

        var defaultConfig = ToolbarConfiguration.CreateDefault();
        var match = defaultConfig.Buttons.FirstOrDefault(button =>
            string.Equals(button.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
        return match?.Icon;
    }

    private static ButtonBarItem? MapToButtonBarItem(
        TcButtonBarItem item,
        List<string> warnings,
        int order,
        IReadOnlyDictionary<string, TcUserCommandDefinition> userCommands)
    {
        if (item.IsSeparator)
        {
            return new ButtonBarItem { Type = ButtonBarItemType.Separator, Order = order };
        }

        var command = item.Command;
        var parameters = item.Parameters;
        var workingDirectory = item.WorkingDirectory;
        var icon = item.Icon;

        if (!string.IsNullOrWhiteSpace(command) &&
            command.Trim().StartsWith("em_", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveUserCommand(command.Trim(), userCommands, parameters, workingDirectory, out var resolved))
            {
                command = resolved.Command ?? command;
                parameters = resolved.Parameters;
                workingDirectory = resolved.WorkingDirectory;
                if (string.IsNullOrWhiteSpace(icon))
                    icon = resolved.IconPath;
            }
            else
            {
                warnings.Add($"Button bar user command '{command.Trim()}' imported without usercmd.ini data.");
                return null;
            }
        }

        if (TcCommandMapper.TryMapToButtonBarCommand(command, out var builtInId))
        {
            return new ButtonBarItem
            {
                Label = item.Label,
                Icon = icon,
                Order = order,
                Type = ButtonBarItemType.Command,
                Command = new ButtonCommand
                {
                    Type = ButtonCommandType.BuiltIn,
                    CommandId = builtInId
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            return new ButtonBarItem
            {
                Label = item.Label,
                Icon = icon,
                IconPath = GuessIconPath(icon),
                Order = order,
                Type = ButtonBarItemType.Command,
                Command = new ButtonCommand
                {
                    Type = ButtonCommandType.External,
                    ExecutablePath = command,
                    Arguments = parameters,
                    WorkingDirectory = workingDirectory
                }
            };
        }

        warnings.Add($"Button bar item '{item.Label}' has no command; skipped.");
        return null;
    }

    private static string? GuessIconPath(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        var trimmed = icon.Trim();
        if (trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar))
            return trimmed;
        if (trimmed.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return null;
    }

    private static List<TcButtonBarItem> ParseButtonBarItems(Dictionary<string, string> section, List<string> warnings)
    {
        var items = new List<TcButtonBarItem>();
        var maxIndex = GetMaxIndex(section, "button", "cmd", "param", "path", "icon");

        for (var index = 1; index <= maxIndex; index++)
        {
            section.TryGetValue($"button{index}", out var label);
            section.TryGetValue($"cmd{index}", out var command);
            section.TryGetValue($"param{index}", out var parameters);
            section.TryGetValue($"path{index}", out var workingDirectory);
            section.TryGetValue($"icon{index}", out var icon);

            if (string.IsNullOrWhiteSpace(label) &&
                string.IsNullOrWhiteSpace(command) &&
                string.IsNullOrWhiteSpace(parameters) &&
                string.IsNullOrWhiteSpace(workingDirectory))
            {
                continue;
            }

            if (IsSubmenuStart(command))
            {
                warnings.Add($"Button bar submenu '{label}' not supported; skipped.");
                continue;
            }

            var isSeparator = IsSeparator(label, command);
            var itemLabel = string.IsNullOrWhiteSpace(label) ? command ?? $"Button {index}" : label.Trim();
            items.Add(new TcButtonBarItem
            {
                Label = itemLabel,
                Command = command ?? string.Empty,
                Parameters = parameters,
                WorkingDirectory = workingDirectory,
                Icon = icon,
                IsSeparator = isSeparator
            });
        }

        return items;
    }

    private static List<UserMenuItem> ParseUserMenuItems(
        Dictionary<string, string> section,
        List<string> warnings,
        IReadOnlyDictionary<string, TcUserCommandDefinition> userCommands)
    {
        var root = new List<UserMenuItem>();
        var stack = new Stack<List<UserMenuItem>>();
        stack.Push(root);

        var maxIndex = GetMaxIndex(section, "menu", "cmd", "param", "path");
        for (var index = 1; index <= maxIndex; index++)
        {
            section.TryGetValue($"menu{index}", out var label);
            section.TryGetValue($"cmd{index}", out var command);
            section.TryGetValue($"param{index}", out var parameters);
            section.TryGetValue($"path{index}", out var workingDirectory);

            if (string.IsNullOrWhiteSpace(label) &&
                string.IsNullOrWhiteSpace(command) &&
                string.IsNullOrWhiteSpace(parameters) &&
                string.IsNullOrWhiteSpace(workingDirectory))
            {
                continue;
            }

            if (IsSubmenuEnd(command))
            {
                if (stack.Count > 1)
                    stack.Pop();
                continue;
            }

            if (IsSubmenuStart(command))
            {
                var submenu = new UserMenuItem
                {
                    Label = string.IsNullOrWhiteSpace(label) ? $"Menu {index}" : label.Trim(),
                    Type = MenuItemType.SubMenu,
                    Order = stack.Peek().Count
                };
                submenu.SubItems = new List<UserMenuItem>();
                stack.Peek().Add(submenu);
                stack.Push(submenu.SubItems);
                continue;
            }

            if (IsSeparator(label, command))
            {
                stack.Peek().Add(new UserMenuItem
                {
                    Type = MenuItemType.Separator,
                    Order = stack.Peek().Count
                });
                continue;
            }

            var itemLabel = string.IsNullOrWhiteSpace(label) ? command ?? $"Item {index}" : label.Trim();
            var userCommand = BuildUserMenuCommand(command, parameters, workingDirectory, warnings, userCommands);
            if (userCommand == null)
            {
                warnings.Add($"User menu item '{itemLabel}' has no command; skipped.");
                continue;
            }

            stack.Peek().Add(new UserMenuItem
            {
                Label = itemLabel,
                Type = MenuItemType.Command,
                Command = userCommand,
                Order = stack.Peek().Count
            });
        }

        return root;
    }

    private static UserMenuCommand? BuildUserMenuCommand(
        string? command,
        string? parameters,
        string? workingDirectory,
        List<string> warnings,
        IReadOnlyDictionary<string, TcUserCommandDefinition> userCommands)
    {
        if (string.IsNullOrWhiteSpace(command) && string.IsNullOrWhiteSpace(workingDirectory))
            return null;

        if (!string.IsNullOrWhiteSpace(command))
        {
            var trimmed = command.Trim();
            if (trimmed.StartsWith("em_", StringComparison.OrdinalIgnoreCase))
            {
                if (TryResolveUserCommand(trimmed, userCommands, parameters, workingDirectory, out var resolved))
                    return BuildResolvedMenuCommand(resolved, warnings);

                warnings.Add($"User command '{trimmed}' imported without usercmd.ini data.");
                return new UserMenuCommand
                {
                    Type = MenuCommandType.InternalCommand,
                    InternalAction = trimmed
                };
            }

            if (trimmed.StartsWith("cm_", StringComparison.OrdinalIgnoreCase))
            {
                return new UserMenuCommand
                {
                    Type = MenuCommandType.InternalCommand,
                    InternalAction = trimmed
                };
            }

            if (trimmed.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
            {
                var path = ResolvePathFromCommand(trimmed, workingDirectory);
                return new UserMenuCommand
                {
                    Type = MenuCommandType.ChangePath,
                    CommandLine = path
                };
            }
        }

        return new UserMenuCommand
        {
            Type = MenuCommandType.External,
            CommandLine = command?.Trim(),
            Parameters = parameters,
            WorkingDirectory = workingDirectory
        };
    }

    private static UserMenuCommand? BuildResolvedMenuCommand(
        ResolvedUserCommand resolved,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(resolved.Command))
            return null;

        var trimmed = resolved.Command.Trim();
        if (trimmed.StartsWith("cm_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("em_", StringComparison.OrdinalIgnoreCase))
        {
            return new UserMenuCommand
            {
                Type = MenuCommandType.InternalCommand,
                InternalAction = trimmed
            };
        }

        if (trimmed.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
        {
            var path = ResolvePathFromCommand(trimmed, resolved.WorkingDirectory);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return new UserMenuCommand
                {
                    Type = MenuCommandType.ChangePath,
                    CommandLine = path
                };
            }

            warnings.Add($"Failed to resolve change-path command '{resolved.Command}'.");
            return null;
        }

        return new UserMenuCommand
        {
            Type = MenuCommandType.External,
            CommandLine = trimmed,
            Parameters = resolved.Parameters,
            WorkingDirectory = resolved.WorkingDirectory
        };
    }

    private static Dictionary<string, TcUserCommandDefinition> LoadUserCommands(
        string wincmdIniPath,
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        List<string> warnings)
    {
        var commands = new Dictionary<string, TcUserCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        var baseDirectory = Path.GetDirectoryName(wincmdIniPath) ?? string.Empty;
        var userCmdPath = ResolveUserCmdIniPath(baseDirectory, sections);

        if (string.IsNullOrWhiteSpace(userCmdPath) || !File.Exists(userCmdPath))
            return commands;

        try
        {
            var ini = IniFile.Load(userCmdPath, Encoding.Default);
            foreach (var section in ini.Sections)
            {
                var sectionName = section.Key.Trim();
                if (!sectionName.StartsWith("em_", StringComparison.OrdinalIgnoreCase))
                    continue;

                section.Value.TryGetValue("cmd", out var cmd);
                section.Value.TryGetValue("param", out var param);
                section.Value.TryGetValue("path", out var path);
                section.Value.TryGetValue("icon", out var icon);
                section.Value.TryGetValue("button", out var button);

                var definition = new TcUserCommandDefinition
                {
                    Name = sectionName,
                    CommandLine = NormalizeUserCommand(cmd, baseDirectory),
                    Parameters = string.IsNullOrWhiteSpace(param) ? null : param.Trim(),
                    WorkingDirectory = NormalizeUserPath(path, baseDirectory),
                    IconPath = NormalizeUserPath(string.IsNullOrWhiteSpace(icon) ? button : icon, baseDirectory)
                };

                commands[sectionName] = definition;
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to read usercmd.ini: {ex.Message}");
        }

        return commands;
    }

    private static string? ResolveUserCmdIniPath(
        string baseDirectory,
        IReadOnlyDictionary<string, Dictionary<string, string>> sections)
    {
        if (sections.TryGetValue("Configuration", out var config))
        {
            if (TryResolveUserCmdCandidate(config, "UserCmdIni", baseDirectory, out var candidate))
                return candidate;
            if (TryResolveUserCmdCandidate(config, "UserIni", baseDirectory, out candidate))
                return candidate;
            if (TryResolveUserCmdCandidate(config, "UserIni2", baseDirectory, out candidate))
                return candidate;
        }

        return Path.Combine(baseDirectory, "usercmd.ini");
    }

    private static bool TryResolveUserCmdCandidate(
        Dictionary<string, string> section,
        string key,
        string baseDirectory,
        out string path)
    {
        path = string.Empty;
        if (!section.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;

        var candidate = NormalizeUserPath(raw, baseDirectory);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.Equals(Path.GetFileName(candidate), "usercmd.ini", StringComparison.OrdinalIgnoreCase))
        {
            path = candidate;
            return true;
        }

        return false;
    }

    private static bool TryResolveUserCommand(
        string command,
        IReadOnlyDictionary<string, TcUserCommandDefinition> userCommands,
        string? parameters,
        string? workingDirectory,
        out ResolvedUserCommand resolved)
    {
        resolved = default;
        if (!userCommands.TryGetValue(command, out var definition))
            return false;

        if (string.IsNullOrWhiteSpace(definition.CommandLine))
            return false;

        resolved = new ResolvedUserCommand(
            definition.CommandLine,
            CombineParameters(definition.Parameters, parameters),
            string.IsNullOrWhiteSpace(workingDirectory) ? definition.WorkingDirectory : workingDirectory,
            definition.IconPath);
        return true;
    }

    private static string? CombineParameters(string? baseParameters, string? additionalParameters)
    {
        if (string.IsNullOrWhiteSpace(baseParameters))
            return string.IsNullOrWhiteSpace(additionalParameters) ? null : additionalParameters.Trim();
        if (string.IsNullOrWhiteSpace(additionalParameters))
            return baseParameters.Trim();

        return $"{baseParameters.Trim()} {additionalParameters.Trim()}";
    }

    private static string? NormalizeUserCommand(string? value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().Trim('"');
        if (trimmed.StartsWith("cm_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("em_", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var expanded = ExpandCommanderPath(trimmed, baseDirectory);
        if (!Path.IsPathRooted(expanded))
            expanded = Path.Combine(baseDirectory, expanded);

        return ConvertPath(expanded);
    }

    private static string? NormalizeUserPath(string? value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().Trim('"');
        var expanded = ExpandCommanderPath(trimmed, baseDirectory);
        if (!Path.IsPathRooted(expanded))
            expanded = Path.Combine(baseDirectory, expanded);

        return ConvertPath(expanded);
    }

    private static string ExpandCommanderPath(string value, string baseDirectory)
    {
        var expanded = value.Replace("%COMMANDER_PATH%", baseDirectory, StringComparison.OrdinalIgnoreCase);
        return Environment.ExpandEnvironmentVariables(expanded);
    }

    private async Task PersistUserMenuAsync(List<UserMenuItem> items, List<string> warnings)
    {
        if (_userMenuService == null)
            return;

        var menu = await _userMenuService.GetMainMenuAsync();
        if (menu == null)
        {
            menu = await _userMenuService.CreateMenuAsync("Imported User Menu");
        }

        menu.IsMain = true;
        menu.Items.Clear();
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }

        await _userMenuService.UpdateMenuAsync(menu);
    }

    private static int CountMenuItems(IEnumerable<UserMenuItem> items)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Type != MenuItemType.Separator)
                count++;
            if (item.SubItems != null)
                count += CountMenuItems(item.SubItems);
        }
        return count;
    }

    private sealed class TcCustomColumnSet
    {
        public TcCustomColumnSet(string id)
        {
            Id = id;
            Name = id;
        }

        public string Id { get; }
        public string Name { get; set; }
        public List<string> Fields { get; set; } = new();
        public List<string> Names { get; set; } = new();
        public List<int> Widths { get; set; } = new();
        public List<string> Alignments { get; set; } = new();
    }

    private sealed class TcUserCommandDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string? CommandLine { get; init; }
        public string? Parameters { get; init; }
        public string? WorkingDirectory { get; init; }
        public string? IconPath { get; init; }
    }

    private readonly struct ResolvedUserCommand
    {
        public ResolvedUserCommand(
            string? command,
            string? parameters,
            string? workingDirectory,
            string? iconPath)
        {
            Command = command;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            IconPath = iconPath;
        }

        public string? Command { get; }
        public string? Parameters { get; }
        public string? WorkingDirectory { get; }
        public string? IconPath { get; }
    }

    private sealed class TcButtonBarItem
    {
        public string Label { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string? Parameters { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? Icon { get; set; }
        public bool IsSeparator { get; set; }
    }
    
    private static string ConvertPath(string tcPath)
    {
        // Convert Windows paths to current platform
        if (!OperatingSystem.IsWindows())
        {
            // Basic conversion: replace backslashes
            return tcPath.Replace('\\', '/');
        }
        
        return tcPath;
    }
}
