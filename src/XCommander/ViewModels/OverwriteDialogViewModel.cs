using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Result of the overwrite dialog.
/// </summary>
public enum OverwriteDialogResult
{
    /// <summary>Overwrite this file</summary>
    Overwrite,
    /// <summary>Overwrite all subsequent conflicts</summary>
    OverwriteAll,
    /// <summary>Skip this file</summary>
    Skip,
    /// <summary>Skip all subsequent conflicts</summary>
    SkipAll,
    /// <summary>Rename this file (auto-generate new name)</summary>
    Rename,
    /// <summary>Rename all subsequent conflicts</summary>
    RenameAll,
    /// <summary>Cancel the entire operation</summary>
    Cancel,
    /// <summary>Compare files before deciding</summary>
    Compare,
    /// <summary>Overwrite only if source is newer</summary>
    OverwriteOlder,
    /// <summary>Append to existing file</summary>
    Append
}

/// <summary>
/// ViewModel for the TC-style file overwrite conflict dialog.
/// </summary>
public partial class OverwriteDialogViewModel : ViewModelBase
{
    public event EventHandler<OverwriteDialogResult>? RequestClose;
    
    // Source file info
    [ObservableProperty]
    private string _sourceFileName = string.Empty;
    
    [ObservableProperty]
    private string _sourceFilePath = string.Empty;
    
    [ObservableProperty]
    private long _sourceSize;
    
    [ObservableProperty]
    private DateTime _sourceModified;
    
    [ObservableProperty]
    private string _sourceSizeFormatted = string.Empty;
    
    [ObservableProperty]
    private bool _sourceIsImage;
    
    [ObservableProperty]
    private string? _sourceImagePath;
    
    // Destination file info
    [ObservableProperty]
    private string _destinationFileName = string.Empty;
    
    [ObservableProperty]
    private string _destinationFilePath = string.Empty;
    
    [ObservableProperty]
    private long _destinationSize;
    
    [ObservableProperty]
    private DateTime _destinationModified;
    
    [ObservableProperty]
    private string _destinationSizeFormatted = string.Empty;
    
    [ObservableProperty]
    private bool _destinationIsImage;
    
    [ObservableProperty]
    private string? _destinationImagePath;
    
    // Comparison results
    [ObservableProperty]
    private string _sizeComparison = string.Empty;
    
    [ObservableProperty]
    private string _dateComparison = string.Empty;
    
    [ObservableProperty]
    private bool _sourceIsNewer;
    
    [ObservableProperty]
    private bool _sourceIsLarger;
    
    // New name for rename option
    [ObservableProperty]
    private string _newFileName = string.Empty;
    
    // Remaining conflicts
    [ObservableProperty]
    private int _remainingConflicts;
    
    [ObservableProperty]
    private bool _hasMoreConflicts;
    
    // Remember choice
    [ObservableProperty]
    private bool _rememberChoice;
    
    public OverwriteDialogResult Result { get; private set; } = OverwriteDialogResult.Cancel;
    
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp", ".svg", ".tiff", ".tif"
    };
    
    public void Initialize(string sourcePath, string destinationPath, int remainingConflicts = 0)
    {
        SourceFilePath = sourcePath;
        DestinationFilePath = destinationPath;
        RemainingConflicts = remainingConflicts;
        HasMoreConflicts = remainingConflicts > 0;
        
        // Load source file info
        if (File.Exists(sourcePath))
        {
            var sourceInfo = new FileInfo(sourcePath);
            SourceFileName = sourceInfo.Name;
            SourceSize = sourceInfo.Length;
            SourceModified = sourceInfo.LastWriteTime;
            SourceSizeFormatted = FormatSize(sourceInfo.Length);
            
            var ext = sourceInfo.Extension;
            SourceIsImage = ImageExtensions.Contains(ext);
            if (SourceIsImage)
            {
                SourceImagePath = sourcePath;
            }
        }
        
        // Load destination file info
        if (File.Exists(destinationPath))
        {
            var destInfo = new FileInfo(destinationPath);
            DestinationFileName = destInfo.Name;
            DestinationSize = destInfo.Length;
            DestinationModified = destInfo.LastWriteTime;
            DestinationSizeFormatted = FormatSize(destInfo.Length);
            
            var ext = destInfo.Extension;
            DestinationIsImage = ImageExtensions.Contains(ext);
            if (DestinationIsImage)
            {
                DestinationImagePath = destinationPath;
            }
        }
        
        // Calculate comparisons
        SourceIsNewer = SourceModified > DestinationModified;
        SourceIsLarger = SourceSize > DestinationSize;
        
        if (SourceIsNewer)
            DateComparison = $"Source is newer by {(SourceModified - DestinationModified).TotalHours:N1} hours";
        else if (SourceModified < DestinationModified)
            DateComparison = $"Destination is newer by {(DestinationModified - SourceModified).TotalHours:N1} hours";
        else
            DateComparison = "Same modification time";
        
        var sizeDiff = Math.Abs(SourceSize - DestinationSize);
        if (SourceIsLarger)
            SizeComparison = $"Source is larger by {FormatSize(sizeDiff)}";
        else if (SourceSize < DestinationSize)
            SizeComparison = $"Destination is larger by {FormatSize(sizeDiff)}";
        else
            SizeComparison = "Same size";
        
        // Generate new filename for rename option
        NewFileName = GenerateNewFileName(destinationPath);
    }
    
    [RelayCommand]
    private void Overwrite()
    {
        Result = RememberChoice ? OverwriteDialogResult.OverwriteAll : OverwriteDialogResult.Overwrite;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void OverwriteAll()
    {
        Result = OverwriteDialogResult.OverwriteAll;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Skip()
    {
        Result = RememberChoice ? OverwriteDialogResult.SkipAll : OverwriteDialogResult.Skip;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void SkipAll()
    {
        Result = OverwriteDialogResult.SkipAll;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Rename()
    {
        Result = RememberChoice ? OverwriteDialogResult.RenameAll : OverwriteDialogResult.Rename;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void RenameAll()
    {
        Result = OverwriteDialogResult.RenameAll;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Cancel()
    {
        Result = OverwriteDialogResult.Cancel;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Compare()
    {
        Result = OverwriteDialogResult.Compare;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void OverwriteOlder()
    {
        Result = OverwriteDialogResult.OverwriteOlder;
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Append()
    {
        Result = OverwriteDialogResult.Append;
        RequestClose?.Invoke(this, Result);
    }
    
    private static string GenerateNewFileName(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        
        int counter = 1;
        string newName;
        do
        {
            newName = $"{nameWithoutExt} ({counter}){extension}";
            counter++;
        } while (File.Exists(Path.Combine(directory, newName)));
        
        return newName;
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
