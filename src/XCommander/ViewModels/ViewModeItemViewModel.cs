using CommunityToolkit.Mvvm.ComponentModel;
using ServicesViewMode = XCommander.Services.ViewMode;

namespace XCommander.ViewModels;

public partial class ViewModeItemViewModel : ViewModelBase
{
    public ViewModeItemViewModel(ServicesViewMode viewMode)
    {
        ViewMode = viewMode;
    }

    public ServicesViewMode ViewMode { get; }

    public string Name => ViewMode.Name;

    public string? Description => ViewMode.Description;

    [ObservableProperty]
    private bool _isActive;
}
