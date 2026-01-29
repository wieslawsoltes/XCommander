using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz;

namespace XCommander.Services;

/// <summary>
/// Service for handling legacy and specialized archive formats.
/// Provides support for ACE, ARJ, CAB, LZH, UC2, XZ/LZMA formats.
/// </summary>
public class LegacyArchiveService : ILegacyArchiveService
{
    private readonly Dictionary<string, string> _toolPaths = new();
    
    private static readonly LegacyArchiveFormat AceFormat = new()
    {
        Id = "ace",
        Name = "ACE",
        Description = "ACE Archive (read-only)",
        Extensions = new[] { ".ace" },
        CanRead = true,
        CanCreate = false,
        RequiresExternalTool = true,
        ExternalToolName = "unace",
        MagicBytes = new byte[] { 0x2A, 0x2A, 0x41, 0x43, 0x45, 0x2A, 0x2A } // **ACE**
    };
    
    private static readonly LegacyArchiveFormat ArjFormat = new()
    {
        Id = "arj",
        Name = "ARJ",
        Description = "ARJ Archive",
        Extensions = new[] { ".arj" },
        CanRead = true,
        CanCreate = true,
        RequiresExternalTool = true,
        ExternalToolName = "arj",
        MagicBytes = new byte[] { 0x60, 0xEA }
    };
    
    private static readonly LegacyArchiveFormat CabFormat = new()
    {
        Id = "cab",
        Name = "CAB",
        Description = "Microsoft Cabinet Archive",
        Extensions = new[] { ".cab" },
        CanRead = true,
        CanCreate = false,
        RequiresExternalTool = false, // Can use built-in on Windows
        MagicBytes = new byte[] { 0x4D, 0x53, 0x43, 0x46 } // MSCF
    };
    
    private static readonly LegacyArchiveFormat LzhFormat = new()
    {
        Id = "lzh",
        Name = "LZH/LHA",
        Description = "LZH/LHA Archive",
        Extensions = new[] { ".lzh", ".lha" },
        CanRead = true,
        CanCreate = true,
        RequiresExternalTool = true,
        ExternalToolName = "lha"
    };
    
    private static readonly LegacyArchiveFormat Uc2Format = new()
    {
        Id = "uc2",
        Name = "UC2",
        Description = "UltraCompressor II Archive",
        Extensions = new[] { ".uc2", ".ucn" },
        CanRead = true,
        CanCreate = false,
        RequiresExternalTool = true,
        ExternalToolName = "uc2"
    };
    
    private static readonly LegacyArchiveFormat XzFormat = new()
    {
        Id = "xz",
        Name = "XZ/LZMA",
        Description = "XZ/LZMA Compressed Archive",
        Extensions = new[] { ".xz", ".lzma", ".txz" },
        CanRead = true,
        CanCreate = true,
        RequiresExternalTool = false, // Native .NET support via System.IO.Compression
        MagicBytes = new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 } // XZ magic
    };
    
    private static readonly List<LegacyArchiveFormat> AllFormats = new()
    {
        AceFormat, ArjFormat, CabFormat, LzhFormat, Uc2Format, XzFormat
    };
    
    public IReadOnlyList<LegacyArchiveFormat> SupportedFormats => AllFormats;
    
    public bool IsSupported(string archivePath)
    {
        return GetFormat(archivePath) != null;
    }
    
    public LegacyArchiveFormat? GetFormat(string archivePath)
    {
        var extension = Path.GetExtension(archivePath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension)) return null;
        
        // First try by extension
        var format = AllFormats.FirstOrDefault(f => f.Extensions.Contains(extension));
        if (format != null) return format;
        
        // Try by magic bytes
        if (File.Exists(archivePath))
        {
            try
            {
                var header = new byte[16];
                using var stream = File.OpenRead(archivePath);
                var bytesRead = stream.Read(header, 0, header.Length);
                
                foreach (var fmt in AllFormats.Where(f => f.MagicBytes != null))
                {
                    if (bytesRead >= fmt.MagicBytes!.Length)
                    {
                        var match = true;
                        for (int i = 0; i < fmt.MagicBytes.Length; i++)
                        {
                            if (header[i] != fmt.MagicBytes[i])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match) return fmt;
                    }
                }
            }
            catch { }
        }
        
        return null;
    }
    
    public async Task<IEnumerable<LegacyArchiveEntry>> ListContentsAsync(string archivePath,
        CancellationToken cancellationToken = default)
    {
        var format = GetFormat(archivePath);
        if (format == null)
            throw new NotSupportedException($"Unsupported archive format: {archivePath}");
        
        return format.Id switch
        {
            "xz" => await ListXzContentsAsync(archivePath, cancellationToken),
            "cab" => await ListCabContentsAsync(archivePath, cancellationToken),
            _ => await ListExternalToolContentsAsync(archivePath, format, cancellationToken)
        };
    }
    
    public async Task ExtractAllAsync(string archivePath, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var format = GetFormat(archivePath);
        if (format == null)
            throw new NotSupportedException($"Unsupported archive format: {archivePath}");
        
        Directory.CreateDirectory(destinationPath);
        
        switch (format.Id)
        {
            case "xz":
                await ExtractXzAsync(archivePath, destinationPath, progress, cancellationToken);
                break;
            case "cab":
                await ExtractCabAsync(archivePath, destinationPath, progress, cancellationToken);
                break;
            default:
                await ExtractExternalToolAsync(archivePath, destinationPath, format, progress, cancellationToken);
                break;
        }
    }
    
    public async Task ExtractFilesAsync(string archivePath, IEnumerable<string> entryPaths, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // For most legacy formats, selective extraction requires external tools
        var format = GetFormat(archivePath);
        if (format == null)
            throw new NotSupportedException($"Unsupported archive format: {archivePath}");
        
        Directory.CreateDirectory(destinationPath);
        
        // Extract all and then remove unwanted files (simplistic approach)
        // For production, would implement proper selective extraction per format
        await ExtractAllAsync(archivePath, destinationPath, progress, cancellationToken);
        
        var entrySet = new HashSet<string>(entryPaths, StringComparer.OrdinalIgnoreCase);
        var extractedFiles = Directory.GetFiles(destinationPath, "*", SearchOption.AllDirectories);
        
        foreach (var file in extractedFiles)
        {
            var relativePath = Path.GetRelativePath(destinationPath, file);
            if (!entrySet.Contains(relativePath) && !entrySet.Contains(Path.GetFileName(file)))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
    
    public async Task CreateArchiveAsync(string archivePath, IEnumerable<string> sourcePaths,
        LegacyArchiveFormat format, LegacyCompressionOptions? options = null,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!format.CanCreate)
            throw new NotSupportedException($"Cannot create archives in {format.Name} format");
        
        switch (format.Id)
        {
            case "xz":
                await CreateXzAsync(archivePath, sourcePaths, options, progress, cancellationToken);
                break;
            default:
                await CreateExternalToolAsync(archivePath, sourcePaths, format, options, progress, cancellationToken);
                break;
        }
    }
    
    public async Task<LegacyArchiveTestResult> TestArchiveAsync(string archivePath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var format = GetFormat(archivePath);
        if (format == null)
            throw new NotSupportedException($"Unsupported archive format: {archivePath}");
        
        var errors = new List<string>();
        int totalEntries = 0;
        int validEntries = 0;
        
        try
        {
            var entries = await ListContentsAsync(archivePath, cancellationToken);
            totalEntries = entries.Count();
            
            // For XZ, we can verify by trying to decompress
            if (format.Id == "xz")
            {
                try
                {
                    using var inputStream = File.OpenRead(archivePath);
                    using var xzStream = new System.IO.Compression.BrotliStream(inputStream, CompressionMode.Decompress);
                    var buffer = new byte[4096];
                    while (await xzStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken) > 0) { }
                    validEntries = totalEntries;
                }
                catch (Exception ex)
                {
                    errors.Add($"Decompression failed: {ex.Message}");
                }
            }
            else if (format.RequiresExternalTool)
            {
                // Use external tool test command
                var result = await RunExternalToolAsync(format, "t", archivePath, cancellationToken);
                if (result.ExitCode == 0)
                    validEntries = totalEntries;
                else
                    errors.Add(result.Output);
            }
            else
            {
                validEntries = totalEntries;
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }
        
        return new LegacyArchiveTestResult
        {
            IsValid = errors.Count == 0 && validEntries == totalEntries,
            TotalEntries = totalEntries,
            ValidEntries = validEntries,
            CorruptedEntries = totalEntries - validEntries,
            Errors = errors
        };
    }
    
    public async Task<LegacyArchiveInfo> GetArchiveInfoAsync(string archivePath,
        CancellationToken cancellationToken = default)
    {
        var format = GetFormat(archivePath);
        if (format == null)
            throw new NotSupportedException($"Unsupported archive format: {archivePath}");
        
        var fileInfo = new FileInfo(archivePath);
        var entries = await ListContentsAsync(archivePath, cancellationToken);
        var entryList = entries.ToList();
        
        var totalSize = entryList.Sum(e => e.Size);
        var compressedSize = entryList.Sum(e => e.CompressedSize > 0 ? e.CompressedSize : e.Size);
        
        return new LegacyArchiveInfo
        {
            Path = archivePath,
            Format = format,
            FileSize = fileInfo.Length,
            UncompressedSize = totalSize,
            EntryCount = entryList.Count,
            FileCount = entryList.Count(e => !e.IsDirectory),
            DirectoryCount = entryList.Count(e => e.IsDirectory),
            CompressionRatio = totalSize > 0 ? (double)compressedSize / totalSize : 1.0,
            Created = fileInfo.CreationTime,
            Modified = fileInfo.LastWriteTime
        };
    }
    
    public bool IsToolAvailable(LegacyArchiveFormat format)
    {
        if (!format.RequiresExternalTool) return true;
        
        if (_toolPaths.TryGetValue(format.Id, out var customPath) && File.Exists(customPath))
            return true;
        
        // Check if tool is in PATH
        var toolName = format.ExternalToolName;
        if (string.IsNullOrEmpty(toolName)) return false;
        
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = pathEnv.Split(Path.PathSeparator);
            var extensions = OperatingSystem.IsWindows() 
                ? new[] { ".exe", ".cmd", ".bat" } 
                : new[] { string.Empty };
            
            foreach (var path in paths)
            {
                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(path, toolName + ext);
                    if (File.Exists(fullPath)) return true;
                }
            }
        }
        catch { }
        
        return false;
    }
    
    public void SetToolPath(LegacyArchiveFormat format, string toolPath)
    {
        _toolPaths[format.Id] = toolPath;
    }
    
    #region XZ/LZMA Support
    
    private async Task<IEnumerable<LegacyArchiveEntry>> ListXzContentsAsync(string archivePath,
        CancellationToken cancellationToken)
    {
        // XZ is typically a single-file compressor
        var fileInfo = new FileInfo(archivePath);
        var innerName = Path.GetFileNameWithoutExtension(archivePath);
        
        // Try to determine uncompressed size
        long uncompressedSize = 0;
        try
        {
            using var inputStream = File.OpenRead(archivePath);
            // Read footer for XZ files (last 12 bytes contain backward size)
            inputStream.Seek(-12, SeekOrigin.End);
            var footer = new byte[12];
            await inputStream.ReadAsync(footer, 0, 12, cancellationToken);
            // Simplified - actual XZ parsing would be more complex
            uncompressedSize = fileInfo.Length * 2; // Estimate
        }
        catch { }
        
        return await Task.FromResult(new[]
        {
            new LegacyArchiveEntry
            {
                Path = innerName,
                Name = innerName,
                IsDirectory = false,
                Size = uncompressedSize,
                CompressedSize = fileInfo.Length,
                Modified = fileInfo.LastWriteTime,
                CompressionMethod = "LZMA"
            }
        });
    }
    
    private async Task ExtractXzAsync(string archivePath, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var innerName = Path.GetFileNameWithoutExtension(archivePath);
        var outputPath = Path.Combine(destinationPath, innerName);
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Extracting",
            CurrentEntry = innerName,
            EntriesProcessed = 0,
            TotalEntries = 1
        });
        
        // Use xz command line tool if available
        if (IsToolAvailable(XzFormat))
        {
            var result = await RunExternalToolAsync(XzFormat, $"--decompress --keep --stdout \"{archivePath}\" > \"{outputPath}\"", archivePath, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"XZ extraction failed: {result.Output}");
        }
        else
        {
            await using var inputStream = File.OpenRead(archivePath);
            await using var outputStream = File.Create(outputPath);
            await using var xzStream = new XZStream(inputStream);
            await xzStream.CopyToAsync(outputStream, cancellationToken);
        }
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Complete",
            CurrentEntry = innerName,
            EntriesProcessed = 1,
            TotalEntries = 1
        });
    }
    
    private async Task CreateXzAsync(string archivePath, IEnumerable<string> sourcePaths,
        LegacyCompressionOptions? options,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceList = sourcePaths.ToList();
        if (sourceList.Count != 1 || !File.Exists(sourceList[0]))
            throw new ArgumentException("XZ compression requires exactly one input file");
        
        var inputFile = sourceList[0];
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Compressing",
            CurrentEntry = Path.GetFileName(inputFile),
            EntriesProcessed = 0,
            TotalEntries = 1
        });
        
        // Use xz command line tool
        var level = options?.CompressionLevel ?? 6;
        if (IsToolAvailable(XzFormat))
        {
            var result = await RunExternalToolAsync(XzFormat, $"-{level} --keep --stdout \"{inputFile}\" > \"{archivePath}\"", inputFile, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"XZ compression failed: {result.Output}");
        }
        else
        {
            await using var inputStream = File.OpenRead(inputFile);
            await using var outputStream = File.Create(archivePath);
            await using var xzStream = new XZStream(outputStream);
            await inputStream.CopyToAsync(xzStream, cancellationToken);
        }
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Complete",
            CurrentEntry = Path.GetFileName(inputFile),
            EntriesProcessed = 1,
            TotalEntries = 1
        });
    }
    
    #endregion
    
    #region CAB Support
    
    private async Task<IEnumerable<LegacyArchiveEntry>> ListCabContentsAsync(string archivePath,
        CancellationToken cancellationToken)
    {
        var entries = new List<LegacyArchiveEntry>();
        
        if (OperatingSystem.IsWindows())
        {
            // Use expand.exe on Windows
            var result = await RunProcessAsync("expand", $"-D \"{archivePath}\"", cancellationToken);
            if (result.ExitCode == 0)
            {
                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1)) // Skip header
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        entries.Add(new LegacyArchiveEntry
                        {
                            Path = trimmed,
                            Name = Path.GetFileName(trimmed),
                            IsDirectory = false,
                            CompressionMethod = "CAB"
                        });
                    }
                }
            }
        }
        else
        {
            // Use cabextract on Unix
            var result = await RunProcessAsync("cabextract", $"-l \"{archivePath}\"", cancellationToken);
            if (result.ExitCode == 0)
            {
                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(2)) // Skip headers
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        var name = parts[^1].Trim();
                        long.TryParse(parts[0].Trim(), out var size);
                        entries.Add(new LegacyArchiveEntry
                        {
                            Path = name,
                            Name = Path.GetFileName(name),
                            IsDirectory = false,
                            Size = size,
                            CompressionMethod = "MSZIP"
                        });
                    }
                }
            }
        }
        
        return entries;
    }
    
    private async Task ExtractCabAsync(string archivePath, string destinationPath,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Extracting",
            CurrentEntry = Path.GetFileName(archivePath),
            EntriesProcessed = 0,
            TotalEntries = 1
        });
        
        ProcessResult result;
        if (OperatingSystem.IsWindows())
        {
            result = await RunProcessAsync("expand", $"\"{archivePath}\" -F:* \"{destinationPath}\"", cancellationToken);
        }
        else
        {
            result = await RunProcessAsync("cabextract", $"-d \"{destinationPath}\" \"{archivePath}\"", cancellationToken);
        }
        
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"CAB extraction failed: {result.Output}");
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Complete",
            CurrentEntry = Path.GetFileName(archivePath),
            EntriesProcessed = 1,
            TotalEntries = 1
        });
    }
    
    #endregion
    
    #region External Tool Support
    
    private async Task<IEnumerable<LegacyArchiveEntry>> ListExternalToolContentsAsync(string archivePath,
        LegacyArchiveFormat format, CancellationToken cancellationToken)
    {
        var entries = new List<LegacyArchiveEntry>();
        
        var listArg = format.Id switch
        {
            "ace" => "l",
            "arj" => "l",
            "lzh" => "l",
            "uc2" => "l",
            _ => "l"
        };
        
        var result = await RunExternalToolAsync(format, listArg, archivePath, cancellationToken);
        
        // Parse output based on format
        // This is format-specific and would need proper implementation per tool
        // For now, return basic structure
        if (result.ExitCode == 0)
        {
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Simplified parsing - would need format-specific parsers
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith('-') && !line.Contains("Archive"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[^1];
                        entries.Add(new LegacyArchiveEntry
                        {
                            Path = name,
                            Name = Path.GetFileName(name),
                            IsDirectory = name.EndsWith('/'),
                            CompressionMethod = format.Name
                        });
                    }
                }
            }
        }
        
        return entries;
    }
    
    private async Task ExtractExternalToolAsync(string archivePath, string destinationPath,
        LegacyArchiveFormat format,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Extracting",
            CurrentEntry = Path.GetFileName(archivePath)
        });
        
        var extractArg = format.Id switch
        {
            "ace" => "x",
            "arj" => $"x \"{archivePath}\" \"{destinationPath}\"",
            "lzh" => $"x \"{archivePath}\" \"{destinationPath}\"",
            "uc2" => "x",
            _ => "x"
        };
        
        var result = await RunExternalToolAsync(format, extractArg, archivePath, cancellationToken);
        
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Extraction failed: {result.Output}");
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Complete",
            CurrentEntry = Path.GetFileName(archivePath)
        });
    }
    
    private async Task CreateExternalToolAsync(string archivePath, IEnumerable<string> sourcePaths,
        LegacyArchiveFormat format, LegacyCompressionOptions? options,
        IProgress<LegacyArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!format.CanCreate)
            throw new NotSupportedException($"Creation not supported for {format.Name}");
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Creating",
            CurrentEntry = Path.GetFileName(archivePath)
        });
        
        var sources = string.Join(" ", sourcePaths.Select(p => $"\"{p}\""));
        var createArg = format.Id switch
        {
            "arj" => $"a \"{archivePath}\" {sources}",
            "lzh" => $"a \"{archivePath}\" {sources}",
            _ => $"a \"{archivePath}\" {sources}"
        };
        
        var result = await RunExternalToolAsync(format, createArg, archivePath, cancellationToken);
        
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Archive creation failed: {result.Output}");
        
        progress?.Report(new LegacyArchiveProgress
        {
            Phase = "Complete",
            CurrentEntry = Path.GetFileName(archivePath)
        });
    }
    
    private async Task<ProcessResult> RunExternalToolAsync(LegacyArchiveFormat format, string arguments,
        string archivePath, CancellationToken cancellationToken)
    {
        var toolPath = _toolPaths.TryGetValue(format.Id, out var custom) ? custom : format.ExternalToolName;
        if (string.IsNullOrEmpty(toolPath))
            throw new InvalidOperationException($"No tool configured for {format.Name}");
        
        return await RunProcessAsync(toolPath!, arguments, cancellationToken);
    }
    
    private async Task<ProcessResult> RunProcessAsync(string executable, string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = psi };
        var output = new List<string>();
        var error = new List<string>();
        
        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.Add(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.Add(e.Data); };
        
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync(cancellationToken);
            
            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = string.Join("\n", output),
                Error = string.Join("\n", error)
            };
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                ExitCode = -1,
                Error = ex.Message
            };
        }
    }
    
    private record ProcessResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }
    
    #endregion
}
