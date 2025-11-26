using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Text;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class KeyboardShortcutsDialog : Window
{
    private KeyboardShortcutManager? _manager;
    
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        
        DataContextChanged += (_, _) =>
        {
            if (DataContext is KeyboardShortcutManager manager)
            {
                _manager = manager;
                manager.ExportRequested += OnExportRequested;
                manager.ImportRequested += OnImportRequested;
            }
        };
    }
    
    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not KeyboardShortcut shortcut)
            return;
        
        // Build the gesture string
        var gesture = new StringBuilder();
        
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            gesture.Append("Ctrl+");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            gesture.Append("Alt+");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            gesture.Append("Shift+");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            gesture.Append("Cmd+");
        
        // Skip modifier-only keys
        var key = e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }
        
        gesture.Append(key.ToString());
        
        var gestureStr = gesture.ToString();
        
        // Check for conflicts
        if (_manager != null)
        {
            var conflict = _manager.GetConflictingShortcut(shortcut.Id, gestureStr);
            if (conflict != null)
            {
                textBox.Watermark = $"Conflicts with: {conflict.Name}";
                // Still allow setting it, user may want to resolve manually
            }
        }
        
        shortcut.CurrentGesture = gestureStr;
        e.Handled = true;
    }
    
    private void OnClearShortcut(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyboardShortcut shortcut)
        {
            shortcut.CurrentGesture = string.Empty;
        }
    }
    
    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _manager?.SaveShortcuts();
        Close(true);
    }
    
    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
    
    private async void OnExportRequested(object? sender, EventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Keyboard Shortcuts",
            SuggestedFileName = "shortcuts.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });
        
        if (file != null && _manager != null)
        {
            await _manager.ExportToFileAsync(file.Path.LocalPath);
        }
    }
    
    private async void OnImportRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Keyboard Shortcuts",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });
        
        if (files.Count > 0 && _manager != null)
        {
            await _manager.ImportFromFileAsync(files[0].Path.LocalPath);
        }
    }
}
