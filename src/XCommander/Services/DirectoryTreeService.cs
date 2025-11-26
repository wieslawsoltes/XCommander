using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of IDirectoryTreeService for independent panel trees.
/// </summary>
public class DirectoryTreeService : IDirectoryTreeService
{
    private readonly ConcurrentDictionary<string, DirectoryTreeNode> _panelTrees = new();
    private readonly ConcurrentDictionary<string, DirectoryTreeOptions> _panelOptions = new();
    private readonly ConcurrentDictionary<string, string?> _selectedPaths = new();
    private readonly ConcurrentDictionary<string, List<DirectoryTreeNode>> _panelFavorites = new();
    private readonly ILongPathService? _longPathService;

    public event EventHandler<TreeSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TreeNodeExpandedEventArgs>? NodeExpanded;

    public DirectoryTreeService(ILongPathService? longPathService = null)
    {
        _longPathService = longPathService;
    }

    public async Task<DirectoryTreeNode> GetTreeForPanelAsync(
        string panelId,
        CancellationToken cancellationToken = default)
    {
        if (_panelTrees.TryGetValue(panelId, out var existingTree))
        {
            return existingTree;
        }

        // Create new tree with root nodes
        var options = GetOptions(panelId);
        var rootNodes = await GetRootNodesAsync(options, cancellationToken);

        var tree = new DirectoryTreeNode
        {
            Path = string.Empty,
            Name = "Computer",
            IsExpanded = true,
            ChildrenLoaded = true,
            Children = rootNodes.ToList()
        };

        _panelTrees[panelId] = tree;
        return tree;
    }

    public async Task<IReadOnlyList<DirectoryTreeNode>> LoadChildrenAsync(
        string path,
        DirectoryTreeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DirectoryTreeOptions();
        var children = new List<DirectoryTreeNode>();

        await Task.Run(() =>
        {
            try
            {
                var normalizedPath = _longPathService?.NormalizePath(path) ?? path;
                var directories = Directory.GetDirectories(normalizedPath);

                foreach (var dir in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        
                        // Filter based on options
                        if (!options.ShowHidden && (dirInfo.Attributes & FileAttributes.Hidden) != 0)
                            continue;
                        if (!options.ShowSystem && (dirInfo.Attributes & FileAttributes.System) != 0)
                            continue;

                        var hasSubdirs = false;
                        try
                        {
                            hasSubdirs = Directory.GetDirectories(dir).Length > 0;
                        }
                        catch
                        {
                            // Can't access subdirectories
                        }

                        children.Add(new DirectoryTreeNode
                        {
                            Path = dir,
                            Name = dirInfo.Name,
                            ParentPath = path,
                            IsAccessible = true,
                            SubdirectoryCount = hasSubdirs ? 1 : 0 // Just indicate if there are any
                        });
                    }
                    catch (Exception ex)
                    {
                        // Add inaccessible node
                        children.Add(new DirectoryTreeNode
                        {
                            Path = dir,
                            Name = Path.GetFileName(dir),
                            ParentPath = path,
                            IsAccessible = false,
                            AccessError = ex.Message
                        });
                    }
                }

                if (options.SortAlphabetically)
                {
                    children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch
            {
                // Directory not accessible
            }
        }, cancellationToken);

        return children;
    }

    public async Task<DirectoryTreeNode> ExpandNodeAsync(
        string panelId,
        string path,
        CancellationToken cancellationToken = default)
    {
        var tree = await GetTreeForPanelAsync(panelId, cancellationToken);
        var node = FindNode(tree, path);

        if (node != null && !node.ChildrenLoaded)
        {
            var options = GetOptions(panelId);
            var children = await LoadChildrenAsync(path, options, cancellationToken);
            
            node.Children.Clear();
            node.Children.AddRange(children);
            node.ChildrenLoaded = true;
        }

        if (node != null)
        {
            node.IsExpanded = true;
            NodeExpanded?.Invoke(this, new TreeNodeExpandedEventArgs
            {
                PanelId = panelId,
                Path = path,
                IsExpanded = true
            });
        }

        return node ?? tree;
    }

    public void CollapseNode(string panelId, string path)
    {
        if (_panelTrees.TryGetValue(panelId, out var tree))
        {
            var node = FindNode(tree, path);
            if (node != null)
            {
                node.IsExpanded = false;
                NodeExpanded?.Invoke(this, new TreeNodeExpandedEventArgs
                {
                    PanelId = panelId,
                    Path = path,
                    IsExpanded = false
                });
            }
        }
    }

    public async Task SelectNodeAsync(string panelId, string path)
    {
        var oldPath = _selectedPaths.GetValueOrDefault(panelId);
        _selectedPaths[panelId] = path;

        if (_panelTrees.TryGetValue(panelId, out var tree))
        {
            // Clear old selection
            if (!string.IsNullOrEmpty(oldPath))
            {
                var oldNode = FindNode(tree, oldPath);
                if (oldNode != null)
                {
                    oldNode.IsSelected = false;
                }
            }

            // Set new selection
            var newNode = FindNode(tree, path);
            if (newNode != null)
            {
                newNode.IsSelected = true;
            }
        }

        SelectionChanged?.Invoke(this, new TreeSelectionChangedEventArgs
        {
            PanelId = panelId,
            OldPath = oldPath,
            NewPath = path
        });

        await Task.CompletedTask;
    }

    public async Task NavigateToPathAsync(
        string panelId,
        string path,
        bool expandToPath = true,
        CancellationToken cancellationToken = default)
    {
        if (!expandToPath)
        {
            await SelectNodeAsync(panelId, path);
            return;
        }

        // Get all path segments and expand each
        var segments = GetPathSegments(path);
        var tree = await GetTreeForPanelAsync(panelId, cancellationToken);

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExpandNodeAsync(panelId, segment, cancellationToken);
        }

        await SelectNodeAsync(panelId, path);
    }

    private static List<string> GetPathSegments(string path)
    {
        var segments = new List<string>();
        var current = path;

        while (!string.IsNullOrEmpty(current))
        {
            segments.Insert(0, current);
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break; // Root reached
            current = parent ?? string.Empty;
        }

        return segments;
    }

    public string? GetSelectedPath(string panelId)
    {
        return _selectedPaths.GetValueOrDefault(panelId);
    }

    public async Task RefreshNodeAsync(
        string panelId,
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_panelTrees.TryGetValue(panelId, out var tree))
        {
            var node = FindNode(tree, path);
            if (node != null)
            {
                node.ChildrenLoaded = false;
                node.Children.Clear();
                
                if (node.IsExpanded)
                {
                    await ExpandNodeAsync(panelId, path, cancellationToken);
                }
            }
        }
    }

    public async Task RefreshTreeAsync(
        string panelId,
        CancellationToken cancellationToken = default)
    {
        _panelTrees.TryRemove(panelId, out _);
        await GetTreeForPanelAsync(panelId, cancellationToken);
    }

    public DirectoryTreeOptions GetOptions(string panelId)
    {
        return _panelOptions.GetValueOrDefault(panelId) ?? new DirectoryTreeOptions();
    }

    public void SetOptions(string panelId, DirectoryTreeOptions options)
    {
        _panelOptions[panelId] = options;
    }

    public void SetSyncMode(string panelId, TreeSyncMode mode)
    {
        var options = GetOptions(panelId);
        SetOptions(panelId, options with { SyncMode = mode });
    }

    public IReadOnlyList<DirectoryTreeNode> GetTreeFavorites(string panelId)
    {
        return _panelFavorites.GetValueOrDefault(panelId) ?? new List<DirectoryTreeNode>();
    }

    public Task AddToTreeFavoritesAsync(string panelId, string path)
    {
        var favorites = _panelFavorites.GetOrAdd(panelId, _ => new List<DirectoryTreeNode>());
        
        if (!favorites.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            favorites.Add(new DirectoryTreeNode
            {
                Path = path,
                Name = Path.GetFileName(path) ?? path,
                IsFavorite = true
            });
        }

        return Task.CompletedTask;
    }

    public Task RemoveFromTreeFavoritesAsync(string panelId, string path)
    {
        if (_panelFavorites.TryGetValue(panelId, out var favorites))
        {
            favorites.RemoveAll(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<DirectoryTreeNode> GetSpecialFolders()
    {
        var specialFolders = new List<DirectoryTreeNode>();
        
        var folders = new[]
        {
            (Environment.SpecialFolder.Desktop, "Desktop"),
            (Environment.SpecialFolder.MyDocuments, "Documents"),
            (Environment.SpecialFolder.MyMusic, "Music"),
            (Environment.SpecialFolder.MyPictures, "Pictures"),
            (Environment.SpecialFolder.MyVideos, "Videos"),
            (Environment.SpecialFolder.UserProfile, "Home"),
        };

        foreach (var (folder, name) in folders)
        {
            try
            {
                var path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    specialFolders.Add(new DirectoryTreeNode
                    {
                        Path = path,
                        Name = name,
                        IsSpecialFolder = true,
                        SpecialFolderType = folder,
                        IconKey = $"special_{name.ToLowerInvariant()}"
                    });
                }
            }
            catch
            {
                // Ignore inaccessible special folders
            }
        }

        return specialFolders;
    }

    public async Task<IReadOnlyList<DirectoryTreeNode>> GetRootNodesAsync(
        DirectoryTreeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DirectoryTreeOptions();
        var rootNodes = new List<DirectoryTreeNode>();

        await Task.Run(() =>
        {
            // Add special folders first
            if (options.ShowSpecialFolders)
            {
                rootNodes.AddRange(GetSpecialFolders());
            }

            // Add drives
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var isReady = drive.IsReady;
                        var name = isReady 
                            ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd(Path.DirectorySeparatorChar)})"
                            : drive.Name.TrimEnd(Path.DirectorySeparatorChar);

                        rootNodes.Add(new DirectoryTreeNode
                        {
                            Path = drive.Name,
                            Name = name,
                            IsAccessible = isReady,
                            IconKey = drive.DriveType switch
                            {
                                DriveType.Fixed => "drive_fixed",
                                DriveType.Removable => "drive_removable",
                                DriveType.Network => "drive_network",
                                DriveType.CDRom => "drive_cdrom",
                                _ => "drive"
                            }
                        });
                    }
                    catch
                    {
                        rootNodes.Add(new DirectoryTreeNode
                        {
                            Path = drive.Name,
                            Name = drive.Name,
                            IsAccessible = false
                        });
                    }
                }
            }
            catch
            {
                // Ignore drive enumeration errors
            }
        }, cancellationToken);

        return rootNodes;
    }

    public async Task<IReadOnlyList<DirectoryTreeNode>> SearchTreeAsync(
        string panelId,
        string searchPattern,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DirectoryTreeNode>();
        var tree = await GetTreeForPanelAsync(panelId, cancellationToken);

        void SearchRecursive(DirectoryTreeNode node)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (node.Name.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(node);
            }

            foreach (var child in node.Children)
            {
                SearchRecursive(child);
            }
        }

        SearchRecursive(tree);
        return results;
    }

    private static DirectoryTreeNode? FindNode(DirectoryTreeNode root, string path)
    {
        if (root.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var found = FindNode(child, path);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
