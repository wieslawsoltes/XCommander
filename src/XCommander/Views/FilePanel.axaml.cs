using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XCommander.Controls;
using XCommander.Models;
using XCommander.ViewModels;

namespace XCommander.Views;

public partial class FilePanel : UserControl
{
    // Gesture tracking
    private Point? _swipeStartPoint;
    private const double SwipeThreshold = 100; // Minimum swipe distance
    private const double SwipeAngleThreshold = 30; // Max degrees from horizontal for swipe
    
    // Rubber band selection tracking
    private bool _isRubberBandActive;
    private Point _rubberBandStart;
    private readonly HashSet<FileItemViewModel> _rubberBandPreSelection = new();
    
    public FilePanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        
        // Add gesture handlers for swipe navigation
        PointerPressed += OnSwipeStart;
        PointerReleased += OnSwipeEnd;
        PointerMoved += OnPointerMoved;
    }
    
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isRubberBandActive)
            return;
            
        var position = e.GetPosition(this);
        RubberBandAdorner.Update(position);
        
        // Update selection based on rubber band
        UpdateRubberBandSelection(position);
    }
    
    private void OnSwipeStart(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        
        // Only track touch/pen for swipe gestures
        if (e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen)
        {
            _swipeStartPoint = e.GetPosition(this);
        }
        
        // Start rubber band selection on left-click in empty area (with Ctrl held for additive selection)
        if (point.Properties.IsLeftButtonPressed && 
            e.Source is Control control && 
            !(control.FindAncestorOfType<DataGridRow>() != null || control.FindAncestorOfType<Border>()?.DataContext is FileItemViewModel))
        {
            StartRubberBandSelection(e.GetPosition(this), e.KeyModifiers.HasFlag(KeyModifiers.Control));
            e.Handled = true;
        }
    }
    
    private void OnSwipeEnd(object? sender, PointerReleasedEventArgs e)
    {
        // End rubber band selection
        if (_isRubberBandActive)
        {
            EndRubberBandSelection();
        }
        
        if (_swipeStartPoint == null)
            return;
            
        if (e.Pointer.Type != PointerType.Touch && e.Pointer.Type != PointerType.Pen)
            return;
            
        var endPoint = e.GetPosition(this);
        var deltaX = endPoint.X - _swipeStartPoint.Value.X;
        var deltaY = endPoint.Y - _swipeStartPoint.Value.Y;
        
        // Check if this is a horizontal swipe (within angle threshold)
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance < SwipeThreshold)
        {
            _swipeStartPoint = null;
            return;
        }
        
        var angle = Math.Abs(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);
        var isHorizontal = angle < SwipeAngleThreshold || angle > (180 - SwipeAngleThreshold);
        
        if (isHorizontal && DataContext is FilePanelViewModel vm)
        {
            if (deltaX > SwipeThreshold)
            {
                // Swipe right = go back
                vm.GoBackCommand.Execute(null);
            }
            else if (deltaX < -SwipeThreshold)
            {
                // Swipe left = go forward
                vm.GoForwardCommand.Execute(null);
            }
        }
        
        _swipeStartPoint = null;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is FilePanelViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(FilePanelViewModel.ViewMode))
                {
                    if (vm.ViewMode == FilePanelViewMode.Thumbnails)
                    {
                        LoadVisibleThumbnails();
                    }
                }
            };
        }
    }
    
    /// <summary>
    /// Handles keyboard input at the UserControl level.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        HandleKeyDown(e);
    }
    
    /// <summary>
    /// Handles keyboard input directly on the DataGrid to ensure Enter key works.
    /// This is needed because DataGrid may consume certain keys before they bubble up.
    /// </summary>
    private void OnDataGridKeyDown(object? sender, KeyEventArgs e)
    {
        HandleKeyDown(e);
    }
    
    /// <summary>
    /// Common keyboard handling logic for all views.
    /// Total Commander keyboard shortcuts compatibility.
    /// </summary>
    private void HandleKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
            return;
            
        if (DataContext is not FilePanelViewModel vm)
            return;
            
        switch (e.Key)
        {
            case Key.Space:
                // Toggle selection on current item
                if (vm.SelectedItem != null)
                {
                    vm.ToggleItemSelection(vm.SelectedItem);
                    e.Handled = true;
                }
                break;
                
            case Key.Insert:
                // Select current item and move to next
                if (vm.SelectedItem != null)
                {
                    vm.SelectItemAndMoveNext();
                    e.Handled = true;
                }
                break;
                
            case Key.Enter:
                // Open selected item (navigate to folder or open file)
                if (vm.SelectedItem != null)
                {
                    vm.OpenItemCommand.Execute(vm.SelectedItem);
                    e.Handled = true;
                }
                break;
                
            case Key.Back:
                // Go to parent directory (Backspace)
                vm.GoToParentCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Home:
                // Go to first item (or with Ctrl, select from current to first)
                if (vm.Items.Count > 0)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        // Select from current to first
                        vm.SelectRangeToFirst();
                    }
                    else
                    {
                        vm.SelectedItem = vm.Items[0];
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.End:
                // Go to last item (or with Shift, select from current to last)
                if (vm.Items.Count > 0)
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        // Select from current to last
                        vm.SelectRangeToLast();
                    }
                    else
                    {
                        vm.SelectedItem = vm.Items[^1];
                    }
                    e.Handled = true;
                }
                break;
                
            case Key.PageUp:
                // Navigate to parent directory with Ctrl+PageUp (TC compatible)
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    vm.GoToParentCommand.Execute(null);
                    e.Handled = true;
                }
                break;
                
            case Key.PageDown:
                // Enter directory with Ctrl+PageDown (TC compatible)
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    if (vm.SelectedItem != null && vm.SelectedItem.IsDirectory)
                    {
                        vm.OpenItemCommand.Execute(vm.SelectedItem);
                        e.Handled = true;
                    }
                }
                break;
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm && vm.SelectedItem != null)
        {
            vm.OpenItemCommand.Execute(vm.SelectedItem);
        }
    }

    private void OnPanelGotFocus(object? sender, GotFocusEventArgs e)
    {
        // Notify parent to set this panel as active
        if (DataContext is FilePanelViewModel vm)
        {
            var mainWindow = this.FindAncestorOfType<MainWindow>();
            if (mainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.SetActivePanelCommand.Execute(vm);
            }
        }
    }
    
    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        
        // Right-click: Select item under cursor before showing context menu
        if (point.Properties.IsRightButtonPressed)
        {
            if (DataContext is FilePanelViewModel panelVm)
            {
                // Find the item under the pointer
                var dataGrid = sender as DataGrid;
                if (dataGrid != null)
                {
                    var visual = dataGrid.InputHitTest(point.Position);
                    if (visual is Control control)
                    {
                        var row = control.FindAncestorOfType<DataGridRow>();
                        if (row?.DataContext is FileItemViewModel fileVm)
                        {
                            // If item is not already selected, select only this one
                            if (!fileVm.IsSelected)
                            {
                                panelVm.DeselectAllCommand.Execute(null);
                                panelVm.SelectedItem = fileVm;
                                fileVm.IsSelected = true;
                                if (!panelVm.SelectedItems.Contains(fileVm))
                                    panelVm.SelectedItems.Add(fileVm);
                            }
                        }
                    }
                }
                
                // Set focus to this panel
                OnPanelGotFocus(sender, new GotFocusEventArgs());
            }
        }
        
        // Middle-click: Open in new tab
        if (point.Properties.IsMiddleButtonPressed)
        {
            if (DataContext is FilePanelViewModel panelVm)
            {
                var dataGrid = sender as DataGrid;
                if (dataGrid != null)
                {
                    var visual = dataGrid.InputHitTest(point.Position);
                    if (visual is Control control)
                    {
                        var row = control.FindAncestorOfType<DataGridRow>();
                        if (row?.DataContext is FileItemViewModel fileVm && fileVm.IsDirectory)
                        {
                            panelVm.OpenInNewTabCommand.Execute(fileVm.FullPath);
                            e.Handled = true;
                        }
                    }
                }
            }
        }
    }
    
    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        
        if (sender is Border border && border.DataContext is FileItemViewModel fileVm)
        {
            if (DataContext is FilePanelViewModel panelVm)
            {
                // Right-click: Select and show context menu
                if (point.Properties.IsRightButtonPressed)
                {
                    if (!fileVm.IsSelected)
                    {
                        panelVm.DeselectAllCommand.Execute(null);
                        panelVm.SelectedItem = fileVm;
                        fileVm.IsSelected = true;
                        if (!panelVm.SelectedItems.Contains(fileVm))
                            panelVm.SelectedItems.Add(fileVm);
                    }
                }
                // Left-click: Normal selection
                else if (point.Properties.IsLeftButtonPressed)
                {
                    panelVm.SelectedItem = fileVm;
                }
                // Middle-click: Open in new tab
                else if (point.Properties.IsMiddleButtonPressed && fileVm.IsDirectory)
                {
                    panelVm.OpenInNewTabCommand.Execute(fileVm.FullPath);
                    e.Handled = true;
                }
                
                // Set focus
                OnPanelGotFocus(sender, new GotFocusEventArgs());
            }
        }
    }
    
    private void OnThumbnailDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is FileItemViewModel fileVm)
        {
            if (DataContext is FilePanelViewModel panelVm)
            {
                panelVm.OpenItemCommand.Execute(fileVm);
            }
        }
    }
    
    private async void LoadVisibleThumbnails()
    {
        if (DataContext is not FilePanelViewModel vm)
            return;
            
        // Load thumbnails for image and video files
        foreach (var item in vm.Items.Where(i => i.CanHaveThumbnail && !i.ThumbnailLoaded))
        {
            await item.LoadThumbnailAsync();
        }
    }
    
    #region Rubber Band Selection
    
    /// <summary>
    /// Starts rubber band selection.
    /// </summary>
    private void StartRubberBandSelection(Point startPoint, bool additive)
    {
        _isRubberBandActive = true;
        _rubberBandStart = startPoint;
        _rubberBandPreSelection.Clear();
        
        // Store current selection for additive mode
        if (additive && DataContext is FilePanelViewModel vm)
        {
            foreach (var item in vm.SelectedItems)
            {
                _rubberBandPreSelection.Add(item);
            }
        }
        else if (DataContext is FilePanelViewModel panelVm)
        {
            // Clear selection if not additive
            panelVm.DeselectAllCommand.Execute(null);
        }
        
        RubberBandAdorner.Start(startPoint);
    }
    
    /// <summary>
    /// Updates selection based on rubber band rectangle.
    /// </summary>
    private void UpdateRubberBandSelection(Point currentPoint)
    {
        if (DataContext is not FilePanelViewModel vm)
            return;
            
        var selectionRect = RubberBandAdorner.SelectionRect;
        
        // Find items that intersect with the rubber band
        foreach (var item in vm.Items)
        {
            if (item.ItemType == FileSystemItemType.ParentDirectory)
                continue;
                
            // Get the visual position of the item (simplified - ideally we'd get the actual row bounds)
            var itemIndex = vm.Items.IndexOf(item);
            var itemRect = GetItemBounds(itemIndex);
            
            bool shouldBeSelected = selectionRect.Intersects(itemRect) || _rubberBandPreSelection.Contains(item);
            
            if (item.IsSelected != shouldBeSelected)
            {
                item.IsSelected = shouldBeSelected;
                
                if (shouldBeSelected && !vm.SelectedItems.Contains(item))
                {
                    vm.SelectedItems.Add(item);
                }
                else if (!shouldBeSelected && vm.SelectedItems.Contains(item))
                {
                    vm.SelectedItems.Remove(item);
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the approximate bounds of an item by index.
    /// </summary>
    private Rect GetItemBounds(int index)
    {
        // Get the DataGrid's row height (approximate)
        const double rowHeight = 24;
        const double headerHeight = 32;
        const double topOffset = 80; // Drive bar + path bar
        
        var y = topOffset + headerHeight + (index * rowHeight);
        return new Rect(0, y, Bounds.Width, rowHeight);
    }
    
    /// <summary>
    /// Ends rubber band selection.
    /// </summary>
    private void EndRubberBandSelection()
    {
        _isRubberBandActive = false;
        RubberBandAdorner.End();
        _rubberBandPreSelection.Clear();
        
        // Release pointer capture
        // Note: In Avalonia, we don't need to explicitly release in most cases
    }
    
    #endregion
}
