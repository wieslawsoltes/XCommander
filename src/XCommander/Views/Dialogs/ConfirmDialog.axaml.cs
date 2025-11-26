using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XCommander.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public bool Result { get; private set; }
    
    public ConfirmDialog()
    {
        InitializeComponent();
    }
    
    public ConfirmDialog(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        Title = title;
    }

    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
