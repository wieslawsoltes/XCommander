using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace XCommander.Controls;

/// <summary>
/// Adorner displayed during drag operations showing file count and operation type.
/// </summary>
public class DragAdorner : Control
{
    private readonly List<string> _paths = new();
    private int _fileCount;
    private int _folderCount;
    private DragDropEffects _currentEffect = DragDropEffects.Copy;
    private Point _position;
    
    public static readonly StyledProperty<bool> IsVisibleAdornerProperty =
        AvaloniaProperty.Register<DragAdorner, bool>(nameof(IsVisibleAdorner), false);
    
    public bool IsVisibleAdorner
    {
        get => GetValue(IsVisibleAdornerProperty);
        set => SetValue(IsVisibleAdornerProperty, value);
    }
    
    public DragAdorner()
    {
        IsHitTestVisible = false;
        IsVisible = false;
    }
    
    public void StartDrag(IEnumerable<string> paths, Point position)
    {
        _paths.Clear();
        _fileCount = 0;
        _folderCount = 0;
        
        foreach (var path in paths)
        {
            _paths.Add(path);
            if (Directory.Exists(path))
                _folderCount++;
            else
                _fileCount++;
        }
        
        _position = position;
        IsVisible = true;
        IsVisibleAdorner = true;
        InvalidateVisual();
    }
    
    public void UpdatePosition(Point position)
    {
        _position = position;
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
    
    public void EndDrag()
    {
        _paths.Clear();
        _fileCount = 0;
        _folderCount = 0;
        IsVisible = false;
        IsVisibleAdorner = false;
        InvalidateVisual();
    }
    
    public override void Render(DrawingContext context)
    {
        if (!IsVisibleAdorner || _paths.Count == 0)
            return;
        
        var totalCount = _fileCount + _folderCount;
        
        // Build display text
        var text = totalCount switch
        {
            1 when _fileCount == 1 => Path.GetFileName(_paths[0]),
            1 when _folderCount == 1 => Path.GetFileName(_paths[0]),
            _ when _folderCount == 0 => $"{_fileCount} files",
            _ when _fileCount == 0 => $"{_folderCount} folders",
            _ => $"{_fileCount} files, {_folderCount} folders"
        };
        
        // Operation indicator
        var operationIcon = _currentEffect switch
        {
            DragDropEffects.Move => "→",
            DragDropEffects.Copy => "+",
            DragDropEffects.Link => "🔗",
            DragDropEffects.None => "⛔",
            _ => "+"
        };
        
        var operationText = _currentEffect switch
        {
            DragDropEffects.Move => "Move",
            DragDropEffects.Copy => "Copy",
            DragDropEffects.Link => "Link",
            DragDropEffects.None => "Can't drop here",
            _ => "Copy"
        };
        
        // Background color based on operation
        var bgColor = _currentEffect switch
        {
            DragDropEffects.Move => Color.FromArgb(230, 70, 130, 180),  // Steel blue for move
            DragDropEffects.Copy => Color.FromArgb(230, 60, 120, 60),   // Green for copy
            DragDropEffects.Link => Color.FromArgb(230, 128, 90, 160),  // Purple for link
            DragDropEffects.None => Color.FromArgb(230, 180, 60, 60),   // Red for invalid
            _ => Color.FromArgb(230, 60, 120, 60)
        };
        
        // Badge dimensions
        var padding = 8.0;
        
        var typeface = new Typeface("Segoe UI, San Francisco, -apple-system, sans-serif");
        var fontSize = 12.0;
        
        var formattedText = new FormattedText(
            $"{operationIcon} {operationText}: {text}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White);
        
        var badgeWidth = formattedText.Width + padding * 2;
        var badgeHeight = formattedText.Height + padding * 2;
        
        // Position badge offset from cursor
        var offsetX = 16.0;
        var offsetY = 16.0;
        var badgeRect = new Rect(_position.X + offsetX, _position.Y + offsetY, badgeWidth, badgeHeight);
        
        // Ensure badge stays within bounds
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            if (badgeRect.Right > Bounds.Width)
                badgeRect = badgeRect.WithX(Bounds.Width - badgeWidth - 4);
            if (badgeRect.Bottom > Bounds.Height)
                badgeRect = badgeRect.WithY(Bounds.Height - badgeHeight - 4);
        }
        
        // Draw shadow
        var shadowRect = badgeRect.Translate(new Vector(2, 2));
        context.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
            null,
            shadowRect,
            6, 6);
        
        // Draw background
        context.DrawRectangle(
            new SolidColorBrush(bgColor),
            new Pen(Brushes.White, 1),
            badgeRect,
            6, 6);
        
        // Draw text
        var textPoint = new Point(badgeRect.X + padding, badgeRect.Y + (badgeHeight - formattedText.Height) / 2);
        context.DrawText(formattedText, textPoint);
        
        // Draw item count badge if multiple items
        if (totalCount > 1)
        {
            var countText = new FormattedText(
                totalCount.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                Brushes.White);
            
            var countBadgeSize = Math.Max(countText.Width + 8, 20);
            var countBadgeRect = new Rect(
                badgeRect.Right - countBadgeSize / 2,
                badgeRect.Top - 8,
                countBadgeSize,
                18);
            
            context.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(255, 220, 80, 80)),
                new Pen(Brushes.White, 1),
                countBadgeRect,
                9, 9);
            
            var countPoint = new Point(
                countBadgeRect.X + (countBadgeSize - countText.Width) / 2,
                countBadgeRect.Y + (18 - countText.Height) / 2);
            context.DrawText(countText, countPoint);
        }
    }
}
