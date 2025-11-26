// IOverwriteDialogService.cs - Enhanced Overwrite Dialog Service
// Provides TC-style advanced overwrite dialog with preview and comparison

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for handling file overwrite decisions with enhanced preview and comparison.
/// </summary>
public interface IOverwriteDialogService
{
    /// <summary>
    /// Shows the enhanced overwrite dialog for a file conflict.
    /// </summary>
    Task<OverwriteDecision> ShowOverwriteDialogAsync(
        FileConflictInfo conflict,
        OverwriteDialogOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows the enhanced overwrite dialog for multiple file conflicts.
    /// </summary>
    Task<BatchOverwriteResult> ShowBatchOverwriteDialogAsync(
        IEnumerable<FileConflictInfo> conflicts,
        OverwriteDialogOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two files by content from the overwrite dialog.
    /// </summary>
    Task<FileCompareResult> CompareFilesAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets preview data for a file (image, text preview, etc.).
    /// </summary>
    Task<FilePreviewData> GetFilePreviewAsync(
        string filePath,
        OverwritePreviewOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a file conflict.
/// </summary>
public record FileConflictInfo
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public required long SourceSize { get; init; }
    public required long TargetSize { get; init; }
    public required DateTime SourceModified { get; init; }
    public required DateTime TargetModified { get; init; }
    public string? SourceChecksum { get; init; }
    public string? TargetChecksum { get; init; }
    public FileAttributes SourceAttributes { get; init; }
    public FileAttributes TargetAttributes { get; init; }
    public Dictionary<string, string>? SourceCustomFields { get; init; }
    public Dictionary<string, string>? TargetCustomFields { get; init; }
    
    /// <summary>
    /// Whether the source file is newer than the target.
    /// </summary>
    public bool IsSourceNewer => SourceModified > TargetModified;
    
    /// <summary>
    /// Whether the files have the same size.
    /// </summary>
    public bool IsSameSize => SourceSize == TargetSize;
    
    /// <summary>
    /// Whether the files are identical by checksum.
    /// </summary>
    public bool? AreIdentical => 
        SourceChecksum != null && TargetChecksum != null
            ? SourceChecksum == TargetChecksum
            : null;
}

/// <summary>
/// Options for the overwrite dialog.
/// </summary>
public class OverwriteDialogOptions
{
    /// <summary>
    /// Whether to show file previews (images, etc.).
    /// </summary>
    public bool ShowPreview { get; set; } = true;
    
    /// <summary>
    /// Whether to show custom content plugin fields.
    /// </summary>
    public bool ShowCustomFields { get; set; } = true;
    
    /// <summary>
    /// Whether to enable the "Compare by content" button.
    /// </summary>
    public bool AllowCompare { get; set; } = true;
    
    /// <summary>
    /// Custom fields to display from content plugins.
    /// </summary>
    public List<string> CustomFieldsToShow { get; set; } = new();
    
    /// <summary>
    /// Auto-rename pattern when choosing to rename.
    /// </summary>
    public string AutoRenamePattern { get; set; } = "{name}_{counter}{ext}";
    
    /// <summary>
    /// Whether to remember the decision for future conflicts.
    /// </summary>
    public bool AllowRememberChoice { get; set; } = true;
    
    /// <summary>
    /// Maximum preview image size in pixels.
    /// </summary>
    public int MaxPreviewSize { get; set; } = 200;
}

/// <summary>
/// User's decision for handling a file overwrite.
/// </summary>
public enum OverwriteAction
{
    /// <summary>Overwrite the target file.</summary>
    Overwrite,
    
    /// <summary>Skip this file.</summary>
    Skip,
    
    /// <summary>Rename the source file.</summary>
    Rename,
    
    /// <summary>Cancel the entire operation.</summary>
    Cancel,
    
    /// <summary>Queue for later decision.</summary>
    Queue,
    
    /// <summary>Overwrite all remaining files.</summary>
    OverwriteAll,
    
    /// <summary>Skip all remaining files.</summary>
    SkipAll,
    
    /// <summary>Overwrite all newer files.</summary>
    OverwriteAllNewer,
    
    /// <summary>Skip all older files.</summary>
    SkipAllOlder,
    
    /// <summary>Rename all conflicting files.</summary>
    RenameAll
}

/// <summary>
/// Result of an overwrite decision.
/// </summary>
public record OverwriteDecision
{
    public required OverwriteAction Action { get; init; }
    
    /// <summary>
    /// New filename if Action is Rename.
    /// </summary>
    public string? NewName { get; init; }
    
    /// <summary>
    /// Whether to apply this decision to all remaining conflicts.
    /// </summary>
    public bool ApplyToAll { get; init; }
    
    /// <summary>
    /// Additional condition for ApplyToAll (e.g., "only newer files").
    /// </summary>
    public OverwriteCondition? Condition { get; init; }
}

/// <summary>
/// Conditions for batch overwrite decisions.
/// </summary>
public enum OverwriteCondition
{
    All,
    NewerOnly,
    OlderOnly,
    LargerOnly,
    SmallerOnly,
    DifferentOnly,
    IdenticalOnly
}

/// <summary>
/// Result of a batch overwrite dialog.
/// </summary>
public record BatchOverwriteResult
{
    public required IReadOnlyList<OverwriteDecisionItem> Decisions { get; init; }
    public bool Cancelled { get; init; }
    public int OverwriteCount { get; init; }
    public int SkipCount { get; init; }
    public int RenameCount { get; init; }
    public int QueuedCount { get; init; }
}

/// <summary>
/// Decision for a single file in a batch.
/// </summary>
public record OverwriteDecisionItem
{
    public required FileConflictInfo Conflict { get; init; }
    public required OverwriteDecision Decision { get; init; }
}

/// <summary>
/// Result of comparing two files.
/// </summary>
public record FileCompareResult
{
    public required bool AreIdentical { get; init; }
    public required long BytesDifferent { get; init; }
    public required double SimilarityPercentage { get; init; }
    public string? FirstDifferenceOffset { get; init; }
    public TimeSpan CompareTime { get; init; }
}

/// <summary>
/// Options for file preview generation in overwrite dialog.
/// </summary>
public class OverwritePreviewOptions
{
    public int MaxWidth { get; set; } = 200;
    public int MaxHeight { get; set; } = 200;
    public bool GenerateThumbnail { get; set; } = true;
    public bool ExtractTextPreview { get; set; } = true;
    public int MaxTextLength { get; set; } = 500;
}

/// <summary>
/// Preview data for a file.
/// </summary>
public record FilePreviewData
{
    public required string FilePath { get; init; }
    public required FilePreviewType Type { get; init; }
    
    /// <summary>
    /// Image thumbnail data (PNG format).
    /// </summary>
    public byte[]? ThumbnailData { get; init; }
    
    /// <summary>
    /// Text preview for text files.
    /// </summary>
    public string? TextPreview { get; init; }
    
    /// <summary>
    /// File type description.
    /// </summary>
    public string? TypeDescription { get; init; }
    
    /// <summary>
    /// Additional metadata fields.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Type of preview available.
/// </summary>
public enum FilePreviewType
{
    None,
    Image,
    Text,
    Video,
    Audio,
    Document,
    Archive,
    Unknown
}
