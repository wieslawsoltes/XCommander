using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class TcConfigImportDialog : Window
{
    public TcConfigImportDialog()
    {
        InitializeComponent();
    }
    
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = StorageProvider;
        
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select wincmd.ini file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("INI Files") { Patterns = new[] { "*.ini" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });
        
        if (files.Count > 0 && DataContext is TcConfigImportViewModel vm)
        {
            vm.WincmdIniPath = files[0].Path.LocalPath;
        }
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
