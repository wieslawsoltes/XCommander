// IMultiRenameToolService.cs - TC-style Multi-Rename Tool
// Comprehensive batch renaming with patterns, regex, and plugins

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Types of rename operations
/// </summary>
public enum RenameOperationType
{
    Replace,         // Find and replace text
    Case,            // Change case
    Counter,         // Add counter/numbering
    DateTime,        // Add date/time
    RegexReplace,    // Regular expression replace
    Extension,       // Change extension
    Insert,          // Insert text at position
    Delete,          // Delete characters at position
    Plugin,          // Content plugin field
    Script           // Custom script
}

/// <summary>
/// Case change options
/// </summary>
public enum CaseChangeType
{
    None,
    Lowercase,
    Uppercase,
    TitleCase,       // First Letter Of Each Word
    SentenceCase,    // First letter of sentence
    InvertCase,      // Toggle case
    RandomCase
}

/// <summary>
/// Counter padding options
/// </summary>
public enum CounterPadding
{
    None,
    Auto,            // Based on number of files
    Custom           // User-defined width
}

/// <summary>
/// Definition of a rename rule
/// </summary>
public record RenameRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public RenameOperationType Type { get; init; }
    public bool Enabled { get; init; } = true;
    public int Order { get; init; }
    
    // Replace options
    public string? SearchText { get; init; }
    public string? ReplaceText { get; init; }
    public bool CaseSensitive { get; init; }
    public bool WholeWord { get; init; }
    public bool UseRegex { get; init; }
    public bool ApplyToExtension { get; init; }
    
    // Case options
    public CaseChangeType CaseType { get; init; }
    public bool CaseApplyToName { get; init; } = true;
    public bool CaseApplyToExtension { get; init; }
    
    // Counter options
    public int CounterStart { get; init; } = 1;
    public int CounterStep { get; init; } = 1;
    public CounterPadding CounterPadding { get; init; } = CounterPadding.Auto;
    public int CounterWidth { get; init; } = 3;
    public string? CounterPrefix { get; init; }
    public string? CounterSuffix { get; init; }
    public int CounterPosition { get; init; } = -1;  // -1 = end, 0+ = position
    
    // DateTime options
    public string? DateTimeFormat { get; init; } = "yyyyMMdd";
    public DateTimeSource DateTimeSource { get; init; } = DateTimeSource.Modified;
    public int DateTimePosition { get; init; } = 0;
    
    // Insert/Delete options
    public int Position { get; init; }           // Position for insert/delete
    public bool FromEnd { get; init; }           // Count from end
    public int Length { get; init; }             // Characters to delete
    public string? InsertText { get; init; }
    
    // Extension options
    public string? NewExtension { get; init; }
    public bool AddExtension { get; init; }      // Add instead of replace
    public bool RemoveExtension { get; init; }
    
    // Plugin options
    public string? PluginId { get; init; }
    public string? PluginField { get; init; }
}

/// <summary>
/// Source for date/time in rename
/// </summary>
public enum DateTimeSource
{
    Modified,
    Created,
    Accessed,
    Exif,            // From image EXIF data
    Current,         // Current date/time
    FileName         // Extract from file name
}

/// <summary>
/// Preview of a rename operation
/// </summary>
public record RenamePreview
{
    public string OriginalPath { get; init; } = string.Empty;
    public string OriginalName { get; init; } = string.Empty;
    public string NewName { get; init; } = string.Empty;
    public string? NewPath { get; init; }
    public bool WillChange { get; init; }
    public bool HasConflict { get; init; }
    public string? ConflictMessage { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a rename operation
/// </summary>
public record RenameResult
{
    public string OriginalPath { get; init; } = string.Empty;
    public string? NewPath { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Multi-rename settings/preset
/// </summary>
public record RenamePreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<RenameRule> Rules { get; init; } = Array.Empty<RenameRule>();
    public string? Mask { get; init; }            // File mask filter
    public bool IncludeSubfolders { get; init; }
    public bool RenameDirectories { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? LastUsedAt { get; init; }
    public bool IsBuiltIn { get; init; }
}

/// <summary>
/// Rename conflict resolution
/// </summary>
public enum RenameConflictResolution
{
    Skip,
    Overwrite,
    AutoNumber,      // Add (1), (2), etc.
    Ask
}

/// <summary>
/// Options for batch rename
/// </summary>
public record RenameOptions
{
    public IReadOnlyList<RenameRule> Rules { get; init; } = Array.Empty<RenameRule>();
    public RenameConflictResolution ConflictResolution { get; init; } = RenameConflictResolution.AutoNumber;
    public bool CreateUndo { get; init; } = true;
    public bool RenameDirectories { get; init; }
    public bool PreserveExtensionCase { get; init; }
    public string? Mask { get; init; }
    public bool SimulateOnly { get; init; }      // Preview without renaming
}

/// <summary>
/// Undo information for rename operation
/// </summary>
public record RenameUndoInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public IReadOnlyList<(string NewPath, string OriginalPath)> Renames { get; init; } = Array.Empty<(string, string)>();
    public string? Description { get; init; }
}

/// <summary>
/// Service for TC-style Multi-Rename Tool functionality
/// </summary>
public interface IMultiRenameToolService
{
    // ======= Preview Operations =======
    
    /// <summary>
    /// Generate preview of rename operations
    /// </summary>
    Task<IReadOnlyList<RenamePreview>> PreviewRenameAsync(
        IEnumerable<string> files,
        RenameOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Apply single rule and preview
    /// </summary>
    Task<RenamePreview> PreviewSingleRenameAsync(
        string filePath,
        RenameRule rule,
        int counterValue = 0,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate rename rules
    /// </summary>
    Task<IReadOnlyList<(string RuleId, string Error)>> ValidateRulesAsync(
        IEnumerable<RenameRule> rules,
        CancellationToken cancellationToken = default);
    
    // ======= Rename Operations =======
    
    /// <summary>
    /// Execute batch rename
    /// </summary>
    Task<IReadOnlyList<RenameResult>> RenameAsync(
        IEnumerable<string> files,
        RenameOptions options,
        IProgress<(int Current, int Total, string CurrentFile)>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rename single file
    /// </summary>
    Task<RenameResult> RenameSingleAsync(
        string filePath,
        string newName,
        RenameConflictResolution conflictResolution = RenameConflictResolution.AutoNumber,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Undo last rename operation
    /// </summary>
    Task<IReadOnlyList<RenameResult>> UndoAsync(
        string? undoId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get undo history
    /// </summary>
    IReadOnlyList<RenameUndoInfo> GetUndoHistory();
    
    /// <summary>
    /// Clear undo history
    /// </summary>
    void ClearUndoHistory();
    
    // ======= Preset Management =======
    
    /// <summary>
    /// Get all presets
    /// </summary>
    IReadOnlyList<RenamePreset> GetPresets();
    
    /// <summary>
    /// Get preset by ID
    /// </summary>
    RenamePreset? GetPreset(string presetId);
    
    /// <summary>
    /// Save preset
    /// </summary>
    Task<RenamePreset> SavePresetAsync(RenamePreset preset, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete preset
    /// </summary>
    Task<bool> DeletePresetAsync(string presetId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import presets from file
    /// </summary>
    Task<IReadOnlyList<RenamePreset>> ImportPresetsAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export presets to file
    /// </summary>
    Task ExportPresetsAsync(IEnumerable<string> presetIds, string filePath, CancellationToken cancellationToken = default);
    
    // ======= Rule Helpers =======
    
    /// <summary>
    /// Create a find/replace rule
    /// </summary>
    RenameRule CreateReplaceRule(string search, string replace, bool caseSensitive = false, bool useRegex = false);
    
    /// <summary>
    /// Create a case change rule
    /// </summary>
    RenameRule CreateCaseRule(CaseChangeType caseType, bool applyToExtension = false);
    
    /// <summary>
    /// Create a counter rule
    /// </summary>
    RenameRule CreateCounterRule(int start = 1, int step = 1, int width = 3, string? prefix = null, string? suffix = null);
    
    /// <summary>
    /// Create a date/time rule
    /// </summary>
    RenameRule CreateDateTimeRule(string format, DateTimeSource source = DateTimeSource.Modified, int position = 0);
    
    /// <summary>
    /// Create an insert rule
    /// </summary>
    RenameRule CreateInsertRule(string text, int position, bool fromEnd = false);
    
    /// <summary>
    /// Create a delete rule
    /// </summary>
    RenameRule CreateDeleteRule(int position, int length, bool fromEnd = false);
    
    /// <summary>
    /// Create an extension change rule
    /// </summary>
    RenameRule CreateExtensionRule(string newExtension, bool add = false);
    
    // ======= Pattern Helpers =======
    
    /// <summary>
    /// Apply TC-style pattern to filename
    /// Pattern: [N] = name, [E] = extension, [C] = counter, [Y/M/D/h/m/s] = date parts
    /// </summary>
    Task<string> ApplyPatternAsync(string pattern, string filePath, int counter = 0, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available pattern placeholders
    /// </summary>
    IReadOnlyList<PatternPlaceholder> GetPatternPlaceholders();
    
    /// <summary>
    /// Validate a pattern string
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidatePattern(string pattern);
    
    // ======= Events =======
    
    /// <summary>
    /// Event raised when rename operation completes
    /// </summary>
    event EventHandler<RenameCompletedEventArgs>? RenameCompleted;
    
    /// <summary>
    /// Event raised when presets change
    /// </summary>
    event EventHandler<EventArgs>? PresetsChanged;
}

/// <summary>
/// Pattern placeholder information
/// </summary>
public record PatternPlaceholder
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
    public bool AcceptsParameters { get; init; }
}

/// <summary>
/// Rename completed event arguments
/// </summary>
public class RenameCompletedEventArgs : EventArgs
{
    public IReadOnlyList<RenameResult> Results { get; }
    public int SuccessCount { get; }
    public int FailureCount { get; }
    public RenameUndoInfo? UndoInfo { get; }
    
    public RenameCompletedEventArgs(IReadOnlyList<RenameResult> results, RenameUndoInfo? undoInfo)
    {
        Results = results;
        SuccessCount = results.Count(r => r.Success);
        FailureCount = results.Count(r => !r.Success);
        UndoInfo = undoInfo;
    }
}
