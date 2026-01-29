using System;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class DirectoryCompareViewModel : ViewModelBase
{
    private const string SelectedColumnKey = "selected";
    private const string StatusColumnKey = "status";
    private const string PathColumnKey = "path";
    private const string LeftSizeColumnKey = "left-size";
    private const string LeftDateColumnKey = "left-date";
    private const string RightSizeColumnKey = "right-size";
    private const string RightDateColumnKey = "right-date";

    private readonly IFileSystemService _fileSystemService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _leftPath = string.Empty;

    [ObservableProperty]
    private string _rightPath = string.Empty;

    [ObservableProperty]
    private bool _isComparing;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private bool _showOnlyDifferences = true;

    [ObservableProperty]
    private bool _compareSubdirectories = true;

    [ObservableProperty]
    private bool _compareByContent;

    [ObservableProperty]
    private bool _compareByDate = true;

    [ObservableProperty]
    private bool _compareBySize = true;

    [ObservableProperty]
    private bool _ignoreCase = true;

    [ObservableProperty]
    private int _leftOnlyCount;

    [ObservableProperty]
    private int _rightOnlyCount;

    [ObservableProperty]
    private int _differentCount;

    [ObservableProperty]
    private int _identicalCount;

    public ObservableCollection<CompareResultItem> Results { get; } = new();
    public DataGridCollectionView ResultsView { get; }
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }

    public DirectoryCompareViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        ResultsView = new DataGridCollectionView(Results);
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<CompareResultItem>();

        IPropertyInfo isSelectedProperty = CreateProperty(
            nameof(CompareResultItem.IsSelected),
            item => item.IsSelected,
            (item, value) => item.IsSelected = value);
        IPropertyInfo statusProperty = CreateProperty(
            nameof(CompareResultItem.StatusDisplay),
            item => item.StatusDisplay);
        IPropertyInfo pathProperty = CreateProperty(
            nameof(CompareResultItem.RelativePath),
            item => item.RelativePath);
        IPropertyInfo leftSizeProperty = CreateProperty(
            nameof(CompareResultItem.LeftSizeDisplay),
            item => item.LeftSizeDisplay);
        IPropertyInfo leftDateProperty = CreateProperty(
            nameof(CompareResultItem.LeftDateDisplay),
            item => item.LeftDateDisplay);
        IPropertyInfo rightSizeProperty = CreateProperty(
            nameof(CompareResultItem.RightSizeDisplay),
            item => item.RightSizeDisplay);
        IPropertyInfo rightDateProperty = CreateProperty(
            nameof(CompareResultItem.RightDateDisplay),
            item => item.RightDateDisplay);

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.CheckBox(
                header: string.Empty,
                property: isSelectedProperty,
                getter: item => item.IsSelected,
                setter: (item, value) => item.IsSelected = value,
                configure: column =>
                {
                    column.ColumnKey = SelectedColumnKey;
                    column.Width = new DataGridLength(30);
                    column.CanUserSort = false;
                    column.ShowFilterButton = false;
                }),
            builder.Text(
                header: "Status",
                property: statusProperty,
                getter: item => item.StatusDisplay,
                configure: column =>
                {
                    column.ColumnKey = StatusColumnKey;
                    column.Width = new DataGridLength(50);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Path",
                property: pathProperty,
                getter: item => item.RelativePath,
                configure: column =>
                {
                    column.ColumnKey = PathColumnKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Left Size",
                property: leftSizeProperty,
                getter: item => item.LeftSizeDisplay,
                configure: column =>
                {
                    column.ColumnKey = LeftSizeColumnKey;
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Left Date",
                property: leftDateProperty,
                getter: item => item.LeftDateDisplay,
                configure: column =>
                {
                    column.ColumnKey = LeftDateColumnKey;
                    column.Width = new DataGridLength(140);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Right Size",
                property: rightSizeProperty,
                getter: item => item.RightSizeDisplay,
                configure: column =>
                {
                    column.ColumnKey = RightSizeColumnKey;
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Right Date",
                property: rightDateProperty,
                getter: item => item.RightDateDisplay,
                configure: column =>
                {
                    column.ColumnKey = RightDateColumnKey;
                    column.Width = new DataGridLength(140);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                })
        };
    }

    private static IPropertyInfo CreateProperty<TValue>(
        string name,
        Func<CompareResultItem, TValue> getter,
        Action<CompareResultItem, TValue>? setter = null)
    {
        return new ClrPropertyInfo(
            name,
            target => getter((CompareResultItem)target),
            setter == null
                ? null
                : (target, value) => setter((CompareResultItem)target, value is null ? default! : (TValue)value),
            typeof(TValue));
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            Status = "Please specify both directories";
            return;
        }

        if (!Directory.Exists(LeftPath))
        {
            Status = $"Left directory does not exist: {LeftPath}";
            return;
        }

        if (!Directory.Exists(RightPath))
        {
            Status = $"Right directory does not exist: {RightPath}";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsComparing = true;
            Results.Clear();
            Progress = 0;
            LeftOnlyCount = 0;
            RightOnlyCount = 0;
            DifferentCount = 0;
            IdenticalCount = 0;
            Status = "Comparing directories...";

            await Task.Run(() => CompareDirectoriesAsync(LeftPath, RightPath, "", _cancellationTokenSource.Token));

            Status = $"Comparison complete. Left only: {LeftOnlyCount}, Right only: {RightOnlyCount}, Different: {DifferentCount}, Identical: {IdenticalCount}";
        }
        catch (OperationCanceledException)
        {
            Status = "Comparison cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsComparing = false;
            Progress = 100;
        }
    }

    private async Task CompareDirectoriesAsync(string leftDir, string rightDir, string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stringComparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // Get files in both directories
        var leftFiles = GetFilesSafe(leftDir)
            .Select(f => Path.GetFileName(f))
            .ToHashSet(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var rightFiles = GetFilesSafe(rightDir)
            .Select(f => Path.GetFileName(f))
            .ToHashSet(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        // Files only in left
        foreach (var file in leftFiles.Except(rightFiles))
        {
            var fullPath = Path.Combine(leftDir, file);
            var relPath = string.IsNullOrEmpty(relativePath) ? file : Path.Combine(relativePath, file);
            var item = new CompareResultItem
            {
                RelativePath = relPath,
                LeftPath = fullPath,
                RightPath = null,
                Status = CompareStatus.LeftOnly,
                IsDirectory = false
            };

            try
            {
                var info = new FileInfo(fullPath);
                item.LeftSize = info.Length;
                item.LeftDate = info.LastWriteTime;
            }
            catch { }

            AddResult(item);
            LeftOnlyCount++;
        }

        // Files only in right
        foreach (var file in rightFiles.Except(leftFiles))
        {
            var fullPath = Path.Combine(rightDir, file);
            var relPath = string.IsNullOrEmpty(relativePath) ? file : Path.Combine(relativePath, file);
            var item = new CompareResultItem
            {
                RelativePath = relPath,
                LeftPath = null,
                RightPath = fullPath,
                Status = CompareStatus.RightOnly,
                IsDirectory = false
            };

            try
            {
                var info = new FileInfo(fullPath);
                item.RightSize = info.Length;
                item.RightDate = info.LastWriteTime;
            }
            catch { }

            AddResult(item);
            RightOnlyCount++;
        }

        // Files in both
        foreach (var file in leftFiles.Intersect(rightFiles))
        {
            var leftFullPath = Path.Combine(leftDir, file);
            var rightFullPath = Path.Combine(rightDir, file);
            var relPath = string.IsNullOrEmpty(relativePath) ? file : Path.Combine(relativePath, file);

            await CompareFilesAsync(leftFullPath, rightFullPath, relPath, cancellationToken);
        }

        // Process subdirectories if enabled
        if (CompareSubdirectories)
        {
            var leftDirs = GetDirectoriesSafe(leftDir)
                .Select(d => Path.GetFileName(d))
                .ToHashSet(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            var rightDirs = GetDirectoriesSafe(rightDir)
                .Select(d => Path.GetFileName(d))
                .ToHashSet(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            // Directories only in left
            foreach (var dir in leftDirs.Except(rightDirs))
            {
                var fullPath = Path.Combine(leftDir, dir);
                var relPath = string.IsNullOrEmpty(relativePath) ? dir : Path.Combine(relativePath, dir);
                var item = new CompareResultItem
                {
                    RelativePath = relPath,
                    LeftPath = fullPath,
                    RightPath = null,
                    Status = CompareStatus.LeftOnly,
                    IsDirectory = true
                };
                AddResult(item);
                LeftOnlyCount++;

                // Recursively add all items in left-only directory
                await AddDirectoryContentsAsync(fullPath, relPath, true, cancellationToken);
            }

            // Directories only in right
            foreach (var dir in rightDirs.Except(leftDirs))
            {
                var fullPath = Path.Combine(rightDir, dir);
                var relPath = string.IsNullOrEmpty(relativePath) ? dir : Path.Combine(relativePath, dir);
                var item = new CompareResultItem
                {
                    RelativePath = relPath,
                    LeftPath = null,
                    RightPath = fullPath,
                    Status = CompareStatus.RightOnly,
                    IsDirectory = true
                };
                AddResult(item);
                RightOnlyCount++;

                // Recursively add all items in right-only directory
                await AddDirectoryContentsAsync(fullPath, relPath, false, cancellationToken);
            }

            // Directories in both
            foreach (var dir in leftDirs.Intersect(rightDirs))
            {
                var leftSubDir = Path.Combine(leftDir, dir);
                var rightSubDir = Path.Combine(rightDir, dir);
                var relPath = string.IsNullOrEmpty(relativePath) ? dir : Path.Combine(relativePath, dir);

                await CompareDirectoriesAsync(leftSubDir, rightSubDir, relPath, cancellationToken);
            }
        }
    }

    private async Task CompareFilesAsync(string leftPath, string rightPath, string relativePath, CancellationToken cancellationToken)
    {
        var item = new CompareResultItem
        {
            RelativePath = relativePath,
            LeftPath = leftPath,
            RightPath = rightPath,
            IsDirectory = false
        };

        try
        {
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);

            item.LeftSize = leftInfo.Length;
            item.RightSize = rightInfo.Length;
            item.LeftDate = leftInfo.LastWriteTime;
            item.RightDate = rightInfo.LastWriteTime;

            bool isDifferent = false;

            if (CompareBySize && leftInfo.Length != rightInfo.Length)
            {
                isDifferent = true;
            }

            if (CompareByDate && !isDifferent)
            {
                var timeDiff = Math.Abs((leftInfo.LastWriteTime - rightInfo.LastWriteTime).TotalSeconds);
                if (timeDiff > 2) // Allow 2 second tolerance
                {
                    isDifferent = true;
                }
            }

            if (CompareByContent && !isDifferent)
            {
                isDifferent = !await CompareFileContentsAsync(leftPath, rightPath, cancellationToken);
            }

            item.Status = isDifferent ? CompareStatus.Different : CompareStatus.Identical;

            if (isDifferent)
            {
                DifferentCount++;
            }
            else
            {
                IdenticalCount++;
            }
        }
        catch (Exception ex)
        {
            item.Status = CompareStatus.Error;
            item.ErrorMessage = ex.Message;
            DifferentCount++;
        }

        if (!ShowOnlyDifferences || item.Status != CompareStatus.Identical)
        {
            AddResult(item);
        }
    }

    private async Task<bool> CompareFileContentsAsync(string leftPath, string rightPath, CancellationToken cancellationToken)
    {
        const int bufferSize = 4096;
        var leftBuffer = new byte[bufferSize];
        var rightBuffer = new byte[bufferSize];

        await using var leftStream = new FileStream(leftPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var rightStream = new FileStream(rightPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leftRead = await leftStream.ReadAsync(leftBuffer.AsMemory(0, bufferSize), cancellationToken);
            var rightRead = await rightStream.ReadAsync(rightBuffer.AsMemory(0, bufferSize), cancellationToken);

            if (leftRead != rightRead)
                return false;

            if (leftRead == 0)
                return true;

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
                return false;
        }
    }

    private async Task AddDirectoryContentsAsync(string directory, string relativePath, bool isLeft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var file in GetFilesSafe(directory))
        {
            var fileName = Path.GetFileName(file);
            var relPath = Path.Combine(relativePath, fileName);
            var item = new CompareResultItem
            {
                RelativePath = relPath,
                LeftPath = isLeft ? file : null,
                RightPath = isLeft ? null : file,
                Status = isLeft ? CompareStatus.LeftOnly : CompareStatus.RightOnly,
                IsDirectory = false
            };

            try
            {
                var info = new FileInfo(file);
                if (isLeft)
                {
                    item.LeftSize = info.Length;
                    item.LeftDate = info.LastWriteTime;
                }
                else
                {
                    item.RightSize = info.Length;
                    item.RightDate = info.LastWriteTime;
                }
            }
            catch { }

            AddResult(item);
            if (isLeft) LeftOnlyCount++; else RightOnlyCount++;
        }

        foreach (var subDir in GetDirectoriesSafe(directory))
        {
            var dirName = Path.GetFileName(subDir);
            var relPath = Path.Combine(relativePath, dirName);
            var item = new CompareResultItem
            {
                RelativePath = relPath,
                LeftPath = isLeft ? subDir : null,
                RightPath = isLeft ? null : subDir,
                Status = isLeft ? CompareStatus.LeftOnly : CompareStatus.RightOnly,
                IsDirectory = true
            };
            AddResult(item);
            if (isLeft) LeftOnlyCount++; else RightOnlyCount++;

            await AddDirectoryContentsAsync(subDir, relPath, isLeft, cancellationToken);
        }
    }

    private string[] GetFilesSafe(string directory)
    {
        try
        {
            return Directory.GetFiles(directory);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private string[] GetDirectoriesSafe(string directory)
    {
        try
        {
            return Directory.GetDirectories(directory);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void AddResult(CompareResultItem item)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Results.Add(item));
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void CopyToRight()
    {
        // Copy selected items from left to right
        var itemsToCopy = Results.Where(r => r.IsSelected && r.LeftPath != null).ToList();
        foreach (var item in itemsToCopy)
        {
            try
            {
                var destPath = item.RightPath ?? Path.Combine(RightPath, item.RelativePath);
                if (item.IsDirectory)
                {
                    if (!Directory.Exists(destPath))
                        Directory.CreateDirectory(destPath);
                }
                else
                {
                    var destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(item.LeftPath!, destPath, true);
                }
            }
            catch { }
        }
        Status = $"Copied {itemsToCopy.Count} items to right";
    }

    [RelayCommand]
    private void CopyToLeft()
    {
        // Copy selected items from right to left
        var itemsToCopy = Results.Where(r => r.IsSelected && r.RightPath != null).ToList();
        foreach (var item in itemsToCopy)
        {
            try
            {
                var destPath = item.LeftPath ?? Path.Combine(LeftPath, item.RelativePath);
                if (item.IsDirectory)
                {
                    if (!Directory.Exists(destPath))
                        Directory.CreateDirectory(destPath);
                }
                else
                {
                    var destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(item.RightPath!, destPath, true);
                }
            }
            catch { }
        }
        Status = $"Copied {itemsToCopy.Count} items to left";
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Results)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var item in Results)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SelectLeftOnly()
    {
        foreach (var item in Results)
        {
            item.IsSelected = item.Status == CompareStatus.LeftOnly;
        }
    }

    [RelayCommand]
    private void SelectRightOnly()
    {
        foreach (var item in Results)
        {
            item.IsSelected = item.Status == CompareStatus.RightOnly;
        }
    }

    [RelayCommand]
    private void SelectDifferent()
    {
        foreach (var item in Results)
        {
            item.IsSelected = item.Status == CompareStatus.Different;
        }
    }
}

public enum CompareStatus
{
    Identical,
    Different,
    LeftOnly,
    RightOnly,
    Error
}

public partial class CompareResultItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string RelativePath { get; set; } = string.Empty;
    public string? LeftPath { get; set; }
    public string? RightPath { get; set; }
    public long? LeftSize { get; set; }
    public long? RightSize { get; set; }
    public DateTime? LeftDate { get; set; }
    public DateTime? RightDate { get; set; }
    public CompareStatus Status { get; set; }
    public bool IsDirectory { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusDisplay => Status switch
    {
        CompareStatus.Identical => "=",
        CompareStatus.Different => "≠",
        CompareStatus.LeftOnly => "←",
        CompareStatus.RightOnly => "→",
        CompareStatus.Error => "!",
        _ => "?"
    };

    public string LeftSizeDisplay => LeftSize.HasValue ? FormatSize(LeftSize.Value) : "";
    public string RightSizeDisplay => RightSize.HasValue ? FormatSize(RightSize.Value) : "";
    public string LeftDateDisplay => LeftDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    public string RightDateDisplay => RightDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

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
