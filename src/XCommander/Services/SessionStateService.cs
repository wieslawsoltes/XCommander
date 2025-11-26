using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCommander.Services;

/// <summary>
/// State of a single file panel tab
/// </summary>
public class TabState
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Path { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsActive { get; set; }
    public int SortColumn { get; set; }
    public bool SortAscending { get; set; } = true;
    public string? ViewMode { get; set; } = "Details"; // Details, List, Thumbnails
    public List<string>? SelectedItems { get; set; }
    public string? FocusedItem { get; set; }
    public double ScrollPosition { get; set; }
    public Dictionary<string, double>? ColumnWidths { get; set; }
    public bool ShowHidden { get; set; }
    public string? Filter { get; set; }
    public List<string>? History { get; set; }
    public int HistoryPosition { get; set; }
}

/// <summary>
/// State of a file panel (left or right)
/// </summary>
public class PanelState
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Position { get; set; } = "Left"; // Left, Right
    public List<TabState> Tabs { get; set; } = new();
    public int ActiveTabIndex { get; set; }
    public double Width { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// State of the main window
/// </summary>
public class WindowState
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public bool IsMaximized { get; set; }
    public string? DisplayId { get; set; }
}

/// <summary>
/// User preferences
/// </summary>
public class UserPreferences
{
    public string? Theme { get; set; } = "Dark";
    public string? FontFamily { get; set; }
    public double FontSize { get; set; } = 12;
    public bool ConfirmDelete { get; set; } = true;
    public bool ConfirmOverwrite { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowCommandLine { get; set; }
    public bool ShowButtonBar { get; set; } = true;
    public bool ShowDriveButtons { get; set; } = true;
    public int MaxRecentItems { get; set; } = 20;
    public int MaxHistoryItems { get; set; } = 50;
    public bool SaveOnExit { get; set; } = true;
    public bool RestoreOnStartup { get; set; } = true;
    public string? DefaultEditor { get; set; }
    public string? DefaultViewer { get; set; }
    public string? TerminalCommand { get; set; }
    public Dictionary<string, string>? FileAssociations { get; set; }
    public Dictionary<string, object>? CustomSettings { get; set; }
}

/// <summary>
/// Complete application session state
/// </summary>
public class SessionState
{
    public int Version { get; set; } = 1;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public WindowState Window { get; set; } = new();
    public PanelState LeftPanel { get; set; } = new() { Position = "Left" };
    public PanelState RightPanel { get; set; } = new() { Position = "Right" };
    public string? ActivePanel { get; set; } = "Left";
    public double SplitterPosition { get; set; } = 0.5;
    public UserPreferences Preferences { get; set; } = new();
    public List<string> RecentPaths { get; set; } = new();
    public List<string> CommandHistory { get; set; } = new();
    public Dictionary<string, object>? ExtendedState { get; set; }
}

/// <summary>
/// Event args for state change events
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public string PropertyName { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
}

/// <summary>
/// Service for persisting and restoring application session state
/// </summary>
public interface ISessionStateService
{
    /// <summary>
    /// Event raised when state changes
    /// </summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Get the current session state
    /// </summary>
    SessionState CurrentState { get; }
    
    /// <summary>
    /// Get user preferences
    /// </summary>
    UserPreferences Preferences { get; }
    
    /// <summary>
    /// Load session state from disk
    /// </summary>
    Task<SessionState> LoadAsync();
    
    /// <summary>
    /// Save current session state to disk
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Save session state immediately (synchronous for shutdown)
    /// </summary>
    void SaveImmediate();
    
    /// <summary>
    /// Update window state
    /// </summary>
    void UpdateWindowState(WindowState windowState);
    
    /// <summary>
    /// Update panel state
    /// </summary>
    void UpdatePanelState(PanelState panelState);
    
    /// <summary>
    /// Update active panel
    /// </summary>
    void SetActivePanel(string panelId);
    
    /// <summary>
    /// Update splitter position
    /// </summary>
    void SetSplitterPosition(double position);
    
    /// <summary>
    /// Add a tab to a panel
    /// </summary>
    TabState AddTab(string panelId, string path, bool activate = true);
    
    /// <summary>
    /// Remove a tab from a panel
    /// </summary>
    bool RemoveTab(string panelId, string tabId);
    
    /// <summary>
    /// Update tab state
    /// </summary>
    void UpdateTabState(string panelId, TabState tabState);
    
    /// <summary>
    /// Set active tab in a panel
    /// </summary>
    void SetActiveTab(string panelId, int tabIndex);
    
    /// <summary>
    /// Add path to recent paths
    /// </summary>
    void AddRecentPath(string path);
    
    /// <summary>
    /// Get recent paths
    /// </summary>
    IReadOnlyList<string> GetRecentPaths();
    
    /// <summary>
    /// Clear recent paths
    /// </summary>
    void ClearRecentPaths();
    
    /// <summary>
    /// Add command to history
    /// </summary>
    void AddCommandHistory(string command);
    
    /// <summary>
    /// Get command history
    /// </summary>
    IReadOnlyList<string> GetCommandHistory();
    
    /// <summary>
    /// Clear command history
    /// </summary>
    void ClearCommandHistory();
    
    /// <summary>
    /// Update user preferences
    /// </summary>
    void UpdatePreferences(Action<UserPreferences> updateAction);
    
    /// <summary>
    /// Set a custom preference value
    /// </summary>
    void SetCustomSetting(string key, object value);
    
    /// <summary>
    /// Get a custom preference value
    /// </summary>
    T? GetCustomSetting<T>(string key, T? defaultValue = default);
    
    /// <summary>
    /// Reset to default state
    /// </summary>
    void ResetToDefaults();
    
    /// <summary>
    /// Export state to file
    /// </summary>
    Task ExportAsync(string filePath);
    
    /// <summary>
    /// Import state from file
    /// </summary>
    Task ImportAsync(string filePath);
    
    /// <summary>
    /// Check if there are unsaved changes
    /// </summary>
    bool HasUnsavedChanges { get; }
    
    /// <summary>
    /// Mark state as dirty (needs saving)
    /// </summary>
    void MarkDirty();
}

public class SessionStateService : ISessionStateService
{
    private readonly string _stateFilePath;
    private SessionState _state = new();
    private bool _isDirty;
    private readonly object _lock = new();
    private DateTime _lastAutoSave = DateTime.MinValue;
    private readonly TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(1);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public event EventHandler<StateChangedEventArgs>? StateChanged;
    
    public SessionState CurrentState
    {
        get
        {
            lock (_lock) return _state;
        }
    }
    
    public UserPreferences Preferences
    {
        get
        {
            lock (_lock) return _state.Preferences;
        }
    }
    
    public bool HasUnsavedChanges => _isDirty;
    
    public SessionStateService(string? stateFilePath = null)
    {
        _stateFilePath = stateFilePath ?? GetDefaultStatePath();
    }
    
    private static string GetDefaultStatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XCommander", "session.json");
    }
    
    public async Task<SessionState> LoadAsync()
    {
        if (!File.Exists(_stateFilePath))
        {
            InitializeDefaultState();
            return _state;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath);
            var state = JsonSerializer.Deserialize<SessionState>(json, JsonOptions);
            
            lock (_lock)
            {
                _state = state ?? new SessionState();
                _isDirty = false;
            }
            
            // Validate loaded state
            EnsureValidState();
            
            return _state;
        }
        catch
        {
            InitializeDefaultState();
            return _state;
        }
    }
    
    public async Task SaveAsync()
    {
        if (!_isDirty && (DateTime.UtcNow - _lastAutoSave) < _autoSaveInterval)
            return;
        
        SessionState stateToSave;
        
        lock (_lock)
        {
            _state.SavedAt = DateTime.UtcNow;
            stateToSave = JsonSerializer.Deserialize<SessionState>(
                JsonSerializer.Serialize(_state, JsonOptions), 
                JsonOptions)!;
            _isDirty = false;
            _lastAutoSave = DateTime.UtcNow;
        }
        
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        var json = JsonSerializer.Serialize(stateToSave, JsonOptions);
        await File.WriteAllTextAsync(_stateFilePath, json);
    }
    
    public void SaveImmediate()
    {
        try
        {
            lock (_lock)
            {
                _state.SavedAt = DateTime.UtcNow;
                
                var directory = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                
                var json = JsonSerializer.Serialize(_state, JsonOptions);
                File.WriteAllText(_stateFilePath, json);
                _isDirty = false;
            }
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }
    
    public void UpdateWindowState(WindowState windowState)
    {
        lock (_lock)
        {
            var old = _state.Window;
            _state.Window = windowState;
            _isDirty = true;
            OnStateChanged(nameof(SessionState.Window), old, windowState);
        }
    }
    
    public void UpdatePanelState(PanelState panelState)
    {
        lock (_lock)
        {
            if (panelState.Position == "Left")
            {
                var old = _state.LeftPanel;
                _state.LeftPanel = panelState;
                OnStateChanged(nameof(SessionState.LeftPanel), old, panelState);
            }
            else
            {
                var old = _state.RightPanel;
                _state.RightPanel = panelState;
                OnStateChanged(nameof(SessionState.RightPanel), old, panelState);
            }
            _isDirty = true;
        }
    }
    
    public void SetActivePanel(string panelId)
    {
        lock (_lock)
        {
            var old = _state.ActivePanel;
            _state.ActivePanel = panelId;
            _isDirty = true;
            OnStateChanged(nameof(SessionState.ActivePanel), old, panelId);
        }
    }
    
    public void SetSplitterPosition(double position)
    {
        lock (_lock)
        {
            var old = _state.SplitterPosition;
            _state.SplitterPosition = position;
            _isDirty = true;
            OnStateChanged(nameof(SessionState.SplitterPosition), old, position);
        }
    }
    
    public TabState AddTab(string panelId, string path, bool activate = true)
    {
        var tab = new TabState
        {
            Path = path,
            Title = Path.GetFileName(path),
            IsActive = activate
        };
        
        lock (_lock)
        {
            var panel = panelId == "Left" ? _state.LeftPanel : _state.RightPanel;
            
            if (activate)
            {
                foreach (var existingTab in panel.Tabs)
                    existingTab.IsActive = false;
            }
            
            panel.Tabs.Add(tab);
            
            if (activate)
                panel.ActiveTabIndex = panel.Tabs.Count - 1;
            
            _isDirty = true;
        }
        
        OnStateChanged("Tab.Added", null, tab);
        return tab;
    }
    
    public bool RemoveTab(string panelId, string tabId)
    {
        lock (_lock)
        {
            var panel = panelId == "Left" ? _state.LeftPanel : _state.RightPanel;
            var tab = panel.Tabs.FirstOrDefault(t => t.Id == tabId);
            
            if (tab == null)
                return false;
            
            var index = panel.Tabs.IndexOf(tab);
            panel.Tabs.Remove(tab);
            
            // Adjust active tab index
            if (panel.ActiveTabIndex >= panel.Tabs.Count)
                panel.ActiveTabIndex = Math.Max(0, panel.Tabs.Count - 1);
            
            // Ensure we always have at least one tab
            if (!panel.Tabs.Any())
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                AddTab(panelId, homeDir, true);
            }
            
            _isDirty = true;
            OnStateChanged("Tab.Removed", tab, null);
            return true;
        }
    }
    
    public void UpdateTabState(string panelId, TabState tabState)
    {
        lock (_lock)
        {
            var panel = panelId == "Left" ? _state.LeftPanel : _state.RightPanel;
            var existingTab = panel.Tabs.FirstOrDefault(t => t.Id == tabState.Id);
            
            if (existingTab != null)
            {
                var index = panel.Tabs.IndexOf(existingTab);
                panel.Tabs[index] = tabState;
                _isDirty = true;
                OnStateChanged("Tab.Updated", existingTab, tabState);
            }
        }
    }
    
    public void SetActiveTab(string panelId, int tabIndex)
    {
        lock (_lock)
        {
            var panel = panelId == "Left" ? _state.LeftPanel : _state.RightPanel;
            
            if (tabIndex < 0 || tabIndex >= panel.Tabs.Count)
                return;
            
            var oldIndex = panel.ActiveTabIndex;
            panel.ActiveTabIndex = tabIndex;
            
            for (var i = 0; i < panel.Tabs.Count; i++)
                panel.Tabs[i].IsActive = i == tabIndex;
            
            _isDirty = true;
            OnStateChanged("Tab.Activated", oldIndex, tabIndex);
        }
    }
    
    public void AddRecentPath(string path)
    {
        lock (_lock)
        {
            // Remove if already exists
            _state.RecentPaths.Remove(path);
            
            // Add to front
            _state.RecentPaths.Insert(0, path);
            
            // Trim to max
            var max = _state.Preferences.MaxRecentItems;
            if (_state.RecentPaths.Count > max)
                _state.RecentPaths.RemoveRange(max, _state.RecentPaths.Count - max);
            
            _isDirty = true;
        }
    }
    
    public IReadOnlyList<string> GetRecentPaths()
    {
        lock (_lock)
        {
            return _state.RecentPaths.ToList();
        }
    }
    
    public void ClearRecentPaths()
    {
        lock (_lock)
        {
            _state.RecentPaths.Clear();
            _isDirty = true;
        }
    }
    
    public void AddCommandHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;
        
        lock (_lock)
        {
            // Remove if already exists
            _state.CommandHistory.Remove(command);
            
            // Add to front
            _state.CommandHistory.Insert(0, command);
            
            // Trim to max
            var max = _state.Preferences.MaxHistoryItems;
            if (_state.CommandHistory.Count > max)
                _state.CommandHistory.RemoveRange(max, _state.CommandHistory.Count - max);
            
            _isDirty = true;
        }
    }
    
    public IReadOnlyList<string> GetCommandHistory()
    {
        lock (_lock)
        {
            return _state.CommandHistory.ToList();
        }
    }
    
    public void ClearCommandHistory()
    {
        lock (_lock)
        {
            _state.CommandHistory.Clear();
            _isDirty = true;
        }
    }
    
    public void UpdatePreferences(Action<UserPreferences> updateAction)
    {
        lock (_lock)
        {
            updateAction(_state.Preferences);
            _isDirty = true;
            OnStateChanged(nameof(SessionState.Preferences), null, _state.Preferences);
        }
    }
    
    public void SetCustomSetting(string key, object value)
    {
        lock (_lock)
        {
            _state.Preferences.CustomSettings ??= new Dictionary<string, object>();
            _state.Preferences.CustomSettings[key] = value;
            _isDirty = true;
        }
    }
    
    public T? GetCustomSetting<T>(string key, T? defaultValue = default)
    {
        lock (_lock)
        {
            if (_state.Preferences.CustomSettings?.TryGetValue(key, out var value) == true)
            {
                if (value is JsonElement element)
                {
                    return element.Deserialize<T>(JsonOptions);
                }
                
                if (value is T typedValue)
                    return typedValue;
            }
            
            return defaultValue;
        }
    }
    
    public void ResetToDefaults()
    {
        InitializeDefaultState();
        _isDirty = true;
        OnStateChanged("Reset", null, _state);
    }
    
    public async Task ExportAsync(string filePath)
    {
        SessionState stateToExport;
        
        lock (_lock)
        {
            stateToExport = JsonSerializer.Deserialize<SessionState>(
                JsonSerializer.Serialize(_state, JsonOptions),
                JsonOptions)!;
        }
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        var json = JsonSerializer.Serialize(stateToExport, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importedState = JsonSerializer.Deserialize<SessionState>(json, JsonOptions);
        
        if (importedState == null)
            throw new InvalidOperationException("Failed to deserialize state file");
        
        lock (_lock)
        {
            _state = importedState;
            _isDirty = true;
        }
        
        EnsureValidState();
        OnStateChanged("Import", null, _state);
    }
    
    public void MarkDirty()
    {
        _isDirty = true;
    }
    
    private void InitializeDefaultState()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        lock (_lock)
        {
            _state = new SessionState
            {
                LeftPanel = new PanelState
                {
                    Position = "Left",
                    Tabs = new List<TabState>
                    {
                        new() { Path = homeDir, Title = "Home", IsActive = true }
                    },
                    ActiveTabIndex = 0,
                    IsVisible = true
                },
                RightPanel = new PanelState
                {
                    Position = "Right",
                    Tabs = new List<TabState>
                    {
                        new() { Path = homeDir, Title = "Home", IsActive = true }
                    },
                    ActiveTabIndex = 0,
                    IsVisible = true
                },
                ActivePanel = "Left",
                SplitterPosition = 0.5,
                Window = new WindowState
                {
                    Width = 1200,
                    Height = 800
                },
                Preferences = new UserPreferences()
            };
        }
    }
    
    private void EnsureValidState()
    {
        lock (_lock)
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // Ensure left panel has at least one tab
            if (!_state.LeftPanel.Tabs.Any())
            {
                _state.LeftPanel.Tabs.Add(new TabState { Path = homeDir, Title = "Home", IsActive = true });
                _state.LeftPanel.ActiveTabIndex = 0;
            }
            
            // Ensure right panel has at least one tab
            if (!_state.RightPanel.Tabs.Any())
            {
                _state.RightPanel.Tabs.Add(new TabState { Path = homeDir, Title = "Home", IsActive = true });
                _state.RightPanel.ActiveTabIndex = 0;
            }
            
            // Ensure active tab indices are valid
            if (_state.LeftPanel.ActiveTabIndex >= _state.LeftPanel.Tabs.Count)
                _state.LeftPanel.ActiveTabIndex = 0;
            
            if (_state.RightPanel.ActiveTabIndex >= _state.RightPanel.Tabs.Count)
                _state.RightPanel.ActiveTabIndex = 0;
            
            // Ensure window size is reasonable
            if (_state.Window.Width < 400)
                _state.Window.Width = 1200;
            
            if (_state.Window.Height < 300)
                _state.Window.Height = 800;
            
            // Ensure preferences exist
            _state.Preferences ??= new UserPreferences();
        }
    }
    
    private void OnStateChanged(string propertyName, object? oldValue, object? newValue)
    {
        StateChanged?.Invoke(this, new StateChangedEventArgs
        {
            PropertyName = propertyName,
            OldValue = oldValue,
            NewValue = newValue
        });
    }
}
