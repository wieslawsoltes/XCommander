using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XCommander.Controls;

public class TcBevelPanel : ContentControl
{
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<TcBevelPanel, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ShadowBrushProperty =
        AvaloniaProperty.Register<TcBevelPanel, IBrush?>(nameof(ShadowBrush));

    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public IBrush? ShadowBrush
    {
        get => GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }
}
