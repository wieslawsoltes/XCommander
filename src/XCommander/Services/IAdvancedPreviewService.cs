using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Advanced content preview service for specialized file types.
/// Supports HTML/Web, Office documents, CAD files, databases, fonts, and plugin management.
/// </summary>
public interface IAdvancedPreviewService
{
    /// <summary>
    /// Gets a preview for the specified file.
    /// </summary>
    Task<FilePreview> GetPreviewAsync(string filePath, PreviewOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file type is supported for preview.
    /// </summary>
    bool IsSupported(string filePath);
    
    /// <summary>
    /// Gets the preview provider for a file type.
    /// </summary>
    IPreviewProvider? GetProvider(string filePath);
    
    /// <summary>
    /// Registers a preview provider.
    /// </summary>
    void RegisterProvider(IPreviewProvider provider);
    
    /// <summary>
    /// Unregisters a preview provider.
    /// </summary>
    void UnregisterProvider(string providerId);
    
    /// <summary>
    /// Gets all registered preview providers.
    /// </summary>
    IReadOnlyList<IPreviewProvider> GetProviders();
    
    /// <summary>
    /// Sets the priority for a preview provider.
    /// </summary>
    void SetProviderPriority(string providerId, int priority);
    
    /// <summary>
    /// Gets HTML preview for web content.
    /// </summary>
    Task<HtmlPreview> GetHtmlPreviewAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets Office document preview.
    /// </summary>
    Task<DocumentPreview> GetOfficePreviewAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets CAD file preview (DWG, DXF).
    /// </summary>
    Task<CadPreview> GetCadPreviewAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets database preview (SQLite, DBF).
    /// </summary>
    Task<DatabasePreview> GetDatabasePreviewAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets font preview.
    /// </summary>
    Task<FontPreview> GetFontPreviewAsync(string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a query on a database file.
    /// </summary>
    Task<QueryResult> ExecuteDatabaseQueryAsync(string filePath, string query,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets table schema from a database file.
    /// </summary>
    Task<IReadOnlyList<TableSchema>> GetDatabaseSchemaAsync(string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for preview providers.
/// </summary>
public interface IPreviewProvider
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    int Priority { get; set; }
    IReadOnlyList<string> SupportedExtensions { get; }
    bool IsSupported(string filePath);
    Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Preview options.
/// </summary>
public record PreviewOptions
{
    public int MaxWidth { get; init; } = 800;
    public int MaxHeight { get; init; } = 600;
    public bool GenerateThumbnail { get; init; }
    public int ThumbnailSize { get; init; } = 128;
    public bool ExtractText { get; init; }
    public int MaxTextLength { get; init; } = 10000;
    public bool IncludeMetadata { get; init; } = true;
}

/// <summary>
/// Base preview result.
/// </summary>
public record FilePreview
{
    public string FilePath { get; init; } = string.Empty;
    public PreviewType Type { get; init; }
    public string ProviderId { get; init; } = string.Empty;
    public bool IsSupported { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? ThumbnailData { get; init; }
    public string? TextContent { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Preview type enumeration.
/// </summary>
public enum PreviewType
{
    None,
    Text,
    Html,
    Image,
    Video,
    Audio,
    Document,
    Spreadsheet,
    Presentation,
    Cad,
    Database,
    Font,
    Archive,
    Binary,
    Custom
}

/// <summary>
/// HTML/Web preview result.
/// </summary>
public record HtmlPreview : FilePreview
{
    public string? HtmlContent { get; init; }
    public string? RenderedHtml { get; init; }
    public IReadOnlyList<string>? LinkedResources { get; init; }
    public IReadOnlyList<string>? Scripts { get; init; }
    public IReadOnlyList<string>? Stylesheets { get; init; }
    public string? Title { get; init; }
    public string? BaseUrl { get; init; }
}

/// <summary>
/// Office document preview result.
/// </summary>
public record DocumentPreview : FilePreview
{
    public string? DocumentTitle { get; init; }
    public string? Author { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public int PageCount { get; init; }
    public int WordCount { get; init; }
    public int CharacterCount { get; init; }
    public string? Subject { get; init; }
    public IReadOnlyList<string>? Keywords { get; init; }
    public IReadOnlyList<DocumentPage>? Pages { get; init; }
}

/// <summary>
/// Document page representation.
/// </summary>
public record DocumentPage
{
    public int PageNumber { get; init; }
    public string? TextContent { get; init; }
    public byte[]? ImageData { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// CAD file preview result.
/// </summary>
public record CadPreview : FilePreview
{
    public string? CadFormat { get; init; }
    public string? Version { get; init; }
    public CadBounds? Bounds { get; init; }
    public int LayerCount { get; init; }
    public int EntityCount { get; init; }
    public IReadOnlyList<CadLayer>? Layers { get; init; }
    public IReadOnlyList<CadEntity>? Entities { get; init; }
    public byte[]? RenderedImage { get; init; }
    public string? SvgContent { get; init; }
}

/// <summary>
/// CAD bounding box.
/// </summary>
public record CadBounds
{
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MinZ { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
    public double MaxZ { get; init; }
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double Depth => MaxZ - MinZ;
}

/// <summary>
/// CAD layer definition.
/// </summary>
public record CadLayer
{
    public string Name { get; init; } = string.Empty;
    public bool IsVisible { get; init; } = true;
    public bool IsLocked { get; init; }
    public string? Color { get; init; }
    public int EntityCount { get; init; }
}

/// <summary>
/// CAD entity representation.
/// </summary>
public record CadEntity
{
    public string Type { get; init; } = string.Empty;
    public string? LayerName { get; init; }
    public string? Color { get; init; }
    public IReadOnlyList<double>? Points { get; init; }
    public IReadOnlyDictionary<string, object>? Properties { get; init; }
}

/// <summary>
/// Database preview result.
/// </summary>
public record DatabasePreview : FilePreview
{
    public string? DatabaseType { get; init; }
    public string? Version { get; init; }
    public long FileSize { get; init; }
    public int TableCount { get; init; }
    public int ViewCount { get; init; }
    public int IndexCount { get; init; }
    public IReadOnlyList<TableSchema>? Tables { get; init; }
    public IReadOnlyDictionary<string, int>? TableRowCounts { get; init; }
}

/// <summary>
/// Database table schema.
/// </summary>
public record TableSchema
{
    public string Name { get; init; } = string.Empty;
    public string? Type { get; init; } = "TABLE";
    public IReadOnlyList<ColumnSchema>? Columns { get; init; }
    public IReadOnlyList<string>? PrimaryKeys { get; init; }
    public IReadOnlyList<ForeignKey>? ForeignKeys { get; init; }
    public IReadOnlyList<IndexSchema>? Indexes { get; init; }
    public int RowCount { get; init; }
}

/// <summary>
/// Database column schema.
/// </summary>
public record ColumnSchema
{
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
    public string? DefaultValue { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
}

/// <summary>
/// Database foreign key definition.
/// </summary>
public record ForeignKey
{
    public string Name { get; init; } = string.Empty;
    public string Column { get; init; } = string.Empty;
    public string ReferencedTable { get; init; } = string.Empty;
    public string ReferencedColumn { get; init; } = string.Empty;
}

/// <summary>
/// Database index schema.
/// </summary>
public record IndexSchema
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string>? Columns { get; init; }
    public bool IsUnique { get; init; }
    public bool IsPrimaryKey { get; init; }
}

/// <summary>
/// Database query result.
/// </summary>
public record QueryResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int RowsAffected { get; init; }
    public IReadOnlyList<string>? ColumnNames { get; init; }
    public IReadOnlyList<IReadOnlyList<object?>>? Rows { get; init; }
    public TimeSpan ExecutionTime { get; init; }
}

/// <summary>
/// Font preview result.
/// </summary>
public record FontPreview : FilePreview
{
    public string? FontFamily { get; init; }
    public string? FullName { get; init; }
    public string? Style { get; init; }
    public string? Weight { get; init; }
    public string? Version { get; init; }
    public string? Copyright { get; init; }
    public string? Designer { get; init; }
    public string? Manufacturer { get; init; }
    public string? License { get; init; }
    public int GlyphCount { get; init; }
    public IReadOnlyList<string>? SupportedLanguages { get; init; }
    public IReadOnlyList<FontSample>? Samples { get; init; }
    public byte[]? SampleImage { get; init; }
}

/// <summary>
/// Font sample text with rendered preview.
/// </summary>
public record FontSample
{
    public string Text { get; init; } = string.Empty;
    public int FontSize { get; init; }
    public byte[]? RenderedImage { get; init; }
    public string? SvgContent { get; init; }
}
