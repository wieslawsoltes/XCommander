using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views
{
    public partial class DirectorySyncDialog : UserControl
    {
        public DirectorySyncDialog()
        {
            InitializeComponent();
        }

        private async void BrowseLeft_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is DirectorySyncViewModel vm)
            {
                var folder = await BrowseFolderAsync("Select Left Directory", vm.LeftPath);
                if (folder != null)
                {
                    vm.LeftPath = folder;
                }
            }
        }

        private async void BrowseRight_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is DirectorySyncViewModel vm)
            {
                var folder = await BrowseFolderAsync("Select Right Directory", vm.RightPath);
                if (folder != null)
                {
                    vm.RightPath = folder;
                }
            }
        }

        private async Task<string?> BrowseFolderAsync(string title, string? suggestedPath)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var startLocation = !string.IsNullOrEmpty(suggestedPath) && System.IO.Directory.Exists(suggestedPath)
                    ? await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedPath)
                    : null;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    SuggestedStartLocation = startLocation,
                    AllowMultiple = false
                });

                return folders.Count > 0 ? folders[0].Path.LocalPath : null;
            }
            return null;
        }

        private void SelectAll_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is DirectorySyncViewModel vm && sender is CheckBox checkBox)
            {
                if (checkBox.IsChecked == true)
                {
                    vm.SelectAllCommand.Execute(null);
                }
                else
                {
                    vm.SelectNoneCommand.Execute(null);
                }
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                window.Close();
            }
        }
    }
}

namespace XCommander.ViewModels
{
    public class EnumToIndexConverter : IValueConverter
    {
        public static readonly EnumToIndexConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Enum e)
            {
                return System.Convert.ToInt32(e);
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int index && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, index);
            }
            return Enum.ToObject(targetType, 0);
        }
    }

    public class SyncActionToColorConverter : IValueConverter
    {
        public static readonly SyncActionToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SyncAction action)
            {
                return action switch
                {
                    SyncAction.CopyLeft or SyncAction.CopyRight => new SolidColorBrush(Color.Parse("#4CAF50")),
                    SyncAction.UpdateLeft or SyncAction.UpdateRight => new SolidColorBrush(Color.Parse("#2196F3")),
                    SyncAction.DeleteLeft or SyncAction.DeleteRight => new SolidColorBrush(Color.Parse("#FF9800")),
                    _ => new SolidColorBrush(Colors.Transparent)
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
