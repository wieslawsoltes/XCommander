using System.Collections.ObjectModel;
using System.Linq;
using XCommander.Services;

namespace XCommander.ViewModels;

public abstract class UserMenuEntryViewModel : ViewModelBase
{
    public abstract bool IsSeparator { get; }
}

public sealed class UserMenuItemViewModel : UserMenuEntryViewModel
{
    public UserMenuItemViewModel(UserMenuItem model)
    {
        Model = model;
        if (model.SubItems != null)
        {
            foreach (var subItem in model.SubItems.OrderBy(item => item.Order))
            {
                SubItems.Add(UserMenuViewModelFactory.Create(subItem));
            }
        }
    }

    public UserMenuItem Model { get; }

    public string Label => Model.Label;

    public string? Icon => Model.Icon;

    public override bool IsSeparator => false;

    public bool IsEnabled => Model.IsEnabled;

    public bool IsVisible => Model.IsVisible;

    public ObservableCollection<UserMenuEntryViewModel> SubItems { get; } = new();
}

public sealed class UserMenuSeparatorViewModel : UserMenuEntryViewModel
{
    public override bool IsSeparator => true;
}

public static class UserMenuViewModelFactory
{
    public static UserMenuEntryViewModel Create(UserMenuItem item)
    {
        return item.Type == MenuItemType.Separator
            ? new UserMenuSeparatorViewModel()
            : new UserMenuItemViewModel(item);
    }
}
