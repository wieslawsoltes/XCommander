using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Plugins;

namespace XCommander.ViewModels;

/// <summary>
/// Item representing an available plugin in the marketplace.
/// </summary>
public partial class MarketplacePluginItem : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _description = string.Empty;
    
    [ObservableProperty]
    private string _version = string.Empty;
    
    [ObservableProperty]
    private string _author = string.Empty;
    
    [ObservableProperty]
    private bool _isInstalled;
    
    [ObservableProperty]
    private bool _hasUpdate;
    
    [ObservableProperty]
    private string _category = "General";
    
    [ObservableProperty]
    private string _icon = "🧩";
    
    [ObservableProperty]
    private int _downloads;
    
    [ObservableProperty]
    private double _rating;
}

/// <summary>
/// ViewModel for plugin marketplace/manager dialog (extends PluginsViewModel).
/// </summary>
public partial class PluginMarketplaceViewModel : ViewModelBase
{
    private readonly PluginManager? _pluginManager;
    
    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    [ObservableProperty]
    private string _selectedCategory = "All";
    
    [ObservableProperty]
    private MarketplacePluginItem? _selectedMarketplacePlugin;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private int _installedCount;
    
    [ObservableProperty]
    private int _availableCount;
    
    [ObservableProperty]
    private bool _showInstalled = true;
    
    [ObservableProperty]
    private bool _showAvailable = true;
    
    public ObservableCollection<MarketplacePluginItem> AllPlugins { get; } = [];
    public ObservableCollection<MarketplacePluginItem> FilteredPlugins { get; } = [];
    
    public ObservableCollection<string> Categories { get; } =
    [
        "All",
        "File System",
        "Archives",
        "Network",
        "Preview",
        "Columns",
        "Commands",
        "Themes"
    ];
    
    public PluginMarketplaceViewModel(PluginManager? pluginManager = null)
    {
        _pluginManager = pluginManager;
        LoadInstalledPlugins();
    }
    
    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }
    
    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }
    
    partial void OnShowInstalledChanged(bool value)
    {
        ApplyFilter();
    }
    
    partial void OnShowAvailableChanged(bool value)
    {
        ApplyFilter();
    }
    
    private void LoadInstalledPlugins()
    {
        AllPlugins.Clear();
        
        if (_pluginManager != null)
        {
            foreach (var plugin in _pluginManager.LoadedPlugins)
            {
                var item = new MarketplacePluginItem
                {
                    Id = plugin.Metadata.Id,
                    Name = plugin.Metadata.Name,
                    Description = plugin.Metadata.Description ?? "No description available",
                    Version = plugin.Metadata.Version ?? "1.0.0",
                    Author = plugin.Metadata.Author ?? "Unknown",
                    IsInstalled = true,
                    Category = GetPluginCategory(plugin.Instance)
                };
                AllPlugins.Add(item);
            }
        }
        
        InstalledCount = AllPlugins.Count(p => p.IsInstalled);
        AvailableCount = AllPlugins.Count(p => !p.IsInstalled);
        ApplyFilter();
    }
    
    private static string GetPluginCategory(IPlugin plugin)
    {
        if (plugin is IFileSystemPlugin) return "File System";
        if (plugin is IPackerPlugin) return "Archives";
        if (plugin is IViewerPlugin) return "Preview";
        if (plugin is IColumnPlugin) return "Columns";
        if (plugin is ICommandPlugin) return "Commands";
        return "General";
    }
    
    private void ApplyFilter()
    {
        FilteredPlugins.Clear();
        
        foreach (var plugin in AllPlugins)
        {
            // Filter by installed/available
            if (plugin.IsInstalled && !ShowInstalled) continue;
            if (!plugin.IsInstalled && !ShowAvailable) continue;
            
            // Filter by search query
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                if (!plugin.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) &&
                    !plugin.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) &&
                    !plugin.Author.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            
            // Filter by category
            if (SelectedCategory != "All" && plugin.Category != SelectedCategory)
                continue;
            
            FilteredPlugins.Add(plugin);
        }
    }
    
    [RelayCommand]
    private async Task InstallPluginAsync(MarketplacePluginItem? plugin)
    {
        if (plugin == null || plugin.IsInstalled)
            return;
            
        IsLoading = true;
        StatusText = $"Installing {plugin.Name}...";
        
        try
        {
            // TODO: Implement actual download and installation
            await Task.Delay(1000);
            
            plugin.IsInstalled = true;
            InstalledCount++;
            AvailableCount--;
            
            StatusText = $"Plugin '{plugin.Name}' installed successfully. Restart to activate.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to install plugin: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private async Task UninstallPluginAsync(MarketplacePluginItem? plugin)
    {
        if (plugin == null || !plugin.IsInstalled)
            return;
            
        IsLoading = true;
        StatusText = $"Uninstalling {plugin.Name}...";
        
        try
        {
            // TODO: Implement actual uninstallation
            await Task.Delay(500);
            
            plugin.IsInstalled = false;
            InstalledCount--;
            AvailableCount++;
            
            StatusText = $"Plugin '{plugin.Name}' uninstalled. Restart required.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to uninstall plugin: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private async Task RefreshMarketplaceAsync()
    {
        IsLoading = true;
        StatusText = "Fetching available plugins...";
        
        try
        {
            // Simulate marketplace API call
            await Task.Delay(500);
            
            // Add sample available plugins (not yet installed)
            var availablePlugins = new[]
            {
                new MarketplacePluginItem
                {
                    Id = "marketplace.sftp",
                    Name = "SFTP File System",
                    Description = "Adds secure FTP (SFTP) file system support with key-based authentication",
                    Version = "1.2.0",
                    Author = "XCommander Team",
                    Category = "Network",
                    Icon = "🔒",
                    Downloads = 15420,
                    Rating = 4.8
                },
                new MarketplacePluginItem
                {
                    Id = "marketplace.7zip",
                    Name = "7-Zip Integration",
                    Description = "Full support for 7z, XZ, and other 7-Zip archive formats",
                    Version = "2.0.0",
                    Author = "XCommander Team",
                    Category = "Archives",
                    Icon = "📦",
                    Downloads = 28350,
                    Rating = 4.9
                },
                new MarketplacePluginItem
                {
                    Id = "marketplace.pdf-preview",
                    Name = "PDF Preview",
                    Description = "Preview PDF files directly in Quick View panel",
                    Version = "1.0.5",
                    Author = "Community",
                    Category = "Preview",
                    Icon = "📄",
                    Downloads = 9820,
                    Rating = 4.5
                },
                new MarketplacePluginItem
                {
                    Id = "marketplace.media-columns",
                    Name = "Media Columns",
                    Description = "Add columns for audio/video metadata (duration, bitrate, codec)",
                    Version = "1.1.0",
                    Author = "Community",
                    Category = "Columns",
                    Icon = "🎬",
                    Downloads = 5630,
                    Rating = 4.3
                },
                new MarketplacePluginItem
                {
                    Id = "marketplace.git-tools",
                    Name = "Git Tools",
                    Description = "Extended Git integration with branch switching and commit commands",
                    Version = "1.0.0",
                    Author = "XCommander Team",
                    Category = "Commands",
                    Icon = "📂",
                    Downloads = 12100,
                    Rating = 4.7
                }
            };
            
            foreach (var plugin in availablePlugins)
            {
                // Only add if not already in the list
                if (!AllPlugins.Any(p => p.Id == plugin.Id))
                {
                    AllPlugins.Add(plugin);
                }
            }
            
            AvailableCount = AllPlugins.Count(p => !p.IsInstalled);
            ApplyFilter();
            StatusText = $"Found {AvailableCount} available plugins.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to fetch plugins: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsLoading = true;
        StatusText = "Checking for updates...";
        
        try
        {
            await Task.Delay(500);
            
            var updatesFound = 0;
            foreach (var plugin in AllPlugins.Where(p => p.IsInstalled))
            {
                // Simulate update check
                if (Random.Shared.NextDouble() > 0.7)
                {
                    plugin.HasUpdate = true;
                    updatesFound++;
                }
            }
            
            StatusText = updatesFound > 0 
                ? $"Found {updatesFound} plugin update(s) available." 
                : "All plugins are up to date.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to check for updates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private void OpenPluginsFolder()
    {
        try
        {
            var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            
            if (!Directory.Exists(pluginsDir))
            {
                Directory.CreateDirectory(pluginsDir);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pluginsDir,
                UseShellExecute = true
            };
            
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening plugins folder: {ex.Message}";
        }
    }
}

