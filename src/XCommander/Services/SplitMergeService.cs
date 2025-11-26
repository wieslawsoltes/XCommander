// SplitMergeService.cs - TC-style file split and merge implementation
// Full split/merge with CRC verification, batch file generation

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public sealed class SplitMergeService : ISplitMergeService
{
    private const int BufferSize = 81920; // 80KB buffer
    private readonly ILongPathService _longPathService;
    
    private static readonly Dictionary<string, long> _presetSizes = new()
    {
        ["360K Floppy"] = SplitOptions.Presets.Floppy360K,
        ["720K Floppy"] = SplitOptions.Presets.Floppy720K,
        ["1.44M Floppy"] = SplitOptions.Presets.Floppy144M,
        ["100M ZIP"] = SplitOptions.Presets.Zip100M,
        ["250M ZIP"] = SplitOptions.Presets.Zip250M,
        ["650M CD"] = SplitOptions.Presets.CD650M,
        ["700M CD"] = SplitOptions.Presets.CD700M,
        ["4.7G DVD"] = SplitOptions.Presets.DVD47G,
        ["8.5G DVD DL"] = SplitOptions.Presets.DVD85G,
        ["25G Blu-ray"] = SplitOptions.Presets.BluRay25G,
        ["50G Blu-ray DL"] = SplitOptions.Presets.BluRay50G,
        ["1G USB"] = SplitOptions.Presets.USB1G,
        ["2G USB"] = SplitOptions.Presets.USB2G,
        ["4G USB"] = SplitOptions.Presets.USB4G,
        ["8G USB"] = SplitOptions.Presets.USB8G
    };
    
    public SplitMergeService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
    }
    
    public IReadOnlyDictionary<string, long> GetPresetSizes() => _presetSizes;
    
    public int CalculatePartCount(long fileSize, long partSize)
    {
        if (partSize <= 0) throw new ArgumentException("Part size must be positive", nameof(partSize));
        return (int)Math.Ceiling((double)fileSize / partSize);
    }
    
    public long CalculateOptimalPartSize(long fileSize, int maxParts)
    {
        if (maxParts <= 0) throw new ArgumentException("Max parts must be positive", nameof(maxParts));
        return (long)Math.Ceiling((double)fileSize / maxParts);
    }
    
    public async Task<SplitResult> SplitFileAsync(
        string sourceFile,
        SplitOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var parts = new List<SplitPart>();
        
        try
        {
            var longSourcePath = _longPathService.NormalizePath(sourceFile);
            var fileInfo = new FileInfo(longSourcePath);
            
            if (!fileInfo.Exists)
            {
                return new SplitResult
                {
                    OriginalFile = sourceFile,
                    Success = false,
                    ErrorMessage = "Source file not found"
                };
            }
            
            var originalSize = fileInfo.Length;
            var targetDir = options.TargetDirectory ?? Path.GetDirectoryName(sourceFile) ?? ".";
            Directory.CreateDirectory(_longPathService.NormalizePath(targetDir));
            
            var totalParts = CalculatePartCount(originalSize, options.PartSize);
            var baseName = Path.GetFileNameWithoutExtension(sourceFile);
            var originalExt = Path.GetExtension(sourceFile);
            
            // Calculate original file checksum
            string? originalChecksum = null;
            if (options.CreateChecksumFile || options.VerifyAfterSplit)
            {
                progress?.Report(new SplitMergeProgress
                {
                    CurrentOperation = "Calculating checksum...",
                    TotalParts = totalParts,
                    TotalBytes = originalSize
                });
                
                originalChecksum = await CalculateCrc32Async(longSourcePath, cancellationToken);
            }
            
            // Split the file
            await using var sourceStream = new FileStream(
                longSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
            
            var buffer = new byte[BufferSize];
            long totalBytesProcessed = 0;
            
            for (int partNum = options.StartNumber; partNum < options.StartNumber + totalParts; partNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var actualPartNum = partNum - options.StartNumber;
                var partFileName = GetPartFileName(baseName, originalExt, partNum, totalParts, options.NamingPattern);
                var partPath = Path.Combine(targetDir, partFileName);
                var longPartPath = _longPathService.NormalizePath(partPath);
                
                var startOffset = totalBytesProcessed;
                var partSize = Math.Min(options.PartSize, originalSize - totalBytesProcessed);
                
                await using var partStream = new FileStream(
                    longPartPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
                
                long partBytesWritten = 0;
                while (partBytesWritten < partSize)
                {
                    var toRead = (int)Math.Min(buffer.Length, partSize - partBytesWritten);
                    var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                    
                    if (bytesRead == 0) break;
                    
                    await partStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    partBytesWritten += bytesRead;
                    totalBytesProcessed += bytesRead;
                    
                    var elapsed = stopwatch.Elapsed;
                    var speed = elapsed.TotalSeconds > 0 ? totalBytesProcessed / elapsed.TotalSeconds : 0;
                    var remaining = speed > 0 
                        ? TimeSpan.FromSeconds((originalSize - totalBytesProcessed) / speed) 
                        : (TimeSpan?)null;
                    
                    progress?.Report(new SplitMergeProgress
                    {
                        CurrentOperation = $"Splitting part {partNum}/{totalParts + options.StartNumber - 1}",
                        CurrentPart = partNum,
                        TotalParts = totalParts + options.StartNumber - 1,
                        BytesProcessed = totalBytesProcessed,
                        TotalBytes = originalSize,
                        Elapsed = elapsed,
                        EstimatedRemaining = remaining,
                        SpeedBytesPerSecond = speed
                    });
                }
                
                // Calculate part checksum
                string? partChecksum = null;
                if (options.CreateChecksumFile)
                {
                    partChecksum = await CalculateCrc32Async(longPartPath, cancellationToken);
                }
                
                parts.Add(new SplitPart
                {
                    FilePath = partPath,
                    PartNumber = partNum,
                    Size = partBytesWritten,
                    StartOffset = startOffset,
                    EndOffset = startOffset + partBytesWritten,
                    Checksum = partChecksum
                });
            }
            
            // Create checksum file
            string? checksumFilePath = null;
            if (options.CreateChecksumFile)
            {
                checksumFilePath = Path.Combine(targetDir, baseName + ".crc");
                await CreateChecksumFileAsync(checksumFilePath, sourceFile, originalSize, 
                    originalChecksum!, parts, cancellationToken);
            }
            
            // Create batch file for Windows
            string? batchFilePath = null;
            if (options.CreateBatchFile)
            {
                batchFilePath = Path.Combine(targetDir, baseName + "_merge.bat");
                await CreateBatchFileAsync(batchFilePath, baseName + originalExt, parts, cancellationToken);
            }
            
            // Create shell script for Unix
            string? shellScriptPath = null;
            if (options.CreateShellScript)
            {
                shellScriptPath = Path.Combine(targetDir, baseName + "_merge.sh");
                await CreateShellScriptAsync(shellScriptPath, baseName + originalExt, parts, cancellationToken);
            }
            
            // Verify if requested
            if (options.VerifyAfterSplit && checksumFilePath != null)
            {
                progress?.Report(new SplitMergeProgress
                {
                    CurrentOperation = "Verifying split files...",
                    TotalParts = totalParts,
                    TotalBytes = originalSize
                });
                
                var verified = await VerifyPartsAsync(checksumFilePath, null, cancellationToken);
                if (!verified)
                {
                    return new SplitResult
                    {
                        OriginalFile = sourceFile,
                        OriginalSize = originalSize,
                        OriginalChecksum = originalChecksum,
                        Parts = parts,
                        Success = false,
                        ErrorMessage = "Verification failed - checksums do not match",
                        Duration = stopwatch.Elapsed,
                        ChecksumFilePath = checksumFilePath
                    };
                }
            }
            
            // Delete original if requested
            if (options.DeleteOriginalAfterSplit)
            {
                File.Delete(longSourcePath);
            }
            
            stopwatch.Stop();
            
            return new SplitResult
            {
                OriginalFile = sourceFile,
                OriginalSize = originalSize,
                OriginalChecksum = originalChecksum,
                Parts = parts,
                Success = true,
                Duration = stopwatch.Elapsed,
                ChecksumFilePath = checksumFilePath,
                BatchFilePath = batchFilePath,
                ShellScriptPath = shellScriptPath
            };
        }
        catch (Exception ex)
        {
            return new SplitResult
            {
                OriginalFile = sourceFile,
                Parts = parts,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }
    
    private static string GetPartFileName(string baseName, string originalExt, int partNum, int totalParts, SplitNamingPattern pattern)
    {
        var digits = Math.Max(3, totalParts.ToString().Length);
        var numStr = partNum.ToString().PadLeft(digits, '0');
        
        return pattern switch
        {
            SplitNamingPattern.NumberedExtension => $"{baseName}.{numStr}",
            SplitNamingPattern.PreserveExtension => $"{baseName}{originalExt}.{numStr}",
            SplitNamingPattern.PartSuffix => $"{baseName}_part{partNum}.dat",
            _ => $"{baseName}.{numStr}"
        };
    }
    
    private async Task CreateChecksumFileAsync(
        string filePath,
        string originalFileName,
        long originalSize,
        string originalChecksum,
        IReadOnlyList<SplitPart> parts,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"filename={Path.GetFileName(originalFileName)}");
        sb.AppendLine($"size={originalSize}");
        sb.AppendLine($"crc32={originalChecksum}");
        sb.AppendLine($"parts={parts.Count}");
        sb.AppendLine();
        
        foreach (var part in parts)
        {
            if (part.Checksum != null)
            {
                sb.AppendLine($"{Path.GetFileName(part.FilePath)}={part.Checksum}");
            }
        }
        
        await File.WriteAllTextAsync(
            _longPathService.NormalizePath(filePath), 
            sb.ToString(), 
            cancellationToken);
    }
    
    private async Task CreateBatchFileAsync(
        string filePath,
        string targetFileName,
        IReadOnlyList<SplitPart> parts,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine($"echo Merging files to {targetFileName}...");
        sb.Append($"copy /b ");
        
        var partNames = parts.Select(p => Path.GetFileName(p.FilePath));
        sb.Append(string.Join("+", partNames));
        sb.AppendLine($" \"{targetFileName}\"");
        
        sb.AppendLine("echo Done!");
        sb.AppendLine("pause");
        
        await File.WriteAllTextAsync(
            _longPathService.NormalizePath(filePath), 
            sb.ToString(), 
            cancellationToken);
    }
    
    private async Task CreateShellScriptAsync(
        string filePath,
        string targetFileName,
        IReadOnlyList<SplitPart> parts,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine($"echo \"Merging files to {targetFileName}...\"");
        sb.Append("cat ");
        
        var partNames = parts.Select(p => Path.GetFileName(p.FilePath));
        sb.Append(string.Join(" ", partNames));
        sb.AppendLine($" > \"{targetFileName}\"");
        
        sb.AppendLine("echo \"Done!\"");
        
        await File.WriteAllTextAsync(
            _longPathService.NormalizePath(filePath), 
            sb.ToString(), 
            cancellationToken);
    }
    
    public async Task<MergeResult> MergeFilesAsync(
        string firstPartOrChecksumFile,
        MergeOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Detect parts
        var partFiles = await DetectPartsAsync(firstPartOrChecksumFile, cancellationToken);
        
        if (partFiles.Count == 0)
        {
            return new MergeResult
            {
                Success = false,
                ErrorMessage = "No part files found"
            };
        }
        
        // Determine target path
        var targetPath = options.TargetPath;
        if (string.IsNullOrEmpty(targetPath))
        {
            // Try to determine from checksum file or first part
            var checksumInfo = await ReadChecksumFileAsync(firstPartOrChecksumFile, cancellationToken);
            if (checksumInfo != null)
            {
                var dir = Path.GetDirectoryName(firstPartOrChecksumFile) ?? ".";
                targetPath = Path.Combine(dir, checksumInfo.OriginalFileName);
            }
            else
            {
                // Strip part extension
                var firstPart = partFiles[0];
                var ext = Path.GetExtension(firstPart);
                targetPath = ext.All(char.IsDigit) || ext.StartsWith(".0")
                    ? Path.ChangeExtension(firstPart, null)
                    : firstPart + ".merged";
            }
        }
        
        return await MergeFilesAsync(partFiles, targetPath!, options, progress, cancellationToken);
    }
    
    public async Task<MergeResult> MergeFilesAsync(
        IReadOnlyList<string> partFiles,
        string targetPath,
        MergeOptions options,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Calculate total size
            long totalSize = 0;
            foreach (var part in partFiles)
            {
                var info = new FileInfo(_longPathService.NormalizePath(part));
                if (!info.Exists)
                {
                    return new MergeResult
                    {
                        PartFiles = partFiles,
                        Success = false,
                        ErrorMessage = $"Part file not found: {part}"
                    };
                }
                totalSize += info.Length;
            }
            
            var longTargetPath = _longPathService.NormalizePath(targetPath);
            
            // Handle resume
            long startOffset = 0;
            int startPart = 0;
            if (options.ResumeIfExists && File.Exists(longTargetPath))
            {
                var existingSize = new FileInfo(longTargetPath).Length;
                long runningSize = 0;
                for (int i = 0; i < partFiles.Count; i++)
                {
                    var partSize = new FileInfo(_longPathService.NormalizePath(partFiles[i])).Length;
                    if (runningSize + partSize > existingSize)
                    {
                        startPart = i;
                        startOffset = existingSize;
                        break;
                    }
                    runningSize += partSize;
                }
            }
            
            // Create/open target file
            var fileMode = startOffset > 0 ? FileMode.Append : FileMode.Create;
            await using var targetStream = new FileStream(
                longTargetPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, true);
            
            var buffer = new byte[BufferSize];
            long totalBytesProcessed = startOffset;
            int partsProcessed = startPart;
            
            for (int i = startPart; i < partFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var partPath = partFiles[i];
                var longPartPath = _longPathService.NormalizePath(partPath);
                
                await using var partStream = new FileStream(
                    longPartPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
                
                int bytesRead;
                while ((bytesRead = await partStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesProcessed += bytesRead;
                    
                    var elapsed = stopwatch.Elapsed;
                    var speed = elapsed.TotalSeconds > 0 ? totalBytesProcessed / elapsed.TotalSeconds : 0;
                    var remaining = speed > 0 
                        ? TimeSpan.FromSeconds((totalSize - totalBytesProcessed) / speed) 
                        : (TimeSpan?)null;
                    
                    progress?.Report(new SplitMergeProgress
                    {
                        CurrentOperation = $"Merging part {i + 1}/{partFiles.Count}",
                        CurrentPart = i + 1,
                        TotalParts = partFiles.Count,
                        BytesProcessed = totalBytesProcessed,
                        TotalBytes = totalSize,
                        Elapsed = elapsed,
                        EstimatedRemaining = remaining,
                        SpeedBytesPerSecond = speed
                    });
                }
                
                partsProcessed++;
            }
            
            // Verify checksum if requested
            bool checksumVerified = false;
            string? finalChecksum = null;
            if (options.VerifyChecksums)
            {
                progress?.Report(new SplitMergeProgress
                {
                    CurrentOperation = "Verifying merged file...",
                    TotalParts = partFiles.Count,
                    TotalBytes = totalSize,
                    BytesProcessed = totalSize
                });
                
                finalChecksum = await CalculateCrc32Async(longTargetPath, cancellationToken);
                
                // Try to find and verify against checksum file
                var dir = Path.GetDirectoryName(partFiles[0]) ?? ".";
                var crcFiles = Directory.GetFiles(dir, "*.crc");
                foreach (var crcFile in crcFiles)
                {
                    var info = await ReadChecksumFileAsync(crcFile, cancellationToken);
                    if (info != null && info.Checksum.Equals(finalChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        checksumVerified = true;
                        break;
                    }
                }
            }
            
            // Delete parts if requested
            if (options.DeletePartsAfterMerge)
            {
                foreach (var part in partFiles)
                {
                    File.Delete(_longPathService.NormalizePath(part));
                }
            }
            
            stopwatch.Stop();
            
            return new MergeResult
            {
                MergedFile = targetPath,
                MergedSize = totalBytesProcessed,
                Checksum = finalChecksum,
                PartFiles = partFiles,
                PartsProcessed = partsProcessed,
                Success = true,
                Duration = stopwatch.Elapsed,
                ChecksumVerified = checksumVerified
            };
        }
        catch (Exception ex)
        {
            return new MergeResult
            {
                MergedFile = targetPath,
                PartFiles = partFiles,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }
    
    public async Task<IReadOnlyList<string>> DetectPartsAsync(
        string firstPartOrChecksumFile,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        var dir = Path.GetDirectoryName(firstPartOrChecksumFile) ?? ".";
        
        // Check if it's a checksum file
        if (firstPartOrChecksumFile.EndsWith(".crc", StringComparison.OrdinalIgnoreCase))
        {
            var info = await ReadChecksumFileAsync(firstPartOrChecksumFile, cancellationToken);
            if (info != null && info.PartChecksums.Count > 0)
            {
                foreach (var kvp in info.PartChecksums.OrderBy(k => k.Key))
                {
                    var partPath = Path.Combine(dir, kvp.Key.ToString());
                    if (File.Exists(_longPathService.NormalizePath(partPath)))
                    {
                        parts.Add(partPath);
                    }
                }
                
                if (parts.Count > 0) return parts;
            }
        }
        
        // Detect numbered parts (file.001, file.002, etc.)
        var fileName = Path.GetFileName(firstPartOrChecksumFile);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        
        // Remove number extension if present
        var numberMatch = Regex.Match(baseName, @"^(.+)\.(\d+)$");
        if (!numberMatch.Success)
        {
            numberMatch = Regex.Match(fileName, @"^(.+)\.(\d+)$");
        }
        
        if (numberMatch.Success)
        {
            baseName = numberMatch.Groups[1].Value;
            var numDigits = numberMatch.Groups[2].Value.Length;
            
            // Find all matching parts
            var pattern = $"{baseName}.*";
            var files = Directory.GetFiles(dir, pattern)
                .Where(f => Regex.IsMatch(Path.GetFileName(f), $@"^{Regex.Escape(baseName)}\.\d+$"))
                .OrderBy(f => 
                {
                    var match = Regex.Match(Path.GetFileName(f), @"\.(\d+)$");
                    return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                })
                .ToList();
            
            return files;
        }
        
        // Fallback: return just the single file
        if (File.Exists(_longPathService.NormalizePath(firstPartOrChecksumFile)))
        {
            parts.Add(firstPartOrChecksumFile);
        }
        
        return parts;
    }
    
    public async Task<bool> VerifyPartsAsync(
        string checksumFile,
        IProgress<SplitMergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var info = await ReadChecksumFileAsync(checksumFile, cancellationToken);
        if (info == null) return false;
        
        var dir = Path.GetDirectoryName(checksumFile) ?? ".";
        int current = 0;
        
        foreach (var kvp in info.PartChecksums)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;
            
            progress?.Report(new SplitMergeProgress
            {
                CurrentOperation = $"Verifying part {current}/{info.TotalParts}",
                CurrentPart = current,
                TotalParts = info.TotalParts
            });
            
            // The key might be the filename directly
            string partPath;
            if (kvp.Key is int partNum)
            {
                // Find file matching this part number
                var files = Directory.GetFiles(dir, $"*.{partNum:D3}");
                if (files.Length == 0) return false;
                partPath = files[0];
            }
            else
            {
                partPath = Path.Combine(dir, kvp.Key.ToString()!);
            }
            
            var actualChecksum = await CalculateCrc32Async(
                _longPathService.NormalizePath(partPath), cancellationToken);
            
            if (!actualChecksum.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        
        return true;
    }
    
    public async Task<ChecksumFileInfo?> ReadChecksumFileAsync(
        string checksumFile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var longPath = _longPathService.NormalizePath(checksumFile);
            if (!File.Exists(longPath)) return null;
            
            var lines = await File.ReadAllLinesAsync(longPath, cancellationToken);
            
            string? originalFileName = null;
            long originalSize = 0;
            string? checksum = null;
            int totalParts = 0;
            var partChecksums = new Dictionary<int, string>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var eqIndex = line.IndexOf('=');
                if (eqIndex <= 0) continue;
                
                var key = line.Substring(0, eqIndex).Trim().ToLowerInvariant();
                var value = line.Substring(eqIndex + 1).Trim();
                
                switch (key)
                {
                    case "filename":
                        originalFileName = value;
                        break;
                    case "size":
                        long.TryParse(value, out originalSize);
                        break;
                    case "crc32":
                        checksum = value;
                        break;
                    case "parts":
                        int.TryParse(value, out totalParts);
                        break;
                    default:
                        // Part checksum entry (e.g., "file.001=ABCD1234")
                        if (Regex.IsMatch(key, @"\.\d+$"))
                        {
                            var numMatch = Regex.Match(key, @"\.(\d+)$");
                            if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var partNum))
                            {
                                partChecksums[partNum] = value;
                            }
                        }
                        break;
                }
            }
            
            if (originalFileName == null || checksum == null)
                return null;
            
            return new ChecksumFileInfo
            {
                OriginalFileName = originalFileName,
                OriginalFileSize = originalSize,
                Checksum = checksum,
                ChecksumType = "CRC32",
                TotalParts = totalParts > 0 ? totalParts : partChecksums.Count,
                PartChecksums = partChecksums
            };
        }
        catch
        {
            return null;
        }
    }
    
    private static async Task<string> CalculateCrc32Async(string filePath, CancellationToken cancellationToken)
    {
        // Use built-in CRC32 if available, otherwise simple implementation
        uint crc = 0xFFFFFFFF;
        var buffer = new byte[BufferSize];
        
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }
        
        crc ^= 0xFFFFFFFF;
        return crc.ToString("X8");
    }
    
    // CRC32 lookup table
    private static readonly uint[] Crc32Table = GenerateCrc32Table();
    
    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320;
        
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        
        return table;
    }
}
