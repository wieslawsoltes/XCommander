using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using SessionWindowState = XCommander.Services.WindowState;
using AvaloniaWindowState = Avalonia.Controls.WindowState;

namespace XCommander.Behaviors;

public sealed class WindowSessionStateBehavior : Behavior<Window>
{
    public static readonly StyledProperty<SessionWindowState?> StateProperty =
        AvaloniaProperty.Register<WindowSessionStateBehavior, SessionWindowState?>(nameof(State));

    private bool _isApplying;
    private bool _isUpdating;

    public SessionWindowState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null)
            return;

        AssociatedObject.Opened += OnOpened;
        AssociatedObject.PositionChanged += OnPositionChanged;
        AssociatedObject.PropertyChanged += OnWindowPropertyChanged;

        ApplyState();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.Opened -= OnOpened;
            AssociatedObject.PositionChanged -= OnPositionChanged;
            AssociatedObject.PropertyChanged -= OnWindowPropertyChanged;
        }

        base.OnDetaching();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty && !_isUpdating)
        {
            ApplyState();
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyState();
        UpdateStateFromWindow();
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        UpdateStateFromWindow();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty ||
            e.Property == Window.WidthProperty ||
            e.Property == Window.HeightProperty)
        {
            UpdateStateFromWindow();
        }
    }

    private void ApplyState()
    {
        if (_isApplying || AssociatedObject == null || State == null)
            return;

        _isApplying = true;
        try
        {
            if (State.Width > 0)
                AssociatedObject.Width = State.Width;
            if (State.Height > 0)
                AssociatedObject.Height = State.Height;

            if (State.Left != 0 || State.Top != 0)
                AssociatedObject.Position = new PixelPoint((int)State.Left, (int)State.Top);

            AssociatedObject.WindowState = State.IsMaximized ? AvaloniaWindowState.Maximized : AvaloniaWindowState.Normal;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private void UpdateStateFromWindow()
    {
        if (_isApplying || AssociatedObject == null)
            return;

        _isUpdating = true;
        try
        {
            SetCurrentValue(StateProperty, new SessionWindowState
            {
                Left = AssociatedObject.Position.X,
                Top = AssociatedObject.Position.Y,
                Width = AssociatedObject.Width,
                Height = AssociatedObject.Height,
                IsMaximized = AssociatedObject.WindowState == AvaloniaWindowState.Maximized
            });
        }
        finally
        {
            _isUpdating = false;
        }
    }
}
