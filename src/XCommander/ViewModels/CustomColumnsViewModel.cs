using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;

namespace XCommander.ViewModels;

public partial class ColumnConfigItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private double _width = 100;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    [ObservableProperty]
    private int _order;
    
    [ObservableProperty]
    private ColumnAlignment _alignment = ColumnAlignment.Left;
    
    [ObservableProperty]
    private bool _isBuiltIn;
    
    [ObservableProperty]
    private string? _pluginId;
    
    public ColumnConfiguration ToConfiguration()
    {
        return new ColumnConfiguration
        {
            Id = Id,
            Name = Name,
            Width = Width,
            IsVisible = IsVisible,
            Order = Order,
            Alignment = Alignment,
            IsBuiltIn = IsBuiltIn,
            PluginId = PluginId
        };
    }
    
    public static ColumnConfigItemViewModel FromConfiguration(ColumnConfiguration config)
    {
        return new ColumnConfigItemViewModel
        {
            Id = config.Id,
            Name = config.Name,
            Width = config.Width,
            IsVisible = config.IsVisible,
            Order = config.Order,
            Alignment = config.Alignment,
            IsBuiltIn = config.IsBuiltIn,
            PluginId = config.PluginId
        };
    }
}

public partial class CustomColumnsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<ColumnConfigItemViewModel> _columns = new();
    
    [ObservableProperty]
    private ObservableCollection<ColumnConfigItemViewModel> _availableColumns = new();
    
    [ObservableProperty]
    private ColumnConfigItemViewModel? _selectedColumn;
    
    [ObservableProperty]
    private ColumnConfigItemViewModel? _selectedAvailableColumn;

    public ObservableCollection<DataGridColumnDefinition> ColumnsColumnDefinitions { get; }
    public FilteringModel ColumnsFilteringModel { get; }
    public SortingModel ColumnsSortingModel { get; }
    public SearchModel ColumnsSearchModel { get; }
    public ObservableCollection<DataGridColumnDefinition> AvailableColumnDefinitions { get; }
    public FilteringModel AvailableFilteringModel { get; }
    public SortingModel AvailableSortingModel { get; }
    public SearchModel AvailableSearchModel { get; }
    
    public event EventHandler? ColumnsChanged;

    public CustomColumnsViewModel()
    {
        ColumnsFilteringModel = new FilteringModel { OwnsViewFilter = true };
        ColumnsSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        ColumnsSearchModel = new SearchModel();
        ColumnsColumnDefinitions = BuildActiveColumnDefinitions();

        AvailableFilteringModel = new FilteringModel { OwnsViewFilter = true };
        AvailableSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        AvailableSearchModel = new SearchModel();
        AvailableColumnDefinitions = BuildAvailableColumnDefinitions();
        LoadDefaultColumns();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildActiveColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<ColumnConfigItemViewModel>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Active Columns",
                cellTemplateKey: "ActiveColumnTemplate",
                configure: column =>
                {
                    column.ColumnKey = "active-column";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<ColumnConfigItemViewModel, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<ColumnConfigItemViewModel, string>(
                            item => item.Name),
                        SearchTextProvider = item =>
                        {
                            if (item is not ColumnConfigItemViewModel columnItem)
                                return string.Empty;
                            return $"{columnItem.Name} {columnItem.Id}";
                        }
                    };
                })
        };
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildAvailableColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<ColumnConfigItemViewModel>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Available Columns",
                cellTemplateKey: "AvailableColumnTemplate",
                configure: column =>
                {
                    column.ColumnKey = "available-column";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<ColumnConfigItemViewModel, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<ColumnConfigItemViewModel, string>(
                            item => item.Name),
                        SearchTextProvider = item =>
                        {
                            if (item is not ColumnConfigItemViewModel columnItem)
                                return string.Empty;
                            return $"{columnItem.Name} {columnItem.Id}";
                        }
                    };
                })
        };
    }

    private void LoadDefaultColumns()
    {
        Columns.Clear();
        AvailableColumns.Clear();
        
        var defaultColumns = BuiltInColumns.GetDefaultColumns();
        var allColumns = BuiltInColumns.GetAllAvailableColumns();
        
        foreach (var col in defaultColumns.OrderBy(c => c.Order))
        {
            Columns.Add(ColumnConfigItemViewModel.FromConfiguration(col));
        }
        
        // Add columns that are not in default as available
        var activeIds = defaultColumns.Select(c => c.Id).ToHashSet();
        foreach (var col in allColumns.Where(c => !activeIds.Contains(c.Id)))
        {
            AvailableColumns.Add(ColumnConfigItemViewModel.FromConfiguration(col));
        }
    }

    public void LoadFromConfiguration(List<ColumnConfiguration> configurations)
    {
        Columns.Clear();
        AvailableColumns.Clear();
        
        var allColumns = BuiltInColumns.GetAllAvailableColumns();
        var configuredIds = configurations.Select(c => c.Id).ToHashSet();
        
        foreach (var config in configurations.OrderBy(c => c.Order))
        {
            Columns.Add(ColumnConfigItemViewModel.FromConfiguration(config));
        }
        
        // Add non-configured columns as available
        foreach (var col in allColumns.Where(c => !configuredIds.Contains(c.Id)))
        {
            AvailableColumns.Add(ColumnConfigItemViewModel.FromConfiguration(col));
        }
    }

    public List<ColumnConfiguration> GetConfiguration()
    {
        return Columns.Select(c => c.ToConfiguration()).ToList();
    }

    [RelayCommand]
    public void AddColumn()
    {
        if (SelectedAvailableColumn == null)
            return;

        var column = SelectedAvailableColumn;
        AvailableColumns.Remove(column);
        column.Order = Columns.Count;
        column.IsVisible = true;
        Columns.Add(column);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void RemoveColumn()
    {
        if (SelectedColumn == null || SelectedColumn.Id == BuiltInColumns.Name)
            return; // Can't remove name column

        var column = SelectedColumn;
        Columns.Remove(column);
        column.IsVisible = false;
        AvailableColumns.Add(column);
        UpdateColumnOrder();
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void MoveColumnUp()
    {
        if (SelectedColumn == null)
            return;

        var index = Columns.IndexOf(SelectedColumn);
        if (index > 0)
        {
            Columns.Move(index, index - 1);
            UpdateColumnOrder();
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void MoveColumnDown()
    {
        if (SelectedColumn == null)
            return;

        var index = Columns.IndexOf(SelectedColumn);
        if (index < Columns.Count - 1)
        {
            Columns.Move(index, index + 1);
            UpdateColumnOrder();
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void ResetToDefault()
    {
        LoadDefaultColumns();
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateColumnOrder()
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            Columns[i].Order = i;
        }
    }

    [RelayCommand]
    public void ToggleColumnVisibility(ColumnConfigItemViewModel? column)
    {
        if (column == null || column.Id == BuiltInColumns.Name)
            return; // Can't hide name column

        column.IsVisible = !column.IsVisible;
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }
}
