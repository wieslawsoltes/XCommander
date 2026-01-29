using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls.DataGridFiltering;
using ReactiveUI;

namespace XCommander.ViewModels;

public sealed class TextFilterContext : ReactiveObject, IFilterTextContext
{
    private readonly Action<string?> _apply;
    private readonly Action _clear;
    private string? _text;

    public TextFilterContext(string label, Action<string?> apply, Action clear)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _clear = clear ?? throw new ArgumentNullException(nameof(clear));
        ApplyCommand = ReactiveCommand.Create(() => _apply(Text));
        ClearCommand = ReactiveCommand.Create(() =>
        {
            Text = string.Empty;
            _clear();
        });
    }

    public string Label { get; }

    public string? Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }
}

public sealed class NumberFilterContext : ReactiveObject, IFilterNumberContext
{
    private readonly Action<double?, double?> _apply;
    private readonly Action _clear;
    private double? _min;
    private double? _max;

    public NumberFilterContext(string label, Action<double?, double?> apply, Action clear)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _clear = clear ?? throw new ArgumentNullException(nameof(clear));
        ApplyCommand = ReactiveCommand.Create(() => _apply(MinValue, MaxValue));
        ClearCommand = ReactiveCommand.Create(() =>
        {
            MinValue = null;
            MaxValue = null;
            _clear();
        });
    }

    public string Label { get; }

    public double Minimum { get; set; } = 0;
    public double Maximum { get; set; } = double.MaxValue;

    public double? MinValue
    {
        get => _min;
        set => this.RaiseAndSetIfChanged(ref _min, value);
    }

    public double? MaxValue
    {
        get => _max;
        set => this.RaiseAndSetIfChanged(ref _max, value);
    }

    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }
}

public sealed class DateFilterContext : ReactiveObject, IFilterDateContext
{
    private readonly Action<DateTimeOffset?, DateTimeOffset?> _apply;
    private readonly Action _clear;
    private DateTimeOffset? _from;
    private DateTimeOffset? _to;

    public DateFilterContext(string label, Action<DateTimeOffset?, DateTimeOffset?> apply, Action clear)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _clear = clear ?? throw new ArgumentNullException(nameof(clear));
        ApplyCommand = ReactiveCommand.Create(() => _apply(From, To));
        ClearCommand = ReactiveCommand.Create(() =>
        {
            From = null;
            To = null;
            _clear();
        });
    }

    public string Label { get; }

    public DateTimeOffset? From
    {
        get => _from;
        set => this.RaiseAndSetIfChanged(ref _from, value);
    }

    public DateTimeOffset? To
    {
        get => _to;
        set => this.RaiseAndSetIfChanged(ref _to, value);
    }

    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }
}

public sealed class EnumFilterContext : ReactiveObject, IFilterEnumContext
{
    private readonly ObservableCollection<IEnumOption> _options;
    private readonly Action<IReadOnlyList<string>> _apply;
    private readonly Action _clear;

    public EnumFilterContext(string label, IEnumerable<string> options, Action<IReadOnlyList<string>> apply, Action clear)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        _options = new ObservableCollection<IEnumOption>(options.Select(o => new EnumOption(o)));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _clear = clear ?? throw new ArgumentNullException(nameof(clear));
        ApplyCommand = ReactiveCommand.Create(() => _apply(SelectedValues));
        ClearCommand = ReactiveCommand.Create(() =>
        {
            SelectNone();
            _clear();
        });
    }

    public string Label { get; }

    public ObservableCollection<IEnumOption> Options => _options;

    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }

    private IReadOnlyList<string> SelectedValues => _options
        .Where(option => option.IsSelected)
        .Select(option => option.Display)
        .ToArray();

    public void SelectNone()
    {
        foreach (var option in _options)
        {
            option.IsSelected = false;
        }
    }
}

public sealed class EnumOption : ReactiveObject, IEnumOption
{
    private bool _isSelected;

    public EnumOption(string display)
    {
        Display = display ?? throw new ArgumentNullException(nameof(display));
    }

    public string Display { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}
