using System.Collections.ObjectModel;
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

public partial class DirectoryTreeNodeViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private bool _isLoaded;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    [ObservableProperty]
    private bool _isExpanded;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private string _icon = "📁";
    
    public ObservableCollection<DirectoryTreeNodeViewModel> Children { get; } = [];
    
    public DirectoryTreeNodeViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
    
    public DirectoryTreeNodeViewModel(IFileSystemService fileSystemService, string path, string name) 
        : this(fileSystemService)
    {
        FullPath = path;
        Name = name;
        
        // Add placeholder child to show expander
        if (HasSubdirectories(path))
        {
            Children.Add(new DirectoryTreeNodeViewModel(fileSystemService) { Name = "Loading..." });
        }
    }
    
    private bool HasSubdirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }
    
    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_isLoaded)
        {
            LoadChildren();
        }
    }
    
    public void LoadChildren()
    {
        if (_isLoaded) return;
        
        Children.Clear();
        
        try
        {
            var directories = Directory.GetDirectories(FullPath)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);
            
            foreach (var dir in directories)
            {
                var name = Path.GetFileName(dir);
                
                // Skip hidden directories unless showing hidden files
                if (name.StartsWith("."))
                    continue;
                    
                var node = new DirectoryTreeNodeViewModel(_fileSystemService, dir, name);
                Children.Add(node);
            }
            
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading directory children: {ex.Message}");
        }
    }
    
    public void Refresh()
    {
        _isLoaded = false;
        Children.Clear();
        
        if (HasSubdirectories(FullPath))
        {
            Children.Add(new DirectoryTreeNodeViewModel(_fileSystemService) { Name = "Loading..." });
        }
        
        if (IsExpanded)
        {
            LoadChildren();
        }
    }
}

public partial class DirectoryTreeViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private const string NameColumnKey = "name";
    private bool _isSelectionSync;
    
    [ObservableProperty]
    private bool _isVisible;
    
    [ObservableProperty]
    private DirectoryTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private HierarchicalNode? _selectedHierarchicalNode;
    
    [ObservableProperty]
    private string _currentPath = string.Empty;
    
    public ObservableCollection<DirectoryTreeNodeViewModel> RootNodes { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    public HierarchicalModel<DirectoryTreeNodeViewModel> HierarchicalModel { get; }
    
    public event EventHandler<string>? NavigationRequested;
    
    public DirectoryTreeViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
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
        LoadRootNodes();
    }

    private static HierarchicalModel<DirectoryTreeNodeViewModel> BuildHierarchicalModel()
    {
        var options = new HierarchicalOptions<DirectoryTreeNodeViewModel>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children.Count == 0,
            IsExpandedSelector = node => node.IsExpanded,
            IsExpandedSetter = (node, isExpanded) => node.IsExpanded = isExpanded,
            ExpandedStateKeyMode = ExpandedStateKeyMode.Custom,
            ExpandedStateKeySelector = node => node.FullPath
        };

        return new HierarchicalModel<DirectoryTreeNodeViewModel>(options);
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<HierarchicalNode>();

        IPropertyInfo itemProperty = DataGridColumnHelper.CreateProperty(
            "Item",
            (HierarchicalNode node) => (DirectoryTreeNodeViewModel)node.Item);

        var nameColumn = builder.Hierarchical(
            header: "Folder",
            property: itemProperty,
            getter: node => (DirectoryTreeNodeViewModel)node.Item,
            configure: column =>
            {
                column.ColumnKey = NameColumnKey;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
                column.CellTemplateKey = "DirectoryTreeNodeTemplate";
                column.ValueAccessor = new DataGridColumnValueAccessor<HierarchicalNode, string>(
                    node => ((DirectoryTreeNodeViewModel)node.Item).Name);
                column.ValueType = typeof(string);
                column.Options = new DataGridColumnDefinitionOptions
                {
                    SortValueAccessor = new DataGridColumnValueAccessor<HierarchicalNode, string>(
                        node => ((DirectoryTreeNodeViewModel)node.Item).Name)
                };
            });

        return new ObservableCollection<DataGridColumnDefinition>
        {
            nameColumn
        };
    }
    
    public void LoadRootNodes()
    {
        RootNodes.Clear();
        
        try
        {
            var drives = _fileSystemService.GetDrives();
            
            foreach (var drive in drives)
            {
                var node = new DirectoryTreeNodeViewModel(_fileSystemService, drive.RootPath, drive.DisplayName)
                {
                    Icon = GetDriveIcon(drive.DriveType)
                };
                RootNodes.Add(node);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading root nodes: {ex.Message}");
        }

        HierarchicalModel.SetRoots(RootNodes);
    }
    
    private string GetDriveIcon(DriveType type)
    {
        return type switch
        {
            DriveType.Fixed => "💾",
            DriveType.Removable => "💿",
            DriveType.Network => "🌐",
            DriveType.CDRom => "📀",
            _ => "📁"
        };
    }
    
    partial void OnSelectedNodeChanged(DirectoryTreeNodeViewModel? value)
    {
        if (!_isSelectionSync)
        {
            _isSelectionSync = true;
            SelectedHierarchicalNode = value == null ? null : ((IHierarchicalModel)HierarchicalModel).FindNode(value);
            _isSelectionSync = false;
        }

        if (value != null && !string.IsNullOrEmpty(value.FullPath))
        {
            NavigationRequested?.Invoke(this, value.FullPath);
        }
    }

    partial void OnSelectedHierarchicalNodeChanged(HierarchicalNode? value)
    {
        if (_isSelectionSync)
            return;

        _isSelectionSync = true;
        SelectedNode = value?.Item as DirectoryTreeNodeViewModel;
        _isSelectionSync = false;
    }
    
    [RelayCommand]
    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }
    
    [RelayCommand]
    public void Refresh()
    {
        LoadRootNodes();
    }
    
    /// <summary>
    /// Expands the tree to show and select the given path.
    /// </summary>
    public void NavigateToPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
            
        CurrentPath = path;
        
        // Find and expand the path in the tree
        var pathParts = GetPathParts(path);
        if (pathParts.Count == 0)
            return;
            
        // Find the root node
        var rootPath = pathParts[0];
        var rootNode = RootNodes.FirstOrDefault(n => 
            n.FullPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
            n.FullPath.TrimEnd(Path.DirectorySeparatorChar).Equals(rootPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
            
        if (rootNode == null)
            return;
            
        // Expand and navigate through the path
        var currentNode = rootNode;
        currentNode.IsExpanded = true;
        
        for (int i = 1; i < pathParts.Count; i++)
        {
            var part = pathParts[i];
            var childNode = currentNode.Children.FirstOrDefault(n =>
                n.Name.Equals(Path.GetFileName(part), StringComparison.OrdinalIgnoreCase));
                
            if (childNode == null)
                break;
                
            childNode.IsExpanded = true;
            currentNode = childNode;
        }
        
        // Select the final node
        if (SelectedNode != null)
            SelectedNode.IsSelected = false;
            
        currentNode.IsSelected = true;
        SelectedNode = currentNode;
    }
    
    private List<string> GetPathParts(string path)
    {
        var parts = new List<string>();
        var current = path;
        
        while (!string.IsNullOrEmpty(current))
        {
            parts.Insert(0, current);
            var parent = Path.GetDirectoryName(current);
            if (parent == current)
                break;
            current = parent ?? string.Empty;
        }
        
        return parts;
    }
}
