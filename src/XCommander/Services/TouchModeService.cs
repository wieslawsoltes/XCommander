namespace XCommander.Services;

/// <summary>
/// Settings for touch mode - provides larger touch targets and gesture support.
/// </summary>
public class TouchModeSettings
{
    /// <summary>
    /// Whether touch mode is enabled (larger touch targets, gestures).
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// Item height in touch mode (default: 48dp for touch-friendly size).
    /// </summary>
    public double ItemHeight { get; set; } = 48;
    
    /// <summary>
    /// Minimum swipe distance to trigger navigation (in pixels).
    /// </summary>
    public double SwipeThreshold { get; set; } = 50;
    
    /// <summary>
    /// Long press duration to trigger context menu (in milliseconds).
    /// </summary>
    public int LongPressDuration { get; set; } = 500;
    
    /// <summary>
    /// Touch target padding around buttons and controls.
    /// </summary>
    public double TouchPadding { get; set; } = 8;
    
    /// <summary>
    /// Whether swipe gestures are enabled for navigation.
    /// </summary>
    public bool EnableSwipeNavigation { get; set; } = true;
    
    /// <summary>
    /// Whether pinch-to-zoom is enabled for thumbnails.
    /// </summary>
    public bool EnablePinchZoom { get; set; } = true;
}

/// <summary>
/// Service for managing touch mode and gesture handling.
/// </summary>
public interface ITouchModeService
{
    /// <summary>
    /// Current touch mode settings.
    /// </summary>
    TouchModeSettings Settings { get; }
    
    /// <summary>
    /// Whether touch mode is currently active.
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// Enable touch mode.
    /// </summary>
    void Enable();
    
    /// <summary>
    /// Disable touch mode.
    /// </summary>
    void Disable();
    
    /// <summary>
    /// Toggle touch mode.
    /// </summary>
    void Toggle();
    
    /// <summary>
    /// Auto-detect if device has touch screen and enable accordingly.
    /// </summary>
    void AutoDetect();
    
    /// <summary>
    /// Event raised when touch mode is changed.
    /// </summary>
    event EventHandler<bool>? TouchModeChanged;
}

/// <summary>
/// Implementation of touch mode service.
/// </summary>
public class TouchModeService : ITouchModeService
{
    private bool _isActive;
    
    public TouchModeSettings Settings { get; } = new();
    
    public bool IsActive => _isActive;
    
    public event EventHandler<bool>? TouchModeChanged;
    
    public void Enable()
    {
        if (!_isActive)
        {
            _isActive = true;
            Settings.IsEnabled = true;
            TouchModeChanged?.Invoke(this, true);
        }
    }
    
    public void Disable()
    {
        if (_isActive)
        {
            _isActive = false;
            Settings.IsEnabled = false;
            TouchModeChanged?.Invoke(this, false);
        }
    }
    
    public void Toggle()
    {
        if (_isActive)
            Disable();
        else
            Enable();
    }
    
    public void AutoDetect()
    {
        // On touch-enabled devices, enable touch mode
        // This is a simplified detection - in production, check for touch screen
        // For now, we can check platform hints
        
        // Default to disabled on desktop, let user enable manually
        _isActive = false;
        Settings.IsEnabled = false;
    }
}
