using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace XCommander.Views.Dialogs;

public partial class ProgressDialog : Window
{
    private CancellationTokenSource? _cts;
    
    public bool IsCancelled { get; private set; }
    
    public ProgressDialog()
    {
        InitializeComponent();
    }
    
    public void SetTitle(string title)
    {
        TitleText.Text = title;
        Title = title;
    }
    
    public void UpdateProgress(double percentage, string currentItem)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = percentage;
            ProgressText.Text = $"{percentage:F0}%";
            CurrentItemText.Text = currentItem;
        });
    }
    
    public void SetCancellationTokenSource(CancellationTokenSource cts)
    {
        _cts = cts;
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsCancelled = true;
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        CancelButton.Content = "Cancelling...";
    }
    
    public void Complete()
    {
        Dispatcher.UIThread.Post(() => Close());
    }
}
