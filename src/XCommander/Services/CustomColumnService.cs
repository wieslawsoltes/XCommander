// CustomColumnService.cs - Implementation of TC-style custom columns
// Manages user-defined columns with expression support and plugin integration

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public class CustomColumnService : ICustomColumnService
{
    private readonly ConcurrentDictionary<string, CustomColumnDefinition> _builtInColumns = new();
    private readonly ConcurrentDictionary<string, CustomColumnDefinition> _userColumns = new();
    private readonly ConcurrentDictionary<string, CustomColumnSet> _columnSets = new();
    private readonly ConcurrentDictionary<string, IColumnValueProvider> _providers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CustomColumnValue>> _valueCache = new();
    
    public event EventHandler<EventArgs>? ColumnsChanged;
    public event EventHandler<EventArgs>? ColumnSetsChanged;
    
    public CustomColumnService()
    {
        InitializeBuiltInColumns();
        InitializeDefaultColumnSets();
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
    
    public Task<CustomColumnDefinition> CreateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default)
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
        
        return Task.FromResult(definition);
    }
    
    public Task<bool> UpdateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default)
    {
        if (_builtInColumns.ContainsKey(definition.Id))
        {
            return Task.FromResult(false); // Cannot modify built-in columns
        }
        
        if (!_userColumns.ContainsKey(definition.Id))
        {
            return Task.FromResult(false);
        }
        
        _userColumns[definition.Id] = definition with { IsBuiltIn = false };
        InvalidateCacheForColumn(definition.Id);
        OnColumnsChanged();
        
        return Task.FromResult(true);
    }
    
    public Task<bool> DeleteColumnAsync(string columnId, CancellationToken cancellationToken = default)
    {
        if (_builtInColumns.ContainsKey(columnId))
        {
            return Task.FromResult(false); // Cannot delete built-in columns
        }
        
        if (_userColumns.TryRemove(columnId, out _))
        {
            InvalidateCacheForColumn(columnId);
            OnColumnsChanged();
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }
    
    public IReadOnlyList<CustomColumnSet> GetColumnSets()
    {
        return _columnSets.Values.OrderBy(s => s.IsBuiltIn ? 0 : 1).ThenBy(s => s.Name).ToList();
    }
    
    public CustomColumnSet? GetColumnSet(string setId)
    {
        return _columnSets.TryGetValue(setId, out var set) ? set : null;
    }
    
    public Task<CustomColumnSet> CreateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(set.Id))
        {
            set = set with { Id = Guid.NewGuid().ToString() };
        }
        
        _columnSets[set.Id] = set with { IsBuiltIn = false };
        OnColumnSetsChanged();
        
        return Task.FromResult(set);
    }
    
    public Task<bool> UpdateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default)
    {
        if (!_columnSets.TryGetValue(set.Id, out var existing))
        {
            return Task.FromResult(false);
        }
        
        if (existing.IsBuiltIn)
        {
            return Task.FromResult(false);
        }
        
        _columnSets[set.Id] = set with { IsBuiltIn = false };
        OnColumnSetsChanged();
        
        return Task.FromResult(true);
    }
    
    public Task<bool> DeleteColumnSetAsync(string setId, CancellationToken cancellationToken = default)
    {
        if (!_columnSets.TryGetValue(setId, out var existing))
        {
            return Task.FromResult(false);
        }
        
        if (existing.IsBuiltIn)
        {
            return Task.FromResult(false);
        }
        
        if (_columnSets.TryRemove(setId, out _))
        {
            OnColumnSetsChanged();
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
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
        // Shell property extraction would require platform-specific code
        // This is a placeholder for now
        return Task.FromResult<object?>(null);
    }
    
    private Task<object?> GetContentFieldValueAsync(string fieldName, string filePath, CancellationToken cancellationToken)
    {
        // Content field extraction from content plugins
        // This is a placeholder for now
        return Task.FromResult<object?>(null);
    }
    
    private Task<object?> EvaluateExpressionAsync(string expression, string filePath, CancellationToken cancellationToken)
    {
        // Simple expression evaluation
        // Full implementation would support TC-style expressions
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return Task.FromResult<object?>(null);
            
            // Support basic expressions like "[=tc.Size/1024]" for KB
            if (expression.Contains("tc.Size"))
            {
                var sizeKb = fileInfo.Length / 1024.0;
                return Task.FromResult<object?>(sizeKb);
            }
            
            return Task.FromResult<object?>(null);
        }
        catch
        {
            return Task.FromResult<object?>(null);
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
    
    private void OnColumnsChanged()
    {
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnColumnSetsChanged()
    {
        ColumnSetsChanged?.Invoke(this, EventArgs.Empty);
    }
}
