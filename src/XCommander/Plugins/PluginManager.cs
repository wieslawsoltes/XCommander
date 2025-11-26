using System.Reflection;
using System.Runtime.Loader;

namespace XCommander.Plugins;

/// <summary>
/// Plugin metadata stored in a JSON file alongside plugins.
/// </summary>
public class PluginMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Author { get; init; }
    public string? AssemblyName { get; init; }
    public string? PluginTypeName { get; init; }
    public bool Enabled { get; init; } = true;
    public List<string>? Dependencies { get; init; }
}

/// <summary>
/// Represents a loaded plugin instance.
/// </summary>
public class LoadedPlugin
{
    public required PluginMetadata Metadata { get; init; }
    public required IPlugin Instance { get; init; }
    public required string PluginDirectory { get; init; }
    public Assembly? Assembly { get; init; }
    public AssemblyLoadContext? LoadContext { get; init; }
    public bool IsInitialized { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Exception? LoadError { get; set; }
}

/// <summary>
/// Service for discovering, loading, and managing plugins.
/// </summary>
public class PluginManager : IDisposable
{
    private readonly string _pluginsDirectory;
    private readonly List<LoadedPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, IPlugin> _pluginsById = new();
    private readonly Dictionary<Type, List<IPlugin>> _pluginsByType = new();
    private IPluginContext? _context;
    private bool _disposed;

    public PluginManager(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? GetDefaultPluginsDirectory();
        
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    private static string GetDefaultPluginsDirectory()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDir, "Plugins");
    }

    /// <summary>
    /// All loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// Get all plugins of a specific type.
    /// </summary>
    public IEnumerable<T> GetPlugins<T>() where T : IPlugin
    {
        if (_pluginsByType.TryGetValue(typeof(T), out var plugins))
        {
            return plugins.OfType<T>();
        }
        return Enumerable.Empty<T>();
    }

    /// <summary>
    /// Get a plugin by its ID.
    /// </summary>
    public IPlugin? GetPlugin(string id)
    {
        return _pluginsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Get a plugin by its ID with type cast.
    /// </summary>
    public T? GetPlugin<T>(string id) where T : class, IPlugin
    {
        return _pluginsById.GetValueOrDefault(id) as T;
    }

    /// <summary>
    /// Discover and load all plugins from the plugins directory.
    /// </summary>
    public async Task DiscoverAndLoadPluginsAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        
        if (!Directory.Exists(_pluginsDirectory))
            return;

        // Look for plugin directories
        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            try
            {
                await LoadPluginFromDirectoryAsync(pluginDir, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading plugin from {pluginDir}: {ex.Message}");
            }
        }

        // Also load any DLL files directly in the plugins directory
        foreach (var dllFile in Directory.GetFiles(_pluginsDirectory, "*.dll"))
        {
            try
            {
                await LoadPluginFromAssemblyAsync(dllFile, _pluginsDirectory, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading plugin from {dllFile}: {ex.Message}");
            }
        }
    }

    private async Task LoadPluginFromDirectoryAsync(string pluginDir, CancellationToken cancellationToken)
    {
        // Look for a plugin.json metadata file
        var metadataPath = Path.Combine(pluginDir, "plugin.json");
        PluginMetadata? metadata = null;
        
        if (File.Exists(metadataPath))
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            metadata = System.Text.Json.JsonSerializer.Deserialize<PluginMetadata>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        // Find the main assembly
        string? assemblyPath = null;
        
        if (metadata?.AssemblyName != null)
        {
            assemblyPath = Path.Combine(pluginDir, metadata.AssemblyName);
            if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyPath += ".dll";
            }
        }
        else
        {
            // Look for DLL files in the directory
            var dlls = Directory.GetFiles(pluginDir, "*.dll");
            // Prefer one that matches the directory name
            var dirName = Path.GetFileName(pluginDir);
            assemblyPath = dlls.FirstOrDefault(d => Path.GetFileNameWithoutExtension(d).Equals(dirName, StringComparison.OrdinalIgnoreCase))
                          ?? dlls.FirstOrDefault();
        }

        if (assemblyPath != null && File.Exists(assemblyPath))
        {
            await LoadPluginFromAssemblyAsync(assemblyPath, pluginDir, cancellationToken, metadata);
        }
    }

    private async Task LoadPluginFromAssemblyAsync(string assemblyPath, string pluginDir, CancellationToken cancellationToken, PluginMetadata? metadata = null)
    {
        // Create a custom load context for isolation
        var loadContext = new PluginLoadContext(assemblyPath);
        
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            
            // Find types that implement IPlugin
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            foreach (var pluginType in pluginTypes)
            {
                // Check if this specific type should be loaded based on metadata
                if (metadata?.PluginTypeName != null && !pluginType.FullName!.Equals(metadata.PluginTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var instance = (IPlugin?)Activator.CreateInstance(pluginType);
                    if (instance == null)
                        continue;

                    // Use metadata or create from instance
                    var pluginMetadata = metadata ?? new PluginMetadata
                    {
                        Id = instance.Id,
                        Name = instance.Name,
                        Description = instance.Description,
                        Version = instance.Version.ToString(),
                        Author = instance.Author
                    };

                    var loadedPlugin = new LoadedPlugin
                    {
                        Metadata = pluginMetadata,
                        Instance = instance,
                        PluginDirectory = pluginDir,
                        Assembly = assembly,
                        LoadContext = loadContext
                    };

                    _loadedPlugins.Add(loadedPlugin);
                    _pluginsById[instance.Id] = instance;
                    
                    // Register by type
                    RegisterPluginByType(instance);

                    // Initialize the plugin
                    if (_context != null && pluginMetadata.Enabled)
                    {
                        await InitializePluginAsync(loadedPlugin, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating plugin instance {pluginType.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading assembly {assemblyPath}: {ex.Message}");
        }
    }

    private void RegisterPluginByType(IPlugin plugin)
    {
        var interfaces = plugin.GetType().GetInterfaces()
            .Where(i => typeof(IPlugin).IsAssignableFrom(i) && i != typeof(IPlugin));

        foreach (var iface in interfaces)
        {
            if (!_pluginsByType.TryGetValue(iface, out var list))
            {
                list = new List<IPlugin>();
                _pluginsByType[iface] = list;
            }
            list.Add(plugin);
        }
    }

    private async Task InitializePluginAsync(LoadedPlugin loadedPlugin, CancellationToken cancellationToken)
    {
        if (loadedPlugin.IsInitialized || _context == null)
            return;

        try
        {
            await loadedPlugin.Instance.InitializeAsync(_context);
            loadedPlugin.IsInitialized = true;
        }
        catch (Exception ex)
        {
            loadedPlugin.LoadError = ex;
            loadedPlugin.IsEnabled = false;
            System.Diagnostics.Debug.WriteLine($"Error initializing plugin {loadedPlugin.Metadata.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Enable a plugin.
    /// </summary>
    public async Task EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var loadedPlugin = _loadedPlugins.FirstOrDefault(p => p.Metadata.Id == pluginId);
        if (loadedPlugin == null || loadedPlugin.IsEnabled)
            return;

        loadedPlugin.IsEnabled = true;
        if (!loadedPlugin.IsInitialized && _context != null)
        {
            await InitializePluginAsync(loadedPlugin, cancellationToken);
        }
    }

    /// <summary>
    /// Disable a plugin.
    /// </summary>
    public async Task DisablePluginAsync(string pluginId)
    {
        var loadedPlugin = _loadedPlugins.FirstOrDefault(p => p.Metadata.Id == pluginId);
        if (loadedPlugin == null || !loadedPlugin.IsEnabled)
            return;

        loadedPlugin.IsEnabled = false;
        
        if (loadedPlugin.IsInitialized)
        {
            try
            {
                await loadedPlugin.Instance.ShutdownAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error shutting down plugin {pluginId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Unload all plugins.
    /// </summary>
    public async Task UnloadAllPluginsAsync()
    {
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.IsInitialized && loadedPlugin.IsEnabled)
            {
                try
                {
                    await loadedPlugin.Instance.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error shutting down plugin {loadedPlugin.Metadata.Id}: {ex.Message}");
                }
            }
        }

        _loadedPlugins.Clear();
        _pluginsById.Clear();
        _pluginsByType.Clear();
    }

    /// <summary>
    /// Get viewer plugin that can handle a file.
    /// </summary>
    public IViewerPlugin? GetViewerForFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return GetPlugins<IViewerPlugin>()
            .Where(v => v.CanView(filePath))
            .OrderByDescending(v => v.Priority)
            .FirstOrDefault();
    }

    /// <summary>
    /// Get file system plugin that handles a protocol.
    /// </summary>
    public IFileSystemPlugin? GetFileSystemPluginForProtocol(string path)
    {
        foreach (var plugin in GetPlugins<IFileSystemPlugin>())
        {
            if (path.StartsWith(plugin.Protocol, StringComparison.OrdinalIgnoreCase))
            {
                return plugin;
            }
        }
        return null;
    }

    /// <summary>
    /// Get packer plugin that can handle an archive.
    /// </summary>
    public IPackerPlugin? GetPackerForFile(string filePath)
    {
        return GetPlugins<IPackerPlugin>()
            .FirstOrDefault(p => p.CanHandle(filePath));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        // Unload plugins synchronously
        foreach (var loadedPlugin in _loadedPlugins)
        {
            try
            {
                loadedPlugin.Instance.ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _loadedPlugins.Clear();
        _pluginsById.Clear();
        _pluginsByType.Clear();
    }
}

/// <summary>
/// Custom assembly load context for plugin isolation.
/// </summary>
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to default context for shared assemblies
        return null;
    }
}
