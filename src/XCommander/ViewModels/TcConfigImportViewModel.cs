using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class TcConfigImportViewModel : ViewModelBase
{
    private readonly ITcConfigImportService _importService;
    
    [ObservableProperty]
    private string _wincmdIniPath = string.Empty;
    
    [ObservableProperty]
    private bool _isValidated;
    
    [ObservableProperty]
    private bool _isImporting;
    
    [ObservableProperty]
    private bool _isImportComplete;
    
    [ObservableProperty]
    private string _statusText = "Select your Total Commander wincmd.ini file to import settings.";
    
    [ObservableProperty]
    private string _validationStatus = string.Empty;
    
    [ObservableProperty]
    private string _tcVersion = string.Empty;
    
    // Import options
    [ObservableProperty]
    private bool _importBookmarks = true;
    
    [ObservableProperty]
    private bool _importFtpConnections = true;
    
    [ObservableProperty]
    private bool _importColorSchemes = true;
    
    [ObservableProperty]
    private bool _importCustomColumns = true;
    
    [ObservableProperty]
    private bool _importButtonBar = true;
    
    [ObservableProperty]
    private bool _importUserMenu = true;
    
    // Validation results
    [ObservableProperty]
    private bool _hasBookmarks;
    
    [ObservableProperty]
    private bool _hasFtpConnections;
    
    [ObservableProperty]
    private bool _hasColorSchemes;
    
    [ObservableProperty]
    private bool _hasCustomColumns;
    
    [ObservableProperty]
    private bool _hasButtonBar;
    
    [ObservableProperty]
    private bool _hasUserMenu;
    
    // Import results
    [ObservableProperty]
    private int _bookmarksImported;
    
    [ObservableProperty]
    private int _ftpConnectionsImported;
    
    [ObservableProperty]
    private int _colorSchemesImported;
    
    [ObservableProperty]
    private int _customColumnsImported;
    
    [ObservableProperty]
    private int _buttonBarItemsImported;
    
    [ObservableProperty]
    private int _menuItemsImported;
    
    public ObservableCollection<string> Warnings { get; } = [];
    
    public event EventHandler? ImportSuccessful;
    
    public TcConfigImportViewModel(ITcConfigImportService importService)
    {
        _importService = importService;
    }
    
    partial void OnWincmdIniPathChanged(string value)
    {
        IsValidated = false;
        ValidationStatus = string.Empty;
        IsImportComplete = false;
        Warnings.Clear();
    }
    
    [RelayCommand]
    private void ValidateFile()
    {
        if (string.IsNullOrEmpty(WincmdIniPath))
        {
            ValidationStatus = "Please select a wincmd.ini file.";
            return;
        }
        
        if (!File.Exists(WincmdIniPath))
        {
            ValidationStatus = "File not found.";
            return;
        }
        
        var result = _importService.Validate(WincmdIniPath);
        
        if (!result.IsValid)
        {
            ValidationStatus = result.ErrorMessage ?? "Invalid file";
            IsValidated = false;
            return;
        }
        
        IsValidated = true;
        TcVersion = result.TcVersion;
        HasBookmarks = result.HasBookmarks;
        HasFtpConnections = result.HasFtpConnections;
        HasColorSchemes = result.HasColorSchemes;
        HasCustomColumns = result.HasCustomColumns;
        HasButtonBar = result.HasButtonBar;
        HasUserMenu = result.HasUserMenu;
        
        ValidationStatus = $"✓ Valid Total Commander config file (v{TcVersion})";
        StatusText = "Select which settings to import and click Import.";
    }
    
    [RelayCommand]
    private async Task ImportAsync()
    {
        if (!IsValidated)
            return;
            
        IsImporting = true;
        StatusText = "Importing settings...";
        Warnings.Clear();
        
        try
        {
            var result = await _importService.ImportAsync(WincmdIniPath);
            
            if (result.Success)
            {
                BookmarksImported = result.BookmarksImported;
                FtpConnectionsImported = result.FtpConnectionsImported;
                ColorSchemesImported = result.ColorSchemesImported;
                CustomColumnsImported = result.CustomColumnsImported;
                ButtonBarItemsImported = result.ButtonBarItemsImported;
                MenuItemsImported = result.MenuItemsImported;
                
                foreach (var warning in result.Warnings)
                {
                    Warnings.Add(warning);
                }
                
                IsImportComplete = true;
                StatusText = "Import complete!";
                ImportSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusText = $"Import failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Import error: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }
}
