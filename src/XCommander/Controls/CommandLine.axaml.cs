using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace XCommander.Controls;

public partial class CommandLine : UserControl
{
    public static readonly StyledProperty<string> CommandTextProperty =
        AvaloniaProperty.Register<CommandLine, string>(nameof(CommandText), string.Empty);
    
    public static readonly StyledProperty<string> CurrentDirectoryProperty =
        AvaloniaProperty.Register<CommandLine, string>(nameof(CurrentDirectory), string.Empty);
    
    public static readonly StyledProperty<ICommand?> ExecuteCommandProperty =
        AvaloniaProperty.Register<CommandLine, ICommand?>(nameof(ExecuteCommand));
    
    public string CommandText
    {
        get => GetValue(CommandTextProperty);
        set => SetValue(CommandTextProperty, value);
    }
    
    public string CurrentDirectory
    {
        get => GetValue(CurrentDirectoryProperty);
        set => SetValue(CurrentDirectoryProperty, value);
    }
    
    public ICommand? ExecuteCommand
    {
        get => GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }
    
    public ObservableCollection<string> CommandHistory { get; } = new();
    
    private int _historyIndex = -1;
    
    public CommandLine()
    {
        InitializeComponent();
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (!string.IsNullOrWhiteSpace(CommandText))
                {
                    // Add to history
                    if (CommandHistory.Count == 0 || CommandHistory[^1] != CommandText)
                    {
                        CommandHistory.Add(CommandText);
                    }
                    _historyIndex = CommandHistory.Count;
                    
                    // Execute
                    ExecuteCommand?.Execute(CommandText);
                    CommandText = string.Empty;
                }
                e.Handled = true;
                break;
                
            case Key.Up:
                NavigateHistory(-1);
                e.Handled = true;
                break;
                
            case Key.Down:
                NavigateHistory(1);
                e.Handled = true;
                break;
                
            case Key.Escape:
                CommandText = string.Empty;
                _historyIndex = CommandHistory.Count;
                e.Handled = true;
                break;
        }
    }
    
    private void NavigateHistory(int direction)
    {
        if (CommandHistory.Count == 0)
            return;
        
        _historyIndex += direction;
        
        if (_historyIndex < 0)
            _historyIndex = 0;
        else if (_historyIndex >= CommandHistory.Count)
        {
            _historyIndex = CommandHistory.Count;
            CommandText = string.Empty;
            return;
        }
        
        CommandText = CommandHistory[_historyIndex];
    }
    
    public void Focus()
    {
        var input = this.FindControl<TextBox>("CommandInput");
        input?.Focus();
    }
}
