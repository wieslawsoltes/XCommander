using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Provides navigation from settings to configuration dialogs.
/// </summary>
public interface ISettingsNavigationService
{
    /// <summary>
    /// Opens the keyboard shortcuts dialog.
    /// </summary>
    Task OpenKeyboardShortcutsAsync();

    /// <summary>
    /// Opens the toolbar configuration dialog.
    /// </summary>
    Task OpenToolbarConfigurationAsync();

    /// <summary>
    /// Opens the custom columns dialog.
    /// </summary>
    Task OpenCustomColumnsAsync();

    /// <summary>
    /// Opens the file coloring settings dialog.
    /// </summary>
    Task OpenFileColoringAsync();

    /// <summary>
    /// Opens the file associations dialog.
    /// </summary>
    Task OpenFileAssociationsAsync();

    /// <summary>
    /// Opens the plugins dialog.
    /// </summary>
    Task OpenPluginsAsync();

    /// <summary>
    /// Opens the Total Commander config import dialog.
    /// </summary>
    Task OpenTcConfigImportAsync();
}
