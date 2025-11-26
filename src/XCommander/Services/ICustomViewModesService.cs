// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for managing custom view modes.
/// Similar to Total Commander's configurable column layouts and view styles.
/// </summary>
public interface ICustomViewModesService
{
    /// <summary>
    /// Gets available view modes.
    /// </summary>
    IReadOnlyList<ViewMode> ViewModes { get; }
    
    /// <summary>
    /// Gets the currently active view mode.
    /// </summary>
    ViewMode? ActiveViewMode { get; }
    
    /// <summary>
    /// Creates a new view mode.
    /// </summary>
    Task<ViewMode> CreateAsync(ViewModeOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing view mode.
    /// </summary>
    Task UpdateAsync(ViewMode viewMode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a view mode.
    /// </summary>
    Task DeleteAsync(string viewModeId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a view mode by ID.
    /// </summary>
    Task<ViewMode?> GetAsync(string viewModeId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activates a view mode.
    /// </summary>
    Task ActivateAsync(string viewModeId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Duplicates a view mode.
    /// </summary>
    Task<ViewMode> DuplicateAsync(string viewModeId, string newName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports a view mode from a file.
    /// </summary>
    Task<ViewMode> ImportAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a view mode to a file.
    /// </summary>
    Task ExportAsync(string viewModeId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resets to default view modes.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets suggested view mode for a directory based on its contents.
    /// </summary>
    Task<ViewMode?> GetSuggestedViewModeAsync(string directoryPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets view mode auto-selection rule for a path pattern.
    /// </summary>
    Task SetAutoSelectRuleAsync(string pathPattern, string viewModeId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets auto-selection rules.
    /// </summary>
    IReadOnlyList<ViewModeAutoSelectRule> AutoSelectRules { get; }
    
    /// <summary>
    /// Event raised when view mode changes.
    /// </summary>
    event EventHandler<ViewModeChangedEventArgs>? ViewModeChanged;
}

/// <summary>
/// Options for creating a view mode.
/// </summary>
public class ViewModeOptions
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ViewStyle Style { get; set; } = ViewStyle.Details;
    public List<ColumnDefinition> Columns { get; init; } = new();
    public SortDefinition? DefaultSort { get; set; }
    public GroupDefinition? Grouping { get; set; }
    public FilterDefinition? DefaultFilter { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowSystemFiles { get; set; }
    public ThumbnailSettings? Thumbnails { get; set; }
    public string? IconPath { get; set; }
}

/// <summary>
/// A custom view mode configuration.
/// </summary>
public class ViewMode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public ViewStyle Style { get; set; }
    public List<ColumnDefinition> Columns { get; init; } = new();
    public SortDefinition? DefaultSort { get; set; }
    public GroupDefinition? Grouping { get; set; }
    public FilterDefinition? DefaultFilter { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool ShowSystemFiles { get; set; }
    public ThumbnailSettings? Thumbnails { get; set; }
    public string? IconPath { get; set; }
    public string? KeyboardShortcut { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// View styles.
/// </summary>
public enum ViewStyle
{
    Details,
    List,
    LargeIcons,
    SmallIcons,
    Tiles,
    Content,
    Thumbnails,
    Custom
}

/// <summary>
/// Column definition for detail view.
/// </summary>
public class ColumnDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public ColumnType Type { get; set; }
    public double Width { get; set; } = 100;
    public double? MinWidth { get; set; }
    public double? MaxWidth { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool CanResize { get; set; } = true;
    public bool CanReorder { get; set; } = true;
    public bool CanSort { get; set; } = true;
    public int Order { get; set; }
    public TextAlignment Alignment { get; set; }
    public string? Format { get; set; }
    public string? CustomRenderer { get; set; }
    public bool ShowInTooltip { get; set; }
}

/// <summary>
/// Column types.
/// </summary>
public enum ColumnType
{
    Text,
    Number,
    Size,
    DateTime,
    Boolean,
    Icon,
    Progress,
    Rating,
    Tags,
    Custom
}

/// <summary>
/// Text alignment.
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Sort definition.
/// </summary>
public class SortDefinition
{
    public string ColumnId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public SortDirection Direction { get; set; }
    public bool FoldersFirst { get; set; } = true;
    public List<SortDefinition>? ThenBy { get; set; }
}

/// <summary>
/// Sort direction.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Group definition.
/// </summary>
public class GroupDefinition
{
    public string Field { get; set; } = string.Empty;
    public GroupingType Type { get; set; }
    public SortDirection SortDirection { get; set; }
    public bool CollapsedByDefault { get; set; }
    public bool ShowEmptyGroups { get; set; }
    public string? CustomGrouper { get; set; }
}

/// <summary>
/// Grouping types.
/// </summary>
public enum GroupingType
{
    None,
    ByValue,
    ByFirstLetter,
    ByDateRange,
    BySizeRange,
    ByType,
    ByExtension,
    Custom
}

/// <summary>
/// Filter definition.
/// </summary>
public class FilterDefinition
{
    public string? FilePattern { get; set; }
    public IReadOnlyList<string>? Extensions { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public DateTime? ModifiedAfter { get; set; }
    public DateTime? ModifiedBefore { get; set; }
    public FileAttributeFilter? Attributes { get; set; }
    public string? ContentFilter { get; set; }
    public List<FilterCondition>? CustomConditions { get; set; }
}

/// <summary>
/// File attribute filter.
/// </summary>
public class FileAttributeFilter
{
    public bool? IsReadOnly { get; set; }
    public bool? IsHidden { get; set; }
    public bool? IsSystem { get; set; }
    public bool? IsDirectory { get; set; }
    public bool? IsArchive { get; set; }
    public bool? IsEncrypted { get; set; }
    public bool? IsCompressed { get; set; }
}

/// <summary>
/// Custom filter condition.
/// </summary>
public class FilterCondition
{
    public string Field { get; set; } = string.Empty;
    public FilterOperator Operator { get; set; }
    public object? Value { get; set; }
    public FilterLogic Logic { get; set; }
}

/// <summary>
/// Filter operators.
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Between,
    In,
    IsNull,
    IsNotNull,
    Matches
}

/// <summary>
/// Filter logic operators.
/// </summary>
public enum FilterLogic
{
    And,
    Or
}

/// <summary>
/// Thumbnail settings.
/// </summary>
public class ThumbnailSettings
{
    public int Size { get; set; } = 120;
    public ThumbnailQuality Quality { get; set; } = ThumbnailQuality.Medium;
    public bool ShowForImages { get; set; } = true;
    public bool ShowForVideos { get; set; } = true;
    public bool ShowForPDFs { get; set; }
    public bool ShowForDocuments { get; set; }
    public bool UseShellThumbnails { get; set; } = true;
    public int CacheSize { get; set; } = 1000;
}

/// <summary>
/// Thumbnail quality levels.
/// </summary>
public enum ThumbnailQuality
{
    Low,
    Medium,
    High
}

/// <summary>
/// Auto-selection rule for view modes.
/// </summary>
public class ViewModeAutoSelectRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string PathPattern { get; set; } = string.Empty;
    public string ViewModeId { get; set; } = string.Empty;
    public AutoSelectRuleType Type { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Types of auto-selection rules.
/// </summary>
public enum AutoSelectRuleType
{
    PathWildcard,
    PathRegex,
    Extension,
    ContentType,
    Custom
}

/// <summary>
/// Event args for view mode changes.
/// </summary>
public class ViewModeChangedEventArgs : EventArgs
{
    public ViewMode? PreviousViewMode { get; init; }
    public ViewMode? NewViewMode { get; init; }
    public ViewModeChangeReason Reason { get; init; }
}

/// <summary>
/// Reasons for view mode changes.
/// </summary>
public enum ViewModeChangeReason
{
    UserSelection,
    AutoSelect,
    Reset,
    Import
}
