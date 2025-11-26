namespace XCommander.Models;

/// <summary>
/// Represents a column configuration for file panels.
/// </summary>
public class ColumnConfiguration
{
    /// <summary>
    /// Unique identifier for the column.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the column.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Property path or binding expression for the column value.
    /// </summary>
    public string Binding { get; set; } = string.Empty;
    
    /// <summary>
    /// Column width in pixels.
    /// </summary>
    public double Width { get; set; } = 100;
    
    /// <summary>
    /// Whether the column is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;
    
    /// <summary>
    /// Display order of the column.
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Text alignment within the column.
    /// </summary>
    public ColumnAlignment Alignment { get; set; } = ColumnAlignment.Left;
    
    /// <summary>
    /// Whether users can sort by this column.
    /// </summary>
    public bool CanSort { get; set; } = true;
    
    /// <summary>
    /// Custom format string for the column value.
    /// </summary>
    public string? FormatString { get; set; }
    
    /// <summary>
    /// Name of a plugin column provider.
    /// </summary>
    public string? PluginId { get; set; }
    
    /// <summary>
    /// Whether this is a built-in column.
    /// </summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// Column alignment options.
/// </summary>
public enum ColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Built-in column identifiers.
/// </summary>
public static class BuiltInColumns
{
    public const string Name = "name";
    public const string Extension = "ext";
    public const string Size = "size";
    public const string DateModified = "dateModified";
    public const string DateCreated = "dateCreated";
    public const string Attributes = "attributes";
    public const string Path = "path";
    
    /// <summary>
    /// Get default columns configuration.
    /// </summary>
    public static List<ColumnConfiguration> GetDefaultColumns()
    {
        return new List<ColumnConfiguration>
        {
            new()
            {
                Id = Name,
                Name = "Name",
                Binding = "Name",
                Width = 250,
                Order = 0,
                IsBuiltIn = true,
                Alignment = ColumnAlignment.Left
            },
            new()
            {
                Id = Extension,
                Name = "Ext",
                Binding = "Extension",
                Width = 60,
                Order = 1,
                IsBuiltIn = true,
                Alignment = ColumnAlignment.Left
            },
            new()
            {
                Id = Size,
                Name = "Size",
                Binding = "DisplaySize",
                Width = 80,
                Order = 2,
                IsBuiltIn = true,
                Alignment = ColumnAlignment.Right
            },
            new()
            {
                Id = DateModified,
                Name = "Date Modified",
                Binding = "DateModified",
                Width = 130,
                Order = 3,
                IsBuiltIn = true,
                FormatString = "yyyy-MM-dd HH:mm"
            }
        };
    }
    
    /// <summary>
    /// Get all available built-in columns.
    /// </summary>
    public static List<ColumnConfiguration> GetAllAvailableColumns()
    {
        return new List<ColumnConfiguration>
        {
            new()
            {
                Id = Name,
                Name = "Name",
                Binding = "Name",
                Width = 250,
                IsBuiltIn = true
            },
            new()
            {
                Id = Extension,
                Name = "Extension",
                Binding = "Extension",
                Width = 60,
                IsBuiltIn = true
            },
            new()
            {
                Id = Size,
                Name = "Size",
                Binding = "DisplaySize",
                Width = 80,
                IsBuiltIn = true,
                Alignment = ColumnAlignment.Right
            },
            new()
            {
                Id = DateModified,
                Name = "Date Modified",
                Binding = "DateModified",
                Width = 130,
                IsBuiltIn = true,
                FormatString = "yyyy-MM-dd HH:mm"
            },
            new()
            {
                Id = DateCreated,
                Name = "Date Created",
                Binding = "DateCreated",
                Width = 130,
                IsBuiltIn = true,
                IsVisible = false,
                FormatString = "yyyy-MM-dd HH:mm"
            },
            new()
            {
                Id = Attributes,
                Name = "Attributes",
                Binding = "Attributes",
                Width = 80,
                IsBuiltIn = true,
                IsVisible = false
            },
            new()
            {
                Id = Path,
                Name = "Path",
                Binding = "FullPath",
                Width = 300,
                IsBuiltIn = true,
                IsVisible = false
            }
        };
    }
}
