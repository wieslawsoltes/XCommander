using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Helpers;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class DirectorySyncViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _leftPath = string.Empty;

    [ObservableProperty]
    private string _rightPath = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isSynchronizing;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private SyncDirection _syncDirection = SyncDirection.LeftToRight;

    [ObservableProperty]
    private bool _syncSubdirectories = true;

    [ObservableProperty]
    private bool _compareByContent;

    [ObservableProperty]
    private bool _deleteExtraFiles;

    [ObservableProperty]
    private string _fileFilter = "*.*";

    [ObservableProperty]
    private int _filesToCopy;

    [ObservableProperty]
    private int _filesToDelete;

    [ObservableProperty]
    private int _filesToUpdate;

    [ObservableProperty]
    private long _totalBytesToTransfer;

    public ObservableCollection<SyncItemViewModel> SyncItems { get; } = new();
    public ObservableCollection<SyncItemViewModel> SelectedItems { get; } = new();

    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }

    public string TotalBytesDisplay => FormatSize(TotalBytesToTransfer);
    public bool CanSync => SyncItems.Any(i => i.IsSelected && i.Action != SyncAction.None);

    public DirectorySyncViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
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
        var builder = DataGridColumnDefinitionBuilder.For<SyncItemViewModel>();

        IPropertyInfo leftSizeProperty = DataGridColumnHelper.CreateProperty(
            nameof(SyncItemViewModel.LeftSizeDisplay),
            (SyncItemViewModel item) => item.LeftSizeDisplay);
        IPropertyInfo rightSizeProperty = DataGridColumnHelper.CreateProperty(
            nameof(SyncItemViewModel.RightSizeDisplay),
            (SyncItemViewModel item) => item.RightSizeDisplay);
        IPropertyInfo leftModifiedProperty = DataGridColumnHelper.CreateProperty(
            nameof(SyncItemViewModel.LeftModifiedDisplay),
            (SyncItemViewModel item) => item.LeftModifiedDisplay);
        IPropertyInfo rightModifiedProperty = DataGridColumnHelper.CreateProperty(
            nameof(SyncItemViewModel.RightModifiedDisplay),
            (SyncItemViewModel item) => item.RightModifiedDisplay);

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: string.Empty,
                cellTemplateKey: "SyncItemSelectTemplate",
                configure: column =>
                {
                    column.ColumnKey = "select";
                    column.Width = new DataGridLength(32);
                    column.IsReadOnly = true;
                    column.CanUserSort = false;
                    column.ShowFilterButton = false;
                    column.ValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, bool>(
                        item => item.IsSelected);
                    column.ValueType = typeof(bool);
                }),
            builder.Template(
                header: "Name",
                cellTemplateKey: "SyncItemNameTemplate",
                configure: column =>
                {
                    column.ColumnKey = "name";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, string>(
                        item => item.RelativePath);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, string>(
                            item => item.RelativePath),
                        SearchTextProvider = item =>
                        {
                            if (item is not SyncItemViewModel syncItem)
                                return string.Empty;
                            return $"{syncItem.RelativePath} {syncItem.Status}";
                        }
                    };
                }),
            builder.Text(
                header: "Left Size",
                property: leftSizeProperty,
                getter: item => item.LeftSizeDisplay,
                configure: column =>
                {
                    column.ColumnKey = "left-size";
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, long>(
                            item => item.LeftSize)
                    };
                }),
            builder.Text(
                header: "Right Size",
                property: rightSizeProperty,
                getter: item => item.RightSizeDisplay,
                configure: column =>
                {
                    column.ColumnKey = "right-size";
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, long>(
                            item => item.RightSize)
                    };
                }),
            builder.Text(
                header: "Left Modified",
                property: leftModifiedProperty,
                getter: item => item.LeftModifiedDisplay,
                configure: column =>
                {
                    column.ColumnKey = "left-modified";
                    column.Width = new DataGridLength(140);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, DateTime>(
                            item => item.LeftModified)
                    };
                }),
            builder.Text(
                header: "Right Modified",
                property: rightModifiedProperty,
                getter: item => item.RightModifiedDisplay,
                configure: column =>
                {
                    column.ColumnKey = "right-modified";
                    column.Width = new DataGridLength(140);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, DateTime>(
                            item => item.RightModified)
                    };
                }),
            builder.Template(
                header: "Action",
                cellTemplateKey: "SyncItemActionTemplate",
                configure: column =>
                {
                    column.ColumnKey = "action";
                    column.Width = new DataGridLength(80);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, string>(
                        item => item.ActionDisplay);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SyncItemViewModel, SyncAction>(
                            item => item.Action),
                        SearchTextProvider = item =>
                        {
                            if (item is not SyncItemViewModel syncItem)
                                return string.Empty;
                            return syncItem.ActionDisplay;
                        }
                    };
                })
        };
    }

    public void Initialize(string leftPath, string rightPath)
    {
        LeftPath = leftPath;
        RightPath = rightPath;
    }

    [RelayCommand]
    public async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            Status = "Please specify both directories";
            return;
        }

        if (!Directory.Exists(LeftPath))
        {
            Status = "Left directory does not exist";
            return;
        }

        if (!Directory.Exists(RightPath))
        {
            Status = "Right directory does not exist";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsAnalyzing = true;
            SyncItems.Clear();
            FilesToCopy = 0;
            FilesToDelete = 0;
            FilesToUpdate = 0;
            TotalBytesToTransfer = 0;
            Status = "Analyzing directories...";

            await Task.Run(() => AnalyzeDirectories(token), token);

            Status = $"Analysis complete: {FilesToCopy} to copy, {FilesToUpdate} to update, {FilesToDelete} to delete";
        }
        catch (OperationCanceledException)
        {
            Status = "Analysis cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            OnPropertyChanged(nameof(CanSync));
        }
    }

    private void AnalyzeDirectories(CancellationToken token)
    {
        var searchOption = SyncSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var leftFiles = GetFileList(LeftPath, searchOption);
        var rightFiles = GetFileList(RightPath, searchOption);

        var allPaths = leftFiles.Keys.Union(rightFiles.Keys).OrderBy(p => p).ToList();
        int processed = 0;

        foreach (var relativePath in allPaths)
        {
            token.ThrowIfCancellationRequested();

            var leftExists = leftFiles.TryGetValue(relativePath, out var leftInfo);
            var rightExists = rightFiles.TryGetValue(relativePath, out var rightInfo);

            var item = new SyncItemViewModel
            {
                RelativePath = relativePath,
                LeftExists = leftExists,
                RightExists = rightExists,
                LeftSize = leftInfo?.Length ?? 0,
                RightSize = rightInfo?.Length ?? 0,
                LeftModified = leftInfo?.LastWriteTimeUtc ?? DateTime.MinValue,
                RightModified = rightInfo?.LastWriteTimeUtc ?? DateTime.MinValue,
                IsDirectory = (leftInfo ?? rightInfo)?.Attributes.HasFlag(FileAttributes.Directory) ?? false,
                IsSelected = true
            };

            DetermineAction(item);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SyncItems.Add(item);
                UpdateCounts(item);
            });

            processed++;
            Progress = (double)processed / allPaths.Count * 100;
        }
    }

    private Dictionary<string, FileInfo> GetFileList(string basePath, SearchOption searchOption)
    {
        var result = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            var patterns = FileFilter.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var file in Directory.EnumerateFiles(basePath, "*", searchOption))
            {
                var relativePath = Path.GetRelativePath(basePath, file);
                var fileName = Path.GetFileName(file);
                
                // Check if matches any pattern
                if (patterns.Any(p => MatchesPattern(fileName, p.Trim())))
                {
                    result[relativePath] = new FileInfo(file);
                }
            }
            
            // Include directories if syncing subdirectories
            if (SyncSubdirectories)
            {
                foreach (var dir in Directory.EnumerateDirectories(basePath, "*", searchOption))
                {
                    var relativePath = Path.GetRelativePath(basePath, dir);
                    result[relativePath] = new FileInfo(dir) { Attributes = FileAttributes.Directory };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scanning {basePath}: {ex.Message}");
        }
        
        return result;
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*" || pattern == "*.*")
            return true;
            
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
            
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private void DetermineAction(SyncItemViewModel item)
    {
        if (item.IsDirectory)
        {
            if (item.LeftExists && !item.RightExists && SyncDirection != SyncDirection.RightToLeft)
            {
                item.Action = SyncAction.CopyRight;
            }
            else if (!item.LeftExists && item.RightExists && SyncDirection != SyncDirection.LeftToRight)
            {
                item.Action = SyncAction.CopyLeft;
            }
            else if (DeleteExtraFiles)
            {
                if (!item.LeftExists && item.RightExists && SyncDirection == SyncDirection.LeftToRight)
                {
                    item.Action = SyncAction.DeleteRight;
                }
                else if (item.LeftExists && !item.RightExists && SyncDirection == SyncDirection.RightToLeft)
                {
                    item.Action = SyncAction.DeleteLeft;
                }
            }
            else
            {
                item.Action = SyncAction.None;
            }
            return;
        }

        // File handling
        switch (SyncDirection)
        {
            case SyncDirection.LeftToRight:
                HandleLeftToRight(item);
                break;
            case SyncDirection.RightToLeft:
                HandleRightToLeft(item);
                break;
            case SyncDirection.Bidirectional:
                HandleBidirectional(item);
                break;
        }
    }

    private void HandleLeftToRight(SyncItemViewModel item)
    {
        if (item.LeftExists && !item.RightExists)
        {
            item.Action = SyncAction.CopyRight;
        }
        else if (!item.LeftExists && item.RightExists)
        {
            item.Action = DeleteExtraFiles ? SyncAction.DeleteRight : SyncAction.None;
        }
        else if (item.LeftExists && item.RightExists)
        {
            if (item.LeftModified > item.RightModified || (CompareByContent && item.LeftSize != item.RightSize))
            {
                item.Action = SyncAction.UpdateRight;
            }
            else
            {
                item.Action = SyncAction.None;
            }
        }
    }

    private void HandleRightToLeft(SyncItemViewModel item)
    {
        if (item.RightExists && !item.LeftExists)
        {
            item.Action = SyncAction.CopyLeft;
        }
        else if (!item.RightExists && item.LeftExists)
        {
            item.Action = DeleteExtraFiles ? SyncAction.DeleteLeft : SyncAction.None;
        }
        else if (item.LeftExists && item.RightExists)
        {
            if (item.RightModified > item.LeftModified || (CompareByContent && item.LeftSize != item.RightSize))
            {
                item.Action = SyncAction.UpdateLeft;
            }
            else
            {
                item.Action = SyncAction.None;
            }
        }
    }

    private void HandleBidirectional(SyncItemViewModel item)
    {
        if (item.LeftExists && !item.RightExists)
        {
            item.Action = SyncAction.CopyRight;
        }
        else if (!item.LeftExists && item.RightExists)
        {
            item.Action = SyncAction.CopyLeft;
        }
        else if (item.LeftExists && item.RightExists)
        {
            if (item.LeftModified > item.RightModified)
            {
                item.Action = SyncAction.UpdateRight;
            }
            else if (item.RightModified > item.LeftModified)
            {
                item.Action = SyncAction.UpdateLeft;
            }
            else
            {
                item.Action = SyncAction.None;
            }
        }
    }

    private void UpdateCounts(SyncItemViewModel item)
    {
        switch (item.Action)
        {
            case SyncAction.CopyLeft:
            case SyncAction.CopyRight:
                FilesToCopy++;
                TotalBytesToTransfer += item.LeftExists ? item.LeftSize : item.RightSize;
                break;
            case SyncAction.UpdateLeft:
            case SyncAction.UpdateRight:
                FilesToUpdate++;
                TotalBytesToTransfer += item.LeftExists ? item.LeftSize : item.RightSize;
                break;
            case SyncAction.DeleteLeft:
            case SyncAction.DeleteRight:
                FilesToDelete++;
                break;
        }
        OnPropertyChanged(nameof(TotalBytesDisplay));
    }

    [RelayCommand]
    public async Task SynchronizeAsync()
    {
        var itemsToSync = SyncItems.Where(i => i.IsSelected && i.Action != SyncAction.None).ToList();
        if (itemsToSync.Count == 0)
        {
            Status = "No items to synchronize";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsSynchronizing = true;
            Progress = 0;
            int processed = 0;

            foreach (var item in itemsToSync)
            {
                token.ThrowIfCancellationRequested();
                
                Status = $"Processing: {item.RelativePath}";
                await ProcessSyncItemAsync(item);
                
                item.Status = "Done";
                processed++;
                Progress = (double)processed / itemsToSync.Count * 100;
            }

            Status = $"Synchronization complete: {processed} items processed";
        }
        catch (OperationCanceledException)
        {
            Status = "Synchronization cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsSynchronizing = false;
        }
    }

    private async Task ProcessSyncItemAsync(SyncItemViewModel item)
    {
        var leftFullPath = Path.Combine(LeftPath, item.RelativePath);
        var rightFullPath = Path.Combine(RightPath, item.RelativePath);

        await Task.Run(() =>
        {
            switch (item.Action)
            {
                case SyncAction.CopyLeft:
                    CopyFile(rightFullPath, leftFullPath, item.IsDirectory);
                    break;
                case SyncAction.CopyRight:
                    CopyFile(leftFullPath, rightFullPath, item.IsDirectory);
                    break;
                case SyncAction.UpdateLeft:
                    CopyFile(rightFullPath, leftFullPath, item.IsDirectory);
                    break;
                case SyncAction.UpdateRight:
                    CopyFile(leftFullPath, rightFullPath, item.IsDirectory);
                    break;
                case SyncAction.DeleteLeft:
                    DeleteItem(leftFullPath, item.IsDirectory);
                    break;
                case SyncAction.DeleteRight:
                    DeleteItem(rightFullPath, item.IsDirectory);
                    break;
            }
        });
    }

    private void CopyFile(string source, string destination, bool isDirectory)
    {
        var destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        if (isDirectory)
        {
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }
        }
        else
        {
            File.Copy(source, destination, true);
        }
    }

    private void DeleteItem(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        else
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in SyncItems)
        {
            item.IsSelected = true;
        }
        OnPropertyChanged(nameof(CanSync));
    }

    [RelayCommand]
    public void SelectNone()
    {
        foreach (var item in SyncItems)
        {
            item.IsSelected = false;
        }
        OnPropertyChanged(nameof(CanSync));
    }

    [RelayCommand]
    public void InvertSelection()
    {
        foreach (var item in SyncItems)
        {
            item.IsSelected = !item.IsSelected;
        }
        OnPropertyChanged(nameof(CanSync));
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

public enum SyncDirection
{
    LeftToRight,
    RightToLeft,
    Bidirectional
}

public enum SyncAction
{
    None,
    CopyLeft,
    CopyRight,
    UpdateLeft,
    UpdateRight,
    DeleteLeft,
    DeleteRight
}

public partial class SyncItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _leftExists;

    [ObservableProperty]
    private bool _rightExists;

    [ObservableProperty]
    private long _leftSize;

    [ObservableProperty]
    private long _rightSize;

    [ObservableProperty]
    private DateTime _leftModified;

    [ObservableProperty]
    private DateTime _rightModified;

    [ObservableProperty]
    private SyncAction _action = SyncAction.None;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private string _status = string.Empty;

    public string ActionDisplay => Action switch
    {
        SyncAction.CopyLeft => "← Copy",
        SyncAction.CopyRight => "Copy →",
        SyncAction.UpdateLeft => "← Update",
        SyncAction.UpdateRight => "Update →",
        SyncAction.DeleteLeft => "Delete ←",
        SyncAction.DeleteRight => "Delete →",
        _ => "-"
    };

    public string Icon => IsDirectory ? "📁" : "📄";

    public string LeftSizeDisplay => LeftExists && !IsDirectory ? FormatSize(LeftSize) : "-";
    public string RightSizeDisplay => RightExists && !IsDirectory ? FormatSize(RightSize) : "-";
    public string LeftModifiedDisplay => LeftExists ? LeftModified.ToString("yyyy-MM-dd HH:mm:ss") : "-";
    public string RightModifiedDisplay => RightExists ? RightModified.ToString("yyyy-MM-dd HH:mm:ss") : "-";

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
