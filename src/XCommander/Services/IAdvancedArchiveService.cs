using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Advanced archive operations interface for Total Commander parity.
/// Extends basic archive operations with multi-volume, conversion, repair, and comments.
/// </summary>
public interface IAdvancedArchiveService
{
    /// <summary>
    /// Tests the integrity of an archive and returns detailed results.
    /// </summary>
    Task<ArchiveTestResult> TestArchiveDetailedAsync(string archivePath, 
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempts to repair a damaged archive.
    /// </summary>
    Task<ArchiveRepairResult> RepairArchiveAsync(string archivePath, string outputPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts an archive from one format to another.
    /// </summary>
    Task ConvertArchiveAsync(string sourcePath, string destinationPath, ArchiveType targetType,
        CompressionLevel compressionLevel = CompressionLevel.Normal,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a multi-volume (split) archive.
    /// </summary>
    Task CreateMultiVolumeArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        long volumeSize, ArchiveType type = ArchiveType.Zip,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts a multi-volume archive (requires all volumes present).
    /// </summary>
    Task ExtractMultiVolumeArchiveAsync(string firstVolumePath, string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the archive comment if any.
    /// </summary>
    Task<string?> GetArchiveCommentAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the archive comment (ZIP only).
    /// </summary>
    Task SetArchiveCommentAsync(string archivePath, string comment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a self-extracting (SFX) archive.
    /// </summary>
    Task CreateSfxArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        SfxOptions options,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets archive information including type, compression ratio, etc.
    /// </summary>
    Task<ArchiveInfo> GetArchiveInfoAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Encrypts an archive with password protection.
    /// </summary>
    Task CreateEncryptedArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        string password, EncryptionMethod method = EncryptionMethod.AES256,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts a password-protected archive.
    /// </summary>
    Task ExtractEncryptedArchiveAsync(string archivePath, string destinationPath,
        string password,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Archive integrity test result.
/// </summary>
public class ArchiveTestResult
{
    public bool IsValid { get; init; }
    public List<ArchiveTestEntry> TestedEntries { get; init; } = new();
    public List<ArchiveTestError> Errors { get; init; } = new();
    public int TotalEntries { get; init; }
    public int ValidEntries { get; init; }
    public int CorruptedEntries { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Individual entry test result.
/// </summary>
public class ArchiveTestEntry
{
    public string Path { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public uint? ExpectedCrc { get; init; }
    public uint? ActualCrc { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Archive test error details.
/// </summary>
public class ArchiveTestError
{
    public string EntryPath { get; init; } = string.Empty;
    public string ErrorType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Archive repair result.
/// </summary>
public class ArchiveRepairResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public int RecoveredEntries { get; init; }
    public int LostEntries { get; init; }
    public List<string> RecoveredFiles { get; init; } = new();
    public List<string> LostFiles { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Self-extracting archive options.
/// </summary>
public class SfxOptions
{
    public string? Title { get; init; }
    public string? ExtractPath { get; init; }
    public string? RunAfterExtract { get; init; }
    public bool ShowProgress { get; init; } = true;
    public bool OverwriteExisting { get; init; } = false;
    public SfxTarget Target { get; init; } = SfxTarget.Console;
}

/// <summary>
/// SFX target platform.
/// </summary>
public enum SfxTarget
{
    Console,
    Windows,
    CrossPlatform
}

/// <summary>
/// Archive information summary.
/// </summary>
public class ArchiveInfo
{
    public string Path { get; init; } = string.Empty;
    public ArchiveType Type { get; init; }
    public long FileSize { get; init; }
    public long UncompressedSize { get; init; }
    public double CompressionRatio { get; init; }
    public int EntryCount { get; init; }
    public int FileCount { get; init; }
    public int DirectoryCount { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public string? Comment { get; init; }
    public bool IsEncrypted { get; init; }
    public bool IsMultiVolume { get; init; }
    public bool IsSolid { get; init; }
    public string CompressionMethod { get; init; } = string.Empty;
}

/// <summary>
/// Encryption methods for password-protected archives.
/// </summary>
public enum EncryptionMethod
{
    ZipCrypto,      // Classic ZIP encryption (weak)
    AES128,         // AES 128-bit
    AES192,         // AES 192-bit
    AES256          // AES 256-bit (recommended)
}
