using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.Controls;

public partial class QuickFilterBar : UserControl
{
    public static readonly StyledProperty<string> FilterTextProperty =
        AvaloniaProperty.Register<QuickFilterBar, string>(nameof(FilterText), string.Empty);
    
    public static readonly StyledProperty<int> MatchCountProperty =
        AvaloniaProperty.Register<QuickFilterBar, int>(nameof(MatchCount), 0);
    
    public static readonly StyledProperty<bool> CaseSensitiveProperty =
        AvaloniaProperty.Register<QuickFilterBar, bool>(nameof(CaseSensitive), false);
    
    public static readonly StyledProperty<bool> UseRegexProperty =
        AvaloniaProperty.Register<QuickFilterBar, bool>(nameof(UseRegex), false);
    
    public static readonly StyledProperty<bool> IncludeDirectoriesProperty =
        AvaloniaProperty.Register<QuickFilterBar, bool>(nameof(IncludeDirectories), true);
    
    public static readonly StyledProperty<string?> SelectedPresetProperty =
        AvaloniaProperty.Register<QuickFilterBar, string?>(nameof(SelectedPreset));
    
    public static readonly StyledProperty<ICommand?> FilterChangedCommandProperty =
        AvaloniaProperty.Register<QuickFilterBar, ICommand?>(nameof(FilterChangedCommand));
    
    public string FilterText
    {
        get => GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }
    
    public int MatchCount
    {
        get => GetValue(MatchCountProperty);
        set => SetValue(MatchCountProperty, value);
    }
    
    public bool CaseSensitive
    {
        get => GetValue(CaseSensitiveProperty);
        set => SetValue(CaseSensitiveProperty, value);
    }
    
    public bool UseRegex
    {
        get => GetValue(UseRegexProperty);
        set => SetValue(UseRegexProperty, value);
    }
    
    public bool IncludeDirectories
    {
        get => GetValue(IncludeDirectoriesProperty);
        set => SetValue(IncludeDirectoriesProperty, value);
    }
    
    public string? SelectedPreset
    {
        get => GetValue(SelectedPresetProperty);
        set => SetValue(SelectedPresetProperty, value);
    }
    
    public ICommand? FilterChangedCommand
    {
        get => GetValue(FilterChangedCommandProperty);
        set => SetValue(FilterChangedCommandProperty, value);
    }
    
    public ICommand ClearFilterCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    
    public event EventHandler<string>? FilterChanged;
    public event EventHandler? FilterCleared;
    
    public QuickFilterBar()
    {
        ClearFilterCommand = new RelayCommand(ClearFilter);
        ApplyFilterCommand = new RelayCommand(ApplyFilter);
        
        InitializeComponent();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == FilterTextProperty)
        {
            OnFilterTextChanged();
        }
        else if (change.Property == CaseSensitiveProperty ||
                 change.Property == UseRegexProperty ||
                 change.Property == IncludeDirectoriesProperty)
        {
            ApplyFilter();
        }
        else if (change.Property == SelectedPresetProperty)
        {
            OnPresetChanged(change.GetNewValue<string?>());
        }
    }
    
    private void OnFilterTextChanged()
    {
        // Debounce: Apply filter after short delay
        ApplyFilter();
    }
    
    private void OnPresetChanged(string? preset)
    {
        if (!string.IsNullOrEmpty(preset) && preset != "All files")
        {
            FilterText = preset;
        }
        else if (preset == "All files")
        {
            FilterText = string.Empty;
        }
    }
    
    private void ApplyFilter()
    {
        FilterChanged?.Invoke(this, FilterText);
        FilterChangedCommand?.Execute(FilterText);
    }
    
    private void ClearFilter()
    {
        FilterText = string.Empty;
        IsVisible = false;
        FilterCleared?.Invoke(this, EventArgs.Empty);
    }
    
    public void Show()
    {
        IsVisible = true;
        var textBox = this.FindControl<TextBox>("FilterTextBox");
        textBox?.Focus();
        textBox?.SelectAll();
    }
    
    public void Hide()
    {
        IsVisible = false;
        FilterText = string.Empty;
    }
    
    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }
}
