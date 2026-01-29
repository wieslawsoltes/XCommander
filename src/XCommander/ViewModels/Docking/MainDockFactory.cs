using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;

namespace XCommander.ViewModels.Docking;

public sealed class MainDockFactory : Factory
{
    public const string RootDockId = "dock-root";
    public const string MainDockId = "dock-main";
    public const string LeftSplitDockId = "dock-left-split";
    public const string BookmarksSplitDockId = "dock-left-bookmarks-split";
    public const string RightSplitDockId = "dock-right-split";
    public const string DirectoryTreeDockId = "dock-directory-tree";
    public const string BookmarksDockId = "dock-bookmarks";
    public const string LeftPanelDockId = "dock-left-panel";
    public const string RightPanelDockId = "dock-right-panel";
    public const string QuickViewDockId = "dock-quick-view";
    public const string DirectoryTreeToolId = "tool-directory-tree";
    public const string BookmarksToolId = "tool-bookmarks";
    public const string LeftPanelToolId = "tool-left-panel";
    public const string RightPanelToolId = "tool-right-panel";
    public const string QuickViewToolId = "tool-quick-view";

    public IRootDock? RootDock { get; private set; }
    public IProportionalDock? MainDock { get; private set; }
    public ISplitViewDock? LeftSplitDock { get; private set; }
    public ISplitViewDock? BookmarksSplitDock { get; private set; }
    public ISplitViewDock? RightSplitDock { get; private set; }
    public IToolDock? DirectoryTreeDock { get; private set; }
    public IToolDock? BookmarksDock { get; private set; }
    public IToolDock? LeftPanelDock { get; private set; }
    public IToolDock? RightPanelDock { get; private set; }
    public IToolDock? QuickViewDock { get; private set; }
    public Tool? DirectoryTreeTool { get; private set; }
    public Tool? BookmarksTool { get; private set; }
    public Tool? LeftPanelTool { get; private set; }
    public Tool? RightPanelTool { get; private set; }
    public Tool? QuickViewTool { get; private set; }

    public MainDockFactory(
        TabbedPanelViewModel leftPanel,
        TabbedPanelViewModel rightPanel,
        DirectoryTreeViewModel? directoryTree,
        BookmarksViewModel bookmarks,
        QuickViewViewModel quickView)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            [DirectoryTreeToolId] = () => directoryTree,
            [BookmarksToolId] = () => bookmarks,
            [LeftPanelToolId] = () => leftPanel,
            [RightPanelToolId] = () => rightPanel,
            [QuickViewToolId] = () => quickView
        };
    }

    public override IRootDock CreateLayout()
    {
        DirectoryTreeTool = CreateTool(DirectoryTreeToolId, "Directory Tree");
        BookmarksTool = CreateTool(BookmarksToolId, "Bookmarks");
        LeftPanelTool = CreateTool(LeftPanelToolId, "Left Panel");
        RightPanelTool = CreateTool(RightPanelToolId, "Right Panel");
        QuickViewTool = CreateTool(QuickViewToolId, "Quick View");

        DirectoryTreeDock = CreateToolDock(DirectoryTreeDockId, new IDockable[] { DirectoryTreeTool });
        BookmarksDock = CreateToolDock(BookmarksDockId, new IDockable[] { BookmarksTool });
        LeftPanelDock = CreateToolDock(LeftPanelDockId, new IDockable[] { LeftPanelTool });
        RightPanelDock = CreateToolDock(RightPanelDockId, new IDockable[] { RightPanelTool });
        QuickViewDock = CreateToolDock(QuickViewDockId, new IDockable[] { QuickViewTool });

        BookmarksSplitDock = CreateSplitViewDock(
            BookmarksSplitDockId,
            BookmarksDock,
            LeftPanelDock,
            SplitViewPanePlacement.Left,
            openPaneLength: 250);
        LeftSplitDock = CreateSplitViewDock(
            LeftSplitDockId,
            DirectoryTreeDock,
            BookmarksSplitDock,
            SplitViewPanePlacement.Left,
            openPaneLength: 220);
        RightSplitDock = CreateSplitViewDock(
            RightSplitDockId,
            QuickViewDock,
            RightPanelDock,
            SplitViewPanePlacement.Right,
            openPaneLength: 350);

        MainDock = CreateProportionalDock();
        MainDock.Id = MainDockId;
        MainDock.Orientation = Dock.Model.Core.Orientation.Horizontal;
        MainDock.VisibleDockables = CreateList<IDockable>(
            LeftSplitDock,
            CreateProportionalDockSplitter(),
            RightSplitDock);
        MainDock.ActiveDockable = LeftSplitDock;

        var root = CreateRootDock();
        root.Id = RootDockId;
        root.VisibleDockables = CreateList<IDockable>(MainDock);
        root.ActiveDockable = MainDock;
        root.DefaultDockable = MainDock;
        RootDock = root;
        return root;
    }

    public void AttachLayout(IRootDock layout)
    {
        RootDock = layout;
        MainDock = FindById<IProportionalDock>(layout, MainDockId);
        LeftSplitDock = FindById<ISplitViewDock>(layout, LeftSplitDockId);
        BookmarksSplitDock = FindById<ISplitViewDock>(layout, BookmarksSplitDockId);
        RightSplitDock = FindById<ISplitViewDock>(layout, RightSplitDockId);
        DirectoryTreeDock = FindById<IToolDock>(layout, DirectoryTreeDockId);
        BookmarksDock = FindById<IToolDock>(layout, BookmarksDockId);
        LeftPanelDock = FindById<IToolDock>(layout, LeftPanelDockId);
        RightPanelDock = FindById<IToolDock>(layout, RightPanelDockId);
        QuickViewDock = FindById<IToolDock>(layout, QuickViewDockId);
        DirectoryTreeTool = FindById<Tool>(layout, DirectoryTreeToolId);
        BookmarksTool = FindById<Tool>(layout, BookmarksToolId);
        LeftPanelTool = FindById<Tool>(layout, LeftPanelToolId);
        RightPanelTool = FindById<Tool>(layout, RightPanelToolId);
        QuickViewTool = FindById<Tool>(layout, QuickViewToolId);
    }

    private Tool CreateTool(string id, string title)
    {
        return new Tool
        {
            Id = id,
            Title = title,
            CanClose = false,
            CanFloat = false,
            CanPin = false
        };
    }

    private IToolDock CreateToolDock(string id, IEnumerable<IDockable> dockables)
    {
        var dock = CreateToolDock();
        dock.Id = id;
        dock.VisibleDockables = CreateList(dockables.ToArray());
        dock.ActiveDockable = GetFirstDockable(dock.VisibleDockables);
        dock.CanCloseLastDockable = false;
        return dock;
    }

    private ISplitViewDock CreateSplitViewDock(
        string id,
        IDockable paneDockable,
        IDockable contentDockable,
        SplitViewPanePlacement placement,
        double openPaneLength)
    {
        var dock = CreateSplitViewDock();
        dock.Id = id;
        dock.PaneDockable = paneDockable;
        dock.ContentDockable = contentDockable;
        dock.PanePlacement = placement;
        dock.DisplayMode = SplitViewDisplayMode.Inline;
        dock.OpenPaneLength = openPaneLength;
        dock.IsPaneOpen = true;
        dock.VisibleDockables = CreateList<IDockable>(paneDockable, contentDockable);
        dock.ActiveDockable = contentDockable;
        return dock;
    }

    private T? FindById<T>(IRootDock root, string id) where T : class, IDockable
    {
        if (root is not IDock dock)
        {
            return null;
        }

        var result = FindDockable(dock, dockable => string.Equals(dockable.Id, id, StringComparison.Ordinal));
        return result as T;
    }

    private static IDockable? GetFirstDockable(IList<IDockable>? dockables)
    {
        if (dockables == null)
        {
            return null;
        }

        foreach (var dockable in dockables)
        {
            if (dockable is not ISplitter)
            {
                return dockable;
            }
        }

        return null;
    }
}
