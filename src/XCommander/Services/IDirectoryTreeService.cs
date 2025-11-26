using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Represents a node in the directory tree
/// </summary>
public record DirectoryTreeNode
{
    /// <summary>Full path to the directory</summary>
    public string Path { get; init; } = string.Empty;
    
    /// <summary>Display name (folder name)</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Parent node path (null for root)</summary>
    public string? ParentPath { get; init; }
    
    /// <summary>Whether this node is expanded</summary>
    public bool IsExpanded { get; set; }
    
    /// <summary>Whether this node is selected</summary>
    public bool IsSelected { get; set; }
    
    /// <summary>Whether children have been loaded</summary>
    public bool ChildrenLoaded { get; set; }
    
    /// <summary>Whether this is a special folder (Desktop, Documents, etc.)</summary>
    public bool IsSpecialFolder { get; init; }
    
    /// <summary>Special folder type if applicable</summary>
    public Environment.SpecialFolder? SpecialFolderType { get; init; }
    
    /// <summary>Icon key for this folder</summary>
    public string? IconKey { get; init; }
    
    /// <summary>Whether this folder is a favorite</summary>
    public bool IsFavorite { get; set; }
    
    /// <summary>Child nodes</summary>
    public List<DirectoryTreeNode> Children { get; init; } = new();
    
    /// <summary>Number of subdirectories (if known)</summary>
    public int? SubdirectoryCount { get; init; }
    
    /// <summary>Whether the folder is accessible</summary>
    public bool IsAccessible { get; init; } = true;
    
    /// <summary>Error message if not accessible</summary>
    public string? AccessError { get; init; }
}

/// <summary>
/// Tree synchronization mode
/// </summary>
public enum TreeSyncMode
{
    /// <summary>Tree follows panel navigation</summary>
    FollowPanel,
    
    /// <summary>Panel follows tree selection</summary>
    FollowTree,
    
    /// <summary>Bidirectional sync</summary>
    Bidirectional,
    
    /// <summary>No synchronization</summary>
    Independent
}

/// <summary>
/// Tree display options
/// </summary>
public record DirectoryTreeOptions
{
    /// <summary>Show hidden folders</summary>
    public bool ShowHidden { get; init; } = false;
    
    /// <summary>Show system folders</summary>
    public bool ShowSystem { get; init; } = false;
    
    /// <summary>Show special folders (Desktop, Documents, etc.)</summary>
    public bool ShowSpecialFolders { get; init; } = true;
    
    /// <summary>Show network locations</summary>
    public bool ShowNetwork { get; init; } = true;
    
    /// <summary>Sort folders alphabetically</summary>
    public bool SortAlphabetically { get; init; } = true;
    
    /// <summary>Show folder icons</summary>
    public bool ShowIcons { get; init; } = true;
    
    /// <summary>Show favorites section</summary>
    public bool ShowFavorites { get; init; } = true;
    
    /// <summary>Maximum depth to auto-expand</summary>
    public int AutoExpandDepth { get; init; } = 1;
    
    /// <summary>Synchronization mode with panel</summary>
    public TreeSyncMode SyncMode { get; init; } = TreeSyncMode.Bidirectional;
}

/// <summary>
/// Service for managing independent directory trees for each panel.
/// TC equivalent: Separate tree view for each file panel.
/// </summary>
public interface IDirectoryTreeService
{
    /// <summary>
    /// Get or create tree for a specific panel
    /// </summary>
    Task<DirectoryTreeNode> GetTreeForPanelAsync(
        string panelId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Load children for a node
    /// </summary>
    Task<IReadOnlyList<DirectoryTreeNode>> LoadChildrenAsync(
        string path,
        DirectoryTreeOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Expand node and load children
    /// </summary>
    Task<DirectoryTreeNode> ExpandNodeAsync(
        string panelId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Collapse a node
    /// </summary>
    void CollapseNode(string panelId, string path);
    
    /// <summary>
    /// Select a node in the tree
    /// </summary>
    Task SelectNodeAsync(string panelId, string path);
    
    /// <summary>
    /// Navigate to path and expand tree to show it
    /// </summary>
    Task NavigateToPathAsync(
        string panelId,
        string path,
        bool expandToPath = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current selected path for panel
    /// </summary>
    string? GetSelectedPath(string panelId);
    
    /// <summary>
    /// Refresh a node and its children
    /// </summary>
    Task RefreshNodeAsync(
        string panelId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refresh entire tree
    /// </summary>
    Task RefreshTreeAsync(
        string panelId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get tree options for panel
    /// </summary>
    DirectoryTreeOptions GetOptions(string panelId);
    
    /// <summary>
    /// Set tree options for panel
    /// </summary>
    void SetOptions(string panelId, DirectoryTreeOptions options);
    
    /// <summary>
    /// Set synchronization mode
    /// </summary>
    void SetSyncMode(string panelId, TreeSyncMode mode);
    
    /// <summary>
    /// Get favorites for tree
    /// </summary>
    IReadOnlyList<DirectoryTreeNode> GetTreeFavorites(string panelId);
    
    /// <summary>
    /// Add folder to tree favorites
    /// </summary>
    Task AddToTreeFavoritesAsync(string panelId, string path);
    
    /// <summary>
    /// Remove folder from tree favorites
    /// </summary>
    Task RemoveFromTreeFavoritesAsync(string panelId, string path);
    
    /// <summary>
    /// Get special folders (Desktop, Documents, Downloads, etc.)
    /// </summary>
    IReadOnlyList<DirectoryTreeNode> GetSpecialFolders();
    
    /// <summary>
    /// Get root nodes (drives, special folders, network)
    /// </summary>
    Task<IReadOnlyList<DirectoryTreeNode>> GetRootNodesAsync(
        DirectoryTreeOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search in tree
    /// </summary>
    Task<IReadOnlyList<DirectoryTreeNode>> SearchTreeAsync(
        string panelId,
        string searchPattern,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when tree selection changes
    /// </summary>
    event EventHandler<TreeSelectionChangedEventArgs>? SelectionChanged;
    
    /// <summary>
    /// Event raised when tree node is expanded
    /// </summary>
    event EventHandler<TreeNodeExpandedEventArgs>? NodeExpanded;
}

/// <summary>
/// Event args for tree selection change
/// </summary>
public record TreeSelectionChangedEventArgs
{
    public string PanelId { get; init; } = string.Empty;
    public string? OldPath { get; init; }
    public string NewPath { get; init; } = string.Empty;
}

/// <summary>
/// Event args for tree node expansion
/// </summary>
public record TreeNodeExpandedEventArgs
{
    public string PanelId { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsExpanded { get; init; }
}
