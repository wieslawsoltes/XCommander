using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;
using XCommander.ViewModels;
using XCommander.Views.Dialogs;

namespace XCommander.Views.Dialogs;

public partial class DirectoryHotlistDialog : UserControl
{
    public DirectoryHotlistDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DirectoryHotlistViewModel vm)
        {
            vm.NavigateSelectedCommand.Execute(null);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.NavigateSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            OnCloseClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None)
        {
            var shortcut = KeyToShortcut(e.Key);
            if (!string.IsNullOrEmpty(shortcut))
            {
                _ = vm.TryNavigateByShortcutAsync(shortcut);
                e.Handled = true;
            }
        }
    }

    private async void OnNewCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm)
            return;

        var name = await ShowInputAsync("New Category", "Enter category name:", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var parentId = vm.GetSelectedParentCategoryId();
        await vm.AddCategoryAsync(name.Trim(), parentId);
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm || vm.SelectedNode == null)
            return;

        if (vm.SelectedNode.IsSeparator)
            return;

        var title = vm.SelectedNode.IsCategory ? "Rename Category" : "Rename Entry";
        var name = await ShowInputAsync(title, "Enter new name:", vm.SelectedNode.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        await vm.RenameSelectedAsync(name.Trim());
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm || vm.SelectedNode == null)
            return;

        if (vm.SelectedNode.IsCategory)
        {
            var deleteContents = await ShowConfirmAsync(
                "Delete Category",
                "Delete category and all contents?\n\nNo = move items to parent.");
            await vm.DeleteSelectedAsync(deleteContents);
        }
        else
        {
            var ok = await ShowConfirmAsync("Delete Entry", "Delete selected hotlist entry?");
            if (ok)
                await vm.DeleteSelectedAsync(deleteContents: false);
        }
    }

    private async void OnSetShortcutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm || vm.SelectedNode == null)
            return;

        if (vm.SelectedNode.IsCategory || vm.SelectedNode.IsSeparator)
            return;

        var shortcut = await ShowInputAsync(
            "Set Shortcut",
            "Enter single key (A-Z, 0-9). Leave empty to clear.",
            vm.SelectedNode.Shortcut ?? string.Empty);

        if (shortcut == null)
            return;

        shortcut = shortcut.Trim();
        if (shortcut.Length == 0)
        {
            await vm.SetShortcutAsync(null);
            return;
        }

        var keyChar = shortcut[0];
        if (!char.IsLetterOrDigit(keyChar))
            return;

        shortcut = char.ToUpperInvariant(keyChar).ToString();

        await vm.SetShortcutAsync(shortcut);
    }

    private async void OnAddSeparatorClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryHotlistViewModel vm)
            return;

        var parentId = vm.GetSelectedParentCategoryId();
        await vm.AddSeparatorAsync(parentId);
    }

    private async Task<string?> ShowInputAsync(string title, string prompt, string defaultValue)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return null;

        var dialog = new InputDialog(title, prompt, defaultValue);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }

    private async Task<bool> ShowConfirmAsync(string title, string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return false;

        var dialog = new ConfirmDialog(title, message);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }

    private static string? KeyToShortcut(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            var offset = key - Key.A;
            return ((char)('A' + offset)).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            var offset = key - Key.D0;
            return ((char)('0' + offset)).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            var offset = key - Key.NumPad0;
            return ((char)('0' + offset)).ToString();
        }

        return null;
    }
}
