namespace XCommander.Models;

public class DriveItem
{
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public DriveType DriveType { get; init; }
    public string? VolumeLabel { get; init; }
    public long TotalSize { get; init; }
    public long AvailableFreeSpace { get; init; }
    public bool IsReady { get; init; }
    
    public string DisplayName => string.IsNullOrEmpty(VolumeLabel) 
        ? Name 
        : $"{VolumeLabel} ({Name})";
        
    public double UsedPercentage => TotalSize > 0 
        ? ((TotalSize - AvailableFreeSpace) / (double)TotalSize) * 100 
        : 0;
}
