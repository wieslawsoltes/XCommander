using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class CommandPalette : UserControl
{
    public CommandPalette()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Focus the search box when the palette is shown
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox?.Focus();
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Up:
                vm.MoveSelectionUpCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Down:
                vm.MoveSelectionDownCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Escape:
                vm.CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnCommandDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.ExecuteSelectedCommand.Execute(null);
        }
    }

    public void FocusSearchBox()
    {
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox?.Focus();
        searchBox?.SelectAll();
    }
}
