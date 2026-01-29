using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.Models;
using XCommander.ViewModels;

namespace XCommander.Views.Dialogs;

public partial class DriveSelectionDialog : Window
{
    public DriveItem? SelectedDrive { get; private set; }

    public DriveSelectionDialog()
    {
        InitializeComponent();
    }

    public DriveSelectionDialog(IEnumerable<DriveItem> drives) : this()
    {
        var viewModel = new DriveSelectionViewModel(drives);
        if (viewModel.Drives.Count > 0)
        {
            viewModel.SelectedDrive = viewModel.Drives[0];
        }
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DriveSelectionViewModel viewModel)
        {
            SelectedDrive = viewModel.SelectedDrive;
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        SelectedDrive = null;
        Close();
    }

    private void OnDriveDoubleTapped(object? sender, RoutedEventArgs e)
    {
        OnOkClick(sender, e);
    }
}
