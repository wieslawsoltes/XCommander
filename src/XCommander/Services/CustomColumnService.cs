// CustomColumnService.cs - Implementation of TC-style custom columns
// Manages user-defined columns with expression support and plugin integration

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public class CustomColumnService : ICustomColumnService
{
    private static readonly Regex ExpressionVariableRegex = new(@"\b(?<prefix>tc|shell|content)\.(?<name>[A-Za-z0-9_]+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> KnownTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "Text Document",
        [".log"] = "Log File",
        [".md"] = "Markdown Document",
        [".json"] = "JSON Document",
        [".xml"] = "XML Document",
        [".cs"] = "C# Source File",
        [".js"] = "JavaScript File",
        [".ts"] = "TypeScript File",
        [".html"] = "HTML Document",
        [".css"] = "Style Sheet",
        [".png"] = "PNG Image",
        [".jpg"] = "JPEG Image",
        [".jpeg"] = "JPEG Image",
        [".gif"] = "GIF Image",
        [".bmp"] = "Bitmap Image",
        [".zip"] = "ZIP Archive",
        [".7z"] = "7-Zip Archive",
        [".rar"] = "RAR Archive",
        [".gz"] = "GZip Archive",
        [".tar"] = "TAR Archive",
        [".exe"] = "Application",
        [".dll"] = "Application Extension",
        [".pdf"] = "PDF Document"
    };

    private readonly ConcurrentDictionary<string, CustomColumnDefinition> _builtInColumns = new();
    private readonly ConcurrentDictionary<string, CustomColumnDefinition> _userColumns = new();
    private readonly ConcurrentDictionary<string, CustomColumnSet> _columnSets = new();
    private readonly ConcurrentDictionary<string, IColumnValueProvider> _providers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CustomColumnValue>> _valueCache = new();
    private readonly string _storagePath;
    private readonly SemaphoreSlim _storageLock = new(1, 1);
    private readonly IContentPluginService? _contentPluginService;
    private readonly IPluginService? _pluginService;
    private readonly IDescriptionFileService? _descriptionFileService;
    private readonly IFileChecksumService? _fileChecksumService;
    
    public event EventHandler<EventArgs>? ColumnsChanged;
    public event EventHandler<EventArgs>? ColumnSetsChanged;

    private static readonly JsonSerializerOptions StorageOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public CustomColumnService(
        IContentPluginService? contentPluginService = null,
        IPluginService? pluginService = null,
        IDescriptionFileService? descriptionFileService = null,
        IFileChecksumService? fileChecksumService = null)
    {
        _contentPluginService = contentPluginService;
        _pluginService = pluginService;
        _descriptionFileService = descriptionFileService;
        _fileChecksumService = fileChecksumService;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storagePath = Path.Combine(appData, "XCommander", "custom-columns.json");
        InitializeBuiltInColumns();
        InitializeDefaultColumnSets();
        LoadUserData();
    }
    
    private void InitializeBuiltInColumns()
    {
        // Name column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Name,
            Name = "Name",
            DisplayName = "Name",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.FileProperty,
            SourceField = "Name",
            Width = 200,
            Sortable = true,
            Searchable = true,
            IsBuiltIn = true,
            DisplayOrder = 0
        });
        
        // Extension column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Extension,
            Name = "Ext",
            DisplayName = "Extension",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.FileProperty,
            SourceField = "Extension",
            Width = 60,
            Sortable = true,
            Searchable = true,
            IsBuiltIn = true,
            DisplayOrder = 1
        });
        
        // Size column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Size,
            Name = "Size",
            DisplayName = "Size",
            DataType = CustomColumnDataType.Size,
            Source = CustomColumnSource.FileProperty,
            SourceField = "Length",
            Alignment = CustomColumnAlignment.Right,
            Width = 80,
            Sortable = true,
            IsBuiltIn = true,
            DisplayOrder = 2
        });
        
        // Modified date column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Date,
            Name = "Date",
            DisplayName = "Modified",
            DataType = CustomColumnDataType.DateTime,
            Source = CustomColumnSource.FileProperty,
            SourceField = "LastWriteTime",
            Width = 130,
            FormatString = "yyyy-MM-dd HH:mm",
            Sortable = true,
            IsBuiltIn = true,
            DisplayOrder = 3
        });
        
        // Created date column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Created,
            Name = "Created",
            DisplayName = "Created",
            DataType = CustomColumnDataType.DateTime,
            Source = CustomColumnSource.FileProperty,
            SourceField = "CreationTime",
            Width = 130,
            FormatString = "yyyy-MM-dd HH:mm",
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 4
        });
        
        // Accessed date column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Accessed,
            Name = "Accessed",
            DisplayName = "Accessed",
            DataType = CustomColumnDataType.DateTime,
            Source = CustomColumnSource.FileProperty,
            SourceField = "LastAccessTime",
            Width = 130,
            FormatString = "yyyy-MM-dd HH:mm",
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 5
        });
        
        // Attributes column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Attributes,
            Name = "Attr",
            DisplayName = "Attributes",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.FileProperty,
            SourceField = "Attributes",
            Width = 60,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 6
        });
        
        // Type column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Type,
            Name = "Type",
            DisplayName = "Type",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "TypeName",
            Width = 150,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 7
        });
        
        // Path column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Path,
            Name = "Path",
            DisplayName = "Path",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.FileProperty,
            SourceField = "DirectoryName",
            Width = 300,
            Sortable = true,
            Searchable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 8
        });

        // Description column (descript.ion or shell comments)
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Description,
            Name = "Desc",
            DisplayName = "Description",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "Description",
            Width = 200,
            Sortable = true,
            Searchable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 9
        });

        // Version column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Version,
            Name = "Ver",
            DisplayName = "Version",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "Version",
            Width = 140,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 10
        });

        // Company column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Company,
            Name = "Company",
            DisplayName = "Company",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "Company",
            Width = 160,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 11
        });

        // Comments column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Comments,
            Name = "Comments",
            DisplayName = "Comments",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "Comments",
            Width = 200,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 12
        });

        // Owner column
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Owner,
            Name = "Owner",
            DisplayName = "Owner",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "Owner",
            Width = 120,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 13
        });

        // Checksums
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.MD5,
            Name = "MD5",
            DisplayName = "MD5",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "MD5",
            Width = 200,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 14
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.SHA256,
            Name = "SHA256",
            DisplayName = "SHA-256",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "SHA256",
            Width = 220,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 15
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.CRC32,
            Name = "CRC32",
            DisplayName = "CRC32",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Shell,
            SourceField = "CRC32",
            Width = 120,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 16
        });

        // Media metadata (content plugins)
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Dimensions,
            Name = "Dim",
            DisplayName = "Dimensions",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Dimensions",
            Width = 120,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 17
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Duration,
            Name = "Duration",
            DisplayName = "Duration",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Duration",
            Width = 90,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 18
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.BitRate,
            Name = "Bitrate",
            DisplayName = "Bit Rate",
            DataType = CustomColumnDataType.Number,
            Source = CustomColumnSource.Content,
            SourceField = "BitRate",
            Width = 90,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 19
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Artist,
            Name = "Artist",
            DisplayName = "Artist",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Artist",
            Width = 160,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 20
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Album,
            Name = "Album",
            DisplayName = "Album",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Album",
            Width = 160,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 21
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Title,
            Name = "Title",
            DisplayName = "Title",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Title",
            Width = 200,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 22
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Genre,
            Name = "Genre",
            DisplayName = "Genre",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Genre",
            Width = 120,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 23
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Year,
            Name = "Year",
            DisplayName = "Year",
            DataType = CustomColumnDataType.Number,
            Source = CustomColumnSource.Content,
            SourceField = "Year",
            Width = 80,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 24
        });

        // Document metadata (content plugins)
        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.PageCount,
            Name = "Pages",
            DisplayName = "Pages",
            DataType = CustomColumnDataType.Number,
            Source = CustomColumnSource.Content,
            SourceField = "PageCount",
            Width = 80,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 25
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Author,
            Name = "Author",
            DisplayName = "Author",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Author",
            Width = 160,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 26
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Subject,
            Name = "Subject",
            DisplayName = "Subject",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Subject",
            Width = 180,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 27
        });

        AddBuiltInColumn(new CustomColumnDefinition
        {
            Id = BuiltInColumns.Keywords,
            Name = "Keywords",
            DisplayName = "Keywords",
            DataType = CustomColumnDataType.String,
            Source = CustomColumnSource.Content,
            SourceField = "Keywords",
            Width = 200,
            Sortable = true,
            Visible = false,
            IsBuiltIn = true,
            DisplayOrder = 28
        });
    }
    
    private void AddBuiltInColumn(CustomColumnDefinition column)
    {
        _builtInColumns[column.Id] = column;
    }
    
    private void InitializeDefaultColumnSets()
    {
        // Default column set
        _columnSets["default"] = new CustomColumnSet
        {
            Id = "default",
            Name = "Default",
            Description = "Default column set",
            ColumnIds = new[] { BuiltInColumns.Name, BuiltInColumns.Extension, BuiltInColumns.Size, BuiltInColumns.Date },
            IsDefault = true,
            IsBuiltIn = true
        };
        
        // Brief column set
        _columnSets["brief"] = new CustomColumnSet
        {
            Id = "brief",
            Name = "Brief",
            Description = "Compact view with name only",
            ColumnIds = new[] { BuiltInColumns.Name },
            IsBuiltIn = true
        };
        
        // Full column set
        _columnSets["full"] = new CustomColumnSet
        {
            Id = "full",
            Name = "Full",
            Description = "All available columns",
            ColumnIds = new[] 
            { 
                BuiltInColumns.Name, 
                BuiltInColumns.Extension, 
                BuiltInColumns.Size, 
                BuiltInColumns.Date,
                BuiltInColumns.Created,
                BuiltInColumns.Attributes,
                BuiltInColumns.Type
            },
            IsBuiltIn = true
        };
        
        // Comments column set (with description for plugins)
        _columnSets["comments"] = new CustomColumnSet
        {
            Id = "comments",
            Name = "Comments",
            Description = "Columns with file comments/descriptions",
            ColumnIds = new[] { BuiltInColumns.Name, BuiltInColumns.Size, BuiltInColumns.Date, BuiltInColumns.Description },
            IsBuiltIn = true
        };
    }

    private void LoadUserData()
    {
        try
        {
            if (!File.Exists(_storagePath))
                return;

            var json = File.ReadAllText(_storagePath);
            var data = JsonSerializer.Deserialize<CustomColumnStore>(json, StorageOptions);
            if (data == null)
                return;

            foreach (var column in data.Columns ?? new List<CustomColumnDefinition>())
            {
                if (string.IsNullOrWhiteSpace(column.Id) || _builtInColumns.ContainsKey(column.Id))
                    continue;
                _userColumns[column.Id] = column with { IsBuiltIn = false };
            }

            foreach (var set in data.ColumnSets ?? new List<CustomColumnSet>())
            {
                if (string.IsNullOrWhiteSpace(set.Id) || _columnSets.TryGetValue(set.Id, out var existing) && existing.IsBuiltIn)
                    continue;
                _columnSets[set.Id] = set with { IsBuiltIn = false };
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private async Task SaveUserDataAsync(CancellationToken cancellationToken = default)
    {
        await _storageLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var data = new CustomColumnStore
            {
                Columns = _userColumns.Values.OrderBy(c => c.DisplayOrder).ToList(),
                ColumnSets = _columnSets.Values.Where(s => !s.IsBuiltIn).OrderBy(s => s.Name).ToList()
            };

            var json = JsonSerializer.Serialize(data, StorageOptions);
            await File.WriteAllTextAsync(_storagePath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
        finally
        {
            _storageLock.Release();
        }
    }
    
    public IReadOnlyList<CustomColumnDefinition> GetBuiltInColumns()
    {
        return _builtInColumns.Values.OrderBy(c => c.DisplayOrder).ToList();
    }
    
    public IReadOnlyList<CustomColumnDefinition> GetUserColumns()
    {
        return _userColumns.Values.OrderBy(c => c.DisplayOrder).ToList();
    }
    
    public IReadOnlyList<CustomColumnDefinition> GetAllColumns()
    {
        return _builtInColumns.Values
            .Concat(_userColumns.Values)
            .OrderBy(c => c.IsBuiltIn ? 0 : 1)
            .ThenBy(c => c.DisplayOrder)
            .ToList();
    }
    
    public CustomColumnDefinition? GetColumn(string columnId)
    {
        if (_builtInColumns.TryGetValue(columnId, out var builtIn))
            return builtIn;
        if (_userColumns.TryGetValue(columnId, out var user))
            return user;
        return null;
    }
    
    public async Task<CustomColumnDefinition> CreateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(definition.Id))
        {
            definition = definition with { Id = Guid.NewGuid().ToString() };
        }
        
        if (_builtInColumns.ContainsKey(definition.Id))
        {
            throw new InvalidOperationException("Cannot create column with built-in ID");
        }
        
        _userColumns[definition.Id] = definition with { IsBuiltIn = false };
        OnColumnsChanged();

        await SaveUserDataAsync(cancellationToken);
        return definition;
    }
    
    public async Task<bool> UpdateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default)
    {
        if (_builtInColumns.ContainsKey(definition.Id))
        {
            return false; // Cannot modify built-in columns
        }
        
        if (!_userColumns.ContainsKey(definition.Id))
        {
            return false;
        }
        
        _userColumns[definition.Id] = definition with { IsBuiltIn = false };
        InvalidateCacheForColumn(definition.Id);
        OnColumnsChanged();

        await SaveUserDataAsync(cancellationToken);
        return true;
    }
    
    public async Task<bool> DeleteColumnAsync(string columnId, CancellationToken cancellationToken = default)
    {
        if (_builtInColumns.ContainsKey(columnId))
        {
            return false; // Cannot delete built-in columns
        }
        
        if (_userColumns.TryRemove(columnId, out _))
        {
            InvalidateCacheForColumn(columnId);
            OnColumnsChanged();
            await SaveUserDataAsync(cancellationToken);
            return true;
        }
        
        return false;
    }
    
    public IReadOnlyList<CustomColumnSet> GetColumnSets()
    {
        return _columnSets.Values.OrderBy(s => s.IsBuiltIn ? 0 : 1).ThenBy(s => s.Name).ToList();
    }
    
    public CustomColumnSet? GetColumnSet(string setId)
    {
        return _columnSets.TryGetValue(setId, out var set) ? set : null;
    }
    
    public async Task<CustomColumnSet> CreateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(set.Id))
        {
            set = set with { Id = Guid.NewGuid().ToString() };
        }
        
        _columnSets[set.Id] = set with { IsBuiltIn = false };
        OnColumnSetsChanged();

        await SaveUserDataAsync(cancellationToken);
        return set;
    }
    
    public async Task<bool> UpdateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default)
    {
        if (!_columnSets.TryGetValue(set.Id, out var existing))
        {
            return false;
        }
        
        if (existing.IsBuiltIn)
        {
            return false;
        }
        
        _columnSets[set.Id] = set with { IsBuiltIn = false };
        OnColumnSetsChanged();

        await SaveUserDataAsync(cancellationToken);
        return true;
    }
    
    public async Task<bool> DeleteColumnSetAsync(string setId, CancellationToken cancellationToken = default)
    {
        if (!_columnSets.TryGetValue(setId, out var existing))
        {
            return false;
        }
        
        if (existing.IsBuiltIn)
        {
            return false;
        }
        
        if (_columnSets.TryRemove(setId, out _))
        {
            OnColumnSetsChanged();
            await SaveUserDataAsync(cancellationToken);
            return true;
        }
        
        return false;
    }
    
    public CustomColumnSet GetColumnSetForFolder(string folderPath)
    {
        // Check for folder-specific column set
        foreach (var set in _columnSets.Values.Where(s => !string.IsNullOrEmpty(s.FolderFilter)))
        {
            if (MatchesFilter(folderPath, set.FolderFilter!))
            {
                return set;
            }
        }
        
        // Return default set
        return _columnSets.Values.FirstOrDefault(s => s.IsDefault) ?? _columnSets["default"];
    }
    
    private bool MatchesFilter(string path, string filter)
    {
        // Simple glob matching
        if (filter.Contains('*'))
        {
            var pattern = filter.Replace("*", ".*");
            return System.Text.RegularExpressions.Regex.IsMatch(path, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return path.StartsWith(filter, StringComparison.OrdinalIgnoreCase);
    }
    
    public async Task<CustomColumnValue> GetColumnValueAsync(string columnId, string filePath, CancellationToken cancellationToken = default)
    {
        // Check cache
        var cacheKey = filePath;
        if (_valueCache.TryGetValue(cacheKey, out var fileCache))
        {
            if (fileCache.TryGetValue(columnId, out var cachedValue))
            {
                return cachedValue;
            }
        }
        
        var column = GetColumn(columnId);
        if (column == null)
        {
            return new CustomColumnValue
            {
                ColumnId = columnId,
                ErrorMessage = "Column not found"
            };
        }
        
        try
        {
            var value = await GetColumnValueInternalAsync(column, filePath, cancellationToken);
            
            // Cache the result
            var cache = _valueCache.GetOrAdd(cacheKey, _ => new ConcurrentDictionary<string, CustomColumnValue>());
            cache[columnId] = value;
            
            return value;
        }
        catch (Exception ex)
        {
            return new CustomColumnValue
            {
                ColumnId = columnId,
                ErrorMessage = ex.Message
            };
        }
    }
    
    private async Task<CustomColumnValue> GetColumnValueInternalAsync(CustomColumnDefinition column, string filePath, CancellationToken cancellationToken)
    {
        object? rawValue = null;
        
        switch (column.Source)
        {
            case CustomColumnSource.FileProperty:
                rawValue = GetFilePropertyValue(column.SourceField, filePath);
                break;
                
            case CustomColumnSource.Shell:
                rawValue = await GetShellPropertyValueAsync(column.SourceField, filePath, cancellationToken);
                break;
                
            case CustomColumnSource.Plugin:
                if (!string.IsNullOrEmpty(column.PluginId) && _providers.TryGetValue(column.PluginId, out var provider))
                {
                    rawValue = await provider.GetFieldValueAsync(column.SourceField, filePath, cancellationToken);
                }
                break;
                
            case CustomColumnSource.Expression:
                rawValue = await EvaluateExpressionAsync(column.SourceField, filePath, cancellationToken);
                break;
                
            case CustomColumnSource.Content:
                rawValue = await GetContentFieldValueAsync(column.SourceField, filePath, cancellationToken);
                break;
        }
        
        var displayValue = FormatValue(rawValue, column);
        var sortValue = GetSortValue(rawValue, column);
        
        return new CustomColumnValue
        {
            ColumnId = column.Id,
            Value = rawValue,
            DisplayValue = displayValue,
            SortValue = sortValue
        };
    }
    
    private object? GetFilePropertyValue(string propertyName, string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                var dirInfo = new DirectoryInfo(filePath);
                if (dirInfo.Exists)
                {
                    return GetDirectoryPropertyValue(propertyName, dirInfo);
                }
                return null;
            }
            
            return propertyName switch
            {
                "Name" => fileInfo.Name,
                "Extension" => fileInfo.Extension.TrimStart('.'),
                "Length" => fileInfo.Length,
                "LastWriteTime" => fileInfo.LastWriteTime,
                "CreationTime" => fileInfo.CreationTime,
                "LastAccessTime" => fileInfo.LastAccessTime,
                "Attributes" => FormatAttributes(fileInfo.Attributes),
                "DirectoryName" => fileInfo.DirectoryName,
                "FullName" => fileInfo.FullName,
                "IsReadOnly" => fileInfo.IsReadOnly,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
    
    private object? GetDirectoryPropertyValue(string propertyName, DirectoryInfo dirInfo)
    {
        return propertyName switch
        {
            "Name" => dirInfo.Name,
            "Extension" => string.Empty,
            "Length" => null, // Directories don't have size directly
            "LastWriteTime" => dirInfo.LastWriteTime,
            "CreationTime" => dirInfo.CreationTime,
            "LastAccessTime" => dirInfo.LastAccessTime,
            "Attributes" => FormatAttributes(dirInfo.Attributes),
            "DirectoryName" => dirInfo.Parent?.FullName,
            "FullName" => dirInfo.FullName,
            _ => null
        };
    }
    
    private string FormatAttributes(FileAttributes attributes)
    {
        var result = new System.Text.StringBuilder();
        if (attributes.HasFlag(FileAttributes.ReadOnly)) result.Append('r');
        if (attributes.HasFlag(FileAttributes.Hidden)) result.Append('h');
        if (attributes.HasFlag(FileAttributes.System)) result.Append('s');
        if (attributes.HasFlag(FileAttributes.Archive)) result.Append('a');
        if (attributes.HasFlag(FileAttributes.Compressed)) result.Append('c');
        if (attributes.HasFlag(FileAttributes.Encrypted)) result.Append('e');
        return result.ToString();
    }
    
    private Task<object?> GetShellPropertyValueAsync(string propertyName, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Task.FromResult<object?>(null);
        }

        var normalized = propertyName.Trim();
        return GetShellPropertyValueCoreAsync(normalized, filePath, cancellationToken);
    }
    
    private Task<object?> GetContentFieldValueAsync(string fieldName, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return Task.FromResult<object?>(null);
        }

        return GetContentFieldValueCoreAsync(fieldName.Trim(), filePath, cancellationToken);
    }
    
    private Task<object?> EvaluateExpressionAsync(string expression, string filePath, CancellationToken cancellationToken)
    {
        return EvaluateExpressionCoreAsync(expression, filePath, cancellationToken);
    }

    private async Task<object?> GetShellPropertyValueCoreAsync(string propertyName, string filePath, CancellationToken cancellationToken)
    {
        var normalized = propertyName.Trim();
        var lower = normalized.ToLowerInvariant();

        switch (lower)
        {
            case "typename":
            case "type":
                return GetTypeName(filePath);

            case "description":
                return await GetDescriptionAsync(filePath, cancellationToken);

            case "version":
                return GetFileVersionInfo(filePath)?.FileVersion;

            case "productversion":
                return GetFileVersionInfo(filePath)?.ProductVersion;

            case "company":
            case "companyname":
                return GetFileVersionInfo(filePath)?.CompanyName;

            case "comments":
                return GetFileVersionInfo(filePath)?.Comments;

            case "owner":
                return GetOwnerName(filePath);

            case "compressedsize":
                return GetCompressedSize(filePath);

            case "crc32":
                return await GetChecksumAsync(filePath, ChecksumAlgorithm.CRC32, cancellationToken);

            case "md5":
                return await GetChecksumAsync(filePath, ChecksumAlgorithm.MD5, cancellationToken);

            case "sha256":
                return await GetChecksumAsync(filePath, ChecksumAlgorithm.SHA256, cancellationToken);

            case "dimensions":
            case "duration":
            case "bitrate":
            case "artist":
            case "album":
            case "title":
            case "genre":
            case "year":
            case "pagecount":
            case "author":
            case "subject":
            case "keywords":
                return await GetContentFieldValueCoreAsync(normalized, filePath, cancellationToken);
        }

        return null;
    }

    private async Task<object?> GetContentFieldValueCoreAsync(string fieldName, string filePath, CancellationToken cancellationToken)
    {
        // Support "pluginId:field" format for TC-style content plugins
        var splitIndex = fieldName.IndexOf(':');
        if (splitIndex > 0 && _pluginService != null)
        {
            var pluginId = fieldName.Substring(0, splitIndex);
            var field = fieldName.Substring(splitIndex + 1);
            if (!string.IsNullOrWhiteSpace(pluginId) && !string.IsNullOrWhiteSpace(field))
            {
                return await _pluginService.GetContentFieldValueAsync(pluginId, field, filePath, cancellationToken);
            }
        }

        if (_contentPluginService != null)
        {
            var contentValue = await _contentPluginService.GetFieldValueAsync(filePath, fieldName, cancellationToken);
            if (contentValue != null)
            {
                return contentValue.Value ?? contentValue.DisplayValue;
            }
        }

        if (_pluginService != null)
        {
            foreach (var plugin in _pluginService.GetPluginsByType(PluginType.Content))
            {
                var fields = _pluginService.GetContentFields(plugin.Id);
                var match = fields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return await _pluginService.GetContentFieldValueAsync(plugin.Id, match.Name, filePath, cancellationToken);
                }
            }
        }

        return null;
    }

    private async Task<object?> EvaluateExpressionCoreAsync(string expression, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var trimmed = expression.Trim();
        if (trimmed.StartsWith("[=", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(2, trimmed.Length - 3).Trim();
        }

        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var variablesNeeded = ExtractExpressionVariables(trimmed);
        var variables = await BuildExpressionVariablesAsync(variablesNeeded, filePath, cancellationToken);

        // Direct variable expression
        if (variablesNeeded.Count == 1 && string.Equals(trimmed, variablesNeeded.First(), StringComparison.OrdinalIgnoreCase))
        {
            var key = variablesNeeded.First();
            return variables.TryGetValue(key, out var value) ? value : null;
        }

        var prepared = ReplaceVariables(trimmed, variables);
        if (string.Equals(prepared, trimmed, StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            var table = new DataTable
            {
                Locale = CultureInfo.InvariantCulture
            };

            var result = table.Compute(prepared, string.Empty);
            return result is DBNull ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> ExtractExpressionVariables(string expression)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ExpressionVariableRegex.Matches(expression))
        {
            results.Add(match.Value);
        }
        return results;
    }

    private async Task<Dictionary<string, object?>> BuildExpressionVariablesAsync(
        HashSet<string> variableNames,
        string filePath,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in variableNames)
        {
            var parts = raw.Split('.', 2);
            if (parts.Length != 2)
                continue;

            var prefix = parts[0].ToLowerInvariant();
            var name = parts[1];
            object? value = null;

            switch (prefix)
            {
                case "tc":
                    value = await ResolveTcVariableAsync(name, filePath, cancellationToken);
                    break;
                case "shell":
                    value = await GetShellPropertyValueCoreAsync(name, filePath, cancellationToken);
                    break;
                case "content":
                    value = await GetContentFieldValueCoreAsync(name, filePath, cancellationToken);
                    break;
            }

            variables[raw] = value;
        }

        return variables;
    }

    private async Task<object?> ResolveTcVariableAsync(string name, string filePath, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();

        switch (lower)
        {
            case "name":
                return Path.GetFileName(filePath);
            case "ext":
            case "extension":
                return Path.GetExtension(filePath).TrimStart('.');
            case "size":
                return new FileInfo(filePath).Exists ? new FileInfo(filePath).Length : 0L;
            case "date":
            case "modified":
                return GetFilePropertyValue("LastWriteTime", filePath);
            case "created":
                return GetFilePropertyValue("CreationTime", filePath);
            case "accessed":
                return GetFilePropertyValue("LastAccessTime", filePath);
            case "attributes":
                return GetFilePropertyValue("Attributes", filePath);
            case "path":
            case "directory":
            case "dir":
                return Path.GetDirectoryName(filePath);
            case "fullname":
            case "fullpath":
                return Path.GetFullPath(filePath);
            case "type":
            case "typename":
                return await GetShellPropertyValueCoreAsync("TypeName", filePath, cancellationToken);
            case "description":
                return await GetShellPropertyValueCoreAsync("Description", filePath, cancellationToken);
            case "version":
                return await GetShellPropertyValueCoreAsync("Version", filePath, cancellationToken);
            case "company":
                return await GetShellPropertyValueCoreAsync("Company", filePath, cancellationToken);
            case "comments":
                return await GetShellPropertyValueCoreAsync("Comments", filePath, cancellationToken);
            case "owner":
                return await GetShellPropertyValueCoreAsync("Owner", filePath, cancellationToken);
            case "crc32":
                return await GetShellPropertyValueCoreAsync("CRC32", filePath, cancellationToken);
            case "md5":
                return await GetShellPropertyValueCoreAsync("MD5", filePath, cancellationToken);
            case "sha256":
                return await GetShellPropertyValueCoreAsync("SHA256", filePath, cancellationToken);
            case "dimensions":
            case "duration":
            case "bitrate":
            case "artist":
            case "album":
            case "title":
            case "genre":
            case "year":
            case "pagecount":
            case "author":
            case "subject":
            case "keywords":
                return await GetContentFieldValueCoreAsync(name, filePath, cancellationToken);
        }

        return null;
    }

    private static string ReplaceVariables(string expression, Dictionary<string, object?> variables)
    {
        return ExpressionVariableRegex.Replace(expression, match =>
        {
            var key = match.Value;
            if (!variables.TryGetValue(key, out var value))
            {
                return "NULL";
            }

            return FormatExpressionLiteral(value);
        });
    }

    private static string FormatExpressionLiteral(object? value)
    {
        if (value == null)
            return "NULL";

        if (value is bool boolValue)
            return boolValue ? "TRUE" : "FALSE";

        if (value is DateTime dateTime)
            return dateTime.Ticks.ToString(CultureInfo.InvariantCulture);

        if (value is string strValue)
        {
            var escaped = strValue.Replace("'", "''");
            return $"'{escaped}'";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return $"'{value.ToString()?.Replace("'", "''")}'";
    }

    private string GetTypeName(string filePath)
    {
        if (Directory.Exists(filePath))
            return "File Folder";

        try
        {
            if (Path.GetPathRoot(filePath) == filePath)
            {
                var driveInfo = new DriveInfo(filePath);
                if (driveInfo.IsReady)
                {
                    return string.IsNullOrEmpty(driveInfo.VolumeLabel)
                        ? $"{driveInfo.DriveType} Drive"
                        : $"{driveInfo.VolumeLabel} ({driveInfo.DriveType})";
                }
            }
        }
        catch
        {
            // Ignore drive errors
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return "File";

        if (KnownTypeNames.TryGetValue(extension, out var typeName))
            return typeName;

        return $"{extension.TrimStart('.').ToUpperInvariant()} File";
    }

    private async Task<string?> GetDescriptionAsync(string filePath, CancellationToken cancellationToken)
    {
        if (_descriptionFileService == null)
            return null;

        try
        {
            return await _descriptionFileService.GetDescriptionAsync(filePath);
        }
        catch
        {
            return null;
        }
    }

    private static FileVersionInfo? GetFileVersionInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;
            return FileVersionInfo.GetVersionInfo(filePath);
        }
        catch
        {
            return null;
        }
    }

    private string? GetOwnerName(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    var accessControl = fileInfo.GetAccessControl();
                    var owner = accessControl.GetOwner(typeof(System.Security.Principal.NTAccount));
                    return owner?.ToString();
                }

                var directoryInfo = new DirectoryInfo(filePath);
                if (directoryInfo.Exists)
                {
                    var accessControl = directoryInfo.GetAccessControl();
                    var owner = accessControl.GetOwner(typeof(System.Security.Principal.NTAccount));
                    return owner?.ToString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static long? GetCompressedSize(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
                return null;
            return info.Length;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetChecksumAsync(string filePath, ChecksumAlgorithm algorithm, CancellationToken cancellationToken)
    {
        if (_fileChecksumService == null)
            return null;

        try
        {
            if (!File.Exists(filePath))
                return null;

            var result = await _fileChecksumService.CalculateChecksumAsync(filePath, algorithm, cancellationToken);
            return result.Success ? result.Hash : null;
        }
        catch
        {
            return null;
        }
    }
    
    private string? FormatValue(object? value, CustomColumnDefinition column)
    {
        if (value == null)
        {
            return column.NullValueDisplay ?? string.Empty;
        }
        
        switch (column.DataType)
        {
            case CustomColumnDataType.Size:
                if (value is long size)
                {
                    return FormatFileSize(size);
                }
                break;
                
            case CustomColumnDataType.DateTime:
            case CustomColumnDataType.Date:
                if (value is DateTime dateTime)
                {
                    return string.IsNullOrEmpty(column.FormatString) 
                        ? dateTime.ToString() 
                        : dateTime.ToString(column.FormatString);
                }
                break;
                
            case CustomColumnDataType.Number:
                if (!string.IsNullOrEmpty(column.FormatString) && value is IFormattable formattable)
                {
                    return formattable.ToString(column.FormatString, null);
                }
                break;
                
            case CustomColumnDataType.Boolean:
                if (value is bool boolValue)
                {
                    return boolValue ? "Yes" : "No";
                }
                break;
        }
        
        return value.ToString();
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:N1} {suffixes[suffixIndex]}";
    }
    
    private string? GetSortValue(object? value, CustomColumnDefinition column)
    {
        if (value == null) return null;
        
        // For sorting, we want a value that sorts correctly
        return column.DataType switch
        {
            CustomColumnDataType.Size or CustomColumnDataType.Number => value.ToString()?.PadLeft(20, '0'),
            CustomColumnDataType.DateTime or CustomColumnDataType.Date when value is DateTime dt => dt.Ticks.ToString().PadLeft(20, '0'),
            _ => value.ToString()?.ToLowerInvariant()
        };
    }
    
    public async Task<IReadOnlyDictionary<string, CustomColumnValue>> GetColumnValuesAsync(IEnumerable<string> columnIds, string filePath, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, CustomColumnValue>();
        
        foreach (var columnId in columnIds)
        {
            results[columnId] = await GetColumnValueAsync(columnId, filePath, cancellationToken);
        }
        
        return results;
    }
    
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, CustomColumnValue>>> GetBatchColumnValuesAsync(
        IEnumerable<string> columnIds, 
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken = default)
    {
        var columnIdList = columnIds.ToList();
        var results = new Dictionary<string, IReadOnlyDictionary<string, CustomColumnValue>>();
        
        // Process in parallel for better performance
        var tasks = filePaths.Select(async filePath =>
        {
            var values = await GetColumnValuesAsync(columnIdList, filePath, cancellationToken);
            return (filePath, values);
        });
        
        foreach (var task in tasks)
        {
            var (filePath, values) = await task;
            results[filePath] = values;
        }
        
        return results;
    }
    
    public void RegisterColumnProvider(string pluginId, IColumnValueProvider provider)
    {
        _providers[pluginId] = provider;
    }
    
    public void UnregisterColumnProvider(string pluginId)
    {
        _providers.TryRemove(pluginId, out _);
    }
    
    public Task<IReadOnlyList<string>> GetAvailableFieldNamesAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_providers.TryGetValue(pluginId, out var provider))
        {
            return Task.FromResult(provider.GetSupportedFields());
        }

        if (string.Equals(pluginId, "content", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pluginId, "builtin", StringComparison.OrdinalIgnoreCase))
        {
            if (_contentPluginService != null)
            {
                var fields = _contentPluginService.GetPlugins()
                    .SelectMany(p => p.Fields)
                    .Select(f => f.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return Task.FromResult<IReadOnlyList<string>>(fields);
            }
        }

        if (_pluginService != null)
        {
            var fields = _pluginService.GetPluginsByType(PluginType.Content)
                .SelectMany(p => _pluginService.GetContentFields(p.Id))
                .Select(f => f.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fields.Count > 0)
            {
                return Task.FromResult<IReadOnlyList<string>>(fields);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
    
    public async Task<IReadOnlyList<CustomColumnDefinition>> ImportColumnsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var columns = JsonSerializer.Deserialize<List<CustomColumnDefinition>>(json) ?? new();
        
        var imported = new List<CustomColumnDefinition>();
        foreach (var column in columns)
        {
            var newColumn = await CreateColumnAsync(column with { Id = Guid.NewGuid().ToString() }, cancellationToken);
            imported.Add(newColumn);
        }
        
        return imported;
    }
    
    public async Task ExportColumnsAsync(IEnumerable<string> columnIds, string filePath, CancellationToken cancellationToken = default)
    {
        var columns = columnIds
            .Select(id => GetColumn(id))
            .Where(c => c != null)
            .Cast<CustomColumnDefinition>()
            .ToList();
        
        var json = JsonSerializer.Serialize(columns, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public Task<(bool IsValid, string? ErrorMessage)> ValidateExpressionAsync(string expression, CancellationToken cancellationToken = default)
    {
        // Basic expression validation
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult((false, (string?)"Expression cannot be empty"));
        }
        
        // Check for balanced brackets
        int brackets = 0;
        foreach (var c in expression)
        {
            if (c == '[') brackets++;
            else if (c == ']') brackets--;
            
            if (brackets < 0)
            {
                return Task.FromResult((false, (string?)"Unbalanced brackets"));
            }
        }
        
        if (brackets != 0)
        {
            return Task.FromResult((false, (string?)"Unbalanced brackets"));
        }
        
        return Task.FromResult((true, (string?)null));
    }
    
    private void InvalidateCacheForColumn(string columnId)
    {
        foreach (var fileCache in _valueCache.Values)
        {
            fileCache.TryRemove(columnId, out _);
        }
    }
    
    public void InvalidateCache()
    {
        _valueCache.Clear();
    }
    
    public void InvalidateCacheForFile(string filePath)
    {
        _valueCache.TryRemove(filePath, out _);
    }

    private sealed class CustomColumnStore
    {
        public List<CustomColumnDefinition> Columns { get; init; } = new();
        public List<CustomColumnSet> ColumnSets { get; init; } = new();
    }
    
    private void OnColumnsChanged()
    {
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnColumnSetsChanged()
    {
        ColumnSetsChanged?.Invoke(this, EventArgs.Empty);
    }
}
