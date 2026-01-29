using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Options for file overwrite handling (TC-style).
/// </summary>
public enum OverwriteMode
{
    /// <summary>Ask for each conflict</summary>
    Ask,
    /// <summary>Overwrite all without asking</summary>
    OverwriteAll,
    /// <summary>Overwrite only if source is newer</summary>
    OverwriteOlder,
    /// <summary>Skip existing files</summary>
    SkipExisting,
    /// <summary>Rename automatically (add number suffix)</summary>
    RenameAuto,
    /// <summary>Resume partial transfers</summary>
    Resume
}

/// <summary>
/// File operation type.
/// </summary>
public enum FileOperationType
{
    Copy,
    Move
}

/// <summary>
/// ViewModel for the TC-style Copy/Move dialog with detailed options.
/// </summary>
public partial class CopyMoveDialogViewModel : ViewModelBase
{
    private static readonly string RecentDestinationsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "recent_copy_move.json");

    public event EventHandler? RequestClose;
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;
    public event EventHandler? BrowseRequested;
    
    [ObservableProperty]
    private FileOperationType _operationType = FileOperationType.Copy;
    
    [ObservableProperty]
    private string _sourceDescription = string.Empty;
    
    [ObservableProperty]
    private string _destinationPath = string.Empty;
    
    [ObservableProperty]
    private OverwriteMode _overwriteMode = OverwriteMode.Ask;
    
    [ObservableProperty]
    private bool _preserveDateTime = true;
    
    [ObservableProperty]
    private bool _preserveAttributes = true;
    
    [ObservableProperty]
    private bool _verifyAfterCopy = false;
    
    [ObservableProperty]
    private bool _copyEmptyDirectories = true;
    
    [ObservableProperty]
    private bool _copySystemAndHidden = true;
    
    [ObservableProperty]
    private bool _useBackgroundTransfer = false;
    
    [ObservableProperty]
    private bool _followSymlinks = false;
    
    [ObservableProperty]
    private bool _createRelativeSymlinks = false;
    
    [ObservableProperty]
    private bool _useFastMove = true;
    
    [ObservableProperty]
    private bool _keepPartialOnError = false;
    
    [ObservableProperty]
    private bool _lowPriority = false;
    
    [ObservableProperty]
    private string _includeFilter = "*.*";
    
    [ObservableProperty]
    private string _excludeFilter = string.Empty;
    
    [ObservableProperty]
    private bool _queueOperation = false;
    
    [ObservableProperty]
    private int _selectedFilesCount;
    
    [ObservableProperty]
    private int _selectedFoldersCount;
    
    [ObservableProperty]
    private long _totalSize;
    
    [ObservableProperty]
    private string _totalSizeFormatted = "0 bytes";
    
    public ObservableCollection<string> SourcePaths { get; } = new();
    public ObservableCollection<string> RecentDestinations { get; } = new();

    public ObservableCollection<DataGridColumnDefinition> SourcePathColumnDefinitions { get; }
    public FilteringModel SourcePathFilteringModel { get; }
    public SortingModel SourcePathSortingModel { get; }
    public SearchModel SourcePathSearchModel { get; }
    
    public string DialogTitle => OperationType == FileOperationType.Copy ? "Copy" : "Move";
    public string ActionButtonText => OperationType == FileOperationType.Copy ? "Copy" : "Move";
    public bool IsCopyOperation => OperationType == FileOperationType.Copy;
    public bool IsMoveOperation => OperationType == FileOperationType.Move;
    
    public bool? DialogResult { get; private set; }
    
    public CopyMoveDialogViewModel()
    {
        SourcePathFilteringModel = new FilteringModel { OwnsViewFilter = true };
        SourcePathSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SourcePathSearchModel = new SearchModel();
        SourcePathColumnDefinitions = BuildSourcePathColumnDefinitions();

        // Load recent destinations from settings
        LoadRecentDestinations();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildSourcePathColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<string>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Source",
                cellTemplateKey: "SourcePathTemplate",
                configure: column =>
                {
                    column.ColumnKey = "source-path";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<string, string>(item => item);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<string, string>(item => item)
                    };
                })
        };
    }
    
    public void Initialize(IEnumerable<string> sourcePaths, string destination, FileOperationType operationType)
    {
        OperationType = operationType;
        DestinationPath = destination;
        
        SourcePaths.Clear();
        int fileCount = 0;
        int folderCount = 0;
        long totalSize = 0;
        
        foreach (var path in sourcePaths)
        {
            SourcePaths.Add(path);
            
            if (File.Exists(path))
            {
                fileCount++;
                try { totalSize += new FileInfo(path).Length; } catch { }
            }
            else if (Directory.Exists(path))
            {
                folderCount++;
                // Calculate folder size
                try
                {
                    totalSize += CalculateDirectorySize(path);
                }
                catch { }
            }
        }
        
        SelectedFilesCount = fileCount;
        SelectedFoldersCount = folderCount;
        TotalSize = totalSize;
        TotalSizeFormatted = FormatSize(totalSize);
        
        // Generate source description
        if (SourcePaths.Count == 1)
        {
            SourceDescription = Path.GetFileName(SourcePaths[0]);
        }
        else
        {
            SourceDescription = $"{fileCount} files, {folderCount} folders";
        }
        
        OnPropertyChanged(nameof(DialogTitle));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(IsCopyOperation));
        OnPropertyChanged(nameof(IsMoveOperation));
    }
    
    [RelayCommand]
    private void BrowseDestination()
    {
        // This will be handled by the view to show folder browser
        BrowseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void SwapSourceDestination()
    {
        if (SourcePaths.Count == 1 && Directory.Exists(SourcePaths[0]))
        {
            var temp = DestinationPath;
            DestinationPath = SourcePaths[0];
            SourcePaths.Clear();
            SourcePaths.Add(temp);
        }
    }
    
    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
            return;
        
        // Save destination to recent list
        AddToRecentDestinations(DestinationPath);
        
        DialogResult = true;
        Confirmed?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        Cancelled?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void Queue()
    {
        QueueOperation = true;
        Confirm();
    }
    
    private void LoadRecentDestinations()
    {
        RecentDestinations.Clear();

        try
        {
            if (File.Exists(RecentDestinationsPath))
            {
                var json = File.ReadAllText(RecentDestinationsPath);
                var recent = JsonSerializer.Deserialize<List<string>>(json) ?? [];
                foreach (var path in recent.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    RecentDestinations.Add(path);
                }
            }
        }
        catch
        {
            // Ignore errors and fall back to defaults.
        }

        if (RecentDestinations.Count == 0)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var downloadsDir = Path.Combine(homeDir, "Downloads");

            if (Directory.Exists(desktopDir)) RecentDestinations.Add(desktopDir);
            if (Directory.Exists(documentsDir)) RecentDestinations.Add(documentsDir);
            if (Directory.Exists(downloadsDir)) RecentDestinations.Add(downloadsDir);
        }
    }
    
    private void AddToRecentDestinations(string path)
    {
        if (RecentDestinations.Contains(path))
        {
            RecentDestinations.Remove(path);
        }
        RecentDestinations.Insert(0, path);
        
        // Keep only last 10
        while (RecentDestinations.Count > 10)
        {
            RecentDestinations.RemoveAt(RecentDestinations.Count - 1);
        }

        SaveRecentDestinations();
    }

    private void SaveRecentDestinations()
    {
        try
        {
            var directory = Path.GetDirectoryName(RecentDestinationsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(RecentDestinations.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(RecentDestinationsPath, json);
        }
        catch
        {
            // Ignore errors saving recent destinations.
        }
    }
    
    private static long CalculateDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return size;
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
}
