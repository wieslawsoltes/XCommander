using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of IQuickSearchService for TC-style instant file filtering.
/// </summary>
public class QuickSearchService : IQuickSearchService
{
    private readonly ConcurrentDictionary<string, QuickSearchState> _activeSearches = new();
    private readonly List<string> _searchHistory = new();
    private readonly List<QuickSearchPreset> _presets = new();
    private readonly object _historyLock = new();
    private readonly object _presetsLock = new();
    private const int MaxHistorySize = 100;

    public event EventHandler<QuickSearchResult>? SearchResultsChanged;

    private class QuickSearchState
    {
        public string Pattern { get; set; } = string.Empty;
        public QuickSearchOptions Options { get; set; } = new();
        public bool IsActive { get; set; }
        public IReadOnlyList<string>? CurrentFiles { get; set; }
    }

    public void BeginQuickSearch(string panelId)
    {
        _activeSearches.AddOrUpdate(
            panelId,
            _ => new QuickSearchState { IsActive = true },
            (_, state) =>
            {
                state.IsActive = true;
                state.Pattern = string.Empty;
                return state;
            });
    }

    public void EndQuickSearch(string panelId)
    {
        if (_activeSearches.TryGetValue(panelId, out var state))
        {
            state.IsActive = false;
            
            // Save non-empty patterns to history
            if (!string.IsNullOrWhiteSpace(state.Pattern))
            {
                AddToHistory(state.Pattern);
            }
        }
    }

    public bool IsQuickSearchActive(string panelId)
    {
        return _activeSearches.TryGetValue(panelId, out var state) && state.IsActive;
    }

    public string GetCurrentPattern(string panelId)
    {
        return _activeSearches.TryGetValue(panelId, out var state) ? state.Pattern : string.Empty;
    }

    public QuickSearchResult UpdatePattern(string panelId, string pattern, QuickSearchOptions? options = null)
    {
        var state = _activeSearches.GetOrAdd(panelId, _ => new QuickSearchState());
        state.Pattern = pattern;
        if (options != null)
        {
            state.Options = options;
        }

        // If we have cached files, apply filter immediately
        if (state.CurrentFiles != null)
        {
            var result = ApplyQuickSearchAsync(state.CurrentFiles, pattern, state.Options).GetAwaiter().GetResult();
            SearchResultsChanged?.Invoke(this, result);
            return result;
        }

        return new QuickSearchResult
        {
            Pattern = pattern,
            OriginalCount = 0,
            MatchedCount = 0
        };
    }

    public async Task<QuickSearchResult> ApplyQuickSearchAsync(
        IEnumerable<string> filePaths,
        string pattern,
        QuickSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        options ??= new QuickSearchOptions();
        
        var files = filePaths.ToList();
        var originalCount = files.Count;

        if (string.IsNullOrEmpty(pattern))
        {
            return new QuickSearchResult
            {
                OriginalCount = originalCount,
                MatchedCount = originalCount,
                MatchedPaths = files,
                Pattern = pattern,
                SearchDuration = sw.Elapsed
            };
        }

        var matched = new List<string>();
        var firstMatchIndex = -1;

        await Task.Run(() =>
        {
            for (int i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var file = files[i];
                var fileName = Path.GetFileName(file);
                
                if (!options.IncludeHidden && IsHidden(file))
                    continue;
                    
                if (!options.IncludeDirectories && Directory.Exists(file))
                    continue;

                if (Matches(fileName, pattern, options))
                {
                    matched.Add(file);
                    if (firstMatchIndex < 0)
                    {
                        firstMatchIndex = i;
                    }
                }
            }
        }, cancellationToken);

        sw.Stop();

        return new QuickSearchResult
        {
            OriginalCount = originalCount,
            MatchedCount = matched.Count,
            MatchedPaths = matched,
            FirstMatchIndex = firstMatchIndex,
            Pattern = pattern,
            SearchDuration = sw.Elapsed
        };
    }

    public bool Matches(string fileName, string pattern, QuickSearchOptions? options = null)
    {
        options ??= new QuickSearchOptions();
        
        if (string.IsNullOrEmpty(pattern))
            return true;

        var comparison = options.CaseSensitive 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;

        string nameToMatch = fileName;
        string extensionToMatch = string.Empty;
        
        if (options.MatchExtensionSeparately)
        {
            nameToMatch = Path.GetFileNameWithoutExtension(fileName);
            extensionToMatch = Path.GetExtension(fileName);
        }

        return options.MatchType switch
        {
            QuickSearchMatchType.Contains => ContainsMatch(nameToMatch, pattern, comparison),
            QuickSearchMatchType.StartsWith => nameToMatch.StartsWith(pattern, comparison),
            QuickSearchMatchType.Wildcard => WildcardMatch(nameToMatch, pattern, options.CaseSensitive),
            QuickSearchMatchType.Regex => RegexMatch(nameToMatch, pattern, options.CaseSensitive),
            QuickSearchMatchType.Initials => InitialsMatch(nameToMatch, pattern, comparison),
            _ => ContainsMatch(nameToMatch, pattern, comparison)
        };
    }

    private static bool ContainsMatch(string text, string pattern, StringComparison comparison)
    {
        return text.Contains(pattern, comparison);
    }

    private static bool WildcardMatch(string text, string pattern, bool caseSensitive)
    {
        // Convert wildcard to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        
        try
        {
            return Regex.IsMatch(text, regexPattern, options, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }

    private static bool RegexMatch(string text, string pattern, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(text, pattern, options, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }

    private static bool InitialsMatch(string text, string pattern, StringComparison comparison)
    {
        // Match by initials: "fp" matches "FilePanel.cs", "FTPClientService.cs"
        // Extract uppercase letters and word starts
        var initials = new List<char>();
        bool wasLower = false;
        
        foreach (var c in text)
        {
            if (char.IsUpper(c) || (char.IsLetter(c) && initials.Count == 0))
            {
                initials.Add(char.ToLowerInvariant(c));
            }
            else if (c == '_' || c == '-' || c == '.')
            {
                wasLower = true;
                continue;
            }
            else if (wasLower && char.IsLetter(c))
            {
                initials.Add(char.ToLowerInvariant(c));
            }
            wasLower = char.IsLower(c);
        }

        var initialsStr = new string(initials.ToArray());
        var lowerPattern = pattern.ToLowerInvariant();
        
        return initialsStr.StartsWith(lowerPattern);
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetSearchHistory(int maxItems = 20)
    {
        lock (_historyLock)
        {
            return _searchHistory.Take(maxItems).ToList();
        }
    }

    public void AddToHistory(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return;

        lock (_historyLock)
        {
            // Remove if exists to move to front
            _searchHistory.Remove(pattern);
            _searchHistory.Insert(0, pattern);
            
            // Trim to max size
            while (_searchHistory.Count > MaxHistorySize)
            {
                _searchHistory.RemoveAt(_searchHistory.Count - 1);
            }
        }
    }

    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _searchHistory.Clear();
        }
    }

    public IReadOnlyList<QuickSearchPreset> GetPresets()
    {
        lock (_presetsLock)
        {
            return _presets.ToList();
        }
    }

    public void SavePreset(QuickSearchPreset preset)
    {
        lock (_presetsLock)
        {
            var existingIndex = _presets.FindIndex(p => p.Name == preset.Name);
            if (existingIndex >= 0)
            {
                _presets[existingIndex] = preset;
            }
            else
            {
                _presets.Add(preset);
            }
        }
    }

    public bool DeletePreset(string name)
    {
        lock (_presetsLock)
        {
            return _presets.RemoveAll(p => p.Name == name) > 0;
        }
    }

    public QuickSearchResult ApplyPreset(string panelId, string presetName)
    {
        QuickSearchPreset? preset;
        lock (_presetsLock)
        {
            preset = _presets.FirstOrDefault(p => p.Name == presetName);
        }

        if (preset == null)
        {
            return new QuickSearchResult { Pattern = string.Empty };
        }

        // Update usage count
        lock (_presetsLock)
        {
            var index = _presets.FindIndex(p => p.Name == presetName);
            if (index >= 0)
            {
                _presets[index] = preset with { UsageCount = preset.UsageCount + 1 };
            }
        }

        return UpdatePattern(panelId, preset.Pattern, preset.Options);
    }

    public IReadOnlyList<string> GetSuggestions(string partialPattern, int maxSuggestions = 10)
    {
        if (string.IsNullOrWhiteSpace(partialPattern))
        {
            return GetSearchHistory(maxSuggestions);
        }

        var suggestions = new List<string>();
        
        // Add matching history items
        lock (_historyLock)
        {
            suggestions.AddRange(_searchHistory
                .Where(h => h.StartsWith(partialPattern, StringComparison.OrdinalIgnoreCase))
                .Take(maxSuggestions));
        }

        // Add matching preset names
        lock (_presetsLock)
        {
            suggestions.AddRange(_presets
                .Where(p => p.Pattern.StartsWith(partialPattern, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.UsageCount)
                .Select(p => p.Pattern)
                .Take(maxSuggestions - suggestions.Count));
        }

        return suggestions.Distinct().Take(maxSuggestions).ToList();
    }
}
