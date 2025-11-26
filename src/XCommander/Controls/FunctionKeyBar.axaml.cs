using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace XCommander.Controls;

public partial class FunctionKeyBar : UserControl
{
    public static readonly StyledProperty<ICommand?> F1CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F1Command));
    
    public static readonly StyledProperty<ICommand?> F2CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F2Command));
    
    public static readonly StyledProperty<ICommand?> F3CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F3Command));
    
    public static readonly StyledProperty<ICommand?> F4CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F4Command));
    
    public static readonly StyledProperty<ICommand?> F5CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F5Command));
    
    public static readonly StyledProperty<ICommand?> F6CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F6Command));
    
    public static readonly StyledProperty<ICommand?> F7CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F7Command));
    
    public static readonly StyledProperty<ICommand?> F8CommandProperty =
        AvaloniaProperty.Register<FunctionKeyBar, ICommand?>(nameof(F8Command));
    
    public ICommand? F1Command
    {
        get => GetValue(F1CommandProperty);
        set => SetValue(F1CommandProperty, value);
    }
    
    public ICommand? F2Command
    {
        get => GetValue(F2CommandProperty);
        set => SetValue(F2CommandProperty, value);
    }
    
    public ICommand? F3Command
    {
        get => GetValue(F3CommandProperty);
        set => SetValue(F3CommandProperty, value);
    }
    
    public ICommand? F4Command
    {
        get => GetValue(F4CommandProperty);
        set => SetValue(F4CommandProperty, value);
    }
    
    public ICommand? F5Command
    {
        get => GetValue(F5CommandProperty);
        set => SetValue(F5CommandProperty, value);
    }
    
    public ICommand? F6Command
    {
        get => GetValue(F6CommandProperty);
        set => SetValue(F6CommandProperty, value);
    }
    
    public ICommand? F7Command
    {
        get => GetValue(F7CommandProperty);
        set => SetValue(F7CommandProperty, value);
    }
    
    public ICommand? F8Command
    {
        get => GetValue(F8CommandProperty);
        set => SetValue(F8CommandProperty, value);
    }
    
    public FunctionKeyBar()
    {
        InitializeComponent();
    }
}
