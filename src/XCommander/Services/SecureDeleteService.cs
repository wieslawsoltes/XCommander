using System.Security.Cryptography;

namespace XCommander.Services;

/// <summary>
/// Secure deletion method
/// </summary>
public enum SecureDeleteMethod
{
    /// <summary>
    /// Single pass with zeros
    /// </summary>
    SinglePassZero,
    
    /// <summary>
    /// Single pass with random data
    /// </summary>
    SinglePassRandom,
    
    /// <summary>
    /// Three passes (random, random, zeros)
    /// </summary>
    ThreePass,
    
    /// <summary>
    /// DoD 5220.22-M (7 passes)
    /// </summary>
    DoD7Pass,
    
    /// <summary>
    /// Gutmann method (35 passes)
    /// </summary>
    Gutmann35Pass,
    
    /// <summary>
    /// Custom number of passes with random data
    /// </summary>
    CustomRandom
}

/// <summary>
/// Progress information for secure delete operations
/// </summary>
public class SecureDeleteProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public int CurrentPass { get; init; }
    public int TotalPasses { get; init; }
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public long BytesWritten { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesWritten / TotalBytes * 100 : 0;
}

/// <summary>
/// Result of a secure delete operation
/// </summary>
public class SecureDeleteResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int FilesDeleted { get; init; }
    public int FilesFailed { get; init; }
    public long BytesOverwritten { get; init; }
    public TimeSpan Duration { get; init; }
    public SecureDeleteMethod Method { get; init; }
    public IReadOnlyList<string> FailedFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Service for secure file deletion with overwriting
/// </summary>
public interface ISecureDeleteService
{
    /// <summary>
    /// Securely delete a single file
    /// </summary>
    Task<SecureDeleteResult> SecureDeleteFileAsync(
        string filePath,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Securely delete multiple files
    /// </summary>
    Task<SecureDeleteResult> SecureDeleteFilesAsync(
        IEnumerable<string> filePaths,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Securely delete a directory and all its contents
    /// </summary>
    Task<SecureDeleteResult> SecureDeleteDirectoryAsync(
        string directoryPath,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Wipe free space on a drive
    /// </summary>
    Task<SecureDeleteResult> WipeFreeSpaceAsync(
        string drivePath,
        SecureDeleteMethod method = SecureDeleteMethod.SinglePassZero,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the number of passes for a given method
    /// </summary>
    int GetPassCount(SecureDeleteMethod method, int customPasses = 3);
    
    /// <summary>
    /// Get description of the deletion method
    /// </summary>
    string GetMethodDescription(SecureDeleteMethod method);
}

public class SecureDeleteService : ISecureDeleteService
{
    private const int BufferSize = 65536; // 64KB buffer
    
    // Gutmann patterns (simplified - original uses specific patterns for magnetic media)
    private static readonly byte[][] GutmannPatterns = new byte[][]
    {
        new byte[] { 0x55 }, new byte[] { 0xAA }, new byte[] { 0x92, 0x49, 0x24 },
        new byte[] { 0x49, 0x24, 0x92 }, new byte[] { 0x24, 0x92, 0x49 }, new byte[] { 0x00 },
        new byte[] { 0x11 }, new byte[] { 0x22 }, new byte[] { 0x33 }, new byte[] { 0x44 },
        new byte[] { 0x55 }, new byte[] { 0x66 }, new byte[] { 0x77 }, new byte[] { 0x88 },
        new byte[] { 0x99 }, new byte[] { 0xAA }, new byte[] { 0xBB }, new byte[] { 0xCC },
        new byte[] { 0xDD }, new byte[] { 0xEE }, new byte[] { 0xFF }, new byte[] { 0x92, 0x49, 0x24 },
        new byte[] { 0x49, 0x24, 0x92 }, new byte[] { 0x24, 0x92, 0x49 }, new byte[] { 0x6D, 0xB6, 0xDB },
        new byte[] { 0xB6, 0xDB, 0x6D }, new byte[] { 0xDB, 0x6D, 0xB6 }
    };
    
    public async Task<SecureDeleteResult> SecureDeleteFileAsync(
        string filePath,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await SecureDeleteFilesAsync(
            new[] { filePath },
            method,
            customPasses,
            progress,
            cancellationToken);
    }
    
    public async Task<SecureDeleteResult> SecureDeleteFilesAsync(
        IEnumerable<string> filePaths,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var files = filePaths.Where(File.Exists).ToList();
        var failedFiles = new List<string>();
        var totalPasses = GetPassCount(method, customPasses);
        var totalBytes = files.Sum(f => new FileInfo(f).Length) * totalPasses;
        var bytesWritten = 0L;
        var filesDeleted = 0;
        
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var fileInfo = new FileInfo(file);
                var fileSize = fileInfo.Length;
                
                // Remove read-only attribute if present
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
                
                // Overwrite file with each pass
                for (var pass = 0; pass < totalPasses; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    progress?.Report(new SecureDeleteProgress
                    {
                        CurrentFile = Path.GetFileName(file),
                        CurrentPass = pass + 1,
                        TotalPasses = totalPasses,
                        FilesProcessed = i,
                        TotalFiles = files.Count,
                        BytesWritten = bytesWritten,
                        TotalBytes = totalBytes
                    });
                    
                    await OverwriteFileAsync(file, fileSize, method, pass, cancellationToken);
                    bytesWritten += fileSize;
                }
                
                // Truncate file to zero length
                await using (var fs = new FileStream(file, FileMode.Open, FileAccess.Write))
                {
                    fs.SetLength(0);
                }
                
                // Rename to random name before deletion (makes recovery harder)
                var directory = Path.GetDirectoryName(file) ?? ".";
                var randomName = Path.Combine(directory, Guid.NewGuid().ToString("N"));
                File.Move(file, randomName);
                
                // Finally delete
                File.Delete(randomName);
                filesDeleted++;
            }
            catch (Exception ex)
            {
                failedFiles.Add($"{file}: {ex.Message}");
            }
        }
        
        stopwatch.Stop();
        
        return new SecureDeleteResult
        {
            Success = failedFiles.Count == 0,
            ErrorMessage = failedFiles.Count > 0 ? $"{failedFiles.Count} files failed" : null,
            FilesDeleted = filesDeleted,
            FilesFailed = failedFiles.Count,
            BytesOverwritten = bytesWritten,
            Duration = stopwatch.Elapsed,
            Method = method,
            FailedFiles = failedFiles
        };
    }
    
    public async Task<SecureDeleteResult> SecureDeleteDirectoryAsync(
        string directoryPath,
        SecureDeleteMethod method = SecureDeleteMethod.ThreePass,
        int customPasses = 3,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new SecureDeleteResult
            {
                Success = false,
                ErrorMessage = "Directory not found",
                Method = method
            };
        }
        
        // Get all files recursively
        var files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories).ToList();
        
        // Securely delete all files
        var result = await SecureDeleteFilesAsync(files, method, customPasses, progress, cancellationToken);
        
        // Delete empty directories (bottom-up)
        var directories = Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length)
            .ToList();
        
        foreach (var dir in directories)
        {
            try
            {
                Directory.Delete(dir);
            }
            catch
            {
                // Ignore directory deletion errors
            }
        }
        
        // Delete root directory
        try
        {
            Directory.Delete(directoryPath);
        }
        catch
        {
            // Ignore
        }
        
        return result;
    }
    
    public async Task<SecureDeleteResult> WipeFreeSpaceAsync(
        string drivePath,
        SecureDeleteMethod method = SecureDeleteMethod.SinglePassZero,
        IProgress<SecureDeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var bytesWritten = 0L;
        var tempFiles = new List<string>();
        
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(drivePath) ?? drivePath);
            var tempDir = Path.Combine(driveInfo.RootDirectory.FullName, ".wipe_temp_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            
            var totalPasses = GetPassCount(method, 1);
            var fileIndex = 0;
            
            // Create files to fill free space
            while (driveInfo.AvailableFreeSpace > BufferSize * 10)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var tempFile = Path.Combine(tempDir, $"wipe_{fileIndex++}.tmp");
                tempFiles.Add(tempFile);
                
                try
                {
                    // Try to write a 100MB file
                    var targetSize = Math.Min(100 * 1024 * 1024, driveInfo.AvailableFreeSpace - BufferSize);
                    
                    await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
                    var buffer = new byte[BufferSize];
                    
                    for (var pass = 0; pass < totalPasses; pass++)
                    {
                        fs.Position = 0;
                        var written = 0L;
                        
                        FillBuffer(buffer, method, pass);
                        
                        while (written < targetSize && driveInfo.AvailableFreeSpace > BufferSize)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            var toWrite = (int)Math.Min(BufferSize, targetSize - written);
                            await fs.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken);
                            written += toWrite;
                            bytesWritten += toWrite;
                            
                            progress?.Report(new SecureDeleteProgress
                            {
                                CurrentFile = Path.GetFileName(tempFile),
                                CurrentPass = pass + 1,
                                TotalPasses = totalPasses,
                                FilesProcessed = fileIndex,
                                TotalFiles = 0, // Unknown
                                BytesWritten = bytesWritten,
                                TotalBytes = 0 // Unknown
                            });
                        }
                    }
                }
                catch (IOException)
                {
                    // Drive full, stop
                    break;
                }
            }
            
            // Delete all temp files
            foreach (var tempFile in tempFiles)
            {
                try { File.Delete(tempFile); } catch { }
            }
            
            try { Directory.Delete(tempDir); } catch { }
            
            stopwatch.Stop();
            
            return new SecureDeleteResult
            {
                Success = true,
                FilesDeleted = tempFiles.Count,
                BytesOverwritten = bytesWritten,
                Duration = stopwatch.Elapsed,
                Method = method
            };
        }
        catch (Exception ex)
        {
            // Cleanup
            foreach (var tempFile in tempFiles)
            {
                try { File.Delete(tempFile); } catch { }
            }
            
            stopwatch.Stop();
            
            return new SecureDeleteResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesOverwritten = bytesWritten,
                Duration = stopwatch.Elapsed,
                Method = method
            };
        }
    }
    
    public int GetPassCount(SecureDeleteMethod method, int customPasses = 3)
    {
        return method switch
        {
            SecureDeleteMethod.SinglePassZero => 1,
            SecureDeleteMethod.SinglePassRandom => 1,
            SecureDeleteMethod.ThreePass => 3,
            SecureDeleteMethod.DoD7Pass => 7,
            SecureDeleteMethod.Gutmann35Pass => 35,
            SecureDeleteMethod.CustomRandom => Math.Max(1, customPasses),
            _ => 3
        };
    }
    
    public string GetMethodDescription(SecureDeleteMethod method)
    {
        return method switch
        {
            SecureDeleteMethod.SinglePassZero => "Single pass with zeros - Fast but basic security",
            SecureDeleteMethod.SinglePassRandom => "Single pass with random data - Good for SSDs",
            SecureDeleteMethod.ThreePass => "Three passes (random, random, zeros) - Recommended",
            SecureDeleteMethod.DoD7Pass => "DoD 5220.22-M (7 passes) - Military standard",
            SecureDeleteMethod.Gutmann35Pass => "Gutmann (35 passes) - Maximum security, very slow",
            SecureDeleteMethod.CustomRandom => "Custom number of random passes",
            _ => "Unknown method"
        };
    }
    
    private async Task OverwriteFileAsync(string filePath, long fileSize, SecureDeleteMethod method, int pass, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None, BufferSize);
        
        var buffer = new byte[BufferSize];
        FillBuffer(buffer, method, pass);
        
        var remaining = fileSize;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var toWrite = (int)Math.Min(BufferSize, remaining);
            
            // For random methods, refresh random data each chunk
            if (method == SecureDeleteMethod.SinglePassRandom || 
                method == SecureDeleteMethod.CustomRandom ||
                (method == SecureDeleteMethod.ThreePass && pass < 2) ||
                (method == SecureDeleteMethod.DoD7Pass && (pass == 0 || pass == 2 || pass == 4 || pass == 6)))
            {
                RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
            }
            
            await fs.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken);
            remaining -= toWrite;
        }
        
        await fs.FlushAsync(cancellationToken);
    }
    
    private static void FillBuffer(byte[] buffer, SecureDeleteMethod method, int pass)
    {
        switch (method)
        {
            case SecureDeleteMethod.SinglePassZero:
                Array.Fill(buffer, (byte)0x00);
                break;
                
            case SecureDeleteMethod.SinglePassRandom:
            case SecureDeleteMethod.CustomRandom:
                RandomNumberGenerator.Fill(buffer);
                break;
                
            case SecureDeleteMethod.ThreePass:
                if (pass < 2)
                    RandomNumberGenerator.Fill(buffer);
                else
                    Array.Fill(buffer, (byte)0x00);
                break;
                
            case SecureDeleteMethod.DoD7Pass:
                // DoD pattern: 0x00, 0xFF, random, 0x00, 0xFF, random, random
                switch (pass)
                {
                    case 0:
                    case 3:
                        Array.Fill(buffer, (byte)0x00);
                        break;
                    case 1:
                    case 4:
                        Array.Fill(buffer, (byte)0xFF);
                        break;
                    default:
                        RandomNumberGenerator.Fill(buffer);
                        break;
                }
                break;
                
            case SecureDeleteMethod.Gutmann35Pass:
                if (pass < 4 || pass >= 31)
                {
                    // First 4 and last 4 passes are random
                    RandomNumberGenerator.Fill(buffer);
                }
                else
                {
                    // Use Gutmann patterns for middle passes
                    var patternIndex = (pass - 4) % GutmannPatterns.Length;
                    var pattern = GutmannPatterns[patternIndex];
                    for (var i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] = pattern[i % pattern.Length];
                    }
                }
                break;
                
            default:
                RandomNumberGenerator.Fill(buffer);
                break;
        }
    }
}
