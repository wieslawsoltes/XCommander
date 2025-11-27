using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Delete mode for file deletion.
/// </summary>
public enum DeleteMode
{
    /// <summary>Move to recycle bin / trash (recoverable)</summary>
    RecycleBin,
    /// <summary>Permanently delete files</summary>
    Permanent,
    /// <summary>Secure delete (overwrite with random data)</summary>
    SecureDelete
}

/// <summary>
/// Result of the delete dialog.
/// </summary>
public enum DeleteDialogResult
{
    Delete,
    Cancel
}

/// <summary>
/// Item to be deleted in the delete confirmation dialog.
/// </summary>
public partial class DeleteItemInfo : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    [ObservableProperty]
    private bool _isDirectory;
    
    [ObservableProperty]
    private long _size;
    
    [ObservableProperty]
    private string _sizeFormatted = string.Empty;
    
    [ObservableProperty]
    private int _fileCount;
    
    [ObservableProperty]
    private int _folderCount;
    
    [ObservableProperty]
    private DateTime _dateModified;
    
    [ObservableProperty]
    private bool _isSelected = true;
    
    public string Icon => IsDirectory ? "📁" : GetFileIcon(System.IO.Path.GetExtension(Name));
    
    private static string GetFileIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".log" => "📝",
            ".doc" or ".docx" or ".pdf" => "📄",
            ".xls" or ".xlsx" or ".csv" => "📊",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".ogg" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".exe" or ".dll" or ".app" => "⚙️",
            ".cs" or ".js" or ".py" or ".java" => "💻",
            _ => "📄"
        };
    }
}

/// <summary>
/// ViewModel for the enhanced TC-style delete confirmation dialog.
/// </summary>
public partial class DeleteConfirmationViewModel : ViewModelBase
{
    public event EventHandler<DeleteDialogResult>? RequestClose;
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;
    
    [ObservableProperty]
    private DeleteMode _deleteMode = DeleteMode.RecycleBin;
    
    [ObservableProperty]
    private bool _deleteReadOnly = false;
    
    [ObservableProperty]
    private bool _deleteHidden = false;
    
    /// <summary>
    /// Number of wipe passes for secure delete (default 3).
    /// </summary>
    [ObservableProperty]
    private int _wipePassCount = 3;
    
    [ObservableProperty]
    private bool _showDeleteConfirmation = true;
    
    [ObservableProperty]
    private int _totalFileCount;
    
    [ObservableProperty]
    private int _totalFolderCount;
    
    [ObservableProperty]
    private long _totalSize;
    
    [ObservableProperty]
    private string _totalSizeFormatted = "0 bytes";
    
    [ObservableProperty]
    private bool _hasReadOnlyFiles;
    
    [ObservableProperty]
    private bool _hasHiddenFiles;
    
    [ObservableProperty]
    private bool _hasSystemFiles;
    
    [ObservableProperty]
    private string _warningMessage = string.Empty;
    
    [ObservableProperty]
    private bool _isCalculatingSize = false;
    
    public ObservableCollection<DeleteItemInfo> Items { get; } = new();
    
    public bool IsRecycleBinMode => DeleteMode == DeleteMode.RecycleBin;
    public bool IsPermanentMode => DeleteMode == DeleteMode.Permanent;
    public bool IsSecureDeleteMode => DeleteMode == DeleteMode.SecureDelete;
    
    public DeleteDialogResult Result { get; private set; } = DeleteDialogResult.Cancel;
    
    public async Task InitializeAsync(IEnumerable<string> paths)
    {
        Items.Clear();
        TotalFileCount = 0;
        TotalFolderCount = 0;
        TotalSize = 0;
        HasReadOnlyFiles = false;
        HasHiddenFiles = false;
        HasSystemFiles = false;
        
        IsCalculatingSize = true;
        
        var tasks = new List<Task>();
        
        foreach (var path in paths)
        {
            var itemInfo = new DeleteItemInfo
            {
                FullPath = path,
                Name = Path.GetFileName(path)
            };
            
            Items.Add(itemInfo);
            
            // Calculate stats in background
            tasks.Add(Task.Run(() => CalculateItemStats(itemInfo)));
        }
        
        await Task.WhenAll(tasks);
        
        // Update totals
        TotalFileCount = Items.Where(i => !i.IsDirectory).Count() + Items.Where(i => i.IsDirectory).Sum(i => i.FileCount);
        TotalFolderCount = Items.Count(i => i.IsDirectory) + Items.Where(i => i.IsDirectory).Sum(i => i.FolderCount);
        TotalSize = Items.Sum(i => i.Size);
        TotalSizeFormatted = FormatSize(TotalSize);
        
        // Generate warning message
        GenerateWarningMessage();
        
        IsCalculatingSize = false;
    }
    
    private void CalculateItemStats(DeleteItemInfo item)
    {
        try
        {
            if (File.Exists(item.FullPath))
            {
                var fileInfo = new FileInfo(item.FullPath);
                item.IsDirectory = false;
                item.Size = fileInfo.Length;
                item.SizeFormatted = FormatSize(fileInfo.Length);
                item.DateModified = fileInfo.LastWriteTime;
                item.FileCount = 1;
                
                CheckFileAttributes(fileInfo.Attributes);
            }
            else if (Directory.Exists(item.FullPath))
            {
                var dirInfo = new DirectoryInfo(item.FullPath);
                item.IsDirectory = true;
                item.DateModified = dirInfo.LastWriteTime;
                
                CheckFileAttributes(dirInfo.Attributes);
                
                // Calculate directory contents
                long size = 0;
                int fileCount = 0;
                int folderCount = 0;
                
                try
                {
                    foreach (var file in Directory.EnumerateFiles(item.FullPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            size += fi.Length;
                            fileCount++;
                            CheckFileAttributes(fi.Attributes);
                        }
                        catch { }
                    }
                    
                    folderCount = Directory.EnumerateDirectories(item.FullPath, "*", SearchOption.AllDirectories).Count();
                }
                catch { }
                
                item.Size = size;
                item.SizeFormatted = FormatSize(size);
                item.FileCount = fileCount;
                item.FolderCount = folderCount;
            }
        }
        catch { }
    }
    
    private void CheckFileAttributes(FileAttributes attributes)
    {
        if (attributes.HasFlag(FileAttributes.ReadOnly))
            HasReadOnlyFiles = true;
        if (attributes.HasFlag(FileAttributes.Hidden))
            HasHiddenFiles = true;
        if (attributes.HasFlag(FileAttributes.System))
            HasSystemFiles = true;
    }
    
    private void GenerateWarningMessage()
    {
        var warnings = new List<string>();
        
        if (HasSystemFiles)
            warnings.Add("⚠️ Contains system files");
        if (HasReadOnlyFiles && !DeleteReadOnly)
            warnings.Add("🔒 Contains read-only files");
        if (HasHiddenFiles)
            warnings.Add("👁️ Contains hidden files");
        
        if (TotalSize > 1024 * 1024 * 1024) // > 1GB
            warnings.Add("⚡ Large amount of data");
        
        if (TotalFileCount > 1000)
            warnings.Add("📁 Many files to delete");
        
        WarningMessage = string.Join(" | ", warnings);
    }
    
    [RelayCommand]
    private void SetRecycleBinMode()
    {
        DeleteMode = DeleteMode.RecycleBin;
        OnPropertyChanged(nameof(IsRecycleBinMode));
        OnPropertyChanged(nameof(IsPermanentMode));
        OnPropertyChanged(nameof(IsSecureDeleteMode));
    }
    
    [RelayCommand]
    private void SetPermanentMode()
    {
        DeleteMode = DeleteMode.Permanent;
        OnPropertyChanged(nameof(IsRecycleBinMode));
        OnPropertyChanged(nameof(IsPermanentMode));
        OnPropertyChanged(nameof(IsSecureDeleteMode));
    }
    
    [RelayCommand]
    private void SetSecureDeleteMode()
    {
        DeleteMode = DeleteMode.SecureDelete;
        OnPropertyChanged(nameof(IsRecycleBinMode));
        OnPropertyChanged(nameof(IsPermanentMode));
        OnPropertyChanged(nameof(IsSecureDeleteMode));
    }
    
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
            item.IsSelected = true;
    }
    
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in Items)
            item.IsSelected = false;
    }
    
    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var item in Items)
            item.IsSelected = !item.IsSelected;
    }
    
    [RelayCommand]
    private void Delete()
    {
        Result = DeleteDialogResult.Delete;
        Confirmed?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, Result);
    }
    
    [RelayCommand]
    private void Cancel()
    {
        Result = DeleteDialogResult.Cancel;
        Cancelled?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, Result);
    }
    
    public IEnumerable<string> GetSelectedPaths()
    {
        return Items.Where(i => i.IsSelected).Select(i => i.FullPath);
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
