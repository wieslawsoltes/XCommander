using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XCommander.Controls;

public partial class StatusBar : UserControl
{
    public static readonly StyledProperty<string> SelectionInfoProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(SelectionInfo), "0 of 0 files selected");
    
    public static readonly StyledProperty<string> SizeInfoProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(SizeInfo), string.Empty);
    
    public static readonly StyledProperty<string> FreeSpaceProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(FreeSpace), string.Empty);
    
    public static readonly StyledProperty<int> FileCountProperty =
        AvaloniaProperty.Register<StatusBar, int>(nameof(FileCount), 0);
    
    public static readonly StyledProperty<int> FolderCountProperty =
        AvaloniaProperty.Register<StatusBar, int>(nameof(FolderCount), 0);
    
    public static readonly StyledProperty<bool> HasFilesProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(HasFiles), false);
    
    public static readonly StyledProperty<bool> HasFoldersProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(HasFolders), false);
    
    public static readonly StyledProperty<double> DiskUsageWidthProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(DiskUsageWidth), 0);
    
    public static readonly StyledProperty<IBrush> DiskUsageColorProperty =
        AvaloniaProperty.Register<StatusBar, IBrush>(nameof(DiskUsageColor));
    
    public static readonly StyledProperty<bool> IsOperationActiveProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(IsOperationActive), false);
    
    public static readonly StyledProperty<string> OperationStatusProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(OperationStatus), string.Empty);
    
    public static readonly StyledProperty<string> GitBranchProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(GitBranch), string.Empty);
    
    public string SelectionInfo
    {
        get => GetValue(SelectionInfoProperty);
        set => SetValue(SelectionInfoProperty, value);
    }
    
    public string SizeInfo
    {
        get => GetValue(SizeInfoProperty);
        set => SetValue(SizeInfoProperty, value);
    }
    
    public string FreeSpace
    {
        get => GetValue(FreeSpaceProperty);
        set => SetValue(FreeSpaceProperty, value);
    }
    
    public int FileCount
    {
        get => GetValue(FileCountProperty);
        set => SetValue(FileCountProperty, value);
    }
    
    public int FolderCount
    {
        get => GetValue(FolderCountProperty);
        set => SetValue(FolderCountProperty, value);
    }
    
    public bool HasFiles
    {
        get => GetValue(HasFilesProperty);
        set => SetValue(HasFilesProperty, value);
    }
    
    public bool HasFolders
    {
        get => GetValue(HasFoldersProperty);
        set => SetValue(HasFoldersProperty, value);
    }
    
    public double DiskUsageWidth
    {
        get => GetValue(DiskUsageWidthProperty);
        set => SetValue(DiskUsageWidthProperty, value);
    }
    
    public IBrush DiskUsageColor
    {
        get => GetValue(DiskUsageColorProperty);
        set => SetValue(DiskUsageColorProperty, value);
    }
    
    public bool IsOperationActive
    {
        get => GetValue(IsOperationActiveProperty);
        set => SetValue(IsOperationActiveProperty, value);
    }
    
    public string OperationStatus
    {
        get => GetValue(OperationStatusProperty);
        set => SetValue(OperationStatusProperty, value);
    }
    
    public string GitBranch
    {
        get => GetValue(GitBranchProperty);
        set => SetValue(GitBranchProperty, value);
    }
    
    public StatusBar()
    {
        InitializeComponent();
        DiskUsageColor = new SolidColorBrush(Color.FromRgb(60, 120, 180));
    }
    
    public void UpdateSelection(int selectedCount, int totalCount, long selectedSize, int fileCount = 0, int folderCount = 0)
    {
        SelectionInfo = selectedCount > 0 
            ? $"{selectedCount} of {totalCount} selected"
            : $"{totalCount} items";
        SizeInfo = selectedCount > 0 ? FormatSize(selectedSize) : string.Empty;
        
        FileCount = fileCount;
        FolderCount = folderCount;
        HasFiles = fileCount > 0;
        HasFolders = folderCount > 0;
    }
    
    public void UpdateFreeSpace(string path)
    {
        try
        {
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(path) ?? path);
            if (driveInfo.IsReady)
            {
                var total = driveInfo.TotalSize;
                var free = driveInfo.AvailableFreeSpace;
                var used = total - free;
                var usagePercent = (double)used / total;
                
                FreeSpace = $"{FormatSize(free)} free";
                
                // Calculate width for 40px progress bar
                DiskUsageWidth = usagePercent * 40;
                
                // Color based on usage level
                DiskUsageColor = usagePercent switch
                {
                    > 0.9 => new SolidColorBrush(Color.FromRgb(220, 60, 60)),   // Red - critical
                    > 0.75 => new SolidColorBrush(Color.FromRgb(220, 160, 60)), // Orange - warning
                    _ => new SolidColorBrush(Color.FromRgb(60, 160, 60))        // Green - OK
                };
            }
        }
        catch
        {
            FreeSpace = string.Empty;
            DiskUsageWidth = 0;
        }
    }
    
    public void SetOperationStatus(bool active, string status = "")
    {
        IsOperationActive = active;
        OperationStatus = status;
    }
    
    public void UpdateGitStatus(string? branch)
    {
        GitBranch = branch ?? string.Empty;
    }
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
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
