using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class ChecksumViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _calculateMd5 = true;

    [ObservableProperty]
    private bool _calculateSha1 = true;

    [ObservableProperty]
    private bool _calculateSha256 = true;

    [ObservableProperty]
    private bool _calculateSha512;

    [ObservableProperty]
    private bool _calculateCrc32 = true;

    [ObservableProperty]
    private bool _uppercaseHex = true;

    [ObservableProperty]
    private string _verifyChecksum = string.Empty;

    [ObservableProperty]
    private string _verifyResult = string.Empty;

    public ObservableCollection<ChecksumResult> Results { get; } = new();

    public void Initialize(IEnumerable<string> filePaths)
    {
        Results.Clear();
        foreach (var path in filePaths)
        {
            if (File.Exists(path))
            {
                Results.Add(new ChecksumResult { FilePath = path, FileName = Path.GetFileName(path) });
            }
        }
    }

    [RelayCommand]
    private async Task CalculateAsync()
    {
        if (Results.Count == 0)
        {
            Status = "No files to calculate checksums for";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsCalculating = true;
            Progress = 0;
            Status = "Calculating checksums...";

            for (int i = 0; i < Results.Count; i++)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                var result = Results[i];
                Status = $"Processing: {result.FileName}";
                
                await CalculateFileChecksumsAsync(result, _cancellationTokenSource.Token);
                
                Progress = (i + 1.0) / Results.Count * 100;
            }

            Status = $"Completed. Calculated checksums for {Results.Count} file(s)";
            VerifyAgainstInput();
        }
        catch (OperationCanceledException)
        {
            Status = "Calculation cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    private async Task CalculateFileChecksumsAsync(ChecksumResult result, CancellationToken cancellationToken)
    {
        result.ClearChecksums();

        try
        {
            await using var stream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            var fileSize = stream.Length;
            result.FileSize = FormatSize(fileSize);

            // Calculate all checksums in parallel by reading file once
            var tasks = new List<Task>();
            
            if (CalculateMd5)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var md5 = MD5.Create();
                    await using var fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = await ComputeHashAsync(md5, fileStream, cancellationToken);
                    result.Md5 = FormatHash(hash);
                }, cancellationToken));
            }

            if (CalculateSha1)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var sha1 = SHA1.Create();
                    await using var fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = await ComputeHashAsync(sha1, fileStream, cancellationToken);
                    result.Sha1 = FormatHash(hash);
                }, cancellationToken));
            }

            if (CalculateSha256)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var sha256 = SHA256.Create();
                    await using var fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = await ComputeHashAsync(sha256, fileStream, cancellationToken);
                    result.Sha256 = FormatHash(hash);
                }, cancellationToken));
            }

            if (CalculateSha512)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var sha512 = SHA512.Create();
                    await using var fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = await ComputeHashAsync(sha512, fileStream, cancellationToken);
                    result.Sha512 = FormatHash(hash);
                }, cancellationToken));
            }

            if (CalculateCrc32)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var fileStream = new FileStream(result.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var crc = await ComputeCrc32Async(fileStream, cancellationToken);
                    result.Crc32 = UppercaseHex ? crc.ToString("X8") : crc.ToString("x8");
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
    }

    private async Task<byte[]> ComputeHashAsync(HashAlgorithm algorithm, Stream stream, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
        {
            algorithm.TransformBlock(buffer, 0, read, buffer, 0);
        }

        algorithm.TransformFinalBlock(buffer, 0, 0);
        return algorithm.Hash ?? Array.Empty<byte>();
    }

    private async Task<uint> ComputeCrc32Async(Stream stream, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];
        uint crc = 0xFFFFFFFF;
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    private string FormatHash(byte[] hash)
    {
        var sb = new StringBuilder(hash.Length * 2);
        var format = UppercaseHex ? "X2" : "x2";
        foreach (var b in hash)
        {
            sb.Append(b.ToString(format));
        }
        return sb.ToString();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    partial void OnVerifyChecksumChanged(string value)
    {
        VerifyAgainstInput();
    }

    partial void OnUppercaseHexChanged(bool value)
    {
        // Recalculate display format - the values are already stored and displayed correctly
        // No action needed as the hash values remain the same
    }

    private void VerifyAgainstInput()
    {
        if (string.IsNullOrWhiteSpace(VerifyChecksum))
        {
            VerifyResult = "";
            return;
        }

        var inputHash = VerifyChecksum.Trim().Replace(" ", "").Replace("-", "");
        
        foreach (var result in Results)
        {
            if (MatchesChecksum(result.Md5, inputHash))
            {
                VerifyResult = $"✓ MD5 match: {result.FileName}";
                return;
            }
            if (MatchesChecksum(result.Sha1, inputHash))
            {
                VerifyResult = $"✓ SHA1 match: {result.FileName}";
                return;
            }
            if (MatchesChecksum(result.Sha256, inputHash))
            {
                VerifyResult = $"✓ SHA256 match: {result.FileName}";
                return;
            }
            if (MatchesChecksum(result.Sha512, inputHash))
            {
                VerifyResult = $"✓ SHA512 match: {result.FileName}";
                return;
            }
            if (MatchesChecksum(result.Crc32, inputHash))
            {
                VerifyResult = $"✓ CRC32 match: {result.FileName}";
                return;
            }
        }

        VerifyResult = "✗ No match found";
    }

    private bool MatchesChecksum(string? computed, string input)
    {
        if (string.IsNullOrEmpty(computed))
            return false;
        return string.Equals(computed, input, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void CopyToClipboard(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            CopyToClipboardRequested?.Invoke(this, text);
        }
    }

    public event EventHandler<string>? CopyToClipboardRequested;

    [RelayCommand]
    private void ExportResults()
    {
        var sb = new StringBuilder();
        foreach (var result in Results)
        {
            sb.AppendLine($"File: {result.FilePath}");
            sb.AppendLine($"Size: {result.FileSize}");
            if (!string.IsNullOrEmpty(result.Md5))
                sb.AppendLine($"MD5: {result.Md5}");
            if (!string.IsNullOrEmpty(result.Sha1))
                sb.AppendLine($"SHA1: {result.Sha1}");
            if (!string.IsNullOrEmpty(result.Sha256))
                sb.AppendLine($"SHA256: {result.Sha256}");
            if (!string.IsNullOrEmpty(result.Sha512))
                sb.AppendLine($"SHA512: {result.Sha512}");
            if (!string.IsNullOrEmpty(result.Crc32))
                sb.AppendLine($"CRC32: {result.Crc32}");
            sb.AppendLine();
        }

        ExportRequested?.Invoke(this, sb.ToString());
    }

    public event EventHandler<string>? ExportRequested;
}

public partial class ChecksumResult : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    
    [ObservableProperty]
    private string _fileSize = string.Empty;
    
    [ObservableProperty]
    private string? _md5;
    
    [ObservableProperty]
    private string? _sha1;
    
    [ObservableProperty]
    private string? _sha256;
    
    [ObservableProperty]
    private string? _sha512;
    
    [ObservableProperty]
    private string? _crc32;
    
    [ObservableProperty]
    private string? _error;

    public void ClearChecksums()
    {
        Md5 = null;
        Sha1 = null;
        Sha256 = null;
        Sha512 = null;
        Crc32 = null;
        Error = null;
    }
}
