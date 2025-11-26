using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Represents a single rename operation for undo purposes.
/// </summary>
public class RenameOperation
{
    public string OldPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsDirectory { get; set; }
}

/// <summary>
/// Represents a batch of rename operations.
/// </summary>
public class RenameBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<RenameOperation> Operations { get; set; } = [];
    public bool IsUndone { get; set; }
}

/// <summary>
/// Manages rename history for undo functionality.
/// </summary>
public class RenameHistoryManager
{
    private readonly string _historyFilePath;
    private List<RenameBatch> _batches = [];
    private const int MaxHistoryBatches = 50;
    
    public RenameHistoryManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander");
        Directory.CreateDirectory(appDataPath);
        _historyFilePath = Path.Combine(appDataPath, "rename_history.json");
        LoadHistory();
    }
    
    public IReadOnlyList<RenameBatch> Batches => _batches.AsReadOnly();
    
    public IEnumerable<RenameBatch> UndoableBatches => _batches.Where(b => !b.IsUndone).OrderByDescending(b => b.Timestamp);
    
    public void AddBatch(RenameBatch batch)
    {
        _batches.Add(batch);
        
        // Limit history size
        while (_batches.Count > MaxHistoryBatches)
        {
            _batches.RemoveAt(0);
        }
        
        SaveHistory();
    }
    
    public void MarkAsUndone(string batchId)
    {
        var batch = _batches.FirstOrDefault(b => b.Id == batchId);
        if (batch != null)
        {
            batch.IsUndone = true;
            SaveHistory();
        }
    }
    
    public void ClearHistory()
    {
        _batches.Clear();
        SaveHistory();
    }
    
    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                _batches = JsonSerializer.Deserialize<List<RenameBatch>>(json) ?? [];
            }
        }
        catch
        {
            _batches = [];
        }
    }
    
    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_batches, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

public partial class MultiRenameViewModel : ViewModelBase
{
    private readonly RenameHistoryManager _historyManager = new();
    
    [ObservableProperty]
    private string _searchPattern = string.Empty;
    
    [ObservableProperty]
    private string _replacePattern = string.Empty;
    
    [ObservableProperty]
    private string _extensionPattern = string.Empty;
    
    [ObservableProperty]
    private string _counterStart = "1";
    
    [ObservableProperty]
    private string _counterStep = "1";
    
    [ObservableProperty]
    private string _counterDigits = "1";
    
    [ObservableProperty]
    private bool _useRegex;
    
    [ObservableProperty]
    private bool _caseSensitive;
    
    [ObservableProperty]
    private CaseTransform _nameCase = CaseTransform.None;
    
    [ObservableProperty]
    private CaseTransform _extensionCase = CaseTransform.None;
    
    public ObservableCollection<RenameItem> Items { get; } = [];
    
    // Placeholders available:
    // [N] - Original name (without extension)
    // [N1-5] - Characters 1-5 of original name
    // [E] - Original extension
    // [C] - Counter
    // [D] - Date (yyyyMMdd)
    // [T] - Time (HHmmss)
    // [Y] - Year
    // [M] - Month
    // [Dx] - Day
    // [H] - Hour
    // [m] - Minute
    // [s] - Second
    
    public MultiRenameViewModel()
    {
    }
    
    public void Initialize(IEnumerable<string> filePaths)
    {
        Items.Clear();
        foreach (var path in filePaths)
        {
            var item = new RenameItem
            {
                OriginalPath = path,
                OriginalName = Path.GetFileName(path),
                NewName = Path.GetFileName(path),
                Directory = Path.GetDirectoryName(path) ?? string.Empty
            };
            Items.Add(item);
        }
        UpdatePreview();
    }
    
    partial void OnSearchPatternChanged(string value) => UpdatePreview();
    partial void OnReplacePatternChanged(string value) => UpdatePreview();
    partial void OnExtensionPatternChanged(string value) => UpdatePreview();
    partial void OnCounterStartChanged(string value) => UpdatePreview();
    partial void OnCounterStepChanged(string value) => UpdatePreview();
    partial void OnCounterDigitsChanged(string value) => UpdatePreview();
    partial void OnUseRegexChanged(bool value) => UpdatePreview();
    partial void OnCaseSensitiveChanged(bool value) => UpdatePreview();
    partial void OnNameCaseChanged(CaseTransform value) => UpdatePreview();
    partial void OnExtensionCaseChanged(CaseTransform value) => UpdatePreview();
    
    [RelayCommand]
    public void UpdatePreview()
    {
        if (!int.TryParse(CounterStart, out int counter))
            counter = 1;
        if (!int.TryParse(CounterStep, out int step))
            step = 1;
        if (!int.TryParse(CounterDigits, out int digits))
            digits = 1;
            
        var now = DateTime.Now;
        
        foreach (var item in Items)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(item.OriginalName);
            var extension = Path.GetExtension(item.OriginalName);
            
            // Apply search/replace to name
            var newName = ApplySearchReplace(nameWithoutExt);
            
            // Apply replacement pattern with placeholders
            newName = ApplyPlaceholders(newName, nameWithoutExt, extension, counter, digits, now);
            
            // Apply case transformation to name
            newName = ApplyCaseTransform(newName, NameCase);
            
            // Handle extension
            var newExtension = extension;
            if (!string.IsNullOrEmpty(ExtensionPattern))
            {
                newExtension = ExtensionPattern.StartsWith(".") ? ExtensionPattern : "." + ExtensionPattern;
            }
            newExtension = ApplyCaseTransform(newExtension, ExtensionCase);
            
            item.NewName = newName + newExtension;
            item.HasConflict = Items.Any(i => i != item && 
                i.NewName.Equals(item.NewName, StringComparison.OrdinalIgnoreCase) &&
                i.Directory.Equals(item.Directory, StringComparison.OrdinalIgnoreCase));
            
            counter += step;
        }
    }
    
    private string ApplySearchReplace(string input)
    {
        if (string.IsNullOrEmpty(SearchPattern))
            return input;
            
        try
        {
            if (UseRegex)
            {
                var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.Replace(input, SearchPattern, ReplacePattern ?? string.Empty, options);
            }
            else
            {
                var comparison = CaseSensitive 
                    ? StringComparison.Ordinal 
                    : StringComparison.OrdinalIgnoreCase;
                    
                // Simple string replace (case insensitive requires manual handling)
                if (CaseSensitive)
                {
                    return input.Replace(SearchPattern, ReplacePattern ?? string.Empty);
                }
                else
                {
                    return Regex.Replace(input, Regex.Escape(SearchPattern), ReplacePattern ?? string.Empty, RegexOptions.IgnoreCase);
                }
            }
        }
        catch
        {
            return input;
        }
    }
    
    private string ApplyPlaceholders(string template, string originalName, string extension, int counter, int digits, DateTime now)
    {
        var result = template;
        
        // If no placeholders, return as-is
        if (!result.Contains('['))
            return result;
        
        // [N] - Full original name
        result = result.Replace("[N]", originalName);
        
        // [N1-5] - Substring of name
        result = Regex.Replace(result, @"\[N(\d+)-(\d+)\]", m =>
        {
            if (int.TryParse(m.Groups[1].Value, out int start) && 
                int.TryParse(m.Groups[2].Value, out int end))
            {
                start--; // Convert to 0-based
                if (start >= 0 && start < originalName.Length)
                {
                    var length = Math.Min(end - start, originalName.Length - start);
                    return originalName.Substring(start, length);
                }
            }
            return m.Value;
        });
        
        // [N1] - Single character from name
        result = Regex.Replace(result, @"\[N(\d+)\]", m =>
        {
            if (int.TryParse(m.Groups[1].Value, out int pos))
            {
                pos--; // Convert to 0-based
                if (pos >= 0 && pos < originalName.Length)
                {
                    return originalName[pos].ToString();
                }
            }
            return m.Value;
        });
        
        // [E] - Extension (without dot)
        result = result.Replace("[E]", extension.TrimStart('.'));
        
        // [C] - Counter
        result = result.Replace("[C]", counter.ToString().PadLeft(digits, '0'));
        
        // Date/time placeholders
        result = result.Replace("[D]", now.ToString("yyyyMMdd"));
        result = result.Replace("[T]", now.ToString("HHmmss"));
        result = result.Replace("[Y]", now.Year.ToString());
        result = result.Replace("[M]", now.Month.ToString("D2"));
        result = result.Replace("[Dx]", now.Day.ToString("D2"));
        result = result.Replace("[H]", now.Hour.ToString("D2"));
        result = result.Replace("[m]", now.Minute.ToString("D2"));
        result = result.Replace("[s]", now.Second.ToString("D2"));
        
        return result;
    }
    
    private static string ApplyCaseTransform(string input, CaseTransform transform)
    {
        return transform switch
        {
            CaseTransform.Lower => input.ToLowerInvariant(),
            CaseTransform.Upper => input.ToUpperInvariant(),
            CaseTransform.Title => ToTitleCase(input),
            CaseTransform.Sentence => ToSentenceCase(input),
            _ => input
        };
    }
    
    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
    
    private static string ToSentenceCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }
    
    [RelayCommand]
    public async Task ExecuteRenameAsync()
    {
        var errors = new List<string>();
        var batch = new RenameBatch
        {
            Timestamp = DateTime.Now,
            Description = $"Rename {Items.Count} items"
        };
        
        foreach (var item in Items.Where(i => i.OriginalName != i.NewName && !i.HasConflict))
        {
            try
            {
                var oldPath = item.OriginalPath;
                var newPath = Path.Combine(item.Directory, item.NewName);
                var isDirectory = Directory.Exists(oldPath);
                
                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                    batch.Operations.Add(new RenameOperation
                    {
                        OldPath = oldPath,
                        NewPath = newPath,
                        Timestamp = DateTime.Now,
                        IsDirectory = false
                    });
                    item.OriginalPath = newPath;
                    item.OriginalName = item.NewName;
                    item.IsRenamed = true;
                }
                else if (isDirectory)
                {
                    Directory.Move(oldPath, newPath);
                    batch.Operations.Add(new RenameOperation
                    {
                        OldPath = oldPath,
                        NewPath = newPath,
                        Timestamp = DateTime.Now,
                        IsDirectory = true
                    });
                    item.OriginalPath = newPath;
                    item.OriginalName = item.NewName;
                    item.IsRenamed = true;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.OriginalName}: {ex.Message}");
                item.HasError = true;
                item.ErrorMessage = ex.Message;
            }
        }
        
        // Save batch to history if any operations succeeded
        if (batch.Operations.Count > 0)
        {
            _historyManager.AddBatch(batch);
            LastBatchId = batch.Id;
            CanUndo = true;
        }
        
        if (errors.Count > 0)
        {
            // Errors will be visible in the UI through HasError property
        }
        
        await Task.CompletedTask;
    }
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoLastRenameCommand))]
    private bool _canUndo;
    
    [ObservableProperty]
    private string _lastBatchId = string.Empty;
    
    public ObservableCollection<RenameBatch> RenameHistory { get; } = new();
    
    [RelayCommand]
    public void LoadHistory()
    {
        RenameHistory.Clear();
        foreach (var batch in _historyManager.UndoableBatches)
        {
            RenameHistory.Add(batch);
        }
        CanUndo = RenameHistory.Count > 0;
    }
    
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public async Task UndoLastRenameAsync()
    {
        var batch = _historyManager.UndoableBatches.FirstOrDefault();
        if (batch == null)
        {
            CanUndo = false;
            return;
        }
        
        await UndoBatchAsync(batch);
    }
    
    [RelayCommand]
    public async Task UndoBatchAsync(RenameBatch? batch)
    {
        if (batch == null) return;
        
        var errors = new List<string>();
        
        // Undo in reverse order
        foreach (var op in batch.Operations.AsEnumerable().Reverse())
        {
            try
            {
                if (op.IsDirectory)
                {
                    if (Directory.Exists(op.NewPath))
                    {
                        Directory.Move(op.NewPath, op.OldPath);
                    }
                }
                else
                {
                    if (File.Exists(op.NewPath))
                    {
                        File.Move(op.NewPath, op.OldPath);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to restore {op.OldPath}: {ex.Message}");
            }
        }
        
        _historyManager.MarkAsUndone(batch.Id);
        RenameHistory.Remove(batch);
        CanUndo = _historyManager.UndoableBatches.Any();
        
        await Task.CompletedTask;
    }
    
    [RelayCommand]
    public void ClearHistory()
    {
        _historyManager.ClearHistory();
        RenameHistory.Clear();
        CanUndo = false;
    }
    
    [RelayCommand]
    public void MoveItemUp(RenameItem? item)
    {
        if (item == null) return;
        var index = Items.IndexOf(item);
        if (index > 0)
        {
            Items.Move(index, index - 1);
            UpdatePreview();
        }
    }
    
    [RelayCommand]
    public void MoveItemDown(RenameItem? item)
    {
        if (item == null) return;
        var index = Items.IndexOf(item);
        if (index < Items.Count - 1)
        {
            Items.Move(index, index + 1);
            UpdatePreview();
        }
    }
    
    [RelayCommand]
    public void RemoveItem(RenameItem? item)
    {
        if (item != null)
        {
            Items.Remove(item);
            UpdatePreview();
        }
    }
    
    [RelayCommand]
    public void Reset()
    {
        SearchPattern = string.Empty;
        ReplacePattern = string.Empty;
        ExtensionPattern = string.Empty;
        CounterStart = "1";
        CounterStep = "1";
        CounterDigits = "1";
        UseRegex = false;
        CaseSensitive = false;
        NameCase = CaseTransform.None;
        ExtensionCase = CaseTransform.None;
        UpdatePreview();
    }
}

public partial class RenameItem : ObservableObject
{
    [ObservableProperty]
    private string _originalPath = string.Empty;
    
    [ObservableProperty]
    private string _originalName = string.Empty;
    
    [ObservableProperty]
    private string _newName = string.Empty;
    
    [ObservableProperty]
    private string _directory = string.Empty;
    
    [ObservableProperty]
    private bool _hasConflict;
    
    [ObservableProperty]
    private bool _hasError;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool _isRenamed;
}

public enum CaseTransform
{
    None,
    Lower,
    Upper,
    Title,
    Sentence
}
