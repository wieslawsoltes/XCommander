using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using XCommander.Services;

namespace XCommander.Controls;

public partial class NotificationOverlay : UserControl
{
    private INotificationService? _notificationService;
    private readonly ObservableCollection<Notification> _notifications = new();
    
    public NotificationOverlay()
    {
        InitializeComponent();
        
        NotificationList.ItemsSource = _notifications;
    }
    
    /// <summary>
    /// Binds the overlay to a notification service.
    /// </summary>
    public void BindToService(INotificationService notificationService)
    {
        // Unbind from previous service
        if (_notificationService != null)
        {
            _notificationService.NotificationAdded -= OnNotificationAdded;
            _notificationService.NotificationRemoved -= OnNotificationRemoved;
        }
        
        _notificationService = notificationService;
        
        if (_notificationService != null)
        {
            _notificationService.NotificationAdded += OnNotificationAdded;
            _notificationService.NotificationRemoved += OnNotificationRemoved;
        }
    }
    
    private void OnNotificationAdded(object? sender, NotificationEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Check if notification already exists (for updates)
            var existing = _notifications.FirstOrDefault(n => n.Id == e.Notification.Id);
            if (existing != null)
            {
                var index = _notifications.IndexOf(existing);
                _notifications[index] = e.Notification;
            }
            else
            {
                _notifications.Add(e.Notification);
            }
        });
    }
    
    private void OnNotificationRemoved(object? sender, NotificationEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = _notifications.FirstOrDefault(n => n.Id == e.Notification.Id);
            if (existing != null)
            {
                _notifications.Remove(existing);
            }
        });
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Notification notification)
        {
            _notifications.Remove(notification);
            
            // Also notify the service
            if (_notificationService is NotificationService ns)
            {
                ns.RemoveNotification(notification);
            }
        }
    }
    
    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Notification notification)
        {
            notification.RetryAction?.Invoke();
            
            // Remove the notification after retry
            _notifications.Remove(notification);
            if (_notificationService is NotificationService ns)
            {
                ns.RemoveNotification(notification);
            }
        }
    }
}

/// <summary>
/// Converter for notification type to icon.
/// </summary>
public class NotificationIconConverter : IValueConverter
{
    public static readonly NotificationIconConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => "ℹ️",
                NotificationType.Success => "✅",
                NotificationType.Warning => "⚠️",
                NotificationType.Error => "❌",
                NotificationType.Progress => "⏳",
                _ => "ℹ️"
            };
        }
        return "ℹ️";
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
