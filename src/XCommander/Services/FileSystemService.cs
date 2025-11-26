using XCommander.Models;

namespace XCommander.Services;

public class FileSystemService : IFileSystemService
{
    public IEnumerable<FileSystemItem> GetDirectoryContents(string path, bool showHidden = false)
    {
        var items = new List<FileSystemItem>();
        
        try
        {
            var directoryInfo = new DirectoryInfo(path);
            
            if (!directoryInfo.Exists)
            {
                return items;
            }
            
            // Add parent directory entry if not root
            if (directoryInfo.Parent != null)
            {
                items.Add(new FileSystemItem
                {
                    Name = "..",
                    FullPath = directoryInfo.Parent.FullName,
                    ItemType = FileSystemItemType.ParentDirectory,
                    DateModified = directoryInfo.Parent.LastWriteTime,
                    DateCreated = directoryInfo.Parent.CreationTime,
                    Attributes = directoryInfo.Parent.Attributes
                });
            }
            
            // Add directories
            foreach (var dir in directoryInfo.GetDirectories())
            {
                try
                {
                    if (!showHidden && (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                        
                    items.Add(new FileSystemItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        ItemType = FileSystemItemType.Directory,
                        DateModified = dir.LastWriteTime,
                        DateCreated = dir.CreationTime,
                        Attributes = dir.Attributes
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories we can't access
                }
            }
            
            // Add files
            foreach (var file in directoryInfo.GetFiles())
            {
                try
                {
                    if (!showHidden && (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                        
                    items.Add(new FileSystemItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        ItemType = FileSystemItemType.File,
                        Size = file.Length,
                        DateModified = file.LastWriteTime,
                        DateCreated = file.CreationTime,
                        Extension = file.Extension,
                        Attributes = file.Attributes
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files we can't access
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading directory: {ex.Message}");
        }
        
        return items;
    }

    public IEnumerable<DriveItem> GetDrives()
    {
        var drives = new List<DriveItem>();
        
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                drives.Add(new DriveItem
                {
                    Name = drive.Name.TrimEnd(Path.DirectorySeparatorChar),
                    RootPath = drive.RootDirectory.FullName,
                    DriveType = drive.DriveType,
                    VolumeLabel = drive.IsReady ? drive.VolumeLabel : null,
                    TotalSize = drive.IsReady ? drive.TotalSize : 0,
                    AvailableFreeSpace = drive.IsReady ? drive.AvailableFreeSpace : 0,
                    IsReady = drive.IsReady
                });
            }
            catch (Exception)
            {
                // Skip drives that throw exceptions
            }
        }
        
        return drives;
    }

    public async Task CopyAsync(IEnumerable<string> sourcePaths, string destinationFolder, 
        IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var paths = sourcePaths.ToList();
        var totalItems = paths.Count;
        var processedItems = 0;
        long totalBytes = 0;
        long processedBytes = 0;
        
        // Calculate total size
        foreach (var sourcePath in paths)
        {
            if (File.Exists(sourcePath))
            {
                totalBytes += new FileInfo(sourcePath).Length;
            }
            else if (Directory.Exists(sourcePath))
            {
                totalBytes += GetDirectorySize(sourcePath);
            }
        }
        
        foreach (var sourcePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var name = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, name);
            
            if (File.Exists(sourcePath))
            {
                progress?.Report(new FileOperationProgress
                {
                    CurrentItem = name,
                    TotalItems = totalItems,
                    ProcessedItems = processedItems,
                    TotalBytes = totalBytes,
                    ProcessedBytes = processedBytes
                });
                
                await CopyFileAsync(sourcePath, destPath, cancellationToken);
                processedBytes += new FileInfo(sourcePath).Length;
            }
            else if (Directory.Exists(sourcePath))
            {
                var state = new CopyState { ProcessedItems = processedItems, ProcessedBytes = processedBytes };
                await CopyDirectoryAsync(sourcePath, destPath, progress, totalItems, totalBytes, state, cancellationToken);
                processedItems = state.ProcessedItems;
                processedBytes = state.ProcessedBytes;
            }
            
            processedItems++;
        }
    }

    private class CopyState
    {
        public int ProcessedItems { get; set; }
        public long ProcessedBytes { get; set; }
    }

    private async Task CopyDirectoryAsync(string sourceDir, string destDir, 
        IProgress<FileOperationProgress>? progress, int totalItems,
        long totalBytes, CopyState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            
            progress?.Report(new FileOperationProgress
            {
                CurrentItem = Path.GetFileName(file),
                TotalItems = totalItems,
                ProcessedItems = state.ProcessedItems,
                TotalBytes = totalBytes,
                ProcessedBytes = state.ProcessedBytes
            });
            
            await CopyFileAsync(file, destFile, cancellationToken);
            state.ProcessedBytes += new FileInfo(file).Length;
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir, progress, totalItems, totalBytes, state, cancellationToken);
        }
    }

    private static async Task CopyFileAsync(string source, string dest, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
        await using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);
        await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
    }

    public async Task MoveAsync(IEnumerable<string> sourcePaths, string destinationFolder,
        IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var paths = sourcePaths.ToList();
        var totalItems = paths.Count;
        var processedItems = 0;
        
        foreach (var sourcePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var name = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, name);
            
            progress?.Report(new FileOperationProgress
            {
                CurrentItem = name,
                TotalItems = totalItems,
                ProcessedItems = processedItems,
                TotalBytes = totalItems,
                ProcessedBytes = processedItems
            });
            
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destPath, true);
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destPath);
            }
            
            processedItems++;
            await Task.Yield(); // Allow UI updates
        }
    }

    public async Task DeleteAsync(IEnumerable<string> paths, bool permanent = false,
        IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var pathList = paths.ToList();
        var totalItems = pathList.Count;
        var processedItems = 0;
        
        foreach (var path in pathList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var name = Path.GetFileName(path);
            
            progress?.Report(new FileOperationProgress
            {
                CurrentItem = name,
                TotalItems = totalItems,
                ProcessedItems = processedItems,
                TotalBytes = totalItems,
                ProcessedBytes = processedItems
            });
            
            if (File.Exists(path))
            {
                if (permanent)
                {
                    File.Delete(path);
                }
                else
                {
                    // Move to trash - platform specific
                    MoveToTrash(path);
                }
            }
            else if (Directory.Exists(path))
            {
                if (permanent)
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    MoveToTrash(path);
                }
            }
            
            processedItems++;
            await Task.Yield();
        }
    }

    private static void MoveToTrash(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                MoveToTrashWindows(path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                MoveToTrashMacOS(path);
            }
            else if (OperatingSystem.IsLinux())
            {
                MoveToTrashLinux(path);
            }
            else
            {
                // Fallback: permanent delete
                DeletePermanently(path);
            }
        }
        catch
        {
            // If trash fails, delete permanently
            DeletePermanently(path);
        }
    }
    
    private static void DeletePermanently(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
    
    private static void MoveToTrashWindows(string path)
    {
        // Windows: Use shell to move to Recycle Bin
        // We use the FileOperation COM object via PowerShell
        var escapedPath = path.Replace("'", "''");
        var script = $@"
            Add-Type -AssemblyName Microsoft.VisualBasic
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile('{escapedPath}', 'OnlyErrorDialogs', 'SendToRecycleBin')
        ";
        
        if (Directory.Exists(path))
        {
            script = $@"
                Add-Type -AssemblyName Microsoft.VisualBasic
                [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory('{escapedPath}', 'OnlyErrorDialogs', 'SendToRecycleBin')
            ";
        }
        
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        using var process = System.Diagnostics.Process.Start(psi);
        process?.WaitForExit(5000);
        
        if (process?.ExitCode != 0)
        {
            // Fallback to permanent delete if shell fails
            DeletePermanently(path);
        }
    }
    
    private static void MoveToTrashMacOS(string path)
    {
        // macOS: Use osascript to move to Trash
        var escapedPath = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var script = $"tell application \"Finder\" to delete POSIX file \"{escapedPath}\"";
        
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e '{script}'",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        using var process = System.Diagnostics.Process.Start(psi);
        process?.WaitForExit(5000);
        
        if (process?.ExitCode != 0)
        {
            // Fallback to permanent delete
            DeletePermanently(path);
        }
    }
    
    private static void MoveToTrashLinux(string path)
    {
        // Linux: Use freedesktop.org Trash specification
        // First try gio trash, then trash-cli, then manual implementation
        
        // Try gio trash (GNOME/modern distros)
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "gio",
            Arguments = $"trash \"{path}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
            if (process?.ExitCode == 0)
                return;
        }
        catch
        {
            // gio not available, try trash-cli
        }
        
        // Try trash-put (trash-cli package)
        psi.FileName = "trash-put";
        psi.Arguments = $"\"{path}\"";
        
        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
            if (process?.ExitCode == 0)
                return;
        }
        catch
        {
            // trash-cli not available
        }
        
        // Manual implementation using freedesktop.org Trash spec
        try
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var trashDir = Path.Combine(homeDir, ".local", "share", "Trash");
            var trashFilesDir = Path.Combine(trashDir, "files");
            var trashInfoDir = Path.Combine(trashDir, "info");
            
            Directory.CreateDirectory(trashFilesDir);
            Directory.CreateDirectory(trashInfoDir);
            
            var fileName = Path.GetFileName(path);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var trashFileName = fileName;
            var counter = 1;
            
            while (File.Exists(Path.Combine(trashFilesDir, trashFileName)) || 
                   Directory.Exists(Path.Combine(trashFilesDir, trashFileName)))
            {
                trashFileName = $"{baseName}.{counter}{extension}";
                counter++;
            }
            
            // Create .trashinfo file
            var infoContent = $"""
                [Trash Info]
                Path={Uri.EscapeDataString(path)}
                DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}
                """;
            
            File.WriteAllText(Path.Combine(trashInfoDir, $"{trashFileName}.trashinfo"), infoContent);
            
            // Move file/directory to trash
            var trashPath = Path.Combine(trashFilesDir, trashFileName);
            if (File.Exists(path))
            {
                File.Move(path, trashPath);
            }
            else if (Directory.Exists(path))
            {
                Directory.Move(path, trashPath);
            }
        }
        catch
        {
            // Fallback to permanent delete
            DeletePermanently(path);
        }
    }

    public void Rename(string path, string newName)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Cannot get directory name");
        var newPath = Path.Combine(directory, newName);
        
        if (File.Exists(path))
        {
            File.Move(path, newPath);
        }
        else if (Directory.Exists(path))
        {
            Directory.Move(path, newPath);
        }
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void CreateFile(string path)
    {
        File.Create(path).Dispose();
    }

    public bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    public bool IsDirectory(string path)
    {
        return Directory.Exists(path);
    }

    public string GetParentDirectory(string path)
    {
        return Path.GetDirectoryName(path) ?? path;
    }

    public string CombinePath(string path1, string path2)
    {
        return Path.Combine(path1, path2);
    }
    
    public FileSystemItem? GetFileInfo(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                return new FileSystemItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    ItemType = FileSystemItemType.File,
                    Size = fileInfo.Length,
                    DateModified = fileInfo.LastWriteTime,
                    DateCreated = fileInfo.CreationTime,
                    Extension = fileInfo.Extension,
                    Attributes = fileInfo.Attributes
                };
            }
            else if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                return new FileSystemItem
                {
                    Name = dirInfo.Name,
                    FullPath = dirInfo.FullName,
                    ItemType = FileSystemItemType.Directory,
                    DateModified = dirInfo.LastWriteTime,
                    DateCreated = dirInfo.CreationTime,
                    Attributes = dirInfo.Attributes
                };
            }
        }
        catch
        {
            // Ignore access errors
        }
        
        return null;
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        
        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                size += new FileInfo(file).Length;
            }
            
            foreach (var dir in Directory.GetDirectories(path))
            {
                size += GetDirectorySize(dir);
            }
        }
        catch
        {
            // Ignore access errors
        }
        
        return size;
    }
}
