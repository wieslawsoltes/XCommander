using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FileViewerDialog : Window
{
    private TextBox? _contentTextBox;
    
    public FileViewerDialog()
    {
        InitializeComponent();
        
        _contentTextBox = this.FindControl<TextBox>("ContentTextBox");
        
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is FileViewerViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(FileViewerViewModel.WordWrap) && _contentTextBox != null)
                {
                    _contentTextBox.TextWrapping = vm.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
                }
            };
            
            // Set initial value
            if (_contentTextBox != null)
            {
                _contentTextBox.TextWrapping = vm.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            }
        }
    }
    
    private void OnEncodingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is string encoding)
        {
            var vm = DataContext as FileViewerViewModel;
            vm?.SetEncodingByNameCommand.Execute(encoding);
        }
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
