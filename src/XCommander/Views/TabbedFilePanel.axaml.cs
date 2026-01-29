using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using System.ComponentModel;
using System.Linq;
using XCommander.Models;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class TabbedFilePanel : UserControl
{
    private const string TabDragDataFormat = "XCommander.Tab";
    private TabViewModel? _draggedTab;
    private TabbedPanelViewModel? _sourcePanel;
    private TabViewModel? _thumbnailTab;
    private AppSettings? _panelSettings;

    public static readonly StyledProperty<string> TypeAheadBufferProperty =
        AvaloniaProperty.Register<TabbedFilePanel, string>(nameof(TypeAheadBuffer), string.Empty);

    public string TypeAheadBuffer
    {
        get => GetValue(TypeAheadBufferProperty);
        set => SetValue(TypeAheadBufferProperty, value);
    }
    
    // Type-ahead navigation (TC-style: just start typing to jump to files)
    private string _typeAheadBuffer = string.Empty;
    private DateTime _lastTypeAheadTime = DateTime.MinValue;
    private int _typeAheadTimeoutMs = 1000; // Clear buffer after 1 second of inactivity
    private DispatcherTimer? _typeAheadClearTimer;

    private enum QuickSearchMatchMode
    {
        StartsWith,
        Contains,
        StartsWithThenContains
    }
    
    public TabbedFilePanel()
    {
        InitializeComponent();
        
        // Attach drag-drop events
        AddHandler(DragDrop.DragEnterEvent, OnTabDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnTabDragLeave);
        AddHandler(DragDrop.DropEvent, OnTabDrop);

        DataContextChanged += OnDataContextChanged;
        
        // Initialize type-ahead timer
        _typeAheadClearTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_typeAheadTimeoutMs)
        };
        _typeAheadClearTimer.Tick += (_, _) =>
        {
            _typeAheadBuffer = string.Empty;
            TypeAheadBuffer = string.Empty;
            _typeAheadClearTimer.Stop();
        };
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is TabbedPanelViewModel vm && vm.ActiveTab?.SelectedItem != null)
        {
            if (vm.Settings.SingleClickOpen)
                return;
            vm.ActiveTab.OpenItemCommand.Execute(vm.ActiveTab.SelectedItem);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not TabbedPanelViewModel vm)
            return;

        if (_panelSettings != null)
        {
            _panelSettings.PropertyChanged -= OnSettingsChanged;
        }

        _panelSettings = vm.Settings;
        _panelSettings.PropertyChanged += OnSettingsChanged;
        UpdateTypeAheadSettings(_panelSettings);

        vm.PropertyChanged -= OnPanelPropertyChanged;
        vm.PropertyChanged += OnPanelPropertyChanged;
        HookActiveTab(vm.ActiveTab);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_panelSettings == null)
            return;

        if (e.PropertyName == nameof(AppSettings.QuickSearchTimeoutMs))
        {
            UpdateTypeAheadSettings(_panelSettings);
            return;
        }

        if (e.PropertyName == nameof(AppSettings.QuickSearchEnabled) && !_panelSettings.QuickSearchEnabled)
        {
            ClearTypeAheadBuffer();
        }
    }

    private void UpdateTypeAheadSettings(AppSettings settings)
    {
        var timeout = settings.QuickSearchTimeoutMs;
        if (timeout <= 0)
            timeout = 1000;

        _typeAheadTimeoutMs = timeout;
        if (_typeAheadClearTimer != null)
        {
            _typeAheadClearTimer.Interval = TimeSpan.FromMilliseconds(_typeAheadTimeoutMs);
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TabbedPanelViewModel vm)
            return;

        if (e.PropertyName == nameof(TabbedPanelViewModel.ActiveTab))
        {
            HookActiveTab(vm.ActiveTab);
        }
    }

    private void HookActiveTab(TabViewModel? tab)
    {
        if (_thumbnailTab != null)
            _thumbnailTab.PropertyChanged -= OnActiveTabPropertyChanged;

        _thumbnailTab = tab;

        if (_thumbnailTab != null)
        {
            _thumbnailTab.PropertyChanged += OnActiveTabPropertyChanged;
            if (_thumbnailTab.ViewMode == FilePanelViewMode.Thumbnails)
            {
                LoadVisibleThumbnails(_thumbnailTab);
            }
        }
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TabViewModel tab)
            return;

        if (e.PropertyName == nameof(TabViewModel.ViewMode) &&
            tab.ViewMode == FilePanelViewMode.Thumbnails)
        {
            LoadVisibleThumbnails(tab);
        }
    }
    
    /// <summary>
    /// Handles text input for type-ahead file navigation (TC-style: just start typing).
    /// </summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        
        if (e.Handled || string.IsNullOrWhiteSpace(e.Text))
            return;
        
        if (DataContext is not TabbedPanelViewModel vm || vm.ActiveTab == null)
            return;

        if (!vm.Settings.QuickSearchEnabled)
            return;
        
        // Don't handle when editing text or quick filter is visible.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox || QuickFilterBar?.IsVisible == true)
            return;
        
        // Add character to type-ahead buffer
        _typeAheadBuffer += e.Text;
        _lastTypeAheadTime = DateTime.Now;
        TypeAheadBuffer = _typeAheadBuffer;
        
        // Reset the clear timer
        _typeAheadClearTimer?.Stop();
        _typeAheadClearTimer?.Start();
        
        // Find and jump to matching file
        JumpToMatchingFile(vm.ActiveTab);
        
        e.Handled = true;
    }
    
    /// <summary>
    /// Jumps to the first file matching the type-ahead buffer.
    /// </summary>
    private void JumpToMatchingFile(TabViewModel tab)
    {
        if (string.IsNullOrEmpty(_typeAheadBuffer) || tab.Items.Count == 0)
            return;

        var mode = GetQuickSearchMatchMode(_panelSettings);
        FileItemViewModel? match = null;
        if (mode == QuickSearchMatchMode.StartsWith || mode == QuickSearchMatchMode.StartsWithThenContains)
        {
            match = tab.Items.FirstOrDefault(item =>
                item.ItemType != FileSystemItemType.ParentDirectory &&
                item.Name.StartsWith(_typeAheadBuffer, StringComparison.OrdinalIgnoreCase));
        }

        if (match == null && (mode == QuickSearchMatchMode.Contains || mode == QuickSearchMatchMode.StartsWithThenContains))
        {
            match = tab.Items.FirstOrDefault(item =>
                item.ItemType != FileSystemItemType.ParentDirectory &&
                item.Name.Contains(_typeAheadBuffer, StringComparison.OrdinalIgnoreCase));
        }

        if (match != null)
        {
            tab.SelectedItem = match;
            // Scroll into view will be handled by the DataGrid binding
        }
    }

    private static QuickSearchMatchMode GetQuickSearchMatchMode(AppSettings? settings)
    {
        var mode = settings?.QuickSearchMatchMode;
        if (string.Equals(mode, "Contains", StringComparison.OrdinalIgnoreCase))
            return QuickSearchMatchMode.Contains;
        if (string.Equals(mode, "StartsWith", StringComparison.OrdinalIgnoreCase))
            return QuickSearchMatchMode.StartsWith;

        return QuickSearchMatchMode.StartsWithThenContains;
    }
    
    /// <summary>
    /// Clears the type-ahead buffer (e.g., on navigation or Escape).
    /// </summary>
    public void ClearTypeAheadBuffer()
    {
        _typeAheadBuffer = string.Empty;
        TypeAheadBuffer = string.Empty;
        _typeAheadClearTimer?.Stop();
    }
    
    /// <summary>
    /// Gets the current type-ahead search string for display in status bar.
    /// </summary>
    public string GetTypeAheadBuffer() => _typeAheadBuffer;

    public void ToggleQuickFilter()
    {
        QuickFilterBar?.Toggle();
    }

    public void ShowQuickFilter()
    {
        QuickFilterBar?.Show();
    }

    public void HideQuickFilter()
    {
        QuickFilterBar?.Hide();
    }

    public void FocusDriveBar()
    {
        if (DriveBarItems == null)
            return;

        var button = DriveBarItems.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.IsVisible && b.IsEnabled);

        button?.Focus();
    }
    
    /// <summary>
    /// Handles keyboard input on the DataGrid for Total Commander compatible shortcuts.
    /// </summary>
    private void OnDataGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;
            
        if (DataContext is not TabbedPanelViewModel vm || vm.ActiveTab == null)
            return;
            
        var tab = vm.ActiveTab;
        
        switch (e.Key)
        {
            case Key.Enter:
                // Open selected item (navigate to folder or open file)
                if (tab.SelectedItem != null)
                {
                    tab.OpenItemCommand.Execute(tab.SelectedItem);
                    e.Handled = true;
                }
                break;
                
            case Key.Space:
                // Toggle selection on current item
                if (tab.SelectedItem != null)
                {
                    tab.ToggleItemSelection(tab.SelectedItem);
                    e.Handled = true;
                }
                break;
                
            case Key.Insert:
                // Select current item and move to next
                if (tab.SelectedItem != null)
                {
                    tab.SelectItemAndMoveNext();
                    e.Handled = true;
                }
                break;
                
            case Key.Back:
                // Go to parent directory (Backspace)
                tab.GoToParentCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Home:
                // Go to first item (or with Shift, select from current to first)
                if (tab.Items.Count > 0)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        tab.SelectRangeToFirst();
                    }
                    else
                    {
                        tab.SelectedItem = tab.Items[0];
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.End:
                // Go to last item (or with Shift, select from current to last)
                if (tab.Items.Count > 0)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        tab.SelectRangeToLast();
                    }
                    else
                    {
                        tab.SelectedItem = tab.Items[^1];
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.PageUp:
                // Navigate to parent directory with Ctrl+PageUp (TC compatible)
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    tab.GoToParentCommand.Execute(null);
                    e.Handled = true;
                }
                break;
                
            case Key.PageDown:
                // Enter directory with Ctrl+PageDown (TC compatible)
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    if (tab.SelectedItem != null && tab.SelectedItem.IsDirectory)
                    {
                        tab.OpenItemCommand.Execute(tab.SelectedItem);
                        e.Handled = true;
                    }
                }
                break;
                
            case Key.Escape:
                if (QuickFilterBar?.IsVisible == true)
                {
                    QuickFilterBar.Hide();
                    e.Handled = true;
                    break;
                }

                // Clear type-ahead buffer on Escape
                if (!string.IsNullOrEmpty(_typeAheadBuffer))
                {
                    ClearTypeAheadBuffer();
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TabbedPanelViewModel vm || vm.ActiveTab == null)
            return;

        if (!vm.Settings.SingleClickOpen)
            return;

        if (e.ClickCount != 1)
            return;

        if (e.KeyModifiers != KeyModifiers.None)
            return;

        var point = e.GetCurrentPoint(sender as Control);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Control control)
        {
            var row = control.FindAncestorOfType<DataGridRow>();
            if (row?.DataContext is FileItemViewModel file)
            {
                vm.ActiveTab.SelectedItem = file;
                vm.ActiveTab.OpenItemCommand.Execute(file);
                e.Handled = true;
            }
        }
    }

    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FileItemViewModel fileVm)
            return;

        if (DataContext is not TabbedPanelViewModel vm || vm.ActiveTab == null)
            return;

        var point = e.GetCurrentPoint(border);

        if (point.Properties.IsRightButtonPressed)
        {
            vm.ActiveTab.SelectedItem = fileVm;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            vm.ActiveTab.SelectedItem = fileVm;
            if (vm.Settings.SingleClickOpen)
            {
                vm.ActiveTab.OpenItemCommand.Execute(fileVm);
                e.Handled = true;
            }
        }

        var mainWindow = this.FindAncestorOfType<MainWindow>();
        if (mainWindow?.DataContext is MainWindowViewModel mainVm)
        {
            mainVm.SetActivePanelCommand.Execute(vm);
        }
    }

    private void OnThumbnailDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not TabbedPanelViewModel vm || vm.ActiveTab == null)
            return;

        if (vm.Settings.SingleClickOpen)
            return;

        if (sender is Border border && border.DataContext is FileItemViewModel fileVm)
        {
            vm.ActiveTab.OpenItemCommand.Execute(fileVm);
        }
    }

    private async void LoadVisibleThumbnails(TabViewModel tab)
    {
        foreach (var item in tab.Items.Where(i => i.CanHaveThumbnail && !i.ThumbnailLoaded))
        {
            await item.LoadThumbnailAsync();
        }
    }

    private void OnPanelGotFocus(object? sender, GotFocusEventArgs e)
    {
        // Notify parent to set this panel as active
        if (DataContext is TabbedPanelViewModel vm)
        {
            var mainWindow = this.FindAncestorOfType<MainWindow>();
            if (mainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.SetActivePanelCommand.Execute(vm);
            }
        }
    }
    
#pragma warning disable CS0618 // Type or member is obsolete
    private async void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: TabViewModel tab } border)
            return;
            
        // Only start drag on left mouse button
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            return;
            
        _draggedTab = tab;
        _sourcePanel = DataContext as TabbedPanelViewModel;
        
        var data = new DataObject();
        data.Set(TabDragDataFormat, tab);
        
        // Start drag operation
        var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        
        _draggedTab = null;
        _sourcePanel = null;
    }
    
    private void OnTabDragEnter(object? sender, DragEventArgs e)
    {
        // Find the target tab border
        var border = (e.Source as Visual)?.FindAncestorOfType<Border>();
        if (border?.Tag is not TabViewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        
        // Accept tab drag or file drag
        if (e.Data.Contains(TabDragDataFormat))
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else if (e.Data.Contains(DataFormats.Files))
        {
            // Check keyboard modifiers for copy vs move
            e.DragEffects = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? DragDropEffects.Move
                : DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        
        // Visual feedback - add highlighting
        border.BorderBrush = Avalonia.Media.Brushes.DeepSkyBlue;
        border.BorderThickness = new Avalonia.Thickness(2, 0, 0, 0);
    }
    
    private void OnTabDragLeave(object? sender, DragEventArgs e)
    {
        // Find the target tab border
        var border = (e.Source as Visual)?.FindAncestorOfType<Border>();
        if (border == null)
            return;
            
        // Remove visual feedback
        border.BorderBrush = null;
        border.BorderThickness = new Avalonia.Thickness(0);
    }
    
    private async void OnTabDrop(object? sender, DragEventArgs e)
    {
        // Find the target tab border
        var border = (e.Source as Visual)?.FindAncestorOfType<Border>();
        if (border == null)
            return;
            
        // Remove visual feedback
        border.BorderBrush = null;
        border.BorderThickness = new Avalonia.Thickness(0);
        
        // Handle file drop on tab
        if (e.Data.Contains(DataFormats.Files))
        {
            HandleFileDropOnTabAsync(border, e);
            return;
        }
        
        if (!e.Data.Contains(TabDragDataFormat))
            return;
            
        var draggedTab = e.Data.Get(TabDragDataFormat) as TabViewModel;
        if (draggedTab == null)
            return;
            
        var targetTab = border.Tag as TabViewModel;
        if (targetTab == null)
            return;
            
        var targetPanel = DataContext as TabbedPanelViewModel;
        if (targetPanel == null)
            return;
            
        var targetIndex = targetPanel.Tabs.IndexOf(targetTab);
        
        // Check if this is a cross-panel move
        if (_sourcePanel != null && _sourcePanel != targetPanel)
        {
            // Cross-panel move
            if (_sourcePanel.DetachTab(draggedTab))
            {
                targetPanel.AcceptTab(draggedTab, targetIndex);
            }
        }
        else
        {
            // Same panel reorder
            var sourceIndex = targetPanel.Tabs.IndexOf(draggedTab);
            if (sourceIndex != targetIndex && sourceIndex >= 0)
            {
                targetPanel.MoveTabTo(draggedTab, targetIndex);
            }
        }
    }
    
    /// <summary>
    /// Handles file drop on a tab header - copies or moves files to that tab's directory.
    /// </summary>
    private void HandleFileDropOnTabAsync(Border border, DragEventArgs e)
    {
        var targetTab = border.Tag as TabViewModel;
        if (targetTab == null)
            return;
            
        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0)
            return;
            
        var targetPath = targetTab.CurrentPath;
        if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            return;
            
        var isMoveOperation = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var sourcePaths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
            
        if (sourcePaths.Count == 0)
            return;
        
        try
        {
            foreach (var sourcePath in sourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetPath, fileName);
                
                if (File.Exists(sourcePath))
                {
                    if (isMoveOperation)
                        File.Move(sourcePath, destPath, overwrite: true);
                    else
                        File.Copy(sourcePath, destPath, overwrite: true);
                }
                else if (Directory.Exists(sourcePath))
                {
                    if (isMoveOperation)
                        Directory.Move(sourcePath, destPath);
                    else
                        CopyDirectory(sourcePath, destPath);
                }
            }
            
            // Refresh the target tab
            targetTab.RefreshCommand?.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File drop failed: {ex.Message}");
        }
    }
    
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
