using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class ArchiveDialog : UserControl
{
    public ArchiveDialog()
    {
        InitializeComponent();
        
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ArchiveViewModel vm)
            {
                vm.AddFilesRequested += OnAddFilesRequested;
            }
        };
    }
    
    private async void OnAddFilesRequested(object? sender, EventArgs e)
    {
        if (DataContext is not ArchiveViewModel vm) return;
        
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add Files to Archive",
            AllowMultiple = true
        });
        
        if (files.Count > 0)
        {
            var filePaths = files.Select(f => f.Path.LocalPath).ToList();
            await vm.AddFilesToArchiveAsync(filePaths);
        }
    }

    private async void ExtractAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ArchiveViewModel vm)
        {
            var folder = await SelectDestinationFolderAsync("Extract All Files");
            if (folder != null)
            {
                await vm.ExtractAllAsync(folder);
            }
        }
    }

    private async void ExtractSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ArchiveViewModel vm)
        {
            var folder = await SelectDestinationFolderAsync("Extract Selected Files");
            if (folder != null)
            {
                await vm.ExtractSelectedAsync(folder);
            }
        }
    }

    private async Task<string?> SelectDestinationFolderAsync(string title)
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

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is ArchiveEntryViewModel entry)
        {
            if (DataContext is ArchiveViewModel vm)
            {
                vm.NavigateToCommand.Execute(entry);
            }
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }
}
