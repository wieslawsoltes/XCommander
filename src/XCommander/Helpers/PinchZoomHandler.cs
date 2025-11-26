using Avalonia;
using Avalonia.Input;

namespace XCommander.Helpers;

/// <summary>
/// Helper for handling pinch-to-zoom gestures.
/// </summary>
public class PinchZoomHandler
{
    private readonly Dictionary<int, Point> _activePointers = new();
    private double _initialDistance;
    private double _currentScale = 1.0;
    
    /// <summary>
    /// Gets or sets the minimum scale allowed.
    /// </summary>
    public double MinScale { get; set; } = 0.5;
    
    /// <summary>
    /// Gets or sets the maximum scale allowed.
    /// </summary>
    public double MaxScale { get; set; } = 3.0;
    
    /// <summary>
    /// Gets the current scale factor.
    /// </summary>
    public double CurrentScale => _currentScale;
    
    /// <summary>
    /// Event raised when the scale changes.
    /// </summary>
    public event EventHandler<double>? ScaleChanged;
    
    /// <summary>
    /// Handles pointer pressed events.
    /// </summary>
    public void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;
            
        _activePointers[(int)e.Pointer.Id] = e.GetPosition(e.Source as Visual);
        
        if (_activePointers.Count == 2)
        {
            // Start pinch gesture
            _initialDistance = GetPointerDistance();
        }
    }
    
    /// <summary>
    /// Handles pointer moved events.
    /// </summary>
    public void OnPointerMoved(PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;
            
        if (!_activePointers.ContainsKey((int)e.Pointer.Id))
            return;
            
        _activePointers[(int)e.Pointer.Id] = e.GetPosition(e.Source as Visual);
        
        if (_activePointers.Count == 2 && _initialDistance > 0)
        {
            var currentDistance = GetPointerDistance();
            var scaleFactor = currentDistance / _initialDistance;
            
            var newScale = Math.Clamp(_currentScale * scaleFactor, MinScale, MaxScale);
            
            if (Math.Abs(newScale - _currentScale) > 0.01)
            {
                _currentScale = newScale;
                ScaleChanged?.Invoke(this, _currentScale);
            }
            
            _initialDistance = currentDistance;
        }
    }
    
    /// <summary>
    /// Handles pointer released events.
    /// </summary>
    public void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;
            
        _activePointers.Remove((int)e.Pointer.Id);
        
        if (_activePointers.Count < 2)
        {
            _initialDistance = 0;
        }
    }
    
    /// <summary>
    /// Resets the scale to the default value.
    /// </summary>
    public void Reset()
    {
        _currentScale = 1.0;
        _activePointers.Clear();
        _initialDistance = 0;
        ScaleChanged?.Invoke(this, _currentScale);
    }
    
    private double GetPointerDistance()
    {
        if (_activePointers.Count < 2)
            return 0;
            
        var points = _activePointers.Values.ToArray();
        var dx = points[1].X - points[0].X;
        var dy = points[1].Y - points[0].Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
