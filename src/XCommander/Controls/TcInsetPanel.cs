using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XCommander.Controls;

public class TcInsetPanel : ContentControl
{
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<TcInsetPanel, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ShadowBrushProperty =
        AvaloniaProperty.Register<TcInsetPanel, IBrush?>(nameof(ShadowBrush));

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
