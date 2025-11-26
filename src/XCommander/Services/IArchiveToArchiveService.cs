using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Entry info for archive-to-archive operations
/// </summary>
public record ArchiveEntryInfo
{
    /// <summary>Entry key/path within archive</summary>
    public string Key { get; init; } = string.Empty;
    
    /// <summary>Is this a directory entry</summary>
    public bool IsDirectory { get; init; }
    
    /// <summary>Uncompressed size</summary>
    public long Size { get; init; }
    
    /// <summary>Compressed size</summary>
    public long CompressedSize { get; init; }
    
    /// <summary>Last modified time</summary>
    public DateTime? LastModified { get; init; }
    
    /// <summary>CRC32 checksum if available</summary>
    public uint? Crc32 { get; init; }
}

/// <summary>
/// Options for archive-to-archive copy
/// </summary>
public record ArchiveCopyOptions
{
    /// <summary>Preserve directory structure</summary>
    public bool PreserveStructure { get; init; } = true;
    
    /// <summary>Overwrite existing entries</summary>
    public bool OverwriteExisting { get; init; } = true;
    
    /// <summary>Skip identical files (by CRC)</summary>
    public bool SkipIdentical { get; init; } = true;
    
    /// <summary>Compression level (0-9, -1 for default)</summary>
    public int CompressionLevel { get; init; } = -1;
    
    /// <summary>Use direct copy when possible (no recompression)</summary>
    public bool DirectCopyWhenPossible { get; init; } = true;
    
    /// <summary>Path prefix to add in destination</summary>
    public string? DestinationPrefix { get; init; }
    
    /// <summary>Path prefix to strip from source</summary>
    public string? SourcePrefixToStrip { get; init; }
}

/// <summary>
/// Result of archive-to-archive copy
/// </summary>
public record ArchiveCopyResult
{
    /// <summary>Operation succeeded</summary>
    public bool Success { get; init; }
    
    /// <summary>Entries copied</summary>
    public int EntriesCopied { get; init; }
    
    /// <summary>Entries skipped</summary>
    public int EntriesSkipped { get; init; }
    
    /// <summary>Total bytes copied</summary>
    public long BytesCopied { get; init; }
    
    /// <summary>Duration of operation</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>Errors encountered</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    
    /// <summary>Whether direct copy was used</summary>
    public bool UsedDirectCopy { get; init; }
}

/// <summary>
/// Progress for archive copy operation
/// </summary>
public record ArchiveCopyProgress
{
    /// <summary>Current entry being processed</summary>
    public string CurrentEntry { get; init; } = string.Empty;
    
    /// <summary>Total entries to copy</summary>
    public int TotalEntries { get; init; }
    
    /// <summary>Entries processed</summary>
    public int ProcessedEntries { get; init; }
    
    /// <summary>Total bytes to copy</summary>
    public long TotalBytes { get; init; }
    
    /// <summary>Bytes copied</summary>
    public long CopiedBytes { get; init; }
    
    /// <summary>Current phase</summary>
    public string Phase { get; init; } = string.Empty;
    
    /// <summary>Percentage complete</summary>
    public double PercentComplete => TotalEntries > 0 
        ? (ProcessedEntries * 100.0 / TotalEntries) 
        : 0;
}

/// <summary>
/// Service for copying files directly between archives.
/// TC equivalent: Direct archive-to-archive file copy.
/// </summary>
public interface IArchiveToArchiveService
{
    /// <summary>
    /// Copy entries from source archive to destination archive
    /// </summary>
    Task<ArchiveCopyResult> CopyEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        IEnumerable<string> entryKeys,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy all entries from source to destination
    /// </summary>
    Task<ArchiveCopyResult> CopyAllEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move entries from source archive to destination archive
    /// </summary>
    Task<ArchiveCopyResult> MoveEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        IEnumerable<string> entryKeys,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List entries in archive
    /// </summary>
    Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if direct copy is possible between two archive types
    /// </summary>
    bool CanDirectCopy(string sourceArchive, string destinationArchive);
    
    /// <summary>
    /// Get supported archive extensions for source
    /// </summary>
    IReadOnlyList<string> GetSupportedSourceExtensions();
    
    /// <summary>
    /// Get supported archive extensions for destination
    /// </summary>
    IReadOnlyList<string> GetSupportedDestinationExtensions();
    
    /// <summary>
    /// Event raised when operation completes
    /// </summary>
    event EventHandler<ArchiveCopyResult>? OperationCompleted;
}
