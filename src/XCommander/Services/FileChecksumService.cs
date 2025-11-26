using System.Security.Cryptography;
using System.Text;

namespace XCommander.Services;

/// <summary>
/// Supported checksum algorithms
/// </summary>
public enum ChecksumAlgorithm
{
    MD5,
    SHA1,
    SHA256,
    SHA384,
    SHA512,
    CRC32
}

/// <summary>
/// Result of a checksum calculation
/// </summary>
public class ChecksumResult
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public ChecksumAlgorithm Algorithm { get; init; }
    public string Hash { get; init; } = string.Empty;
    public string HashUpperCase => Hash.ToUpperInvariant();
    public long FileSize { get; init; }
    public TimeSpan CalculationTime { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Progress information for checksum calculation
/// </summary>
public class ChecksumProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public double PercentComplete => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
}

/// <summary>
/// Service for calculating file checksums and verifying file integrity
/// </summary>
public interface IFileChecksumService
{
    /// <summary>
    /// Calculate checksum for a single file
    /// </summary>
    Task<ChecksumResult> CalculateChecksumAsync(
        string filePath, 
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate checksums for multiple files
    /// </summary>
    IAsyncEnumerable<ChecksumResult> CalculateChecksumsAsync(
        IEnumerable<string> filePaths,
        ChecksumAlgorithm algorithm,
        IProgress<ChecksumProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate multiple checksums for a single file
    /// </summary>
    Task<IReadOnlyList<ChecksumResult>> CalculateMultipleChecksumsAsync(
        string filePath,
        IEnumerable<ChecksumAlgorithm> algorithms,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify a file against an expected checksum
    /// </summary>
    Task<bool> VerifyChecksumAsync(
        string filePath,
        string expectedHash,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a checksum file (SFV, MD5SUM, SHA1SUM format)
    /// </summary>
    Task CreateChecksumFileAsync(
        IEnumerable<string> filePaths,
        string outputPath,
        ChecksumAlgorithm algorithm,
        IProgress<ChecksumProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify files against a checksum file
    /// </summary>
    Task<IReadOnlyList<ChecksumVerificationResult>> VerifyChecksumFileAsync(
        string checksumFilePath,
        IProgress<ChecksumProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Parse a checksum file and return the entries
    /// </summary>
    IReadOnlyList<ChecksumFileEntry> ParseChecksumFile(string checksumFilePath);
    
    /// <summary>
    /// Compare two files by checksum
    /// </summary>
    Task<bool> CompareFilesAsync(
        string filePath1,
        string filePath2,
        ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Entry from a checksum file
/// </summary>
public class ChecksumFileEntry
{
    public string FileName { get; init; } = string.Empty;
    public string ExpectedHash { get; init; } = string.Empty;
    public ChecksumAlgorithm Algorithm { get; init; }
}

/// <summary>
/// Result of verifying a file against expected checksum
/// </summary>
public class ChecksumVerificationResult
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string ExpectedHash { get; init; } = string.Empty;
    public string ActualHash { get; init; } = string.Empty;
    public ChecksumAlgorithm Algorithm { get; init; }
    public bool IsMatch { get; init; }
    public bool FileExists { get; init; }
    public string? ErrorMessage { get; init; }
}

public class FileChecksumService : IFileChecksumService
{
    private const int BufferSize = 81920; // 80KB buffer for streaming
    
    public async Task<ChecksumResult> CalculateChecksumAsync(
        string filePath,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            if (!File.Exists(filePath))
            {
                return new ChecksumResult
                {
                    FilePath = filePath,
                    Algorithm = algorithm,
                    Success = false,
                    ErrorMessage = "File not found"
                };
            }
            
            var fileInfo = new FileInfo(filePath);
            
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan | FileOptions.Asynchronous);
            
            var hash = algorithm switch
            {
                ChecksumAlgorithm.MD5 => await ComputeHashAsync<MD5>(stream, cancellationToken),
                ChecksumAlgorithm.SHA1 => await ComputeHashAsync<SHA1>(stream, cancellationToken),
                ChecksumAlgorithm.SHA256 => await ComputeHashAsync<SHA256>(stream, cancellationToken),
                ChecksumAlgorithm.SHA384 => await ComputeHashAsync<SHA384>(stream, cancellationToken),
                ChecksumAlgorithm.SHA512 => await ComputeHashAsync<SHA512>(stream, cancellationToken),
                ChecksumAlgorithm.CRC32 => await ComputeCrc32Async(stream, cancellationToken),
                _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}")
            };
            
            stopwatch.Stop();
            
            return new ChecksumResult
            {
                FilePath = filePath,
                Algorithm = algorithm,
                Hash = hash,
                FileSize = fileInfo.Length,
                CalculationTime = stopwatch.Elapsed,
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ChecksumResult
            {
                FilePath = filePath,
                Algorithm = algorithm,
                Success = false,
                ErrorMessage = ex.Message,
                CalculationTime = stopwatch.Elapsed
            };
        }
    }
    
    public async IAsyncEnumerable<ChecksumResult> CalculateChecksumsAsync(
        IEnumerable<string> filePaths,
        ChecksumAlgorithm algorithm,
        IProgress<ChecksumProgress>? progress = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var files = filePaths.ToList();
        var totalFiles = files.Count;
        var totalBytes = files.Where(File.Exists).Sum(f => new FileInfo(f).Length);
        var bytesProcessed = 0L;
        
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filePath = files[i];
            
            progress?.Report(new ChecksumProgress
            {
                CurrentFile = Path.GetFileName(filePath),
                FilesProcessed = i,
                TotalFiles = totalFiles,
                BytesProcessed = bytesProcessed,
                TotalBytes = totalBytes
            });
            
            var result = await CalculateChecksumAsync(filePath, algorithm, cancellationToken);
            bytesProcessed += result.FileSize;
            
            yield return result;
        }
        
        progress?.Report(new ChecksumProgress
        {
            CurrentFile = string.Empty,
            FilesProcessed = totalFiles,
            TotalFiles = totalFiles,
            BytesProcessed = totalBytes,
            TotalBytes = totalBytes
        });
    }
    
    public async Task<IReadOnlyList<ChecksumResult>> CalculateMultipleChecksumsAsync(
        string filePath,
        IEnumerable<ChecksumAlgorithm> algorithms,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ChecksumResult>();
        
        foreach (var algorithm in algorithms)
        {
            var result = await CalculateChecksumAsync(filePath, algorithm, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    public async Task<bool> VerifyChecksumAsync(
        string filePath,
        string expectedHash,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        var result = await CalculateChecksumAsync(filePath, algorithm, cancellationToken);
        
        if (!result.Success)
            return false;
        
        return string.Equals(result.Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
    
    public async Task CreateChecksumFileAsync(
        IEnumerable<string> filePaths,
        string outputPath,
        ChecksumAlgorithm algorithm,
        IProgress<ChecksumProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var baseDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        
        await foreach (var result in CalculateChecksumsAsync(filePaths, algorithm, progress, cancellationToken))
        {
            if (!result.Success)
                continue;
            
            var relativePath = Path.GetRelativePath(baseDir, result.FilePath);
            
            // Format depends on algorithm
            if (algorithm == ChecksumAlgorithm.CRC32)
            {
                // SFV format: filename hash
                sb.AppendLine($"{relativePath} {result.Hash}");
            }
            else
            {
                // BSD format: ALGORITHM (filename) = HASH
                // or GNU format: hash *filename
                sb.AppendLine($"{result.Hash} *{relativePath}");
            }
        }
        
        await File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken);
    }
    
    public async Task<IReadOnlyList<ChecksumVerificationResult>> VerifyChecksumFileAsync(
        string checksumFilePath,
        IProgress<ChecksumProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = ParseChecksumFile(checksumFilePath);
        var baseDir = Path.GetDirectoryName(checksumFilePath) ?? string.Empty;
        var results = new List<ChecksumVerificationResult>();
        
        var totalFiles = entries.Count;
        
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var entry = entries[i];
            var fullPath = Path.Combine(baseDir, entry.FileName);
            
            progress?.Report(new ChecksumProgress
            {
                CurrentFile = entry.FileName,
                FilesProcessed = i,
                TotalFiles = totalFiles
            });
            
            if (!File.Exists(fullPath))
            {
                results.Add(new ChecksumVerificationResult
                {
                    FilePath = fullPath,
                    ExpectedHash = entry.ExpectedHash,
                    ActualHash = string.Empty,
                    Algorithm = entry.Algorithm,
                    IsMatch = false,
                    FileExists = false,
                    ErrorMessage = "File not found"
                });
                continue;
            }
            
            var checksumResult = await CalculateChecksumAsync(fullPath, entry.Algorithm, cancellationToken);
            
            results.Add(new ChecksumVerificationResult
            {
                FilePath = fullPath,
                ExpectedHash = entry.ExpectedHash,
                ActualHash = checksumResult.Hash,
                Algorithm = entry.Algorithm,
                IsMatch = string.Equals(checksumResult.Hash, entry.ExpectedHash, StringComparison.OrdinalIgnoreCase),
                FileExists = true,
                ErrorMessage = checksumResult.ErrorMessage
            });
        }
        
        progress?.Report(new ChecksumProgress
        {
            CurrentFile = string.Empty,
            FilesProcessed = totalFiles,
            TotalFiles = totalFiles
        });
        
        return results;
    }
    
    public IReadOnlyList<ChecksumFileEntry> ParseChecksumFile(string checksumFilePath)
    {
        var entries = new List<ChecksumFileEntry>();
        var extension = Path.GetExtension(checksumFilePath).ToLowerInvariant();
        
        var algorithm = extension switch
        {
            ".md5" => ChecksumAlgorithm.MD5,
            ".sha1" => ChecksumAlgorithm.SHA1,
            ".sha256" => ChecksumAlgorithm.SHA256,
            ".sha384" => ChecksumAlgorithm.SHA384,
            ".sha512" => ChecksumAlgorithm.SHA512,
            ".sfv" => ChecksumAlgorithm.CRC32,
            _ => ChecksumAlgorithm.SHA256
        };
        
        foreach (var line in File.ReadAllLines(checksumFilePath))
        {
            var trimmedLine = line.Trim();
            
            // Skip comments and empty lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
                continue;
            
            // Try BSD format: ALGORITHM (filename) = HASH
            if (trimmedLine.Contains('(') && trimmedLine.Contains(')'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    trimmedLine, 
                    @"^(\w+)\s+\((.+)\)\s*=\s*([a-fA-F0-9]+)$");
                
                if (match.Success)
                {
                    var alg = match.Groups[1].Value.ToUpperInvariant() switch
                    {
                        "MD5" => ChecksumAlgorithm.MD5,
                        "SHA1" => ChecksumAlgorithm.SHA1,
                        "SHA256" => ChecksumAlgorithm.SHA256,
                        "SHA384" => ChecksumAlgorithm.SHA384,
                        "SHA512" => ChecksumAlgorithm.SHA512,
                        _ => algorithm
                    };
                    
                    entries.Add(new ChecksumFileEntry
                    {
                        FileName = match.Groups[2].Value,
                        ExpectedHash = match.Groups[3].Value,
                        Algorithm = alg
                    });
                    continue;
                }
            }
            
            // Try GNU format: hash *filename or hash filename
            var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var hash = parts[0];
                var fileName = parts[1].TrimStart('*', ' ');
                
                // Detect algorithm from hash length if not determined by extension
                var detectedAlgorithm = hash.Length switch
                {
                    8 => ChecksumAlgorithm.CRC32,
                    32 => ChecksumAlgorithm.MD5,
                    40 => ChecksumAlgorithm.SHA1,
                    64 => ChecksumAlgorithm.SHA256,
                    96 => ChecksumAlgorithm.SHA384,
                    128 => ChecksumAlgorithm.SHA512,
                    _ => algorithm
                };
                
                entries.Add(new ChecksumFileEntry
                {
                    FileName = fileName,
                    ExpectedHash = hash,
                    Algorithm = detectedAlgorithm
                });
            }
            // Try SFV format: filename hash (hash at end)
            else if (algorithm == ChecksumAlgorithm.CRC32)
            {
                var lastSpace = trimmedLine.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    entries.Add(new ChecksumFileEntry
                    {
                        FileName = trimmedLine[..lastSpace].Trim(),
                        ExpectedHash = trimmedLine[(lastSpace + 1)..].Trim(),
                        Algorithm = ChecksumAlgorithm.CRC32
                    });
                }
            }
        }
        
        return entries;
    }
    
    public async Task<bool> CompareFilesAsync(
        string filePath1,
        string filePath2,
        ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256,
        CancellationToken cancellationToken = default)
    {
        var result1 = await CalculateChecksumAsync(filePath1, algorithm, cancellationToken);
        var result2 = await CalculateChecksumAsync(filePath2, algorithm, cancellationToken);
        
        if (!result1.Success || !result2.Success)
            return false;
        
        return string.Equals(result1.Hash, result2.Hash, StringComparison.OrdinalIgnoreCase);
    }
    
    private static async Task<string> ComputeHashAsync<T>(Stream stream, CancellationToken cancellationToken) 
        where T : HashAlgorithm
    {
        using var algorithm = (T)typeof(T).GetMethod("Create", Type.EmptyTypes)!.Invoke(null, null)!;
        var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    private static async Task<string> ComputeCrc32Async(Stream stream, CancellationToken cancellationToken)
    {
        var crc = new Crc32();
        var buffer = new byte[BufferSize];
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            crc.Update(buffer, 0, bytesRead);
        }
        
        return crc.Value.ToString("x8");
    }
}

/// <summary>
/// CRC32 implementation (IEEE polynomial)
/// </summary>
internal class Crc32
{
    private static readonly uint[] Table = CreateTable();
    private uint _crc = 0xFFFFFFFF;
    
    public uint Value => _crc ^ 0xFFFFFFFF;
    
    private static uint[] CreateTable()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320;
        
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        
        return table;
    }
    
    public void Update(byte[] buffer, int offset, int count)
    {
        for (var i = offset; i < offset + count; i++)
        {
            _crc = Table[(_crc ^ buffer[i]) & 0xFF] ^ (_crc >> 8);
        }
    }
}
