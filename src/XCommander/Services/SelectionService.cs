using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Result of a pattern-based selection operation
/// </summary>
public class SelectionResult
{
    public int MatchedCount { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<string> MatchedItems { get; init; } = Array.Empty<string>();
    public string Pattern { get; init; } = string.Empty;
}

/// <summary>
/// Stored selection for later recall
/// </summary>
public class StoredSelection
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public List<string> SelectedItems { get; set; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Comparison result between two panel selections
/// </summary>
public class SelectionComparison
{
    public IReadOnlyList<string> OnlyInLeft { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OnlyInRight { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InBoth { get; init; } = Array.Empty<string>();
    public int LeftCount => OnlyInLeft.Count + InBoth.Count;
    public int RightCount => OnlyInRight.Count + InBoth.Count;
}

/// <summary>
/// Service for advanced file selection operations
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Select items matching a wildcard pattern (e.g., *.txt, photo*.jpg)
    /// </summary>
    SelectionResult SelectByPattern(IEnumerable<string> allItems, string pattern, bool caseSensitive = false);
    
    /// <summary>
    /// Deselect items matching a wildcard pattern
    /// </summary>
    SelectionResult DeselectByPattern(IEnumerable<string> selectedItems, IEnumerable<string> allItems, string pattern, bool caseSensitive = false);
    
    /// <summary>
    /// Select items by file extension (e.g., .txt, .jpg)
    /// </summary>
    SelectionResult SelectByExtension(IEnumerable<string> allItems, string extension);
    
    /// <summary>
    /// Invert current selection
    /// </summary>
    IReadOnlyList<string> InvertSelection(IEnumerable<string> currentSelection, IEnumerable<string> allItems);
    
    /// <summary>
    /// Select items by regex pattern
    /// </summary>
    SelectionResult SelectByRegex(IEnumerable<string> allItems, string regexPattern, bool caseSensitive = false);
    
    /// <summary>
    /// Select items by size range
    /// </summary>
    SelectionResult SelectBySize(IEnumerable<string> allItems, Func<string, long> getSizeFunc, long? minSize = null, long? maxSize = null);
    
    /// <summary>
    /// Select items by date range
    /// </summary>
    SelectionResult SelectByDate(IEnumerable<string> allItems, Func<string, DateTime> getDateFunc, DateTime? after = null, DateTime? before = null);
    
    /// <summary>
    /// Select items by attributes
    /// </summary>
    SelectionResult SelectByAttributes(IEnumerable<string> allItems, Func<string, FileAttributes> getAttrsFunc, FileAttributes requiredAttributes);
    
    /// <summary>
    /// Store current selection for later recall
    /// </summary>
    void StoreSelection(string name, string directory, IEnumerable<string> selectedItems);
    
    /// <summary>
    /// Recall a stored selection
    /// </summary>
    StoredSelection? RecallSelection(string name);
    
    /// <summary>
    /// Get all stored selections
    /// </summary>
    IReadOnlyList<StoredSelection> GetStoredSelections();
    
    /// <summary>
    /// Delete a stored selection
    /// </summary>
    bool DeleteStoredSelection(string id);
    
    /// <summary>
    /// Clear all stored selections
    /// </summary>
    void ClearStoredSelections();
    
    /// <summary>
    /// Compare selections between two panels
    /// </summary>
    SelectionComparison CompareSelections(IEnumerable<string> leftSelection, IEnumerable<string> rightSelection);
    
    /// <summary>
    /// Select items that exist in both panels
    /// </summary>
    IReadOnlyList<string> SelectCommonItems(IEnumerable<string> leftItems, IEnumerable<string> rightItems);
    
    /// <summary>
    /// Select items unique to one panel
    /// </summary>
    IReadOnlyList<string> SelectUniqueItems(IEnumerable<string> sourceItems, IEnumerable<string> compareItems);
    
    /// <summary>
    /// Parse a selection pattern (supports wildcards and extensions)
    /// </summary>
    string PatternToRegex(string wildcardPattern);
    
    /// <summary>
    /// Push current selection onto history stack for undo support
    /// </summary>
    void PushSelectionHistory(string panelId, IEnumerable<string> selection);
    
    /// <summary>
    /// Undo last selection change, returns previous selection or null if no history
    /// </summary>
    IReadOnlyList<string>? UndoSelection(string panelId);
    
    /// <summary>
    /// Redo previously undone selection change
    /// </summary>
    IReadOnlyList<string>? RedoSelection(string panelId);
    
    /// <summary>
    /// Check if undo is available for panel
    /// </summary>
    bool CanUndo(string panelId);
    
    /// <summary>
    /// Check if redo is available for panel
    /// </summary>
    bool CanRedo(string panelId);
    
    /// <summary>
    /// Clear selection history for panel
    /// </summary>
    void ClearHistory(string panelId);
}

/// <summary>
/// Selection history entry for undo/redo support
/// </summary>
public class SelectionHistoryEntry
{
    public List<string> Selection { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Panel-specific selection history stack
/// </summary>
public class PanelSelectionHistory
{
    private readonly Stack<SelectionHistoryEntry> _undoStack = new();
    private readonly Stack<SelectionHistoryEntry> _redoStack = new();
    private const int MaxHistorySize = 50;
    
    public void Push(IEnumerable<string> selection)
    {
        // Clear redo stack on new action
        _redoStack.Clear();
        
        // Limit history size
        if (_undoStack.Count >= MaxHistorySize)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < temp.Length - 1; i++)
            {
                _undoStack.Push(temp[temp.Length - 1 - i]);
            }
        }
        
        _undoStack.Push(new SelectionHistoryEntry { Selection = selection.ToList() });
    }
    
    public IReadOnlyList<string>? Undo()
    {
        if (_undoStack.Count <= 1) return null; // Need at least 2 entries to undo
        
        var current = _undoStack.Pop();
        _redoStack.Push(current);
        
        return _undoStack.Peek().Selection;
    }
    
    public IReadOnlyList<string>? Redo()
    {
        if (_redoStack.Count == 0) return null;
        
        var entry = _redoStack.Pop();
        _undoStack.Push(entry);
        
        return entry.Selection;
    }
    
    public bool CanUndo => _undoStack.Count > 1;
    public bool CanRedo => _redoStack.Count > 0;
    
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

public class SelectionService : ISelectionService
{
    private readonly List<StoredSelection> _storedSelections = new();
    private readonly Dictionary<string, PanelSelectionHistory> _panelHistories = new();
    private readonly object _lock = new();
    
    public SelectionResult SelectByPattern(IEnumerable<string> allItems, string pattern, bool caseSensitive = false)
    {
        var items = allItems.ToList();
        var regexPattern = PatternToRegex(pattern);
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        var regex = new Regex($"^{regexPattern}$", options);
        
        var matched = items.Where(item => regex.IsMatch(Path.GetFileName(item))).ToList();
        
        return new SelectionResult
        {
            MatchedCount = matched.Count,
            TotalCount = items.Count,
            MatchedItems = matched,
            Pattern = pattern
        };
    }
    
    public SelectionResult DeselectByPattern(IEnumerable<string> selectedItems, IEnumerable<string> allItems, string pattern, bool caseSensitive = false)
    {
        var selected = selectedItems.ToList();
        var regexPattern = PatternToRegex(pattern);
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        var regex = new Regex($"^{regexPattern}$", options);
        
        var toDeselect = selected.Where(item => regex.IsMatch(Path.GetFileName(item))).ToList();
        var remaining = selected.Except(toDeselect).ToList();
        
        return new SelectionResult
        {
            MatchedCount = toDeselect.Count,
            TotalCount = selected.Count,
            MatchedItems = remaining,
            Pattern = pattern
        };
    }
    
    public SelectionResult SelectByExtension(IEnumerable<string> allItems, string extension)
    {
        var items = allItems.ToList();
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        
        var matched = items
            .Where(item => Path.GetExtension(item).Equals(ext, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        return new SelectionResult
        {
            MatchedCount = matched.Count,
            TotalCount = items.Count,
            MatchedItems = matched,
            Pattern = $"*{ext}"
        };
    }
    
    public IReadOnlyList<string> InvertSelection(IEnumerable<string> currentSelection, IEnumerable<string> allItems)
    {
        var selected = new HashSet<string>(currentSelection, StringComparer.OrdinalIgnoreCase);
        return allItems.Where(item => !selected.Contains(item)).ToList();
    }
    
    public SelectionResult SelectByRegex(IEnumerable<string> allItems, string regexPattern, bool caseSensitive = false)
    {
        var items = allItems.ToList();
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        
        try
        {
            var regex = new Regex(regexPattern, options);
            var matched = items.Where(item => regex.IsMatch(Path.GetFileName(item))).ToList();
            
            return new SelectionResult
            {
                MatchedCount = matched.Count,
                TotalCount = items.Count,
                MatchedItems = matched,
                Pattern = regexPattern
            };
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern
            return new SelectionResult
            {
                MatchedCount = 0,
                TotalCount = items.Count,
                MatchedItems = Array.Empty<string>(),
                Pattern = regexPattern
            };
        }
    }
    
    public SelectionResult SelectBySize(IEnumerable<string> allItems, Func<string, long> getSizeFunc, long? minSize = null, long? maxSize = null)
    {
        var items = allItems.ToList();
        var matched = new List<string>();
        
        foreach (var item in items)
        {
            try
            {
                var size = getSizeFunc(item);
                var meetsMin = !minSize.HasValue || size >= minSize.Value;
                var meetsMax = !maxSize.HasValue || size <= maxSize.Value;
                
                if (meetsMin && meetsMax)
                    matched.Add(item);
            }
            catch
            {
                // Skip items where size cannot be determined
            }
        }
        
        return new SelectionResult
        {
            MatchedCount = matched.Count,
            TotalCount = items.Count,
            MatchedItems = matched,
            Pattern = $"Size: {FormatSizeRange(minSize, maxSize)}"
        };
    }
    
    public SelectionResult SelectByDate(IEnumerable<string> allItems, Func<string, DateTime> getDateFunc, DateTime? after = null, DateTime? before = null)
    {
        var items = allItems.ToList();
        var matched = new List<string>();
        
        foreach (var item in items)
        {
            try
            {
                var date = getDateFunc(item);
                var meetsAfter = !after.HasValue || date >= after.Value;
                var meetsBefore = !before.HasValue || date <= before.Value;
                
                if (meetsAfter && meetsBefore)
                    matched.Add(item);
            }
            catch
            {
                // Skip items where date cannot be determined
            }
        }
        
        return new SelectionResult
        {
            MatchedCount = matched.Count,
            TotalCount = items.Count,
            MatchedItems = matched,
            Pattern = $"Date: {FormatDateRange(after, before)}"
        };
    }
    
    public SelectionResult SelectByAttributes(IEnumerable<string> allItems, Func<string, FileAttributes> getAttrsFunc, FileAttributes requiredAttributes)
    {
        var items = allItems.ToList();
        var matched = new List<string>();
        
        foreach (var item in items)
        {
            try
            {
                var attrs = getAttrsFunc(item);
                if ((attrs & requiredAttributes) == requiredAttributes)
                    matched.Add(item);
            }
            catch
            {
                // Skip items where attributes cannot be determined
            }
        }
        
        return new SelectionResult
        {
            MatchedCount = matched.Count,
            TotalCount = items.Count,
            MatchedItems = matched,
            Pattern = $"Attributes: {requiredAttributes}"
        };
    }
    
    public void StoreSelection(string name, string directory, IEnumerable<string> selectedItems)
    {
        lock (_lock)
        {
            // Remove existing with same name
            _storedSelections.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            _storedSelections.Add(new StoredSelection
            {
                Name = name,
                Directory = directory,
                SelectedItems = selectedItems.ToList()
            });
        }
    }
    
    public StoredSelection? RecallSelection(string name)
    {
        lock (_lock)
        {
            return _storedSelections.FirstOrDefault(s => 
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    public IReadOnlyList<StoredSelection> GetStoredSelections()
    {
        lock (_lock)
        {
            return _storedSelections.ToList();
        }
    }
    
    public bool DeleteStoredSelection(string id)
    {
        lock (_lock)
        {
            var selection = _storedSelections.FirstOrDefault(s => s.Id == id);
            if (selection != null)
            {
                _storedSelections.Remove(selection);
                return true;
            }
            return false;
        }
    }
    
    public void ClearStoredSelections()
    {
        lock (_lock)
        {
            _storedSelections.Clear();
        }
    }
    
    public SelectionComparison CompareSelections(IEnumerable<string> leftSelection, IEnumerable<string> rightSelection)
    {
        string GetName(string path) => Path.GetFileName(path) ?? string.Empty;

        var leftSet = new HashSet<string>(leftSelection.Select(GetName), StringComparer.OrdinalIgnoreCase);
        var rightSet = new HashSet<string>(rightSelection.Select(GetName), StringComparer.OrdinalIgnoreCase);
        
        var leftItems = leftSelection.ToList();
        var rightItems = rightSelection.ToList();
        
        var onlyInLeft = leftItems.Where(item => !rightSet.Contains(GetName(item))).ToList();
        var onlyInRight = rightItems.Where(item => !leftSet.Contains(GetName(item))).ToList();
        var inBoth = leftItems.Where(item => rightSet.Contains(GetName(item))).ToList();
        
        return new SelectionComparison
        {
            OnlyInLeft = onlyInLeft,
            OnlyInRight = onlyInRight,
            InBoth = inBoth
        };
    }
    
    public IReadOnlyList<string> SelectCommonItems(IEnumerable<string> leftItems, IEnumerable<string> rightItems)
    {
        string GetName(string path) => Path.GetFileName(path) ?? string.Empty;
        var rightNames = new HashSet<string>(rightItems.Select(GetName), StringComparer.OrdinalIgnoreCase);
        return leftItems.Where(item => rightNames.Contains(GetName(item))).ToList();
    }
    
    public IReadOnlyList<string> SelectUniqueItems(IEnumerable<string> sourceItems, IEnumerable<string> compareItems)
    {
        string GetName(string path) => Path.GetFileName(path) ?? string.Empty;
        var compareNames = new HashSet<string>(compareItems.Select(GetName), StringComparer.OrdinalIgnoreCase);
        return sourceItems.Where(item => !compareNames.Contains(GetName(item))).ToList();
    }
    
    public string PatternToRegex(string wildcardPattern)
    {
        // Escape regex special characters except * and ?
        var escaped = Regex.Escape(wildcardPattern);
        
        // Convert wildcards to regex
        // * matches any number of characters
        // ? matches exactly one character
        var regex = escaped
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        
        return regex;
    }
    
    private static string FormatSizeRange(long? min, long? max)
    {
        if (min.HasValue && max.HasValue)
            return $"{FormatSize(min.Value)} - {FormatSize(max.Value)}";
        if (min.HasValue)
            return $">= {FormatSize(min.Value)}";
        if (max.HasValue)
            return $"<= {FormatSize(max.Value)}";
        return "Any";
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {suffixes[order]}";
    }
    
    private static string FormatDateRange(DateTime? after, DateTime? before)
    {
        if (after.HasValue && before.HasValue)
            return $"{after.Value:d} - {before.Value:d}";
        if (after.HasValue)
            return $">= {after.Value:d}";
        if (before.HasValue)
            return $"<= {before.Value:d}";
        return "Any";
    }
    
    // Selection history undo/redo methods
    
    private PanelSelectionHistory GetOrCreateHistory(string panelId)
    {
        lock (_lock)
        {
            if (!_panelHistories.TryGetValue(panelId, out var history))
            {
                history = new PanelSelectionHistory();
                _panelHistories[panelId] = history;
            }
            return history;
        }
    }
    
    public void PushSelectionHistory(string panelId, IEnumerable<string> selection)
    {
        var history = GetOrCreateHistory(panelId);
        history.Push(selection);
    }
    
    public IReadOnlyList<string>? UndoSelection(string panelId)
    {
        var history = GetOrCreateHistory(panelId);
        return history.Undo();
    }
    
    public IReadOnlyList<string>? RedoSelection(string panelId)
    {
        var history = GetOrCreateHistory(panelId);
        return history.Redo();
    }
    
    public bool CanUndo(string panelId)
    {
        var history = GetOrCreateHistory(panelId);
        return history.CanUndo;
    }
    
    public bool CanRedo(string panelId)
    {
        var history = GetOrCreateHistory(panelId);
        return history.CanRedo;
    }
    
    public void ClearHistory(string panelId)
    {
        lock (_lock)
        {
            if (_panelHistories.TryGetValue(panelId, out var history))
            {
                history.Clear();
            }
        }
    }
}
