using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FtpDialog : Window
{
    public string? DownloadPath { get; private set; }
    
    public FtpDialog()
    {
        InitializeComponent();
    }
    
    private async void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        var vm = DataContext as FtpViewModel;
        if (vm?.SelectedItem?.IsDirectory == true)
        {
            await vm.OpenItemCommand.ExecuteAsync(vm.SelectedItem);
        }
    }
    
    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as FtpViewModel;
        if (vm?.SelectedItem == null || vm.SelectedItem.IsDirectory)
            return;
            
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Download Destination",
            AllowMultiple = false
        });
        
        if (folders.Count > 0)
        {
            await vm.DownloadSelectedCommand.ExecuteAsync(folders[0].Path.LocalPath);
            DownloadPath = folders[0].Path.LocalPath;
        }
    }
    
    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as FtpViewModel;
        if (vm == null || !vm.IsConnected)
            return;
            
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File to Upload",
            AllowMultiple = false
        });
        
        if (files.Count > 0)
        {
            await vm.UploadFileCommand.ExecuteAsync(files[0].Path.LocalPath);
        }
    }
    
    private async void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as FtpViewModel;
        if (vm == null || !vm.IsConnected)
            return;
            
        var name = await ShowInputDialogAsync("New Folder", "Enter folder name:", "New Folder");
        if (!string.IsNullOrWhiteSpace(name))
        {
            await vm.CreateDirectoryCommand.ExecuteAsync(name);
        }
    }
    
    private async Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue)
    {
        var result = defaultValue;
        var confirmed = false;
        
        var textBox = new TextBox
        {
            Text = defaultValue,
            Width = 300,
            SelectionStart = 0,
            SelectionEnd = defaultValue.Length
        };
        
        var okButton = new Button { Content = "OK", IsDefault = true, MinWidth = 75 };
        var cancelButton = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75 };
        
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = prompt },
                    textBox,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { okButton, cancelButton }
                    }
                }
            }
        };
        
        okButton.Click += (_, _) =>
        {
            confirmed = true;
            result = textBox.Text;
            dialog.Close();
        };
        
        cancelButton.Click += (_, _) =>
        {
            dialog.Close();
        };
        
        await dialog.ShowDialog(this);
        return confirmed ? result : null;
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
