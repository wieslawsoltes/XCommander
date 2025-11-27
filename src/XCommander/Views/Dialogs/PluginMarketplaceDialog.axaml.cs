using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XCommander.Views.Dialogs;

public partial class PluginMarketplaceDialog : Window
{
    public PluginMarketplaceDialog()
    {
        InitializeComponent();
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
