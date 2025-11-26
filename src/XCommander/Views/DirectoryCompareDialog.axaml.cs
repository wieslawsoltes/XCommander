using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class DirectoryCompareDialog : UserControl
{
    public DirectoryCompareDialog()
    {
        InitializeComponent();
    }

    private async void BrowseLeft_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DirectoryCompareViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Left Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.LeftPath = folders[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowseRight_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DirectoryCompareViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Right Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.RightPath = folders[0].Path.LocalPath;
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
