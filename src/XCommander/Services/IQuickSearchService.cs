using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Quick search/filter mode for instant file filtering as user types.
/// TC equivalent: Ctrl+S quick filter, quick search as you type
/// </summary>
public enum QuickSearchMode
{
    /// <summary>Filter visible files (hide non-matching)</summary>
    Filter,
    
    /// <summary>Jump to first matching file</summary>
    JumpToFirst,
    
    /// <summary>Select all matching files</summary>
    SelectMatching,
    
    /// <summary>Highlight matching files without filtering</summary>
    Highlight
}

/// <summary>
/// Match type for quick search
/// </summary>
public enum QuickSearchMatchType
{
    /// <summary>Match anywhere in filename</summary>
    Contains,
    
    /// <summary>Match from start of filename</summary>
    StartsWith,
    
    /// <summary>Match using wildcard pattern (* and ?)</summary>
    Wildcard,
    
    /// <summary>Match using regular expression</summary>
    Regex,
    
    /// <summary>Match by initials (e.g., "fp" matches "FilePanel.cs")</summary>
    Initials
}

/// <summary>
/// Quick search options
/// </summary>
public record QuickSearchOptions
{
    /// <summary>Search mode (filter, jump, select, highlight)</summary>
    public QuickSearchMode Mode { get; init; } = QuickSearchMode.Filter;
    
    /// <summary>Match type (contains, starts with, wildcard, regex)</summary>
    public QuickSearchMatchType MatchType { get; init; } = QuickSearchMatchType.Contains;
    
    /// <summary>Case-sensitive matching</summary>
    public bool CaseSensitive { get; init; } = false;
    
    /// <summary>Match against extension separately</summary>
    public bool MatchExtensionSeparately { get; init; } = false;
    
    /// <summary>Include hidden files in search</summary>
    public bool IncludeHidden { get; init; } = true;
    
    /// <summary>Include directories in search</summary>
    public bool IncludeDirectories { get; init; } = true;
    
    /// <summary>Auto-clear filter after delay (0 = never)</summary>
    public TimeSpan AutoClearDelay { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Quick search result
/// </summary>
public record QuickSearchResult
{
    /// <summary>Original file list count</summary>
    public int OriginalCount { get; init; }
    
    /// <summary>Filtered/matched file count</summary>
    public int MatchedCount { get; init; }
    
    /// <summary>Matched file paths</summary>
    public IReadOnlyList<string> MatchedPaths { get; init; } = Array.Empty<string>();
    
    /// <summary>Index of first match (for JumpToFirst mode)</summary>
    public int FirstMatchIndex { get; init; } = -1;
    
    /// <summary>Search pattern used</summary>
    public string Pattern { get; init; } = string.Empty;
    
    /// <summary>Time taken for search</summary>
    public TimeSpan SearchDuration { get; init; }
}

/// <summary>
/// Saved quick search preset
/// </summary>
public record QuickSearchPreset
{
    /// <summary>Preset name</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Search pattern</summary>
    public string Pattern { get; init; } = string.Empty;
    
    /// <summary>Search options</summary>
    public QuickSearchOptions Options { get; init; } = new();
    
    /// <summary>Hotkey for this preset</summary>
    public string? Hotkey { get; init; }
    
    /// <summary>Usage count for sorting by most used</summary>
    public int UsageCount { get; init; }
}

/// <summary>
/// Service for TC-style quick search and filtering as user types.
/// Provides instant file filtering in current panel.
/// </summary>
public interface IQuickSearchService
{
    /// <summary>
    /// Start quick search mode in panel
    /// </summary>
    void BeginQuickSearch(string panelId);
    
    /// <summary>
    /// End quick search mode
    /// </summary>
    void EndQuickSearch(string panelId);
    
    /// <summary>
    /// Check if quick search is active
    /// </summary>
    bool IsQuickSearchActive(string panelId);
    
    /// <summary>
    /// Get current quick search pattern
    /// </summary>
    string GetCurrentPattern(string panelId);
    
    /// <summary>
    /// Update search pattern (called on each keypress)
    /// </summary>
    QuickSearchResult UpdatePattern(string panelId, string pattern, QuickSearchOptions? options = null);
    
    /// <summary>
    /// Apply quick search/filter to file list
    /// </summary>
    Task<QuickSearchResult> ApplyQuickSearchAsync(
        IEnumerable<string> filePaths,
        string pattern,
        QuickSearchOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a single file matches the pattern
    /// </summary>
    bool Matches(string fileName, string pattern, QuickSearchOptions? options = null);
    
    /// <summary>
    /// Get search history
    /// </summary>
    IReadOnlyList<string> GetSearchHistory(int maxItems = 20);
    
    /// <summary>
    /// Add pattern to search history
    /// </summary>
    void AddToHistory(string pattern);
    
    /// <summary>
    /// Clear search history
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Get saved presets
    /// </summary>
    IReadOnlyList<QuickSearchPreset> GetPresets();
    
    /// <summary>
    /// Save a preset
    /// </summary>
    void SavePreset(QuickSearchPreset preset);
    
    /// <summary>
    /// Delete a preset
    /// </summary>
    bool DeletePreset(string name);
    
    /// <summary>
    /// Apply a preset
    /// </summary>
    QuickSearchResult ApplyPreset(string panelId, string presetName);
    
    /// <summary>
    /// Get quick search suggestions based on partial input
    /// </summary>
    IReadOnlyList<string> GetSuggestions(string partialPattern, int maxSuggestions = 10);
    
    /// <summary>
    /// Event raised when quick search results change
    /// </summary>
    event EventHandler<QuickSearchResult>? SearchResultsChanged;
}
