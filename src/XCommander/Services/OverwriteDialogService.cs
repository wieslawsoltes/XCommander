// OverwriteDialogService.cs - Enhanced Overwrite Dialog Service Implementation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of the enhanced overwrite dialog service.
/// </summary>
public class OverwriteDialogService : IOverwriteDialogService
{
    private readonly IContentPluginService? _contentPluginService;
    private readonly IAdvancedPreviewService? _previewService;
    
    // Common image extensions for preview
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico", ".tiff", ".tif"
    };
    
    // Common text extensions
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
        ".cs", ".java", ".py", ".rb", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
        ".yaml", ".yml", ".ini", ".cfg", ".conf", ".log", ".csv"
    };
    
    public OverwriteDialogService(
        IContentPluginService? contentPluginService = null,
        IAdvancedPreviewService? previewService = null)
    {
        _contentPluginService = contentPluginService;
        _previewService = previewService;
    }
    
    public async Task<OverwriteDecision> ShowOverwriteDialogAsync(
        FileConflictInfo conflict,
        OverwriteDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        // Load additional conflict details if needed
        var enrichedConflict = await EnrichConflictInfoAsync(conflict, options, cancellationToken);
        
        // In a real implementation, this would show a dialog
        // For now, we provide the logic and let the ViewModel handle UI
        
        return new OverwriteDecision
        {
            Action = OverwriteAction.Skip,
            ApplyToAll = false
        };
    }
    
    public async Task<BatchOverwriteResult> ShowBatchOverwriteDialogAsync(
        IEnumerable<FileConflictInfo> conflicts,
        OverwriteDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        var conflictList = conflicts.ToList();
        var decisions = new List<OverwriteDecisionItem>();
        
        OverwriteDecision? globalDecision = null;
        
        foreach (var conflict in conflictList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // If we have a global decision that applies, use it
            if (globalDecision?.ApplyToAll == true)
            {
                if (ShouldApplyDecision(conflict, globalDecision))
                {
                    decisions.Add(new OverwriteDecisionItem
                    {
                        Conflict = conflict,
                        Decision = globalDecision
                    });
                    continue;
                }
            }
            
            // Otherwise, get a decision for this conflict
            var decision = await ShowOverwriteDialogAsync(conflict, options, cancellationToken);
            
            if (decision.Action == OverwriteAction.Cancel)
            {
                return new BatchOverwriteResult
                {
                    Decisions = decisions,
                    Cancelled = true,
                    OverwriteCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Overwrite),
                    SkipCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Skip),
                    RenameCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Rename),
                    QueuedCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Queue)
                };
            }
            
            if (decision.ApplyToAll)
            {
                globalDecision = decision;
            }
            
            decisions.Add(new OverwriteDecisionItem
            {
                Conflict = conflict,
                Decision = decision
            });
        }
        
        return new BatchOverwriteResult
        {
            Decisions = decisions,
            Cancelled = false,
            OverwriteCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Overwrite),
            SkipCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Skip),
            RenameCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Rename),
            QueuedCount = decisions.Count(d => d.Decision.Action == OverwriteAction.Queue)
        };
    }
    
    public async Task<FileCompareResult> CompareFilesAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        var sourceInfo = new FileInfo(sourcePath);
        var targetInfo = new FileInfo(targetPath);
        
        if (!sourceInfo.Exists || !targetInfo.Exists)
        {
            return new FileCompareResult
            {
                AreIdentical = false,
                BytesDifferent = Math.Max(sourceInfo.Length, targetInfo.Length),
                SimilarityPercentage = 0,
                CompareTime = DateTime.UtcNow - startTime
            };
        }
        
        // Quick size check
        if (sourceInfo.Length != targetInfo.Length)
        {
            return new FileCompareResult
            {
                AreIdentical = false,
                BytesDifferent = Math.Abs(sourceInfo.Length - targetInfo.Length),
                SimilarityPercentage = CalculateSimilarity(sourceInfo.Length, targetInfo.Length),
                CompareTime = DateTime.UtcNow - startTime
            };
        }
        
        // Byte-by-byte comparison
        const int bufferSize = 81920; // 80KB buffer
        var sourceBuffer = new byte[bufferSize];
        var targetBuffer = new byte[bufferSize];
        long bytesDifferent = 0;
        long firstDifferenceOffset = -1;
        long totalBytes = sourceInfo.Length;
        
        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        await using var targetStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        
        long offset = 0;
        int sourceBytesRead;
        
        while ((sourceBytesRead = await sourceStream.ReadAsync(sourceBuffer.AsMemory(), cancellationToken)) > 0)
        {
            var targetBytesRead = await targetStream.ReadAsync(targetBuffer.AsMemory(0, sourceBytesRead), cancellationToken);
            
            for (int i = 0; i < sourceBytesRead; i++)
            {
                if (sourceBuffer[i] != targetBuffer[i])
                {
                    bytesDifferent++;
                    if (firstDifferenceOffset == -1)
                    {
                        firstDifferenceOffset = offset + i;
                    }
                }
            }
            
            offset += sourceBytesRead;
        }
        
        return new FileCompareResult
        {
            AreIdentical = bytesDifferent == 0,
            BytesDifferent = bytesDifferent,
            SimilarityPercentage = totalBytes > 0 ? (1.0 - (double)bytesDifferent / totalBytes) * 100 : 100,
            FirstDifferenceOffset = firstDifferenceOffset >= 0 ? $"0x{firstDifferenceOffset:X}" : null,
            CompareTime = DateTime.UtcNow - startTime
        };
    }
    
    public async Task<FilePreviewData> GetFilePreviewAsync(
        string filePath,
        OverwritePreviewOptions options,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);
        var previewType = DeterminePreviewType(extension);
        
        var preview = new FilePreviewData
        {
            FilePath = filePath,
            Type = previewType,
            TypeDescription = GetTypeDescription(extension)
        };
        
        try
        {
            switch (previewType)
            {
                case FilePreviewType.Image:
                    preview = preview with
                    {
                        ThumbnailData = await GenerateImageThumbnailAsync(filePath, options, cancellationToken)
                    };
                    break;
                    
                case FilePreviewType.Text:
                    preview = preview with
                    {
                        TextPreview = await GetTextPreviewAsync(filePath, options.MaxTextLength, cancellationToken)
                    };
                    break;
                    
                case FilePreviewType.Video:
                case FilePreviewType.Audio:
                    // Try to get metadata
                    preview = preview with
                    {
                        Metadata = await GetMediaMetadataAsync(filePath, cancellationToken)
                    };
                    break;
            }
        }
        catch
        {
            // Return basic preview on error
        }
        
        return preview;
    }
    
    /// <summary>
    /// Creates conflict info from source and target paths.
    /// </summary>
    public static async Task<FileConflictInfo> CreateConflictInfoAsync(
        string sourcePath,
        string targetPath,
        bool calculateChecksum = false,
        CancellationToken cancellationToken = default)
    {
        var sourceInfo = new FileInfo(sourcePath);
        var targetInfo = new FileInfo(targetPath);
        
        string? sourceChecksum = null;
        string? targetChecksum = null;
        
        if (calculateChecksum && sourceInfo.Exists && targetInfo.Exists)
        {
            sourceChecksum = await CalculateChecksumAsync(sourcePath, cancellationToken);
            targetChecksum = await CalculateChecksumAsync(targetPath, cancellationToken);
        }
        
        return new FileConflictInfo
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            SourceSize = sourceInfo.Exists ? sourceInfo.Length : 0,
            TargetSize = targetInfo.Exists ? targetInfo.Length : 0,
            SourceModified = sourceInfo.Exists ? sourceInfo.LastWriteTimeUtc : DateTime.MinValue,
            TargetModified = targetInfo.Exists ? targetInfo.LastWriteTimeUtc : DateTime.MinValue,
            SourceChecksum = sourceChecksum,
            TargetChecksum = targetChecksum,
            SourceAttributes = sourceInfo.Exists ? sourceInfo.Attributes : 0,
            TargetAttributes = targetInfo.Exists ? targetInfo.Attributes : 0
        };
    }
    
    /// <summary>
    /// Generates an auto-rename suggestion based on the pattern.
    /// </summary>
    public static string GenerateAutoRename(string originalPath, string pattern, int counter = 1)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(originalPath);
        var ext = Path.GetExtension(originalPath);
        
        var newName = pattern
            .Replace("{name}", name)
            .Replace("{counter}", counter.ToString())
            .Replace("{counter:2}", counter.ToString("D2"))
            .Replace("{counter:3}", counter.ToString("D3"))
            .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"))
            .Replace("{time}", DateTime.Now.ToString("HHmmss"))
            .Replace("{ext}", ext);
        
        // Ensure we have extension if pattern didn't include it
        if (!newName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            newName += ext;
        }
        
        var newPath = Path.Combine(directory, newName);
        
        // If file exists, increment counter and try again
        if (File.Exists(newPath) && counter < 1000)
        {
            return GenerateAutoRename(originalPath, pattern, counter + 1);
        }
        
        return newPath;
    }
    
    private async Task<FileConflictInfo> EnrichConflictInfoAsync(
        FileConflictInfo conflict,
        OverwriteDialogOptions options,
        CancellationToken cancellationToken)
    {
        var customFields = new Dictionary<string, string>();
        
        if (options.ShowCustomFields && _contentPluginService != null)
        {
            foreach (var fieldName in options.CustomFieldsToShow)
            {
                try
                {
                    var sourceValue = await _contentPluginService.GetFieldValueAsync(
                        conflict.SourcePath, fieldName, cancellationToken);
                    if (sourceValue != null && !sourceValue.IsEmpty)
                    {
                        customFields[$"Source_{fieldName}"] = sourceValue.DisplayValue ?? sourceValue.Value?.ToString() ?? "";
                    }
                    
                    var targetValue = await _contentPluginService.GetFieldValueAsync(
                        conflict.TargetPath, fieldName, cancellationToken);
                    if (targetValue != null && !targetValue.IsEmpty)
                    {
                        customFields[$"Target_{fieldName}"] = targetValue.DisplayValue ?? targetValue.Value?.ToString() ?? "";
                    }
                }
                catch
                {
                    // Ignore field errors
                }
            }
        }
        
        return conflict with
        {
            SourceCustomFields = customFields.Any() ? customFields : null
        };
    }
    
    private static bool ShouldApplyDecision(FileConflictInfo conflict, OverwriteDecision decision)
    {
        if (decision.Condition == null || decision.Condition == OverwriteCondition.All)
            return true;
            
        return decision.Condition switch
        {
            OverwriteCondition.NewerOnly => conflict.IsSourceNewer,
            OverwriteCondition.OlderOnly => !conflict.IsSourceNewer,
            OverwriteCondition.LargerOnly => conflict.SourceSize > conflict.TargetSize,
            OverwriteCondition.SmallerOnly => conflict.SourceSize < conflict.TargetSize,
            OverwriteCondition.DifferentOnly => conflict.AreIdentical != true,
            OverwriteCondition.IdenticalOnly => conflict.AreIdentical == true,
            _ => true
        };
    }
    
    private static double CalculateSimilarity(long size1, long size2)
    {
        if (size1 == 0 && size2 == 0) return 100;
        var larger = Math.Max(size1, size2);
        var smaller = Math.Min(size1, size2);
        return (double)smaller / larger * 100;
    }
    
    private static FilePreviewType DeterminePreviewType(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return FilePreviewType.Unknown;
        
        if (ImageExtensions.Contains(extension)) return FilePreviewType.Image;
        if (TextExtensions.Contains(extension)) return FilePreviewType.Text;
        
        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => FilePreviewType.Video,
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" or ".wma" => FilePreviewType.Audio,
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => FilePreviewType.Document,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => FilePreviewType.Archive,
            _ => FilePreviewType.Unknown
        };
    }
    
    private static string GetTypeDescription(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return "Unknown";
        
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "JPEG Image",
            ".png" => "PNG Image",
            ".gif" => "GIF Image",
            ".bmp" => "Bitmap Image",
            ".webp" => "WebP Image",
            ".txt" => "Text File",
            ".md" => "Markdown Document",
            ".json" => "JSON File",
            ".xml" => "XML Document",
            ".html" or ".htm" => "HTML Document",
            ".css" => "CSS Stylesheet",
            ".js" => "JavaScript File",
            ".ts" => "TypeScript File",
            ".cs" => "C# Source File",
            ".mp4" => "MP4 Video",
            ".avi" => "AVI Video",
            ".mp3" => "MP3 Audio",
            ".pdf" => "PDF Document",
            ".doc" or ".docx" => "Word Document",
            ".xls" or ".xlsx" => "Excel Spreadsheet",
            ".zip" => "ZIP Archive",
            ".rar" => "RAR Archive",
            ".7z" => "7-Zip Archive",
            _ => $"{extension.TrimStart('.').ToUpperInvariant()} File"
        };
    }
    
    private static async Task<byte[]?> GenerateImageThumbnailAsync(
        string filePath,
        OverwritePreviewOptions options,
        CancellationToken cancellationToken)
    {
        // In a real implementation, use SkiaSharp or similar to generate thumbnail
        // For now, return null
        await Task.CompletedTask;
        return null;
    }
    
    private static async Task<string> GetTextPreviewAsync(
        string filePath,
        int maxLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(filePath, cancellationToken);
            return text.Length > maxLength ? text[..maxLength] + "..." : text;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private static async Task<Dictionary<string, string>> GetMediaMetadataAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        // In a real implementation, use TagLib or similar to extract metadata
        await Task.CompletedTask;
        return new Dictionary<string, string>
        {
            ["Type"] = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()
        };
    }
    
    private static async Task<string> CalculateChecksumAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        var hash = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
