using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class CreateArchiveViewModel : ViewModelBase
{
    private readonly IArchiveService _archiveService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _archivePath = string.Empty;

    [ObservableProperty]
    private ArchiveType _selectedType = ArchiveType.Zip;

    [ObservableProperty]
    private CompressionLevel _selectedCompressionLevel = CompressionLevel.Normal;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private long _totalSize;

    public ObservableCollection<FileToArchive> Files { get; } = new();

    public ArchiveType[] ArchiveTypes => Enum.GetValues<ArchiveType>();
    public CompressionLevel[] CompressionLevels => Enum.GetValues<CompressionLevel>();

    public string TotalSizeDisplay => FormatSize(TotalSize);

    public CreateArchiveViewModel(IArchiveService archiveService)
    {
        _archiveService = archiveService;
    }

    public void Initialize(IEnumerable<string> paths, string defaultArchivePath)
    {
        Files.Clear();
        TotalSize = 0;

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                Files.Add(new FileToArchive
                {
                    FullPath = path,
                    Name = info.Name,
                    Size = info.Length,
                    IsDirectory = false
                });
                TotalSize += info.Length;
            }
            else if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                var size = GetDirectorySize(path);
                Files.Add(new FileToArchive
                {
                    FullPath = path,
                    Name = info.Name,
                    Size = size,
                    IsDirectory = true
                });
                TotalSize += size;
            }
        }

        // Set default archive name
        if (Files.Count == 1)
        {
            var name = Path.GetFileNameWithoutExtension(Files[0].Name);
            ArchivePath = Path.Combine(defaultArchivePath, name + ".zip");
        }
        else
        {
            ArchivePath = Path.Combine(defaultArchivePath, "archive.zip");
        }

        OnPropertyChanged(nameof(TotalSizeDisplay));
    }

    private long GetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    partial void OnSelectedTypeChanged(ArchiveType value)
    {
        // Update extension based on type
        var extension = value switch
        {
            ArchiveType.Zip => ".zip",
            ArchiveType.SevenZip => ".7z",
            ArchiveType.Tar => ".tar",
            ArchiveType.GZip => ".tar.gz",
            ArchiveType.BZip2 => ".tar.bz2",
            _ => ".zip"
        };

        if (!string.IsNullOrEmpty(ArchivePath))
        {
            var dir = Path.GetDirectoryName(ArchivePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(ArchivePath);
            // Remove any existing double extension
            if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);
            ArchivePath = Path.Combine(dir, name + extension);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrEmpty(ArchivePath) || Files.Count == 0)
        {
            Status = "Please specify archive path and add files";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsCreating = true;
            Progress = 0;
            Status = "Creating archive...";

            var sourcePaths = Files.Select(f => f.FullPath).ToList();

            var progressReporter = new Progress<ArchiveProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Adding: {p.CurrentEntry}";
            });

            await _archiveService.CreateArchiveAsync(
                ArchivePath, 
                sourcePaths, 
                SelectedType,
                SelectedCompressionLevel,
                progressReporter, 
                _cancellationTokenSource.Token);

            var fileInfo = new FileInfo(ArchivePath);
            var ratio = TotalSize > 0 ? (1 - (double)fileInfo.Length / TotalSize) * 100 : 0;
            Status = $"Archive created: {FormatSize(fileInfo.Length)} ({ratio:F1}% compression)";
            Progress = 100;
            
            ArchiveCreated?.Invoke(this, ArchivePath);
        }
        catch (OperationCanceledException)
        {
            Status = "Creation cancelled";
            // Delete partial archive
            if (File.Exists(ArchivePath))
            {
                try { File.Delete(ArchivePath); } catch { }
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void RemoveFile(FileToArchive? file)
    {
        if (file != null)
        {
            TotalSize -= file.Size;
            Files.Remove(file);
            OnPropertyChanged(nameof(TotalSizeDisplay));
        }
    }

    public event EventHandler<string>? ArchiveCreated;

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
}

public class FileToArchive
{
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDirectory { get; set; }

    public string SizeDisplay => IsDirectory ? "<DIR>" : FormatSize(Size);
    public string Icon => IsDirectory ? "📁" : "📄";

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
}
