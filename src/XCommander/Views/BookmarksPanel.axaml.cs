using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class BookmarksPanel : UserControl
{
    public BookmarksPanel()
    {
        InitializeComponent();
    }

    private void OnBookmarkDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is BookmarksViewModel vm && vm.SelectedBookmark != null)
        {
            vm.NavigateToBookmarkCommand.Execute(vm.SelectedBookmark);
        }
    }

    private void OnRecentDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is string path && DataContext is BookmarksViewModel vm)
        {
            vm.NavigateToRecentCommand.Execute(path);
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BookmarksViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Folder to Bookmark",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            vm.NewBookmarkPath = folders[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(vm.NewBookmarkName))
            {
                vm.NewBookmarkName = System.IO.Path.GetFileName(vm.NewBookmarkPath);
            }
        }
    }
}
