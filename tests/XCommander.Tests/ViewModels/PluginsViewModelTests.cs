using XCommander.Plugins;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public sealed class PluginsViewModelTests : IDisposable
{
    private readonly PluginManager _pluginManager;
    private readonly PluginsViewModel _viewModel;
    private readonly string _tempDir;

    public PluginsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "xcmd-tests", Guid.NewGuid().ToString("N"));
        _pluginManager = new PluginManager(_tempDir);
        _viewModel = new PluginsViewModel(_pluginManager);
    }

    [Fact]
    public void LoadPlugins_WhenSearchEmpty_ShowsAllPlugins()
    {
        var plugins = new[]
        {
            CreateLoadedPlugin("alpha", "Alpha Plugin", "First plugin", "Acme", "1.0.0"),
            CreateLoadedPlugin("beta", "Beta Tooling", "Second plugin", "Contoso", "2.1.0")
        };

        _viewModel.LoadPlugins(plugins);

        Assert.Equal(2, _viewModel.Plugins.Count);
        Assert.Equal("2 plugin(s) loaded", _viewModel.StatusMessage);
    }

    [Fact]
    public void SearchText_FiltersByNameAndDescription()
    {
        var plugins = new[]
        {
            CreateLoadedPlugin("viewer", "Viewer Tools", "Opens files", "Acme", "1.0.0"),
            CreateLoadedPlugin("packer", "Archive Pack", "Compresses data", "Contoso", "2.1.0")
        };

        _viewModel.LoadPlugins(plugins);
        _viewModel.SearchText = "viewer";

        Assert.Single(_viewModel.Plugins);
        Assert.Equal("Viewer Tools", _viewModel.Plugins[0].Name);
        Assert.Equal("1 of 2 plugin(s) shown", _viewModel.StatusMessage);
    }

    [Fact]
    public void SearchText_MultipleTermsRequireAllTerms()
    {
        var plugins = new[]
        {
            CreateLoadedPlugin("sync", "Sync Tools", "File sync utility", "Acme", "1.0.0"),
            CreateLoadedPlugin("editor", "Editor Suite", "Text editor", "Acme", "3.0.0")
        };

        _viewModel.LoadPlugins(plugins);
        _viewModel.SearchText = "Acme sync";

        Assert.Single(_viewModel.Plugins);
        Assert.Equal("Sync Tools", _viewModel.Plugins[0].Name);
    }

    private static LoadedPlugin CreateLoadedPlugin(
        string id,
        string name,
        string description,
        string author,
        string version)
    {
        var plugin = new TestPlugin(id, name, description, author, Version.Parse(version));

        return new LoadedPlugin
        {
            Metadata = new PluginMetadata
            {
                Id = id,
                Name = name,
                Description = description,
                Version = version,
                Author = author
            },
            Instance = plugin,
            PluginDirectory = Path.Combine(Path.GetTempPath(), "xcmd-tests", "plugins"),
            IsEnabled = true,
            IsInitialized = true
        };
    }

    private sealed class TestPlugin : IPlugin
    {
        public TestPlugin(string id, string name, string description, string author, Version version)
        {
            Id = id;
            Name = name;
            Description = description;
            Author = author;
            Version = version;
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public Version Version { get; }

        public string Author { get; }

        public Task InitializeAsync(IPluginContext context)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        _pluginManager.Dispose();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
