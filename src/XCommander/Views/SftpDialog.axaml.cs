using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;
using XCommander.Views.Dialogs;

namespace XCommander.Views;

public partial class SftpDialog : UserControl
{
    public string? DownloadPath { get; private set; }
    
    public SftpDialog()
    {
        InitializeComponent();
    }

    private async void BrowseKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SftpViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Private Key File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Private Key Files") { Patterns = new[] { "*.pem", "*.ppk", "id_rsa", "id_ed25519" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.PrivateKeyPath = files[0].Path.LocalPath;
                }
            }
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SftpViewModel vm)
        {
            vm.OpenItemCommand.Execute(null);
        }
    }

    private async void Upload_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SftpViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select File to Upload",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    await vm.UploadFileAsync(files[0].Path.LocalPath);
                }
            }
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SftpViewModel vm && vm.SelectedItem != null && !vm.SelectedItem.IsDirectory)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Download Folder",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    DownloadPath = folders[0].Path.LocalPath;
                    await vm.DownloadSelectedAsync(DownloadPath);
                }
            }
        }
    }

    private async void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SftpViewModel vm)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                var dialog = new InputDialog("New Folder", "Enter folder name:", "New Folder");
                await dialog.ShowDialog(window);
                
                if (!string.IsNullOrWhiteSpace(dialog.Result))
                {
                    await vm.CreateDirectoryAsync(dialog.Result);
                }
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
