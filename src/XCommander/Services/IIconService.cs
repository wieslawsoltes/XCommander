using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for managing file and folder icons with advanced features.
/// Supports native system icons, overlays, custom assignments, and SVG.
/// </summary>
public interface IIconService
{
    /// <summary>
    /// Gets the icon for a file or folder.
    /// </summary>
    Task<IconInfo> GetIconAsync(string path, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the native system icon for a file extension.
    /// </summary>
    Task<IconInfo> GetSystemIconAsync(string extension, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the icon for a specific file type.
    /// </summary>
    Task<IconInfo> GetFileTypeIconAsync(FileTypeCategory category, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets icon overlays for a file based on its status.
    /// </summary>
    Task<IReadOnlyList<IconOverlay>> GetOverlaysAsync(string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies overlays to a base icon.
    /// </summary>
    Task<IconInfo> ApplyOverlaysAsync(IconInfo baseIcon, IEnumerable<IconOverlay> overlays,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets custom icon assignment for a path if any.
    /// </summary>
    CustomIconAssignment? GetCustomIcon(string path);
    
    /// <summary>
    /// Sets a custom icon for a file or folder.
    /// </summary>
    Task SetCustomIconAsync(string path, CustomIconAssignment assignment,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes custom icon assignment for a path.
    /// </summary>
    Task RemoveCustomIconAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all custom icon assignments.
    /// </summary>
    IReadOnlyDictionary<string, CustomIconAssignment> GetAllCustomIcons();
    
    /// <summary>
    /// Loads an SVG icon from file.
    /// </summary>
    Task<IconInfo> LoadSvgIconAsync(string svgPath, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads an SVG icon from content.
    /// </summary>
    Task<IconInfo> LoadSvgFromContentAsync(string svgContent, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers an icon pack.
    /// </summary>
    void RegisterIconPack(IconPack pack);
    
    /// <summary>
    /// Gets available icon packs.
    /// </summary>
    IReadOnlyList<IconPack> GetIconPacks();
    
    /// <summary>
    /// Sets the active icon pack.
    /// </summary>
    void SetActiveIconPack(string packId);
    
    /// <summary>
    /// Gets the active icon pack.
    /// </summary>
    IconPack? GetActiveIconPack();
    
    /// <summary>
    /// Clears the icon cache.
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Preloads icons for common file types.
    /// </summary>
    Task PreloadCommonIconsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when icons change (theme/pack change).
    /// </summary>
    event EventHandler? IconsChanged;
}

/// <summary>
/// Icon size enumeration.
/// </summary>
public enum IconSize
{
    Small = 16,
    Medium = 24,
    Large = 32,
    ExtraLarge = 48,
    Jumbo = 64,
    Huge = 128,
    Max = 256
}

/// <summary>
/// File type category for icon lookup.
/// </summary>
public enum FileTypeCategory
{
    Unknown,
    Folder,
    FolderOpen,
    FolderEmpty,
    File,
    Document,
    Spreadsheet,
    Presentation,
    Image,
    Video,
    Audio,
    Archive,
    Executable,
    Script,
    Code,
    Database,
    Font,
    Pdf,
    Configuration,
    Binary,
    Link,
    Shortcut,
    Drive,
    Network,
    Cloud
}

/// <summary>
/// Icon information with image data.
/// </summary>
public record IconInfo
{
    public byte[]? ImageData { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public IconFormat Format { get; init; }
    public string? SourcePath { get; init; }
    public bool IsCached { get; init; }
    public FileTypeCategory Category { get; init; }
    public string? SvgContent { get; init; }
}

/// <summary>
/// Icon format enumeration.
/// </summary>
public enum IconFormat
{
    Png,
    Svg,
    Ico,
    Native
}

/// <summary>
/// Icon overlay for status indication.
/// </summary>
public record IconOverlay
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OverlayType Type { get; init; }
    public OverlayPosition Position { get; init; }
    public byte[]? ImageData { get; init; }
    public string? SvgContent { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// Overlay type enumeration.
/// </summary>
public enum OverlayType
{
    None,
    Link,
    Shortcut,
    Share,
    Lock,
    Sync,
    SyncPending,
    SyncError,
    CloudOnly,
    CloudOffline,
    Encrypted,
    ReadOnly,
    Hidden,
    Modified,
    New,
    Conflict,
    Error,
    Warning,
    Custom
}

/// <summary>
/// Overlay position enumeration.
/// </summary>
public enum OverlayPosition
{
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight,
    Center
}

/// <summary>
/// Custom icon assignment.
/// </summary>
public record CustomIconAssignment
{
    public string Path { get; init; } = string.Empty;
    public string IconPath { get; init; } = string.Empty;
    public int IconIndex { get; init; }
    public string? SvgContent { get; init; }
    public byte[]? ImageData { get; init; }
    public bool ApplyToSubfolders { get; init; }
    public DateTime AssignedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Icon pack definition.
/// </summary>
public record IconPack
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0";
    public string BasePath { get; init; } = string.Empty;
    public bool IsSvgBased { get; init; }
    public IReadOnlyDictionary<FileTypeCategory, string> CategoryMappings { get; init; } 
        = new Dictionary<FileTypeCategory, string>();
    public IReadOnlyDictionary<string, string> ExtensionMappings { get; init; } 
        = new Dictionary<string, string>();
}
