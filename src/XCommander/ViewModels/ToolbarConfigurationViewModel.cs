using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;

namespace XCommander.ViewModels;

public partial class ToolbarButtonViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _label = string.Empty;
    
    [ObservableProperty]
    private string _icon = "⚙️";
    
    [ObservableProperty]
    private string _commandName = string.Empty;
    
    [ObservableProperty]
    private string? _tooltip;
    
    [ObservableProperty]
    private bool _isSeparator;
    
    [ObservableProperty]
    private int _order;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    public ToolbarButton ToModel() => new()
    {
        Id = Id,
        Label = Label,
        Icon = Icon,
        CommandName = CommandName,
        Tooltip = Tooltip,
        IsSeparator = IsSeparator,
        Order = Order,
        IsVisible = IsVisible
    };
    
    public static ToolbarButtonViewModel FromModel(ToolbarButton model) => new()
    {
        Id = model.Id,
        Label = model.Label,
        Icon = model.Icon,
        CommandName = model.CommandName,
        Tooltip = model.Tooltip,
        IsSeparator = model.IsSeparator,
        Order = model.Order,
        IsVisible = model.IsVisible
    };
}

public partial class ToolbarConfigurationViewModel : ViewModelBase
{
    private readonly ToolbarConfiguration _configuration;
    
    [ObservableProperty]
    private ObservableCollection<ToolbarButtonViewModel> _buttons = new();
    
    [ObservableProperty]
    private ToolbarButtonViewModel? _selectedButton;
    
    [ObservableProperty]
    private bool _showLabels;
    
    [ObservableProperty]
    private int _selectedSizeIndex;
    
    public string[] AvailableSizes { get; } = ["Small", "Medium", "Large"];
    
    public string[] AvailableCommands { get; } =
    [
        "GoBack",
        "GoForward",
        "GoToParent",
        "GoHome",
        "Refresh",
        "CopySelected",
        "MoveSelected",
        "DeleteSelected",
        "CreateNewFolder",
        "CreateNewFile",
        "Search",
        "NewTab",
        "ToggleBookmarks",
        "AddBookmark",
        "ToggleQuickView",
        "MultiRename",
        "CompareDirectories",
        "CompareFiles",
        "SyncDirectories",
        "CalculateChecksum",
        "EncodingTool",
        "SplitFile",
        "CombineFiles",
        "OpenArchive",
        "CreateArchive",
        "ExtractArchive",
        "FtpConnect",
        "SftpConnect",
        "Settings",
        "Help",
        "CommandPalette"
    ];
    
    public string[] AvailableIcons { get; } =
    [
        "⬅", "➡", "⬆", "🏠", "⟳", "📋", "✂️", "🗑️",
        "📁+", "📄+", "🔍", "📑+", "⭐", "⭐+", "👁️",
        "✏️", "📊", "📁", "📄", "📦", "🔗", "⚙️",
        "❓", "💡", "🌐", "🔒", "📤", "📥", "🔄"
    ];
    
    public event EventHandler? RequestClose;
    public event EventHandler? ConfigurationChanged;
    
    public ToolbarConfigurationViewModel()
    {
        _configuration = ToolbarConfiguration.Load();
        LoadFromConfiguration();
    }
    
    private void LoadFromConfiguration()
    {
        ShowLabels = _configuration.ShowLabels;
        SelectedSizeIndex = _configuration.Size switch
        {
            "Small" => 0,
            "Medium" => 1,
            "Large" => 2,
            _ => 1
        };
        
        Buttons.Clear();
        foreach (var button in _configuration.Buttons.OrderBy(b => b.Order))
        {
            Buttons.Add(ToolbarButtonViewModel.FromModel(button));
        }
    }
    
    [RelayCommand]
    public void AddButton()
    {
        var newButton = new ToolbarButtonViewModel
        {
            Id = Guid.NewGuid().ToString(),
            Label = "New Button",
            Icon = "⚙️",
            CommandName = "Help",
            Order = Buttons.Count + 1,
            IsVisible = true
        };
        
        Buttons.Add(newButton);
        SelectedButton = newButton;
    }
    
    [RelayCommand]
    public void AddSeparator()
    {
        var separator = new ToolbarButtonViewModel
        {
            Id = Guid.NewGuid().ToString(),
            IsSeparator = true,
            Order = Buttons.Count + 1,
            IsVisible = true
        };
        
        Buttons.Add(separator);
        SelectedButton = separator;
    }
    
    [RelayCommand]
    public void RemoveButton(ToolbarButtonViewModel? button)
    {
        if (button == null)
            return;
            
        Buttons.Remove(button);
        ReorderButtons();
    }
    
    [RelayCommand]
    public void MoveUp(ToolbarButtonViewModel? button)
    {
        if (button == null)
            return;
            
        var index = Buttons.IndexOf(button);
        if (index > 0)
        {
            Buttons.Move(index, index - 1);
            ReorderButtons();
        }
    }
    
    [RelayCommand]
    public void MoveDown(ToolbarButtonViewModel? button)
    {
        if (button == null)
            return;
            
        var index = Buttons.IndexOf(button);
        if (index < Buttons.Count - 1)
        {
            Buttons.Move(index, index + 1);
            ReorderButtons();
        }
    }
    
    private void ReorderButtons()
    {
        for (int i = 0; i < Buttons.Count; i++)
        {
            Buttons[i].Order = i + 1;
        }
    }
    
    [RelayCommand]
    public void ResetToDefault()
    {
        var defaultConfig = ToolbarConfiguration.CreateDefault();
        _configuration.Buttons = defaultConfig.Buttons;
        _configuration.ShowLabels = defaultConfig.ShowLabels;
        _configuration.Size = defaultConfig.Size;
        LoadFromConfiguration();
    }
    
    [RelayCommand]
    public void Save()
    {
        _configuration.Buttons = Buttons.Select(b => b.ToModel()).ToList();
        _configuration.ShowLabels = ShowLabels;
        _configuration.Size = AvailableSizes[SelectedSizeIndex];
        _configuration.Save();
        
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    public void SaveAndClose()
    {
        Save();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    public ToolbarConfiguration GetConfiguration()
    {
        return _configuration;
    }
}
