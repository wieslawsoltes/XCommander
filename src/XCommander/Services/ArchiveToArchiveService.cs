using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace XCommander.Services;

/// <summary>
/// Implementation of IArchiveToArchiveService for direct archive copy.
/// </summary>
public class ArchiveToArchiveService : IArchiveToArchiveService
{
    private static readonly HashSet<string> SupportedSourceExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tar.gz", ".tgz", ".tar.bz2"
    };
    
    private static readonly HashSet<string> SupportedDestExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip" // Only ZIP supports modification currently
    };

    public event EventHandler<ArchiveCopyResult>? OperationCompleted;

    public async Task<ArchiveCopyResult> CopyEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        IEnumerable<string> entryKeys,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ArchiveCopyOptions();
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var entryKeySet = new HashSet<string>(entryKeys, StringComparer.OrdinalIgnoreCase);
        
        int entriesCopied = 0;
        int entriesSkipped = 0;
        long bytesCopied = 0;
        bool usedDirectCopy = false;

        try
        {
            // Read source entries
            var sourceEntries = new List<(IArchiveEntry Entry, byte[] Data)>();
            
            await Task.Run(() =>
            {
                using var sourceArch = ArchiveFactory.Open(sourceArchive);
                var entriesToCopy = sourceArch.Entries
                    .Where(e => !e.IsDirectory && entryKeySet.Contains(NormalizeKey(e.Key)))
                    .ToList();

                var totalEntries = entriesToCopy.Count;
                var processed = 0;

                foreach (var entry in entriesToCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    progress?.Report(new ArchiveCopyProgress
                    {
                        CurrentEntry = entry.Key ?? string.Empty,
                        TotalEntries = totalEntries,
                        ProcessedEntries = processed,
                        Phase = "Reading source"
                    });

                    try
                    {
                        using var stream = entry.OpenEntryStream();
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        sourceEntries.Add((entry, ms.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to read {entry.Key}: {ex.Message}");
                    }

                    processed++;
                }
            }, cancellationToken);

            // Write to destination
            await Task.Run(() =>
            {
                var totalEntries = sourceEntries.Count;
                var processed = 0;

                // Check if destination exists
                if (File.Exists(destinationArchive))
                {
                    // Open existing and add entries
                    using var destArch = ZipArchive.Open(destinationArchive);
                    var existingKeys = new HashSet<string>(
                        destArch.Entries.Select(e => NormalizeKey(e.Key)),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var (entry, data) in sourceEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var destKey = GetDestinationKey(entry.Key, options);
                        
                        progress?.Report(new ArchiveCopyProgress
                        {
                            CurrentEntry = destKey,
                            TotalEntries = totalEntries,
                            ProcessedEntries = processed,
                            Phase = "Writing to destination"
                        });

                        // Check if exists
                        if (existingKeys.Contains(NormalizeKey(destKey)))
                        {
                            if (options.SkipIdentical)
                            {
                                var existing = destArch.Entries.FirstOrDefault(e => 
                                    NormalizeKey(e.Key).Equals(NormalizeKey(destKey), StringComparison.OrdinalIgnoreCase));
                                    
                                if (existing != null && existing.Size == data.Length)
                                {
                                    entriesSkipped++;
                                    processed++;
                                    continue;
                                }
                            }

                            if (!options.OverwriteExisting)
                            {
                                entriesSkipped++;
                                processed++;
                                continue;
                            }

                            // Remove existing entry
                            var toRemove = destArch.Entries.FirstOrDefault(e => 
                                NormalizeKey(e.Key).Equals(NormalizeKey(destKey), StringComparison.OrdinalIgnoreCase));
                            if (toRemove != null)
                            {
                                destArch.RemoveEntry(toRemove);
                            }
                        }

                        // Add new entry
                        using var ms = new MemoryStream(data);
                        destArch.AddEntry(destKey, ms, true, data.Length, entry.LastModifiedTime);
                        
                        entriesCopied++;
                        bytesCopied += data.Length;
                        processed++;
                    }

                    // Save changes
                    destArch.SaveTo(destinationArchive + ".tmp", CompressionType.Deflate);
                    
                    // Replace original
                    File.Delete(destinationArchive);
                    File.Move(destinationArchive + ".tmp", destinationArchive);
                }
                else
                {
                    // Create new archive
                    using var destArch = ZipArchive.Create();
                    
                    foreach (var (entry, data) in sourceEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var destKey = GetDestinationKey(entry.Key, options);
                        
                        progress?.Report(new ArchiveCopyProgress
                        {
                            CurrentEntry = destKey,
                            TotalEntries = totalEntries,
                            ProcessedEntries = processed,
                            Phase = "Creating destination archive"
                        });

                        using var ms = new MemoryStream(data);
                        destArch.AddEntry(destKey, ms, true, data.Length, entry.LastModifiedTime);
                        
                        entriesCopied++;
                        bytesCopied += data.Length;
                        processed++;
                    }

                    destArch.SaveTo(destinationArchive, CompressionType.Deflate);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add($"Operation failed: {ex.Message}");
        }
        finally
        {
            // Cleanup temp file
            try
            {
                if (File.Exists(destinationArchive + ".tmp"))
                    File.Delete(destinationArchive + ".tmp");
            }
            catch { }
        }

        sw.Stop();

        var result = new ArchiveCopyResult
        {
            Success = errors.Count == 0,
            EntriesCopied = entriesCopied,
            EntriesSkipped = entriesSkipped,
            BytesCopied = bytesCopied,
            Duration = sw.Elapsed,
            Errors = errors,
            UsedDirectCopy = usedDirectCopy
        };

        OperationCompleted?.Invoke(this, result);
        return result;
    }

    public async Task<ArchiveCopyResult> CopyAllEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await ListEntriesAsync(sourceArchive, cancellationToken);
        var allKeys = entries.Where(e => !e.IsDirectory).Select(e => e.Key);
        
        return await CopyEntriesAsync(
            sourceArchive, 
            destinationArchive, 
            allKeys, 
            options, 
            progress, 
            cancellationToken);
    }

    public async Task<ArchiveCopyResult> MoveEntriesAsync(
        string sourceArchive,
        string destinationArchive,
        IEnumerable<string> entryKeys,
        ArchiveCopyOptions? options = null,
        IProgress<ArchiveCopyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // First copy
        var copyResult = await CopyEntriesAsync(
            sourceArchive, 
            destinationArchive, 
            entryKeys, 
            options, 
            progress, 
            cancellationToken);

        if (!copyResult.Success)
        {
            return copyResult;
        }

        // Then delete from source if ZIP
        var ext = Path.GetExtension(sourceArchive);
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var entryKeySet = new HashSet<string>(entryKeys, StringComparer.OrdinalIgnoreCase);
            
            await Task.Run(() =>
            {
                using var sourceArch = ZipArchive.Open(sourceArchive);
                var toRemove = sourceArch.Entries
                    .Where(e => entryKeySet.Contains(NormalizeKey(e.Key)))
                    .ToList();

                foreach (var entry in toRemove)
                {
                    sourceArch.RemoveEntry(entry);
                }

                sourceArch.SaveTo(sourceArchive + ".tmp", CompressionType.Deflate);
                
                File.Delete(sourceArchive);
                File.Move(sourceArchive + ".tmp", sourceArchive);
            }, cancellationToken);
        }

        return copyResult;
    }

    public async Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<ArchiveEntryInfo>();

        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                entries.Add(new ArchiveEntryInfo
                {
                    Key = entry.Key ?? string.Empty,
                    IsDirectory = entry.IsDirectory,
                    Size = entry.Size,
                    CompressedSize = entry.CompressedSize,
                    LastModified = entry.LastModifiedTime,
                    Crc32 = (uint?)entry.Crc
                });
            }
        }, cancellationToken);

        return entries;
    }

    public bool CanDirectCopy(string sourceArchive, string destinationArchive)
    {
        var sourceExt = Path.GetExtension(sourceArchive);
        var destExt = Path.GetExtension(destinationArchive);
        
        // Direct copy only possible for same format
        return sourceExt.Equals(destExt, StringComparison.OrdinalIgnoreCase) &&
               sourceExt.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetSupportedSourceExtensions()
    {
        return SupportedSourceExts.ToList();
    }

    public IReadOnlyList<string> GetSupportedDestinationExtensions()
    {
        return SupportedDestExts.ToList();
    }

    private static string NormalizeKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;
            
        return key.Replace('\\', '/').TrimStart('/');
    }

    private static string GetDestinationKey(string? sourceKey, ArchiveCopyOptions options)
    {
        var key = NormalizeKey(sourceKey);
        
        // Strip source prefix
        if (!string.IsNullOrEmpty(options.SourcePrefixToStrip))
        {
            var prefix = NormalizeKey(options.SourcePrefixToStrip);
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                key = key[prefix.Length..].TrimStart('/');
            }
        }
        
        // Add destination prefix
        if (!string.IsNullOrEmpty(options.DestinationPrefix))
        {
            var prefix = NormalizeKey(options.DestinationPrefix).TrimEnd('/');
            key = $"{prefix}/{key}";
        }
        
        return key;
    }
}
