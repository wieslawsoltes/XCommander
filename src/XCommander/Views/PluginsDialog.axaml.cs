using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XCommander.Views;

public partial class PluginsDialog : UserControl
{
    public PluginsDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (this.VisualRoot is Window window)
        {
            window.Close();
        }
    }
}
