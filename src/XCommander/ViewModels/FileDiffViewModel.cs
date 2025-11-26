using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace XCommander.ViewModels;

public enum DiffDisplayMode
{
    SideBySide,
    Inline
}

public partial class DiffLineViewModel : ViewModelBase
{
    [ObservableProperty]
    private int? _leftLineNumber;
    
    [ObservableProperty]
    private int? _rightLineNumber;
    
    [ObservableProperty]
    private string _leftText = string.Empty;
    
    [ObservableProperty]
    private string _rightText = string.Empty;
    
    [ObservableProperty]
    private ChangeType _changeType;
    
    [ObservableProperty]
    private string _backgroundBrush = "Transparent";
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _useLeftVersion = true;
    
    [ObservableProperty]
    private bool _useRightVersion;
    
    public int LineIndex { get; set; }
    
    public string ChangeIcon => ChangeType switch
    {
        ChangeType.Inserted => "+",
        ChangeType.Deleted => "-",
        ChangeType.Modified => "~",
        ChangeType.Imaginary => " ",
        _ => " "
    };
    
    public bool HasConflict => ChangeType != ChangeType.Unchanged && ChangeType != ChangeType.Imaginary;
    
    public DiffLineViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChangeType))
            {
                UpdateBackgroundBrush();
            }
        };
    }
    
    private void UpdateBackgroundBrush()
    {
        BackgroundBrush = ChangeType switch
        {
            ChangeType.Inserted => "#2020A020",
            ChangeType.Deleted => "#20A02020",
            ChangeType.Modified => "#20A0A020",
            _ => "Transparent"
        };
    }
    
    [RelayCommand]
    private void SelectLeftVersion()
    {
        UseLeftVersion = true;
        UseRightVersion = false;
    }
    
    [RelayCommand]
    private void SelectRightVersion()
    {
        UseLeftVersion = false;
        UseRightVersion = true;
    }
}

public partial class FileDiffViewModel : ViewModelBase
{
    private readonly Differ _differ = new();
    
    [ObservableProperty]
    private string _leftFilePath = string.Empty;
    
    [ObservableProperty]
    private string _rightFilePath = string.Empty;
    
    [ObservableProperty]
    private string _leftFileName = string.Empty;
    
    [ObservableProperty]
    private string _rightFileName = string.Empty;
    
    [ObservableProperty]
    private DiffDisplayMode _displayMode = DiffDisplayMode.SideBySide;
    
    [ObservableProperty]
    private bool _ignoreWhitespace;
    
    [ObservableProperty]
    private bool _ignoreCase;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private int _addedLines;
    
    [ObservableProperty]
    private int _deletedLines;
    
    [ObservableProperty]
    private int _modifiedLines;
    
    [ObservableProperty]
    private bool _hasUnsavedChanges;
    
    [ObservableProperty]
    private DiffLineViewModel? _selectedDiffLine;
    
    [ObservableProperty]
    private bool _isMergeMode;
    
    public ObservableCollection<DiffLineViewModel> DiffLines { get; } = new();
    public ObservableCollection<DiffPiece> InlineDiffLines { get; } = new();
    
    public event EventHandler? RequestClose;
    
    partial void OnIgnoreWhitespaceChanged(bool value)
    {
        if (!string.IsNullOrEmpty(LeftFilePath) && !string.IsNullOrEmpty(RightFilePath))
        {
            _ = ComputeDiffAsync();
        }
    }
    
    partial void OnIgnoreCaseChanged(bool value)
    {
        if (!string.IsNullOrEmpty(LeftFilePath) && !string.IsNullOrEmpty(RightFilePath))
        {
            _ = ComputeDiffAsync();
        }
    }
    
    public void Initialize(string leftPath, string rightPath)
    {
        LeftFilePath = leftPath;
        RightFilePath = rightPath;
        LeftFileName = Path.GetFileName(leftPath);
        RightFileName = Path.GetFileName(rightPath);
        
        _ = ComputeDiffAsync();
    }
    
    [RelayCommand]
    public async Task ComputeDiffAsync()
    {
        IsLoading = true;
        DiffLines.Clear();
        InlineDiffLines.Clear();
        AddedLines = 0;
        DeletedLines = 0;
        ModifiedLines = 0;
        
        try
        {
            await Task.Run(() =>
            {
                if (!File.Exists(LeftFilePath) || !File.Exists(RightFilePath))
                {
                    StatusText = "One or both files do not exist.";
                    return;
                }
                
                var leftText = File.ReadAllText(LeftFilePath);
                var rightText = File.ReadAllText(RightFilePath);
                
                if (IgnoreCase)
                {
                    leftText = leftText.ToLowerInvariant();
                    rightText = rightText.ToLowerInvariant();
                }
                
                if (DisplayMode == DiffDisplayMode.SideBySide)
                {
                    ComputeSideBySideDiff(leftText, rightText);
                }
                else
                {
                    ComputeInlineDiff(leftText, rightText);
                }
            });
            
            StatusText = $"Changes: +{AddedLines} -{DeletedLines} ~{ModifiedLines}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void ComputeSideBySideDiff(string leftText, string rightText)
    {
        var builder = new SideBySideDiffBuilder(_differ);
        var model = builder.BuildDiffModel(leftText, rightText, IgnoreWhitespace);
        
        var maxLines = Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count);
        
        for (int i = 0; i < maxLines; i++)
        {
            var leftLine = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var rightLine = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;
            
            var changeType = ChangeType.Unchanged;
            
            if (leftLine?.Type == ChangeType.Deleted || rightLine?.Type == ChangeType.Deleted)
            {
                changeType = ChangeType.Deleted;
                DeletedLines++;
            }
            else if (leftLine?.Type == ChangeType.Inserted || rightLine?.Type == ChangeType.Inserted)
            {
                changeType = ChangeType.Inserted;
                AddedLines++;
            }
            else if (leftLine?.Type == ChangeType.Modified || rightLine?.Type == ChangeType.Modified)
            {
                changeType = ChangeType.Modified;
                ModifiedLines++;
            }
            else if (leftLine?.Type == ChangeType.Imaginary || rightLine?.Type == ChangeType.Imaginary)
            {
                changeType = ChangeType.Imaginary;
            }
            
            var diffLine = new DiffLineViewModel
            {
                LeftLineNumber = leftLine?.Position,
                RightLineNumber = rightLine?.Position,
                LeftText = leftLine?.Text ?? string.Empty,
                RightText = rightLine?.Text ?? string.Empty,
                ChangeType = changeType
            };
            
            DiffLines.Add(diffLine);
        }
    }
    
    private void ComputeInlineDiff(string leftText, string rightText)
    {
        var builder = new InlineDiffBuilder(_differ);
        var model = builder.BuildDiffModel(leftText, rightText, IgnoreWhitespace);
        
        foreach (var line in model.Lines)
        {
            InlineDiffLines.Add(line);
            
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    AddedLines++;
                    break;
                case ChangeType.Deleted:
                    DeletedLines++;
                    break;
                case ChangeType.Modified:
                    ModifiedLines++;
                    break;
            }
        }
    }
    
    [RelayCommand]
    public void ToggleDisplayMode()
    {
        DisplayMode = DisplayMode == DiffDisplayMode.SideBySide 
            ? DiffDisplayMode.Inline 
            : DiffDisplayMode.SideBySide;
        
        _ = ComputeDiffAsync();
    }
    
    [RelayCommand]
    public void SwapFiles()
    {
        (LeftFilePath, RightFilePath) = (RightFilePath, LeftFilePath);
        (LeftFileName, RightFileName) = (RightFileName, LeftFileName);
        _ = ComputeDiffAsync();
    }
    
    [RelayCommand]
    public void Refresh()
    {
        _ = ComputeDiffAsync();
    }
    
    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    public async Task CopyDiffToClipboardAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- {LeftFileName}");
        sb.AppendLine($"+++ {RightFileName}");
        sb.AppendLine();
        
        foreach (var line in DiffLines)
        {
            var prefix = line.ChangeType switch
            {
                ChangeType.Inserted => "+",
                ChangeType.Deleted => "-",
                ChangeType.Modified => "~",
                _ => " "
            };
            
            if (line.ChangeType == ChangeType.Deleted)
            {
                sb.AppendLine($"{prefix}{line.LeftText}");
            }
            else if (line.ChangeType == ChangeType.Inserted)
            {
                sb.AppendLine($"{prefix}{line.RightText}");
            }
            else
            {
                sb.AppendLine($" {line.LeftText}");
            }
        }
        
        // Copy to clipboard would need UI integration
        StatusText = "Diff copied to clipboard (requires UI integration)";
    }
    
    [RelayCommand]
    public void CopyLineToRight(DiffLineViewModel? line)
    {
        if (line == null || string.IsNullOrEmpty(line.LeftText)) return;
        
        line.RightText = line.LeftText;
        line.UseLeftVersion = true;
        line.UseRightVersion = false;
        HasUnsavedChanges = true;
        StatusText = "Change applied: Left → Right";
    }
    
    [RelayCommand]
    public void CopyLineToLeft(DiffLineViewModel? line)
    {
        if (line == null || string.IsNullOrEmpty(line.RightText)) return;
        
        line.LeftText = line.RightText;
        line.UseLeftVersion = false;
        line.UseRightVersion = true;
        HasUnsavedChanges = true;
        StatusText = "Change applied: Right → Left";
    }
    
    [RelayCommand]
    public void CopyAllToRight()
    {
        foreach (var line in DiffLines.Where(l => l.HasConflict))
        {
            line.RightText = line.LeftText;
            line.UseLeftVersion = true;
            line.UseRightVersion = false;
        }
        HasUnsavedChanges = true;
        StatusText = "All changes applied: Left → Right";
    }
    
    [RelayCommand]
    public void CopyAllToLeft()
    {
        foreach (var line in DiffLines.Where(l => l.HasConflict))
        {
            line.LeftText = line.RightText;
            line.UseLeftVersion = false;
            line.UseRightVersion = true;
        }
        HasUnsavedChanges = true;
        StatusText = "All changes applied: Right → Left";
    }
    
    [RelayCommand]
    public async Task SaveLeftFileAsync()
    {
        await SaveMergedFileAsync(LeftFilePath, true);
    }
    
    [RelayCommand]
    public async Task SaveRightFileAsync()
    {
        await SaveMergedFileAsync(RightFilePath, false);
    }
    
    [RelayCommand]
    public async Task SaveMergedFileAsAsync()
    {
        var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Merged File As",
            SuggestedFileName = $"merged_{LeftFileName}"
        };
        
        // Request save through event - UI will handle
        MergeRequested?.Invoke(this, ("saveas", string.Empty));
    }
    
    public event EventHandler<(string action, string path)>? MergeRequested;
    
    private async Task SaveMergedFileAsync(string filePath, bool useLeftAsBase)
    {
        try
        {
            var lines = new List<string>();
            
            foreach (var line in DiffLines)
            {
                if (line.ChangeType == ChangeType.Imaginary)
                    continue;
                    
                if (line.HasConflict)
                {
                    // For conflicts, use the selected version
                    if (useLeftAsBase)
                        lines.Add(line.LeftText);
                    else
                        lines.Add(line.RightText);
                }
                else
                {
                    // For unchanged lines, use whichever is available
                    lines.Add(string.IsNullOrEmpty(line.LeftText) ? line.RightText : line.LeftText);
                }
            }
            
            await File.WriteAllLinesAsync(filePath, lines);
            HasUnsavedChanges = false;
            StatusText = $"Saved to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving: {ex.Message}";
        }
    }
    
    public async Task SaveMergedToPathAsync(string filePath)
    {
        await SaveMergedFileAsync(filePath, true);
    }
    
    [RelayCommand]
    public void NavigateToNextDiff()
    {
        var selectedIndex = DiffLines.IndexOf(SelectedDiffLine!);
        for (int i = selectedIndex + 1; i < DiffLines.Count; i++)
        {
            if (DiffLines[i].HasConflict)
            {
                SelectedDiffLine = DiffLines[i];
                return;
            }
        }
        // Wrap around
        for (int i = 0; i <= selectedIndex; i++)
        {
            if (DiffLines[i].HasConflict)
            {
                SelectedDiffLine = DiffLines[i];
                return;
            }
        }
    }
    
    [RelayCommand]
    public void NavigateToPreviousDiff()
    {
        var selectedIndex = SelectedDiffLine != null ? DiffLines.IndexOf(SelectedDiffLine) : DiffLines.Count;
        for (int i = selectedIndex - 1; i >= 0; i--)
        {
            if (DiffLines[i].HasConflict)
            {
                SelectedDiffLine = DiffLines[i];
                return;
            }
        }
        // Wrap around
        for (int i = DiffLines.Count - 1; i >= selectedIndex; i--)
        {
            if (DiffLines[i].HasConflict)
            {
                SelectedDiffLine = DiffLines[i];
                return;
            }
        }
    }
}
