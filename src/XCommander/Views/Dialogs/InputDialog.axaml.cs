using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace XCommander.Views.Dialogs;

public partial class InputDialog : Window
{
    public string? Result { get; private set; }
    
    public InputDialog()
    {
        InitializeComponent();
    }
    
    public InputDialog(string title, string prompt, string defaultValue = "") : this()
    {
        Title = title;
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;
        
        // Select all text and focus
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Result = InputTextBox.Text;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnOkClick(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelClick(sender, e);
        }
    }
}
