using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FileCombineDialog : UserControl
{
    public FileCombineDialog()
    {
        InitializeComponent();
    }

    private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileCombineViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select First Part File (.001)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Split Files") { Patterns = new[] { "*.001" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.Initialize(files[0].Path.LocalPath, 
                        System.IO.Path.GetDirectoryName(vm.DestinationFile) ?? System.IO.Path.GetDirectoryName(files[0].Path.LocalPath) ?? "");
                }
            }
        }
    }

    private async void BrowseDestination_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileCombineViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Combined File As",
                    SuggestedFileName = System.IO.Path.GetFileName(vm.DestinationFile)
                });

                if (file != null)
                {
                    vm.DestinationFile = file.Path.LocalPath;
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
