// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Custom view modes service implementation.
/// </summary>
public class CustomViewModesService : ICustomViewModesService
{
    private readonly List<ViewMode> _viewModes;
    private readonly List<ViewModeAutoSelectRule> _autoSelectRules;
    private readonly string _configPath;
    private readonly string _rulesPath;
    private readonly object _lock = new();
    private ViewMode? _activeViewMode;
    private bool _loaded;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public IReadOnlyList<ViewMode> ViewModes
    {
        get
        {
            lock (_lock)
            {
                return _viewModes.ToList().AsReadOnly();
            }
        }
    }
    
    public ViewMode? ActiveViewMode
    {
        get
        {
            lock (_lock)
            {
                return _activeViewMode;
            }
        }
    }
    
    public IReadOnlyList<ViewModeAutoSelectRule> AutoSelectRules
    {
        get
        {
            lock (_lock)
            {
                return _autoSelectRules.ToList().AsReadOnly();
            }
        }
    }
    
    public event EventHandler<ViewModeChangedEventArgs>? ViewModeChanged;
    
    public CustomViewModesService()
    {
        _viewModes = new List<ViewMode>();
        _autoSelectRules = new List<ViewModeAutoSelectRule>();
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "view-modes.json");
        _rulesPath = Path.Combine(appData, "XCommander", "view-mode-rules.json");
    }
    
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded) return;
        
        // Load view modes
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                var viewModes = JsonSerializer.Deserialize<List<ViewMode>>(json, JsonOptions);
                if (viewModes != null)
                {
                    lock (_lock)
                    {
                        _viewModes.Clear();
                        _viewModes.AddRange(viewModes);
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }
        
        // Load auto-select rules
        if (File.Exists(_rulesPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_rulesPath, cancellationToken);
                var rules = JsonSerializer.Deserialize<List<ViewModeAutoSelectRule>>(json, JsonOptions);
                if (rules != null)
                {
                    lock (_lock)
                    {
                        _autoSelectRules.Clear();
                        _autoSelectRules.AddRange(rules);
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }
        
        // Initialize with defaults if empty
        lock (_lock)
        {
            if (_viewModes.Count == 0)
            {
                _viewModes.AddRange(CreateDefaultViewModes());
            }
            
            // Set default active view mode
            _activeViewMode = _viewModes.FirstOrDefault(v => v.IsDefault) 
                              ?? _viewModes.FirstOrDefault();
        }
        
        _loaded = true;
    }
    
    private static List<ViewMode> CreateDefaultViewModes()
    {
        return new List<ViewMode>
        {
            new ViewMode
            {
                Name = "Details",
                Description = "Full details with all columns",
                IsDefault = true,
                IsBuiltIn = true,
                Style = ViewStyle.Details,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition { Name = "Name", Field = "Name", Type = ColumnType.Text, Width = 250, Order = 0 },
                    new ColumnDefinition { Name = "Size", Field = "Size", Type = ColumnType.Size, Width = 100, Order = 1, Alignment = TextAlignment.Right },
                    new ColumnDefinition { Name = "Type", Field = "Extension", Type = ColumnType.Text, Width = 100, Order = 2 },
                    new ColumnDefinition { Name = "Date Modified", Field = "ModifiedAt", Type = ColumnType.DateTime, Width = 150, Order = 3, Format = "yyyy-MM-dd HH:mm" },
                    new ColumnDefinition { Name = "Attributes", Field = "Attributes", Type = ColumnType.Text, Width = 80, Order = 4, IsVisible = false }
                },
                DefaultSort = new SortDefinition { Field = "Name", Direction = SortDirection.Ascending, FoldersFirst = true },
                KeyboardShortcut = "Ctrl+1"
            },
            new ViewMode
            {
                Name = "Compact",
                Description = "Compact list with minimal columns",
                IsBuiltIn = true,
                Style = ViewStyle.Details,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition { Name = "Name", Field = "Name", Type = ColumnType.Text, Width = 300, Order = 0 },
                    new ColumnDefinition { Name = "Size", Field = "Size", Type = ColumnType.Size, Width = 80, Order = 1, Alignment = TextAlignment.Right }
                },
                DefaultSort = new SortDefinition { Field = "Name", Direction = SortDirection.Ascending, FoldersFirst = true },
                KeyboardShortcut = "Ctrl+2"
            },
            new ViewMode
            {
                Name = "Large Icons",
                Description = "Large icon view",
                IsBuiltIn = true,
                Style = ViewStyle.LargeIcons,
                Thumbnails = new ThumbnailSettings { Size = 96, Quality = ThumbnailQuality.High, ShowForImages = true },
                KeyboardShortcut = "Ctrl+3"
            },
            new ViewMode
            {
                Name = "Small Icons",
                Description = "Small icon view",
                IsBuiltIn = true,
                Style = ViewStyle.SmallIcons,
                Thumbnails = new ThumbnailSettings { Size = 32, Quality = ThumbnailQuality.Medium },
                KeyboardShortcut = "Ctrl+4"
            },
            new ViewMode
            {
                Name = "Thumbnails",
                Description = "Thumbnail view for images and media",
                IsBuiltIn = true,
                Style = ViewStyle.Thumbnails,
                Thumbnails = new ThumbnailSettings 
                { 
                    Size = 160, 
                    Quality = ThumbnailQuality.High,
                    ShowForImages = true,
                    ShowForVideos = true,
                    ShowForPDFs = true
                },
                KeyboardShortcut = "Ctrl+5"
            },
            new ViewMode
            {
                Name = "List",
                Description = "Simple list view",
                IsBuiltIn = true,
                Style = ViewStyle.List,
                KeyboardShortcut = "Ctrl+6"
            },
            new ViewMode
            {
                Name = "Brief",
                Description = "Multiple columns list",
                IsBuiltIn = true,
                Style = ViewStyle.List,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition { Name = "Name", Field = "Name", Type = ColumnType.Text, Width = 200, Order = 0 }
                },
                KeyboardShortcut = "Ctrl+7"
            },
            new ViewMode
            {
                Name = "Full",
                Description = "Full details including all metadata",
                IsBuiltIn = true,
                Style = ViewStyle.Details,
                Columns = new List<ColumnDefinition>
                {
                    new ColumnDefinition { Name = "Name", Field = "Name", Type = ColumnType.Text, Width = 250, Order = 0 },
                    new ColumnDefinition { Name = "Size", Field = "Size", Type = ColumnType.Size, Width = 100, Order = 1, Alignment = TextAlignment.Right },
                    new ColumnDefinition { Name = "Type", Field = "Extension", Type = ColumnType.Text, Width = 100, Order = 2 },
                    new ColumnDefinition { Name = "Date Modified", Field = "ModifiedAt", Type = ColumnType.DateTime, Width = 150, Order = 3, Format = "yyyy-MM-dd HH:mm" },
                    new ColumnDefinition { Name = "Date Created", Field = "CreatedAt", Type = ColumnType.DateTime, Width = 150, Order = 4, Format = "yyyy-MM-dd HH:mm" },
                    new ColumnDefinition { Name = "Attributes", Field = "Attributes", Type = ColumnType.Text, Width = 80, Order = 5 },
                    new ColumnDefinition { Name = "Owner", Field = "Owner", Type = ColumnType.Text, Width = 120, Order = 6 }
                },
                DefaultSort = new SortDefinition { Field = "Name", Direction = SortDirection.Ascending, FoldersFirst = true },
                ShowHiddenFiles = true,
                KeyboardShortcut = "Ctrl+8"
            }
        };
    }
    
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            List<ViewMode> viewModesToSave;
            lock (_lock)
            {
                viewModesToSave = _viewModes.ToList();
            }
            
            var json = JsonSerializer.Serialize(viewModesToSave, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    private async Task SaveRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_rulesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            List<ViewModeAutoSelectRule> rulesToSave;
            lock (_lock)
            {
                rulesToSave = _autoSelectRules.ToList();
            }
            
            var json = JsonSerializer.Serialize(rulesToSave, JsonOptions);
            await File.WriteAllTextAsync(_rulesPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    public async Task<ViewMode> CreateAsync(ViewModeOptions options, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var viewMode = new ViewMode
        {
            Name = options.Name,
            Description = options.Description,
            Style = options.Style,
            Columns = new List<ColumnDefinition>(options.Columns),
            DefaultSort = options.DefaultSort,
            Grouping = options.Grouping,
            DefaultFilter = options.DefaultFilter,
            ShowHiddenFiles = options.ShowHiddenFiles,
            ShowSystemFiles = options.ShowSystemFiles,
            Thumbnails = options.Thumbnails,
            IconPath = options.IconPath,
            IsBuiltIn = false
        };
        
        lock (_lock)
        {
            _viewModes.Add(viewMode);
        }
        
        await SaveAsync(cancellationToken);
        return viewMode;
    }
    
    public async Task UpdateAsync(ViewMode viewMode, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var existing = _viewModes.FirstOrDefault(v => v.Id == viewMode.Id);
            if (existing != null)
            {
                var index = _viewModes.IndexOf(existing);
                viewMode.ModifiedAt = DateTime.Now;
                _viewModes[index] = viewMode;
            }
        }
        
        await SaveAsync(cancellationToken);
    }
    
    public async Task DeleteAsync(string viewModeId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var viewMode = _viewModes.FirstOrDefault(v => v.Id == viewModeId);
            if (viewMode != null && !viewMode.IsBuiltIn)
            {
                _viewModes.Remove(viewMode);
                
                // Remove associated auto-select rules
                _autoSelectRules.RemoveAll(r => r.ViewModeId == viewModeId);
                
                // Reset active view mode if deleted
                if (_activeViewMode?.Id == viewModeId)
                {
                    _activeViewMode = _viewModes.FirstOrDefault(v => v.IsDefault) 
                                      ?? _viewModes.FirstOrDefault();
                }
            }
        }
        
        await SaveAsync(cancellationToken);
        await SaveRulesAsync(cancellationToken);
    }
    
    public async Task<ViewMode?> GetAsync(string viewModeId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            return _viewModes.FirstOrDefault(v => v.Id == viewModeId);
        }
    }
    
    public async Task ActivateAsync(string viewModeId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        ViewMode? previousMode;
        ViewMode? newMode;
        
        lock (_lock)
        {
            previousMode = _activeViewMode;
            newMode = _viewModes.FirstOrDefault(v => v.Id == viewModeId);
            
            if (newMode != null)
            {
                _activeViewMode = newMode;
            }
        }
        
        if (newMode != null && newMode.Id != previousMode?.Id)
        {
            ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs
            {
                PreviousViewMode = previousMode,
                NewViewMode = newMode,
                Reason = ViewModeChangeReason.UserSelection
            });
        }
    }
    
    public async Task<ViewMode> DuplicateAsync(string viewModeId, string newName, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        ViewMode? source;
        lock (_lock)
        {
            source = _viewModes.FirstOrDefault(v => v.Id == viewModeId);
        }
        
        if (source == null)
        {
            throw new InvalidOperationException("View mode not found");
        }
        
        var duplicate = new ViewMode
        {
            Name = newName,
            Description = source.Description,
            Style = source.Style,
            Columns = source.Columns.Select(c => new ColumnDefinition
            {
                Name = c.Name,
                Field = c.Field,
                Type = c.Type,
                Width = c.Width,
                MinWidth = c.MinWidth,
                MaxWidth = c.MaxWidth,
                IsVisible = c.IsVisible,
                CanResize = c.CanResize,
                CanReorder = c.CanReorder,
                CanSort = c.CanSort,
                Order = c.Order,
                Alignment = c.Alignment,
                Format = c.Format,
                CustomRenderer = c.CustomRenderer,
                ShowInTooltip = c.ShowInTooltip
            }).ToList(),
            DefaultSort = source.DefaultSort,
            Grouping = source.Grouping,
            DefaultFilter = source.DefaultFilter,
            ShowHiddenFiles = source.ShowHiddenFiles,
            ShowSystemFiles = source.ShowSystemFiles,
            Thumbnails = source.Thumbnails,
            IconPath = source.IconPath,
            IsBuiltIn = false,
            IsDefault = false
        };
        
        lock (_lock)
        {
            _viewModes.Add(duplicate);
        }
        
        await SaveAsync(cancellationToken);
        return duplicate;
    }
    
    public async Task<ViewMode> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var imported = JsonSerializer.Deserialize<ViewMode>(json, JsonOptions)
                       ?? throw new InvalidDataException("Invalid view mode file");
        
        // Create new view mode with new ID
        var viewMode = new ViewMode
        {
            Name = imported.Name,
            Description = imported.Description,
            Style = imported.Style,
            Columns = imported.Columns,
            DefaultSort = imported.DefaultSort,
            Grouping = imported.Grouping,
            DefaultFilter = imported.DefaultFilter,
            ShowHiddenFiles = imported.ShowHiddenFiles,
            ShowSystemFiles = imported.ShowSystemFiles,
            Thumbnails = imported.Thumbnails,
            IconPath = imported.IconPath,
            KeyboardShortcut = imported.KeyboardShortcut,
            IsBuiltIn = false,
            IsDefault = false
        };
        
        lock (_lock)
        {
            _viewModes.Add(viewMode);
        }
        
        await SaveAsync(cancellationToken);
        
        ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs
        {
            NewViewMode = viewMode,
            Reason = ViewModeChangeReason.Import
        });
        
        return viewMode;
    }
    
    public async Task ExportAsync(string viewModeId, string filePath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        ViewMode? viewMode;
        lock (_lock)
        {
            viewMode = _viewModes.FirstOrDefault(v => v.Id == viewModeId);
        }
        
        if (viewMode == null)
        {
            throw new InvalidOperationException("View mode not found");
        }
        
        var json = JsonSerializer.Serialize(viewMode, JsonOptions);
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var previousMode = _activeViewMode;
        
        lock (_lock)
        {
            _viewModes.Clear();
            _viewModes.AddRange(CreateDefaultViewModes());
            _autoSelectRules.Clear();
            
            _activeViewMode = _viewModes.FirstOrDefault(v => v.IsDefault) 
                              ?? _viewModes.FirstOrDefault();
        }
        
        await SaveAsync(cancellationToken);
        await SaveRulesAsync(cancellationToken);
        
        ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs
        {
            PreviousViewMode = previousMode,
            NewViewMode = _activeViewMode,
            Reason = ViewModeChangeReason.Reset
        });
    }
    
    public async Task<ViewMode?> GetSuggestedViewModeAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        // Check auto-select rules
        List<ViewModeAutoSelectRule> rules;
        lock (_lock)
        {
            rules = _autoSelectRules.Where(r => r.IsEnabled).OrderByDescending(r => r.Priority).ToList();
        }
        
        foreach (var rule in rules)
        {
            bool matches = false;
            
            switch (rule.Type)
            {
                case AutoSelectRuleType.PathWildcard:
                    matches = MatchWildcard(directoryPath, rule.PathPattern);
                    break;
                case AutoSelectRuleType.PathRegex:
                    try
                    {
                        matches = Regex.IsMatch(directoryPath, rule.PathPattern, RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        // Invalid regex
                    }
                    break;
            }
            
            if (matches)
            {
                lock (_lock)
                {
                    return _viewModes.FirstOrDefault(v => v.Id == rule.ViewModeId);
                }
            }
        }
        
        // Check directory contents for suggestions
        try
        {
            var files = await Task.Run(() => Directory.GetFiles(directoryPath), cancellationToken);
            var extensions = files.Select(f => Path.GetExtension(f).ToLowerInvariant()).Distinct().ToList();
            
            var imageExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" };
            var videoExtensions = new HashSet<string> { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" };
            var audioExtensions = new HashSet<string> { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" };
            
            int imageCount = extensions.Count(e => imageExtensions.Contains(e));
            int videoCount = extensions.Count(e => videoExtensions.Contains(e));
            int audioCount = extensions.Count(e => audioExtensions.Contains(e));
            
            // If mostly images or videos, suggest thumbnail view
            if (imageCount + videoCount > extensions.Count * 0.5)
            {
                lock (_lock)
                {
                    return _viewModes.FirstOrDefault(v => v.Style == ViewStyle.Thumbnails);
                }
            }
        }
        catch
        {
            // Cannot analyze directory
        }
        
        return null;
    }
    
    private static bool MatchWildcard(string path, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
    
    public async Task SetAutoSelectRuleAsync(string pathPattern, string viewModeId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var rule = new ViewModeAutoSelectRule
        {
            PathPattern = pathPattern,
            ViewModeId = viewModeId,
            Type = pathPattern.Contains("*") || pathPattern.Contains("?") 
                ? AutoSelectRuleType.PathWildcard 
                : AutoSelectRuleType.PathRegex,
            IsEnabled = true
        };
        
        lock (_lock)
        {
            // Remove existing rule for same pattern
            _autoSelectRules.RemoveAll(r => r.PathPattern.Equals(pathPattern, StringComparison.OrdinalIgnoreCase));
            _autoSelectRules.Add(rule);
        }
        
        await SaveRulesAsync(cancellationToken);
    }
}
