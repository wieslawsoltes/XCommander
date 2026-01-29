using System.Collections.Generic;

namespace XCommander.Services;

/// <summary>
/// Maps Total Commander command identifiers to XCommander command names.
/// </summary>
public static class TcCommandMapper
{
    private static readonly Dictionary<string, string> ToolbarCommandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cm_Copy"] = "CopySelected",
        ["cm_Move"] = "MoveSelected",
        ["cm_Delete"] = "DeleteSelected",
        ["cm_RenameOnly"] = "RenameSelected",
        ["cm_View"] = "ViewSelected",
        ["cm_Edit"] = "EditSelected",
        ["cm_EditNew"] = "CreateNewFile",
        ["cm_MkDir"] = "CreateNewFolder",
        ["cm_SearchFor"] = "Search",
        ["cm_NewTab"] = "NewTab",
        ["cm_CloseTab"] = "CloseTab",
        ["cm_SwitchPanel"] = "SwitchPanel",
        ["cm_GoToParent"] = "GoToParent",
        ["cm_GoBack"] = "GoBack",
        ["cm_GoForward"] = "GoForward",
        ["cm_GoHome"] = "GoHome",
        ["cm_RereadSource"] = "Refresh",
        ["cm_Refresh"] = "Refresh",
        ["cm_CompareDirs"] = "CompareDirectories",
        ["cm_CompareFiles"] = "CompareFiles",
        ["cm_SyncDirs"] = "SyncDirectories",
        ["cm_MultiRenameFiles"] = "MultiRename",
        ["cm_Split"] = "SplitFile",
        ["cm_Combine"] = "CombineFiles",
        ["cm_OpenArchive"] = "OpenArchive",
        ["cm_PackFiles"] = "CreateArchive",
        ["cm_UnpackFiles"] = "ExtractArchive",
        ["cm_FtpConnect"] = "FtpConnect",
        ["cm_SftpConnect"] = "SftpConnect",
        ["cm_Config"] = "Settings",
        ["cm_CommandBrowser"] = "CommandPalette",
        ["cm_Help"] = "Help"
    };

    private static readonly Dictionary<string, string> ButtonBarCommandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cm_Copy"] = "file.copy",
        ["cm_Move"] = "file.move",
        ["cm_Delete"] = "file.delete",
        ["cm_RenameOnly"] = "file.rename",
        ["cm_View"] = "file.view",
        ["cm_Edit"] = "file.edit",
        ["cm_EditNew"] = "file.newFile",
        ["cm_MkDir"] = "file.newFolder",
        ["cm_SearchFor"] = "tools.search",
        ["cm_CompareDirs"] = "tools.compare",
        ["cm_SyncDirs"] = "tools.sync",
        ["cm_PackFiles"] = "tools.pack",
        ["cm_UnpackFiles"] = "tools.unpack",
        ["cm_GoToParent"] = "nav.parent",
        ["cm_GoHome"] = "nav.home",
        ["cm_GoBack"] = "nav.back",
        ["cm_GoForward"] = "nav.forward",
        ["cm_RereadSource"] = "nav.refresh",
        ["cm_Refresh"] = "nav.refresh",
        ["cm_SelectAll"] = "select.all",
        ["cm_ClearAll"] = "select.none",
        ["cm_ReverseSelection"] = "select.invert",
        ["cm_Exchange"] = "panel.swap"
    };

    public static bool TryMapToToolbarCommand(string tcCommand, out string commandName)
    {
        if (string.IsNullOrWhiteSpace(tcCommand))
        {
            commandName = string.Empty;
            return false;
        }

        if (ToolbarCommandMap.TryGetValue(tcCommand.Trim(), out var mapped))
        {
            commandName = mapped;
            return true;
        }

        commandName = string.Empty;
        return false;
    }

    public static bool TryMapToButtonBarCommand(string tcCommand, out string commandId)
    {
        if (string.IsNullOrWhiteSpace(tcCommand))
        {
            commandId = string.Empty;
            return false;
        }

        if (ButtonBarCommandMap.TryGetValue(tcCommand.Trim(), out var mapped))
        {
            commandId = mapped;
            return true;
        }

        commandId = string.Empty;
        return false;
    }
}
