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

public partial class ArchiveViewModel : ViewModelBase
{
    private readonly IArchiveService _archiveService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _archivePath = string.Empty;

    [ObservableProperty]
    private string _archiveName = string.Empty;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isOperationInProgress;

    [ObservableProperty]
    private long _totalSize;

    [ObservableProperty]
    private long _totalCompressedSize;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _folderCount;

    public ObservableCollection<ArchiveEntryViewModel> Entries { get; } = new();
    public ObservableCollection<ArchiveEntryViewModel> SelectedEntries { get; } = new();

    public string TotalSizeDisplay => FormatSize(TotalSize);
    public string TotalCompressedSizeDisplay => FormatSize(TotalCompressedSize);
    public double CompressionRatio => TotalSize > 0 ? (1 - (double)TotalCompressedSize / TotalSize) * 100 : 0;
    public string CompressionRatioDisplay => $"{CompressionRatio:F1}%";

    public ArchiveViewModel(IArchiveService archiveService)
    {
        _archiveService = archiveService;
    }

    public async Task LoadArchiveAsync(string path)
    {
        if (!File.Exists(path) || !_archiveService.IsArchive(path))
        {
            Status = "Invalid archive file";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            ArchivePath = path;
            ArchiveName = Path.GetFileName(path);
            CurrentPath = "";
            Entries.Clear();
            Status = "Loading archive...";

            var entries = await _archiveService.ListEntriesAsync(path, _cancellationTokenSource.Token);

            TotalSize = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
            TotalCompressedSize = entries.Where(e => !e.IsDirectory).Sum(e => e.CompressedSize);
            FileCount = entries.Count(e => !e.IsDirectory);
            FolderCount = entries.Count(e => e.IsDirectory);

            // Build hierarchy for current path
            UpdateVisibleEntries(entries, "");

            Status = $"Loaded {FileCount} files, {FolderCount} folders";
            OnPropertyChanged(nameof(TotalSizeDisplay));
            OnPropertyChanged(nameof(TotalCompressedSizeDisplay));
            OnPropertyChanged(nameof(CompressionRatioDisplay));
        }
        catch (OperationCanceledException)
        {
            Status = "Loading cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private System.Collections.Generic.List<ArchiveEntry>? _allEntries;

    private void UpdateVisibleEntries(System.Collections.Generic.List<ArchiveEntry> entries, string path)
    {
        _allEntries = entries;
        Entries.Clear();

        // Add parent directory entry if not at root
        if (!string.IsNullOrEmpty(path))
        {
            Entries.Add(new ArchiveEntryViewModel
            {
                Name = "..",
                IsDirectory = true,
                IsParent = true,
                Path = GetParentPath(path)
            });
        }

        var normalizedPath = path.TrimEnd('/');
        var prefix = string.IsNullOrEmpty(normalizedPath) ? "" : normalizedPath + "/";

        // Get direct children of current path
        var directChildren = entries
            .Where(e => 
            {
                var entryPath = e.Path.TrimEnd('/');
                if (string.IsNullOrEmpty(prefix))
                {
                    // At root - show entries without slashes or just one level deep
                    return !entryPath.Contains('/') || 
                           entryPath.IndexOf('/') == entryPath.LastIndexOf('/');
                }
                else
                {
                    // Show entries that start with prefix and have no additional slashes
                    if (!entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return false;
                    var remainder = entryPath.Substring(prefix.Length);
                    return !remainder.Contains('/');
                }
            })
            .GroupBy(e => 
            {
                var entryPath = e.Path.TrimEnd('/');
                if (string.IsNullOrEmpty(prefix))
                {
                    var slashIndex = entryPath.IndexOf('/');
                    return slashIndex >= 0 ? entryPath.Substring(0, slashIndex) : entryPath;
                }
                else
                {
                    var remainder = entryPath.Substring(prefix.Length);
                    var slashIndex = remainder.IndexOf('/');
                    return slashIndex >= 0 ? remainder.Substring(0, slashIndex) : remainder;
                }
            })
            .Select(g => g.FirstOrDefault(e => e.IsDirectory) ?? g.First())
            .OrderBy(e => !e.IsDirectory)
            .ThenBy(e => e.Name);

        foreach (var entry in directChildren)
        {
            Entries.Add(new ArchiveEntryViewModel
            {
                Name = entry.Name,
                Path = entry.Path,
                IsDirectory = entry.IsDirectory,
                Size = entry.Size,
                CompressedSize = entry.CompressedSize,
                LastModified = entry.LastModified,
                IsEncrypted = entry.IsEncrypted,
                CompressionRatio = entry.CompressionRatio
            });
        }
    }

    private string GetParentPath(string path)
    {
        var normalized = path.TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized.Substring(0, lastSlash) : "";
    }

    [RelayCommand]
    private void NavigateTo(ArchiveEntryViewModel? entry)
    {
        if (entry == null)
            return;

        if (entry.IsParent)
        {
            CurrentPath = entry.Path;
            if (_allEntries != null)
                UpdateVisibleEntries(_allEntries, CurrentPath);
        }
        else if (entry.IsDirectory)
        {
            CurrentPath = entry.Path;
            if (_allEntries != null)
                UpdateVisibleEntries(_allEntries, CurrentPath);
        }
    }

    [RelayCommand]
    public async Task ExtractAllAsync(string destinationPath)
    {
        if (string.IsNullOrEmpty(ArchivePath))
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsOperationInProgress = true;
            Progress = 0;
            Status = "Extracting...";

            var progressReporter = new Progress<ArchiveProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Extracting: {p.CurrentEntry}";
            });

            await _archiveService.ExtractAllAsync(ArchivePath, destinationPath, progressReporter, _cancellationTokenSource.Token);

            Status = "Extraction complete";
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            Status = "Extraction cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    [RelayCommand]
    public async Task ExtractSelectedAsync(string destinationPath)
    {
        if (string.IsNullOrEmpty(ArchivePath) || SelectedEntries.Count == 0)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsOperationInProgress = true;
            Progress = 0;
            Status = "Extracting selected...";

            var entryPaths = SelectedEntries.Select(e => e.Path).ToList();

            var progressReporter = new Progress<ArchiveProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Extracting: {p.CurrentEntry}";
            });

            await _archiveService.ExtractEntriesAsync(ArchivePath, entryPaths, destinationPath, progressReporter, _cancellationTokenSource.Token);

            Status = $"Extracted {entryPaths.Count} entries";
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            Status = "Extraction cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    [RelayCommand]
    private async Task TestArchiveAsync()
    {
        if (string.IsNullOrEmpty(ArchivePath))
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsOperationInProgress = true;
            Status = "Testing archive...";

            var result = await _archiveService.TestArchiveAsync(ArchivePath, _cancellationTokenSource.Token);

            Status = result ? "Archive is OK" : "Archive is corrupted";
        }
        catch (OperationCanceledException)
        {
            Status = "Test cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var entry in Entries.Where(e => !e.IsParent))
        {
            entry.IsSelected = true;
            if (!SelectedEntries.Contains(entry))
                SelectedEntries.Add(entry);
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var entry in Entries)
        {
            entry.IsSelected = false;
        }
        SelectedEntries.Clear();
    }
    
    [RelayCommand]
    public async Task AddFilesToArchiveAsync(IEnumerable<string> filePaths)
    {
        if (string.IsNullOrEmpty(ArchivePath))
            return;
        
        // Only ZIP archives support adding files
        if (!Path.GetExtension(ArchivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Status = "Adding files is only supported for ZIP archives";
            return;
        }
        
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            IsOperationInProgress = true;
            Progress = 0;
            Status = "Adding files to archive...";
            
            var progressReporter = new Progress<ArchiveProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Adding: {p.CurrentEntry}";
            });
            
            await _archiveService.AddToArchiveAsync(ArchivePath, filePaths, progressReporter, _cancellationTokenSource.Token);
            
            // Reload the archive to show updated contents
            await LoadArchiveAsync(ArchivePath);
            
            Status = "Files added successfully";
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            Status = "Operation cancelled";
        }
        catch (NotSupportedException ex)
        {
            Status = ex.Message;
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }
    
    [RelayCommand]
    public async Task DeleteSelectedFromArchiveAsync()
    {
        if (string.IsNullOrEmpty(ArchivePath) || SelectedEntries.Count == 0)
            return;
        
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            IsOperationInProgress = true;
            Status = "Deleting entries...";
            
            var entryPaths = SelectedEntries.Select(e => e.Path).ToList();
            
            await _archiveService.DeleteEntriesAsync(ArchivePath, entryPaths, _cancellationTokenSource.Token);
            
            // Reload the archive to show updated contents
            await LoadArchiveAsync(ArchivePath);
            
            Status = $"Deleted {entryPaths.Count} entries";
        }
        catch (OperationCanceledException)
        {
            Status = "Operation cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }
    
    public event EventHandler? AddFilesRequested;
    
    [RelayCommand]
    public void RequestAddFiles()
    {
        AddFilesRequested?.Invoke(this, EventArgs.Empty);
    }

#pragma warning disable CS0067 // Events reserved for future use
    public event EventHandler<string>? ExtractRequested;
    public event EventHandler<(string ArchivePath, ArchiveType Type)>? CreateArchiveRequested;
#pragma warning restore CS0067

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

public partial class ArchiveEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsParent { get; set; }
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsEncrypted { get; set; }
    public double CompressionRatio { get; set; }

    public string SizeDisplay => IsDirectory ? "<DIR>" : FormatSize(Size);
    public string CompressedSizeDisplay => IsDirectory ? "" : FormatSize(CompressedSize);
    public string RatioDisplay => IsDirectory ? "" : $"{CompressionRatio:F1}%";
    public string DateDisplay => LastModified?.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string Icon => IsParent ? "⬆️" : IsDirectory ? "📁" : "📄";

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
