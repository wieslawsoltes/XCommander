using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
