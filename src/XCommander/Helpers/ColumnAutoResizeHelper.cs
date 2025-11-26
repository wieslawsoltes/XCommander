using Avalonia.Controls;
using Avalonia.Input;

namespace XCommander.Helpers;

/// <summary>
/// Helper for DataGrid column auto-resize functionality.
/// </summary>
public static class ColumnAutoResizeHelper
{
    /// <summary>
    /// Attaches double-click handlers to DataGrid column headers for auto-resize.
    /// </summary>
    public static void AttachAutoResize(DataGrid dataGrid)
    {
        dataGrid.Loaded += (s, e) =>
        {
            // Find column headers and attach handlers
            AttachHandlersToHeaders(dataGrid);
        };
    }
    
    private static void AttachHandlersToHeaders(DataGrid dataGrid)
    {
        // This would need more complex implementation to find the column separators
        // For now, we'll use a simplified approach via KeyBindings
    }
    
    /// <summary>
    /// Auto-resizes a column to fit its content.
    /// </summary>
    public static void AutoResizeColumn(DataGridColumn column, DataGrid dataGrid)
    {
        if (column == null || dataGrid.ItemsSource == null)
            return;
            
        // Reset to auto-size
        column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        
        // Force measure and arrange
        dataGrid.InvalidateMeasure();
        dataGrid.InvalidateArrange();
    }
    
    /// <summary>
    /// Auto-resizes all columns to fit their content.
    /// </summary>
    public static void AutoResizeAllColumns(DataGrid dataGrid)
    {
        if (dataGrid.Columns == null)
            return;
            
        foreach (var column in dataGrid.Columns)
        {
            AutoResizeColumn(column, dataGrid);
        }
    }
}
