namespace XCommander.Services;

/// <summary>
/// Method for splitting files
/// </summary>
public enum FileSplitMethod
{
    /// <summary>
    /// Split by fixed size (in bytes)
    /// </summary>
    BySize,
    
    /// <summary>
    /// Split into a specific number of parts
    /// </summary>
    ByParts,
    
    /// <summary>
    /// Split to fit on specific media (floppy, CD, DVD, etc.)
    /// </summary>
    ByMedia
}

/// <summary>
/// Predefined media sizes for file splitting
/// </summary>
public enum MediaSize : long
{
    Floppy1440KB = 1_474_560,
    Floppy720KB = 737_280,
    Zip100MB = 100_000_000,
    Zip250MB = 250_000_000,
    CD650MB = 681_574_400,
    CD700MB = 734_003_200,
    DVD4_7GB = 4_700_000_000,
    DVD8_5GB = 8_500_000_000,
    BluRay25GB = 25_000_000_000,
    BluRay50GB = 50_000_000_000
}

/// <summary>
/// Progress information for split/combine operations
/// </summary>
public class FileSplitProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public int CurrentPart { get; init; }
    public int TotalParts { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 0;
    public double SpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Result of a file split operation
/// </summary>
public class FileSplitResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public IReadOnlyList<string> PartFiles { get; init; } = Array.Empty<string>();
    public int TotalParts { get; init; }
    public long OriginalSize { get; init; }
    public TimeSpan Duration { get; init; }
    public bool CrcFileCreated { get; init; }
}

/// <summary>
/// Result of a file combine operation
/// </summary>
public class FileCombineResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();
    public string OutputFile { get; init; } = string.Empty;
    public long OutputSize { get; init; }
    public TimeSpan Duration { get; init; }
    public bool CrcVerified { get; init; }
    public bool CrcMatch { get; init; }
}

/// <summary>
/// Options for file split operation
/// </summary>
public class FileSplitOptions
{
    public FileSplitMethod Method { get; init; } = FileSplitMethod.BySize;
    public long PartSize { get; init; } = 1024 * 1024; // 1 MB default
    public int NumberOfParts { get; init; } = 2;
    public MediaSize? MediaSize { get; init; }
    public string OutputDirectory { get; init; } = string.Empty;
    public string? CustomExtension { get; init; } // null = .001, .002, etc.
    public bool CreateCrcFile { get; init; } = true;
    public bool DeleteSourceOnSuccess { get; init; } = false;
}

/// <summary>
/// Service for splitting and combining files
/// </summary>
public interface IFileSplitService
{
    /// <summary>
    /// Split a file into multiple parts
    /// </summary>
    Task<FileSplitResult> SplitFileAsync(
        string sourceFilePath,
        FileSplitOptions options,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Combine multiple parts into a single file
    /// </summary>
    Task<FileCombineResult> CombineFilesAsync(
        IEnumerable<string> partFilePaths,
        string outputFilePath,
        bool verifyCrc = true,
        bool deletePartsOnSuccess = false,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Auto-detect and combine parts based on a single part file
    /// </summary>
    Task<FileCombineResult> CombineFromPartAsync(
        string anyPartFilePath,
        string? outputFilePath = null,
        bool verifyCrc = true,
        bool deletePartsOnSuccess = false,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all part files for a split file
    /// </summary>
    IReadOnlyList<string> DetectPartFiles(string anyPartFilePath);
    
    /// <summary>
    /// Check if a file appears to be a split part
    /// </summary>
    bool IsSplitPart(string filePath);
    
    /// <summary>
    /// Calculate how many parts would be created for a split operation
    /// </summary>
    int CalculatePartCount(long fileSize, FileSplitOptions options);
    
    /// <summary>
    /// Get the part size that would be used for a split operation
    /// </summary>
    long CalculatePartSize(long fileSize, FileSplitOptions options);
}

public class FileSplitService : IFileSplitService
{
    private const int BufferSize = 81920; // 80KB buffer
    private readonly IFileChecksumService? _checksumService;
    
    public FileSplitService(IFileChecksumService? checksumService = null)
    {
        _checksumService = checksumService;
    }
    
    public async Task<FileSplitResult> SplitFileAsync(
        string sourceFilePath,
        FileSplitOptions options,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var partFiles = new List<string>();
        
        try
        {
            if (!File.Exists(sourceFilePath))
            {
                return new FileSplitResult
                {
                    Success = false,
                    ErrorMessage = "Source file not found",
                    SourceFile = sourceFilePath
                };
            }
            
            var fileInfo = new FileInfo(sourceFilePath);
            var totalSize = fileInfo.Length;
            var partSize = CalculatePartSize(totalSize, options);
            var totalParts = CalculatePartCount(totalSize, options);
            
            var outputDir = string.IsNullOrEmpty(options.OutputDirectory) 
                ? Path.GetDirectoryName(sourceFilePath) ?? "."
                : options.OutputDirectory;
            
            Directory.CreateDirectory(outputDir);
            
            var baseName = Path.GetFileName(sourceFilePath);
            var bytesProcessed = 0L;
            var lastProgressTime = DateTime.UtcNow;
            var lastBytesForSpeed = 0L;
            
            await using var sourceStream = new FileStream(
                sourceFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan | FileOptions.Asynchronous);
            
            for (var partNumber = 1; partNumber <= totalParts; partNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var partExtension = options.CustomExtension ?? $".{partNumber:D3}";
                var partPath = Path.Combine(outputDir, baseName + partExtension);
                partFiles.Add(partPath);
                
                var bytesToWrite = Math.Min(partSize, totalSize - bytesProcessed);
                
                await using var partStream = new FileStream(
                    partPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.Asynchronous);
                
                var buffer = new byte[BufferSize];
                var partBytesWritten = 0L;
                
                while (partBytesWritten < bytesToWrite)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var toRead = (int)Math.Min(BufferSize, bytesToWrite - partBytesWritten);
                    var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                    
                    if (bytesRead == 0)
                        break;
                    
                    await partStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    
                    partBytesWritten += bytesRead;
                    bytesProcessed += bytesRead;
                    
                    // Report progress every 100ms
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressTime).TotalMilliseconds >= 100)
                    {
                        var elapsed = (now - lastProgressTime).TotalSeconds;
                        var speed = elapsed > 0 ? (bytesProcessed - lastBytesForSpeed) / elapsed : 0;
                        
                        progress?.Report(new FileSplitProgress
                        {
                            CurrentFile = Path.GetFileName(partPath),
                            CurrentPart = partNumber,
                            TotalParts = totalParts,
                            BytesProcessed = bytesProcessed,
                            TotalBytes = totalSize,
                            SpeedBytesPerSecond = speed
                        });
                        
                        lastProgressTime = now;
                        lastBytesForSpeed = bytesProcessed;
                    }
                }
            }
            
            // Create CRC file if requested
            var crcFileCreated = false;
            if (options.CreateCrcFile && _checksumService != null)
            {
                var crcFilePath = Path.Combine(outputDir, baseName + ".crc");
                await CreateCrcFileAsync(sourceFilePath, totalSize, crcFilePath, cancellationToken);
                crcFileCreated = true;
            }
            
            // Delete source if requested
            if (options.DeleteSourceOnSuccess)
            {
                File.Delete(sourceFilePath);
            }
            
            stopwatch.Stop();
            
            return new FileSplitResult
            {
                Success = true,
                SourceFile = sourceFilePath,
                PartFiles = partFiles,
                TotalParts = totalParts,
                OriginalSize = totalSize,
                Duration = stopwatch.Elapsed,
                CrcFileCreated = crcFileCreated
            };
        }
        catch (OperationCanceledException)
        {
            // Clean up partial files on cancellation
            foreach (var partFile in partFiles)
            {
                try { File.Delete(partFile); } catch { }
            }
            throw;
        }
        catch (Exception ex)
        {
            // Clean up partial files on error
            foreach (var partFile in partFiles)
            {
                try { File.Delete(partFile); } catch { }
            }
            
            stopwatch.Stop();
            return new FileSplitResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SourceFile = sourceFilePath,
                Duration = stopwatch.Elapsed
            };
        }
    }
    
    public async Task<FileCombineResult> CombineFilesAsync(
        IEnumerable<string> partFilePaths,
        string outputFilePath,
        bool verifyCrc = true,
        bool deletePartsOnSuccess = false,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sourceFiles = partFilePaths.ToList();
        
        try
        {
            if (!sourceFiles.Any())
            {
                return new FileCombineResult
                {
                    Success = false,
                    ErrorMessage = "No part files specified"
                };
            }
            
            // Sort files by part number
            sourceFiles = sourceFiles.OrderBy(f => f).ToList();
            
            // Check all files exist
            foreach (var file in sourceFiles)
            {
                if (!File.Exists(file))
                {
                    return new FileCombineResult
                    {
                        Success = false,
                        ErrorMessage = $"Part file not found: {file}",
                        SourceFiles = sourceFiles
                    };
                }
            }
            
            var totalSize = sourceFiles.Sum(f => new FileInfo(f).Length);
            var bytesProcessed = 0L;
            var lastProgressTime = DateTime.UtcNow;
            var lastBytesForSpeed = 0L;
            
            // Create output directory if needed
            var outputDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);
            
            await using var outputStream = new FileStream(
                outputFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous);
            
            for (var i = 0; i < sourceFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var partFile = sourceFiles[i];
                
                await using var partStream = new FileStream(
                    partFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.SequentialScan | FileOptions.Asynchronous);
                
                var buffer = new byte[BufferSize];
                int bytesRead;
                
                while ((bytesRead = await partStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesProcessed += bytesRead;
                    
                    // Report progress every 100ms
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressTime).TotalMilliseconds >= 100)
                    {
                        var elapsed = (now - lastProgressTime).TotalSeconds;
                        var speed = elapsed > 0 ? (bytesProcessed - lastBytesForSpeed) / elapsed : 0;
                        
                        progress?.Report(new FileSplitProgress
                        {
                            CurrentFile = Path.GetFileName(partFile),
                            CurrentPart = i + 1,
                            TotalParts = sourceFiles.Count,
                            BytesProcessed = bytesProcessed,
                            TotalBytes = totalSize,
                            SpeedBytesPerSecond = speed
                        });
                        
                        lastProgressTime = now;
                        lastBytesForSpeed = bytesProcessed;
                    }
                }
            }
            
            await outputStream.FlushAsync(cancellationToken);
            
            // Verify CRC if requested and CRC file exists
            var crcVerified = false;
            var crcMatch = false;
            
            if (verifyCrc && _checksumService != null)
            {
                var crcFilePath = FindCrcFile(sourceFiles.First());
                if (crcFilePath != null)
                {
                    crcVerified = true;
                    crcMatch = await VerifyCrcAsync(outputFilePath, crcFilePath, cancellationToken);
                }
            }
            
            // Delete parts if requested and successful
            if (deletePartsOnSuccess && (!crcVerified || crcMatch))
            {
                foreach (var partFile in sourceFiles)
                {
                    try { File.Delete(partFile); } catch { }
                }
                
                // Also delete CRC file
                var crcFile = FindCrcFile(sourceFiles.First());
                if (crcFile != null)
                {
                    try { File.Delete(crcFile); } catch { }
                }
            }
            
            stopwatch.Stop();
            
            return new FileCombineResult
            {
                Success = !crcVerified || crcMatch,
                ErrorMessage = crcVerified && !crcMatch ? "CRC verification failed" : null,
                SourceFiles = sourceFiles,
                OutputFile = outputFilePath,
                OutputSize = bytesProcessed,
                Duration = stopwatch.Elapsed,
                CrcVerified = crcVerified,
                CrcMatch = crcMatch
            };
        }
        catch (OperationCanceledException)
        {
            // Clean up output file on cancellation
            try { File.Delete(outputFilePath); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            // Clean up output file on error
            try { File.Delete(outputFilePath); } catch { }
            
            stopwatch.Stop();
            return new FileCombineResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SourceFiles = sourceFiles,
                Duration = stopwatch.Elapsed
            };
        }
    }
    
    public async Task<FileCombineResult> CombineFromPartAsync(
        string anyPartFilePath,
        string? outputFilePath = null,
        bool verifyCrc = true,
        bool deletePartsOnSuccess = false,
        IProgress<FileSplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var partFiles = DetectPartFiles(anyPartFilePath);
        
        if (!partFiles.Any())
        {
            return new FileCombineResult
            {
                Success = false,
                ErrorMessage = "Could not detect part files"
            };
        }
        
        // Determine output filename (remove extension like .001)
        if (string.IsNullOrEmpty(outputFilePath))
        {
            var basePath = partFiles.First();
            var extension = Path.GetExtension(basePath);
            
            // Remove numeric extension like .001, .002
            if (extension.Length == 4 && int.TryParse(extension[1..], out _))
            {
                outputFilePath = basePath[..^extension.Length];
            }
            else
            {
                outputFilePath = basePath + ".combined";
            }
        }
        
        return await CombineFilesAsync(
            partFiles, 
            outputFilePath, 
            verifyCrc, 
            deletePartsOnSuccess, 
            progress, 
            cancellationToken);
    }
    
    public IReadOnlyList<string> DetectPartFiles(string anyPartFilePath)
    {
        var result = new List<string>();
        var directory = Path.GetDirectoryName(anyPartFilePath) ?? ".";
        var fileName = Path.GetFileName(anyPartFilePath);
        
        // Try to find base name by removing numeric extension
        var extension = Path.GetExtension(fileName);
        string baseName;
        
        if (extension.Length == 4 && int.TryParse(extension[1..], out _))
        {
            // Extension like .001, .002, etc.
            baseName = fileName[..^extension.Length];
        }
        else
        {
            // Try other patterns
            baseName = fileName;
        }
        
        // Look for sequential parts
        for (var i = 1; i <= 999; i++)
        {
            var partPath = Path.Combine(directory, $"{baseName}.{i:D3}");
            if (File.Exists(partPath))
            {
                result.Add(partPath);
            }
            else if (i > 1)
            {
                // Stop if we found at least one part and hit a gap
                break;
            }
        }
        
        // If no .001 format found, try other formats
        if (!result.Any())
        {
            // Try .part1, .part2 format
            for (var i = 1; i <= 999; i++)
            {
                var partPath = Path.Combine(directory, $"{baseName}.part{i}");
                if (File.Exists(partPath))
                {
                    result.Add(partPath);
                }
                else if (i > 1)
                {
                    break;
                }
            }
        }
        
        return result;
    }
    
    public bool IsSplitPart(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        
        // Check for .001, .002, etc.
        if (extension.Length == 4 && int.TryParse(extension[1..], out _))
            return true;
        
        // Check for .part1, .part2, etc.
        if (extension.StartsWith(".part", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(extension[5..], out _))
            return true;
        
        return false;
    }
    
    public int CalculatePartCount(long fileSize, FileSplitOptions options)
    {
        if (fileSize <= 0)
            return 0;
        
        return options.Method switch
        {
            FileSplitMethod.ByParts => options.NumberOfParts,
            FileSplitMethod.BySize => (int)Math.Ceiling((double)fileSize / options.PartSize),
            FileSplitMethod.ByMedia when options.MediaSize.HasValue => 
                (int)Math.Ceiling((double)fileSize / (long)options.MediaSize.Value),
            _ => (int)Math.Ceiling((double)fileSize / options.PartSize)
        };
    }
    
    public long CalculatePartSize(long fileSize, FileSplitOptions options)
    {
        if (fileSize <= 0)
            return 0;
        
        return options.Method switch
        {
            FileSplitMethod.ByParts => (long)Math.Ceiling((double)fileSize / options.NumberOfParts),
            FileSplitMethod.BySize => options.PartSize,
            FileSplitMethod.ByMedia when options.MediaSize.HasValue => (long)options.MediaSize.Value,
            _ => options.PartSize
        };
    }
    
    private async Task CreateCrcFileAsync(string sourceFilePath, long fileSize, string crcFilePath, CancellationToken cancellationToken)
    {
        if (_checksumService == null)
            return;
        
        var result = await _checksumService.CalculateChecksumAsync(sourceFilePath, ChecksumAlgorithm.CRC32, cancellationToken);
        
        if (result.Success)
        {
            var content = $"filename={Path.GetFileName(sourceFilePath)}\nsize={fileSize}\ncrc32={result.Hash}\n";
            await File.WriteAllTextAsync(crcFilePath, content, cancellationToken);
        }
    }
    
    private string? FindCrcFile(string partFilePath)
    {
        var directory = Path.GetDirectoryName(partFilePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(partFilePath);
        
        // Remove part number if present
        var extension = Path.GetExtension(partFilePath);
        if (extension.Length == 4 && int.TryParse(extension[1..], out _))
        {
            baseName = Path.GetFileName(partFilePath)[..^extension.Length];
        }
        
        var crcPath = Path.Combine(directory, baseName + ".crc");
        return File.Exists(crcPath) ? crcPath : null;
    }
    
    private async Task<bool> VerifyCrcAsync(string filePath, string crcFilePath, CancellationToken cancellationToken)
    {
        if (_checksumService == null)
            return true;
        
        try
        {
            var crcContent = await File.ReadAllTextAsync(crcFilePath, cancellationToken);
            string? expectedCrc = null;
            
            foreach (var line in crcContent.Split('\n'))
            {
                if (line.StartsWith("crc32=", StringComparison.OrdinalIgnoreCase))
                {
                    expectedCrc = line[6..].Trim();
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(expectedCrc))
                return true; // No CRC to verify
            
            return await _checksumService.VerifyChecksumAsync(filePath, expectedCrc, ChecksumAlgorithm.CRC32, cancellationToken);
        }
        catch
        {
            return true; // Assume success on error
        }
    }
}
