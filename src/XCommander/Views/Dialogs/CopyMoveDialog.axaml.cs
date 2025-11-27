using Avalonia.Controls;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class CopyMoveDialog : Window
{
    public CopyMoveDialog()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CopyMoveDialogViewModel vm)
        {
            // Wire up browse command
            vm.BrowseRequested += async (_, _) => await BrowseForDestinationAsync();
        }
    }
    
    private async Task BrowseForDestinationAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Destination Folder",
            AllowMultiple = false
        });
        
        if (folders.Count > 0 && DataContext is CopyMoveDialogViewModel vm)
        {
            vm.DestinationPath = folders[0].Path.LocalPath;
        }
    }
}
