# Total Commander Parity Plan for XCommander

## Document Version: 1.0
## Created: November 2025
## Last Updated: November 2025

---

## Executive Summary

This document provides a comprehensive analysis of XCommander's feature parity with Total Commander (TC) version 11.x. After reviewing DESIGN.md, all source code, and comparing against Total Commander's feature set, this plan identifies:

- **Current Status**: ~95% feature parity (claimed 100% in DESIGN.md is optimistic)
- **Implemented Features**: 155+ features across file operations, FTP, archives, UI, plugins
- **Gaps Identified**: 25+ features that are documented but missing/incomplete in code
- **UI/UX Differences**: Several workflow and visual differences from TC

---

## Part 1: Feature Comparison Analysis

### 1.1 Features That Work Differently from Total Commander

| Feature | Total Commander | XCommander | Impact |
|---------|-----------------|------------|--------|
| **Quick Search (Type-ahead)** | Letters typed jump to matching file | Ctrl+S opens search dialog | HIGH |
| **F3 Lister** | Full-featured internal viewer with plugins | Basic file viewer | MEDIUM |
| **Drive Buttons** | Clickable drive letters at top | Drive bar with combo boxes | LOW |
| **Copy/Move Dialog** | Detailed dialog with many options | Simpler dialog | MEDIUM |
| **Overwrite Dialog** | Live preview with image comparison | Service exists, UI unclear | MEDIUM |
| **Button Bar** | Horizontal button bar with icons below menu | Toolbar with standard icons | LOW |
| **Status Bar** | Detailed size/free space info | Basic status bar | LOW |
| **Column Auto-Resize** | Double-click header separator | Helper exists, wire-up unclear | LOW |
| **Tabs** | Below path bar | Custom tab implementation | LOW |
| **FTP Mode Switch** | Single connection switching | Separate FTP/SFTP dialogs | LOW |

### 1.2 Features Documented but Not Found in Code

These features are marked ✅ in DESIGN.md but implementation is missing or incomplete:

| Feature | DESIGN.md Status | Actual Status | Details |
|---------|-----------------|---------------|---------|
| **Type-ahead file navigation** | Not mentioned | ❌ Missing | TC's primary quick navigation |
| **NumPad / (restore selection)** | ✅ Done | ⚠️ Partial | Service exists, UI integration unclear |
| **Ctrl+Enter (path to command line)** | ✅ Done | ❓ Unclear | InternalCommandService mentioned but not in OnKeyDown |
| **Alt+Shift+Enter (subdirectory file count)** | ✅ Done | ❓ Unclear | Not found in OnKeyDown handler |
| **Quick View Panel (Ctrl+Q)** | ✅ Done | ⚠️ Exists | Panel exists but toggle unclear |
| **Comments View (descript.ion)** | ✅ Done | ⚠️ Service only | IDescriptionFileService exists, UI integration unclear |
| **Plugin Auto-Update** | ❌ Missing | ❌ Missing | Documented as missing |
| **Plugin Marketplace** | ❌ Missing | ❌ Missing | Documented as missing |
| **UI Automation Tests** | ❌ Pending | ❌ Missing | Documented as pending |
| **Copy with UAC elevation** | ❌ Missing | ❌ Missing | Windows admin elevation for protected files |
| **Cursor in Lister** | ❌ Missing | ❌ Missing | Edit position without full edit mode |

### 1.3 Keyboard Shortcuts Comparison

#### Implemented (Verified in Code)
| Shortcut | Action | File |
|----------|--------|------|
| Tab | Switch panels | MainWindow.axaml.cs |
| Enter | Open item | TabbedFilePanel.axaml.cs |
| Space | Toggle selection | TabbedFilePanel.axaml.cs |
| Insert | Select + move down | TabbedFilePanel.axaml.cs |
| Backspace | Parent directory | TabbedFilePanel.axaml.cs |
| Home/End | First/Last item | TabbedFilePanel.axaml.cs |
| Shift+Home/End | Select range | TabbedFilePanel.axaml.cs |
| Ctrl+PageUp/Down | Navigate directories | TabbedFilePanel.axaml.cs |
| F1 | Help | MainWindow.axaml.cs |
| Alt+Enter | Properties | MainWindow.axaml.cs |
| Ctrl+\ | Go to root | MainWindow.axaml.cs |
| Ctrl+U | Swap panels | MainWindow.axaml.cs |
| Ctrl+L | Calculate dir sizes | MainWindow.axaml.cs |
| Alt+G | Goto path dialog | MainWindow.axaml.cs |
| Alt+F1/F2 | Drive menus | MainWindow.axaml.cs |
| NumPad +/-/* | Pattern select | MainWindow.axaml.cs |
| NumPad / | Restore selection | MainWindow.axaml.cs (partial) |
| Ctrl+B | Brief view | MainWindow.axaml.cs |
| Ctrl+F1/F2 | View modes | MainWindow.axaml.cs |
| F2-F8 | File operations | MainWindow.axaml (KeyBindings) |

#### Missing/Unclear
| Shortcut | Action | Status |
|----------|--------|--------|
| Letter keys | Jump to file | ❌ Not implemented |
| Ctrl+Q | Toggle Quick View | ⚠️ Exists in XAML, logic unclear |
| Ctrl+Enter | Path to command line | ❓ Not in OnKeyDown |
| Shift+Enter | Execute in background | ❓ Not found |
| Alt+Shift+Enter | Show subdir file count | ❓ Not found |
| Ctrl+Shift+Enter | Put quoted path to cmd | ❓ Not found |
| Ctrl+Left/Right | Jump words in command | ❓ Not found |

### 1.4 Service Implementation Status

Total: **120+ service files** in `/Services/`

| Category | Service | Status | Notes |
|----------|---------|--------|-------|
| **Core** | FileSystemService | ✅ Complete | - |
| **Core** | FtpService | ✅ Complete | - |
| **Core** | SftpService | ✅ Complete | - |
| **Core** | ArchiveService | ✅ Complete | SharpCompress |
| **FTP** | FTPClientService | ✅ Complete | FluentFTP |
| **FTP** | FxpTransferService | ✅ Complete | Server-to-server |
| **FTP** | ProxyService | ✅ Complete | SOCKS4/5, HTTP |
| **Cloud** | CloudStorageService | ✅ Interface | Needs real providers |
| **Cloud** | WebDAVService | ✅ Complete | - |
| **Archive** | AdvancedArchiveService | ✅ Complete | - |
| **Archive** | ArchiveSyncService | ✅ Complete | - |
| **Archive** | LegacyArchiveService | ✅ Complete | ACE, ARJ, CAB |
| **Search** | AdvancedSearchService | ✅ Complete | - |
| **Search** | QuickSearchService | ⚠️ Interface | UI integration unclear |
| **UI** | ButtonBarService | ✅ Complete | - |
| **UI** | FileColoringService | ✅ Complete | - |
| **UI** | DragDropService | ✅ Complete | - |
| **UI** | NotificationService | ✅ Complete | Toast UI |
| **UI** | FocusManager | ✅ Complete | - |
| **UI** | TouchModeService | ✅ Complete | - |
| **Tools** | DuplicateFinderService | ✅ Complete | - |
| **Tools** | SecureDeleteService | ✅ Complete | - |
| **Tools** | DiskSpaceAnalyzerService | ✅ Complete | - |
| **Tools** | BatchJobService | ✅ Complete | - |
| **Tools** | PrintService | ✅ Complete | - |
| **Preview** | AdvancedPreviewService | ✅ Complete | - |
| **Preview** | DocumentPreviewService | ✅ Complete | - |
| **Preview** | VideoThumbnailService | ✅ Complete | ffmpeg |
| **Plugin** | PluginService | ✅ Complete | - |
| **Plugin** | ContentPluginService | ✅ Complete | - |
| **Config** | SessionStateService | ✅ Complete | - |
| **Config** | MenuConfigService | ✅ Complete | TC-style menus |

---

## Part 2: Detailed Gap Analysis

### 2.1 Critical Missing Features (HIGH Priority)

#### 2.1.1 Type-Ahead File Navigation
**Description**: In TC, typing letters immediately jumps to files starting with those letters without any key combination.

**Current State**: No implementation found. QuickSearchService exists but requires Ctrl+S activation.

**Required Changes**:
- Add TextInput handler to DataGrid in `TabbedFilePanel.axaml.cs`
- Buffer typed characters with timeout (e.g., 1 second)
- Jump to first matching file name

**Effort**: 2-4 hours
**Files**: `TabbedFilePanel.axaml.cs`, possibly `IQuickSearchService.cs`

```csharp
// Proposed implementation in TabbedFilePanel.axaml.cs
private string _typeAheadBuffer = "";
private DateTime _lastTypeAhead = DateTime.MinValue;
private const int TypeAheadTimeoutMs = 1000;

protected override void OnTextInput(TextInputEventArgs e)
{
    if (string.IsNullOrEmpty(e.Text)) return;
    
    var now = DateTime.Now;
    if ((now - _lastTypeAhead).TotalMilliseconds > TypeAheadTimeoutMs)
        _typeAheadBuffer = "";
    
    _typeAheadBuffer += e.Text;
    _lastTypeAhead = now;
    
    // Find matching item
    var match = Items.FirstOrDefault(i => 
        i.Name.StartsWith(_typeAheadBuffer, StringComparison.OrdinalIgnoreCase));
    if (match != null)
    {
        SelectedItem = match;
        // Scroll to item
    }
    
    e.Handled = true;
}
```

#### 2.1.2 Ctrl+Q Quick View Panel Toggle
**Description**: TC uses Ctrl+Q to toggle a preview panel in the opposite side.

**Current State**: QuickViewPanel.axaml exists, Ctrl+Q documented but not in OnKeyDown handler.

**Required Changes**:
- Add Ctrl+Q handler in `MainWindow.axaml.cs`
- Toggle visibility of QuickViewPanel
- Wire up to inactive panel's selected file

**Effort**: 2-3 hours
**Files**: `MainWindow.axaml.cs`, `MainWindow.axaml`, `QuickViewPanel.axaml.cs`

#### 2.1.3 Ctrl+Enter Path to Command Line
**Description**: TC copies current path + filename to command line for editing.

**Current State**: InternalCommandService has the logic, not wired to keyboard.

**Required Changes**:
- Add Ctrl+Enter handler in `MainWindow.axaml.cs`
- Get selected item path, put in command line TextBox
- Focus command line

**Effort**: 1-2 hours
**Files**: `MainWindow.axaml.cs`, `CommandLine.axaml.cs`

### 2.2 Medium Priority Gaps

#### 2.2.1 Enhanced Copy/Move Dialog
**Description**: TC shows detailed options: overwrite handling, verify after copy, preserve date/time, etc.

**Current State**: Basic file operation dialogs exist.

**Required Changes**:
- Enhance copy/move dialogs with TC-style options
- Wire up to AdvancedCopyService options
- Add verification checkbox, date preservation options

**Effort**: 4-8 hours
**Files**: Create `CopyMoveDialog.axaml`, update file operation flows

#### 2.2.2 Overwrite Dialog UI
**Description**: TC shows side-by-side file comparison with image preview.

**Current State**: `IOverwriteDialogService` interface exists, UI implementation unclear.

**Required Changes**:
- Create `OverwriteDialog.axaml` with:
  - Side-by-side file info
  - Image thumbnail comparison
  - Action buttons (Overwrite, Skip, Rename, etc.)
  - "Remember for all" options

**Effort**: 6-10 hours
**Files**: Create `OverwriteDialog.axaml`, integrate with copy operations

#### 2.2.3 Comments View (descript.ion)
**Description**: TC can show file descriptions from descript.ion files as a column.

**Current State**: `IDescriptionFileService` exists with full API.

**Required Changes**:
- Add "Description" column option
- Wire to DescriptionFileService
- Add context menu to edit descriptions

**Effort**: 4-6 hours
**Files**: `FilePanelViewModel.cs`, `CustomColumnService.cs`, create description editor

#### 2.2.4 Alt+Shift+Enter Subdirectory Count
**Description**: TC calculates and shows file/folder count in selected directories.

**Current State**: Not implemented.

**Required Changes**:
- Add Alt+Shift+Enter handler
- Calculate file count recursively for selected directories
- Show in status bar or tooltip

**Effort**: 2-4 hours
**Files**: `MainWindow.axaml.cs`, `FolderSizeService.cs`

### 2.3 Lower Priority Gaps

#### 2.3.1 F3 Lister Enhancement
**Description**: TC's internal viewer (Lister) has extensive features.

**Current State**: Basic FileViewerDialog exists.

**Gaps**:
- [ ] Cursor positioning without edit mode
- [ ] Plugin-based viewer extensions
- [ ] Search with F7/Shift+F7
- [ ] Font/encoding quick switch
- [ ] Wrap/unwrap toggle

**Effort**: 10-20 hours
**Files**: `FileViewerDialog.axaml`, `FileViewerViewModel.cs`

#### 2.3.2 Plugin Marketplace
**Description**: Browse and install plugins from a repository.

**Current State**: Plugin loading works, no marketplace.

**Required Changes**:
- Design plugin repository format (JSON manifest)
- Create marketplace browse dialog
- Implement download/install flow

**Effort**: 20-40 hours
**Files**: New `PluginMarketplace.axaml`, `IPluginRepository.cs`

#### 2.3.3 wincmd.ini Import
**Description**: Import settings from Total Commander's wincmd.ini file.

**Current State**: Not implemented.

**Required Changes**:
- Parse wincmd.ini format
- Map TC settings to XCommander equivalents
- Import bookmarks, connections, color schemes

**Effort**: 8-16 hours
**Files**: Create `TcConfigImportService.cs`

---

## Part 3: UI/UX Differences and Fixes

### 3.1 Visual Differences

| Element | Total Commander | XCommander | Fix Priority |
|---------|-----------------|------------|--------------|
| Drive bar | Button row with drive letters | ComboBox/buttons | LOW |
| Path bar | Editable text with segments | Breadcrumb-style | OK |
| Status bar | Size, free space, date | Basic info | MEDIUM |
| Function keys | F1-F8 + modifiers | Same | OK |
| Menu bar | Standard Windows menu | Avalonia menu | OK |
| Tabs | Simple tabs below path | TabControl | OK |
| Splitter | Thin grabber | Standard splitter | OK |

### 3.2 Workflow Differences

#### 3.2.1 Selection Workflow
**TC**: Insert key selects and moves down, Space toggles, works on directories too.
**XCommander**: ✅ Same behavior implemented in TabbedFilePanel.axaml.cs

#### 3.2.2 Navigation Workflow
**TC**: Enter on directory enters, Enter on file executes, Backspace goes up.
**XCommander**: ✅ Same behavior implemented

#### 3.2.3 Copy/Move Workflow
**TC**: F5 shows dialog with destination (other panel path), options to verify, etc.
**XCommander**: ⚠️ Simpler - direct operation to inactive panel

#### 3.2.4 Search Workflow
**TC**: Alt+F7 opens search, results can feed to panel, save search templates.
**XCommander**: ✅ Similar with SearchDialog, feed to listbox implemented

### 3.3 Recommended UX Improvements

1. **Add type-ahead navigation** (critical for TC users)
2. **Enhance status bar** with more details (file count, sizes, disk space)
3. **Add overwrite dialog UI** with visual comparison
4. **Improve copy/move dialogs** with more options
5. **Add Quick View toggle** (Ctrl+Q)
6. **Add path to command line** (Ctrl+Enter)

---

## Part 4: Implementation Plan

### Phase 1: Critical Keyboard Shortcuts (1-2 days)
**Goal**: Match TC keyboard behavior

| Task | Effort | Priority |
|------|--------|----------|
| Type-ahead file navigation | 4h | P0 |
| Ctrl+Q Quick View toggle | 3h | P0 |
| Ctrl+Enter path to command line | 2h | P0 |
| Alt+Shift+Enter subdir count | 3h | P1 |
| Ctrl+Shift+Enter quoted path | 1h | P1 |

**Total**: ~13 hours

### Phase 2: Dialog Improvements (3-5 days)
**Goal**: Match TC dialog quality

| Task | Effort | Priority |
|------|--------|----------|
| Copy/Move dialog options | 8h | P1 |
| Overwrite dialog with preview | 10h | P1 |
| Enhanced delete confirmation | 4h | P2 |
| Quick filter inline UI | 6h | P2 |

**Total**: ~28 hours

### Phase 3: View Enhancements (2-3 days)
**Goal**: Match TC view modes

| Task | Effort | Priority |
|------|--------|----------|
| Comments column (descript.ion) | 6h | P2 |
| Enhanced status bar | 4h | P2 |
| F3 Lister improvements | 16h | P3 |

**Total**: ~26 hours

### Phase 4: Integration & Polish (2-3 days)
**Goal**: Complete parity

| Task | Effort | Priority |
|------|--------|----------|
| wincmd.ini import | 12h | P3 |
| Plugin marketplace UI | 20h | P4 |
| UI automation tests | 16h | P3 |

**Total**: ~48 hours

---

## Part 5: Testing Checklist

### 5.1 Keyboard Navigation Tests
- [ ] Type letters to jump to file
- [ ] Ctrl+Q toggles quick view
- [ ] Ctrl+Enter puts path in command line
- [ ] Alt+Shift+Enter shows subdir stats
- [ ] All NumPad operations work
- [ ] Shift+Home/End selects ranges
- [ ] Ctrl+PageUp/Down navigates

### 5.2 File Operation Tests
- [ ] F5 Copy shows dialog with options
- [ ] Overwrite dialog shows file comparison
- [ ] Copy verification works
- [ ] Large file progress is accurate
- [ ] Cancel works mid-operation

### 5.3 Visual Tests
- [ ] Status bar shows correct info
- [ ] File coloring by extension works
- [ ] Alternating row colors work
- [ ] High contrast theme is usable
- [ ] Touch mode spacing works

### 5.4 Cross-Platform Tests
- [ ] Windows paths work
- [ ] macOS paths work
- [ ] Linux paths work
- [ ] Archive handling on all platforms
- [ ] FTP/SFTP on all platforms

---

## Part 6: Files to Modify

### Priority 1 (Critical)
```
src/XCommander/Views/TabbedFilePanel.axaml.cs  - Type-ahead navigation
src/XCommander/Views/MainWindow.axaml.cs       - Ctrl+Q, Ctrl+Enter handlers
src/XCommander/Views/MainWindow.axaml          - Quick View visibility binding
```

### Priority 2 (Important)
```
src/XCommander/Views/Dialogs/CopyMoveDialog.axaml     - CREATE (new)
src/XCommander/Views/Dialogs/OverwriteDialog.axaml   - CREATE (new)
src/XCommander/ViewModels/CopyMoveViewModel.cs        - CREATE (new)
src/XCommander/ViewModels/OverwriteViewModel.cs       - CREATE (new)
```

### Priority 3 (Enhancement)
```
src/XCommander/Views/FileViewerDialog.axaml       - Lister enhancements
src/XCommander/ViewModels/FileViewerViewModel.cs  - Lister features
src/XCommander/Controls/StatusBar.axaml           - Enhanced status
src/XCommander/Services/TcConfigImportService.cs  - CREATE (new)
```

---

## Part 7: Summary

### What Works Well ✅
- Dual panel interface with tabs
- File operations (copy, move, delete, rename)
- Archive handling (ZIP, 7z, RAR, TAR)
- FTP/SFTP connections
- Search with feed to listbox
- Multi-rename tool
- Directory comparison and sync
- Keyboard shortcuts (most of them)
- Dark/Light themes
- Plugin system foundation
- 120+ services for TC features

### What Needs Work ⚠️
- Type-ahead file navigation (critical)
- Quick View toggle (Ctrl+Q)
- Path to command line (Ctrl+Enter)
- Overwrite dialog UI
- Copy/Move dialog options
- F3 Lister enhancements
- Comments column
- Enhanced status bar
- wincmd.ini import
- Plugin marketplace

### Estimated Total Effort
- **Phase 1 (Critical)**: 2 days
- **Phase 2 (Dialogs)**: 5 days
- **Phase 3 (Views)**: 3 days
- **Phase 4 (Polish)**: 3 days
- **Total**: ~13 days for full TC parity

### Current Parity Assessment
| Metric | Value |
|--------|-------|
| **Features Implemented** | ~155 |
| **Features Partial** | ~15 |
| **Features Missing** | ~10 |
| **True Parity** | ~85-90% |
| **After Phase 1** | ~95% |
| **After All Phases** | ~99% |

---

## Appendix A: Complete Keyboard Shortcut Reference

### Navigation
| Key | Action | Status |
|-----|--------|--------|
| Tab | Switch panels | ✅ |
| Enter | Open/Execute | ✅ |
| Backspace | Parent dir | ✅ |
| Alt+Left | Go back | ✅ |
| Alt+Right | Go forward | ✅ |
| Ctrl+\ | Go to root | ✅ |
| Ctrl+PageUp | Parent dir | ✅ |
| Ctrl+PageDown | Enter dir | ✅ |
| Alt+G | Goto dialog | ✅ |
| Home | First item | ✅ |
| End | Last item | ✅ |
| Letters | Jump to file | ❌ |

### Selection
| Key | Action | Status |
|-----|--------|--------|
| Space | Toggle select | ✅ |
| Insert | Select+down | ✅ |
| Shift+Home | Select to first | ✅ |
| Shift+End | Select to last | ✅ |
| Ctrl+A | Select all | ✅ |
| NumPad + | Pattern select | ✅ |
| NumPad - | Pattern deselect | ✅ |
| NumPad * | Invert | ✅ |
| NumPad / | Restore | ⚠️ |

### File Operations
| Key | Action | Status |
|-----|--------|--------|
| F1 | Help | ✅ |
| F2 | Rename | ✅ |
| F3 | View | ✅ |
| F4 | Edit | ✅ |
| F5 | Copy | ✅ |
| F6 | Move | ✅ |
| F7 | New folder | ✅ |
| F8/Delete | Delete | ✅ |
| Shift+F4 | New file | ✅ |
| Alt+Enter | Properties | ✅ |

### View
| Key | Action | Status |
|-----|--------|--------|
| Ctrl+B | Brief view | ✅ |
| Ctrl+F1 | Thumbnails | ✅ |
| Ctrl+F2 | Details view | ✅ |
| Ctrl+H | Show hidden | ✅ |
| Ctrl+Q | Quick view | ⚠️ |
| Ctrl+R | Refresh | ✅ |

### Panel Operations
| Key | Action | Status |
|-----|--------|--------|
| Ctrl+U | Swap panels | ✅ |
| Alt+F1 | Left drive menu | ✅ |
| Alt+F2 | Right drive menu | ✅ |
| Ctrl+L | Calc dir sizes | ✅ |

### Command Line
| Key | Action | Status |
|-----|--------|--------|
| Ctrl+Enter | Path to cmdline | ❌ |
| Ctrl+Shift+Enter | Quoted path | ❌ |
| Alt+Shift+Enter | Subdir count | ❌ |

### Tabs
| Key | Action | Status |
|-----|--------|--------|
| Ctrl+T | New tab | ✅ |
| Ctrl+W | Close tab | ✅ |
| Ctrl+Tab | Next tab | ✅ |
| Ctrl+Shift+Tab | Prev tab | ✅ |

---

## Appendix B: Service Interface List

```
IAccessibilityService
IAdvancedArchiveService
IAdvancedCopyService
IAdvancedPreviewService
IAdvancedSearchService
IArchiveService
IArchiveSyncService
IArchiveToArchiveService
IBackgroundArchiveService
IBackgroundTransferService
IBatchJobService
IBookmarkService
IBranchViewService
IButtonBarService
ICloudStorageService
IContentPluginService
ICustomColumnService
ICustomViewModesService
IDescriptionFileService
IDirectoryHotlistService
IDirectorySyncService
IDirectoryTreeService
IDiskSpaceAnalyzerService
IDocumentPreviewService
IDragDropService
IDuplicateFinderService
IEncodingService
IFileAttributeService
IFileChecksumService
IFileColoringService
IFileLinkService
IFileLoggingService
IFileSplitService
IFileSystemService
IFlatViewService
IFocusManager
IFolderSizeService
IFtpService
IFTPClientService
IFxpTransferService
IGitService
IIconService
IInternalCommandService
ILegacyArchiveService
ILoggingService
ILongPathService
IMainframeFtpService
IMenuConfigService
IMultiRenameToolService
INetworkBrowserService
INotificationService
IOperationLogService
IOverwriteDialogService
IPasswordManagerService
IPluginService
IPrintService
IProxyService
IQuickFilterService
IQuickSearchService
IRtlSupportService
ISecureDeleteService
ISelectionHistoryService
ISelectionService
ISessionStateService
ISftpService
ISplitMergeService
ISystemToolsService
ITextEncodingService
ITouchModeService
ITransferQueueService
IUsbTransferService
IUserMenuService
IVideoThumbnailService
IVirtualScrollingService
IWebDAVService
```

---

*This document should be updated as features are implemented.*
*Priority: P0 = Critical, P1 = High, P2 = Medium, P3 = Low, P4 = Nice-to-have*
