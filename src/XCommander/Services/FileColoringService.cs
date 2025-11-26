using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Criteria for file coloring
/// </summary>
public enum ColorCriteria
{
    Extension,
    Attribute,
    Size,
    Age,
    NamePattern,
    Custom
}

/// <summary>
/// A single file coloring rule
/// </summary>
public class FileColorRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ColorCriteria Criteria { get; set; }
    public string Pattern { get; set; } = string.Empty; // Extension, regex, etc.
    public FileAttributes? RequiredAttributes { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public int? DaysOld { get; set; } // For age-based coloring
    public string ForegroundColor { get; set; } = "#FFFFFF";
    public string? BackgroundColor { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public int Priority { get; set; } // Lower = higher priority
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Color information for a file
/// </summary>
public class FileColorInfo
{
    public string ForegroundColor { get; init; } = "#FFFFFF";
    public string? BackgroundColor { get; init; }
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public string? MatchedRuleName { get; init; }
}

/// <summary>
/// Default color scheme presets
/// </summary>
public class ColorSchemePreset
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<FileColorRule> Rules { get; init; } = new();
}

/// <summary>
/// Service for managing file coloring rules
/// </summary>
public interface IFileColoringService
{
    /// <summary>
    /// Event raised when rules change
    /// </summary>
    event EventHandler? RulesChanged;
    
    /// <summary>
    /// Get color info for a file
    /// </summary>
    FileColorInfo? GetColorForFile(string filePath);
    
    /// <summary>
    /// Get color info for a file with provided attributes
    /// </summary>
    FileColorInfo? GetColorForFile(string fileName, long size, DateTime modified, FileAttributes attributes);
    
    /// <summary>
    /// Get all color rules
    /// </summary>
    IReadOnlyList<FileColorRule> GetAllRules();
    
    /// <summary>
    /// Get enabled rules only
    /// </summary>
    IReadOnlyList<FileColorRule> GetEnabledRules();
    
    /// <summary>
    /// Add a new rule
    /// </summary>
    void AddRule(FileColorRule rule);
    
    /// <summary>
    /// Update an existing rule
    /// </summary>
    bool UpdateRule(FileColorRule rule);
    
    /// <summary>
    /// Remove a rule
    /// </summary>
    bool RemoveRule(string id);
    
    /// <summary>
    /// Enable/disable a rule
    /// </summary>
    bool SetRuleEnabled(string id, bool enabled);
    
    /// <summary>
    /// Move rule up in priority
    /// </summary>
    bool MoveRuleUp(string id);
    
    /// <summary>
    /// Move rule down in priority
    /// </summary>
    bool MoveRuleDown(string id);
    
    /// <summary>
    /// Import rules from file
    /// </summary>
    Task<int> ImportRulesAsync(string filePath);
    
    /// <summary>
    /// Export rules to file
    /// </summary>
    Task ExportRulesAsync(string filePath);
    
    /// <summary>
    /// Load preset color scheme
    /// </summary>
    void LoadPreset(string presetName);
    
    /// <summary>
    /// Get available presets
    /// </summary>
    IReadOnlyList<ColorSchemePreset> GetAvailablePresets();
    
    /// <summary>
    /// Clear all rules
    /// </summary>
    void ClearAllRules();
    
    /// <summary>
    /// Save rules to persistent storage
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Load rules from persistent storage
    /// </summary>
    Task LoadAsync();
}

public class FileColoringService : IFileColoringService
{
    private readonly string _rulesFilePath;
    private readonly List<FileColorRule> _rules = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, Regex> _regexCache = new();
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public event EventHandler? RulesChanged;
    
    public FileColoringService(string? rulesFilePath = null)
    {
        _rulesFilePath = rulesFilePath ?? GetDefaultRulesPath();
    }
    
    private static string GetDefaultRulesPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XCommander", "file_colors.json");
    }
    
    public FileColorInfo? GetColorForFile(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            return GetColorForFile(info.Name, info.Length, info.LastWriteTime, info.Attributes);
        }
        catch
        {
            return null;
        }
    }
    
    public FileColorInfo? GetColorForFile(string fileName, long size, DateTime modified, FileAttributes attributes)
    {
        lock (_lock)
        {
            var enabledRules = _rules
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ToList();
            
            foreach (var rule in enabledRules)
            {
                if (MatchesRule(fileName, size, modified, attributes, rule))
                {
                    return new FileColorInfo
                    {
                        ForegroundColor = rule.ForegroundColor,
                        BackgroundColor = rule.BackgroundColor,
                        IsBold = rule.IsBold,
                        IsItalic = rule.IsItalic,
                        MatchedRuleName = rule.Name
                    };
                }
            }
        }
        
        return null;
    }
    
    public IReadOnlyList<FileColorRule> GetAllRules()
    {
        lock (_lock)
        {
            return _rules.OrderBy(r => r.Priority).ToList();
        }
    }
    
    public IReadOnlyList<FileColorRule> GetEnabledRules()
    {
        lock (_lock)
        {
            return _rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority).ToList();
        }
    }
    
    public void AddRule(FileColorRule rule)
    {
        lock (_lock)
        {
            rule.Priority = _rules.Any() ? _rules.Max(r => r.Priority) + 1 : 0;
            _rules.Add(rule);
        }
        
        OnRulesChanged();
    }
    
    public bool UpdateRule(FileColorRule rule)
    {
        lock (_lock)
        {
            var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing == null) return false;
            
            var index = _rules.IndexOf(existing);
            _rules[index] = rule;
            
            // Clear regex cache for this rule
            _regexCache.Remove(rule.Id);
        }
        
        OnRulesChanged();
        return true;
    }
    
    public bool RemoveRule(string id)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return false;
            
            _rules.Remove(rule);
            _regexCache.Remove(id);
        }
        
        OnRulesChanged();
        return true;
    }
    
    public bool SetRuleEnabled(string id, bool enabled)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return false;
            
            rule.IsEnabled = enabled;
        }
        
        OnRulesChanged();
        return true;
    }
    
    public bool MoveRuleUp(string id)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return false;
            
            var higherPriority = _rules
                .Where(r => r.Priority < rule.Priority)
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();
            
            if (higherPriority == null) return false;
            
            // Swap priorities
            (rule.Priority, higherPriority.Priority) = (higherPriority.Priority, rule.Priority);
        }
        
        OnRulesChanged();
        return true;
    }
    
    public bool MoveRuleDown(string id)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule == null) return false;
            
            var lowerPriority = _rules
                .Where(r => r.Priority > rule.Priority)
                .OrderBy(r => r.Priority)
                .FirstOrDefault();
            
            if (lowerPriority == null) return false;
            
            // Swap priorities
            (rule.Priority, lowerPriority.Priority) = (lowerPriority.Priority, rule.Priority);
        }
        
        OnRulesChanged();
        return true;
    }
    
    public async Task<int> ImportRulesAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var imported = JsonSerializer.Deserialize<List<FileColorRule>>(json, JsonOptions);
        
        if (imported == null) return 0;
        
        var count = 0;
        lock (_lock)
        {
            foreach (var rule in imported)
            {
                // Check for duplicates by pattern
                if (!_rules.Any(r => r.Pattern == rule.Pattern && r.Criteria == rule.Criteria))
                {
                    rule.Priority = _rules.Any() ? _rules.Max(r => r.Priority) + 1 : 0;
                    _rules.Add(rule);
                    count++;
                }
            }
        }
        
        OnRulesChanged();
        return count;
    }
    
    public async Task ExportRulesAsync(string filePath)
    {
        List<FileColorRule> rulesToExport;
        
        lock (_lock)
        {
            rulesToExport = _rules.OrderBy(r => r.Priority).ToList();
        }
        
        var json = JsonSerializer.Serialize(rulesToExport, JsonOptions);
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public void LoadPreset(string presetName)
    {
        var preset = GetAvailablePresets().FirstOrDefault(p => p.Name == presetName);
        if (preset == null) return;
        
        lock (_lock)
        {
            _rules.Clear();
            _regexCache.Clear();
            
            foreach (var rule in preset.Rules)
            {
                _rules.Add(rule);
            }
        }
        
        OnRulesChanged();
    }
    
    public IReadOnlyList<ColorSchemePreset> GetAvailablePresets()
    {
        return new List<ColorSchemePreset>
        {
            CreateTotalCommanderPreset(),
            CreateProgrammerPreset(),
            CreateMediaPreset(),
            CreateMinimalPreset()
        };
    }
    
    public void ClearAllRules()
    {
        lock (_lock)
        {
            _rules.Clear();
            _regexCache.Clear();
        }
        
        OnRulesChanged();
    }
    
    public async Task SaveAsync()
    {
        await ExportRulesAsync(_rulesFilePath);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(_rulesFilePath))
        {
            // Load default preset
            LoadPreset("Total Commander Style");
            return;
        }
        
        try
        {
            await ImportRulesAsync(_rulesFilePath);
        }
        catch
        {
            // Load default on error
            LoadPreset("Total Commander Style");
        }
    }
    
    private bool MatchesRule(string fileName, long size, DateTime modified, FileAttributes attributes, FileColorRule rule)
    {
        switch (rule.Criteria)
        {
            case ColorCriteria.Extension:
                var ext = Path.GetExtension(fileName);
                var patterns = rule.Pattern.Split(';', ',', '|');
                return patterns.Any(p => 
                    ext.Equals(p.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals("." + p.Trim(), StringComparison.OrdinalIgnoreCase));
                
            case ColorCriteria.Attribute:
                if (rule.RequiredAttributes.HasValue)
                    return (attributes & rule.RequiredAttributes.Value) == rule.RequiredAttributes.Value;
                return false;
                
            case ColorCriteria.Size:
                var meetsMin = !rule.MinSize.HasValue || size >= rule.MinSize.Value;
                var meetsMax = !rule.MaxSize.HasValue || size <= rule.MaxSize.Value;
                return meetsMin && meetsMax;
                
            case ColorCriteria.Age:
                if (rule.DaysOld.HasValue)
                {
                    var age = (DateTime.Now - modified).TotalDays;
                    return age >= rule.DaysOld.Value;
                }
                return false;
                
            case ColorCriteria.NamePattern:
            case ColorCriteria.Custom:
                return MatchesWildcard(fileName, rule.Pattern, rule.Id);
                
            default:
                return false;
        }
    }
    
    private bool MatchesWildcard(string fileName, string pattern, string ruleId)
    {
        try
        {
            if (!_regexCache.TryGetValue(ruleId, out var regex))
            {
                var regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                _regexCache[ruleId] = regex;
            }
            
            return regex.IsMatch(fileName);
        }
        catch
        {
            return false;
        }
    }
    
    private void OnRulesChanged()
    {
        RulesChanged?.Invoke(this, EventArgs.Empty);
    }
    
    private static ColorSchemePreset CreateTotalCommanderPreset()
    {
        return new ColorSchemePreset
        {
            Name = "Total Commander Style",
            Description = "Classic Total Commander color scheme",
            Rules = new List<FileColorRule>
            {
                // Executables - Green
                new() { Name = "Executables", Criteria = ColorCriteria.Extension, Pattern = ".exe;.com;.bat;.cmd;.ps1;.sh", ForegroundColor = "#00FF00", Priority = 0 },
                // Archives - Magenta
                new() { Name = "Archives", Criteria = ColorCriteria.Extension, Pattern = ".zip;.rar;.7z;.tar;.gz;.bz2;.xz", ForegroundColor = "#FF00FF", Priority = 1 },
                // Hidden - Gray
                new() { Name = "Hidden Files", Criteria = ColorCriteria.Attribute, RequiredAttributes = FileAttributes.Hidden, ForegroundColor = "#808080", IsItalic = true, Priority = 2 },
                // System - Red
                new() { Name = "System Files", Criteria = ColorCriteria.Attribute, RequiredAttributes = FileAttributes.System, ForegroundColor = "#FF0000", Priority = 3 },
                // Read-only - Blue
                new() { Name = "Read-only", Criteria = ColorCriteria.Attribute, RequiredAttributes = FileAttributes.ReadOnly, ForegroundColor = "#4169E1", Priority = 4 },
                // Documents - Yellow
                new() { Name = "Documents", Criteria = ColorCriteria.Extension, Pattern = ".doc;.docx;.pdf;.txt;.rtf;.odt", ForegroundColor = "#FFD700", Priority = 5 },
                // Images - Cyan
                new() { Name = "Images", Criteria = ColorCriteria.Extension, Pattern = ".jpg;.jpeg;.png;.gif;.bmp;.ico;.svg;.webp", ForegroundColor = "#00FFFF", Priority = 6 },
                // Audio - Orange
                new() { Name = "Audio", Criteria = ColorCriteria.Extension, Pattern = ".mp3;.wav;.flac;.ogg;.m4a;.wma", ForegroundColor = "#FFA500", Priority = 7 },
                // Video - Pink
                new() { Name = "Video", Criteria = ColorCriteria.Extension, Pattern = ".mp4;.avi;.mkv;.mov;.wmv;.webm", ForegroundColor = "#FF69B4", Priority = 8 }
            }
        };
    }
    
    private static ColorSchemePreset CreateProgrammerPreset()
    {
        return new ColorSchemePreset
        {
            Name = "Programmer",
            Description = "Color scheme optimized for developers",
            Rules = new List<FileColorRule>
            {
                // Source code - Green
                new() { Name = "C# Files", Criteria = ColorCriteria.Extension, Pattern = ".cs", ForegroundColor = "#569CD6", Priority = 0 },
                new() { Name = "JavaScript", Criteria = ColorCriteria.Extension, Pattern = ".js;.jsx;.ts;.tsx", ForegroundColor = "#F7DF1E", Priority = 1 },
                new() { Name = "Python", Criteria = ColorCriteria.Extension, Pattern = ".py;.pyw", ForegroundColor = "#3776AB", Priority = 2 },
                new() { Name = "HTML/CSS", Criteria = ColorCriteria.Extension, Pattern = ".html;.htm;.css;.scss;.less", ForegroundColor = "#E44D26", Priority = 3 },
                new() { Name = "Config", Criteria = ColorCriteria.Extension, Pattern = ".json;.yaml;.yml;.xml;.config", ForegroundColor = "#CB4B16", Priority = 4 },
                new() { Name = "Markdown", Criteria = ColorCriteria.Extension, Pattern = ".md;.markdown", ForegroundColor = "#FFFFFF", IsBold = true, Priority = 5 },
                new() { Name = "Git files", Criteria = ColorCriteria.NamePattern, Pattern = ".git*", ForegroundColor = "#F05032", Priority = 6 },
                // Build artifacts - Gray
                new() { Name = "Build Output", Criteria = ColorCriteria.Extension, Pattern = ".dll;.pdb;.obj;.o", ForegroundColor = "#808080", IsItalic = true, Priority = 7 }
            }
        };
    }
    
    private static ColorSchemePreset CreateMediaPreset()
    {
        return new ColorSchemePreset
        {
            Name = "Media Files",
            Description = "Highlighting for media file management",
            Rules = new List<FileColorRule>
            {
                // Photos - Cyan
                new() { Name = "Photos", Criteria = ColorCriteria.Extension, Pattern = ".jpg;.jpeg;.png;.raw;.cr2;.nef;.arw;.dng", ForegroundColor = "#00CED1", Priority = 0 },
                // Vector - Blue
                new() { Name = "Vector Graphics", Criteria = ColorCriteria.Extension, Pattern = ".svg;.ai;.eps;.pdf", ForegroundColor = "#1E90FF", Priority = 1 },
                // Video - Purple
                new() { Name = "Video", Criteria = ColorCriteria.Extension, Pattern = ".mp4;.mov;.avi;.mkv;.webm;.m4v", ForegroundColor = "#9370DB", Priority = 2 },
                // Audio - Green
                new() { Name = "Music", Criteria = ColorCriteria.Extension, Pattern = ".mp3;.flac;.wav;.aac;.m4a;.ogg", ForegroundColor = "#32CD32", Priority = 3 },
                // Subtitles - Yellow
                new() { Name = "Subtitles", Criteria = ColorCriteria.Extension, Pattern = ".srt;.sub;.ass;.ssa;.vtt", ForegroundColor = "#FFD700", Priority = 4 },
                // Large files - Bold
                new() { Name = "Large Files (>1GB)", Criteria = ColorCriteria.Size, MinSize = 1073741824, ForegroundColor = "#FF6347", IsBold = true, Priority = 5 },
                // Old files - Italic gray
                new() { Name = "Old (>1 year)", Criteria = ColorCriteria.Age, DaysOld = 365, ForegroundColor = "#A0A0A0", IsItalic = true, Priority = 6 }
            }
        };
    }
    
    private static ColorSchemePreset CreateMinimalPreset()
    {
        return new ColorSchemePreset
        {
            Name = "Minimal",
            Description = "Simple, minimal color scheme",
            Rules = new List<FileColorRule>
            {
                // Directories handled by UI
                // Only highlight hidden/system
                new() { Name = "Hidden", Criteria = ColorCriteria.Attribute, RequiredAttributes = FileAttributes.Hidden, ForegroundColor = "#606060", IsItalic = true, Priority = 0 },
                new() { Name = "System", Criteria = ColorCriteria.Attribute, RequiredAttributes = FileAttributes.System, ForegroundColor = "#B22222", Priority = 1 }
            }
        };
    }
}
