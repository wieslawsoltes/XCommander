using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using AvaloniaAction = Avalonia.Xaml.Interactivity.Action;

namespace XCommander.Behaviors;

public sealed class CloseWindowAction : AvaloniaAction
{
    public static readonly StyledProperty<bool?> DialogResultProperty =
        AvaloniaProperty.Register<CloseWindowAction, bool?>(nameof(DialogResult));

    public bool? DialogResult
    {
        get => GetValue(DialogResultProperty);
        set => SetValue(DialogResultProperty, value);
    }

    public override object? Execute(object? sender, object? parameter)
    {
        if (sender is not Visual visual)
            return null;

        if (TopLevel.GetTopLevel(visual) is Window window)
        {
            window.Close(DialogResult);
        }

        return null;
    }
}

public sealed class PickFolderPathAction : AvaloniaAction
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PickFolderPathAction, string?>(nameof(Title));

    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<PickFolderPathAction, string?>(nameof(Path));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public override object? Execute(object? sender, object? parameter)
    {
        _ = ExecuteAsync(sender as Visual);
        return null;
    }

    private async Task ExecuteAsync(Visual? sender)
    {
        if (sender == null)
            return;

        if (TopLevel.GetTopLevel(sender) is not TopLevel topLevel)
            return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Title ?? string.Empty,
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            SetCurrentValue(PathProperty, result[0].Path.LocalPath);
        }
    }
}

public sealed class PickFilePathAction : AvaloniaAction
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PickFilePathAction, string?>(nameof(Title));

    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<PickFilePathAction, string?>(nameof(Path));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public override object? Execute(object? sender, object? parameter)
    {
        _ = ExecuteAsync(sender as Visual);
        return null;
    }

    private async Task ExecuteAsync(Visual? sender)
    {
        if (sender == null)
            return;

        if (TopLevel.GetTopLevel(sender) is not TopLevel topLevel)
            return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Title ?? string.Empty,
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            SetCurrentValue(PathProperty, result[0].Path.LocalPath);
        }
    }
}
