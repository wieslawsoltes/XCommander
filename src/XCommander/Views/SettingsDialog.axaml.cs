using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class SettingsDialog : UserControl
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    private async void BrowseLeftPath_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            var path = await BrowseFolderAsync("Select Default Left Path");
            if (path != null)
            {
                vm.DefaultLeftPath = path;
            }
        }
    }

    private async void BrowseRightPath_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            var path = await BrowseFolderAsync("Select Default Right Path");
            if (path != null)
            {
                vm.DefaultRightPath = path;
            }
        }
    }

    private async void BrowseEditor_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            var path = await BrowseFileAsync("Select External Editor");
            if (path != null)
            {
                vm.ExternalEditor = path;
            }
        }
    }

    private async void BrowseViewer_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            var path = await BrowseFileAsync("Select External Viewer");
            if (path != null)
            {
                vm.ExternalViewer = path;
            }
        }
    }

    private async Task<string?> BrowseFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                return folders[0].Path.LocalPath;
            }
        }
        return null;
    }

    private async Task<string?> BrowseFileAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                return files[0].Path.LocalPath;
            }
        }
        return null;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close(true);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close(false);
        }
    }
}
