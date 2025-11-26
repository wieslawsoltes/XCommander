// ISplitMergeService.cs - TC-style file split and merge functionality
// Split large files for floppy/media transport, merge them back

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Options for file splitting
/// </summary>
public record SplitOptions
{
    /// <summary>
    /// Target size for each split part
    /// </summary>
    public long PartSize { get; init; } = 1_457_664; // 1.44 MB floppy default
    
    /// <summary>
    /// Predefined split sizes (TC presets)
    /// </summary>
    public static class Presets
    {
        public const long Floppy360K = 362496;
        public const long Floppy720K = 730112;
        public const long Floppy144M = 1457664;
        public const long Zip100M = 100_663_296;
        public const long Zip250M = 250_331_136;
        public const long CD650M = 681_574_400;
        public const long CD700M = 734_003_200;
        public const long DVD47G = 4_707_319_808;
        public const long DVD85G = 8_543_666_176;
        public const long BluRay25G = 25_025_314_816;
        public const long BluRay50G = 50_050_629_632;
        public const long USB1G = 1_073_741_824;
        public const long USB2G = 2_147_483_648;
        public const long USB4G = 4_294_967_296;
        public const long USB8G = 8_589_934_592;
    }
    
    /// <summary>
    /// Custom target directory for split files (null = same as source)
    /// </summary>
    public string? TargetDirectory { get; init; }
    
    /// <summary>
    /// Naming pattern for split files
    /// </summary>
    public SplitNamingPattern NamingPattern { get; init; } = SplitNamingPattern.NumberedExtension;
    
    /// <summary>
    /// Start number for numbered parts
    /// </summary>
    public int StartNumber { get; init; } = 1;
    
    /// <summary>
    /// Create CRC checksum file
    /// </summary>
    public bool CreateChecksumFile { get; init; } = true;
    
    /// <summary>
    /// Delete original file after successful split
    /// </summary>
    public bool DeleteOriginalAfterSplit { get; init; }
    
    /// <summary>
    /// Verify split files by comparing CRC
    /// </summary>
    public bool VerifyAfterSplit { get; init; } = true;
    
    /// <summary>
    /// Create batch file for DOS/Windows merging
    /// </summary>
    public bool CreateBatchFile { get; init; }
    
    /// <summary>
    /// Create shell script for Unix merging
    /// </summary>
    public bool CreateShellScript { get; init; }
}

/// <summary>
/// Naming patterns for split files
/// </summary>
public enum SplitNamingPattern
{
    /// <summary>
    /// file.001, file.002, etc.
    /// </summary>
    NumberedExtension,
    
    /// <summary>
    /// file.zip.001, file.zip.002, etc. (preserves original extension)
    /// </summary>
    PreserveExtension,
    
    /// <summary>
    /// file_part1.dat, file_part2.dat, etc.
    /// </summary>
    PartSuffix,
    
    /// <summary>
    /// Custom pattern with {0} for number
    /// </summary>
    Custom
}

/// <summary>
/// Information about a split file part
/// </summary>
public record SplitPart
{
    public string FilePath { get; init; } = string.Empty;
    public int PartNumber { get; init; }
    public long Size { get; init; }
    public long StartOffset { get; init; }
    public long EndOffset { get; init; }
    public string? Checksum { get; init; }
}

/// <summary>
/// Result of a split operation
/// </summary>
public record SplitResult
{
    public string OriginalFile { get; init; } = string.Empty;
    public long OriginalSize { get; init; }
    public string? OriginalChecksum { get; init; }
    public IReadOnlyList<SplitPart> Parts { get; init; } = Array.Empty<SplitPart>();
    public int TotalParts => Parts.Count;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ChecksumFilePath { get; init; }
    public string? BatchFilePath { get; init; }
    public string? ShellScriptPath { get; init; }
}

/// <summary>
/// Options for file merging
/// </summary>
public record MergeOptions
{
    /// <summary>
    /// Target file path for merged file
    /// </summary>
    public string? TargetPath { get; init; }
    
    /// <summary>
    /// Verify checksums during merge
    /// </summary>
    public bool VerifyChecksums { get; init; } = true;
    
    /// <summary>
    /// Delete part files after successful merge
    /// </summary>
    public bool DeletePartsAfterMerge { get; init; }
    
    /// <summary>
    /// Resume partial merge if target exists
    /// </summary>
    public bool ResumeIfExists { get; init; }
}

/// <summary>
/// Result of a merge operation
/// </summary>
public record MergeResult
{
    public string MergedFile { get; init; } = string.Empty;
    public long MergedSize { get; init; }
    public string? Checksum { get; init; }
    public IReadOnlyList<string> PartFiles { get; init; } = Array.Empty<string>();
    public int PartsProcessed { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public bool ChecksumVerified { get; init; }
}

/// <summary>
/// Progress information for split/merge operations
/// </summary>
public record SplitMergeProgress
{
    public string CurrentOperation { get; init; } = string.Empty;
    public int CurrentPart { get; init; }
    public int TotalParts { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (BytesProcessed * 100.0 / TotalBytes) : 0;
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public double SpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Checksum file information (TC .crc format)
/// </summary>
public record ChecksumFileInfo
{
    public string OriginalFileName { get; init; } = string.Empty;
    public long OriginalFileSize { get; init; }
    public string Checksum { get; init; } = string.Empty;
    public string ChecksumType { get; init; } = "CRC32";
    public int TotalParts { get; init; }
    public IReadOnlyDictionary<int, string> PartChecksums { get; init; } = new Dictionary<int, string>();
}

/// <summary>
/// Service for splitting and merging files
/// </summary>
public interface ISplitMergeService
{
    /// <summary>
    /// Split a file into multiple parts
    /// </summary>
    Task<SplitResult> SplitFileAsync(
        string sourceFile,
        SplitOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Merge split files back into original
    /// </summary>
    Task<MergeResult> MergeFilesAsync(
        string firstPartOrChecksumFile,
        MergeOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Merge files in explicit order
    /// </summary>
    Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<string> partFiles,
        string targetPath,
        MergeOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect and list all parts of a split file
    /// </summary>
    Task<IReadOnlyList<string>> DetectPartsAsync(
        string firstPartOrChecksumFile,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify split file parts against checksum file
    /// </summary>
    Task<bool> VerifyPartsAsync(
        string checksumFile,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Read checksum file information
    /// </summary>
    Task<ChecksumFileInfo?> ReadChecksumFileAsync(
        string checksumFile,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate optimal part size for target media
    /// </summary>
    long CalculateOptimalPartSize(long fileSize, int maxParts);
    
    /// <summary>
    /// Calculate number of parts for given size
    /// </summary>
    int CalculatePartCount(long fileSize, long partSize);
    
    /// <summary>
    /// Get predefined split sizes
    /// </summary>
    IReadOnlyDictionary<string, long> GetPresetSizes();
}
