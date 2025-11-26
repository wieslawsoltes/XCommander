using Avalonia.Controls;
using Avalonia.Interactivity;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class RenameHistoryDialog : Window
{
    public RenameHistoryDialog()
    {
        InitializeComponent();
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private async void OnViewBatchDetails(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RenameBatch batch)
        {
            var details = new Window
            {
                Title = $"Batch Details - {batch.Timestamp:yyyy-MM-dd HH:mm:ss}",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Grid
                {
                    Margin = new Avalonia.Thickness(10),
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Operations in this batch ({batch.Operations.Count} files):",
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            Margin = new Avalonia.Thickness(0, 0, 0, 10),
                            [Grid.RowProperty] = 0
                        },
                        new DataGrid
                        {
                            ItemsSource = batch.Operations,
                            AutoGenerateColumns = false,
                            IsReadOnly = true,
                            Columns =
                            {
                                new DataGridTextColumn
                                {
                                    Header = "Original",
                                    Binding = new Avalonia.Data.Binding("OldPath"),
                                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                                },
                                new DataGridTextColumn
                                {
                                    Header = "Renamed To",
                                    Binding = new Avalonia.Data.Binding("NewPath"),
                                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                                }
                            },
                            [Grid.RowProperty] = 1
                        },
                        new Button
                        {
                            Content = "Close",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Padding = new Avalonia.Thickness(15, 8),
                            Margin = new Avalonia.Thickness(0, 10, 0, 0),
                            [Grid.RowProperty] = 2
                        }
                    }
                }
            };
            
            // Add close handler
            if (details.Content is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Button closeBtn && closeBtn.Content?.ToString() == "Close")
                    {
                        closeBtn.Click += (_, _) => details.Close();
                    }
                }
            }
            
            await details.ShowDialog(this);
        }
    }
}
