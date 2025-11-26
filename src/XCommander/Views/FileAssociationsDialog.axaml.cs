using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FileAssociationsDialog : Window
{
    public FileAssociationsDialog()
    {
        InitializeComponent();
    }
    
    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileAssociationManager manager)
        {
            manager.SaveAssociations();
        }
        Close(true);
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
