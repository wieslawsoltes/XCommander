// IPluginService.cs - TC-style plugin system
// Supports WCX (packer), WDX (content), WFX (file system), WLX (lister) plugins

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Types of TC plugins
/// </summary>
public enum PluginType
{
    /// <summary>
    /// Packer plugins (WCX) - archive handling
    /// </summary>
    Packer,
    
    /// <summary>
    /// Content plugins (WDX) - file content fields
    /// </summary>
    Content,
    
    /// <summary>
    /// File system plugins (WFX) - virtual file systems
    /// </summary>
    FileSystem,
    
    /// <summary>
    /// Lister plugins (WLX) - file viewing
    /// </summary>
    Lister
}

/// <summary>
/// Plugin capabilities
/// </summary>
[Flags]
public enum PluginCapabilities
{
    None = 0,
    
    // Packer capabilities
    CanCreate = 1 << 0,           // Can create new archives
    CanModify = 1 << 1,           // Can modify existing archives
    CanDelete = 1 << 2,           // Can delete files from archives
    CanExtract = 1 << 3,          // Can extract files
    CanEncrypt = 1 << 4,          // Supports encryption
    CanCompress = 1 << 5,         // Supports compression
    SupportsSolid = 1 << 6,       // Supports solid archives
    SupportsMultiVolume = 1 << 7, // Supports multi-volume archives
    
    // Content capabilities
    CanSearch = 1 << 8,           // Supports searching
    CanSort = 1 << 9,             // Fields can be sorted
    CanEdit = 1 << 10,            // Fields can be edited
    
    // File system capabilities
    CanRead = 1 << 11,            // Can read files
    CanWrite = 1 << 12,           // Can write files
    CanCreateFolder = 1 << 13,    // Can create folders
    CanRename = 1 << 14,          // Can rename files/folders
    CanExecute = 1 << 15,         // Can execute files
    CanSetTime = 1 << 16,         // Can set file times
    CanSetAttr = 1 << 17,         // Can set attributes
    
    // Lister capabilities
    SupportsText = 1 << 18,       // Can display text
    SupportsImage = 1 << 19,      // Can display images
    SupportsMultimedia = 1 << 20, // Can play multimedia
    SupportsPrint = 1 << 21,      // Can print content
    SupportsSearch = 1 << 22,     // Supports text search
    SupportsCopy = 1 << 23        // Supports copy to clipboard
}

/// <summary>
/// Plugin metadata
/// </summary>
public record PluginInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? Version { get; init; }
    public string? Website { get; init; }
    public PluginType Type { get; init; }
    public PluginCapabilities Capabilities { get; init; }
    public string? PluginPath { get; init; }       // Path to plugin file
    public string? ConfigPath { get; init; }       // Path to config file
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();  // Supported extensions
    public IReadOnlyList<string> DetectStrings { get; init; } = Array.Empty<string>(); // Detection strings
    public bool IsEnabled { get; init; } = true;
    public bool IsBuiltIn { get; init; }
    public DateTime? LoadedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
}

/// <summary>
/// Archive item information for packer plugins
/// </summary>
public record ArchiveItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public long PackedSize { get; init; }
    public DateTime ModifiedTime { get; init; }
    public FileAttributes Attributes { get; init; }
    public uint Crc { get; init; }
    public string? Method { get; init; }          // Compression method
    public bool IsDirectory { get; init; }
    public bool IsEncrypted { get; init; }
    public int? VolumeNumber { get; init; }       // For multi-volume archives
}

/// <summary>
/// Content field definition for content plugins
/// </summary>
public record PluginContentField
{
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public PluginContentFieldType FieldType { get; init; }
    public string? Unit { get; init; }
    public bool CanEdit { get; init; }
    public int? MaxLength { get; init; }
}

/// <summary>
/// Content field types (matching TC WDX spec)
/// </summary>
public enum PluginContentFieldType
{
    NoContent = 0,
    NumericInt32 = 1,
    NumericInt64 = 2,
    NumericFloat = 3,
    Date = 4,
    Time = 5,
    Boolean = 6,
    MultChoice = 7,
    String = 8,
    FullText = 9,
    DateTime = 10,
    Comparison = 11
}

/// <summary>
/// File system item for file system plugins
/// </summary>
public record PluginFileSystemItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime ModifiedTime { get; init; }
    public DateTime? CreatedTime { get; init; }
    public DateTime? AccessedTime { get; init; }
    public FileAttributes Attributes { get; init; }
    public bool IsDirectory { get; init; }
    public string? Owner { get; init; }
    public string? Group { get; init; }
    public int? Permissions { get; init; }
    public Dictionary<string, string> CustomProperties { get; init; } = new();
}

/// <summary>
/// Progress callback delegate
/// </summary>
public delegate bool PluginProgressCallback(string? currentFile, int percentDone);

/// <summary>
/// Service for managing TC-style plugins
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// Get all registered plugins
    /// </summary>
    IReadOnlyList<PluginInfo> GetAllPlugins();
    
    /// <summary>
    /// Get plugins by type
    /// </summary>
    IReadOnlyList<PluginInfo> GetPluginsByType(PluginType type);
    
    /// <summary>
    /// Get plugin by ID
    /// </summary>
    PluginInfo? GetPlugin(string pluginId);
    
    /// <summary>
    /// Get plugin for file extension
    /// </summary>
    PluginInfo? GetPluginForExtension(PluginType type, string extension);
    
    /// <summary>
    /// Get plugin for file content (using detection)
    /// </summary>
    Task<PluginInfo?> GetPluginForFileAsync(PluginType type, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Register a plugin
    /// </summary>
    Task<PluginInfo> RegisterPluginAsync(string pluginPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unregister a plugin
    /// </summary>
    Task<bool> UnregisterPluginAsync(string pluginId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enable/disable a plugin
    /// </summary>
    Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Configure a plugin
    /// </summary>
    Task ConfigurePluginAsync(string pluginId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan for plugins in a directory
    /// </summary>
    Task<IReadOnlyList<string>> ScanForPluginsAsync(string directory, CancellationToken cancellationToken = default);
    
    // ======= Packer Plugin Operations (WCX) =======
    
    /// <summary>
    /// Open an archive
    /// </summary>
    Task<IPackerHandle?> OpenArchiveAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List archive contents
    /// </summary>
    Task<IReadOnlyList<ArchiveItem>> ListArchiveAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract files from archive
    /// </summary>
    Task<bool> ExtractFromArchiveAsync(
        string archivePath, 
        string targetPath, 
        IEnumerable<string>? files = null,
        string? password = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new archive
    /// </summary>
    Task<bool> CreateArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        ArchiveOptions? options = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add files to existing archive
    /// </summary>
    Task<bool> AddToArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        string? basePath = null,
        ArchiveOptions? options = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete files from archive
    /// </summary>
    Task<bool> DeleteFromArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test archive integrity
    /// </summary>
    Task<PluginArchiveTestResult> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default);
    
    // ======= Content Plugin Operations (WDX) =======
    
    /// <summary>
    /// Get available content fields from plugin
    /// </summary>
    IReadOnlyList<PluginContentField> GetContentFields(string pluginId);
    
    /// <summary>
    /// Get field value for a file
    /// </summary>
    Task<object?> GetContentFieldValueAsync(
        string pluginId, 
        string fieldName, 
        string filePath, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set field value for a file
    /// </summary>
    Task<bool> SetContentFieldValueAsync(
        string pluginId,
        string fieldName,
        string filePath,
        object value,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare files using content plugin
    /// </summary>
    Task<int> CompareFilesAsync(
        string pluginId,
        string fieldName,
        string file1,
        string file2,
        CancellationToken cancellationToken = default);
    
    // ======= File System Plugin Operations (WFX) =======
    
    /// <summary>
    /// Connect to virtual file system
    /// </summary>
    Task<IFileSystemHandle?> ConnectAsync(string pluginId, string? connectionString = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List directory contents
    /// </summary>
    Task<IReadOnlyList<PluginFileSystemItem>> ListDirectoryAsync(
        string pluginId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file from virtual file system
    /// </summary>
    Task<bool> GetFileAsync(
        string pluginId,
        string remotePath,
        string localPath,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Put file to virtual file system
    /// </summary>
    Task<bool> PutFileAsync(
        string pluginId,
        string localPath,
        string remotePath,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete file in virtual file system
    /// </summary>
    Task<bool> DeleteFileAsync(string pluginId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create directory in virtual file system
    /// </summary>
    Task<bool> CreateDirectoryAsync(string pluginId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove directory in virtual file system
    /// </summary>
    Task<bool> RemoveDirectoryAsync(string pluginId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute file in virtual file system
    /// </summary>
    Task<bool> ExecuteFileAsync(string pluginId, string path, string? parameters = null, CancellationToken cancellationToken = default);
    
    // ======= Lister Plugin Operations (WLX) =======
    
    /// <summary>
    /// Get lister plugin for file
    /// </summary>
    Task<IListerHandle?> LoadFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search text in lister
    /// </summary>
    Task<bool> SearchTextAsync(IListerHandle handle, string searchText, bool caseSensitive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Print file content
    /// </summary>
    Task<bool> PrintAsync(IListerHandle handle, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get text content for clipboard
    /// </summary>
    Task<string?> GetTextAsync(IListerHandle handle, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when plugin is loaded
    /// </summary>
    event EventHandler<PluginEventArgs>? PluginLoaded;
    
    /// <summary>
    /// Event raised when plugin is unloaded
    /// </summary>
    event EventHandler<PluginEventArgs>? PluginUnloaded;
    
    /// <summary>
    /// Event raised when plugin list changes
    /// </summary>
    event EventHandler<EventArgs>? PluginsChanged;
}

/// <summary>
/// Options for archive creation/modification
/// </summary>
public record ArchiveOptions
{
    public int CompressionLevel { get; init; } = 5;
    public string? CompressionMethod { get; init; }
    public string? Password { get; init; }
    public bool EncryptHeaders { get; init; }
    public bool SolidArchive { get; init; }
    public long? VolumeSize { get; init; }       // For multi-volume
    public bool IncludeEmptyFolders { get; init; }
    public bool PreserveAttributes { get; init; } = true;
    public bool PreserveTimes { get; init; } = true;
    public bool RecurseSubfolders { get; init; } = true;
    public IReadOnlyList<string>? ExcludePatterns { get; init; }
}

/// <summary>
/// Archive test result
/// </summary>
public record PluginArchiveTestResult
{
    public bool Success { get; init; }
    public int TotalFiles { get; init; }
    public int TestedFiles { get; init; }
    public int FailedFiles { get; init; }
    public IReadOnlyList<string> FailedFileNames { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Plugin event arguments
/// </summary>
public class PluginEventArgs : EventArgs
{
    public PluginInfo Plugin { get; }
    
    public PluginEventArgs(PluginInfo plugin)
    {
        Plugin = plugin;
    }
}

/// <summary>
/// Handle for packer operations
/// </summary>
public interface IPackerHandle : IDisposable
{
    string ArchivePath { get; }
    PluginInfo Plugin { get; }
    bool IsOpen { get; }
}

/// <summary>
/// Handle for file system operations
/// </summary>
public interface IFileSystemHandle : IDisposable
{
    string PluginId { get; }
    string? ConnectionString { get; }
    bool IsConnected { get; }
    string CurrentPath { get; }
}

/// <summary>
/// Handle for lister operations
/// </summary>
public interface IListerHandle : IDisposable
{
    string FilePath { get; }
    PluginInfo Plugin { get; }
    bool IsLoaded { get; }
    nint WindowHandle { get; }
}
