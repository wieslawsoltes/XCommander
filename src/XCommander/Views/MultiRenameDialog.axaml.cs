using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class MultiRenameDialog : Window
{
    public MultiRenameDialog()
    {
        InitializeComponent();
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private async void OnShowHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MultiRenameViewModel vm) return;
        
        vm.LoadHistory();
        
        var dialog = new RenameHistoryDialog
        {
            DataContext = vm
        };
        
        await dialog.ShowDialog(this);
    }
}
