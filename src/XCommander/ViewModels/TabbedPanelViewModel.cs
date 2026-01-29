using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Services;
using XCommander.Views.Dialogs;

namespace XCommander.ViewModels;

public partial class TabbedPanelViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IDescriptionFileService? _descriptionService;
    private readonly ISelectionService? _selectionService;
    private readonly IFileAssociationService? _fileAssociationService;
    private readonly AppSettings _settings;
    
    [ObservableProperty]
    private bool _isActive;
    
    [ObservableProperty]
    private TabViewModel? _activeTab;

    public AppSettings Settings => _settings;
    
    public ObservableCollection<TabViewModel> Tabs { get; } = [];
    
    /// <summary>
    /// Fired when navigation occurs in any tab.
    /// </summary>
    public event EventHandler<string>? Navigated;
    public event EventHandler<TabViewModel?>? ActiveTabChanged;
    public event EventHandler<TabViewModel.ArchiveRequestEventArgs>? ArchiveOpenRequested;
    
    public TabbedPanelViewModel(
        IFileSystemService fileSystemService,
        AppSettings settings,
        IDescriptionFileService? descriptionService = null,
        ISelectionService? selectionService = null,
        IFileAssociationService? fileAssociationService = null)
    {
        _fileSystemService = fileSystemService;
        _settings = settings;
        _descriptionService = descriptionService;
        _selectionService = selectionService;
        _fileAssociationService = fileAssociationService;
        _settings.PropertyChanged += OnSettingsChanged;
    }
    
    public void Initialize(string initialPath)
    {
        var tab = CreateNewTab();
        tab.NavigateTo(initialPath);
        SetActiveTab(tab);
    }

    partial void OnActiveTabChanged(TabViewModel? value)
    {
        ActiveTabChanged?.Invoke(this, value);
    }
    
    [RelayCommand]
    public void AddNewTab()
    {
        var tab = CreateNewTab();
        if (ActiveTab != null)
        {
            tab.NavigateTo(ActiveTab.CurrentPath);
        }
        SetActiveTab(tab);
    }

    [RelayCommand]
    public void NavigateToDrive(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (_settings.OpenNewTabOnDriveChange)
        {
            var tab = CreateNewTab();
            tab.NavigateTo(path);
            SetActiveTab(tab);
            return;
        }

        ActiveTab?.NavigateTo(path);
    }
    
    public TabViewModel CreateNewTab()
    {
        var tab = new TabViewModel(_fileSystemService, _settings, _descriptionService, _selectionService, _fileAssociationService);
        tab.Navigated += (sender, path) => Navigated?.Invoke(this, path);
        tab.ArchiveOpenRequested += (_, args) => ArchiveOpenRequested?.Invoke(this, args);
        Tabs.Add(tab);
        return tab;
    }
    
    /// <summary>
    /// Creates a new tab with virtual items (e.g., search results).
    /// </summary>
    public TabViewModel CreateVirtualTab(IReadOnlyList<string> paths, string title = "Search Results")
    {
        var tab = CreateNewTab();
        tab.PopulateWithPaths(paths, title);
        SetActiveTab(tab);
        return tab;
    }
    
    [RelayCommand]
    public void CloseTab(TabViewModel? tab)
    {
        if (tab == null || tab.IsLocked || Tabs.Count <= 1)
            return;
            
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        
        // Select adjacent tab if we closed the active one
        if (ActiveTab == tab)
        {
            var newIndex = Math.Min(index, Tabs.Count - 1);
            SetActiveTab(Tabs[newIndex]);
        }
    }
    
    [RelayCommand]
    public void DuplicateTab(TabViewModel? tab)
    {
        if (tab == null)
            return;
            
        var newTab = CreateNewTab();
        newTab.ShowHiddenFiles = tab.ShowHiddenFiles;
        newTab.NavigateTo(tab.CurrentPath);
        SetActiveTab(newTab);
    }
    
    [RelayCommand]
    public void ToggleLockTab(TabViewModel? tab)
    {
        if (tab != null)
        {
            tab.IsLocked = !tab.IsLocked;
        }
    }
    
    [RelayCommand]
    public void SetActiveTab(TabViewModel? tab)
    {
        if (tab == null || ActiveTab == tab)
            return;
            
        if (ActiveTab != null)
        {
            ActiveTab.IsSelected = false;
        }
        
        ActiveTab = tab;
        ActiveTab.IsSelected = true;
    }
    
    [RelayCommand]
    public void NextTab()
    {
        if (Tabs.Count <= 1 || ActiveTab == null)
            return;
            
        var currentIndex = Tabs.IndexOf(ActiveTab);
        var nextIndex = (currentIndex + 1) % Tabs.Count;
        SetActiveTab(Tabs[nextIndex]);
    }
    
    [RelayCommand]
    public void PreviousTab()
    {
        if (Tabs.Count <= 1 || ActiveTab == null)
            return;
            
        var currentIndex = Tabs.IndexOf(ActiveTab);
        var prevIndex = currentIndex == 0 ? Tabs.Count - 1 : currentIndex - 1;
        SetActiveTab(Tabs[prevIndex]);
    }
    
    [RelayCommand]
    public void CloseOtherTabs(TabViewModel? tab)
    {
        if (tab == null)
            return;
            
        var tabsToClose = Tabs.Where(t => t != tab && !t.IsLocked).ToList();
        foreach (var t in tabsToClose)
        {
            Tabs.Remove(t);
        }
        SetActiveTab(tab);
    }
    
    [RelayCommand]
    public void CloseTabsToRight(TabViewModel? tab)
    {
        if (tab == null)
            return;
            
        var index = Tabs.IndexOf(tab);
        var tabsToClose = Tabs.Skip(index + 1).Where(t => !t.IsLocked).ToList();
        foreach (var t in tabsToClose)
        {
            Tabs.Remove(t);
        }
    }
    
    /// <summary>
    /// Moves a tab from one index to another (for drag-and-drop reordering).
    /// </summary>
    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Tabs.Count || toIndex < 0 || toIndex >= Tabs.Count || fromIndex == toIndex)
            return;
            
        Tabs.Move(fromIndex, toIndex);
    }
    
    /// <summary>
    /// Moves a tab to a specific position.
    /// </summary>
    public void MoveTabTo(TabViewModel tab, int targetIndex)
    {
        var currentIndex = Tabs.IndexOf(tab);
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Tabs.Count || currentIndex == targetIndex)
            return;
            
        Tabs.Move(currentIndex, targetIndex);
    }

    public PanelState CreateSessionState(string position)
    {
        var panelState = new PanelState
        {
            Position = position,
            ActiveTabIndex = ActiveTab != null ? Tabs.IndexOf(ActiveTab) : 0,
            IsVisible = true,
            Tabs = Tabs.Select(tab => tab.CreateSessionState()).ToList()
        };

        if (panelState.Tabs.Count == 0)
        {
            panelState.ActiveTabIndex = 0;
            return panelState;
        }

        panelState.ActiveTabIndex = Math.Clamp(panelState.ActiveTabIndex, 0, panelState.Tabs.Count - 1);
        for (var i = 0; i < panelState.Tabs.Count; i++)
            panelState.Tabs[i].IsActive = i == panelState.ActiveTabIndex;

        return panelState;
    }

    public void RestoreFromSession(PanelState panelState, string fallbackPath)
    {
        Tabs.Clear();

        if (panelState.Tabs.Count == 0)
        {
            var tab = CreateNewTab();
            tab.NavigateTo(fallbackPath);
            SetActiveTab(tab);
            return;
        }

        foreach (var tabState in panelState.Tabs)
        {
            var tab = CreateNewTab();
            tab.ApplySessionState(tabState, fallbackPath);
        }

        var activeIndex = Math.Clamp(panelState.ActiveTabIndex, 0, Tabs.Count - 1);
        SetActiveTab(Tabs[activeIndex]);
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.ShowHiddenFiles) ||
            e.PropertyName == nameof(AppSettings.ShowSystemFiles))
        {
            foreach (var tab in Tabs)
            {
                tab.ShowHiddenFiles = _settings.ShowHiddenFiles;
                tab.ShowSystemFiles = _settings.ShowSystemFiles;
            }
        }
    }
    
    /// <summary>
    /// Adds a tab from another panel (for cross-panel drag-and-drop).
    /// </summary>
    public void AcceptTab(TabViewModel tab, int insertIndex = -1)
    {
        // Subscribe to navigation events
        tab.Navigated += (sender, path) => Navigated?.Invoke(this, path);
        
        if (insertIndex >= 0 && insertIndex <= Tabs.Count)
        {
            Tabs.Insert(insertIndex, tab);
        }
        else
        {
            Tabs.Add(tab);
        }
        SetActiveTab(tab);
    }
    
    /// <summary>
    /// Removes a tab without closing it (for cross-panel move).
    /// </summary>
    public bool DetachTab(TabViewModel tab)
    {
        if (Tabs.Count <= 1)
            return false; // Can't remove the last tab
            
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return false;
            
        Tabs.Remove(tab);
        
        // Select adjacent tab if we removed the active one
        if (ActiveTab == tab)
        {
            var newIndex = Math.Min(index, Tabs.Count - 1);
            SetActiveTab(Tabs[newIndex]);
        }
        
        return true;
    }
    
    // Delegate commands to active tab
    public void NavigateTo(string path) => ActiveTab?.NavigateTo(path);
    public void GoBack() => ActiveTab?.GoBack();
    public void GoForward() => ActiveTab?.GoForward();
    public void GoToParent() => ActiveTab?.GoToParent();
    public void GoToRoot() => ActiveTab?.GoToRoot();
    public void Refresh() => ActiveTab?.Refresh();
    public void ToggleHiddenFiles() => ActiveTab?.ToggleHiddenFiles();
    public void SelectAll() => ActiveTab?.SelectAll();
    public void DeselectAll() => ActiveTab?.DeselectAll();
    public void InvertSelection() => ActiveTab?.InvertSelection();
    public void SetViewMode(FilePanelViewMode mode) => ActiveTab?.SetViewMode(mode);
    public void CalculateDirectorySizes() => ActiveTab?.CalculateDirectorySizes();
    
    [RelayCommand]
    public async Task ShowDrives()
    {
        var drives = _fileSystemService.GetDrives().ToList();
        if (drives.Count == 0)
            return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new DriveSelectionDialog(drives);
            if (desktop.MainWindow != null)
            {
                await dialog.ShowDialog(desktop.MainWindow);
            }
            else
            {
                dialog.Show();
            }

            if (dialog.SelectedDrive != null)
            {
                ActiveTab?.NavigateTo(dialog.SelectedDrive.RootPath);
            }
            return;
        }

        ActiveTab?.NavigateTo(drives[0].RootPath);
    }
    
    public string CurrentPath => ActiveTab?.CurrentPath ?? string.Empty;
    public IEnumerable<string> GetSelectedPaths() => ActiveTab?.GetSelectedPaths() ?? [];
    public FileItemViewModel? SelectedItem => ActiveTab?.SelectedItem;
    public ObservableCollection<FileItemViewModel>? Items => ActiveTab?.Items;
    public ObservableCollection<FileItemViewModel>? SelectedItems => ActiveTab?.SelectedItems;
    public bool CanGoBack => ActiveTab?.CanGoBack ?? false;
    public bool CanGoForward => ActiveTab?.CanGoForward ?? false;
}
