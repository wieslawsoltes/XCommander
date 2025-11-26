using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for handling legacy and specialized archive formats.
/// Supports ACE, ARJ, CAB, LZH, UC2, XZ/LZMA formats via external tools or native implementations.
/// </summary>
public interface ILegacyArchiveService
{
    /// <summary>
    /// Gets the list of supported archive formats.
    /// </summary>
    IReadOnlyList<LegacyArchiveFormat> SupportedFormats { get; }
    
    /// <summary>
    /// Checks if a file is a supported legacy archive format.
    /// </summary>
    bool IsSupported(string archivePath);
    
    /// <summary>
    /// Gets archive format information.
    /// </summary>
    LegacyArchiveFormat? GetFormat(string archivePath);
    
    /// <summary>
    /// Lists contents of a legacy archive.
    /// </summary>
    Task<IEnumerable<LegacyArchiveEntry>> ListContentsAsync(string archivePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts all files from a legacy archive.
    /// </summary>
    Task ExtractAllAsync(string archivePath, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts specific files from a legacy archive.
    /// </summary>
    Task ExtractFilesAsync(string archivePath, IEnumerable<string> entryPaths, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new archive (for formats that support creation).
    /// </summary>
    Task CreateArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        LegacyArchiveFormat format, LegacyCompressionOptions? options = null,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tests archive integrity.
    /// </summary>
    Task<LegacyArchiveTestResult> TestArchiveAsync(string archivePath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets archive information.
    /// </summary>
    Task<LegacyArchiveInfo> GetArchiveInfoAsync(string archivePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if required tools are available for a format.
    /// </summary>
    bool IsToolAvailable(LegacyArchiveFormat format);
    
    /// <summary>
    /// Configures external tool path for a format.
    /// </summary>
    void SetToolPath(LegacyArchiveFormat format, string toolPath);
}

/// <summary>
/// Legacy archive format definition.
/// </summary>
public record LegacyArchiveFormat
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    public bool CanRead { get; init; }
    public bool CanCreate { get; init; }
    public bool RequiresExternalTool { get; init; }
    public string? ExternalToolName { get; init; }
    public byte[]? MagicBytes { get; init; }
}

/// <summary>
/// Entry in a legacy archive.
/// </summary>
public record LegacyArchiveEntry
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public DateTime? Modified { get; init; }
    public uint? Crc { get; init; }
    public string? CompressionMethod { get; init; }
    public FileAttributes Attributes { get; init; }
    public double CompressionRatio => Size > 0 ? (double)CompressedSize / Size : 1.0;
}

/// <summary>
/// Progress for legacy archive operations.
/// </summary>
public record LegacyArchiveProgress
{
    public string CurrentEntry { get; init; } = string.Empty;
    public int EntriesProcessed { get; init; }
    public int TotalEntries { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public string Phase { get; init; } = string.Empty;
    public double Percentage => TotalEntries > 0 ? (double)EntriesProcessed / TotalEntries * 100 : 0;
}

/// <summary>
/// Legacy archive test result.
/// </summary>
public record LegacyArchiveTestResult
{
    public bool IsValid { get; init; }
    public int TotalEntries { get; init; }
    public int ValidEntries { get; init; }
    public int CorruptedEntries { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Legacy archive information.
/// </summary>
public record LegacyArchiveInfo
{
    public string Path { get; init; } = string.Empty;
    public LegacyArchiveFormat Format { get; init; } = new();
    public long FileSize { get; init; }
    public long UncompressedSize { get; init; }
    public int EntryCount { get; init; }
    public int FileCount { get; init; }
    public int DirectoryCount { get; init; }
    public double CompressionRatio { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public string? Comment { get; init; }
    public bool IsEncrypted { get; init; }
    public bool IsMultiVolume { get; init; }
    public bool IsSolid { get; init; }
}

/// <summary>
/// Compression options for creating legacy archives.
/// </summary>
public record LegacyCompressionOptions
{
    public int CompressionLevel { get; init; } = 5;
    public bool Solid { get; init; }
    public string? Password { get; init; }
    public Dictionary<string, string>? CustomOptions { get; init; }
}
