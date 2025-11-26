using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class CreateArchiveDialog : UserControl
{
    public CreateArchiveDialog()
    {
        InitializeComponent();
    }

    private async void BrowsePath_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateArchiveViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Archive As",
                    DefaultExtension = GetExtension(vm.SelectedType),
                    SuggestedFileName = System.IO.Path.GetFileName(vm.ArchivePath),
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } },
                        new FilePickerFileType("7-Zip Archive") { Patterns = new[] { "*.7z" } },
                        new FilePickerFileType("TAR Archive") { Patterns = new[] { "*.tar" } },
                        new FilePickerFileType("GZip Archive") { Patterns = new[] { "*.tar.gz", "*.tgz" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (file != null)
                {
                    vm.ArchivePath = file.Path.LocalPath;
                }
            }
        }
    }

    private string GetExtension(Services.ArchiveType type)
    {
        return type switch
        {
            Services.ArchiveType.Zip => "zip",
            Services.ArchiveType.SevenZip => "7z",
            Services.ArchiveType.Tar => "tar",
            Services.ArchiveType.GZip => "tar.gz",
            Services.ArchiveType.BZip2 => "tar.bz2",
            _ => "zip"
        };
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }
}
