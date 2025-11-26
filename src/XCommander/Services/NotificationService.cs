using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace XCommander.Services;

/// <summary>
/// Implementation of INotificationService for displaying toast notifications.
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (Notification notification, Timer? timer)> _activeNotifications = new();
    private bool _disposed;
    
    public event EventHandler<NotificationEventArgs>? NotificationAdded;
    public event EventHandler<NotificationEventArgs>? NotificationRemoved;
    
    public void ShowInfo(string message, string? title = null, int durationMs = 3000)
    {
        var notification = new Notification
        {
            Message = message,
            Title = title ?? "Info",
            Type = NotificationType.Info,
            DurationMs = durationMs
        };
        
        AddNotification(notification);
    }
    
    public void ShowSuccess(string message, string? title = null, int durationMs = 3000)
    {
        var notification = new Notification
        {
            Message = message,
            Title = title ?? "Success",
            Type = NotificationType.Success,
            DurationMs = durationMs
        };
        
        AddNotification(notification);
    }
    
    public void ShowWarning(string message, string? title = null, int durationMs = 5000)
    {
        var notification = new Notification
        {
            Message = message,
            Title = title ?? "Warning",
            Type = NotificationType.Warning,
            DurationMs = durationMs
        };
        
        AddNotification(notification);
    }
    
    public void ShowError(string message, string? title = null, Action? retryAction = null, int durationMs = 8000)
    {
        var notification = new Notification
        {
            Message = message,
            Title = title ?? "Error",
            Type = NotificationType.Error,
            DurationMs = durationMs,
            RetryAction = retryAction
        };
        
        AddNotification(notification);
    }
    
    public IProgressNotification ShowProgress(string message, string? title = null)
    {
        var notification = new Notification
        {
            Message = message,
            Title = title ?? "Progress",
            Type = NotificationType.Progress,
            DurationMs = 0, // Progress notifications don't auto-dismiss
            IsProgress = true,
            Progress = 0
        };
        
        AddNotification(notification, autoRemove: false);
        
        return new ProgressNotificationHandle(notification, this);
    }
    
    public void ClearAll()
    {
        foreach (var (id, (notification, timer)) in _activeNotifications.ToArray())
        {
            RemoveNotification(notification);
        }
    }
    
    private void AddNotification(Notification notification, bool autoRemove = true)
    {
        Timer? timer = null;
        
        if (autoRemove && notification.DurationMs > 0)
        {
            timer = new Timer(notification.DurationMs);
            timer.Elapsed += (s, e) =>
            {
                timer.Stop();
                RemoveNotification(notification);
            };
            timer.AutoReset = false;
            timer.Start();
        }
        
        _activeNotifications[notification.Id] = (notification, timer);
        NotificationAdded?.Invoke(this, new NotificationEventArgs { Notification = notification });
    }
    
    internal void RemoveNotification(Notification notification)
    {
        if (_activeNotifications.TryRemove(notification.Id, out var entry))
        {
            entry.timer?.Stop();
            entry.timer?.Dispose();
            NotificationRemoved?.Invoke(this, new NotificationEventArgs { Notification = notification });
        }
    }
    
    internal void UpdateNotification(Notification notification)
    {
        if (_activeNotifications.ContainsKey(notification.Id))
        {
            // Trigger a refresh by re-raising the event
            NotificationAdded?.Invoke(this, new NotificationEventArgs { Notification = notification });
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        foreach (var (_, (_, timer)) in _activeNotifications)
        {
            timer?.Stop();
            timer?.Dispose();
        }
        _activeNotifications.Clear();
    }
}

/// <summary>
/// Handle for a progress notification.
/// </summary>
internal class ProgressNotificationHandle : IProgressNotification
{
    private readonly Notification _notification;
    private readonly NotificationService _service;
    private bool _disposed;
    
    public ProgressNotificationHandle(Notification notification, NotificationService service)
    {
        _notification = notification;
        _service = service;
    }
    
    public void UpdateProgress(int percentage, string? message = null)
    {
        if (_disposed) return;
        
        _notification.Progress = Math.Clamp(percentage, 0, 100);
        _service.UpdateNotification(_notification);
    }
    
    public void Complete(string? message = null)
    {
        if (_disposed) return;
        
        _notification.Progress = 100;
        _service.UpdateNotification(_notification);
        
        // Auto-remove after a short delay
        Task.Delay(1500).ContinueWith(_ => _service.RemoveNotification(_notification));
    }
    
    public void Fail(string? message = null)
    {
        if (_disposed) return;
        
        _service.RemoveNotification(_notification);
        
        // Show error notification instead
        _service.ShowError(message ?? "Operation failed");
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _service.RemoveNotification(_notification);
    }
}
