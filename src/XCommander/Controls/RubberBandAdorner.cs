using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace XCommander.Controls;

/// <summary>
/// Adorner that displays a rubber band selection rectangle.
/// </summary>
public class RubberBandAdorner : Control
{
    private Point _startPoint;
    private Point _endPoint;
    private bool _isActive;
    
    /// <summary>
    /// Gets or sets whether the rubber band is currently active.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            IsVisible = value;
            InvalidateVisual();
        }
    }
    
    /// <summary>
    /// Gets or sets the start point of the selection rectangle.
    /// </summary>
    public Point StartPoint
    {
        get => _startPoint;
        set
        {
            _startPoint = value;
            InvalidateVisual();
        }
    }
    
    /// <summary>
    /// Gets or sets the end point of the selection rectangle.
    /// </summary>
    public Point EndPoint
    {
        get => _endPoint;
        set
        {
            _endPoint = value;
            InvalidateVisual();
        }
    }
    
    /// <summary>
    /// Gets the selection rectangle bounds.
    /// </summary>
    public Rect SelectionRect
    {
        get
        {
            var left = Math.Min(_startPoint.X, _endPoint.X);
            var top = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_endPoint.X - _startPoint.X);
            var height = Math.Abs(_endPoint.Y - _startPoint.Y);
            return new Rect(left, top, width, height);
        }
    }
    
    /// <summary>
    /// Gets or sets the fill color of the selection rectangle.
    /// </summary>
    public IBrush? Fill { get; set; } = new SolidColorBrush(Color.FromArgb(40, 0, 122, 204));
    
    /// <summary>
    /// Gets or sets the stroke color of the selection rectangle.
    /// </summary>
    public IBrush? Stroke { get; set; } = new SolidColorBrush(Color.FromRgb(0, 122, 204));
    
    /// <summary>
    /// Gets or sets the stroke thickness.
    /// </summary>
    public double StrokeThickness { get; set; } = 1;
    
    public RubberBandAdorner()
    {
        IsVisible = false;
        IsHitTestVisible = false;
    }
    
    public override void Render(DrawingContext context)
    {
        if (!_isActive)
            return;
            
        var rect = SelectionRect;
        if (rect.Width < 1 || rect.Height < 1)
            return;
        
        // Draw fill
        if (Fill != null)
        {
            context.FillRectangle(Fill, rect);
        }
        
        // Draw stroke
        if (Stroke != null)
        {
            var pen = new Pen(Stroke, StrokeThickness);
            context.DrawRectangle(pen, rect);
        }
    }
    
    /// <summary>
    /// Starts the rubber band selection.
    /// </summary>
    public void Start(Point point)
    {
        _startPoint = point;
        _endPoint = point;
        IsActive = true;
    }
    
    /// <summary>
    /// Updates the rubber band selection.
    /// </summary>
    public void Update(Point point)
    {
        if (!_isActive)
            return;
            
        _endPoint = point;
        InvalidateVisual();
    }
    
    /// <summary>
    /// Ends the rubber band selection.
    /// </summary>
    public void End()
    {
        IsActive = false;
    }
    
    /// <summary>
    /// Checks if a point is within the selection rectangle.
    /// </summary>
    public bool ContainsPoint(Point point)
    {
        return SelectionRect.Contains(point);
    }
    
    /// <summary>
    /// Checks if a rectangle intersects with the selection rectangle.
    /// </summary>
    public bool IntersectsRect(Rect rect)
    {
        return SelectionRect.Intersects(rect);
    }
}
