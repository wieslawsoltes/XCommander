using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class HelpViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedCategory = "Overview";
    
    [ObservableProperty]
    private string _helpContent = string.Empty;
    
    public ObservableCollection<string> Categories { get; } = new()
    {
        "Overview",
        "Navigation",
        "File Operations",
        "Selection",
        "Search",
        "Tabs",
        "Archives",
        "FTP/SFTP",
        "Tools",
        "Keyboard Shortcuts"
    };
    
    public HelpViewModel()
    {
        LoadCategory("Overview");
    }
    
    partial void OnSelectedCategoryChanged(string value)
    {
        LoadCategory(value);
    }
    
    private void LoadCategory(string category)
    {
        HelpContent = category switch
        {
            "Overview" => GetOverviewHelp(),
            "Navigation" => GetNavigationHelp(),
            "File Operations" => GetFileOperationsHelp(),
            "Selection" => GetSelectionHelp(),
            "Search" => GetSearchHelp(),
            "Tabs" => GetTabsHelp(),
            "Archives" => GetArchivesHelp(),
            "FTP/SFTP" => GetFtpHelp(),
            "Tools" => GetToolsHelp(),
            "Keyboard Shortcuts" => GetKeyboardShortcutsHelp(),
            _ => "Select a category from the list."
        };
    }
    
    private static string GetOverviewHelp() => """
        XCommander - Cross-Platform File Manager
        =========================================
        
        XCommander is a powerful, cross-platform file manager inspired by 
        Total Commander. It provides a dual-pane interface for efficient 
        file management across Windows, macOS, and Linux.
        
        Key Features:
        • Dual-panel interface with tabs
        • Advanced file operations (copy, move, delete, rename)
        • Archive support (ZIP, 7-Zip, RAR, TAR, GZ)
        • FTP/SFTP connections
        • File comparison and synchronization
        • Multi-rename tool
        • Quick view panel
        • Configurable toolbar and columns
        • Plugin system
        • Command palette (Ctrl+Shift+P)
        
        Getting Started:
        1. Use Tab to switch between panels
        2. Navigate with arrow keys or double-click
        3. Press F5 to copy, F6 to move files
        4. Press F7 to create new folder
        5. Press Ctrl+Q to toggle quick view
        """;
    
    private static string GetNavigationHelp() => """
        Navigation
        ==========
        
        Basic Navigation:
        • Enter/Double-click: Open folder or execute file
        • Backspace: Go to parent directory
        • Alt+Left: Go back in history
        • Alt+Right: Go forward in history
        • Tab: Switch between left and right panels
        
        Drive Selection:
        • Click on drive buttons in the drive bar
        • Use Ctrl+D to open drive selector
        
        Path Navigation:
        • Click on breadcrumb segments to jump to that folder
        • Type path in command line and press Enter
        
        Bookmarks:
        • Ctrl+B: Toggle bookmarks panel
        • Ctrl+D: Add current folder to bookmarks
        • Click bookmark to navigate to it
        
        History:
        • Recent locations are saved automatically
        • Use back/forward buttons or Alt+Left/Right
        """;
    
    private static string GetFileOperationsHelp() => """
        File Operations
        ===============
        
        Copy Files (F5):
        • Select files and press F5
        • Files will be copied to the other panel
        • Progress dialog shows copy status
        
        Move Files (F6):
        • Select files and press F6
        • Files will be moved to the other panel
        
        Delete Files (F8/Delete):
        • Select files and press F8 or Delete
        • Confirmation dialog will appear
        
        Rename (F2):
        • Select file and press F2
        • Enter new name in dialog
        
        Create New Folder (F7):
        • Press F7 to create a new folder
        • Enter folder name in dialog
        
        Create New File (Shift+F4):
        • Press Shift+F4 to create a new file
        • Enter file name in dialog
        
        View File (F3):
        • Press F3 to view selected file
        • Supports text, hex, and image viewing
        
        Edit File (F4):
        • Press F4 to edit selected file
        • Opens with system default editor
        """;
    
    private static string GetSelectionHelp() => """
        Selection
        =========
        
        Single Selection:
        • Click on item to select
        • Arrow keys to move selection
        
        Multiple Selection:
        • Space: Toggle selection on current item
        • Insert: Select and move to next item
        • Ctrl+Click: Add/remove from selection
        • Shift+Click: Select range
        
        Select All:
        • Ctrl+A: Select all items
        • Num +: Select by pattern
        • Num -: Deselect by pattern
        • Num *: Invert selection
        
        Deselect:
        • Ctrl+Shift+A: Deselect all
        • Escape: Clear selection
        
        Selection Info:
        • Status bar shows selected files count
        • Total size of selected files is displayed
        """;
    
    private static string GetSearchHelp() => """
        Search (Alt+F7)
        ===============
        
        Basic Search:
        • Press Alt+F7 to open search dialog
        • Enter file name pattern (supports wildcards)
        • Specify search location
        
        Wildcards:
        • * matches any characters
        • ? matches single character
        • Example: *.txt finds all text files
        
        Advanced Options:
        • Search in subdirectories
        • Case sensitive search
        • Search by date range
        • Search by file size
        • Search file contents
        
        Regular Expressions:
        • Enable regex mode for complex patterns
        • Example: ^test.*\.cs$ matches test files
        
        Search Results:
        • Results shown in list
        • Double-click to navigate to file
        • Feed to listbox to work with results
        """;
    
    private static string GetTabsHelp() => """
        Tabs
        ====
        
        Tab Management:
        • Ctrl+T: Open new tab
        • Ctrl+W: Close current tab
        • Ctrl+Tab: Next tab
        • Ctrl+Shift+Tab: Previous tab
        
        Tab Features:
        • Each tab maintains its own location
        • Drag tabs to reorder
        • Right-click tab for context menu
        • Double-click tab bar to create new tab
        
        Tab Operations:
        • Duplicate tab: Right-click > Duplicate
        • Lock tab: Prevents accidental navigation
        • Close other tabs: Right-click > Close Others
        """;
    
    private static string GetArchivesHelp() => """
        Archives
        ========
        
        Supported Formats:
        • ZIP (full support)
        • 7-Zip (full support)
        • RAR (read-only)
        • TAR, GZ, BZ2, XZ
        
        Opening Archives:
        • Enter archive like a folder
        • Browse contents in file panel
        
        Extract Files:
        • Select files in archive
        • Press F5 to extract to other panel
        • Or use Commands > Extract
        
        Create Archive:
        • Select files to archive
        • Use Commands > Create Archive
        • Choose format and options
        
        Add to Archive:
        • Select files
        • Right-click > Add to Archive
        • Choose existing archive
        """;
    
    private static string GetFtpHelp() => """
        FTP/SFTP
        ========
        
        FTP Connection:
        • Use Commands > FTP Connect
        • Enter server, username, password
        • Supports FTP and FTPS (SSL/TLS)
        
        SFTP Connection:
        • Use Commands > SFTP Connect
        • Enter server, username
        • Supports password or key authentication
        
        File Transfer:
        • Navigate remote files like local
        • Use F5/F6 for copy/move
        • Resume interrupted transfers
        
        Connection Manager:
        • Save frequently used connections
        • Quick connect from saved list
        • Organize connections in folders
        """;
    
    private static string GetToolsHelp() => """
        Tools
        =====
        
        Multi-Rename (Ctrl+M):
        • Batch rename multiple files
        • Use patterns and counters
        • Search and replace in names
        • Preview before applying
        
        Directory Compare:
        • Compare two directories
        • Show differences
        • Synchronize folders
        
        File Compare:
        • Compare two files
        • Side-by-side diff view
        • Inline diff mode
        
        Checksum Calculator:
        • Calculate MD5, SHA1, SHA256
        • Verify file integrity
        • Compare checksums
        
        Encoding Tool (Alt+F6):
        • Base64 encode/decode
        • URL encode/decode
        • Hex encode/decode
        
        Split/Combine Files:
        • Split large files into parts
        • Combine split files
        • CRC verification
        """;
    
    private static string GetKeyboardShortcutsHelp() => """
        Keyboard Shortcuts
        ==================
        
        Navigation:
        Tab             Switch panels
        Enter           Open folder/file
        Backspace       Parent directory
        Alt+Left        Go back
        Alt+Right       Go forward
        
        File Operations:
        F1              Help
        F2              Rename
        F3              View file
        F4              Edit file
        F5              Copy
        F6              Move
        F7              New folder
        F8/Delete       Delete
        Shift+F4        New file
        
        Selection:
        Space           Toggle selection
        Insert          Select and move down
        Ctrl+A          Select all
        Ctrl+Shift+A    Deselect all
        
        View:
        Ctrl+Q          Quick view panel
        Ctrl+H          Show hidden files
        Ctrl+R          Refresh
        
        Search:
        Alt+F7          Search files
        Ctrl+F          Quick search
        
        Tabs:
        Ctrl+T          New tab
        Ctrl+W          Close tab
        Ctrl+Tab        Next tab
        
        Tools:
        Ctrl+M          Multi-rename
        Alt+F6          Encoding tool
        Ctrl+Shift+P    Command palette
        Ctrl+B          Bookmarks panel
        Ctrl+D          Add bookmark
        """;
    
    public event EventHandler? RequestClose;
    
    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
