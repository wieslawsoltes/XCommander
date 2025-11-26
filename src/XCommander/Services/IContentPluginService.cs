using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Interface for content plugins (WDX) - extracts metadata from files.
/// Provides additional columns for file lists like EXIF data, audio tags, etc.
/// </summary>
public interface IContentPluginService
{
    /// <summary>
    /// Gets all registered content plugins.
    /// </summary>
    IReadOnlyList<ContentPlugin> GetPlugins();
    
    /// <summary>
    /// Registers a content plugin.
    /// </summary>
    void RegisterPlugin(ContentPlugin plugin);
    
    /// <summary>
    /// Unregisters a content plugin.
    /// </summary>
    void UnregisterPlugin(string pluginId);
    
    /// <summary>
    /// Gets all available fields for a file.
    /// </summary>
    Task<IReadOnlyList<ContentField>> GetFieldsForFileAsync(string filePath, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the value of a specific field for a file.
    /// </summary>
    Task<ContentFieldValue?> GetFieldValueAsync(string filePath, string fieldName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets multiple field values for a file.
    /// </summary>
    Task<IReadOnlyDictionary<string, ContentFieldValue>> GetFieldValuesAsync(string filePath, 
        IEnumerable<string> fieldNames, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all field values from all applicable plugins.
    /// </summary>
    Task<IReadOnlyDictionary<string, ContentFieldValue>> GetAllFieldValuesAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a field value if the plugin supports editing.
    /// </summary>
    Task<bool> SetFieldValueAsync(string filePath, string fieldName, object value,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file is supported by any plugin.
    /// </summary>
    bool IsFileSupported(string filePath);
}

/// <summary>
/// A content plugin that extracts metadata from specific file types.
/// </summary>
public abstract class ContentPlugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    public abstract string Id { get; }
    
    /// <summary>
    /// Display name of the plugin.
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// Description of what the plugin provides.
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// File extensions this plugin supports (e.g., ".jpg", ".mp3").
    /// </summary>
    public abstract IReadOnlyList<string> SupportedExtensions { get; }
    
    /// <summary>
    /// Fields provided by this plugin.
    /// </summary>
    public abstract IReadOnlyList<ContentFieldDefinition> Fields { get; }
    
    /// <summary>
    /// Priority for ordering plugins (higher = first).
    /// </summary>
    public virtual int Priority => 0;
    
    /// <summary>
    /// Whether the plugin supports editing field values.
    /// </summary>
    public virtual bool SupportsEditing => false;
    
    /// <summary>
    /// Gets field values for a file.
    /// </summary>
    public abstract Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a field value.
    /// </summary>
    public virtual Task<bool> SetValueAsync(string filePath, string fieldName, object value,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
    
    /// <summary>
    /// Checks if a specific file is supported.
    /// </summary>
    public virtual bool IsSupported(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
}

/// <summary>
/// Definition of a content field.
/// </summary>
public class ContentFieldDefinition
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Category { get; init; }
    public ContentFieldType Type { get; init; }
    public string? Unit { get; init; }
    public bool IsEditable { get; init; }
    public int DisplayWidth { get; init; } = 100;
}

/// <summary>
/// Type of content field value.
/// </summary>
public enum ContentFieldType
{
    String,
    Integer,
    Float,
    DateTime,
    Boolean,
    Size,           // File size with formatting
    Duration,       // Time duration
    Dimensions,     // Width x Height
    BitRate,        // Audio/video bitrate
    SampleRate,     // Audio sample rate
    Ratio,          // Aspect ratio
    Custom
}

/// <summary>
/// A content field available for a file.
/// </summary>
public class ContentField
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PluginId { get; init; } = string.Empty;
    public ContentFieldType Type { get; init; }
    public bool IsEditable { get; init; }
}

/// <summary>
/// Value of a content field.
/// </summary>
public class ContentFieldValue
{
    public string FieldName { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string DisplayValue { get; init; } = string.Empty;
    public ContentFieldType Type { get; init; }
    public string? Unit { get; init; }
    public bool IsEmpty => Value == null || (Value is string s && string.IsNullOrEmpty(s));
}
