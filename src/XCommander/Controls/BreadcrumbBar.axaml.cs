using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace XCommander.Controls;

public partial class BreadcrumbBar : UserControl
{
    public static readonly StyledProperty<string> PathProperty =
        AvaloniaProperty.Register<BreadcrumbBar, string>(nameof(Path), string.Empty);
    
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ICommand?>(nameof(NavigateCommand));
    
    public static readonly StyledProperty<ICommand?> DropToSegmentCommandProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ICommand?>(nameof(DropToSegmentCommand));
    
    private BreadcrumbSegment? _highlightedSegment;
    
    public string Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }
    
    public ICommand? NavigateCommand
    {
        get => GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }
    
    /// <summary>
    /// Command executed when files are dropped onto a breadcrumb segment.
    /// Parameter is a tuple of (targetPath, droppedPaths).
    /// </summary>
    public ICommand? DropToSegmentCommand
    {
        get => GetValue(DropToSegmentCommandProperty);
        set => SetValue(DropToSegmentCommandProperty, value);
    }
    
    public ObservableCollection<BreadcrumbSegment> Segments { get; } = new();
    
    /// <summary>
    /// Event raised when a drag operation enters a segment.
    /// </summary>
    public event EventHandler<BreadcrumbDragEventArgs>? SegmentDragEnter;
    
    /// <summary>
    /// Event raised when files are dropped onto a segment.
    /// </summary>
    public event EventHandler<BreadcrumbDropEventArgs>? SegmentDrop;
    
    public BreadcrumbBar()
    {
        InitializeComponent();
        
        // Set up drag/drop handlers
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        
        DragDrop.SetAllowDrop(this, true);
    }
    
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var segment = GetSegmentAtPosition(e.GetPosition(this));
        if (segment != null)
        {
            _highlightedSegment = segment;
            segment.IsHighlighted = true;
            SegmentDragEnter?.Invoke(this, new BreadcrumbDragEventArgs(segment));
            e.DragEffects = e.KeyModifiers.HasFlag(KeyModifiers.Control) 
                ? DragDropEffects.Copy 
                : DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }
    
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var segment = GetSegmentAtPosition(e.GetPosition(this));
        
        if (segment != _highlightedSegment)
        {
            if (_highlightedSegment != null)
                _highlightedSegment.IsHighlighted = false;
            
            _highlightedSegment = segment;
            
            if (segment != null)
                segment.IsHighlighted = true;
        }
        
        if (segment != null)
        {
            e.DragEffects = e.KeyModifiers.HasFlag(KeyModifiers.Control) 
                ? DragDropEffects.Copy 
                : DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }
    
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (_highlightedSegment != null)
        {
            _highlightedSegment.IsHighlighted = false;
            _highlightedSegment = null;
        }
    }
    
    private void OnDrop(object? sender, DragEventArgs e)
    {
        var segment = GetSegmentAtPosition(e.GetPosition(this));
        
        if (segment != null)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToList();
                var effect = e.KeyModifiers.HasFlag(KeyModifiers.Control) 
                    ? DragDropEffects.Copy 
                    : DragDropEffects.Move;
                
                SegmentDrop?.Invoke(this, new BreadcrumbDropEventArgs(segment, paths, effect));
                DropToSegmentCommand?.Execute((segment.Path, paths, effect));
            }
        }
        
        if (_highlightedSegment != null)
        {
            _highlightedSegment.IsHighlighted = false;
            _highlightedSegment = null;
        }
    }
    
    private BreadcrumbSegment? GetSegmentAtPosition(Point position)
    {
        // Find which segment button contains the position
        var itemsControl = this.FindControl<ItemsControl>("BreadcrumbItems");
        if (itemsControl == null) return null;
        
        foreach (var segment in Segments)
        {
            // Get the container for this segment
            var container = itemsControl.ContainerFromItem(segment);
            if (container is Control control)
            {
                var bounds = control.Bounds;
                var controlPos = control.TranslatePoint(new Point(0, 0), this);
                
                if (controlPos.HasValue)
                {
                    var rect = new Rect(controlPos.Value, bounds.Size);
                    if (rect.Contains(position))
                        return segment;
                }
            }
        }
        
        return null;
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == PathProperty)
        {
            UpdateSegments();
        }
    }
    
    private void UpdateSegments()
    {
        Segments.Clear();
        
        if (string.IsNullOrEmpty(Path))
            return;
        
        var parts = Path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        
        // Handle root differently on different platforms
        if (OperatingSystem.IsWindows())
        {
            if (parts.Length > 0 && parts[0].EndsWith(':'))
            {
                currentPath = parts[0] + "\\";
                Segments.Add(new BreadcrumbSegment
                {
                    Name = parts[0],
                    Path = currentPath,
                    IsLast = parts.Length == 1
                });
                parts = parts.Skip(1).ToArray();
            }
        }
        else
        {
            if (Path.StartsWith('/'))
            {
                currentPath = "/";
                Segments.Add(new BreadcrumbSegment
                {
                    Name = "/",
                    Path = "/",
                    IsLast = parts.Length == 0
                });
            }
        }
        
        for (int i = 0; i < parts.Length; i++)
        {
            currentPath = System.IO.Path.Combine(currentPath, parts[i]);
            Segments.Add(new BreadcrumbSegment
            {
                Name = parts[i],
                Path = currentPath,
                IsLast = i == parts.Length - 1
            });
        }
    }
}

public class BreadcrumbSegment : AvaloniaObject
{
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<BreadcrumbSegment, bool>(nameof(IsHighlighted), false);
    
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsLast { get; set; }
    
    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }
}

/// <summary>
/// Event args for breadcrumb drag events.
/// </summary>
public class BreadcrumbDragEventArgs : EventArgs
{
    public BreadcrumbSegment Segment { get; }
    
    public BreadcrumbDragEventArgs(BreadcrumbSegment segment)
    {
        Segment = segment;
    }
}

/// <summary>
/// Event args for breadcrumb drop events.
/// </summary>
public class BreadcrumbDropEventArgs : EventArgs
{
    public BreadcrumbSegment Segment { get; }
    public IReadOnlyList<string> DroppedPaths { get; }
    public DragDropEffects Effect { get; }
    
    public BreadcrumbDropEventArgs(BreadcrumbSegment segment, IReadOnlyList<string> paths, DragDropEffects effect)
    {
        Segment = segment;
        DroppedPaths = paths;
        Effect = effect;
    }
}
