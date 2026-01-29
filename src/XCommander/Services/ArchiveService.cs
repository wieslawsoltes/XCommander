using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace XCommander.Services;

public class ArchiveService : IArchiveService
{
    private static readonly string[] SupportedArchiveExtensions = 
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".tar.gz", 
        ".bz2", ".tbz2", ".tar.bz2", ".xz", ".txz", ".tar.xz"
    };

    public IReadOnlyList<string> SupportedExtensions => SupportedArchiveExtensions;

    public bool IsArchive(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var extension = Path.GetExtension(path).ToLowerInvariant();
        
        // Handle double extensions like .tar.gz
        var fileName = Path.GetFileName(path).ToLowerInvariant();
        if (fileName.EndsWith(".tar.gz") || fileName.EndsWith(".tar.bz2") || fileName.EndsWith(".tar.xz"))
            return true;

        return SupportedArchiveExtensions.Contains(extension);
    }

    public async Task<List<ArchiveEntry>> ListEntriesAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var entries = new List<ArchiveEntry>();

            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entries.Add(new ArchiveEntry
                {
                    Key = entry.Key ?? string.Empty,
                    Name = Path.GetFileName(entry.Key?.TrimEnd('/') ?? string.Empty),
                    Path = entry.Key ?? string.Empty,
                    IsDirectory = entry.IsDirectory,
                    Size = entry.Size,
                    CompressedSize = entry.CompressedSize,
                    LastModified = entry.LastModifiedTime,
                    IsEncrypted = entry.IsEncrypted,
                    Crc = (uint?)entry.Crc
                });
            }

            return entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Path).ToList();
        }, cancellationToken);
    }

    public async Task ExtractAllAsync(string archivePath, string destinationPath,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            var totalEntries = entries.Count;
            var totalBytes = entries.Sum(e => e.Size);
            var processedEntries = 0;
            var processedBytes = 0L;

            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!reader.Entry.IsDirectory)
                {
                    reader.WriteEntryToDirectory(destinationPath, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });

                    processedEntries++;
                    processedBytes += reader.Entry.Size;

                    progress?.Report(new ArchiveProgress
                    {
                        CurrentEntry = reader.Entry.Key ?? string.Empty,
                        EntriesProcessed = processedEntries,
                        TotalEntries = totalEntries,
                        BytesProcessed = processedBytes,
                        TotalBytes = totalBytes
                    });
                }
            }
        }, cancellationToken);
    }

    public async Task ExtractEntriesAsync(string archivePath, IEnumerable<string> entryPaths, string destinationPath,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var entrySet = new HashSet<string>(entryPaths, StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            var entries = archive.Entries
                .Where(e => !e.IsDirectory && entrySet.Contains(e.Key ?? string.Empty))
                .ToList();

            var totalEntries = entries.Count;
            var totalBytes = entries.Sum(e => e.Size);
            var processedEntries = 0;
            var processedBytes = 0L;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entry.WriteToDirectory(destinationPath, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                processedEntries++;
                processedBytes += entry.Size;

                progress?.Report(new ArchiveProgress
                {
                    CurrentEntry = entry.Key ?? string.Empty,
                    EntriesProcessed = processedEntries,
                    TotalEntries = totalEntries,
                    BytesProcessed = processedBytes,
                    TotalBytes = totalBytes
                });
            }
        }, cancellationToken);
    }

    public async Task CreateArchiveAsync(string archivePath, IEnumerable<string> sourcePaths, ArchiveType type,
        CompressionLevel compressionLevel = CompressionLevel.Normal,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var files = CollectFiles(sourcePaths).ToList();
            var totalFiles = files.Count;
            var totalBytes = files.Sum(f => new FileInfo(f.FullPath).Length);
            var processedFiles = 0;
            var processedBytes = 0L;

            var (archiveType, writerOptions) = GetWriterProfile(archivePath, type, compressionLevel);

            using var stream = File.Create(archivePath);
            using var writer = WriterFactory.OpenWriter(stream, archiveType, writerOptions);

            foreach (var (fullPath, relativePath) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(fullPath);
                writer.Write(relativePath, fullPath);

                processedFiles++;
                processedBytes += fileInfo.Length;

                progress?.Report(new ArchiveProgress
                {
                    CurrentEntry = relativePath,
                    EntriesProcessed = processedFiles,
                    TotalEntries = totalFiles,
                    BytesProcessed = processedBytes,
                    TotalBytes = totalBytes
                });
            }
        }, cancellationToken);
    }

    public async Task AddToArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        // For ZIP files, we can add to existing archive
        if (Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() =>
            {
                using var archive = (ZipArchive)ZipArchive.OpenArchive(archivePath, new ReaderOptions());
                var files = CollectFiles(sourcePaths).ToList();
                var totalFiles = files.Count;
                var processedFiles = 0;

                foreach (var (fullPath, relativePath) in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var fileStream = File.OpenRead(fullPath);
                    archive.AddEntry(relativePath, fileStream);
                    processedFiles++;

                    progress?.Report(new ArchiveProgress
                    {
                        CurrentEntry = relativePath,
                        EntriesProcessed = processedFiles,
                        TotalEntries = totalFiles
                    });
                }

                var tempPath = archivePath + ".tmp";
                using (var tempStream = File.Create(tempPath))
                {
                    archive.SaveTo(tempStream, new WriterOptions(CompressionType.Deflate));
                }
                File.Delete(archivePath);
                File.Move(tempPath, archivePath);
            }, cancellationToken);
        }
        else
        {
            if (!File.Exists(archivePath))
            {
                var type = DetermineArchiveType(archivePath);
                await CreateArchiveAsync(archivePath, sourcePaths, type, CompressionLevel.Normal, progress, cancellationToken);
                return;
            }

            await Task.Run(() =>
            {
                var addMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (fullPath, relativePath) in CollectFiles(sourcePaths))
                {
                    var key = NormalizeEntryKey(relativePath);
                    if (string.IsNullOrWhiteSpace(key))
                        key = NormalizeEntryKey(Path.GetFileName(fullPath));
                    if (!string.IsNullOrWhiteSpace(key))
                        addMap[key] = fullPath;
                }

                var addKeys = new HashSet<string>(addMap.Keys, StringComparer.OrdinalIgnoreCase);
                var filesToAdd = addMap.Select(kvp => (FullPath: kvp.Value, RelativePath: kvp.Key)).ToList();
                RebuildArchive(
                    archivePath,
                    filesToAdd,
                    entryKey => !addKeys.Contains(entryKey),
                    progress,
                    cancellationToken);
            }, cancellationToken);
        }
    }

    public async Task DeleteEntriesAsync(string archivePath, IEnumerable<string> entryPaths,
        CancellationToken cancellationToken = default)
    {
        var entrySet = new HashSet<string>(entryPaths, StringComparer.OrdinalIgnoreCase);

        if (Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() =>
            {
                using var archive = (ZipArchive)ZipArchive.OpenArchive(archivePath, new ReaderOptions());
                var entriesToRemove = archive.Entries
                    .Where(e => entrySet.Contains(e.Key ?? string.Empty))
                    .ToList();

                foreach (var entry in entriesToRemove)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.RemoveEntry(entry);
                }

                var tempPath = archivePath + ".tmp";
                using (var tempStream = File.Create(tempPath))
                {
                    archive.SaveTo(tempStream, new WriterOptions(CompressionType.Deflate));
                }
                File.Delete(archivePath);
                File.Move(tempPath, archivePath);
            }, cancellationToken);
        }
        else
        {
            await Task.Run(() =>
            {
                var normalized = entrySet
                    .Select(NormalizeEntryKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (normalized.Count == 0)
                    return;

                RebuildArchive(
                    archivePath,
                    Array.Empty<(string FullPath, string RelativePath)>(),
                    entryKey => !normalized.Contains(entryKey),
                    progress: null,
                    cancellationToken);
            }, cancellationToken);
        }
    }

    public async Task<bool> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Try to read each entry to verify integrity
                    using var entryStream = entry.OpenEntryStream();
                    var buffer = new byte[8192];
                    while (entryStream.Read(buffer, 0, buffer.Length) > 0)
                    {
                        // Just reading to verify
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private IEnumerable<(string FullPath, string RelativePath)> CollectFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                yield return (path, Path.GetFileName(path));
            }
            else if (Directory.Exists(path))
            {
                var basePath = Path.GetDirectoryName(path) ?? path;
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(basePath, file);
                    yield return (file, relativePath);
                }
            }
        }
    }

    private void RebuildArchive(string archivePath,
        IReadOnlyList<(string FullPath, string RelativePath)> filesToAdd,
        Func<string, bool> shouldKeepEntry,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = archivePath + ".tmp";
        try
        {
            var type = DetermineArchiveType(archivePath);
            var (archiveType, writerOptions) = GetWriterProfile(archivePath, type, CompressionLevel.Normal);

            using var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            var keptEntries = entries
                .Select(e => (Entry: e, Key: NormalizeEntryKey(e.Key)))
                .Where(e => shouldKeepEntry(e.Key))
                .ToList();

            var totalEntries = keptEntries.Count + filesToAdd.Count;
            var totalBytes = keptEntries.Sum(e => e.Entry.Size) +
                filesToAdd.Sum(f => new FileInfo(f.FullPath).Length);

            var processedEntries = 0;
            var processedBytes = 0L;

            using var stream = File.Create(tempPath);
            using var writer = WriterFactory.OpenWriter(stream, archiveType, writerOptions);

            foreach (var entry in keptEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var entryStream = entry.Entry.OpenEntryStream();

                var writeKey = string.IsNullOrWhiteSpace(entry.Key)
                    ? $"entry_{processedEntries + 1}"
                    : entry.Key;
                writer.Write(writeKey, entryStream);

                processedEntries++;
                processedBytes += entry.Entry.Size;
                progress?.Report(new ArchiveProgress
                {
                    CurrentEntry = writeKey,
                    EntriesProcessed = processedEntries,
                    TotalEntries = totalEntries,
                    BytesProcessed = processedBytes,
                    TotalBytes = totalBytes
                });
            }

            foreach (var (fullPath, relativePath) in filesToAdd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var writeKey = NormalizeEntryKey(relativePath);
                if (string.IsNullOrWhiteSpace(writeKey))
                    writeKey = NormalizeEntryKey(Path.GetFileName(fullPath));
                if (string.IsNullOrWhiteSpace(writeKey))
                    writeKey = $"entry_{processedEntries + 1}";

                using var fileStream = File.OpenRead(fullPath);
                writer.Write(writeKey, fileStream);

                processedEntries++;
                processedBytes += fileStream.Length;
                progress?.Report(new ArchiveProgress
                {
                    CurrentEntry = writeKey,
                    EntriesProcessed = processedEntries,
                    TotalEntries = totalEntries,
                    BytesProcessed = processedBytes,
                    TotalBytes = totalBytes
                });
            }
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }

        if (File.Exists(archivePath))
            File.Delete(archivePath);
        File.Move(tempPath, archivePath);
    }

    private static string NormalizeEntryKey(string? path)
    {
        return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }

    private static ArchiveType DetermineArchiveType(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath).ToLowerInvariant();
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            return ArchiveType.GZip;
        if (fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
            return ArchiveType.BZip2;
        if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase))
            return ArchiveType.Tar;

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".zip" => ArchiveType.Zip,
            ".7z" => ArchiveType.SevenZip,
            ".rar" => ArchiveType.Rar,
            ".tar" => ArchiveType.Tar,
            ".gz" => ArchiveType.GZip,
            ".bz2" => ArchiveType.BZip2,
            _ => ArchiveType.Zip
        };
    }

    private (SharpCompress.Common.ArchiveType ArchiveType, WriterOptions WriterOptions) GetWriterProfile(
        string archivePath,
        ArchiveType type,
        CompressionLevel level)
    {
        var fileName = Path.GetFileName(archivePath).ToLowerInvariant();
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            return (SharpCompress.Common.ArchiveType.Tar, GetWriterOptions(ArchiveType.GZip, level));
        if (fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
            return (SharpCompress.Common.ArchiveType.Tar, GetWriterOptions(ArchiveType.BZip2, level));
        if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".txz", StringComparison.OrdinalIgnoreCase))
            return (SharpCompress.Common.ArchiveType.Tar, new WriterOptions(CompressionType.Xz));

        if (type is ArchiveType.Rar or ArchiveType.SevenZip)
            throw new NotSupportedException($"Writing {type} archives is not supported");

        return (GetSharpCompressArchiveType(type), GetWriterOptions(type, level));
    }

    private WriterOptions GetWriterOptions(ArchiveType type, CompressionLevel level)
    {
        var compressionType = type switch
        {
            ArchiveType.Zip => CompressionType.Deflate,
            ArchiveType.GZip => CompressionType.GZip,
            ArchiveType.BZip2 => CompressionType.BZip2,
            ArchiveType.Tar => CompressionType.None,
            _ => CompressionType.Deflate
        };

        if (type == ArchiveType.Zip)
        {
            var zipLevel = level switch
            {
                CompressionLevel.None => SharpCompress.Compressors.Deflate.CompressionLevel.None,
                CompressionLevel.Fastest => SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed,
                CompressionLevel.Fast => SharpCompress.Compressors.Deflate.CompressionLevel.Level3,
                CompressionLevel.Normal => SharpCompress.Compressors.Deflate.CompressionLevel.Default,
                CompressionLevel.Maximum => SharpCompress.Compressors.Deflate.CompressionLevel.Level7,
                CompressionLevel.Ultra => SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression,
                _ => SharpCompress.Compressors.Deflate.CompressionLevel.Default
            };

            return new ZipWriterOptions(compressionType)
            {
                DeflateCompressionLevel = zipLevel
            };
        }

        return new WriterOptions(compressionType);
    }

    private SharpCompress.Common.ArchiveType GetSharpCompressArchiveType(ArchiveType type)
    {
        return type switch
        {
            ArchiveType.Zip => SharpCompress.Common.ArchiveType.Zip,
            ArchiveType.SevenZip => SharpCompress.Common.ArchiveType.SevenZip,
            ArchiveType.Tar => SharpCompress.Common.ArchiveType.Tar,
            ArchiveType.GZip => SharpCompress.Common.ArchiveType.GZip,
            ArchiveType.BZip2 => SharpCompress.Common.ArchiveType.Tar,
            ArchiveType.Rar => SharpCompress.Common.ArchiveType.Rar,
            _ => SharpCompress.Common.ArchiveType.Zip
        };
    }
}
