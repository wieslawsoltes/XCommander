using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views
{
    public partial class FileSplitDialog : UserControl
    {
        public FileSplitDialog()
        {
            InitializeComponent();
        }

        private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is FileSplitViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select File to Split",
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        vm.Initialize(files[0].Path.LocalPath, vm.DestinationFolder);
                    }
                }
            }
        }

        private async void BrowseDestination_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is FileSplitViewModel vm)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select Destination Folder",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        vm.DestinationFolder = folders[0].Path.LocalPath;
                    }
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
    using Avalonia.Data;
    using Avalonia.Data.Converters;
    using System;
    
    public class EnumToBoolConverter : IValueConverter
    {
        public static readonly EnumToBoolConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Enum e && parameter is Enum p)
            {
                return e.Equals(p);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is true && parameter is Enum p)
            {
                return p;
            }
            return BindingOperations.DoNothing;
        }
    }
}
