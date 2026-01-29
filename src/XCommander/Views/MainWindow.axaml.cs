using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaMenuItem = Avalonia.Controls.MenuItem;
using AvaloniaSeparator = Avalonia.Controls.Separator;
using XCommander.Controls;
using XCommander.Models;
using XCommander.Plugins;
using XCommander.Services;
using XCommander.ViewModels;
using XCommander.Views.Dialogs;

namespace XCommander.Views;

public partial class MainWindow : Window
{
    private INotificationService? _notificationService;
    private ISelectionHistoryService? _selectionHistoryService;
    private IDirectoryHotlistService? _directoryHotlistService;
    private IAppSettingsService? _settingsService;
    private ISettingsNavigationService? _settingsNavigationService;
    private ContextMenu? _hotlistMenu;
    private Dictionary<string, HotlistItem> _hotlistShortcutMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _hotlistMenuOpen;
    
    public MainWindow()
    {
        InitializeComponent();
        
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
    }
    
    /// <summary>
    /// Initializes services that require dependency injection.
    /// </summary>
    public void InitializeServices(
        INotificationService? notificationService,
        ISelectionHistoryService? selectionHistoryService,
        IDirectoryHotlistService? directoryHotlistService,
        IAppSettingsService? settingsService,
        ISettingsNavigationService? settingsNavigationService)
    {
        _notificationService = notificationService;
        _selectionHistoryService = selectionHistoryService;
        _directoryHotlistService = directoryHotlistService;
        _settingsService = settingsService;
        _settingsNavigationService = settingsNavigationService;
        
        // Bind notification overlay to service
        if (_notificationService != null)
        {
            NotificationOverlay.BindToService(_notificationService);
        }
    }
    
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _settingsService?.Save();
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ViewFileRequested += OnViewFileRequested;
            vm.MultiRenameRequested += OnMultiRenameRequested;
            vm.FtpConnectionRequested += OnFtpConnectionRequested;
            vm.ConfirmationRequested += OnConfirmationRequested;
            vm.InputRequested += OnInputRequested;
            vm.DirectoryCompareRequested += OnDirectoryCompareRequested;
            vm.FileCompareRequested += OnFileCompareRequested;
            vm.ChecksumRequested += OnChecksumRequested;
            vm.SettingsRequested += OnSettingsRequested;
            vm.KeyboardShortcutsRequested += OnKeyboardShortcutsRequested;
            vm.FileAssociationsRequested += OnFileAssociationsRequested;
            vm.OpenArchiveRequested += OnOpenArchiveRequested;
            vm.CreateArchiveRequested += OnCreateArchiveRequested;
            vm.ExtractArchiveRequested += OnExtractArchiveRequested;
            vm.DirectorySyncRequested += OnDirectorySyncRequested;
            vm.SplitFileRequested += OnSplitFileRequested;
            vm.CombineFilesRequested += OnCombineFilesRequested;
            vm.SftpConnectionRequested += OnSftpConnectionRequested;
            vm.AboutRequested += OnAboutRequested;
            vm.PluginsRequested += OnPluginsRequested;
            vm.CustomColumnsRequested += OnCustomColumnsRequested;
            vm.EncodingToolRequested += OnEncodingToolRequested;
            vm.ToolbarConfigurationRequested += OnToolbarConfigRequested;
            vm.HelpRequested += OnHelpRequested;
            vm.BranchViewRequested += OnBranchViewRequested;
            vm.SearchRequested += OnSearchRequested;
            
            // TC-style dialog events
            vm.CopyMoveDialogRequested += OnCopyMoveDialogRequested;
            vm.DeleteConfirmationRequested += OnDeleteConfirmationRequested;
            
            // Subscribe to command palette close event
            vm.CommandPalette.RequestClose += (_, _) => vm.CloseCommandPaletteCommand.Execute(null);
            
            // Subscribe to bookmarks navigation
            vm.Bookmarks.NavigateRequested += (_, path) => vm.NavigateToBookmark(path);
            
            // Initialize plugins
            _ = vm.InitializePluginsAsync();
        }
    }
    
    private async void OnToolbarConfigRequested(object? sender, EventArgs e)
    {
        await ShowToolbarConfigDialogAsync();
        
        // Reload toolbar after configuration dialog closes
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LoadToolbarConfiguration();
        }
    }
    
    private async Task ShowToolbarConfigDialogAsync()
    {
        var vm = new ToolbarConfigurationViewModel();
        
        var dialog = new ToolbarConfigurationDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Configure Toolbar",
            Content = dialog,
            Width = 700,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        vm.RequestClose += (_, _) => window.Close();
        
        await window.ShowDialog(this);
    }
    
    private async void OnHelpRequested(object? sender, EventArgs e)
    {
        await ShowHelpDialogAsync();
    }

    private async void OnSearchRequested(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await ShowSearchDialogAsync(vm);
        }
    }
    
    private async Task ShowHelpDialogAsync()
    {
        var vm = new HelpViewModel();
        
        var dialog = new HelpDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "XCommander Help",
            Content = dialog,
            Width = 850,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        vm.RequestClose += (_, _) => window.Close();
        
        await window.ShowDialog(this);
    }
    
    private async void OnEncodingToolRequested(object? sender, EventArgs e)
    {
        await ShowEncodingToolDialogAsync();
    }
    
    private async Task ShowEncodingToolDialogAsync()
    {
        var vm = new EncodingToolViewModel();
        
        var dialog = new EncodingToolDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Encoding Tool",
            Content = dialog,
            Width = 650,
            Height = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        vm.RequestClose += (_, _) => window.Close();
        
        await window.ShowDialog(this);
    }
    
    private async void OnConfirmationRequested(object? sender, MainWindowViewModel.ConfirmationEventArgs e)
    {
        var result = await ShowConfirmDialogAsync(e.Title, e.Message);
        e.Callback(result);
    }
    
    private async void OnCopyMoveDialogRequested(object? sender, CopyMoveDialogEventArgs e)
    {
        var result = await ShowCopyMoveDialogAsync(e.SourcePaths, e.DestinationFolder, e.IsCopy);
        e.Callback(result);
    }
    
    private async void OnDeleteConfirmationRequested(object? sender, DeleteConfirmationEventArgs e)
    {
        var result = await ShowDeleteConfirmationDialogAsync(e.SourcePaths);
        e.Callback(result);
    }
    
    private async Task<CopyMoveDialogResult?> ShowCopyMoveDialogAsync(
        List<string> sourcePaths, 
        string destinationFolder, 
        bool isCopy)
    {
        var vm = new CopyMoveDialogViewModel();
        vm.Initialize(sourcePaths, destinationFolder, isCopy ? ViewModels.FileOperationType.Copy : ViewModels.FileOperationType.Move);
        
        var dialog = new CopyMoveDialog
        {
            DataContext = vm
        };
        
        CopyMoveDialogResult? result = null;
        vm.Confirmed += (_, _) =>
        {
            result = new CopyMoveDialogResult
            {
                Confirmed = true,
                DestinationPath = vm.DestinationPath,
                PreserveDateTime = vm.PreserveDateTime,
                PreserveAttributes = vm.PreserveAttributes,
                VerifyAfterCopy = vm.VerifyAfterCopy,
                OverwriteMode = vm.OverwriteMode,
                UseBackgroundTransfer = vm.UseBackgroundTransfer || vm.QueueOperation,
                LowPriority = vm.LowPriority
            };
            dialog.Close();
        };
        
        vm.Cancelled += (_, _) =>
        {
            result = null;
            dialog.Close();
        };
        
        await dialog.ShowDialog(this);
        return result;
    }
    
    private async Task<DeleteConfirmationResult?> ShowDeleteConfirmationDialogAsync(List<string> sourcePaths)
    {
        var vm = new DeleteConfirmationViewModel();
        
        // Populate file list
        foreach (var path in sourcePaths)
        {
            var isDirectory = Directory.Exists(path);
            var info = new DeleteItemInfo
            {
                FullPath = path,
                Name = Path.GetFileName(path),
                IsDirectory = isDirectory,
            };
            
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                info.Size = fi.Length;
                info.SizeFormatted = FormatFileSize(fi.Length);
                info.DateModified = fi.LastWriteTime;
            }
            else if (isDirectory)
            {
                info.Size = 0;
                info.SizeFormatted = "<DIR>";
                info.DateModified = new DirectoryInfo(path).LastWriteTime;
            }
            
            vm.Items.Add(info);
        }
        
        var dialog = new DeleteConfirmationDialog
        {
            DataContext = vm
        };
        
        DeleteConfirmationResult? result = null;
        vm.Confirmed += (_, _) =>
        {
            result = new DeleteConfirmationResult
            {
                Confirmed = true,
                DeleteMode = vm.IsSecureDeleteMode ? DeleteMode.SecureDelete 
                           : vm.IsPermanentMode ? DeleteMode.Permanent 
                           : DeleteMode.RecycleBin,
                WipePassCount = vm.WipePassCount,
                DeleteReadOnly = vm.DeleteReadOnly,
                DeleteHidden = vm.DeleteHidden
            };
            dialog.Close();
        };
        
        vm.Cancelled += (_, _) =>
        {
            result = null;
            dialog.Close();
        };
        
        await dialog.ShowDialog(this);
        return result;
    }
    
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
    
    private async void OnInputRequested(object? sender, MainWindowViewModel.InputEventArgs e)
    {
        var result = await ShowInputDialogAsync(e.Title, e.Prompt, e.DefaultValue);
        e.Callback(result);
    }
    
    private async void OnViewFileRequested(object? sender, string filePath)
    {
        await ShowFileViewerAsync(filePath);
    }
    
    private async void OnMultiRenameRequested(object? sender, List<string> filePaths)
    {
        await ShowMultiRenameDialogAsync(filePaths);
    }
    
    private async void OnFtpConnectionRequested(object? sender, EventArgs e)
    {
        await ShowFtpDialogAsync();
    }
    
    private async void OnDirectoryCompareRequested(object? sender, MainWindowViewModel.DirectoryCompareEventArgs e)
    {
        await ShowDirectoryCompareDialogAsync(e.LeftPath, e.RightPath);
    }
    
    private async Task ShowDirectoryCompareDialogAsync(string leftPath, string rightPath)
    {
        var vm = new DirectoryCompareViewModel(new Services.FileSystemService())
        {
            LeftPath = leftPath,
            RightPath = rightPath
        };
        
        var dialog = new DirectoryCompareDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Directory Comparison",
            Content = dialog,
            Width = 950,
            Height = 650,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
    }
    
    private async void OnFileCompareRequested(object? sender, MainWindowViewModel.FileCompareEventArgs e)
    {
        await ShowFileDiffDialogAsync(e.LeftPath, e.RightPath);
    }
    
    private async Task ShowFileDiffDialogAsync(string? leftPath, string? rightPath)
    {
        var vm = new FileDiffViewModel();
        vm.LeftFilePath = leftPath ?? string.Empty;
        vm.RightFilePath = rightPath ?? string.Empty;
        
        var dialog = new FileDiffDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "File Comparison (DiffPlex)",
            Content = dialog,
            Width = 1100,
            Height = 750,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        // Auto-compare if both paths provided
        if (!string.IsNullOrEmpty(leftPath) && !string.IsNullOrEmpty(rightPath))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let UI render
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.ComputeDiffCommand.Execute(null);
                });
            });
        }
        
        await window.ShowDialog(this);
    }
    
    // Keep old dialog available for legacy use
    private async Task ShowFileCompareDialogAsync(string? leftPath, string? rightPath)
    {
        var vm = new FileCompareViewModel();
        vm.Initialize(leftPath, rightPath);
        
        var dialog = new FileCompareDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "File Comparison",
            Content = dialog,
            Width = 1050,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
    }
    
    private async void OnChecksumRequested(object? sender, List<string> filePaths)
    {
        await ShowChecksumDialogAsync(filePaths);
    }
    
    private async Task ShowChecksumDialogAsync(List<string> filePaths)
    {
        var vm = new ChecksumViewModel();
        vm.Initialize(filePaths);
        
        var dialog = new ChecksumDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Checksum Calculator",
            Content = dialog,
            Width = 750,
            Height = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
    }
    
    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        await ShowSettingsDialogAsync();
    }
    
    private async void OnKeyboardShortcutsRequested(object? sender, EventArgs e)
    {
        var manager = new KeyboardShortcutManager();
        var dialog = new KeyboardShortcutsDialog
        {
            DataContext = manager
        };
        await dialog.ShowDialog(this);
    }
    
    private async void OnFileAssociationsRequested(object? sender, EventArgs e)
    {
        var manager = new FileAssociationManager();
        var dialog = new FileAssociationsDialog
        {
            DataContext = manager
        };
        await dialog.ShowDialog(this);
    }
    
    private async void OnOpenArchiveRequested(object? sender, MainWindowViewModel.ArchiveEventArgs e)
    {
        await ShowOpenArchiveDialogAsync(e.ArchivePath, e.ExtractPath);
    }
    
    private async void OnCreateArchiveRequested(object? sender, MainWindowViewModel.CreateArchiveEventArgs e)
    {
        await ShowCreateArchiveDialogAsync(e.SourcePaths, e.DestinationPath);
    }
    
    private async void OnExtractArchiveRequested(object? sender, MainWindowViewModel.ArchiveEventArgs e)
    {
        await ExtractArchiveAsync(e.ArchivePath, e.ExtractPath);
    }
    
    private async Task ShowOpenArchiveDialogAsync(string archivePath, string extractPath)
    {
        var archiveService = new Services.ArchiveService();
        var vm = new ArchiveViewModel(archiveService);
        await vm.LoadArchiveAsync(archivePath);
        
        var dialog = new ArchiveDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = $"Archive: {System.IO.Path.GetFileName(archivePath)}",
            Content = dialog,
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panel if files were extracted
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
            mainVm.InactivePanel.Refresh();
        }
    }
    
    private async Task ShowCreateArchiveDialogAsync(List<string> sourcePaths, string destinationPath)
    {
        var archiveService = new Services.ArchiveService();
        var vm = new CreateArchiveViewModel(archiveService);
        vm.Initialize(sourcePaths, destinationPath);
        
        var dialog = new CreateArchiveDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Create Archive",
            Content = dialog,
            Width = 700,
            Height = 550,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panel after creating archive
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
        }
    }
    
    private async Task ExtractArchiveAsync(string archivePath, string extractPath)
    {
        try
        {
            var archiveService = new Services.ArchiveService();
            var vm = new ArchiveViewModel(archiveService);
            await vm.LoadArchiveAsync(archivePath);
            
            // Extract all files to the inactive panel's path
            await vm.ExtractAllAsync(extractPath);
            
            // Refresh panels
            if (DataContext is MainWindowViewModel mainVm)
            {
                mainVm.ActivePanel.Refresh();
                mainVm.InactivePanel.Refresh();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error extracting archive: {ex.Message}");
        }
    }
    
    private async void OnDirectorySyncRequested(object? sender, MainWindowViewModel.DirectoryCompareEventArgs e)
    {
        await ShowDirectorySyncDialogAsync(e.LeftPath, e.RightPath);
    }
    
    private async Task ShowDirectorySyncDialogAsync(string leftPath, string rightPath)
    {
        var vm = new DirectorySyncViewModel(new Services.FileSystemService())
        {
            LeftPath = leftPath,
            RightPath = rightPath
        };
        
        var dialog = new DirectorySyncDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Directory Synchronization",
            Content = dialog,
            Width = 1000,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panels after sync
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.LeftPanel.Refresh();
            mainVm.RightPanel.Refresh();
        }
    }
    
    private async void OnBranchViewRequested(object? sender, MainWindowViewModel.BranchViewEventArgs e)
    {
        await ShowBranchViewAsync(e.RootPath, e.Panel);
    }
    
    private async Task ShowBranchViewAsync(string rootPath, TabbedPanelViewModel? panel)
    {
        if (panel?.ActiveTab == null)
            return;
            
        var flatViewService = new Services.FlatViewService();
        
        try
        {
            var options = new Services.FlatViewOptions
            {
                IncludeHidden = panel.ActiveTab.ShowHiddenFiles
            };
            
            var result = await flatViewService.GetFlatViewAsync(rootPath, options);
            
            // Populate the active tab with all files from subdirectories
            var paths = result.Items.Select(i => i.FullPath).ToList();
            panel.ActiveTab.PopulateWithPaths(paths, $"Branch: {Path.GetFileName(rootPath)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Branch view error: {ex.Message}");
        }
    }
    
    private async void OnSplitFileRequested(object? sender, MainWindowViewModel.SplitCombineEventArgs e)
    {
        await ShowFileSplitDialogAsync(e.FilePath, e.DestinationPath);
    }
    
    private async Task ShowFileSplitDialogAsync(string filePath, string destinationPath)
    {
        var vm = new FileSplitViewModel();
        vm.Initialize(filePath, destinationPath);
        
        var dialog = new FileSplitDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Split File",
            Content = dialog,
            Width = 550,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panels after split
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
            mainVm.InactivePanel.Refresh();
        }
    }
    
    private async void OnCombineFilesRequested(object? sender, MainWindowViewModel.SplitCombineEventArgs e)
    {
        await ShowFileCombineDialogAsync(e.FilePath, e.DestinationPath);
    }
    
    private async Task ShowFileCombineDialogAsync(string filePath, string destinationPath)
    {
        var vm = new FileCombineViewModel();
        vm.Initialize(filePath, destinationPath);
        
        var dialog = new FileCombineDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Combine Files",
            Content = dialog,
            Width = 550,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panels after combine
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
            mainVm.InactivePanel.Refresh();
        }
    }
    
    private async void OnSftpConnectionRequested(object? sender, EventArgs e)
    {
        await ShowSftpDialogAsync();
    }
    
    private async Task ShowSftpDialogAsync()
    {
        var sftpService = new Services.SftpService();
        var vm = new SftpViewModel(sftpService);
        
        var dialog = new SftpDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "SFTP Connection",
            Content = dialog,
            Width = 900,
            Height = 650,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        await window.ShowDialog(this);
        
        // Refresh panel if downloads were made
        if (dialog.DownloadPath != null && DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
        }
    }
    
    private async Task ShowSettingsDialogAsync()
    {
        if (_settingsService == null)
            return;

        var vm = new SettingsViewModel(_settingsService, _settingsNavigationService);
        vm.Load();
        
        var dialog = new SettingsDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Settings",
            Content = dialog,
            Width = 650,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        var result = await window.ShowDialog<bool?>(this);
        
        if (result == true)
        {
            // Settings were saved, could apply them here
        }
    }
    
    private async Task ShowFtpDialogAsync()
    {
        var ftpService = new Services.FtpService();
        var vm = new FtpViewModel(ftpService);
        
        var dialog = new FtpDialog
        {
            DataContext = vm
        };
        
        await dialog.ShowDialog(this);
        
        // Refresh panel if downloads were made
        if (dialog.DownloadPath != null && DataContext is MainWindowViewModel mainVm)
        {
            // Could navigate to the download folder
        }
    }
    
    private async Task ShowMultiRenameDialogAsync(List<string> filePaths)
    {
        var vm = new MultiRenameViewModel();
        vm.Initialize(filePaths);
        
        var dialog = new MultiRenameDialog
        {
            DataContext = vm
        };
        
        await dialog.ShowDialog(this);
        
        // Refresh panel after renaming
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.ActivePanel.Refresh();
        }
    }
    
    private async Task ShowFileViewerAsync(string filePath)
    {
        var vm = new FileViewerViewModel();
        var dialog = new FileViewerDialog
        {
            DataContext = vm
        };
        
        // Load file after showing dialog
        _ = vm.LoadFileAsync(filePath);
        
        await dialog.ShowDialog(this);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (TryHandleHotlistShortcut(e, vm))
            return;
            
        // Handle Tab key for panel switching (since it can't be bound in XAML easily)
        if (e.Key == Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.SwitchPanelCommand.Execute(null);
            e.Handled = true;
            return;
        }
        
        // Alt+F7 for search
        if (e.Key == Key.F7 && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            await ShowSearchDialogAsync(vm);
            e.Handled = true;
            return;
        }
        
        // F1 for help
        if (e.Key == Key.F1 && e.KeyModifiers == KeyModifiers.None)
        {
            await ShowHelpDialogAsync();
            e.Handled = true;
            return;
        }
        
        // Alt+Enter for properties
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            await ShowPropertiesDialogAsync(vm.ActivePanel.GetSelectedPaths().ToList());
            e.Handled = true;
            return;
        }
        
        // Shift+Enter: Execute selected file in background (TC compatible)
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Shift)
        {
            ExecuteInBackground(vm);
            e.Handled = true;
            return;
        }
        
        // Ctrl+Enter: Copy path + filename to command line (TC compatible)
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            CopyPathToCommandLine(vm, quoted: false);
            e.Handled = true;
            return;
        }
        
        // Ctrl+Shift+Enter: Copy quoted path + filename to command line (TC compatible)
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            CopyPathToCommandLine(vm, quoted: true);
            e.Handled = true;
            return;
        }
        
        // Alt+Shift+Enter: Calculate subdirectory file count (TC compatible)
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            await CalculateSubdirectoryStatsAsync(vm);
            e.Handled = true;
            return;
        }
        
        // Ctrl+\ for go to root
        if (e.Key == Key.OemBackslash && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.ActivePanel.GoToRoot();
            e.Handled = true;
            return;
        }
        
        // Ctrl+D for directory hotlist (TC compatible)
        if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            await ShowHotlistMenuAsync(vm);
            e.Handled = true;
            return;
        }
        
        // Ctrl+S for quick filter
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ToggleQuickFilter(vm);
            e.Handled = true;
            return;
        }
        
        // Ctrl+U for swap panels (TC compatible)
        if (e.Key == Key.U && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.SwapPanelsCommand.Execute(null);
            e.Handled = true;
            return;
        }
        
        // Ctrl+L for calculate directory sizes (TC compatible)
        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.ActivePanel.CalculateDirectorySizes();
            e.Handled = true;
            return;
        }
        
        // Ctrl+G for go to line / path (TC uses Alt+G)
        if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Show goto dialog
            await ShowGotoDialogAsync(vm);
            e.Handled = true;
            return;
        }
        
        // Alt+F1 for left drive menu (TC compatible)
        if (e.Key == Key.F1 && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Focus left panel and show drives
            vm.SetActivePanelCommand.Execute(vm.LeftPanel);
            FocusDriveBar(vm.LeftPanel);
            e.Handled = true;
            return;
        }
        
        // Alt+F2 for right drive menu (TC compatible)
        if (e.Key == Key.F2 && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Focus right panel and show drives
            vm.SetActivePanelCommand.Execute(vm.RightPanel);
            FocusDriveBar(vm.RightPanel);
            e.Handled = true;
            return;
        }
        
        // NumPad keys for selection
        if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.Add: // NumPad +
                    await ShowSelectByPatternDialogAsync(vm, true);
                    e.Handled = true;
                    break;
                    
                case Key.Subtract: // NumPad -
                    await ShowSelectByPatternDialogAsync(vm, false);
                    e.Handled = true;
                    break;
                    
                case Key.Multiply: // NumPad *
                    vm.ActivePanel.InvertSelection();
                    e.Handled = true;
                    break;
                    
                case Key.Divide: // NumPad /
                    // Restore previous selection using SelectionHistoryService
                    RestorePreviousSelection(vm);
                    e.Handled = true;
                    break;
            }
        }
        
        // Ctrl+B for brief view
        if (e.Key == Key.B && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.ActivePanel.SetViewMode(FilePanelViewMode.List);
            e.Handled = true;
            return;
        }
        
        // Ctrl+F1 for thumbnails
        if (e.Key == Key.F1 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.ActivePanel.SetViewMode(FilePanelViewMode.Thumbnails);
            e.Handled = true;
            return;
        }
        
        // Ctrl+F2 for full/details view
        if (e.Key == Key.F2 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.ActivePanel.SetViewMode(FilePanelViewMode.Details);
            e.Handled = true;
            return;
        }
    }
    
    /// <summary>
    /// Copies the current path (and selected filename) to the command line (Ctrl+Enter in TC).
    /// </summary>
    private void CopyPathToCommandLine(MainWindowViewModel vm, bool quoted)
    {
        string path;
        
        var selectedItem = vm.ActivePanel.SelectedItem;
        if (selectedItem != null && selectedItem.ItemType != FileSystemItemType.ParentDirectory)
        {
            path = selectedItem.FullPath;
        }
        else
        {
            path = vm.ActivePanel.CurrentPath;
        }
        
        if (string.IsNullOrEmpty(path))
            return;
        
        // Add quotes if requested or if path contains spaces
        if (quoted || path.Contains(' '))
        {
            path = $"\"{path}\"";
        }
        
        // Append to command line (with space if there's existing content)
        if (!string.IsNullOrEmpty(vm.CommandLine) && !vm.CommandLine.EndsWith(' '))
        {
            vm.CommandLine += " " + path;
        }
        else
        {
            vm.CommandLine += path;
        }
        
        // Focus the command line TextBox
        if (CommandLineTextBox != null)
        {
            CommandLineTextBox.Focus();
            CommandLineTextBox.CaretIndex = CommandLineTextBox.Text?.Length ?? 0;
        }
    }
    
    /// <summary>
    /// Execute selected file in background (Shift+Enter in TC).
    /// Runs executable without waiting and continues working.
    /// </summary>
    private void ExecuteInBackground(MainWindowViewModel vm)
    {
        var selectedItem = vm.ActivePanel.SelectedItem;
        if (selectedItem == null || selectedItem.IsDirectory)
            return;
        
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = selectedItem.FullPath,
                UseShellExecute = true,
                WorkingDirectory = vm.ActivePanel.CurrentPath
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Background execute error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Calculates and displays subdirectory file/folder count (Alt+Shift+Enter in TC).
    /// </summary>
    private async Task CalculateSubdirectoryStatsAsync(MainWindowViewModel vm)
    {
        var selectedPaths = vm.ActivePanel.GetSelectedPaths().ToList();
        if (selectedPaths.Count == 0)
            return;
        
        foreach (var path in selectedPaths)
        {
            if (!Directory.Exists(path))
                continue;
            
            try
            {
                var stats = await Task.Run(() =>
                {
                    long totalSize = 0;
                    int fileCount = 0;
                    int dirCount = 0;
                    
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var fi = new FileInfo(file);
                                totalSize += fi.Length;
                                fileCount++;
                            }
                            catch { /* Skip inaccessible files */ }
                        }
                        
                        dirCount = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Count();
                    }
                    catch { /* Handle access denied */ }
                    
                    return (totalSize, fileCount, dirCount);
                });
                
                // Update the item's calculated size and show notification
                var item = vm.ActivePanel.Items?.FirstOrDefault(i => 
                    i.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
                
                if (item != null)
                {
                    item.CalculatedSize = stats.totalSize;
                }
                
                _notificationService?.ShowInfo(
                    $"{Path.GetFileName(path)}: {stats.fileCount} files, {stats.dirCount} folders, {FormatSize(stats.totalSize)}");
            }
            catch (Exception ex)
            {
                _notificationService?.ShowError($"Error calculating stats: {ex.Message}");
            }
        }
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
    
    /// <summary>
    /// Shows a dialog to go to a specific path (Alt+G in Total Commander).
    /// </summary>
    private async Task ShowGotoDialogAsync(MainWindowViewModel vm)
    {
        var result = await ShowInputDialogAsync(
            "Go to Path",
            "Enter path to navigate to:",
            vm.ActivePanel.CurrentPath);
            
        if (!string.IsNullOrWhiteSpace(result) && Directory.Exists(result))
        {
            vm.ActivePanel.NavigateTo(result);
        }
        else if (!string.IsNullOrWhiteSpace(result))
        {
            _notificationService?.ShowWarning($"Path not found: {result}");
        }
    }
    
    /// <summary>
    /// Restores the previous selection using the SelectionHistoryService (NumPad /).
    /// </summary>
    private void RestorePreviousSelection(MainWindowViewModel vm)
    {
        if (_selectionHistoryService == null)
        {
            _notificationService?.ShowWarning("Selection history service not available.");
            return;
        }
        
        var panelId = vm.ActivePanel == vm.LeftPanel ? "left" : "right";
        var state = _selectionHistoryService.RestoreSelection(panelId);
        
        if (state == null)
        {
            _notificationService?.ShowInfo("No previous selection to restore.");
            return;
        }
        
        // Check if we're in the same directory
        var currentPath = vm.ActivePanel.CurrentPath;
        if (!string.Equals(state.DirectoryPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            // Navigate to the directory first
            vm.ActivePanel.NavigateTo(state.DirectoryPath);
        }
        
        // Clear current selection
        vm.ActivePanel.DeselectAll();
        
        // Restore selection
        int restoredCount = 0;
        foreach (var itemPath in state.SelectedItems)
        {
            var fullPath = Path.Combine(state.DirectoryPath, itemPath);
            var item = vm.ActivePanel.Items?.FirstOrDefault(i => 
                string.Equals(i.FullPath, fullPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Name, itemPath, StringComparison.OrdinalIgnoreCase));
            
            if (item != null)
            {
                item.IsSelected = true;
                if (vm.ActivePanel.SelectedItems != null && !vm.ActivePanel.SelectedItems.Contains(item))
                {
                    vm.ActivePanel.SelectedItems.Add(item);
                }
                restoredCount++;
            }
        }
        
        _notificationService?.ShowSuccess($"Restored {restoredCount} of {state.SelectedItems.Count} items.");
    }
    
    private async Task ShowSelectByPatternDialogAsync(MainWindowViewModel vm, bool isSelect)
    {
        var title = isSelect ? "Select by Pattern" : "Deselect by Pattern";
        var prompt = isSelect ? "Enter pattern to select (e.g., *.txt):" : "Enter pattern to deselect (e.g., *.txt):";
        var pattern = await ShowInputDialogAsync(title, prompt, "*.*");
        
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var items = vm.ActivePanel.Items?
                .Where(i => i.ItemType != FileSystemItemType.ParentDirectory);
            
            if (items == null) return;
            
            foreach (var item in items)
            {
                if (MatchesPattern(item.Name, pattern))
                {
                    item.IsSelected = isSelect;
                    if (isSelect && vm.ActivePanel.SelectedItems != null && !vm.ActivePanel.SelectedItems.Contains(item))
                        vm.ActivePanel.SelectedItems.Add(item);
                    else if (!isSelect && vm.ActivePanel.SelectedItems != null)
                        vm.ActivePanel.SelectedItems.Remove(item);
                }
            }
        }
    }
    
    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple wildcard matching
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    
    private async Task ShowPropertiesDialogAsync(List<string> paths)
    {
        if (paths.Count == 0)
            return;
            
        // Simple properties dialog for now
        var path = paths[0];
        var isFile = File.Exists(path);
        var isDir = Directory.Exists(path);
        
        if (!isFile && !isDir)
            return;
            
        var info = isFile ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);
        
        var message = $"Name: {info.Name}\n" +
                     $"Path: {info.FullName}\n" +
                     $"Created: {info.CreationTime}\n" +
                     $"Modified: {info.LastWriteTime}\n" +
                     $"Attributes: {info.Attributes}";
        
        if (isFile)
        {
            var fileInfo = new FileInfo(path);
            message += $"\nSize: {fileInfo.Length:N0} bytes";
        }
        else
        {
            try
            {
                var dirInfo = new DirectoryInfo(path);
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                var dirs = dirInfo.GetDirectories("*", SearchOption.AllDirectories);
                var totalSize = files.Sum(f => f.Length);
                message += $"\nFiles: {files.Length}\nFolders: {dirs.Length}\nTotal Size: {totalSize:N0} bytes";
            }
            catch
            {
                message += "\n(Unable to calculate folder size)";
            }
        }
        
        await ShowConfirmDialogAsync("Properties", message);
    }
    
    private async void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await ShowSearchDialogAsync(vm);
        }
    }

    private async void OnHotlistClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await ShowHotlistDialogAsync(vm);
        }
    }
    
    public async Task ShowSearchDialogAsync(MainWindowViewModel vm)
    {
        var searchVm = vm.CreateSearchViewModel();
        var dialog = new SearchDialog
        {
            DataContext = searchVm
        };
        
        // Handle feed to panel request
        searchVm.FeedToPanelRequested += (_, paths) =>
        {
            vm.ActivePanel.CreateVirtualTab(paths, $"Search Results ({paths.Count} items)");
            dialog.Close();
        };
        
        // Handle navigation request
        searchVm.NavigateRequested += (_, path) =>
        {
            vm.NavigateToPath(path);
        };
        
        var result = await dialog.ShowDialog<object?>(this);
        
        if (result is bool && dialog.NavigateToPath != null)
        {
            vm.NavigateToPath(dialog.NavigateToPath);
        }
    }
    
    public async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        await dialog.ShowDialog(this);
        return dialog.Result;
    }
    
    public async Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue = "")
    {
        var dialog = new InputDialog(title, prompt, defaultValue);
        await dialog.ShowDialog(this);
        return dialog.Result;
    }
    
    public async Task ShowAboutDialogAsync()
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }
    
    private async void OnAboutRequested(object? sender, EventArgs e)
    {
        await ShowAboutDialogAsync();
    }
    
    private async void OnPluginsRequested(object? sender, EventArgs e)
    {
        await ShowPluginsDialogAsync();
    }
    
    private async Task ShowPluginsDialogAsync()
    {
        if (DataContext is MainWindowViewModel mainVm && mainVm.PluginManager != null)
        {
            var vm = new PluginsViewModel(mainVm.PluginManager);
            
            var dialog = new PluginsDialog
            {
                DataContext = vm
            };
            
            var window = new Window
            {
                Title = "Plugin Manager",
                Content = dialog,
                Width = 800,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            
            await window.ShowDialog(this);
        }
    }
    
    private async void OnCustomColumnsRequested(object? sender, EventArgs e)
    {
        await ShowCustomColumnsDialogAsync();
    }
    
    private async Task ShowCustomColumnsDialogAsync()
    {
        var vm = new CustomColumnsViewModel();
        
        var dialog = new CustomColumnsDialog
        {
            DataContext = vm
        };
        
        var window = new Window
        {
            Title = "Custom Columns",
            Content = dialog,
            Width = 650,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        var result = await window.ShowDialog<object?>(this);
        
        if (result != null)
        {
            // Apply column configuration to panels
            // This would need to update the file panel columns
            System.Diagnostics.Debug.WriteLine("Column configuration updated");
        }
    }
    
    private void OnCommandPaletteBackgroundClick(object? sender, PointerPressedEventArgs e)
    {
        // Close command palette when clicking the background
        if (DataContext is MainWindowViewModel vm && vm.IsCommandPaletteOpen)
        {
            // Check if the click was on the background (not the palette itself)
            if (e.Source is Border)
            {
                vm.CloseCommandPaletteCommand.Execute(null);
            }
        }
    }

    private void ToggleQuickFilter(MainWindowViewModel vm)
    {
        var panelControl = FindTabbedPanel(vm.ActivePanel);
        panelControl?.ToggleQuickFilter();
    }

    private void FocusDriveBar(TabbedPanelViewModel panel)
    {
        var panelControl = FindTabbedPanel(panel);
        panelControl?.FocusDriveBar();
    }

    private TabbedFilePanel? FindTabbedPanel(TabbedPanelViewModel panel)
    {
        return this.GetVisualDescendants()
            .OfType<TabbedFilePanel>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, panel));
    }

    private async Task ShowHotlistDialogAsync(MainWindowViewModel vm)
    {
        if (_directoryHotlistService == null)
        {
            _notificationService?.ShowWarning("Directory hotlist service not available.");
            return;
        }

        var hotlistVm = new DirectoryHotlistViewModel(_directoryHotlistService)
        {
            CurrentPath = vm.ActivePanel.CurrentPath
        };

        hotlistVm.NavigateRequested += (_, path) =>
        {
            vm.ActivePanel.NavigateTo(path);
        };

        var dialog = new DirectoryHotlistDialog
        {
            DataContext = hotlistVm
        };

        var window = new Window
        {
            Title = "Directory Hotlist",
            Content = dialog,
            Width = 420,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        hotlistVm.RequestClose += (_, _) => window.Close();

        await hotlistVm.LoadAsync();
        await window.ShowDialog(this);
    }

    private async Task ShowHotlistMenuAsync(MainWindowViewModel vm)
    {
        if (_directoryHotlistService == null)
        {
            _notificationService?.ShowWarning("Directory hotlist service not available.");
            return;
        }

        var items = (await _directoryHotlistService.GetItemsAsync()).ToList();
        var categories = await _directoryHotlistService.GetCategoryTreeAsync();

        _hotlistShortcutMap = items
            .Where(i => !string.IsNullOrEmpty(i.KeyboardShortcut))
            .GroupBy(i => i.KeyboardShortcut!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var menu = new ContextMenu();
        var rootItems = new List<object>();

        foreach (var item in items.Where(i => i.ParentCategoryId == null).OrderBy(i => i.Order))
        {
            var menuItem = CreateHotlistMenuItem(item, vm);
            if (menuItem != null)
                rootItems.Add(menuItem);
        }

        foreach (var category in categories.OrderBy(c => c.Order))
        {
            rootItems.Add(CreateCategoryMenuItem(category, vm));
        }

        rootItems.Add(new AvaloniaSeparator());
        var addCurrent = new AvaloniaMenuItem { Header = "Add current directory" };
        addCurrent.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(vm.ActivePanel.CurrentPath))
            {
                await _directoryHotlistService.AddDirectoryAsync(vm.ActivePanel.CurrentPath);
            }
        };
        rootItems.Add(addCurrent);

        var configure = new AvaloniaMenuItem { Header = "Configure hotlist..." };
        configure.Click += async (_, _) => await ShowHotlistDialogAsync(vm);
        rootItems.Add(configure);

        menu.ItemsSource = rootItems;
        menu.PlacementTarget = this;
        menu.Closed += (_, _) => _hotlistMenuOpen = false;
        menu.KeyDown += (_, e) => TryHandleHotlistShortcut(e, vm);

        _hotlistMenu = menu;
        _hotlistMenuOpen = true;
        menu.Open(this);
    }

    private bool TryHandleHotlistShortcut(KeyEventArgs e, MainWindowViewModel vm)
    {
        if (!_hotlistMenuOpen || e.KeyModifiers != KeyModifiers.None)
            return false;

        var shortcut = KeyToShortcut(e.Key);
        if (string.IsNullOrEmpty(shortcut) || !_hotlistShortcutMap.TryGetValue(shortcut, out var item))
            return false;

        if (!string.IsNullOrEmpty(item.Path))
        {
            vm.ActivePanel.NavigateTo(item.Path);
            _ = _directoryHotlistService?.RecordAccessAsync(item.Id);
        }

        _hotlistMenu?.Close();
        e.Handled = true;
        return true;
    }

    private Control? CreateHotlistMenuItem(HotlistItem item, MainWindowViewModel vm)
    {
        if (item.Type == HotlistItemType.Separator)
            return new AvaloniaSeparator();

        if (item.Type != HotlistItemType.Directory || string.IsNullOrEmpty(item.Path))
            return null;

        var menuItem = new AvaloniaMenuItem
        {
            Header = item.Name
        };

        var gesture = ShortcutToGesture(item.KeyboardShortcut);
        if (gesture != null)
        {
            menuItem.InputGesture = gesture;
        }

        menuItem.Click += (_, _) =>
        {
            vm.ActivePanel.NavigateTo(item.Path);
            _ = _directoryHotlistService?.RecordAccessAsync(item.Id);
        };

        return menuItem;
    }

    private AvaloniaMenuItem CreateCategoryMenuItem(HotlistCategory category, MainWindowViewModel vm)
    {
        var menuItem = new AvaloniaMenuItem { Header = category.Name };
        var items = new List<object>();

        foreach (var subCategory in category.SubCategories.OrderBy(c => c.Order))
        {
            items.Add(CreateCategoryMenuItem(subCategory, vm));
        }

        foreach (var item in category.Items.OrderBy(i => i.Order))
        {
            var child = CreateHotlistMenuItem(item, vm);
            if (child != null)
                items.Add(child);
        }

        menuItem.ItemsSource = items;
        return menuItem;
    }

    private static string? KeyToShortcut(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            var offset = key - Key.A;
            return ((char)('A' + offset)).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            var offset = key - Key.D0;
            return ((char)('0' + offset)).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            var offset = key - Key.NumPad0;
            return ((char)('0' + offset)).ToString();
        }

        return null;
    }

    private static KeyGesture? ShortcutToGesture(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return null;

        var ch = shortcut.Trim()[0];
        if (char.IsLetter(ch))
        {
            var key = Key.A + (char.ToUpperInvariant(ch) - 'A');
            return new KeyGesture(key);
        }

        if (char.IsDigit(ch))
        {
            var key = Key.D0 + (ch - '0');
            return new KeyGesture(key);
        }

        return null;
    }
}
