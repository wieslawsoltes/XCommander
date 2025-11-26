// ISelectionHistoryService.cs - Selection History Service
// Provides TC-style restore selection (NUM /) functionality

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for managing selection history and restoration (TC NUM / feature).
/// </summary>
public interface ISelectionHistoryService
{
    /// <summary>
    /// Saves the current selection state before an operation.
    /// </summary>
    void SaveSelection(string panelId, SelectionState state);
    
    /// <summary>
    /// Restores the previous selection (NUM / functionality).
    /// </summary>
    SelectionState? RestoreSelection(string panelId);
    
    /// <summary>
    /// Gets the selection history for a panel.
    /// </summary>
    IReadOnlyList<SelectionState> GetHistory(string panelId);
    
    /// <summary>
    /// Clears selection history for a panel.
    /// </summary>
    void ClearHistory(string panelId);
    
    /// <summary>
    /// Clears all selection history.
    /// </summary>
    void ClearAllHistory();
    
    /// <summary>
    /// Gets the maximum history depth.
    /// </summary>
    int MaxHistoryDepth { get; set; }
}

/// <summary>
/// Represents a saved selection state.
/// </summary>
public record SelectionState
{
    /// <summary>
    /// The directory path where the selection was made.
    /// </summary>
    public required string DirectoryPath { get; init; }
    
    /// <summary>
    /// The selected file/folder paths (relative to DirectoryPath).
    /// </summary>
    public required IReadOnlyList<string> SelectedItems { get; init; }
    
    /// <summary>
    /// When the selection was saved.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// The operation that triggered the save (Copy, Move, Delete, etc.).
    /// </summary>
    public SelectionSaveReason Reason { get; init; }
    
    /// <summary>
    /// Total size of selected items.
    /// </summary>
    public long TotalSize { get; init; }
    
    /// <summary>
    /// Number of files in selection.
    /// </summary>
    public int FileCount { get; init; }
    
    /// <summary>
    /// Number of folders in selection.
    /// </summary>
    public int FolderCount { get; init; }
}

/// <summary>
/// Reason for saving selection state.
/// </summary>
public enum SelectionSaveReason
{
    Manual,
    BeforeCopy,
    BeforeMove,
    BeforeDelete,
    BeforeRename,
    BeforeArchive,
    NavigationChange,
    FilterChange
}

/// <summary>
/// Implementation of selection history service.
/// </summary>
public class SelectionHistoryService : ISelectionHistoryService
{
    private readonly Dictionary<string, Stack<SelectionState>> _history = new();
    private readonly object _lock = new();
    
    public int MaxHistoryDepth { get; set; } = 10;
    
    public void SaveSelection(string panelId, SelectionState state)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(panelId, out var stack))
            {
                stack = new Stack<SelectionState>();
                _history[panelId] = stack;
            }
            
            // Don't save empty selections
            if (state.SelectedItems.Count == 0)
                return;
            
            // Don't save duplicate of top state
            if (stack.Count > 0)
            {
                var top = stack.Peek();
                if (top.DirectoryPath == state.DirectoryPath &&
                    top.SelectedItems.Count == state.SelectedItems.Count &&
                    top.SelectedItems.SequenceEqual(state.SelectedItems))
                {
                    return;
                }
            }
            
            stack.Push(state);
            
            // Trim excess history
            while (stack.Count > MaxHistoryDepth)
            {
                var temp = new Stack<SelectionState>();
                for (int i = 0; i < MaxHistoryDepth; i++)
                {
                    temp.Push(stack.Pop());
                }
                stack.Clear();
                while (temp.Count > 0)
                {
                    stack.Push(temp.Pop());
                }
            }
        }
    }
    
    public SelectionState? RestoreSelection(string panelId)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(panelId, out var stack) || stack.Count == 0)
                return null;
            
            return stack.Pop();
        }
    }
    
    public IReadOnlyList<SelectionState> GetHistory(string panelId)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(panelId, out var stack))
                return Array.Empty<SelectionState>();
            
            return stack.ToArray();
        }
    }
    
    public void ClearHistory(string panelId)
    {
        lock (_lock)
        {
            if (_history.TryGetValue(panelId, out var stack))
            {
                stack.Clear();
            }
        }
    }
    
    public void ClearAllHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }
}
