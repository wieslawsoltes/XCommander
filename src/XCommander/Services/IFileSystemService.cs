using XCommander.Models;

namespace XCommander.Services;

public interface IFileSystemService
{
    IEnumerable<FileSystemItem> GetDirectoryContents(string path, bool showHidden = false);
    IEnumerable<DriveItem> GetDrives();
    Task CopyAsync(IEnumerable<string> sourcePaths, string destinationFolder, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task MoveAsync(IEnumerable<string> sourcePaths, string destinationFolder, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<string> paths, bool permanent = false, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    void Rename(string path, string newName);
    void CreateDirectory(string path);
    void CreateFile(string path);
    bool Exists(string path);
    bool IsDirectory(string path);
    string GetParentDirectory(string path);
    string CombinePath(string path1, string path2);
    FileSystemItem? GetFileInfo(string path);
}

public class FileOperationProgress
{
    public required string CurrentItem { get; init; }
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedBytes { get; init; }
    public long TransferSpeedBytesPerSecond { get; init; }
    public double Percentage => TotalBytes > 0 ? (ProcessedBytes / (double)TotalBytes) * 100 : 0;
}
