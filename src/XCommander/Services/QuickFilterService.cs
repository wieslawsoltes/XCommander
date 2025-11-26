// IQuickFilterService.cs - Quick Filter Service
// Provides enhanced Ctrl+S quick filter functionality

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Service for quick filtering in file lists.
/// </summary>
public interface IQuickFilterService
{
    /// <summary>
    /// Parses a filter expression into a filter object.
    /// </summary>
    QuickFilter ParseFilter(string filterExpression);
    
    /// <summary>
    /// Tests if a filename matches the filter.
    /// </summary>
    bool Matches(QuickFilter filter, string fileName, FileAttributes attributes, long size, DateTime modified);
    
    /// <summary>
    /// Gets the filter history.
    /// </summary>
    IReadOnlyList<string> FilterHistory { get; }
    
    /// <summary>
    /// Adds a filter to history.
    /// </summary>
    void AddToHistory(string filterExpression);
    
    /// <summary>
    /// Gets saved filter presets.
    /// </summary>
    IReadOnlyList<FilterPreset> GetPresets();
    
    /// <summary>
    /// Saves a filter preset.
    /// </summary>
    void SavePreset(FilterPreset preset);
    
    /// <summary>
    /// Deletes a filter preset.
    /// </summary>
    void DeletePreset(string name);
}

/// <summary>
/// A quick filter for file lists.
/// </summary>
public record QuickFilter
{
    /// <summary>
    /// Original filter expression.
    /// </summary>
    public string Expression { get; init; } = "";
    
    /// <summary>
    /// File name patterns to match (e.g., *.txt, *.doc).
    /// </summary>
    public List<string> NamePatterns { get; init; } = new();
    
    /// <summary>
    /// Whether to use regex for name matching.
    /// </summary>
    public bool UseRegex { get; init; }
    
    /// <summary>
    /// Compiled regex for name matching.
    /// </summary>
    public Regex? NameRegex { get; init; }
    
    /// <summary>
    /// Minimum file size filter.
    /// </summary>
    public long? MinSize { get; init; }
    
    /// <summary>
    /// Maximum file size filter.
    /// </summary>
    public long? MaxSize { get; init; }
    
    /// <summary>
    /// Minimum modified date filter.
    /// </summary>
    public DateTime? MinDate { get; init; }
    
    /// <summary>
    /// Maximum modified date filter.
    /// </summary>
    public DateTime? MaxDate { get; init; }
    
    /// <summary>
    /// Required attributes.
    /// </summary>
    public FileAttributes? RequiredAttributes { get; init; }
    
    /// <summary>
    /// Excluded attributes.
    /// </summary>
    public FileAttributes? ExcludedAttributes { get; init; }
    
    /// <summary>
    /// Whether the filter is valid.
    /// </summary>
    public bool IsValid { get; init; } = true;
    
    /// <summary>
    /// Error message if not valid.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// A saved filter preset.
/// </summary>
public record FilterPreset
{
    public required string Name { get; init; }
    public required string Expression { get; init; }
    public string? Description { get; init; }
    public string? Shortcut { get; init; }
}

/// <summary>
/// Implementation of quick filter service.
/// </summary>
public class QuickFilterService : IQuickFilterService
{
    private readonly List<string> _history = new();
    private readonly List<FilterPreset> _presets = new();
    private const int MaxHistorySize = 50;
    
    public IReadOnlyList<string> FilterHistory => _history;
    
    public QuickFilterService()
    {
        // Add default presets
        _presets.AddRange(new[]
        {
            new FilterPreset { Name = "Text Files", Expression = "*.txt;*.md;*.log", Description = "Text and log files" },
            new FilterPreset { Name = "Images", Expression = "*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp", Description = "Image files" },
            new FilterPreset { Name = "Documents", Expression = "*.doc;*.docx;*.pdf;*.xls;*.xlsx;*.ppt;*.pptx", Description = "Office documents" },
            new FilterPreset { Name = "Code Files", Expression = "*.cs;*.js;*.ts;*.py;*.java;*.cpp;*.h", Description = "Source code files" },
            new FilterPreset { Name = "Archives", Expression = "*.zip;*.rar;*.7z;*.tar;*.gz", Description = "Archive files" },
            new FilterPreset { Name = "Large Files", Expression = ">100MB", Description = "Files larger than 100MB" },
            new FilterPreset { Name = "Recent", Expression = "<7d", Description = "Modified in last 7 days" },
            new FilterPreset { Name = "Hidden", Expression = "+h", Description = "Hidden files only" }
        });
    }
    
    public QuickFilter ParseFilter(string filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            return new QuickFilter { Expression = "" };
        }
        
        var filter = new QuickFilter { Expression = filterExpression };
        var patterns = new List<string>();
        long? minSize = null, maxSize = null;
        DateTime? minDate = null, maxDate = null;
        FileAttributes? required = null, excluded = null;
        Regex? regex = null;
        bool useRegex = false;
        
        try
        {
            // Split by semicolon for multiple patterns
            var parts = filterExpression.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var part in parts)
            {
                // Check for regex (starts with /)
                if (part.StartsWith('/') && part.EndsWith('/'))
                {
                    useRegex = true;
                    regex = new Regex(part[1..^1], RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    continue;
                }
                
                // Check for size filters
                if (part.StartsWith('>') || part.StartsWith('<'))
                {
                    var (size, isMin) = ParseSizeFilter(part);
                    if (isMin) minSize = size;
                    else maxSize = size;
                    continue;
                }
                
                // Check for date filters
                if (part.StartsWith("date>") || part.StartsWith("date<"))
                {
                    var (date, isMin) = ParseDateFilter(part);
                    if (isMin) minDate = date;
                    else maxDate = date;
                    continue;
                }
                
                // Check for relative date filters
                if (part.StartsWith("<") && part.EndsWith("d"))
                {
                    if (int.TryParse(part[1..^1], out var days))
                    {
                        minDate = DateTime.Now.AddDays(-days);
                    }
                    continue;
                }
                
                // Check for attribute filters
                if (part.StartsWith('+') || part.StartsWith('-'))
                {
                    var (attr, isRequired) = ParseAttributeFilter(part);
                    if (isRequired)
                        required = (required ?? 0) | attr;
                    else
                        excluded = (excluded ?? 0) | attr;
                    continue;
                }
                
                // Otherwise it's a name pattern
                patterns.Add(part);
            }
            
            return filter with
            {
                NamePatterns = patterns,
                UseRegex = useRegex,
                NameRegex = regex,
                MinSize = minSize,
                MaxSize = maxSize,
                MinDate = minDate,
                MaxDate = maxDate,
                RequiredAttributes = required,
                ExcludedAttributes = excluded
            };
        }
        catch (Exception ex)
        {
            return new QuickFilter
            {
                Expression = filterExpression,
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public bool Matches(QuickFilter filter, string fileName, FileAttributes attributes, long size, DateTime modified)
    {
        if (!filter.IsValid || string.IsNullOrEmpty(filter.Expression))
            return true;
        
        // Check name pattern
        if (filter.NamePatterns.Count > 0)
        {
            bool nameMatches = false;
            foreach (var pattern in filter.NamePatterns)
            {
                if (MatchesWildcard(fileName, pattern))
                {
                    nameMatches = true;
                    break;
                }
            }
            if (!nameMatches) return false;
        }
        
        // Check regex
        if (filter.UseRegex && filter.NameRegex != null)
        {
            if (!filter.NameRegex.IsMatch(fileName))
                return false;
        }
        
        // Check size
        if (filter.MinSize.HasValue && size < filter.MinSize.Value)
            return false;
        if (filter.MaxSize.HasValue && size > filter.MaxSize.Value)
            return false;
        
        // Check date
        if (filter.MinDate.HasValue && modified < filter.MinDate.Value)
            return false;
        if (filter.MaxDate.HasValue && modified > filter.MaxDate.Value)
            return false;
        
        // Check required attributes
        if (filter.RequiredAttributes.HasValue && (attributes & filter.RequiredAttributes.Value) != filter.RequiredAttributes.Value)
            return false;
        
        // Check excluded attributes
        if (filter.ExcludedAttributes.HasValue && (attributes & filter.ExcludedAttributes.Value) != 0)
            return false;
        
        return true;
    }
    
    public void AddToHistory(string filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
            return;
        
        // Remove if already exists
        _history.Remove(filterExpression);
        
        // Add to front
        _history.Insert(0, filterExpression);
        
        // Trim
        while (_history.Count > MaxHistorySize)
        {
            _history.RemoveAt(_history.Count - 1);
        }
    }
    
    public IReadOnlyList<FilterPreset> GetPresets() => _presets;
    
    public void SavePreset(FilterPreset preset)
    {
        var existing = _presets.FindIndex(p => p.Name == preset.Name);
        if (existing >= 0)
            _presets[existing] = preset;
        else
            _presets.Add(preset);
    }
    
    public void DeletePreset(string name)
    {
        _presets.RemoveAll(p => p.Name == name);
    }
    
    private static bool MatchesWildcard(string text, string pattern)
    {
        // Convert wildcard to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";
        
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }
    
    private static (long size, bool isMin) ParseSizeFilter(string filter)
    {
        var isMin = filter[0] == '>';
        var sizeStr = filter[1..].Trim();
        long multiplier = 1;
        
        if (sizeStr.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024;
            sizeStr = sizeStr[..^2];
        }
        else if (sizeStr.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024 * 1024;
            sizeStr = sizeStr[..^2];
        }
        else if (sizeStr.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024 * 1024;
            sizeStr = sizeStr[..^2];
        }
        
        var size = long.Parse(sizeStr.Trim()) * multiplier;
        return (size, isMin);
    }
    
    private static (DateTime date, bool isMin) ParseDateFilter(string filter)
    {
        var isMin = filter.Contains('>');
        var dateStr = filter.Replace("date>", "").Replace("date<", "").Trim();
        var date = DateTime.Parse(dateStr);
        return (date, isMin);
    }
    
    private static (FileAttributes attr, bool isRequired) ParseAttributeFilter(string filter)
    {
        var isRequired = filter[0] == '+';
        var attrChar = filter[1..].ToLowerInvariant();
        
        FileAttributes attr = attrChar switch
        {
            "r" => FileAttributes.ReadOnly,
            "h" => FileAttributes.Hidden,
            "s" => FileAttributes.System,
            "a" => FileAttributes.Archive,
            "d" => FileAttributes.Directory,
            "c" => FileAttributes.Compressed,
            "e" => FileAttributes.Encrypted,
            _ => (FileAttributes)0
        };
        
        return (attr, isRequired);
    }
}
