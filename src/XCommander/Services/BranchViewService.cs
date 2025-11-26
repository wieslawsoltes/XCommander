namespace XCommander.Services;

/// <summary>
/// File information for branch view display
/// </summary>
public class BranchViewItem
{
    public string FullPath { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Directory { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime DateModified { get; init; }
    public DateTime DateCreated { get; init; }
    public bool IsDirectory { get; init; }
    public FileAttributes Attributes { get; init; }
    
    public string DisplaySize => IsDirectory ? "<DIR>" : FormatSize(Size);
    
    public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);
    public bool IsHidden => Attributes.HasFlag(FileAttributes.Hidden);
    public bool IsSystem => Attributes.HasFlag(FileAttributes.System);
    public bool IsArchive => Attributes.HasFlag(FileAttributes.Archive);
    
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

/// <summary>
/// Progress information for branch view loading
/// </summary>
public class BranchViewProgress
{
    public int FilesFound { get; init; }
    public int DirectoriesScanned { get; init; }
    public string CurrentDirectory { get; init; } = string.Empty;
}

/// <summary>
/// Options for branch view
/// </summary>
public class BranchViewOptions
{
    /// <summary>
    /// Include files in the results
    /// </summary>
    public bool IncludeFiles { get; init; } = true;
    
    /// <summary>
    /// Include directories in the results
    /// </summary>
    public bool IncludeDirectories { get; init; } = false;
    
    /// <summary>
    /// Include hidden files and directories
    /// </summary>
    public bool IncludeHidden { get; init; } = false;
    
    /// <summary>
    /// Include system files and directories
    /// </summary>
    public bool IncludeSystem { get; init; } = false;
    
    /// <summary>
    /// File pattern filter (e.g., "*.txt", "*.cs")
    /// </summary>
    public string? FilePattern { get; init; }
    
    /// <summary>
    /// Maximum depth to scan (-1 for unlimited)
    /// </summary>
    public int MaxDepth { get; init; } = -1;
    
    /// <summary>
    /// Minimum file size filter
    /// </summary>
    public long MinSize { get; init; } = 0;
    
    /// <summary>
    /// Maximum file size filter
    /// </summary>
    public long MaxSize { get; init; } = long.MaxValue;
    
    /// <summary>
    /// Only include files modified after this date
    /// </summary>
    public DateTime? ModifiedAfter { get; init; }
    
    /// <summary>
    /// Only include files modified before this date
    /// </summary>
    public DateTime? ModifiedBefore { get; init; }
}

/// <summary>
/// Service for branch view (flat view) functionality - shows all files from subdirectories in a flat list
/// </summary>
public interface IBranchViewService
{
    /// <summary>
    /// Get all files from a directory and its subdirectories as a flat list
    /// </summary>
    IAsyncEnumerable<BranchViewItem> GetBranchViewAsync(
        string rootDirectory,
        BranchViewOptions? options = null,
        IProgress<BranchViewProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all files as a list (blocks until complete)
    /// </summary>
    Task<List<BranchViewItem>> GetBranchViewListAsync(
        string rootDirectory,
        BranchViewOptions? options = null,
        IProgress<BranchViewProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public class BranchViewService : IBranchViewService
{
    public async IAsyncEnumerable<BranchViewItem> GetBranchViewAsync(
        string rootDirectory,
        BranchViewOptions? options = null,
        IProgress<BranchViewProgress>? progress = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new BranchViewOptions();
        
        if (!Directory.Exists(rootDirectory))
            yield break;
        
        var filesFound = 0;
        var directoriesScanned = 0;
        var rootUri = new Uri(rootDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        
        var directoriesToScan = new Queue<(string Path, int Depth)>();
        directoriesToScan.Enqueue((rootDirectory, 0));
        
        while (directoriesToScan.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var (currentDir, currentDepth) = directoriesToScan.Dequeue();
            directoriesScanned++;
            
            progress?.Report(new BranchViewProgress
            {
                FilesFound = filesFound,
                DirectoriesScanned = directoriesScanned,
                CurrentDirectory = currentDir
            });
            
            // Get files in current directory
            if (options.IncludeFiles)
            {
                IEnumerable<string> files;
                try
                {
                    var pattern = options.FilePattern ?? "*";
                    files = Directory.EnumerateFiles(currentDir, pattern, SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    BranchViewItem? item = null;
                    try
                    {
                        var info = new FileInfo(file);
                        
                        // Apply filters
                        if (!options.IncludeHidden && info.Attributes.HasFlag(FileAttributes.Hidden))
                            continue;
                        if (!options.IncludeSystem && info.Attributes.HasFlag(FileAttributes.System))
                            continue;
                        if (info.Length < options.MinSize || info.Length > options.MaxSize)
                            continue;
                        if (options.ModifiedAfter.HasValue && info.LastWriteTime < options.ModifiedAfter.Value)
                            continue;
                        if (options.ModifiedBefore.HasValue && info.LastWriteTime > options.ModifiedBefore.Value)
                            continue;
                        
                        var fileUri = new Uri(file);
                        var relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                            .Replace('/', Path.DirectorySeparatorChar);
                        
                        item = new BranchViewItem
                        {
                            FullPath = file,
                            Name = info.Name,
                            Extension = info.Extension,
                            RelativePath = relativePath,
                            Directory = info.DirectoryName ?? string.Empty,
                            Size = info.Length,
                            DateModified = info.LastWriteTime,
                            DateCreated = info.CreationTime,
                            IsDirectory = false,
                            Attributes = info.Attributes
                        };
                        filesFound++;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    
                    if (item != null)
                        yield return item;
                }
            }
            
            // Get directories in current directory
            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            
            foreach (var subdir in subdirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                BranchViewItem? dirItem = null;
                bool shouldQueue = false;
                
                try
                {
                    var dirInfo = new DirectoryInfo(subdir);
                    
                    // Skip hidden/system directories if not included
                    if (!options.IncludeHidden && dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                        continue;
                    if (!options.IncludeSystem && dirInfo.Attributes.HasFlag(FileAttributes.System))
                        continue;
                    
                    // Include directories in results if requested
                    if (options.IncludeDirectories)
                    {
                        var dirUri = new Uri(subdir + Path.DirectorySeparatorChar);
                        var relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(dirUri).ToString())
                            .Replace('/', Path.DirectorySeparatorChar)
                            .TrimEnd(Path.DirectorySeparatorChar);
                        
                        dirItem = new BranchViewItem
                        {
                            FullPath = subdir,
                            Name = dirInfo.Name,
                            Extension = string.Empty,
                            RelativePath = relativePath,
                            Directory = dirInfo.Parent?.FullName ?? string.Empty,
                            Size = 0,
                            DateModified = dirInfo.LastWriteTime,
                            DateCreated = dirInfo.CreationTime,
                            IsDirectory = true,
                            Attributes = dirInfo.Attributes
                        };
                    }
                    
                    // Queue for scanning if within depth limit
                    if (options.MaxDepth < 0 || currentDepth < options.MaxDepth)
                    {
                        shouldQueue = true;
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
                
                if (dirItem != null)
                    yield return dirItem;
                
                if (shouldQueue)
                    directoriesToScan.Enqueue((subdir, currentDepth + 1));
            }
        }
        
        progress?.Report(new BranchViewProgress
        {
            FilesFound = filesFound,
            DirectoriesScanned = directoriesScanned,
            CurrentDirectory = "Complete"
        });
    }
    
    public async Task<List<BranchViewItem>> GetBranchViewListAsync(
        string rootDirectory,
        BranchViewOptions? options = null,
        IProgress<BranchViewProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var items = new List<BranchViewItem>();
        
        await foreach (var item in GetBranchViewAsync(rootDirectory, options, progress, cancellationToken))
        {
            items.Add(item);
        }
        
        return items;
    }
}
