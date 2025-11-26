namespace XCommander.Services;

/// <summary>
/// Information about a disk space item (file or folder)
/// </summary>
public class DiskSpaceItem
{
    public string Path { get; init; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; init; }
    public double Percentage { get; init; }
    public bool IsDirectory { get; init; }
    public int FileCount { get; init; }
    public int FolderCount { get; init; }
    public int Depth { get; init; }
    public DiskSpaceItem? Parent { get; init; }
    public List<DiskSpaceItem> Children { get; init; } = new();
}

/// <summary>
/// Progress information for disk space analysis
/// </summary>
public class DiskAnalysisProgress
{
    public string CurrentPath { get; init; } = string.Empty;
    public int FilesScanned { get; init; }
    public int DirectoriesScanned { get; init; }
    public long BytesScanned { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Result of disk space analysis
/// </summary>
public class DiskAnalysisResult
{
    public string RootPath { get; init; } = string.Empty;
    public long TotalSize { get; init; }
    public long UsedSize { get; init; }
    public long FreeSize { get; init; }
    public int TotalFiles { get; init; }
    public int TotalFolders { get; init; }
    public DiskSpaceItem RootItem { get; init; } = null!;
    public IReadOnlyList<DiskSpaceItem> LargestFiles { get; init; } = Array.Empty<DiskSpaceItem>();
    public IReadOnlyList<DiskSpaceItem> LargestFolders { get; init; } = Array.Empty<DiskSpaceItem>();
    public TimeSpan Duration { get; init; }
    public Dictionary<string, long> SizeByExtension { get; init; } = new();
}

/// <summary>
/// Service for analyzing disk space usage
/// </summary>
public interface IDiskSpaceAnalyzerService
{
    /// <summary>
    /// Analyze disk space usage for a directory
    /// </summary>
    Task<DiskAnalysisResult> AnalyzeAsync(
        string path,
        int maxDepth = -1,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get largest files in a directory
    /// </summary>
    Task<IReadOnlyList<DiskSpaceItem>> GetLargestFilesAsync(
        string path,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get largest folders in a directory
    /// </summary>
    Task<IReadOnlyList<DiskSpaceItem>> GetLargestFoldersAsync(
        string path,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file size breakdown by extension
    /// </summary>
    Task<Dictionary<string, long>> GetSizeByExtensionAsync(
        string path,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file count breakdown by extension
    /// </summary>
    Task<Dictionary<string, int>> GetCountByExtensionAsync(
        string path,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get old files (not modified for specified days)
    /// </summary>
    Task<IReadOnlyList<DiskSpaceItem>> GetOldFilesAsync(
        string path,
        int daysOld = 365,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get drive information
    /// </summary>
    DriveSpaceInfo? GetDriveInfo(string path);
    
    /// <summary>
    /// Get all drives with space info
    /// </summary>
    IReadOnlyList<DriveSpaceInfo> GetAllDrives();
    
    /// <summary>
    /// Export analysis to CSV
    /// </summary>
    Task ExportToCsvAsync(DiskAnalysisResult result, string outputPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export analysis to JSON
    /// </summary>
    Task ExportToJsonAsync(DiskAnalysisResult result, string outputPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Drive space information
/// </summary>
public class DriveSpaceInfo
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public string FileSystem { get; init; } = string.Empty;
    public long TotalSize { get; init; }
    public long FreeSpace { get; init; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
    public bool IsReady { get; init; }
}

public class DiskSpaceAnalyzerService : IDiskSpaceAnalyzerService
{
    public async Task<DiskAnalysisResult> AnalyzeAsync(
        string path,
        int maxDepth = -1,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var filesScanned = 0;
        var directoriesScanned = 0;
        var sizeByExtension = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var allFiles = new List<DiskSpaceItem>();
        var allFolders = new List<DiskSpaceItem>();
        
        var rootItem = await Task.Run(() => ScanDirectory(
            path, 
            null, 
            0, 
            maxDepth, 
            ref filesScanned, 
            ref directoriesScanned,
            sizeByExtension,
            allFiles,
            allFolders,
            progress, 
            cancellationToken), cancellationToken);
        
        // Calculate percentages
        CalculatePercentages(rootItem, rootItem.Size);
        
        // Get drive info if available
        var driveInfo = GetDriveInfo(path);
        
        stopwatch.Stop();
        
        return new DiskAnalysisResult
        {
            RootPath = path,
            TotalSize = driveInfo?.TotalSize ?? rootItem.Size,
            UsedSize = rootItem.Size,
            FreeSize = driveInfo?.FreeSpace ?? 0,
            TotalFiles = filesScanned,
            TotalFolders = directoriesScanned,
            RootItem = rootItem,
            LargestFiles = allFiles.OrderByDescending(f => f.Size).Take(100).ToList(),
            LargestFolders = allFolders.OrderByDescending(f => f.Size).Take(100).ToList(),
            Duration = stopwatch.Elapsed,
            SizeByExtension = sizeByExtension
        };
    }
    
    public async Task<IReadOnlyList<DiskSpaceItem>> GetLargestFilesAsync(
        string path,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = new List<DiskSpaceItem>();
        var filesScanned = 0;
        
        await Task.Run(() => ScanForLargestFiles(path, files, count, ref filesScanned, progress, cancellationToken), cancellationToken);
        
        return files.OrderByDescending(f => f.Size).Take(count).ToList();
    }
    
    public async Task<IReadOnlyList<DiskSpaceItem>> GetLargestFoldersAsync(
        string path,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var folders = new List<DiskSpaceItem>();
        var dirsScanned = 0;
        
        await Task.Run(() => ScanForLargestFolders(path, folders, ref dirsScanned, progress, cancellationToken), cancellationToken);
        
        return folders.OrderByDescending(f => f.Size).Take(count).ToList();
    }
    
    public async Task<Dictionary<string, long>> GetSizeByExtensionAsync(
        string path,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sizeByExt = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var filesScanned = 0;
        
        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "(no extension)";
                    
                    var size = new FileInfo(file).Length;
                    
                    if (sizeByExt.ContainsKey(ext))
                        sizeByExt[ext] += size;
                    else
                        sizeByExt[ext] = size;
                    
                    filesScanned++;
                    
                    if (filesScanned % 1000 == 0)
                    {
                        progress?.Report(new DiskAnalysisProgress
                        {
                            CurrentPath = file,
                            FilesScanned = filesScanned,
                            Status = "Scanning files..."
                        });
                    }
                }
                catch { }
            }
        }, cancellationToken);
        
        return sizeByExt.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    public async Task<Dictionary<string, int>> GetCountByExtensionAsync(
        string path,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var countByExt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filesScanned = 0;
        
        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "(no extension)";
                    
                    if (countByExt.ContainsKey(ext))
                        countByExt[ext]++;
                    else
                        countByExt[ext] = 1;
                    
                    filesScanned++;
                }
                catch { }
            }
        }, cancellationToken);
        
        return countByExt.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    public async Task<IReadOnlyList<DiskSpaceItem>> GetOldFilesAsync(
        string path,
        int daysOld = 365,
        int count = 100,
        IProgress<DiskAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.Now.AddDays(-daysOld);
        var oldFiles = new List<DiskSpaceItem>();
        var filesScanned = 0;
        
        await Task.Run(() =>
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < cutoffDate)
                    {
                        oldFiles.Add(new DiskSpaceItem
                        {
                            Path = file,
                            Size = info.Length,
                            IsDirectory = false
                        });
                    }
                    
                    filesScanned++;
                    
                    if (filesScanned % 1000 == 0)
                    {
                        progress?.Report(new DiskAnalysisProgress
                        {
                            CurrentPath = file,
                            FilesScanned = filesScanned,
                            Status = $"Finding files older than {daysOld} days..."
                        });
                    }
                }
                catch { }
            }
        }, cancellationToken);
        
        return oldFiles.OrderByDescending(f => f.Size).Take(count).ToList();
    }
    
    public DriveSpaceInfo? GetDriveInfo(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return null;
            
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return null;
            
            return new DriveSpaceInfo
            {
                Name = drive.Name,
                Label = drive.VolumeLabel,
                DriveType = drive.DriveType.ToString(),
                FileSystem = drive.DriveFormat,
                TotalSize = drive.TotalSize,
                FreeSpace = drive.AvailableFreeSpace,
                IsReady = drive.IsReady
            };
        }
        catch
        {
            return null;
        }
    }
    
    public IReadOnlyList<DriveSpaceInfo> GetAllDrives()
    {
        var drives = new List<DriveSpaceInfo>();
        
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady)
                {
                    drives.Add(new DriveSpaceInfo
                    {
                        Name = drive.Name,
                        Label = drive.VolumeLabel,
                        DriveType = drive.DriveType.ToString(),
                        FileSystem = drive.DriveFormat,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.AvailableFreeSpace,
                        IsReady = drive.IsReady
                    });
                }
            }
            catch { }
        }
        
        return drives;
    }
    
    public async Task ExportToCsvAsync(DiskAnalysisResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "Path,Size,Percentage,IsDirectory,FileCount,FolderCount"
        };
        
        void AddItem(DiskSpaceItem item, List<string> list)
        {
            var escapedPath = item.Path.Contains(',') ? $"\"{item.Path}\"" : item.Path;
            list.Add($"{escapedPath},{item.Size},{item.Percentage:F2},{item.IsDirectory},{item.FileCount},{item.FolderCount}");
            
            foreach (var child in item.Children)
            {
                AddItem(child, list);
            }
        }
        
        AddItem(result.RootItem, lines);
        
        await File.WriteAllLinesAsync(outputPath, lines, cancellationToken);
    }
    
    public async Task ExportToJsonAsync(DiskAnalysisResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        
        // Create a simplified version without circular references
        var exportData = new
        {
            result.RootPath,
            result.TotalSize,
            result.UsedSize,
            result.FreeSize,
            result.TotalFiles,
            result.TotalFolders,
            Duration = result.Duration.TotalSeconds,
            LargestFiles = result.LargestFiles.Select(f => new { f.Path, f.Size }).ToList(),
            LargestFolders = result.LargestFolders.Select(f => new { f.Path, f.Size, f.FileCount }).ToList(),
            result.SizeByExtension
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(exportData, options);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }
    
    private DiskSpaceItem ScanDirectory(
        string path,
        DiskSpaceItem? parent,
        int depth,
        int maxDepth,
        ref int filesScanned,
        ref int directoriesScanned,
        Dictionary<string, long> sizeByExtension,
        List<DiskSpaceItem> allFiles,
        List<DiskSpaceItem> allFolders,
        IProgress<DiskAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var item = new DiskSpaceItem
        {
            Path = path,
            IsDirectory = true,
            Depth = depth,
            Parent = parent
        };
        
        directoriesScanned++;
        
        if (directoriesScanned % 100 == 0)
        {
            progress?.Report(new DiskAnalysisProgress
            {
                CurrentPath = path,
                DirectoriesScanned = directoriesScanned,
                FilesScanned = filesScanned,
                Status = "Scanning..."
            });
        }
        
        long totalSize = 0;
        var fileCount = 0;
        var folderCount = 0;
        
        // Scan files
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    totalSize += size;
                    fileCount++;
                    filesScanned++;
                    
                    var ext = info.Extension.ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = "(no extension)";
                    
                    if (sizeByExtension.ContainsKey(ext))
                        sizeByExtension[ext] += size;
                    else
                        sizeByExtension[ext] = size;
                    
                    if (maxDepth < 0 || depth < maxDepth)
                    {
                        var fileItem = new DiskSpaceItem
                        {
                            Path = file,
                            Size = size,
                            IsDirectory = false,
                            Depth = depth + 1,
                            Parent = item
                        };
                        item.Children.Add(fileItem);
                        allFiles.Add(fileItem);
                    }
                }
                catch { }
            }
        }
        catch { }
        
        // Scan subdirectories
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    if (maxDepth < 0 || depth < maxDepth)
                    {
                        var childItem = ScanDirectory(
                            dir, item, depth + 1, maxDepth,
                            ref filesScanned, ref directoriesScanned,
                            sizeByExtension, allFiles, allFolders,
                            progress, cancellationToken);
                        
                        item.Children.Add(childItem);
                        totalSize += childItem.Size;
                        fileCount += childItem.FileCount;
                        folderCount += childItem.FolderCount + 1;
                    }
                    else
                    {
                        // Just calculate size without recursing
                        var size = CalculateDirectorySize(dir);
                        totalSize += size;
                        folderCount++;
                    }
                }
                catch { }
            }
        }
        catch { }
        
        // Update item with collected data using reflection or create new item
        var finalItem = new DiskSpaceItem
        {
            Path = item.Path,
            Size = totalSize,
            IsDirectory = true,
            FileCount = fileCount,
            FolderCount = folderCount,
            Depth = depth,
            Parent = parent,
            Children = item.Children
        };
        
        allFolders.Add(finalItem);
        
        return finalItem;
    }
    
    private void CalculatePercentages(DiskSpaceItem item, long totalSize)
    {
        if (totalSize > 0)
        {
            // Use reflection to set percentage since it's init-only
            var percentageProperty = typeof(DiskSpaceItem).GetProperty(nameof(DiskSpaceItem.Percentage));
            percentageProperty?.SetValue(item, (double)item.Size / totalSize * 100);
            
            foreach (var child in item.Children)
            {
                CalculatePercentages(child, totalSize);
            }
        }
    }
    
    private void ScanForLargestFiles(
        string path,
        List<DiskSpaceItem> files,
        int count,
        ref int filesScanned,
        IProgress<DiskAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var info = new FileInfo(file);
                    var item = new DiskSpaceItem
                    {
                        Path = file,
                        Size = info.Length,
                        IsDirectory = false
                    };
                    
                    // Keep sorted list of largest files
                    if (files.Count < count)
                    {
                        files.Add(item);
                        files.Sort((a, b) => b.Size.CompareTo(a.Size));
                    }
                    else if (item.Size > files[^1].Size)
                    {
                        files[^1] = item;
                        files.Sort((a, b) => b.Size.CompareTo(a.Size));
                    }
                    
                    filesScanned++;
                    
                    if (filesScanned % 1000 == 0)
                    {
                        progress?.Report(new DiskAnalysisProgress
                        {
                            CurrentPath = file,
                            FilesScanned = filesScanned,
                            Status = "Finding largest files..."
                        });
                    }
                }
                catch { }
            }
        }
        catch { }
    }
    
    private void ScanForLargestFolders(
        string path,
        List<DiskSpaceItem> folders,
        ref int dirsScanned,
        IProgress<DiskAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var size = CalculateDirectorySize(dir);
                    var fileCount = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
                    
                    var item = new DiskSpaceItem
                    {
                        Path = dir,
                        Size = size,
                        IsDirectory = true,
                        FileCount = fileCount
                    };
                    
                    folders.Add(item);
                    dirsScanned++;
                    
                    if (dirsScanned % 100 == 0)
                    {
                        progress?.Report(new DiskAnalysisProgress
                        {
                            CurrentPath = dir,
                            DirectoriesScanned = dirsScanned,
                            Status = "Finding largest folders..."
                        });
                    }
                }
                catch { }
            }
        }
        catch { }
    }
    
    private static long CalculateDirectorySize(string path)
    {
        long size = 0;
        
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch { }
            }
        }
        catch { }
        
        return size;
    }
}
