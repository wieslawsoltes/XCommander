// ICustomColumnService.cs - TC-style custom column definitions
// Allows users to define custom columns for file lists with expressions

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Data types for custom columns
/// </summary>
public enum CustomColumnDataType
{
    String,
    Number,
    Size,      // Formatted file size
    Date,
    DateTime,
    Boolean,
    Icon,
    Progress
}

/// <summary>
/// Alignment options for column content
/// </summary>
public enum CustomColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Content sources for custom columns
/// </summary>
public enum CustomColumnSource
{
    FileProperty,     // Standard file properties (name, size, date, etc.)
    Shell,            // Shell/extended properties
    Content,          // File content plugins
    Plugin,           // External plugin
    Expression,       // Calculated expression
    Registry,         // Registry-based info (Windows)
    Script            // Script evaluation
}

/// <summary>
/// Definition of a custom column
/// </summary>
public record CustomColumnDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public CustomColumnDataType DataType { get; init; } = CustomColumnDataType.String;
    public CustomColumnSource Source { get; init; } = CustomColumnSource.FileProperty;
    public string SourceField { get; init; } = string.Empty;  // Property name or expression
    public CustomColumnAlignment Alignment { get; init; } = CustomColumnAlignment.Left;
    public int Width { get; init; } = 100;
    public int? MinWidth { get; init; }
    public int? MaxWidth { get; init; }
    public bool Sortable { get; init; } = true;
    public bool Searchable { get; init; }
    public bool Visible { get; init; } = true;
    public int DisplayOrder { get; init; }
    public string? FormatString { get; init; }     // .NET format string
    public string? NullValueDisplay { get; init; } // What to show for null values
    public string? PluginId { get; init; }         // For plugin-based columns
    public bool IsBuiltIn { get; init; }           // System-provided column
    public Dictionary<string, string> Options { get; init; } = new();
}

/// <summary>
/// Column set - a named collection of columns
/// </summary>
public record CustomColumnSet
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> ColumnIds { get; init; } = Array.Empty<string>();
    public bool IsDefault { get; init; }
    public bool IsBuiltIn { get; init; }
    public string? FileTypeFilter { get; init; }  // Apply to specific file types
    public string? FolderFilter { get; init; }    // Apply to specific folders
}

/// <summary>
/// Column value result
/// </summary>
public record CustomColumnValue
{
    public string ColumnId { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string? DisplayValue { get; init; }
    public string? SortValue { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Built-in column IDs
/// </summary>
public static class BuiltInColumns
{
    public const string Name = "builtin.name";
    public const string Extension = "builtin.ext";
    public const string Size = "builtin.size";
    public const string Date = "builtin.date";
    public const string Created = "builtin.created";
    public const string Accessed = "builtin.accessed";
    public const string Attributes = "builtin.attr";
    public const string Type = "builtin.type";
    public const string Path = "builtin.path";
    public const string Icon = "builtin.icon";
    
    // Extended properties
    public const string CompressedSize = "builtin.compressedsize";
    public const string CRC32 = "builtin.crc32";
    public const string MD5 = "builtin.md5";
    public const string SHA256 = "builtin.sha256";
    public const string Owner = "builtin.owner";
    public const string Description = "builtin.description";
    public const string Version = "builtin.version";
    public const string Company = "builtin.company";
    public const string Comments = "builtin.comments";
    
    // Media
    public const string Dimensions = "builtin.dimensions";
    public const string Duration = "builtin.duration";
    public const string BitRate = "builtin.bitrate";
    public const string Artist = "builtin.artist";
    public const string Album = "builtin.album";
    public const string Title = "builtin.title";
    public const string Genre = "builtin.genre";
    public const string Year = "builtin.year";
    
    // Documents
    public const string PageCount = "builtin.pagecount";
    public const string Author = "builtin.author";
    public const string Subject = "builtin.subject";
    public const string Keywords = "builtin.keywords";
}

/// <summary>
/// Service for managing custom column definitions and values
/// </summary>
public interface ICustomColumnService
{
    /// <summary>
    /// Get all built-in column definitions
    /// </summary>
    IReadOnlyList<CustomColumnDefinition> GetBuiltInColumns();
    
    /// <summary>
    /// Get all user-defined columns
    /// </summary>
    IReadOnlyList<CustomColumnDefinition> GetUserColumns();
    
    /// <summary>
    /// Get all available columns (built-in + user)
    /// </summary>
    IReadOnlyList<CustomColumnDefinition> GetAllColumns();
    
    /// <summary>
    /// Get a column by ID
    /// </summary>
    CustomColumnDefinition? GetColumn(string columnId);
    
    /// <summary>
    /// Create a new user column
    /// </summary>
    Task<CustomColumnDefinition> CreateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a user column
    /// </summary>
    Task<bool> UpdateColumnAsync(CustomColumnDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a user column
    /// </summary>
    Task<bool> DeleteColumnAsync(string columnId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all column sets
    /// </summary>
    IReadOnlyList<CustomColumnSet> GetColumnSets();
    
    /// <summary>
    /// Get a column set by ID
    /// </summary>
    CustomColumnSet? GetColumnSet(string setId);
    
    /// <summary>
    /// Create a new column set
    /// </summary>
    Task<CustomColumnSet> CreateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a column set
    /// </summary>
    Task<bool> UpdateColumnSetAsync(CustomColumnSet set, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a column set
    /// </summary>
    Task<bool> DeleteColumnSetAsync(string setId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the appropriate column set for a folder
    /// </summary>
    CustomColumnSet GetColumnSetForFolder(string folderPath);
    
    /// <summary>
    /// Get column value for a file
    /// </summary>
    Task<CustomColumnValue> GetColumnValueAsync(string columnId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get multiple column values for a file
    /// </summary>
    Task<IReadOnlyDictionary<string, CustomColumnValue>> GetColumnValuesAsync(IEnumerable<string> columnIds, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get column values for multiple files (batch operation)
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, CustomColumnValue>>> GetBatchColumnValuesAsync(
        IEnumerable<string> columnIds, 
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Register a column value provider (for plugins)
    /// </summary>
    void RegisterColumnProvider(string pluginId, IColumnValueProvider provider);
    
    /// <summary>
    /// Unregister a column value provider
    /// </summary>
    void UnregisterColumnProvider(string pluginId);
    
    /// <summary>
    /// Get available content field names from plugins
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableFieldNamesAsync(string pluginId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import column definitions from file
    /// </summary>
    Task<IReadOnlyList<CustomColumnDefinition>> ImportColumnsAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export column definitions to file
    /// </summary>
    Task ExportColumnsAsync(IEnumerable<string> columnIds, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate a column expression
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateExpressionAsync(string expression, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when columns are changed
    /// </summary>
    event EventHandler<EventArgs>? ColumnsChanged;
    
    /// <summary>
    /// Event raised when column sets are changed
    /// </summary>
    event EventHandler<EventArgs>? ColumnSetsChanged;
}

/// <summary>
/// Interface for column value providers (plugins)
/// </summary>
public interface IColumnValueProvider
{
    /// <summary>
    /// Get supported field names
    /// </summary>
    IReadOnlyList<string> GetSupportedFields();
    
    /// <summary>
    /// Get field value for a file
    /// </summary>
    Task<object?> GetFieldValueAsync(string fieldName, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get field data type
    /// </summary>
    CustomColumnDataType GetFieldDataType(string fieldName);
    
    /// <summary>
    /// Get field display name
    /// </summary>
    string? GetFieldDisplayName(string fieldName);
    
    /// <summary>
    /// Check if field is supported for file
    /// </summary>
    bool SupportsFile(string fieldName, string filePath);
}
