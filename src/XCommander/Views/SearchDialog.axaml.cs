using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class SearchDialog : Window
{
    public SearchResultItem? SelectedResult => 
        (DataContext as SearchViewModel)?.SelectedResult;
    
    public string? NavigateToPath { get; private set; }
    
    public SearchDialog()
    {
        InitializeComponent();
    }
    
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Search Folder",
            AllowMultiple = false
        });
        
        if (folders.Count > 0)
        {
            var vm = DataContext as SearchViewModel;
            if (vm != null)
            {
                vm.SearchPath = folders[0].Path.LocalPath;
            }
        }
    }
    
    private void OnClearDatesClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as SearchViewModel;
        if (vm != null)
        {
            vm.DateFrom = null;
            vm.DateTo = null;
        }
    }
    
    private void OnResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        OnGoToFileClick(sender, e);
    }
    
    private void OnGoToFileClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as SearchViewModel;
        if (vm?.SelectedResult != null)
        {
            NavigateToPath = vm.SelectedResult.IsDirectory 
                ? vm.SelectedResult.FullPath 
                : vm.SelectedResult.Directory;
            Close(true);
        }
    }
    
    private void OnFeedToPanelClick(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as SearchViewModel;
        vm?.FeedToPanelCommand.Execute(null);
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
