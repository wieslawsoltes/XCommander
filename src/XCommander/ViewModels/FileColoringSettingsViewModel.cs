using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Services;

namespace XCommander.ViewModels;

/// <summary>
/// View model for a file color rule.
/// </summary>
public partial class FileColorRuleViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private ColorCriteria _criteria = ColorCriteria.Extension;
    
    [ObservableProperty]
    private string _pattern = string.Empty;
    
    [ObservableProperty]
    private string _foregroundColor = "#FFFFFF";
    
    [ObservableProperty]
    private string? _backgroundColor;
    
    [ObservableProperty]
    private bool _isBold;
    
    [ObservableProperty]
    private bool _isItalic;
    
    [ObservableProperty]
    private bool _isEnabled = true;
    
    [ObservableProperty]
    private int _priority;
    
    public static FileColorRuleViewModel FromRule(FileColorRule rule)
    {
        return new FileColorRuleViewModel
        {
            Id = rule.Id,
            Name = rule.Name,
            Criteria = rule.Criteria,
            Pattern = rule.Pattern,
            ForegroundColor = rule.ForegroundColor,
            BackgroundColor = rule.BackgroundColor,
            IsBold = rule.IsBold,
            IsItalic = rule.IsItalic,
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority
        };
    }
    
    public FileColorRule ToRule()
    {
        return new FileColorRule
        {
            Id = Id,
            Name = Name,
            Criteria = Criteria,
            Pattern = Pattern,
            ForegroundColor = ForegroundColor,
            BackgroundColor = BackgroundColor,
            IsBold = IsBold,
            IsItalic = IsItalic,
            IsEnabled = IsEnabled,
            Priority = Priority
        };
    }
}

/// <summary>
/// ViewModel for file coloring settings dialog.
/// </summary>
public partial class FileColoringSettingsViewModel : ViewModelBase
{
    private readonly IFileColoringService? _coloringService;
    
    [ObservableProperty]
    private FileColorRuleViewModel? _selectedRule;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private bool _isEditing;
    
    [ObservableProperty]
    private string _selectedPreset = string.Empty;
    
    // Edit fields
    [ObservableProperty]
    private string _editName = string.Empty;
    
    [ObservableProperty]
    private ColorCriteria _editCriteria = ColorCriteria.Extension;
    
    [ObservableProperty]
    private string _editPattern = string.Empty;
    
    [ObservableProperty]
    private string _editForeground = "#FFFFFF";
    
    [ObservableProperty]
    private string _editBackground = string.Empty;
    
    [ObservableProperty]
    private bool _editBold;
    
    [ObservableProperty]
    private bool _editItalic;
    
    public ObservableCollection<FileColorRuleViewModel> Rules { get; } = [];

    public ObservableCollection<DataGridColumnDefinition> RulesColumnDefinitions { get; }
    public FilteringModel RulesFilteringModel { get; }
    public SortingModel RulesSortingModel { get; }
    public SearchModel RulesSearchModel { get; }
    
    public ObservableCollection<string> Presets { get; } =
    [
        "Default",
        "Total Commander Classic",
        "Dark Mode",
        "Minimal"
    ];
    
    public ObservableCollection<ColorCriteria> CriteriaOptions { get; } =
    [
        ColorCriteria.Extension,
        ColorCriteria.Attribute,
        ColorCriteria.Size,
        ColorCriteria.Age,
        ColorCriteria.NamePattern,
        ColorCriteria.Custom
    ];
    
    public ObservableCollection<string> CommonColors { get; } =
    [
        "#FFFFFF", // White
        "#FF0000", // Red
        "#00FF00", // Green
        "#0000FF", // Blue
        "#FFFF00", // Yellow
        "#FF00FF", // Magenta
        "#00FFFF", // Cyan
        "#FFA500", // Orange
        "#808080", // Gray
        "#ADD8E6", // Light Blue
        "#90EE90", // Light Green
        "#FFB6C1"  // Light Pink
    ];
    
    public event EventHandler? RulesModified;
    
    public FileColoringSettingsViewModel(IFileColoringService? coloringService = null)
    {
        _coloringService = coloringService;
        RulesFilteringModel = new FilteringModel { OwnsViewFilter = true };
        RulesSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        RulesSearchModel = new SearchModel();
        RulesColumnDefinitions = BuildRuleColumnDefinitions();
        LoadRules();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildRuleColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<FileColorRuleViewModel>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Rule",
                cellTemplateKey: "FileColorRuleTemplate",
                configure: column =>
                {
                    column.ColumnKey = "rule";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<FileColorRuleViewModel, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<FileColorRuleViewModel, string>(
                            item => item.Name),
                        SearchTextProvider = item =>
                        {
                            if (item is not FileColorRuleViewModel rule)
                                return string.Empty;
                            return $"{rule.Name} {rule.Criteria} {rule.Pattern}";
                        }
                    };
                })
        };
    }
    
    private void LoadRules()
    {
        Rules.Clear();
        
        if (_coloringService != null)
        {
            foreach (var rule in _coloringService.GetAllRules())
            {
                Rules.Add(FileColorRuleViewModel.FromRule(rule));
            }
        }
        else
        {
            // Add some example rules for demo
            Rules.Add(new FileColorRuleViewModel
            {
                Id = "1",
                Name = "Executables",
                Criteria = ColorCriteria.Extension,
                Pattern = "exe;com;bat;cmd",
                ForegroundColor = "#00FF00",
                IsEnabled = true
            });
            Rules.Add(new FileColorRuleViewModel
            {
                Id = "2",
                Name = "Archives",
                Criteria = ColorCriteria.Extension,
                Pattern = "zip;rar;7z;tar;gz",
                ForegroundColor = "#FF00FF",
                IsEnabled = true
            });
            Rules.Add(new FileColorRuleViewModel
            {
                Id = "3",
                Name = "Hidden Files",
                Criteria = ColorCriteria.Attribute,
                Pattern = "Hidden",
                ForegroundColor = "#808080",
                IsItalic = true,
                IsEnabled = true
            });
        }
    }
    
    partial void OnSelectedRuleChanged(FileColorRuleViewModel? value)
    {
        if (value != null)
        {
            EditName = value.Name;
            EditCriteria = value.Criteria;
            EditPattern = value.Pattern;
            EditForeground = value.ForegroundColor;
            EditBackground = value.BackgroundColor ?? string.Empty;
            EditBold = value.IsBold;
            EditItalic = value.IsItalic;
            IsEditing = true;
        }
        else
        {
            IsEditing = false;
        }
    }
    
    [RelayCommand]
    private void AddRule()
    {
        var newRule = new FileColorRuleViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "New Rule",
            Criteria = ColorCriteria.Extension,
            Pattern = "*",
            ForegroundColor = "#FFFFFF",
            IsEnabled = true,
            Priority = Rules.Count
        };
        
        Rules.Add(newRule);
        SelectedRule = newRule;
        
        if (_coloringService != null)
        {
            _coloringService.AddRule(newRule.ToRule());
        }
        
        StatusText = "New rule added. Edit properties below.";
    }
    
    [RelayCommand]
    private void DeleteRule()
    {
        if (SelectedRule == null) return;
        
        var rule = SelectedRule;
        Rules.Remove(rule);
        
        if (_coloringService != null)
        {
            _coloringService.RemoveRule(rule.Id);
        }
        
        SelectedRule = Rules.FirstOrDefault();
        StatusText = $"Rule '{rule.Name}' deleted.";
        RulesModified?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void ApplyChanges()
    {
        if (SelectedRule == null) return;
        
        SelectedRule.Name = EditName;
        SelectedRule.Criteria = EditCriteria;
        SelectedRule.Pattern = EditPattern;
        SelectedRule.ForegroundColor = EditForeground;
        SelectedRule.BackgroundColor = string.IsNullOrEmpty(EditBackground) ? null : EditBackground;
        SelectedRule.IsBold = EditBold;
        SelectedRule.IsItalic = EditItalic;
        
        if (_coloringService != null)
        {
            _coloringService.UpdateRule(SelectedRule.ToRule());
        }
        
        StatusText = $"Rule '{SelectedRule.Name}' updated.";
        RulesModified?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedRule == null) return;
        
        var index = Rules.IndexOf(SelectedRule);
        if (index > 0)
        {
            Rules.Move(index, index - 1);
            
            if (_coloringService != null)
            {
                _coloringService.MoveRuleUp(SelectedRule.Id);
            }
            
            StatusText = $"Rule '{SelectedRule.Name}' moved up.";
        }
    }
    
    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedRule == null) return;
        
        var index = Rules.IndexOf(SelectedRule);
        if (index < Rules.Count - 1)
        {
            Rules.Move(index, index + 1);
            
            if (_coloringService != null)
            {
                _coloringService.MoveRuleDown(SelectedRule.Id);
            }
            
            StatusText = $"Rule '{SelectedRule.Name}' moved down.";
        }
    }
    
    [RelayCommand]
    private void ToggleEnabled()
    {
        if (SelectedRule == null) return;
        
        SelectedRule.IsEnabled = !SelectedRule.IsEnabled;
        
        if (_coloringService != null)
        {
            _coloringService.SetRuleEnabled(SelectedRule.Id, SelectedRule.IsEnabled);
        }
        
        StatusText = SelectedRule.IsEnabled 
            ? $"Rule '{SelectedRule.Name}' enabled." 
            : $"Rule '{SelectedRule.Name}' disabled.";
        RulesModified?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void LoadPreset(string? presetName)
    {
        if (string.IsNullOrEmpty(presetName)) return;
        
        if (_coloringService != null)
        {
            _coloringService.LoadPreset(presetName);
            LoadRules();
        }
        
        StatusText = $"Loaded preset: {presetName}";
        RulesModified?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private async Task ImportRulesAsync()
    {
        // Would open file picker in real implementation
        StatusText = "Import feature coming soon...";
        await Task.CompletedTask;
    }
    
    [RelayCommand]
    private async Task ExportRulesAsync()
    {
        // Would open save dialog in real implementation
        StatusText = "Export feature coming soon...";
        await Task.CompletedTask;
    }
    
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_coloringService != null)
        {
            await _coloringService.SaveAsync();
            StatusText = "Rules saved successfully.";
        }
    }
}
