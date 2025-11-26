using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Advanced search criteria
/// </summary>
public class AdvancedSearchCriteria
{
    // Basic criteria
    public string? SearchPath { get; init; }
    public string? FileNamePattern { get; init; }
    public bool IncludeSubdirectories { get; init; } = true;
    
    // File attributes
    public long? MinSize { get; init; }
    public long? MaxSize { get; init; }
    public DateTime? ModifiedAfter { get; init; }
    public DateTime? ModifiedBefore { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    
    // Content search
    public string? ContentPattern { get; init; }
    public bool ContentRegex { get; init; }
    public bool ContentCaseSensitive { get; init; }
    
    // Exclude patterns
    public IReadOnlyList<string>? ExcludePatterns { get; init; }
    public IReadOnlyList<string>? ExcludeFolders { get; init; }
    
    // Hash search
    public string? FileHash { get; init; }
    public HashAlgorithmType HashAlgorithm { get; init; } = HashAlgorithmType.MD5;
    
    // EXIF search (images)
    public ExifSearchCriteria? ExifCriteria { get; init; }
    
    // Audio tag search
    public AudioTagSearchCriteria? AudioCriteria { get; init; }
    
    // File type filters
    public IReadOnlyList<string>? AllowedExtensions { get; init; }
    public IReadOnlyList<string>? ExcludedExtensions { get; init; }
    
    // Attributes
    public bool? IsReadOnly { get; init; }
    public bool? IsHidden { get; init; }
    public bool? IsSystem { get; init; }
    public bool? IsArchive { get; init; }
}

public enum HashAlgorithmType
{
    MD5,
    SHA1,
    SHA256,
    SHA512,
    CRC32
}

/// <summary>
/// EXIF search criteria for images
/// </summary>
public class ExifSearchCriteria
{
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public DateTime? DateTakenAfter { get; init; }
    public DateTime? DateTakenBefore { get; init; }
    public int? MinWidth { get; init; }
    public int? MaxWidth { get; init; }
    public int? MinHeight { get; init; }
    public int? MaxHeight { get; init; }
    public string? Artist { get; init; }
    public string? Copyright { get; init; }
    public double? MinLatitude { get; init; }
    public double? MaxLatitude { get; init; }
    public double? MinLongitude { get; init; }
    public double? MaxLongitude { get; init; }
}

/// <summary>
/// Audio tag search criteria for MP3/audio files
/// </summary>
public class AudioTagSearchCriteria
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public int? Year { get; init; }
    public int? MinYear { get; init; }
    public int? MaxYear { get; init; }
    public int? MinDurationSeconds { get; init; }
    public int? MaxDurationSeconds { get; init; }
    public int? MinBitrate { get; init; }
    public int? MaxBitrate { get; init; }
}

/// <summary>
/// Search result with extended metadata
/// </summary>
public class AdvancedSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public DateTime Created { get; init; }
    public FileAttributes Attributes { get; init; }
    
    // Computed hash if requested
    public string? FileHash { get; init; }
    
    // EXIF data if image
    public ExifData? Exif { get; init; }
    
    // Audio tags if audio file
    public AudioTags? Audio { get; init; }
    
    // Content match details
    public IReadOnlyList<ContentMatch>? ContentMatches { get; init; }
}

/// <summary>
/// EXIF metadata
/// </summary>
public class ExifData
{
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public DateTime? DateTaken { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? Artist { get; init; }
    public string? Copyright { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? ExposureTime { get; init; }
    public string? FNumber { get; init; }
    public int? IsoSpeed { get; init; }
}

/// <summary>
/// Audio file tags
/// </summary>
public class AudioTags
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public int? Year { get; init; }
    public int? Track { get; init; }
    public TimeSpan? Duration { get; init; }
    public int? Bitrate { get; init; }
    public int? SampleRate { get; init; }
    public int? Channels { get; init; }
}

/// <summary>
/// Content match in file
/// </summary>
public class ContentMatch
{
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = string.Empty;
    public int MatchStart { get; init; }
    public int MatchLength { get; init; }
}

/// <summary>
/// Saved search query
/// </summary>
public record SavedSearchQuery
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AdvancedSearchCriteria Criteria { get; init; } = new();
    public DateTime Created { get; init; } = DateTime.Now;
    public DateTime LastUsed { get; init; }
    public int UseCount { get; init; }
}

/// <summary>
/// Search history entry
/// </summary>
public class SearchHistoryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public AdvancedSearchCriteria Criteria { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int ResultCount { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Search progress information
/// </summary>
public class SearchProgress
{
    public string CurrentPath { get; init; } = string.Empty;
    public int FilesScanned { get; init; }
    public int FoldersScanned { get; init; }
    public int MatchesFound { get; init; }
    public double PercentComplete { get; init; }
}

/// <summary>
/// Duplicate file information
/// </summary>
public class DuplicateFileGroup
{
    public string Hash { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public IReadOnlyList<string> FilePaths { get; init; } = Array.Empty<string>();
    public int FileCount => FilePaths.Count;
    public long WastedSpace => FileSize * (FileCount - 1);
}

/// <summary>
/// Service for advanced file search operations
/// </summary>
public interface IAdvancedSearchService
{
    /// <summary>
    /// Search for files with advanced criteria
    /// </summary>
    IAsyncEnumerable<AdvancedSearchResult> SearchAsync(
        AdvancedSearchCriteria criteria,
        IProgress<SearchProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for duplicate files
    /// </summary>
    Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(
        string searchPath,
        bool includeSubdirectories = true,
        long? minSize = null,
        IProgress<SearchProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate file hash
    /// </summary>
    Task<string> CalculateHashAsync(string filePath, HashAlgorithmType algorithm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Read EXIF data from image file
    /// </summary>
    ExifData? ReadExifData(string filePath);
    
    /// <summary>
    /// Read audio tags from audio file
    /// </summary>
    AudioTags? ReadAudioTags(string filePath);
    
    // Saved queries
    
    /// <summary>
    /// Save a search query
    /// </summary>
    Task SaveQueryAsync(SavedSearchQuery query);
    
    /// <summary>
    /// Get all saved queries
    /// </summary>
    Task<IReadOnlyList<SavedSearchQuery>> GetSavedQueriesAsync();
    
    /// <summary>
    /// Delete a saved query
    /// </summary>
    Task DeleteQueryAsync(string queryId);
    
    /// <summary>
    /// Update saved query last used
    /// </summary>
    Task UpdateQueryUsageAsync(string queryId);
    
    // Search history
    
    /// <summary>
    /// Add entry to search history
    /// </summary>
    Task AddToHistoryAsync(SearchHistoryEntry entry);
    
    /// <summary>
    /// Get search history
    /// </summary>
    Task<IReadOnlyList<SearchHistoryEntry>> GetSearchHistoryAsync(int maxEntries = 100);
    
    /// <summary>
    /// Clear search history
    /// </summary>
    Task ClearHistoryAsync();
}
