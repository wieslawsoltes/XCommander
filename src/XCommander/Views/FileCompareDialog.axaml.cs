using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FileCompareDialog : UserControl
{
    public FileCompareDialog()
    {
        InitializeComponent();
    }

    private async void BrowseLeft_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileCompareViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Left File",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    vm.LeftPath = files[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowseRight_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileCompareViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Right File",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    vm.RightPath = files[0].Path.LocalPath;
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

public class DiffTypeToColorConverter : IMultiValueConverter
{
    public static readonly DiffTypeToColorConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is DiffType type)
        {
            return type switch
            {
                DiffType.Added => new SolidColorBrush(Color.Parse("#C8E6C9")),      // Light green
                DiffType.Deleted => new SolidColorBrush(Color.Parse("#FFCDD2")),    // Light red
                DiffType.Modified => new SolidColorBrush(Color.Parse("#FFF9C4")),   // Light yellow
                DiffType.Empty => new SolidColorBrush(Color.Parse("#EEEEEE")),      // Light gray
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }
}
