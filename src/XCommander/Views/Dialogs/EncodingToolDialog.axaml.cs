using Avalonia.Controls;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class EncodingToolDialog : UserControl
{
    public EncodingToolDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EncodingToolViewModel vm)
        {
            vm.FileSelectionRequested += OnFileSelectionRequested;
        }
    }

    private async void OnFileSelectionRequested(object? sender, FileSelectionEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        if (e.IsSaveDialog)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Decoded File",
                SuggestedFileName = "decoded_output"
            });
            
            e.Callback(file?.Path.LocalPath);
        }
        else
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select File to Encode",
                AllowMultiple = false
            });
            
            e.Callback(files.Count > 0 ? files[0].Path.LocalPath : null);
        }
    }
}
