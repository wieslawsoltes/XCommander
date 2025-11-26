using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public interface IArchiveService
{
    /// <summary>
    /// Gets the list of supported archive extensions.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
    
    /// <summary>
    /// Checks if a file is a supported archive.
    /// </summary>
    bool IsArchive(string path);
    
    /// <summary>
    /// Lists entries in an archive.
    /// </summary>
    Task<List<ArchiveEntry>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts all files from an archive.
    /// </summary>
    Task ExtractAllAsync(string archivePath, string destinationPath, 
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts specific entries from an archive.
    /// </summary>
    Task ExtractEntriesAsync(string archivePath, IEnumerable<string> entryPaths, string destinationPath,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new archive from files.
    /// </summary>
    Task CreateArchiveAsync(string archivePath, IEnumerable<string> sourcePaths, ArchiveType type,
        CompressionLevel compressionLevel = CompressionLevel.Normal,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds files to an existing archive.
    /// </summary>
    Task AddToArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes entries from an archive.
    /// </summary>
    Task DeleteEntriesAsync(string archivePath, IEnumerable<string> entryPaths,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tests archive integrity.
    /// </summary>
    Task<bool> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default);
}

public class ArchiveEntry
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsEncrypted { get; set; }
    public uint? Crc { get; set; }
    
    public double CompressionRatio => Size > 0 ? (1 - (double)CompressedSize / Size) * 100 : 0;
    public string SizeDisplay => FormatSize(Size);
    public string CompressedSizeDisplay => FormatSize(CompressedSize);
    public string RatioDisplay => $"{CompressionRatio:F1}%";
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

public class ArchiveProgress
{
    public string CurrentEntry { get; set; } = string.Empty;
    public int EntriesProcessed { get; set; }
    public int TotalEntries { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage => TotalEntries > 0 ? (double)EntriesProcessed / TotalEntries * 100 : 0;
}

public enum ArchiveType
{
    Zip,
    SevenZip,
    Tar,
    GZip,
    BZip2,
    Rar
}

public enum CompressionLevel
{
    None,
    Fastest,
    Fast,
    Normal,
    Maximum,
    Ultra
}
