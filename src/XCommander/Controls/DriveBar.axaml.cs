using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace XCommander.Controls;

public partial class DriveBar : UserControl
{
    public static readonly StyledProperty<ICommand?> SelectDriveCommandProperty =
        AvaloniaProperty.Register<DriveBar, ICommand?>(nameof(SelectDriveCommand));
    
    public ICommand? SelectDriveCommand
    {
        get => GetValue(SelectDriveCommandProperty);
        set => SetValue(SelectDriveCommandProperty, value);
    }
    
    public ObservableCollection<DriveDisplayItem> Drives { get; } = new();
    
    public DriveBar()
    {
        InitializeComponent();
        RefreshDrives();
    }
    
    public void RefreshDrives()
    {
        Drives.Clear();
        
        try
        {
            var drives = System.IO.DriveInfo.GetDrives();
            
            foreach (var drive in drives.Where(d => d.IsReady))
            {
                var item = new DriveDisplayItem
                {
                    RootPath = drive.RootDirectory.FullName,
                    DisplayName = GetDisplayName(drive),
                    Icon = GetDriveIcon(drive.DriveType),
                    ToolTip = GetToolTip(drive)
                };
                Drives.Add(item);
            }
        }
        catch
        {
            // On some systems, drive enumeration might fail
            // Add at least the root
            if (OperatingSystem.IsWindows())
            {
                Drives.Add(new DriveDisplayItem
                {
                    RootPath = "C:\\",
                    DisplayName = "C:",
                    Icon = "💾",
                    ToolTip = "Local Disk (C:)"
                });
            }
            else
            {
                Drives.Add(new DriveDisplayItem
                {
                    RootPath = "/",
                    DisplayName = "/",
                    Icon = "💾",
                    ToolTip = "Root"
                });
                
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Drives.Add(new DriveDisplayItem
                {
                    RootPath = homeDir,
                    DisplayName = "~",
                    Icon = "🏠",
                    ToolTip = $"Home ({homeDir})"
                });
            }
        }
    }
    
    private static string GetDisplayName(System.IO.DriveInfo drive)
    {
        if (OperatingSystem.IsWindows())
        {
            return drive.Name.TrimEnd('\\');
        }
        
        return drive.RootDirectory.FullName == "/" ? "/" : drive.Name;
    }
    
    private static string GetDriveIcon(System.IO.DriveType driveType)
    {
        return driveType switch
        {
            System.IO.DriveType.Removable => "💿",
            System.IO.DriveType.Fixed => "💾",
            System.IO.DriveType.Network => "🌐",
            System.IO.DriveType.CDRom => "📀",
            System.IO.DriveType.Ram => "⚡",
            _ => "📁"
        };
    }
    
    private static string GetToolTip(System.IO.DriveInfo drive)
    {
        try
        {
            var label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
            var total = FormatSize(drive.TotalSize);
            var free = FormatSize(drive.AvailableFreeSpace);
            return $"{label} ({drive.Name.TrimEnd('\\')})\nTotal: {total}\nFree: {free}";
        }
        catch
        {
            return drive.Name;
        }
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

public class DriveDisplayItem
{
    public string RootPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public string ToolTip { get; set; } = string.Empty;
}
