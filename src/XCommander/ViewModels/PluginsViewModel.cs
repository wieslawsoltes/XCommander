using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Plugins;

namespace XCommander.ViewModels;

public partial class PluginItemViewModel : ViewModelBase
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
    private bool _isEnabled;
    
    [ObservableProperty]
    private bool _isInitialized;
    
    [ObservableProperty]
    private string _status = string.Empty;
    
    [ObservableProperty]
    private string _error = string.Empty;
    
    [ObservableProperty]
    private string _pluginType = string.Empty;
    
    public LoadedPlugin? LoadedPlugin { get; set; }
}

public partial class PluginsViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    
    [ObservableProperty]
    private ObservableCollection<PluginItemViewModel> _plugins = new();
    
    [ObservableProperty]
    private PluginItemViewModel? _selectedPlugin;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    [ObservableProperty]
    private bool _isLoading;

    public PluginsViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        Plugins.Clear();
        
        foreach (var loadedPlugin in _pluginManager.LoadedPlugins)
        {
            var pluginType = GetPluginTypeDescription(loadedPlugin.Instance);
            var status = loadedPlugin.IsInitialized 
                ? (loadedPlugin.IsEnabled ? "Active" : "Disabled")
                : (loadedPlugin.LoadError != null ? "Error" : "Not Initialized");
            
            Plugins.Add(new PluginItemViewModel
            {
                Id = loadedPlugin.Metadata.Id,
                Name = loadedPlugin.Metadata.Name,
                Description = loadedPlugin.Metadata.Description ?? string.Empty,
                Version = loadedPlugin.Metadata.Version ?? "1.0.0",
                Author = loadedPlugin.Metadata.Author ?? "Unknown",
                IsEnabled = loadedPlugin.IsEnabled,
                IsInitialized = loadedPlugin.IsInitialized,
                Status = status,
                Error = loadedPlugin.LoadError?.Message ?? string.Empty,
                PluginType = pluginType,
                LoadedPlugin = loadedPlugin
            });
        }
        
        StatusMessage = $"{Plugins.Count} plugin(s) loaded";
    }

    private static string GetPluginTypeDescription(IPlugin plugin)
    {
        var types = new List<string>();
        
        if (plugin is IFileSystemPlugin) types.Add("File System");
        if (plugin is IViewerPlugin) types.Add("Viewer");
        if (plugin is IColumnPlugin) types.Add("Columns");
        if (plugin is IPackerPlugin) types.Add("Packer");
        if (plugin is ICommandPlugin) types.Add("Commands");
        
        return types.Count > 0 ? string.Join(", ", types) : "General";
    }

    [RelayCommand]
    public async Task EnablePluginAsync()
    {
        if (SelectedPlugin?.LoadedPlugin == null)
            return;

        IsLoading = true;
        StatusMessage = $"Enabling {SelectedPlugin.Name}...";

        try
        {
            await _pluginManager.EnablePluginAsync(SelectedPlugin.Id);
            SelectedPlugin.IsEnabled = true;
            SelectedPlugin.Status = "Active";
            StatusMessage = $"{SelectedPlugin.Name} enabled";
        }
        catch (Exception ex)
        {
            SelectedPlugin.Error = ex.Message;
            StatusMessage = $"Error enabling {SelectedPlugin.Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DisablePluginAsync()
    {
        if (SelectedPlugin?.LoadedPlugin == null)
            return;

        IsLoading = true;
        StatusMessage = $"Disabling {SelectedPlugin.Name}...";

        try
        {
            await _pluginManager.DisablePluginAsync(SelectedPlugin.Id);
            SelectedPlugin.IsEnabled = false;
            SelectedPlugin.Status = "Disabled";
            StatusMessage = $"{SelectedPlugin.Name} disabled";
        }
        catch (Exception ex)
        {
            SelectedPlugin.Error = ex.Message;
            StatusMessage = $"Error disabling {SelectedPlugin.Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void RefreshPlugins()
    {
        LoadPlugins();
    }

    [RelayCommand]
    public void OpenPluginsFolder()
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
            StatusMessage = $"Error opening plugins folder: {ex.Message}";
        }
    }
}
