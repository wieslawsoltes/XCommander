using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Helpers;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class HotlistNodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _path;

    [ObservableProperty]
    private string _icon = "[DIR]";

    [ObservableProperty]
    private string? _shortcut;

    [ObservableProperty]
    private bool _isCategory;

    [ObservableProperty]
    private bool _isSeparator;

    [ObservableProperty]
    private string? _parentCategoryId;

    public ObservableCollection<HotlistNodeViewModel> Children { get; } = new();
}

public partial class DirectoryHotlistViewModel : ViewModelBase
{
    private readonly IDirectoryHotlistService _hotlistService;
    private Dictionary<string, HotlistItem> _itemsById = new();
    private Dictionary<string, HotlistCategory> _categoriesById = new();
    private const string NameColumnKey = "name";
    private bool _isSelectionSync;

    [ObservableProperty]
    private ObservableCollection<HotlistNodeViewModel> _nodes = new();

    [ObservableProperty]
    private HotlistNodeViewModel? _selectedNode;

    [ObservableProperty]
    private HierarchicalNode? _selectedHierarchicalNode;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    public HierarchicalModel<HotlistNodeViewModel> HierarchicalModel { get; }

    public event EventHandler<string>? NavigateRequested;
    public event EventHandler? RequestClose;

    public DirectoryHotlistViewModel(IDirectoryHotlistService hotlistService)
    {
        _hotlistService = hotlistService;
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
        HierarchicalModel = BuildHierarchicalModel();
        HierarchicalModel.SetRoots(Nodes);
        _hotlistService.HotlistChanged += (_, _) => _ = LoadAsync();
    }

    private static HierarchicalModel<HotlistNodeViewModel> BuildHierarchicalModel()
    {
        var options = new HierarchicalOptions<HotlistNodeViewModel>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children.Count == 0,
            ExpandedStateKeyMode = ExpandedStateKeyMode.Custom,
            ExpandedStateKeySelector = node => node.Id
        };

        return new HierarchicalModel<HotlistNodeViewModel>(options);
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<HierarchicalNode>();

        IPropertyInfo itemProperty = DataGridColumnHelper.CreateProperty(
            "Item",
            (HierarchicalNode node) => (HotlistNodeViewModel)node.Item);

        var nameColumn = builder.Hierarchical(
            header: "Name",
            property: itemProperty,
            getter: node => (HotlistNodeViewModel)node.Item,
            configure: column =>
            {
                column.ColumnKey = NameColumnKey;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
                column.CellTemplateKey = "HotlistNodeTemplate";
                column.ValueAccessor = new DataGridColumnValueAccessor<HierarchicalNode, string>(
                    node => ((HotlistNodeViewModel)node.Item).Name);
                column.ValueType = typeof(string);
                column.Options = new DataGridColumnDefinitionOptions
                {
                    SortValueAccessor = new DataGridColumnValueAccessor<HierarchicalNode, string>(
                        node => ((HotlistNodeViewModel)node.Item).Name)
                };
            });

        return new ObservableCollection<DataGridColumnDefinition>
        {
            nameColumn
        };
    }

    public async Task LoadAsync()
    {
        var allItems = (await _hotlistService.GetItemsAsync()).ToList();
        var allCategories = (await _hotlistService.GetCategoriesAsync()).ToList();
        _itemsById = allItems.ToDictionary(i => i.Id, i => i);
        _categoriesById = allCategories.ToDictionary(c => c.Id, c => c);

        var nodes = new List<HotlistNodeViewModel>();

        var rootItems = allItems.Where(i => i.ParentCategoryId == null)
            .OrderBy(i => i.Order);
        foreach (var item in rootItems.OrderBy(i => i.Order))
        {
            var node = CreateItemNode(item);
            if (node != null)
            {
                nodes.Add(node);
            }
        }

        var categories = await _hotlistService.GetCategoryTreeAsync();
        foreach (var category in categories.OrderBy(c => c.Order))
        {
            nodes.Add(CreateCategoryNode(category));
        }

        Nodes = new ObservableCollection<HotlistNodeViewModel>(nodes);
    }

    partial void OnNodesChanged(ObservableCollection<HotlistNodeViewModel> value)
    {
        HierarchicalModel.SetRoots(value);
    }

    partial void OnSelectedNodeChanged(HotlistNodeViewModel? value)
    {
        if (_isSelectionSync)
            return;

        _isSelectionSync = true;
        SelectedHierarchicalNode = value == null ? null : ((IHierarchicalModel)HierarchicalModel).FindNode(value);
        _isSelectionSync = false;
    }

    partial void OnSelectedHierarchicalNodeChanged(HierarchicalNode? value)
    {
        if (_isSelectionSync)
            return;

        _isSelectionSync = true;
        SelectedNode = value?.Item as HotlistNodeViewModel;
        _isSelectionSync = false;
    }

    [RelayCommand]
    public void NavigateSelected()
    {
        if (SelectedNode?.Path == null)
            return;

        _ = _hotlistService.RecordAccessAsync(SelectedNode.Id);
        NavigateRequested?.Invoke(this, SelectedNode.Path);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> TryNavigateByShortcutAsync(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var item = await _hotlistService.GetItemByShortcutAsync(shortcut);
        if (item?.Path == null)
            return false;

        SelectedNode = new HotlistNodeViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Path = item.Path,
            Shortcut = item.KeyboardShortcut,
            ParentCategoryId = item.ParentCategoryId
        };

        NavigateRequested?.Invoke(this, item.Path);
        RequestClose?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [RelayCommand]
    public async Task AddCurrentAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
            return;

        var categoryId = GetSelectedParentCategoryId();
        await _hotlistService.AddDirectoryAsync(
            CurrentPath,
            Path.GetFileName(CurrentPath) ?? CurrentPath,
            categoryId);
        await LoadAsync();
    }

    public async Task AddCategoryAsync(string name, string? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        await _hotlistService.CreateCategoryAsync(name, parentCategoryId);
        await LoadAsync();
    }

    public async Task AddSeparatorAsync(string? parentCategoryId)
    {
        await _hotlistService.AddSeparatorAsync(parentCategoryId);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task RemoveSelectedAsync()
    {
        if (SelectedNode == null)
            return;

        if (SelectedNode.IsCategory)
        {
            await _hotlistService.DeleteCategoryAsync(SelectedNode.Id, deleteContents: false);
        }
        else
        {
            await _hotlistService.RemoveItemAsync(SelectedNode.Id);
        }

        await LoadAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    public async Task RenameSelectedAsync(string newName)
    {
        if (SelectedNode == null || string.IsNullOrWhiteSpace(newName) || SelectedNode.IsSeparator)
            return;

        if (SelectedNode.IsCategory)
        {
            await _hotlistService.RenameCategoryAsync(SelectedNode.Id, newName);
        }
        else if (!SelectedNode.IsSeparator && _itemsById.TryGetValue(SelectedNode.Id, out var item))
        {
            await _hotlistService.UpdateItemAsync(item with { Name = newName });
        }

        await LoadAsync();
    }

    public async Task DeleteSelectedAsync(bool deleteContents)
    {
        if (SelectedNode == null)
            return;

        if (SelectedNode.IsCategory)
        {
            await _hotlistService.DeleteCategoryAsync(SelectedNode.Id, deleteContents);
        }
        else
        {
            await _hotlistService.RemoveItemAsync(SelectedNode.Id);
        }

        await LoadAsync();
    }

    public async Task SetShortcutAsync(string? shortcut)
    {
        if (SelectedNode == null || SelectedNode.IsCategory || SelectedNode.IsSeparator)
            return;

        await _hotlistService.SetItemShortcutAsync(SelectedNode.Id, shortcut);
        await LoadAsync();
    }

    public string? GetSelectedParentCategoryId()
    {
        if (SelectedNode == null)
            return null;

        if (SelectedNode.IsCategory)
            return SelectedNode.Id;

        return SelectedNode.ParentCategoryId;
    }

    private HotlistNodeViewModel CreateCategoryNode(HotlistCategory category)
    {
        var node = new HotlistNodeViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon ?? "[CAT]",
            IsCategory = true,
            ParentCategoryId = category.ParentCategoryId
        };

        foreach (var subCategory in category.SubCategories.OrderBy(c => c.Order))
        {
            node.Children.Add(CreateCategoryNode(subCategory));
        }

        foreach (var item in category.Items.OrderBy(i => i.Order))
        {
            var child = CreateItemNode(item);
            if (child != null)
            {
                node.Children.Add(child);
            }
        }

        return node;
    }

    private HotlistNodeViewModel? CreateItemNode(HotlistItem item)
    {
        if (item.Type == HotlistItemType.Separator)
        {
            return new HotlistNodeViewModel
            {
                Id = item.Id,
                Name = "--------",
                IsSeparator = true,
                ParentCategoryId = item.ParentCategoryId
            };
        }

        if (item.Type == HotlistItemType.Category)
        {
            return null;
        }

        return new HotlistNodeViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Path = item.Path,
            Icon = item.Icon ?? "[DIR]",
            Shortcut = item.KeyboardShortcut,
            ParentCategoryId = item.ParentCategoryId,
            IsCategory = false
        };
    }
}
