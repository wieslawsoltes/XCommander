using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class FileSplitViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _sourceFile = string.Empty;

    [ObservableProperty]
    private string _destinationFolder = string.Empty;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private SplitSizeUnit _sizeUnit = SplitSizeUnit.MB;

    [ObservableProperty]
    private double _splitSize = 100;

    [ObservableProperty]
    private int _numberOfParts = 2;

    [ObservableProperty]
    private SplitMode _mode = SplitMode.BySize;

    [ObservableProperty]
    private long _sourceFileSize;

    [ObservableProperty]
    private int _estimatedParts;

    public string SourceFileSizeDisplay => FormatSize(SourceFileSize);
    public string SplitSizeDisplay => FormatSize(GetSplitSizeBytes());

    public FileSplitViewModel()
    {
    }

    public void Initialize(string sourceFile, string destinationFolder)
    {
        SourceFile = sourceFile;
        DestinationFolder = destinationFolder;
        
        if (File.Exists(sourceFile))
        {
            SourceFileSize = new FileInfo(sourceFile).Length;
            OnPropertyChanged(nameof(SourceFileSizeDisplay));
            UpdateEstimatedParts();
        }
    }

    partial void OnSplitSizeChanged(double value) => UpdateEstimatedParts();
    partial void OnSizeUnitChanged(SplitSizeUnit value) => UpdateEstimatedParts();
    partial void OnModeChanged(SplitMode value) => UpdateEstimatedParts();
    partial void OnNumberOfPartsChanged(int value) => UpdateEstimatedParts();

    private void UpdateEstimatedParts()
    {
        if (SourceFileSize <= 0)
        {
            EstimatedParts = 0;
            return;
        }

        if (Mode == SplitMode.BySize)
        {
            var splitBytes = GetSplitSizeBytes();
            EstimatedParts = splitBytes > 0 ? (int)Math.Ceiling((double)SourceFileSize / splitBytes) : 0;
        }
        else
        {
            EstimatedParts = NumberOfParts;
        }
        
        OnPropertyChanged(nameof(SplitSizeDisplay));
    }

    private long GetSplitSizeBytes()
    {
        if (Mode == SplitMode.ByParts && NumberOfParts > 0)
        {
            return (long)Math.Ceiling((double)SourceFileSize / NumberOfParts);
        }
        
        return SizeUnit switch
        {
            SplitSizeUnit.KB => (long)(SplitSize * 1024),
            SplitSizeUnit.MB => (long)(SplitSize * 1024 * 1024),
            SplitSizeUnit.GB => (long)(SplitSize * 1024 * 1024 * 1024),
            _ => (long)SplitSize
        };
    }

    [RelayCommand]
    public async Task SplitFileAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceFile) || !File.Exists(SourceFile))
        {
            Status = "Invalid source file";
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationFolder))
        {
            Status = "Invalid destination folder";
            return;
        }

        if (!Directory.Exists(DestinationFolder))
        {
            Directory.CreateDirectory(DestinationFolder);
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsProcessing = true;
            Progress = 0;
            Status = "Splitting file...";

            var splitSize = GetSplitSizeBytes();
            var baseName = Path.GetFileName(SourceFile);
            var partNumber = 1;
            long totalRead = 0;

            using var sourceStream = new FileStream(SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[81920]; // 80KB buffer

            while (totalRead < SourceFileSize)
            {
                token.ThrowIfCancellationRequested();

                var partPath = Path.Combine(DestinationFolder, $"{baseName}.{partNumber:D3}");
                var partSize = Math.Min(splitSize, SourceFileSize - totalRead);

                using var partStream = new FileStream(partPath, FileMode.Create, FileAccess.Write);
                long partWritten = 0;

                while (partWritten < partSize)
                {
                    token.ThrowIfCancellationRequested();

                    var toRead = (int)Math.Min(buffer.Length, partSize - partWritten);
                    var bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, toRead), token);
                    
                    if (bytesRead == 0) break;
                    
                    await partStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    
                    partWritten += bytesRead;
                    totalRead += bytesRead;
                    Progress = (double)totalRead / SourceFileSize * 100;
                    Status = $"Creating part {partNumber} ({Progress:F1}%)";
                }

                partNumber++;
            }

            // Create CRC file for verification
            await CreateCrcFileAsync(baseName, partNumber - 1, token);

            Status = $"Split complete: {partNumber - 1} parts created";
        }
        catch (OperationCanceledException)
        {
            Status = "Split cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task CreateCrcFileAsync(string baseName, int partCount, CancellationToken token)
    {
        var crcPath = Path.Combine(DestinationFolder, $"{baseName}.crc");
        var lines = new string[]
        {
            $"filename={baseName}",
            $"size={SourceFileSize}",
            $"parts={partCount}",
            $"date={DateTime.UtcNow:O}"
        };
        await File.WriteAllLinesAsync(crcPath, lines, token);
    }

    [RelayCommand]
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public partial class FileCombineViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _firstPartFile = string.Empty;

    [ObservableProperty]
    private string _destinationFile = string.Empty;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _deletePartsAfterCombine;

    public ObservableCollection<string> DetectedParts { get; } = new();
    
    [ObservableProperty]
    private long _totalSize;

    public string TotalSizeDisplay => FormatSize(TotalSize);
    public int PartCount => DetectedParts.Count;

    public FileCombineViewModel()
    {
    }

    public void Initialize(string firstPartPath, string destinationFolder)
    {
        FirstPartFile = firstPartPath;
        
        // Try to detect original filename
        var baseName = GetBaseNameFromPart(firstPartPath);
        if (!string.IsNullOrEmpty(baseName))
        {
            DestinationFile = Path.Combine(destinationFolder, baseName);
        }
        
        DetectParts();
    }

    private string GetBaseNameFromPart(string partPath)
    {
        var fileName = Path.GetFileName(partPath);
        // Remove .001, .002, etc. extension
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot > 0 && lastDot < fileName.Length - 1)
        {
            var extension = fileName.Substring(lastDot + 1);
            if (int.TryParse(extension, out _))
            {
                return fileName.Substring(0, lastDot);
            }
        }
        return fileName;
    }

    partial void OnFirstPartFileChanged(string value)
    {
        DetectParts();
    }

    public void DetectParts()
    {
        DetectedParts.Clear();
        TotalSize = 0;

        if (string.IsNullOrWhiteSpace(FirstPartFile) || !File.Exists(FirstPartFile))
            return;

        var directory = Path.GetDirectoryName(FirstPartFile);
        if (string.IsNullOrEmpty(directory))
            return;

        var baseName = GetBaseNameFromPart(FirstPartFile);
        var partNumber = 1;

        while (true)
        {
            var partPath = Path.Combine(directory, $"{baseName}.{partNumber:D3}");
            if (File.Exists(partPath))
            {
                DetectedParts.Add(partPath);
                TotalSize += new FileInfo(partPath).Length;
                partNumber++;
            }
            else
            {
                break;
            }
        }

        OnPropertyChanged(nameof(PartCount));
        OnPropertyChanged(nameof(TotalSizeDisplay));
        Status = DetectedParts.Count > 0 ? $"Found {DetectedParts.Count} parts" : "No parts found";
    }

    [RelayCommand]
    public async Task CombineFilesAsync()
    {
        if (DetectedParts.Count == 0)
        {
            Status = "No parts to combine";
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationFile))
        {
            Status = "Invalid destination file";
            return;
        }

        var destDir = Path.GetDirectoryName(DestinationFile);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsProcessing = true;
            Progress = 0;
            Status = "Combining files...";

            using var destStream = new FileStream(DestinationFile, FileMode.Create, FileAccess.Write);
            var buffer = new byte[81920];
            long totalWritten = 0;
            var partIndex = 0;

            foreach (var partPath in DetectedParts)
            {
                token.ThrowIfCancellationRequested();
                
                Status = $"Processing part {partIndex + 1} of {DetectedParts.Count}";
                
                using var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                int bytesRead;
                
                while ((bytesRead = await partStream.ReadAsync(buffer, token)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalWritten += bytesRead;
                    Progress = (double)totalWritten / TotalSize * 100;
                }
                
                partIndex++;
            }

            Status = "Combine complete";

            // Delete parts if requested
            if (DeletePartsAfterCombine)
            {
                await DeletePartsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Combine cancelled";
            // Clean up partial output
            if (File.Exists(DestinationFile))
            {
                try { File.Delete(DestinationFile); } catch { }
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task DeletePartsAsync()
    {
        Status = "Deleting parts...";
        
        foreach (var partPath in DetectedParts.ToList())
        {
            try
            {
                if (File.Exists(partPath))
                {
                    File.Delete(partPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting {partPath}: {ex.Message}");
            }
        }
        
        // Also delete CRC file if exists
        var directory = Path.GetDirectoryName(FirstPartFile);
        var baseName = GetBaseNameFromPart(FirstPartFile);
        var crcPath = Path.Combine(directory ?? "", $"{baseName}.crc");
        if (File.Exists(crcPath))
        {
            try { File.Delete(crcPath); } catch { }
        }
        
        await Task.CompletedTask;
        Status = "Parts deleted";
    }

    [RelayCommand]
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public enum SplitSizeUnit
{
    KB,
    MB,
    GB
}

public enum SplitMode
{
    BySize,
    ByParts
}
