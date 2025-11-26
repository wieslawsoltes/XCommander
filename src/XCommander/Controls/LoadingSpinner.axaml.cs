using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace XCommander.Controls;

public partial class LoadingSpinner : UserControl
{
    private DispatcherTimer? _animationTimer;
    private double _currentAngle;
    
    public static readonly StyledProperty<bool> IsSpinningProperty =
        AvaloniaProperty.Register<LoadingSpinner, bool>(nameof(IsSpinning), false);
    
    public static readonly StyledProperty<IBrush> SpinnerColorProperty =
        AvaloniaProperty.Register<LoadingSpinner, IBrush>(nameof(SpinnerColor));
    
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LoadingSpinner, string>(nameof(Text), string.Empty);
    
    public static readonly StyledProperty<bool> ShowTextProperty =
        AvaloniaProperty.Register<LoadingSpinner, bool>(nameof(ShowText), false);
    
    public static readonly StyledProperty<double> SpinSpeedProperty =
        AvaloniaProperty.Register<LoadingSpinner, double>(nameof(SpinSpeed), 4.0);
    
    public bool IsSpinning
    {
        get => GetValue(IsSpinningProperty);
        set => SetValue(IsSpinningProperty, value);
    }
    
    public IBrush SpinnerColor
    {
        get => GetValue(SpinnerColorProperty);
        set => SetValue(SpinnerColorProperty, value);
    }
    
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public bool ShowText
    {
        get => GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }
    
    public double SpinSpeed
    {
        get => GetValue(SpinSpeedProperty);
        set => SetValue(SpinSpeedProperty, value);
    }
    
    public LoadingSpinner()
    {
        Width = 24;
        Height = 24;
        
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
        
        // Set default color
        SpinnerColor = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        
        this.PropertyChanged += OnPropertyChanged;
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }
    
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UpdateArcGeometry();
        if (IsSpinning)
            StartAnimation();
    }
    
    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsSpinningProperty)
        {
            if (IsSpinning)
                StartAnimation();
            else
                StopAnimation();
        }
    }
    
    private void StartAnimation()
    {
        _animationTimer?.Start();
        UpdateArcGeometry();
    }
    
    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _currentAngle = 0;
    }
    
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _currentAngle += SpinSpeed;
        if (_currentAngle >= 360)
            _currentAngle -= 360;
        
        if (this.FindControl<Canvas>("SpinnerCanvas")?.RenderTransform is RotateTransform rotation)
        {
            rotation.Angle = _currentAngle;
        }
    }
    
    private void UpdateArcGeometry()
    {
        var arc = this.FindControl<AvaloniaPath>("SpinnerArc");
        if (arc == null) return;
        
        var size = Math.Min(Width, Height);
        var radius = (size - 4) / 2;
        var center = new Point(size / 2, size / 2);
        
        // Create arc from 0 to 270 degrees
        var startAngle = 0.0;
        var sweepAngle = 270.0;
        
        var startRad = startAngle * Math.PI / 180;
        var endRad = (startAngle + sweepAngle) * Math.PI / 180;
        
        var startPoint = new Point(
            center.X + radius * Math.Cos(startRad),
            center.Y + radius * Math.Sin(startRad));
        
        var endPoint = new Point(
            center.X + radius * Math.Cos(endRad),
            center.Y + radius * Math.Sin(endRad));
        
        var pathFigure = new PathFigure
        {
            StartPoint = startPoint,
            IsClosed = false
        };
        
        var arcSegment = new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        };
        
        pathFigure.Segments ??= new PathSegments();
        pathFigure.Segments.Add(arcSegment);
        
        var geometry = new PathGeometry();
        geometry.Figures ??= new PathFigures();
        geometry.Figures.Add(pathFigure);
        
        arc.Data = geometry;
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAnimation();
        _animationTimer = null;
    }
}
