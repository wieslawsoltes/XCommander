using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace XCommander.Controls;

/// <summary>
/// Visual highlight shown on drop targets during drag operations.
/// </summary>
public class DropTargetHighlight : Control
{
    private Rect _targetRect;
    private DragDropEffects _currentEffect = DragDropEffects.None;
    private bool _isActive;
    private double _animationPhase;
    
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<DropTargetHighlight, bool>(nameof(IsActive), false);
    
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set
        {
            SetValue(IsActiveProperty, value);
            _isActive = value;
            IsVisible = value;
            InvalidateVisual();
        }
    }
    
    public DropTargetHighlight()
    {
        IsHitTestVisible = false;
        IsVisible = false;
    }
    
    public void ShowHighlight(Rect targetRect, DragDropEffects effect)
    {
        _targetRect = targetRect;
        _currentEffect = effect;
        IsActive = true;
        InvalidateVisual();
    }
    
    public void UpdateEffect(DragDropEffects effect)
    {
        if (_currentEffect != effect)
        {
            _currentEffect = effect;
            InvalidateVisual();
        }
    }
    
    public void HideHighlight()
    {
        IsActive = false;
    }
    
    public override void Render(DrawingContext context)
    {
        if (!_isActive || _targetRect.Width == 0)
            return;
        
        // Colors based on effect
        var (borderColor, fillColor, dashPattern) = _currentEffect switch
        {
            DragDropEffects.Copy => (
                Color.FromArgb(255, 60, 180, 60),
                Color.FromArgb(30, 60, 180, 60),
                new double[] { 4, 2 }
            ),
            DragDropEffects.Move => (
                Color.FromArgb(255, 70, 130, 180),
                Color.FromArgb(30, 70, 130, 180),
                new double[] { 6, 2 }
            ),
            DragDropEffects.Link => (
                Color.FromArgb(255, 128, 90, 160),
                Color.FromArgb(30, 128, 90, 160),
                new double[] { 2, 2 }
            ),
            DragDropEffects.None => (
                Color.FromArgb(255, 180, 60, 60),
                Color.FromArgb(20, 180, 60, 60),
                new double[] { 2, 4 }
            ),
            _ => (
                Color.FromArgb(255, 100, 100, 100),
                Color.FromArgb(20, 100, 100, 100),
                new double[] { 4, 2 }
            )
        };
        
        // Draw fill
        context.DrawRectangle(
            new SolidColorBrush(fillColor),
            null,
            _targetRect,
            4, 4);
        
        // Draw animated dashed border
        var pen = new Pen(
            new SolidColorBrush(borderColor),
            2,
            new DashStyle(dashPattern, _animationPhase));
        
        context.DrawRectangle(null, pen, _targetRect, 4, 4);
        
        // Draw corner indicators for valid drops
        if (_currentEffect != DragDropEffects.None)
        {
            var cornerSize = 12.0;
            var cornerBrush = new SolidColorBrush(borderColor);
            var cornerPen = new Pen(cornerBrush, 3);
            
            // Top-left corner
            context.DrawLine(cornerPen,
                new Point(_targetRect.Left, _targetRect.Top + cornerSize),
                new Point(_targetRect.Left, _targetRect.Top));
            context.DrawLine(cornerPen,
                new Point(_targetRect.Left, _targetRect.Top),
                new Point(_targetRect.Left + cornerSize, _targetRect.Top));
            
            // Top-right corner
            context.DrawLine(cornerPen,
                new Point(_targetRect.Right - cornerSize, _targetRect.Top),
                new Point(_targetRect.Right, _targetRect.Top));
            context.DrawLine(cornerPen,
                new Point(_targetRect.Right, _targetRect.Top),
                new Point(_targetRect.Right, _targetRect.Top + cornerSize));
            
            // Bottom-left corner
            context.DrawLine(cornerPen,
                new Point(_targetRect.Left, _targetRect.Bottom - cornerSize),
                new Point(_targetRect.Left, _targetRect.Bottom));
            context.DrawLine(cornerPen,
                new Point(_targetRect.Left, _targetRect.Bottom),
                new Point(_targetRect.Left + cornerSize, _targetRect.Bottom));
            
            // Bottom-right corner
            context.DrawLine(cornerPen,
                new Point(_targetRect.Right - cornerSize, _targetRect.Bottom),
                new Point(_targetRect.Right, _targetRect.Bottom));
            context.DrawLine(cornerPen,
                new Point(_targetRect.Right, _targetRect.Bottom),
                new Point(_targetRect.Right, _targetRect.Bottom - cornerSize));
        }
        
        // Draw operation icon in center for invalid targets
        if (_currentEffect == DragDropEffects.None)
        {
            var iconText = "✕";
            var typeface = new Typeface("Segoe UI Symbol, sans-serif");
            var formattedText = new FormattedText(
                iconText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                24,
                new SolidColorBrush(borderColor));
            
            var iconPos = new Point(
                _targetRect.Center.X - formattedText.Width / 2,
                _targetRect.Center.Y - formattedText.Height / 2);
            
            // Draw background circle
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                null,
                _targetRect.Center,
                20, 20);
            
            context.DrawText(formattedText, iconPos);
        }
    }
    
    /// <summary>
    /// Update animation phase for dashed border animation.
    /// </summary>
    public void UpdateAnimation(double phase)
    {
        _animationPhase = phase;
        if (_isActive)
            InvalidateVisual();
    }
}

/// <summary>
/// Extension for highlighting individual file items during drag over.
/// </summary>
public class ItemDropHighlight : Control
{
    private bool _isHighlighted;
    private DragDropEffects _dropEffect = DragDropEffects.None;
    
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<ItemDropHighlight, bool>(nameof(IsHighlighted), false);
    
    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set
        {
            SetValue(IsHighlightedProperty, value);
            _isHighlighted = value;
            InvalidateVisual();
        }
    }
    
    public DragDropEffects DropEffect
    {
        get => _dropEffect;
        set
        {
            _dropEffect = value;
            InvalidateVisual();
        }
    }
    
    public ItemDropHighlight()
    {
        IsHitTestVisible = false;
    }
    
    public override void Render(DrawingContext context)
    {
        if (!_isHighlighted)
            return;
        
        var color = _dropEffect switch
        {
            DragDropEffects.Copy => Color.FromArgb(60, 60, 180, 60),
            DragDropEffects.Move => Color.FromArgb(60, 70, 130, 180),
            DragDropEffects.Link => Color.FromArgb(60, 128, 90, 160),
            _ => Color.FromArgb(40, 100, 100, 100)
        };
        
        context.DrawRectangle(
            new SolidColorBrush(color),
            new Pen(new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B)), 1),
            new Rect(Bounds.Size),
            2, 2);
    }
}
