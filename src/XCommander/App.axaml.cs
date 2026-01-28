using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using XCommander.Services;
using XCommander.ViewModels;
using XCommander.Views;

namespace XCommander;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup DI
        var services = new ServiceCollection();
        
        // Core services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IFtpService, FtpService>();
        services.AddSingleton<ISftpService, SftpService>();
        services.AddSingleton<ITransferQueueService, TransferQueueService>();
        
        // TC parity services - File Operations
        services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
        services.AddSingleton<IBranchViewService, BranchViewService>();
        services.AddSingleton<IFileAttributeService, FileAttributeService>();
        services.AddSingleton<IFolderSizeService, FolderSizeService>();
        services.AddSingleton<IFileLinkService, FileLinkService>();
        services.AddSingleton<IFileChecksumService, FileChecksumService>();
        services.AddSingleton<IFileSplitService>(sp => new FileSplitService(sp.GetService<IFileChecksumService>()));
        services.AddSingleton<IBookmarkService, BookmarkService>();
        services.AddSingleton<ISessionStateService, SessionStateService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<ISecureDeleteService, SecureDeleteService>();
        services.AddSingleton<IDiskSpaceAnalyzerService, DiskSpaceAnalyzerService>();
        services.AddSingleton<IFileColoringService, FileColoringService>();
        services.AddSingleton<IEncodingService, EncodingService>();
        
        // TC parity services - Archive
        services.AddSingleton<IArchiveService, ArchiveService>();
        services.AddSingleton<IAdvancedArchiveService>(sp => 
            new AdvancedArchiveService(sp.GetRequiredService<IArchiveService>()));
        
        // TC parity services - Tools
        services.AddSingleton<IPrintService, PrintService>();
        services.AddSingleton<IBatchJobService>(sp => 
            new BatchJobService(sp.GetService<IFileSystemService>(), sp.GetService<IArchiveService>()));
        services.AddSingleton<IContentPluginService, ContentPluginService>();
        
        // TC parity services - UI
        services.AddSingleton<IButtonBarService, ButtonBarService>();
        services.AddSingleton<IUserMenuService, UserMenuService>();
        services.AddSingleton<ICustomViewModesService, CustomViewModesService>();
        
        // TC parity services - Cloud & Network
        services.AddSingleton<ICloudStorageService, CloudStorageService>();
        services.AddSingleton<IWebDAVService, WebDAVService>();
        services.AddSingleton<INetworkBrowserService, NetworkBrowserService>();
        
        // TC parity services - System & Advanced
        services.AddSingleton<ISystemToolsService, SystemToolsService>();
        services.AddSingleton<IAdvancedSearchService, AdvancedSearchService>();
        services.AddSingleton<IDirectoryHotlistService, DirectoryHotlistService>();
        services.AddSingleton<IAdvancedCopyService, AdvancedCopyService>();
        
        // TC parity services - Legacy Archives
        services.AddSingleton<ILegacyArchiveService, LegacyArchiveService>();
        
        // TC parity services - Icons
        services.AddSingleton<IIconService, IconService>();
        
        // TC parity services - Advanced Preview
        services.AddSingleton<IAdvancedPreviewService, AdvancedPreviewService>();
        
        // TC parity services - Flat View
        services.AddSingleton<IFlatViewService, FlatViewService>();
        
        // TC parity services - Enhanced UI/UX (Full Parity)
        services.AddSingleton<IOverwriteDialogService>(sp => 
            new OverwriteDialogService(
                sp.GetService<IContentPluginService>(),
                sp.GetService<IAdvancedPreviewService>()));
        services.AddSingleton<ISelectionHistoryService, SelectionHistoryService>();
        services.AddSingleton<IPasswordManagerService, PasswordManagerService>();
        services.AddSingleton<ILongPathService, LongPathService>();
        services.AddSingleton<IOperationLogService, OperationLogService>();
        services.AddSingleton<IQuickFilterService, QuickFilterService>();
        services.AddSingleton<IAccessibilityService, AccessibilityService>();
        services.AddSingleton<IDragDropService>(sp => 
            new DragDropService(
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetService<IOperationLogService>()));
        
        // TC parity services - Menu Configuration & Customization
        services.AddSingleton<IMenuConfigService, MenuConfigService>();
        
        // TC parity services - File Split/Merge
        services.AddSingleton<ISplitMergeService>(sp => 
            new SplitMergeService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Text Encoding Conversion
        services.AddSingleton<ITextEncodingService>(sp => 
            new TextEncodingService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Background Transfer Queue
        services.AddSingleton<IBackgroundTransferService>(sp => 
            new BackgroundTransferService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Custom Columns
        services.AddSingleton<ICustomColumnService, CustomColumnService>();
        
        // TC parity services - Plugin System (WCX, WDX, WFX, WLX)
        services.AddSingleton<IPluginService, PluginService>();
        
        // TC parity services - FTP/SFTP Client
        services.AddSingleton<IFTPClientService, FTPClientService>();
        
        // TC parity services - Multi-Rename Tool
        services.AddSingleton<IMultiRenameToolService, MultiRenameToolService>();
        
        // TC parity services - Directory Synchronization
        services.AddSingleton<IDirectorySyncService>(sp => 
            new DirectorySyncService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Internal Commands
        services.AddSingleton<IInternalCommandService>(sp => 
            new InternalCommandService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Quick Search/Filter
        services.AddSingleton<IQuickSearchService, QuickSearchService>();
        
        // TC parity services - File Operation Logging
        services.AddSingleton<IFileLoggingService, FileLoggingService>();
        
        // TC parity services - File Descriptions (descript.ion)
        services.AddSingleton<IDescriptionFileService, DescriptionFileService>();
        
        // TC parity services - Proxy Configuration
        services.AddSingleton<IProxyService, ProxyService>();
        
        // TC parity services - Archive Synchronization
        services.AddSingleton<IArchiveSyncService, ArchiveSyncService>();
        
        // TC parity services - Directory Trees
        services.AddSingleton<IDirectoryTreeService>(sp =>
            new DirectoryTreeService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Archive-to-Archive Copy
        services.AddSingleton<IArchiveToArchiveService, ArchiveToArchiveService>();
        
        // TC parity services - Background Archive Operations
        services.AddSingleton<IBackgroundArchiveService>(sp => new BackgroundArchiveService());
        
        // TC parity services - Virtual Scrolling (100K+ files)
        services.AddSingleton<IVirtualScrollingService>(sp =>
            new VirtualScrollingService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - FXP Server-to-Server Transfer
        services.AddSingleton<IFxpTransferService, FxpTransferService>();
        
        // TC parity services - USB Direct Transfer
        services.AddSingleton<IUsbTransferService>(sp =>
            new UsbTransferService(sp.GetRequiredService<ILongPathService>()));
        
        // TC parity services - Mainframe FTP (z/OS, EBCDIC)
        services.AddSingleton<IMainframeFtpService, MainframeFtpService>();
        
        // UX services - Notifications/Toasts
        services.AddSingleton<INotificationService, NotificationService>();
        
        // UX services - Logging
        services.AddSingleton<ILoggingService>(sp => new LoggingService(maxEntries: 10000, enableDebug: false));
        
        // UX services - Touch Mode
        services.AddSingleton<ITouchModeService, TouchModeService>();
        
        // ViewModels
        services.AddSingleton<MainWindowViewModel>(sp => 
            new MainWindowViewModel(
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetService<IDescriptionFileService>(),
                sp.GetService<ISelectionService>()));
        
        var provider = services.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = provider.GetRequiredService<MainWindowViewModel>();
            var notificationService = provider.GetService<INotificationService>();
            var selectionHistoryService = provider.GetService<ISelectionHistoryService>();
            
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            
            // Initialize services
            mainWindow.InitializeServices(
                notificationService,
                selectionHistoryService,
                provider.GetService<IDirectoryHotlistService>());
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
