namespace XCommander.Plugins;

/// <summary>
/// Base interface for all XCommander plugins.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Display name of the plugin.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Plugin description.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Plugin version.
    /// </summary>
    Version Version { get; }
    
    /// <summary>
    /// Plugin author.
    /// </summary>
    string Author { get; }
    
    /// <summary>
    /// Called when the plugin is loaded.
    /// </summary>
    Task InitializeAsync(IPluginContext context);
    
    /// <summary>
    /// Called when the plugin is being unloaded.
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// Plugin for handling specific file types in file panels.
/// </summary>
public interface IFileSystemPlugin : IPlugin
{
    /// <summary>
    /// Protocol prefix this plugin handles (e.g., "ftp://", "sftp://", "archive://").
    /// </summary>
    string Protocol { get; }
    
    /// <summary>
    /// List directory contents.
    /// </summary>
    Task<IEnumerable<PluginFileItem>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Read a file.
    /// </summary>
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Write to a file.
    /// </summary>
    Task<Stream> OpenWriteAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a path exists.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy a file.
    /// </summary>
    Task CopyAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Move a file.
    /// </summary>
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a file or directory.
    /// </summary>
    Task DeleteAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a directory.
    /// </summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin for viewing specific file types.
/// </summary>
public interface IViewerPlugin : IPlugin
{
    /// <summary>
    /// File extensions this viewer handles (e.g., ".jpg", ".png").
    /// </summary>
    IEnumerable<string> SupportedExtensions { get; }
    
    /// <summary>
    /// MIME types this viewer handles.
    /// </summary>
    IEnumerable<string> SupportedMimeTypes { get; }
    
    /// <summary>
    /// Priority when multiple plugins handle the same type (higher = preferred).
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Check if this viewer can handle the given file.
    /// </summary>
    bool CanView(string filePath);
    
    /// <summary>
    /// Create a viewer control for the file.
    /// </summary>
    Task<object> CreateViewerAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin for custom column providers in file panels.
/// </summary>
public interface IColumnPlugin : IPlugin
{
    /// <summary>
    /// Get the columns this plugin provides.
    /// </summary>
    IEnumerable<PluginColumn> GetColumns();
    
    /// <summary>
    /// Get the value for a specific column and file.
    /// </summary>
    Task<object?> GetValueAsync(string columnId, string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin for packer/archive support.
/// </summary>
public interface IPackerPlugin : IPlugin
{
    /// <summary>
    /// File extensions this packer handles.
    /// </summary>
    IEnumerable<string> SupportedExtensions { get; }
    
    /// <summary>
    /// Check if this packer can handle the given file.
    /// </summary>
    bool CanHandle(string filePath);
    
    /// <summary>
    /// List contents of an archive.
    /// </summary>
    Task<IEnumerable<PluginArchiveEntry>> ListContentsAsync(string archivePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract files from an archive.
    /// </summary>
    Task ExtractAsync(string archivePath, string destinationPath, IEnumerable<string>? entries = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create an archive.
    /// </summary>
    Task CreateAsync(string archivePath, IEnumerable<string> sourcePaths, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin that provides menu items and commands.
/// </summary>
public interface ICommandPlugin : IPlugin
{
    /// <summary>
    /// Get the commands this plugin provides.
    /// </summary>
    IEnumerable<PluginCommand> GetCommands();
    
    /// <summary>
    /// Execute a command.
    /// </summary>
    Task ExecuteCommandAsync(string commandId, IPluginContext context, CancellationToken cancellationToken = default);
}
