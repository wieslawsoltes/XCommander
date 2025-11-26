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

            using var archive = ArchiveFactory.Open(archivePath);
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
            using var archive = ArchiveFactory.Open(archivePath);
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
            using var archive = ArchiveFactory.Open(archivePath);
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

            var writerOptions = GetWriterOptions(type, compressionLevel);
            var archiveType = GetSharpCompressArchiveType(type);

            using var stream = File.Create(archivePath);
            using var writer = WriterFactory.Open(stream, archiveType, writerOptions);

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
                using var archive = ZipArchive.Open(archivePath);
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

                archive.SaveTo(archivePath + ".tmp", new WriterOptions(CompressionType.Deflate));
                File.Delete(archivePath);
                File.Move(archivePath + ".tmp", archivePath);
            }, cancellationToken);
        }
        else
        {
            throw new NotSupportedException("Adding to non-ZIP archives is not supported");
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
                using var archive = ZipArchive.Open(archivePath);
                var entriesToRemove = archive.Entries
                    .Where(e => entrySet.Contains(e.Key ?? string.Empty))
                    .ToList();

                foreach (var entry in entriesToRemove)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.RemoveEntry(entry);
                }

                archive.SaveTo(archivePath + ".tmp", new WriterOptions(CompressionType.Deflate));
                File.Delete(archivePath);
                File.Move(archivePath + ".tmp", archivePath);
            }, cancellationToken);
        }
        else
        {
            throw new NotSupportedException("Deleting entries from non-ZIP archives is not supported");
        }
    }

    public async Task<bool> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ArchiveFactory.Open(archivePath);
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
            _ => SharpCompress.Common.ArchiveType.Zip
        };
    }
}
