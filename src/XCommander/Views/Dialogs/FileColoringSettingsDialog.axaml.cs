using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class FileColoringSettingsDialog : Window
{
    public FileColoringSettingsDialog()
    {
        InitializeComponent();
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void OnColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string color && DataContext is FileColoringSettingsViewModel vm)
        {
            vm.EditForeground = color;
        }
    }
}
