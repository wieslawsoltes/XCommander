using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace XCommander.Views;

public partial class QuickViewPanel : UserControl
{
    private ScrollViewer? _imageScrollViewer;
    private bool _isPanning;
    private Point _lastPanPosition;
    private Point[]? _lastTouchPositions;
    private double _currentScale = 1.0;
    private const double MinScale = 0.1;
    private const double MaxScale = 10.0;
    
    public QuickViewPanel()
    {
        InitializeComponent();
        
        // Find the image scroll viewer after initialization
        AttachedToVisualTree += OnAttachedToVisualTree;
    }
    
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Find the ScrollViewer containing the image
        _imageScrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        
        if (_imageScrollViewer != null)
        {
            _imageScrollViewer.PointerPressed += OnImagePointerPressed;
            _imageScrollViewer.PointerMoved += OnImagePointerMoved;
            _imageScrollViewer.PointerReleased += OnImagePointerReleased;
            _imageScrollViewer.PointerWheelChanged += OnImagePointerWheelChanged;
        }
    }
    
    private void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_imageScrollViewer == null) return;
        
        var point = e.GetCurrentPoint(_imageScrollViewer);
        
        // Middle mouse button or Ctrl+Left for panning
        if (point.Properties.IsMiddleButtonPressed ||
            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            _isPanning = true;
            _lastPanPosition = point.Position;
            e.Handled = true;
        }
    }
    
    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || _imageScrollViewer == null) return;
        
        var point = e.GetCurrentPoint(_imageScrollViewer);
        var delta = point.Position - _lastPanPosition;
        
        // Scroll by the delta amount
        var newOffsetX = _imageScrollViewer.Offset.X - delta.X;
        var newOffsetY = _imageScrollViewer.Offset.Y - delta.Y;
        
        _imageScrollViewer.Offset = new Vector(
            Math.Max(0, Math.Min(newOffsetX, _imageScrollViewer.Extent.Width - _imageScrollViewer.Viewport.Width)),
            Math.Max(0, Math.Min(newOffsetY, _imageScrollViewer.Extent.Height - _imageScrollViewer.Viewport.Height))
        );
        
        _lastPanPosition = point.Position;
        e.Handled = true;
    }
    
    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
    }
    
    private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_imageScrollViewer == null) return;
        
        // Ctrl+Wheel for zooming
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var image = _imageScrollViewer.Content as Image;
            if (image == null) return;
            
            var delta = e.Delta.Y > 0 ? 1.1 : 0.9;
            var newScale = Math.Clamp(_currentScale * delta, MinScale, MaxScale);
            
            if (Math.Abs(newScale - _currentScale) > 0.001)
            {
                _currentScale = newScale;
                
                // Apply scale transform
                image.RenderTransform = new ScaleTransform(_currentScale, _currentScale);
                image.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
            
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Handle touch events for two-finger pan gesture.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        if (e.Pointer.Type == PointerType.Touch)
        {
            _lastTouchPositions = new[] { e.GetPosition(this) };
        }
    }
    
    /// <summary>
    /// Handle touch move for two-finger pan.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        
        if (e.Pointer.Type == PointerType.Touch && _lastTouchPositions != null && _imageScrollViewer != null)
        {
            var currentPos = e.GetPosition(this);
            var lastPos = _lastTouchPositions[0];
            var delta = currentPos - lastPos;
            
            // Apply pan
            var newOffsetX = _imageScrollViewer.Offset.X - delta.X;
            var newOffsetY = _imageScrollViewer.Offset.Y - delta.Y;
            
            _imageScrollViewer.Offset = new Vector(
                Math.Max(0, Math.Min(newOffsetX, _imageScrollViewer.Extent.Width - _imageScrollViewer.Viewport.Width)),
                Math.Max(0, Math.Min(newOffsetY, _imageScrollViewer.Extent.Height - _imageScrollViewer.Viewport.Height))
            );
            
            _lastTouchPositions[0] = currentPos;
        }
    }
    
    /// <summary>
    /// Reset zoom level.
    /// </summary>
    public void ResetZoom()
    {
        _currentScale = 1.0;
        
        if (_imageScrollViewer?.Content is Image image)
        {
            image.RenderTransform = null;
        }
        
        _imageScrollViewer?.ScrollToHome();
    }
    
    /// <summary>
    /// Fit image to viewport.
    /// </summary>
    public void FitToWindow()
    {
        if (_imageScrollViewer?.Content is Image image && image.Source is not null)
        {
            var viewportWidth = _imageScrollViewer.Viewport.Width;
            var viewportHeight = _imageScrollViewer.Viewport.Height;
            var imageWidth = image.Source.Size.Width;
            var imageHeight = image.Source.Size.Height;
            
            if (imageWidth > 0 && imageHeight > 0)
            {
                var scaleX = viewportWidth / imageWidth;
                var scaleY = viewportHeight / imageHeight;
                _currentScale = Math.Min(scaleX, scaleY);
                
                image.RenderTransform = new ScaleTransform(_currentScale, _currentScale);
                image.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
        }
    }
}
