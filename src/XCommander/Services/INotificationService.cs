namespace XCommander.Services;

/// <summary>
/// Service for displaying notifications/toasts to the user.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Show an informational notification.
    /// </summary>
    void ShowInfo(string message, string? title = null, int durationMs = 3000);
    
    /// <summary>
    /// Show a success notification.
    /// </summary>
    void ShowSuccess(string message, string? title = null, int durationMs = 3000);
    
    /// <summary>
    /// Show a warning notification.
    /// </summary>
    void ShowWarning(string message, string? title = null, int durationMs = 5000);
    
    /// <summary>
    /// Show an error notification with optional retry action.
    /// </summary>
    void ShowError(string message, string? title = null, Action? retryAction = null, int durationMs = 8000);
    
    /// <summary>
    /// Show a progress notification that can be updated.
    /// </summary>
    IProgressNotification ShowProgress(string message, string? title = null);
    
    /// <summary>
    /// Clear all current notifications.
    /// </summary>
    void ClearAll();
    
    /// <summary>
    /// Event raised when a notification is added.
    /// </summary>
    event EventHandler<NotificationEventArgs>? NotificationAdded;
    
    /// <summary>
    /// Event raised when a notification is removed.
    /// </summary>
    event EventHandler<NotificationEventArgs>? NotificationRemoved;
}

/// <summary>
/// Represents a progress notification that can be updated.
/// </summary>
public interface IProgressNotification : IDisposable
{
    /// <summary>
    /// Update the progress value (0-100).
    /// </summary>
    void UpdateProgress(int percentage, string? message = null);
    
    /// <summary>
    /// Mark the progress as complete.
    /// </summary>
    void Complete(string? message = null);
    
    /// <summary>
    /// Mark the progress as failed.
    /// </summary>
    void Fail(string? message = null);
}

/// <summary>
/// Notification event arguments.
/// </summary>
public class NotificationEventArgs : EventArgs
{
    public required Notification Notification { get; init; }
}

/// <summary>
/// Represents a notification to display.
/// </summary>
public record Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Message { get; init; }
    public string? Title { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Info;
    public int DurationMs { get; init; } = 3000;
    public Action? RetryAction { get; init; }
    public int? Progress { get; set; }
    public bool IsProgress { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Types of notifications.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    Progress
}
