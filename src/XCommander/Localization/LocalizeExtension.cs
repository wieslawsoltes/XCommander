using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace XCommander.Localization;

/// <summary>
/// Markup extension for localized strings in XAML.
/// Usage: {loc:Localize Menu.File}
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Create a binding to the localization manager
        var binding = new Binding
        {
            Source = LocalizationManager.Instance,
            Path = $"[{Key}]",
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}

/// <summary>
/// Alternative markup extension that returns the string directly.
/// Usage: {loc:L Menu.File}
/// </summary>
public class LExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LExtension() { }

    public LExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationManager.Instance[Key];
    }
}
