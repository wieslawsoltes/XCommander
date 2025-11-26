using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.Models;

namespace XCommander.Views;

public partial class CustomColumnsDialog : UserControl
{
    public List<ColumnConfiguration>? Result { get; private set; }
    public bool DialogResult { get; private set; }

    public CustomColumnsDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.CustomColumnsViewModel vm)
        {
            Result = vm.GetConfiguration();
            DialogResult = true;
        }
        
        if (this.VisualRoot is Window window)
        {
            window.Close(Result);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        if (this.VisualRoot is Window window)
        {
            window.Close(null);
        }
    }
}
