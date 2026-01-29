using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using XCommander.Plugins;
using XCommander.ViewModels;
using XCommander.Views;
using XCommander.Views.Dialogs;

namespace XCommander.Services;

/// <summary>
/// Default implementation for opening settings-related dialogs.
/// </summary>
public sealed class SettingsNavigationService : ISettingsNavigationService
{
    private readonly IFileColoringService? _fileColoringService;
    private readonly ITcConfigImportService? _tcConfigImportService;

    public SettingsNavigationService(
        IFileColoringService? fileColoringService = null,
        ITcConfigImportService? tcConfigImportService = null)
    {
        _fileColoringService = fileColoringService;
        _tcConfigImportService = tcConfigImportService;
    }

    public async Task OpenKeyboardShortcutsAsync()
    {
        var manager = new KeyboardShortcutManager();
        var dialog = new KeyboardShortcutsDialog
        {
            DataContext = manager
        };
        await ShowDialogAsync(dialog);
    }

    public async Task OpenToolbarConfigurationAsync()
    {
        var vm = new ToolbarConfigurationViewModel();
        var dialog = new ToolbarConfigurationDialog
        {
            DataContext = vm
        };
        var window = CreateHostWindow("Configure Toolbar", dialog, 700, 500);
        vm.RequestClose += (_, _) => window.Close();
        await ShowDialogAsync(window);
    }

    public async Task OpenCustomColumnsAsync()
    {
        var vm = new CustomColumnsViewModel();
        var dialog = new CustomColumnsDialog
        {
            DataContext = vm
        };
        var window = CreateHostWindow("Custom Columns", dialog, 650, 500);
        await ShowDialogAsync(window);
    }

    public async Task OpenFileColoringAsync()
    {
        var vm = new FileColoringSettingsViewModel(_fileColoringService);
        var dialog = new FileColoringSettingsDialog
        {
            DataContext = vm
        };
        await ShowDialogAsync(dialog);
    }

    public async Task OpenFileAssociationsAsync()
    {
        var manager = new FileAssociationManager();
        var dialog = new FileAssociationsDialog
        {
            DataContext = manager
        };
        await ShowDialogAsync(dialog);
    }

    public async Task OpenPluginsAsync()
    {
        var pluginManager = GetMainViewModel()?.PluginManager ?? new PluginManager();
        var vm = new PluginsViewModel(pluginManager);
        var dialog = new PluginsDialog
        {
            DataContext = vm
        };
        var window = CreateHostWindow("Plugin Manager", dialog, 800, 550);
        await ShowDialogAsync(window);
    }

    public async Task OpenTcConfigImportAsync()
    {
        var importService = _tcConfigImportService ?? new TcConfigImportService();
        var vm = new TcConfigImportViewModel(importService);
        var dialog = new TcConfigImportDialog
        {
            DataContext = vm
        };
        await ShowDialogAsync(dialog);
    }

    private static Window CreateHostWindow(string title, UserControl content, double width, double height)
    {
        return new Window
        {
            Title = title,
            Content = content,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
    }

    private static async Task ShowDialogAsync(Window dialog)
    {
        var owner = GetOwnerWindow();
        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private static MainWindowViewModel? GetMainViewModel()
    {
        return GetOwnerWindow()?.DataContext as MainWindowViewModel;
    }
}
