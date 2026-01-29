// PluginService.cs - Implementation of TC-style plugin system
// Manages packer, content, file system, and lister plugins

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XCommander.Plugins;

namespace XCommander.Services;

public class PluginService : IPluginService
{
    private readonly ConcurrentDictionary<string, PluginInfo> _plugins = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<PluginContentField>> _contentFields = new();
    private readonly ConcurrentDictionary<string, List<string>> _extensionToPlugin = new();
    private readonly ConcurrentDictionary<string, IPlugin> _managedPlugins = new();
    private readonly ConcurrentDictionary<string, IFileSystemPlugin> _fileSystemPlugins = new();
    private readonly ConcurrentDictionary<string, IColumnPlugin> _columnPlugins = new();
    private readonly ConcurrentDictionary<string, bool> _pluginInitialized = new();
    private readonly ServicePluginContext _pluginContext = new();
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private const string LocalFileSystemPluginId = "builtin.localfs";
    
    public event EventHandler<PluginEventArgs>? PluginLoaded;
    public event EventHandler<PluginEventArgs>? PluginUnloaded;
    public event EventHandler<EventArgs>? PluginsChanged;
    
    public PluginService()
    {
        InitializeBuiltInPlugins();
    }
    
    private void InitializeBuiltInPlugins()
    {
        // Built-in ZIP packer plugin using System.IO.Compression
        var zipPlugin = new PluginInfo
        {
            Id = "builtin.zip",
            Name = "ZIP Archive",
            Description = "Built-in ZIP archive support",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.Packer,
            Capabilities = PluginCapabilities.CanCreate | PluginCapabilities.CanExtract | 
                          PluginCapabilities.CanModify | PluginCapabilities.CanDelete,
            Extensions = new[] { ".zip" },
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(zipPlugin);
        
        // Built-in GZip packer plugin
        var gzipPlugin = new PluginInfo
        {
            Id = "builtin.gzip",
            Name = "GZip Archive",
            Description = "Built-in GZip support",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.Packer,
            Capabilities = PluginCapabilities.CanCreate | PluginCapabilities.CanExtract,
            Extensions = new[] { ".gz", ".gzip" },
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(gzipPlugin);
        
        // Built-in file properties content plugin
        var filePropsPlugin = new PluginInfo
        {
            Id = "builtin.fileprops",
            Name = "File Properties",
            Description = "Built-in file property fields",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.Content,
            Capabilities = PluginCapabilities.CanSearch | PluginCapabilities.CanSort,
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(filePropsPlugin);
        
        // Register content fields for file properties plugin
        _contentFields["builtin.fileprops"] = new List<PluginContentField>
        {
            new() { Name = "Name", DisplayName = "File Name", FieldType = PluginContentFieldType.String },
            new() { Name = "Extension", DisplayName = "Extension", FieldType = PluginContentFieldType.String },
            new() { Name = "Size", DisplayName = "Size", FieldType = PluginContentFieldType.NumericInt64, Unit = "bytes" },
            new() { Name = "Modified", DisplayName = "Modified Date", FieldType = PluginContentFieldType.DateTime },
            new() { Name = "Created", DisplayName = "Created Date", FieldType = PluginContentFieldType.DateTime },
            new() { Name = "Accessed", DisplayName = "Last Accessed", FieldType = PluginContentFieldType.DateTime },
            new() { Name = "Attributes", DisplayName = "Attributes", FieldType = PluginContentFieldType.String },
            new() { Name = "ReadOnly", DisplayName = "Read Only", FieldType = PluginContentFieldType.Boolean }
        };
        
        // Built-in text lister plugin
        var textListerPlugin = new PluginInfo
        {
            Id = "builtin.textlister",
            Name = "Text Viewer",
            Description = "Built-in text file viewer",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.Lister,
            Capabilities = PluginCapabilities.SupportsText | PluginCapabilities.SupportsSearch | 
                          PluginCapabilities.SupportsCopy,
            Extensions = new[] { ".txt", ".log", ".md", ".json", ".xml", ".cs", ".py", ".js", ".html", ".css" },
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(textListerPlugin);
        
        // Built-in image lister plugin
        var imageListerPlugin = new PluginInfo
        {
            Id = "builtin.imagelister",
            Name = "Image Viewer",
            Description = "Built-in image viewer",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.Lister,
            Capabilities = PluginCapabilities.SupportsImage | PluginCapabilities.SupportsPrint,
            Extensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".svg" },
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(imageListerPlugin);

        // Built-in local file system plugin
        var localFileSystemPlugin = new PluginInfo
        {
            Id = LocalFileSystemPluginId,
            Name = "Local File System",
            Description = "Built-in local file system access",
            Author = "XCommander",
            Version = "1.0",
            Type = PluginType.FileSystem,
            Capabilities = PluginCapabilities.CanRead | PluginCapabilities.CanWrite |
                          PluginCapabilities.CanCreateFolder | PluginCapabilities.CanDelete |
                          PluginCapabilities.CanRename | PluginCapabilities.CanExecute,
            IsBuiltIn = true,
            IsEnabled = true,
            LoadedAt = DateTime.Now
        };
        RegisterBuiltInPlugin(localFileSystemPlugin);
    }
    
    private void RegisterBuiltInPlugin(PluginInfo plugin)
    {
        _plugins[plugin.Id] = plugin;
        RegisterExtensionMappings(plugin);
    }
    
    public IReadOnlyList<PluginInfo> GetAllPlugins()
    {
        return _plugins.Values.OrderBy(p => p.Type).ThenBy(p => p.Name).ToList();
    }
    
    public IReadOnlyList<PluginInfo> GetPluginsByType(PluginType type)
    {
        return _plugins.Values.Where(p => p.Type == type).OrderBy(p => p.Name).ToList();
    }
    
    public PluginInfo? GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }
    
    public PluginInfo? GetPluginForExtension(PluginType type, string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        var key = $"{type}:{ext.ToLowerInvariant()}";
        
        if (_extensionToPlugin.TryGetValue(key, out var pluginIds) && pluginIds.Count > 0)
        {
            var pluginId = pluginIds.FirstOrDefault(id => _plugins.TryGetValue(id, out var p) && p.IsEnabled);
            if (pluginId != null && _plugins.TryGetValue(pluginId, out var plugin))
            {
                return plugin;
            }
        }
        
        return null;
    }
    
    public async Task<PluginInfo?> GetPluginForFileAsync(PluginType type, string filePath, CancellationToken cancellationToken = default)
    {
        // First try by extension
        var ext = Path.GetExtension(filePath);
        var byExtension = GetPluginForExtension(type, ext);
        if (byExtension != null)
        {
            return byExtension;
        }
        
        // Then try detection strings (read file header)
        foreach (var plugin in _plugins.Values.Where(p => p.Type == type && p.IsEnabled && p.DetectStrings.Count > 0))
        {
            try
            {
                var header = new byte[512];
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bytesRead = await fs.ReadAsync(header, 0, header.Length, cancellationToken);
                
                var headerStr = System.Text.Encoding.ASCII.GetString(header, 0, bytesRead);
                if (plugin.DetectStrings.Any(ds => headerStr.Contains(ds)))
                {
                    return plugin;
                }
            }
            catch
            {
                // Ignore detection errors
            }
        }
        
        return null;
    }
    
    public async Task<PluginInfo> RegisterPluginAsync(string pluginPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
            throw new ArgumentException("Plugin path is required.", nameof(pluginPath));

        var resolvedPath = pluginPath;
        var pluginDirectory = Directory.Exists(pluginPath)
            ? pluginPath
            : Path.GetDirectoryName(pluginPath);

        if (string.IsNullOrEmpty(pluginDirectory))
            throw new DirectoryNotFoundException($"Plugin directory not found for '{pluginPath}'.");

        var manifest = await LoadManifestAsync(pluginPath, pluginDirectory, cancellationToken);
        var assemblyPath = ResolveAssemblyPath(pluginPath, pluginDirectory, manifest);

        IPlugin? instance = null;
        if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
        {
            instance = LoadPluginInstance(assemblyPath, manifest?.PluginTypeName);
        }

        var pluginType = ResolvePluginType(pluginPath, manifest, instance);
        var extensions = ResolveExtensions(pluginPath, manifest, instance, pluginType);
        var pluginId = manifest?.Id ?? instance?.Id ?? $"user.{Path.GetFileNameWithoutExtension(pluginPath).ToLowerInvariant()}";

        var plugin = new PluginInfo
        {
            Id = pluginId,
            Name = manifest?.Name ?? instance?.Name ?? Path.GetFileNameWithoutExtension(pluginPath),
            Description = manifest?.Description ?? instance?.Description,
            Author = manifest?.Author ?? instance?.Author,
            Version = manifest?.Version ?? instance?.Version.ToString(),
            Website = manifest?.Website,
            Type = pluginType,
            Capabilities = ResolveCapabilities(pluginType, manifest, instance),
            PluginPath = resolvedPath,
            ConfigPath = ResolveConfigPath(pluginDirectory, manifest),
            Extensions = extensions,
            DetectStrings = manifest?.DetectStrings ?? new List<string>(),
            IsEnabled = manifest?.Enabled ?? true,
            IsBuiltIn = false,
            LoadedAt = DateTime.Now
        };

        _plugins[plugin.Id] = plugin;
        RegisterExtensionMappings(plugin);

        if (instance != null)
        {
            _managedPlugins[plugin.Id] = instance;
            if (instance is IFileSystemPlugin fileSystemPlugin)
            {
                _fileSystemPlugins[plugin.Id] = fileSystemPlugin;
            }
            if (instance is IColumnPlugin columnPlugin)
            {
                _columnPlugins[plugin.Id] = columnPlugin;
                _contentFields[plugin.Id] = columnPlugin.GetColumns()
                    .Select(c => new PluginContentField
                    {
                        Name = c.Id,
                        DisplayName = c.Name,
                        FieldType = PluginContentFieldType.String,
                        CanEdit = false
                    })
                    .ToList();
            }
            await InitializePluginInstanceAsync(plugin.Id, instance, cancellationToken);
        }

        OnPluginLoaded(plugin);
        OnPluginsChanged();

        return plugin;
    }
    
    public Task<bool> UnregisterPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            return Task.FromResult(false);
        }
        
        if (plugin.IsBuiltIn)
        {
            return Task.FromResult(false); // Cannot unregister built-in plugins
        }
        
        if (_plugins.TryRemove(pluginId, out var removed))
        {
            RemoveExtensionMappings(removed);

            if (_managedPlugins.TryRemove(pluginId, out var instance))
            {
                _fileSystemPlugins.TryRemove(pluginId, out _);
                _columnPlugins.TryRemove(pluginId, out _);
                _contentFields.TryRemove(pluginId, out _);
                _pluginInitialized.TryRemove(pluginId, out _);
                try
                {
                    instance.ShutdownAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore shutdown errors
                }
            }

            OnPluginUnloaded(removed);
            OnPluginsChanged();
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }
    
    public Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            return Task.FromResult(false);
        }
        
        _plugins[pluginId] = plugin with { IsEnabled = enabled };

        if (_managedPlugins.TryGetValue(pluginId, out var instance))
        {
            if (enabled)
            {
                _ = InitializePluginInstanceAsync(pluginId, instance, cancellationToken);
            }
            else
            {
                try
                {
                    instance.ShutdownAsync().GetAwaiter().GetResult();
                    _pluginInitialized[pluginId] = false;
                }
                catch
                {
                    // Ignore shutdown errors
                }
            }
        }
        OnPluginsChanged();
        
        return Task.FromResult(true);
    }
    
    public Task ConfigurePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would call the plugin's config function
        // For now, this is a no-op
        return Task.CompletedTask;
    }
    
    public Task<IReadOnlyList<string>> ScanForPluginsAsync(string directory, CancellationToken cancellationToken = default)
    {
        var pluginExtensions = new[] { ".wcx", ".wcx64", ".wdx", ".wdx64", ".wfx", ".wfx64", ".wlx", ".wlx64", ".dll", ".json" };
        var results = new List<string>();
        
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (pluginExtensions.Contains(ext))
                {
                    if (ext == ".json" && !string.Equals(Path.GetFileName(file), "plugin.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                    results.Add(file);
                }
            }
        }
        
        return Task.FromResult<IReadOnlyList<string>>(results);
    }
    
    // ======= Packer Plugin Operations =======
    
    public Task<IPackerHandle?> OpenArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath);
        var plugin = GetPluginForExtension(PluginType.Packer, ext);
        
        if (plugin == null)
        {
            return Task.FromResult<IPackerHandle?>(null);
        }
        
        return Task.FromResult<IPackerHandle?>(new PackerHandle(archivePath, plugin));
    }
    
    public async Task<IReadOnlyList<ArchiveItem>> ListArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await ListZipArchiveAsync(archivePath, cancellationToken);
        }
        
        // For other formats, we would delegate to the appropriate plugin
        return Array.Empty<ArchiveItem>();
    }
    
    private async Task<IReadOnlyList<ArchiveItem>> ListZipArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        var items = new List<ArchiveItem>();
        
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                items.Add(new ArchiveItem
                {
                    Name = Path.GetFileName(entry.FullName),
                    Path = entry.FullName,
                    Size = entry.Length,
                    PackedSize = entry.CompressedLength,
                    ModifiedTime = entry.LastWriteTime.DateTime,
                    IsDirectory = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'),
                    Crc = entry.Crc32
                });
            }
        }, cancellationToken);
        
        return items;
    }
    
    public async Task<bool> ExtractFromArchiveAsync(
        string archivePath, 
        string targetPath, 
        IEnumerable<string>? files = null,
        string? password = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await ExtractZipAsync(archivePath, targetPath, files, progress, cancellationToken);
        }
        
        return false;
    }
    
    private async Task<bool> ExtractZipAsync(
        string archivePath, 
        string targetPath, 
        IEnumerable<string>? files,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(archivePath);
                var entries = files != null 
                    ? archive.Entries.Where(e => files.Contains(e.FullName))
                    : archive.Entries;
                
                var entryList = entries.ToList();
                var total = entryList.Count;
                var processed = 0;
                
                foreach (var entry in entryList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;
                    
                    var destPath = Path.Combine(targetPath, entry.FullName);
                    var destDir = Path.GetDirectoryName(destPath);
                    
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(destPath, true);
                    }
                    
                    processed++;
                    progress?.Invoke(entry.FullName, (int)(processed * 100.0 / total));
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
    
    public async Task<bool> CreateArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        ArchiveOptions? options = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await CreateZipAsync(archivePath, files, options, progress, cancellationToken);
        }
        
        return false;
    }
    
    private async Task<bool> CreateZipAsync(
        string archivePath,
        IEnumerable<string> files,
        ArchiveOptions? options,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var level = options?.CompressionLevel switch
                {
                    0 => System.IO.Compression.CompressionLevel.NoCompression,
                    >= 1 and <= 4 => System.IO.Compression.CompressionLevel.Fastest,
                    >= 5 and <= 7 => System.IO.Compression.CompressionLevel.Optimal,
                    _ => System.IO.Compression.CompressionLevel.SmallestSize
                };
                
                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                var fileList = files.ToList();
                var total = fileList.Count;
                var processed = 0;
                
                foreach (var file in fileList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;
                    
                    if (File.Exists(file))
                    {
                        var entryName = Path.GetFileName(file);
                        archive.CreateEntryFromFile(file, entryName, level);
                    }
                    else if (Directory.Exists(file) && (options?.RecurseSubfolders ?? true))
                    {
                        AddDirectoryToZip(archive, file, "", level, cancellationToken);
                    }
                    
                    processed++;
                    progress?.Invoke(file, (int)(processed * 100.0 / total));
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
    
    private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryBase, System.IO.Compression.CompressionLevel level, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            if (cancellationToken.IsCancellationRequested) return;
            
            var entryName = string.IsNullOrEmpty(entryBase) 
                ? Path.GetFileName(file) 
                : Path.Combine(entryBase, Path.GetFileName(file));
            archive.CreateEntryFromFile(file, entryName, level);
        }
        
        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            if (cancellationToken.IsCancellationRequested) return;
            
            var dirName = Path.GetFileName(dir);
            var newBase = string.IsNullOrEmpty(entryBase) ? dirName : Path.Combine(entryBase, dirName);
            AddDirectoryToZip(archive, dir, newBase, level, cancellationToken);
        }
    }
    
    public async Task<bool> AddToArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        string? basePath = null,
        ArchiveOptions? options = null,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await AddToZipAsync(archivePath, files, basePath, options, progress, cancellationToken);
        }
        
        return false;
    }
    
    private async Task<bool> AddToZipAsync(
        string archivePath,
        IEnumerable<string> files,
        string? basePath,
        ArchiveOptions? options,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var level = options?.CompressionLevel switch
                {
                    0 => System.IO.Compression.CompressionLevel.NoCompression,
                    >= 1 and <= 4 => System.IO.Compression.CompressionLevel.Fastest,
                    >= 5 and <= 7 => System.IO.Compression.CompressionLevel.Optimal,
                    _ => System.IO.Compression.CompressionLevel.SmallestSize
                };
                
                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
                var fileList = files.ToList();
                var total = fileList.Count;
                var processed = 0;
                
                foreach (var file in fileList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;
                    
                    if (File.Exists(file))
                    {
                        var entryName = basePath != null && file.StartsWith(basePath)
                            ? file[basePath.Length..].TrimStart(Path.DirectorySeparatorChar)
                            : Path.GetFileName(file);
                        archive.CreateEntryFromFile(file, entryName, level);
                    }
                    
                    processed++;
                    progress?.Invoke(file, (int)(processed * 100.0 / total));
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
    
    public async Task<bool> DeleteFromArchiveAsync(
        string archivePath,
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await DeleteFromZipAsync(archivePath, files, cancellationToken);
        }
        
        return false;
    }
    
    private async Task<bool> DeleteFromZipAsync(
        string archivePath,
        IEnumerable<string> files,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var filesToDelete = files.ToHashSet();
                
                using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
                var toRemove = archive.Entries.Where(e => filesToDelete.Contains(e.FullName)).ToList();
                
                foreach (var entry in toRemove)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;
                    
                    entry.Delete();
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
    
    public async Task<PluginArchiveTestResult> TestArchiveAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (ext == ".zip")
        {
            return await TestZipAsync(archivePath, cancellationToken);
        }
        
        return new PluginArchiveTestResult { Success = false, ErrorMessage = "Unsupported archive format" };
    }
    
    private async Task<PluginArchiveTestResult> TestZipAsync(string archivePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(archivePath);
                var total = archive.Entries.Count;
                var tested = 0;
                var failed = new List<string>();
                
                foreach (var entry in archive.Entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new PluginArchiveTestResult
                        {
                            Success = false,
                            TotalFiles = total,
                            TestedFiles = tested,
                            ErrorMessage = "Test cancelled"
                        };
                    }
                    
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        try
                        {
                            using var stream = entry.Open();
                            // Read through the entry to verify
                            var buffer = new byte[8192];
                            while (stream.Read(buffer, 0, buffer.Length) > 0) { }
                            tested++;
                        }
                        catch
                        {
                            failed.Add(entry.FullName);
                        }
                    }
                }
                
                return new PluginArchiveTestResult
                {
                    Success = failed.Count == 0,
                    TotalFiles = total,
                    TestedFiles = tested,
                    FailedFiles = failed.Count,
                    FailedFileNames = failed
                };
            }
            catch (Exception ex)
            {
                return new PluginArchiveTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }, cancellationToken);
    }
    
    // ======= Content Plugin Operations =======
    
    public IReadOnlyList<PluginContentField> GetContentFields(string pluginId)
    {
        return _contentFields.TryGetValue(pluginId, out var fields) ? fields : Array.Empty<PluginContentField>();
    }
    
    public Task<object?> GetContentFieldValueAsync(
        string pluginId, 
        string fieldName, 
        string filePath, 
        CancellationToken cancellationToken = default)
    {
        if (pluginId == "builtin.fileprops")
        {
            return Task.FromResult(GetBuiltInFieldValue(fieldName, filePath));
        }

        if (_columnPlugins.TryGetValue(pluginId, out var columnPlugin))
        {
            return GetColumnValueAsync(columnPlugin, fieldName, filePath, cancellationToken);
        }
        
        return Task.FromResult<object?>(null);
    }

    private static async Task<object?> GetColumnValueAsync(
        IColumnPlugin columnPlugin,
        string fieldName,
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await columnPlugin.GetValueAsync(fieldName, filePath, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
    
    private object? GetBuiltInFieldValue(string fieldName, string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return null;
            
            return fieldName switch
            {
                "Name" => fileInfo.Name,
                "Extension" => fileInfo.Extension.TrimStart('.'),
                "Size" => fileInfo.Length,
                "Modified" => fileInfo.LastWriteTime,
                "Created" => fileInfo.CreationTime,
                "Accessed" => fileInfo.LastAccessTime,
                "Attributes" => fileInfo.Attributes.ToString(),
                "ReadOnly" => fileInfo.IsReadOnly,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
    
    public Task<bool> SetContentFieldValueAsync(
        string pluginId,
        string fieldName,
        string filePath,
        object value,
        CancellationToken cancellationToken = default)
    {
        // Most content fields are read-only
        return Task.FromResult(false);
    }
    
    public Task<int> CompareFilesAsync(
        string pluginId,
        string fieldName,
        string file1,
        string file2,
        CancellationToken cancellationToken = default)
    {
        if (pluginId == "builtin.fileprops")
        {
            var val1 = GetBuiltInFieldValue(fieldName, file1);
            var val2 = GetBuiltInFieldValue(fieldName, file2);
            
            if (val1 == null && val2 == null) return Task.FromResult(0);
            if (val1 == null) return Task.FromResult(-1);
            if (val2 == null) return Task.FromResult(1);
            
            if (val1 is IComparable comp1 && val2 is IComparable)
            {
                return Task.FromResult(comp1.CompareTo(val2));
            }
            
            return Task.FromResult(string.Compare(val1.ToString(), val2.ToString(), StringComparison.Ordinal));
        }
        
        return Task.FromResult(0);
    }
    
    // ======= File System Plugin Operations =======
    
    public Task<IFileSystemHandle?> ConnectAsync(string pluginId, string? connectionString = null, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult<IFileSystemHandle?>(null);
        }

        var handle = new FileSystemHandle(pluginId, connectionString);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            handle.CurrentPath = connectionString;
        }
        else if (pluginId == LocalFileSystemPluginId)
        {
            handle.CurrentPath = Environment.CurrentDirectory;
        }

        return Task.FromResult<IFileSystemHandle?>(handle);
    }
    
    public Task<IReadOnlyList<PluginFileSystemItem>> ListDirectoryAsync(
        string pluginId,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult<IReadOnlyList<PluginFileSystemItem>>(Array.Empty<PluginFileSystemItem>());
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return Task.FromResult<IReadOnlyList<PluginFileSystemItem>>(ListLocalDirectory(path));
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return ListRemoteDirectoryAsync(fileSystemPlugin, path, cancellationToken);
        }

        return Task.FromResult<IReadOnlyList<PluginFileSystemItem>>(Array.Empty<PluginFileSystemItem>());
    }
    
    public Task<bool> GetFileAsync(
        string pluginId,
        string remotePath,
        string localPath,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return CopyLocalFileAsync(remotePath, localPath, progress, cancellationToken);
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return DownloadFileAsync(fileSystemPlugin, remotePath, localPath, progress, cancellationToken);
        }

        return Task.FromResult(false);
    }
    
    public Task<bool> PutFileAsync(
        string pluginId,
        string localPath,
        string remotePath,
        PluginProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return CopyLocalFileAsync(localPath, remotePath, progress, cancellationToken);
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return UploadFileAsync(fileSystemPlugin, localPath, remotePath, progress, cancellationToken);
        }

        return Task.FromResult(false);
    }
    
    public Task<bool> DeleteFileAsync(string pluginId, string path, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return DeleteLocalPathAsync(path, recursive: false, cancellationToken);
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return DeleteRemotePathAsync(fileSystemPlugin, path, recursive: false, cancellationToken);
        }

        return Task.FromResult(false);
    }
    
    public Task<bool> CreateDirectoryAsync(string pluginId, string path, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return CreateLocalDirectoryAsync(path, cancellationToken);
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return CreateRemoteDirectoryAsync(fileSystemPlugin, path, cancellationToken);
        }

        return Task.FromResult(false);
    }
    
    public Task<bool> RemoveDirectoryAsync(string pluginId, string path, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            return DeleteLocalPathAsync(path, recursive: true, cancellationToken);
        }

        if (_fileSystemPlugins.TryGetValue(pluginId, out var fileSystemPlugin))
        {
            return DeleteRemotePathAsync(fileSystemPlugin, path, recursive: true, cancellationToken);
        }

        return Task.FromResult(false);
    }
    
    public Task<bool> ExecuteFileAsync(string pluginId, string path, string? parameters = null, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin) || plugin.Type != PluginType.FileSystem || !plugin.IsEnabled)
        {
            return Task.FromResult(false);
        }

        if (pluginId == LocalFileSystemPluginId)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = parameters ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(processInfo);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }

    private static IReadOnlyList<PluginFileSystemItem> ListLocalDirectory(string path)
    {
        var resolvedPath = NormalizeLocalPath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return Array.Empty<PluginFileSystemItem>();

        try
        {
            var directoryInfo = new DirectoryInfo(resolvedPath);
            if (!directoryInfo.Exists)
                return Array.Empty<PluginFileSystemItem>();

            var items = new List<PluginFileSystemItem>();

            foreach (var dir in directoryInfo.EnumerateDirectories())
            {
                items.Add(new PluginFileSystemItem
                {
                    Name = dir.Name,
                    Path = dir.FullName,
                    Size = 0,
                    ModifiedTime = dir.LastWriteTime,
                    CreatedTime = dir.CreationTime,
                    AccessedTime = dir.LastAccessTime,
                    Attributes = dir.Attributes,
                    IsDirectory = true
                });
            }

            foreach (var file in directoryInfo.EnumerateFiles())
            {
                items.Add(new PluginFileSystemItem
                {
                    Name = file.Name,
                    Path = file.FullName,
                    Size = file.Length,
                    ModifiedTime = file.LastWriteTime,
                    CreatedTime = file.CreationTime,
                    AccessedTime = file.LastAccessTime,
                    Attributes = file.Attributes,
                    IsDirectory = false
                });
            }

            return items;
        }
        catch
        {
            return Array.Empty<PluginFileSystemItem>();
        }
    }

    private static string NormalizeLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Environment.CurrentDirectory;

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
            return uri.LocalPath;

        return path;
    }

    private static async Task<IReadOnlyList<PluginFileSystemItem>> ListRemoteDirectoryAsync(
        IFileSystemPlugin plugin,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await plugin.ListDirectoryAsync(path, cancellationToken);
            return items.Select(ConvertFileItem).ToList();
        }
        catch
        {
            return Array.Empty<PluginFileSystemItem>();
        }
    }

    private static async Task<bool> CopyLocalFileAsync(
        string sourcePath,
        string destinationPath,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedSource = NormalizeLocalPath(sourcePath);
            var resolvedDestination = NormalizeLocalPath(destinationPath);
            var destDir = Path.GetDirectoryName(resolvedDestination);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            await using var source = new FileStream(resolvedSource, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destination = new FileStream(resolvedDestination, FileMode.Create, FileAccess.Write, FileShare.None);
            return await CopyStreamAsync(source, destination, resolvedSource, progress, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> DownloadFileAsync(
        IFileSystemPlugin plugin,
        string remotePath,
        string localPath,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedLocal = NormalizeLocalPath(localPath);
            var destDir = Path.GetDirectoryName(resolvedLocal);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            await using var remoteStream = await plugin.OpenReadAsync(remotePath, cancellationToken);
            await using var localStream = new FileStream(resolvedLocal, FileMode.Create, FileAccess.Write, FileShare.None);
            return await CopyStreamAsync(remoteStream, localStream, remotePath, progress, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> UploadFileAsync(
        IFileSystemPlugin plugin,
        string localPath,
        string remotePath,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedLocal = NormalizeLocalPath(localPath);
            await using var localStream = new FileStream(resolvedLocal, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var remoteStream = await plugin.OpenWriteAsync(remotePath, cancellationToken);
            return await CopyStreamAsync(localStream, remoteStream, resolvedLocal, progress, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> DeleteLocalPathAsync(
        string path,
        bool recursive,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolved = NormalizeLocalPath(path);
            if (File.Exists(resolved))
            {
                File.Delete(resolved);
                return true;
            }

            if (Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return await Task.FromResult(false);
    }

    private static async Task<bool> DeleteRemotePathAsync(
        IFileSystemPlugin plugin,
        string path,
        bool recursive,
        CancellationToken cancellationToken)
    {
        try
        {
            await plugin.DeleteAsync(path, recursive, cancellationToken);
            return true;
        }
        catch
        {
            return await Task.FromResult(false);
        }
    }

    private static async Task<bool> CreateLocalDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var resolved = NormalizeLocalPath(path);
            Directory.CreateDirectory(resolved);
            return await Task.FromResult(true);
        }
        catch
        {
            return await Task.FromResult(false);
        }
    }

    private static async Task<bool> CreateRemoteDirectoryAsync(
        IFileSystemPlugin plugin,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await plugin.CreateDirectoryAsync(path, cancellationToken);
            return true;
        }
        catch
        {
            return await Task.FromResult(false);
        }
    }

    private static async Task<bool> CopyStreamAsync(
        Stream source,
        Stream destination,
        string progressLabel,
        PluginProgressCallback? progress,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];
        long total = source.CanSeek ? source.Length : -1;
        long processed = 0;
        var lastPercent = -1;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead <= 0)
                break;

            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            processed += bytesRead;

            if (total > 0)
            {
                var percent = (int)(processed * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    if (progress != null && !progress(progressLabel, percent))
                        return false;
                }
            }
        }

        progress?.Invoke(progressLabel, 100);
        return true;
    }

    private static PluginFileSystemItem ConvertFileItem(PluginFileItem item)
    {
        var attributes = FileAttributes.Normal;
        if (!string.IsNullOrEmpty(item.Attributes) &&
            Enum.TryParse<FileAttributes>(item.Attributes, out var parsedAttributes))
        {
            attributes = parsedAttributes;
        }

        return new PluginFileSystemItem
        {
            Name = item.Name,
            Path = item.FullPath,
            Size = item.Size,
            ModifiedTime = item.LastModified ?? DateTime.MinValue,
            CreatedTime = item.Created,
            Attributes = attributes,
            IsDirectory = item.IsDirectory,
            CustomProperties = item.CustomProperties != null
                ? new Dictionary<string, string>(item.CustomProperties.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty))
                : new Dictionary<string, string>()
        };
    }
    
    // ======= Lister Plugin Operations =======
    
    public Task<IListerHandle?> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath);
        var plugin = GetPluginForExtension(PluginType.Lister, ext);
        
        if (plugin == null)
        {
            return Task.FromResult<IListerHandle?>(null);
        }
        
        return Task.FromResult<IListerHandle?>(new ListerHandle(filePath, plugin));
    }
    
    public Task<bool> SearchTextAsync(IListerHandle handle, string searchText, bool caseSensitive = false, CancellationToken cancellationToken = default)
    {
        // Lister would search in displayed content
        return Task.FromResult(false);
    }
    
    public Task<bool> PrintAsync(IListerHandle handle, CancellationToken cancellationToken = default)
    {
        // Lister would print the content
        return Task.FromResult(false);
    }
    
    public async Task<string?> GetTextAsync(IListerHandle handle, CancellationToken cancellationToken = default)
    {
        try
        {
            return await File.ReadAllTextAsync(handle.FilePath, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    // ======= Plugin Loading Helpers =======

    private static async Task<PluginManifest?> LoadManifestAsync(
        string pluginPath,
        string pluginDirectory,
        CancellationToken cancellationToken)
    {
        string? manifestPath = null;

        if (Directory.Exists(pluginPath))
        {
            manifestPath = Path.Combine(pluginPath, "plugin.json");
        }
        else if (File.Exists(pluginPath))
        {
            if (pluginPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                manifestPath = pluginPath;
            }
            else
            {
                var sidecar = Path.ChangeExtension(pluginPath, ".json");
                if (File.Exists(sidecar))
                {
                    manifestPath = sidecar;
                }
                else
                {
                    manifestPath = Path.Combine(pluginDirectory, "plugin.json");
                }
            }
        }

        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            return JsonSerializer.Deserialize<PluginManifest>(json, ManifestJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveAssemblyPath(string pluginPath, string pluginDirectory, PluginManifest? manifest)
    {
        if (File.Exists(pluginPath) && Path.GetExtension(pluginPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            return pluginPath;

        if (!string.IsNullOrWhiteSpace(manifest?.Assembly))
        {
            var assemblyPath = Path.Combine(pluginDirectory, manifest.Assembly);
            if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                assemblyPath += ".dll";
            return assemblyPath;
        }

        if (Directory.Exists(pluginPath))
        {
            var dirName = Path.GetFileName(pluginPath);
            var preferred = Path.Combine(pluginPath, $"{dirName}.dll");
            if (File.Exists(preferred))
                return preferred;

            var dlls = Directory.GetFiles(pluginPath, "*.dll");
            return dlls.FirstOrDefault();
        }

        return null;
    }

    private static IPlugin? LoadPluginInstance(string assemblyPath, string? pluginTypeName)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            if (!string.IsNullOrWhiteSpace(pluginTypeName))
            {
                pluginTypes = pluginTypes.Where(t => string.Equals(t.FullName, pluginTypeName, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var pluginType in pluginTypes)
            {
                var instance = Activator.CreateInstance(pluginType) as IPlugin;
                if (instance != null)
                    return instance;
            }
        }
        catch
        {
            // Ignore load failures
        }

        return null;
    }

    private static PluginType ResolvePluginType(string pluginPath, PluginManifest? manifest, IPlugin? instance)
    {
        if (!string.IsNullOrWhiteSpace(manifest?.Type) &&
            Enum.TryParse<PluginType>(manifest.Type, true, out var manifestType))
        {
            return manifestType;
        }

        if (instance is IFileSystemPlugin)
            return PluginType.FileSystem;
        if (instance is IPackerPlugin)
            return PluginType.Packer;
        if (instance is IViewerPlugin)
            return PluginType.Lister;
        if (instance is IColumnPlugin)
            return PluginType.Content;

        var extension = Path.GetExtension(pluginPath).ToLowerInvariant();
        return extension switch
        {
            ".wcx" or ".wcx64" => PluginType.Packer,
            ".wdx" or ".wdx64" => PluginType.Content,
            ".wfx" or ".wfx64" => PluginType.FileSystem,
            ".wlx" or ".wlx64" => PluginType.Lister,
            _ => PluginType.Packer
        };
    }

    private static IReadOnlyList<string> ResolveExtensions(
        string pluginPath,
        PluginManifest? manifest,
        IPlugin? instance,
        PluginType pluginType)
    {
        var extensions = new List<string>();

        if (manifest?.Extensions != null && manifest.Extensions.Count > 0)
        {
            extensions.AddRange(manifest.Extensions);
        }
        else
        {
            if (instance is IPackerPlugin packer)
                extensions.AddRange(packer.SupportedExtensions);
            else if (instance is IViewerPlugin viewer)
                extensions.AddRange(viewer.SupportedExtensions);
        }

        if (extensions.Count == 0)
        {
            return Array.Empty<string>();
        }

        return NormalizeExtensions(extensions);
    }

    private static PluginCapabilities ResolveCapabilities(
        PluginType pluginType,
        PluginManifest? manifest,
        IPlugin? instance)
    {
        if (manifest?.Capabilities != null && manifest.Capabilities.Count > 0)
        {
            var capabilities = PluginCapabilities.None;
            foreach (var capability in manifest.Capabilities)
            {
                if (Enum.TryParse<PluginCapabilities>(capability, true, out var parsed))
                    capabilities |= parsed;
            }
            return capabilities;
        }

        return pluginType switch
        {
            PluginType.Packer => PluginCapabilities.CanCreate | PluginCapabilities.CanExtract,
            PluginType.Content => PluginCapabilities.CanSearch | PluginCapabilities.CanSort,
            PluginType.FileSystem => PluginCapabilities.CanRead | PluginCapabilities.CanWrite | PluginCapabilities.CanCreateFolder | PluginCapabilities.CanDelete,
            PluginType.Lister => PluginCapabilities.SupportsText | PluginCapabilities.SupportsImage,
            _ => PluginCapabilities.None
        };
    }

    private static string? ResolveConfigPath(string pluginDirectory, PluginManifest? manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest?.ConfigPath))
        {
            return Path.IsPathRooted(manifest.ConfigPath)
                ? manifest.ConfigPath
                : Path.Combine(pluginDirectory, manifest.ConfigPath);
        }

        return null;
    }

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string> extensions)
    {
        return extensions
            .Select(ext => ext.StartsWith('.') ? ext : "." + ext)
            .Select(ext => ext.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private void RegisterExtensionMappings(PluginInfo plugin)
    {
        foreach (var ext in NormalizeExtensions(plugin.Extensions))
        {
            var key = $"{plugin.Type}:{ext}";
            if (!_extensionToPlugin.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _extensionToPlugin[key] = list;
            }

            if (!list.Contains(plugin.Id))
            {
                list.Add(plugin.Id);
            }
        }
    }

    private void RemoveExtensionMappings(PluginInfo plugin)
    {
        foreach (var entry in _extensionToPlugin)
        {
            entry.Value.Remove(plugin.Id);
        }
    }

    private async Task InitializePluginInstanceAsync(string pluginId, IPlugin plugin, CancellationToken cancellationToken)
    {
        if (_pluginInitialized.TryGetValue(pluginId, out var initialized) && initialized)
            return;

        try
        {
            await plugin.InitializeAsync(_pluginContext);
            _pluginInitialized[pluginId] = true;
        }
        catch
        {
            _pluginInitialized[pluginId] = false;
        }
    }
    
    private void OnPluginLoaded(PluginInfo plugin)
    {
        PluginLoaded?.Invoke(this, new PluginEventArgs(plugin));
    }
    
    private void OnPluginUnloaded(PluginInfo plugin)
    {
        PluginUnloaded?.Invoke(this, new PluginEventArgs(plugin));
    }
    
    private void OnPluginsChanged()
    {
        PluginsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class PluginManifest
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
        public string? Website { get; set; }
        public string? Type { get; set; }
        public List<string>? Extensions { get; set; }
        public List<string>? DetectStrings { get; set; }
        public List<string>? Capabilities { get; set; }
        public string? Assembly { get; set; }
        public string? PluginTypeName { get; set; }
        public string? ConfigPath { get; set; }
        public bool? Enabled { get; set; }
    }

    private sealed class ServicePluginContext : IPluginContext
    {
        private readonly Dictionary<string, object> _config = new();
        private string _activePath = Environment.CurrentDirectory;

        public string LeftPanelPath => _activePath;
        public string RightPanelPath => _activePath;
        public string ActivePanelPath => _activePath;
        public IReadOnlyList<string> SelectedPaths => Array.Empty<string>();

        public void NavigateTo(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                _activePath = path;
        }

        public void NavigateLeftTo(string path) => NavigateTo(path);

        public void NavigateRightTo(string path) => NavigateTo(path);

        public void RefreshActivePanel()
        {
            // No-op in service context
        }

        public void RefreshAllPanels()
        {
            // No-op in service context
        }

        public Task ShowMessageAsync(string title, string message)
        {
            Debug.WriteLine($"[{title}] {message}");
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            Debug.WriteLine($"[{title}] {message}");
            return Task.FromResult(false);
        }

        public Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "")
        {
            Debug.WriteLine($"[{title}] {prompt}");
            return Task.FromResult<string?>(defaultValue);
        }

        public void Log(PluginLogLevel level, string message)
        {
            var prefix = level.ToString().ToUpperInvariant();
            Debug.WriteLine($"[{prefix}] {message}");
        }

        public T? GetConfig<T>(string key)
        {
            if (_config.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        public void SetConfig<T>(string key, T value)
        {
            if (value == null)
                _config.Remove(key);
            else
                _config[key] = value;
        }

        public void RegisterMenuItem(PluginMenuItem menuItem)
        {
            // No-op in service context
        }

        public void RegisterKeyboardShortcut(PluginKeyboardShortcut shortcut)
        {
            // No-op in service context
        }

        public string GetPluginDataDirectory(string pluginId)
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pluginDataDir = Path.Combine(baseDir, "XCommander", "Plugins", pluginId);
            Directory.CreateDirectory(pluginDataDir);
            return pluginDataDir;
        }
    }
    
    // Handle implementations
    
    private class PackerHandle : IPackerHandle
    {
        public string ArchivePath { get; }
        public PluginInfo Plugin { get; }
        public bool IsOpen { get; private set; } = true;
        
        public PackerHandle(string archivePath, PluginInfo plugin)
        {
            ArchivePath = archivePath;
            Plugin = plugin;
        }
        
        public void Dispose()
        {
            IsOpen = false;
        }
    }
    
    private class FileSystemHandle : IFileSystemHandle
    {
        public string PluginId { get; }
        public string? ConnectionString { get; }
        public bool IsConnected { get; private set; } = true;
        public string CurrentPath { get; set; } = "/";
        
        public FileSystemHandle(string pluginId, string? connectionString)
        {
            PluginId = pluginId;
            ConnectionString = connectionString;
        }
        
        public void Dispose()
        {
            IsConnected = false;
        }
    }
    
    private class ListerHandle : IListerHandle
    {
        public string FilePath { get; }
        public PluginInfo Plugin { get; }
        public bool IsLoaded { get; private set; } = true;
        public nint WindowHandle => nint.Zero;
        
        public ListerHandle(string filePath, PluginInfo plugin)
        {
            FilePath = filePath;
            Plugin = plugin;
        }
        
        public void Dispose()
        {
            IsLoaded = false;
        }
    }
}
