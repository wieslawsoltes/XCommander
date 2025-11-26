using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace XCommander.Services;

/// <summary>
/// Manages focus across panels and dialogs for proper keyboard navigation.
/// </summary>
public class FocusManager
{
    private Control? _lastFocusedControl;
    private Control? _activePanelControl;
    private readonly WeakReference<Control>? _mainWindow;
    
    public FocusManager(Control? mainWindow = null)
    {
        if (mainWindow != null)
            _mainWindow = new WeakReference<Control>(mainWindow);
    }
    
    /// <summary>
    /// Gets or sets the currently active panel control.
    /// </summary>
    public Control? ActivePanel
    {
        get => _activePanelControl;
        set
        {
            if (_activePanelControl != value)
            {
                _activePanelControl = value;
                ActivePanelChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    /// <summary>
    /// Event raised when the active panel changes.
    /// </summary>
    public event EventHandler? ActivePanelChanged;
    
    /// <summary>
    /// Save the current focus before opening a dialog.
    /// </summary>
    public void SaveFocus()
    {
        if (_mainWindow?.TryGetTarget(out var window) == true)
        {
            _lastFocusedControl = FocusManagerHelper.GetFocusedElement(window);
        }
    }
    
    /// <summary>
    /// Restore focus after a dialog closes.
    /// </summary>
    public void RestoreFocus()
    {
        if (_lastFocusedControl != null && _lastFocusedControl.IsVisible)
        {
            _lastFocusedControl.Focus();
        }
        else if (_activePanelControl != null)
        {
            // Fall back to active panel
            _activePanelControl.Focus();
        }
    }
    
    /// <summary>
    /// Set focus to the active panel.
    /// </summary>
    public void FocusActivePanel()
    {
        _activePanelControl?.Focus();
    }
    
    /// <summary>
    /// Switch focus to the specified panel.
    /// </summary>
    public void SwitchToPanel(Control panel)
    {
        ActivePanel = panel;
        panel.Focus();
    }
    
    /// <summary>
    /// Track which control has focus within a panel.
    /// </summary>
    public void TrackFocus(Control control)
    {
        _lastFocusedControl = control;
    }
}

/// <summary>
/// Helper class for focus management operations.
/// </summary>
public static class FocusManagerHelper
{
    /// <summary>
    /// Get the currently focused element within a control tree.
    /// </summary>
    public static Control? GetFocusedElement(Control root)
    {
        // Check if root is focused
        if (root.IsFocused)
            return root;
        
        // Check children
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control control)
                {
                    var focused = GetFocusedElement(control);
                    if (focused != null)
                        return focused;
                }
            }
        }
        else if (root is ContentControl contentControl && contentControl.Content is Control content)
        {
            var focused = GetFocusedElement(content);
            if (focused != null)
                return focused;
        }
        else if (root is Decorator decorator && decorator.Child is Control child)
        {
            var focused = GetFocusedElement(child);
            if (focused != null)
                return focused;
        }
        
        return null;
    }
    
    /// <summary>
    /// Find the first focusable control in tab order.
    /// </summary>
    public static Control? FindFirstFocusable(Control root)
    {
        if (root.Focusable && root.IsVisible && root.IsEnabled)
            return root;
        
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control control)
                {
                    var focusable = FindFirstFocusable(control);
                    if (focusable != null)
                        return focusable;
                }
            }
        }
        else if (root is ContentControl contentControl && contentControl.Content is Control content)
        {
            return FindFirstFocusable(content);
        }
        else if (root is Decorator decorator && decorator.Child is Control child)
        {
            return FindFirstFocusable(child);
        }
        
        return null;
    }
    
    /// <summary>
    /// Set up tab order for a collection of controls.
    /// </summary>
    public static void SetTabOrder(params Control[] controls)
    {
        for (int i = 0; i < controls.Length; i++)
        {
            KeyboardNavigation.SetTabIndex(controls[i], i);
        }
    }
}

/// <summary>
/// Behavior for automatic focus restoration after dialogs.
/// </summary>
public class DialogFocusHelper
{
    private readonly FocusManager _focusManager;
    
    public DialogFocusHelper(FocusManager focusManager)
    {
        _focusManager = focusManager;
    }
    
    /// <summary>
    /// Execute an action that shows a dialog, automatically saving and restoring focus.
    /// </summary>
    public async Task<T?> WithFocusRestoration<T>(Func<Task<T?>> dialogAction)
    {
        _focusManager.SaveFocus();
        try
        {
            return await dialogAction();
        }
        finally
        {
            _focusManager.RestoreFocus();
        }
    }
    
    /// <summary>
    /// Execute an action that shows a dialog, automatically saving and restoring focus.
    /// </summary>
    public async Task WithFocusRestoration(Func<Task> dialogAction)
    {
        _focusManager.SaveFocus();
        try
        {
            await dialogAction();
        }
        finally
        {
            _focusManager.RestoreFocus();
        }
    }
}
