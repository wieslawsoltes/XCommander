// MultiRenameToolService.cs - Implementation of TC-style Multi-Rename Tool
// Comprehensive batch renaming with patterns, regex, counters, and more

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public class MultiRenameToolService : IMultiRenameToolService
{
    private readonly ConcurrentDictionary<string, RenamePreset> _presets = new();
    private readonly List<RenameUndoInfo> _undoHistory = new();
    private readonly string _presetsPath;
    private readonly object _undoLock = new();
    
    private static readonly IReadOnlyList<PatternPlaceholder> _placeholders = new[]
    {
        new PatternPlaceholder { Code = "[N]", Name = "Name", Description = "Original filename without extension", Example = "document" },
        new PatternPlaceholder { Code = "[N1-5]", Name = "Name substring", Description = "Characters 1-5 of name", Example = "docum", AcceptsParameters = true },
        new PatternPlaceholder { Code = "[E]", Name = "Extension", Description = "File extension without dot", Example = "txt" },
        new PatternPlaceholder { Code = "[C]", Name = "Counter", Description = "Counter value", Example = "001", AcceptsParameters = true },
        new PatternPlaceholder { Code = "[C:3]", Name = "Counter with width", Description = "Counter with specified width", Example = "001", AcceptsParameters = true },
        new PatternPlaceholder { Code = "[Y]", Name = "Year", Description = "4-digit year", Example = "2024" },
        new PatternPlaceholder { Code = "[M]", Name = "Month", Description = "2-digit month", Example = "03" },
        new PatternPlaceholder { Code = "[D]", Name = "Day", Description = "2-digit day", Example = "15" },
        new PatternPlaceholder { Code = "[h]", Name = "Hour", Description = "2-digit hour (24h)", Example = "14" },
        new PatternPlaceholder { Code = "[m]", Name = "Minute", Description = "2-digit minute", Example = "30" },
        new PatternPlaceholder { Code = "[s]", Name = "Second", Description = "2-digit second", Example = "45" },
        new PatternPlaceholder { Code = "[P]", Name = "Parent", Description = "Parent folder name", Example = "Documents" },
        new PatternPlaceholder { Code = "[G]", Name = "Grandparent", Description = "Grandparent folder name", Example = "User" },
        new PatternPlaceholder { Code = "[A]", Name = "Attributes", Description = "File attributes", Example = "ra" },
        new PatternPlaceholder { Code = "[=field]", Name = "Plugin field", Description = "Content plugin field value", Example = "Artist", AcceptsParameters = true }
    };
    
    public event EventHandler<RenameCompletedEventArgs>? RenameCompleted;
    public event EventHandler<EventArgs>? PresetsChanged;
    
    public MultiRenameToolService()
    {
        _presetsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander", "rename_presets.json");
        
        InitializeBuiltInPresets();
        LoadPresets();
    }
    
    private void InitializeBuiltInPresets()
    {
        // Photo rename preset
        _presets["builtin.photos"] = new RenamePreset
        {
            Id = "builtin.photos",
            Name = "Photo Rename",
            Description = "Rename photos by date: YYYY-MM-DD_HH-mm-ss",
            Rules = new[]
            {
                CreateDateTimeRule("yyyy-MM-dd_HH-mm-ss", DateTimeSource.Modified, 0)
            },
            Mask = "*.jpg;*.jpeg;*.png;*.gif",
            IsBuiltIn = true
        };
        
        // Music files preset
        _presets["builtin.music"] = new RenamePreset
        {
            Id = "builtin.music",
            Name = "Music Files",
            Description = "Format: ## - Artist - Title",
            Rules = new[]
            {
                CreateCounterRule(1, 1, 2, null, " - ")
            },
            Mask = "*.mp3;*.flac;*.m4a;*.ogg",
            IsBuiltIn = true
        };
        
        // Lowercase preset
        _presets["builtin.lowercase"] = new RenamePreset
        {
            Id = "builtin.lowercase",
            Name = "Lowercase All",
            Description = "Convert filename and extension to lowercase",
            Rules = new[]
            {
                CreateCaseRule(CaseChangeType.Lowercase, true)
            },
            IsBuiltIn = true
        };
        
        // Remove spaces preset
        _presets["builtin.nospaces"] = new RenamePreset
        {
            Id = "builtin.nospaces",
            Name = "Remove Spaces",
            Description = "Replace spaces with underscores",
            Rules = new[]
            {
                CreateReplaceRule(" ", "_")
            },
            IsBuiltIn = true
        };
        
        // Sequential numbering preset
        _presets["builtin.sequence"] = new RenamePreset
        {
            Id = "builtin.sequence",
            Name = "Sequential Numbering",
            Description = "Add sequential numbers: 001, 002, 003...",
            Rules = new[]
            {
                CreateCounterRule(1, 1, 3)
            },
            IsBuiltIn = true
        };
    }
    
    private void LoadPresets()
    {
        try
        {
            if (File.Exists(_presetsPath))
            {
                var json = File.ReadAllText(_presetsPath);
                var presets = JsonSerializer.Deserialize<List<RenamePreset>>(json);
                if (presets != null)
                {
                    foreach (var preset in presets.Where(p => !p.IsBuiltIn))
                    {
                        _presets[preset.Id] = preset;
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }
    
    private async Task SavePresetsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dir = Path.GetDirectoryName(_presetsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var toSave = _presets.Values.Where(p => !p.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_presetsPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    // ======= Preview Operations =======
    
    public async Task<IReadOnlyList<RenamePreview>> PreviewRenameAsync(
        IEnumerable<string> files,
        RenameOptions options,
        CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        var previews = new List<RenamePreview>();
        var usedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var counter = 1;
        
        foreach (var file in fileList)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var preview = await GeneratePreviewAsync(file, options, counter, usedNames, cancellationToken);
            previews.Add(preview);
            
            if (preview.WillChange && !preview.HasConflict)
            {
                usedNames[preview.NewName] = file;
            }
            
            counter++;
        }
        
        return previews;
    }
    
    private async Task<RenamePreview> GeneratePreviewAsync(
        string filePath,
        RenameOptions options,
        int counter,
        Dictionary<string, string> usedNames,
        CancellationToken cancellationToken)
    {
        try
        {
            var originalName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? "";
            var newName = originalName;
            
            // Apply each enabled rule in order
            foreach (var rule in options.Rules.Where(r => r.Enabled).OrderBy(r => r.Order))
            {
                newName = await ApplyRuleAsync(newName, filePath, rule, counter, cancellationToken);
            }
            
            var willChange = !string.Equals(originalName, newName, StringComparison.Ordinal);
            var hasConflict = false;
            string? conflictMessage = null;
            
            if (willChange)
            {
                // Check for conflicts
                var newPath = Path.Combine(directory, newName);
                
                if (usedNames.TryGetValue(newName, out var existingFile) && existingFile != filePath)
                {
                    hasConflict = true;
                    conflictMessage = $"Name already used by: {Path.GetFileName(existingFile)}";
                }
                else if (File.Exists(newPath) && !string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    hasConflict = true;
                    conflictMessage = "File already exists";
                }
            }
            
            return new RenamePreview
            {
                OriginalPath = filePath,
                OriginalName = originalName,
                NewName = newName,
                NewPath = Path.Combine(directory, newName),
                WillChange = willChange,
                HasConflict = hasConflict,
                ConflictMessage = conflictMessage
            };
        }
        catch (Exception ex)
        {
            return new RenamePreview
            {
                OriginalPath = filePath,
                OriginalName = Path.GetFileName(filePath),
                NewName = Path.GetFileName(filePath),
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<RenamePreview> PreviewSingleRenameAsync(
        string filePath,
        RenameRule rule,
        int counterValue = 0,
        CancellationToken cancellationToken = default)
    {
        var options = new RenameOptions { Rules = new[] { rule } };
        var previews = await PreviewRenameAsync(new[] { filePath }, options, cancellationToken);
        return previews.FirstOrDefault() ?? new RenamePreview
        {
            OriginalPath = filePath,
            OriginalName = Path.GetFileName(filePath),
            NewName = Path.GetFileName(filePath)
        };
    }
    
    public Task<IReadOnlyList<(string RuleId, string Error)>> ValidateRulesAsync(
        IEnumerable<RenameRule> rules,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<(string, string)>();
        
        foreach (var rule in rules)
        {
            if (rule.UseRegex && !string.IsNullOrEmpty(rule.SearchText))
            {
                try
                {
                    _ = new Regex(rule.SearchText);
                }
                catch (Exception ex)
                {
                    errors.Add((rule.Id, $"Invalid regex: {ex.Message}"));
                }
            }
            
            if (rule.Type == RenameOperationType.Extension && string.IsNullOrWhiteSpace(rule.NewExtension) && !rule.RemoveExtension)
            {
                errors.Add((rule.Id, "Extension is required"));
            }
        }
        
        return Task.FromResult<IReadOnlyList<(string, string)>>(errors);
    }
    
    // ======= Rename Operations =======
    
    public async Task<IReadOnlyList<RenameResult>> RenameAsync(
        IEnumerable<string> files,
        RenameOptions options,
        IProgress<(int Current, int Total, string CurrentFile)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        var results = new List<RenameResult>();
        var undoList = new List<(string NewPath, string OriginalPath)>();
        var counter = 1;
        var total = fileList.Count;
        
        // First generate all previews to detect conflicts
        var previews = await PreviewRenameAsync(fileList, options, cancellationToken);
        
        foreach (var preview in previews)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new RenameResult
                {
                    OriginalPath = preview.OriginalPath,
                    ErrorMessage = "Cancelled"
                });
                continue;
            }
            
            progress?.Report((counter, total, preview.OriginalName));
            
            if (!preview.WillChange)
            {
                results.Add(new RenameResult
                {
                    OriginalPath = preview.OriginalPath,
                    NewPath = preview.OriginalPath,
                    Success = true
                });
            }
            else if (preview.HasConflict)
            {
                var resolved = await ResolveConflictAsync(preview, options.ConflictResolution, cancellationToken);
                results.Add(resolved);
                
                if (resolved.Success && resolved.NewPath != null)
                {
                    undoList.Add((resolved.NewPath, preview.OriginalPath));
                }
            }
            else if (!options.SimulateOnly)
            {
                var result = await ExecuteRenameAsync(preview.OriginalPath, preview.NewPath!, cancellationToken);
                results.Add(result);
                
                if (result.Success && result.NewPath != null)
                {
                    undoList.Add((result.NewPath, preview.OriginalPath));
                }
            }
            else
            {
                results.Add(new RenameResult
                {
                    OriginalPath = preview.OriginalPath,
                    NewPath = preview.NewPath,
                    Success = true
                });
            }
            
            counter++;
        }
        
        // Create undo info
        RenameUndoInfo? undoInfo = null;
        if (options.CreateUndo && undoList.Count > 0 && !options.SimulateOnly)
        {
            undoInfo = new RenameUndoInfo
            {
                Renames = undoList,
                Description = $"Renamed {undoList.Count} files"
            };
            
            lock (_undoLock)
            {
                _undoHistory.Insert(0, undoInfo);
                
                // Keep only last 50 undo entries
                while (_undoHistory.Count > 50)
                {
                    _undoHistory.RemoveAt(_undoHistory.Count - 1);
                }
            }
        }
        
        OnRenameCompleted(results, undoInfo);
        return results;
    }
    
    private async Task<RenameResult> ResolveConflictAsync(
        RenamePreview preview,
        RenameConflictResolution resolution,
        CancellationToken cancellationToken)
    {
        switch (resolution)
        {
            case RenameConflictResolution.Skip:
                return new RenameResult
                {
                    OriginalPath = preview.OriginalPath,
                    ErrorMessage = "Skipped due to conflict"
                };
                
            case RenameConflictResolution.Overwrite:
                return await ExecuteRenameAsync(preview.OriginalPath, preview.NewPath!, cancellationToken);
                
            case RenameConflictResolution.AutoNumber:
                var newPath = GetUniqueFileName(preview.NewPath!);
                return await ExecuteRenameAsync(preview.OriginalPath, newPath, cancellationToken);
                
            default:
                return new RenameResult
                {
                    OriginalPath = preview.OriginalPath,
                    ErrorMessage = "Conflict not resolved"
                };
        }
    }
    
    private string GetUniqueFileName(string path)
    {
        if (!File.Exists(path))
            return path;
        
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var counter = 1;
        
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
        } while (File.Exists(newPath) && counter < 1000);
        
        return newPath;
    }
    
    private async Task<RenameResult> ExecuteRenameAsync(string originalPath, string newPath, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(originalPath))
                {
                    File.Move(originalPath, newPath);
                }
                else if (Directory.Exists(originalPath))
                {
                    Directory.Move(originalPath, newPath);
                }
            }, cancellationToken);
            
            return new RenameResult
            {
                OriginalPath = originalPath,
                NewPath = newPath,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new RenameResult
            {
                OriginalPath = originalPath,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<RenameResult> RenameSingleAsync(
        string filePath,
        string newName,
        RenameConflictResolution conflictResolution = RenameConflictResolution.AutoNumber,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var newPath = Path.Combine(dir, newName);
        
        if (File.Exists(newPath) && !string.Equals(filePath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            switch (conflictResolution)
            {
                case RenameConflictResolution.Skip:
                    return new RenameResult { OriginalPath = filePath, ErrorMessage = "File exists" };
                case RenameConflictResolution.AutoNumber:
                    newPath = GetUniqueFileName(newPath);
                    break;
            }
        }
        
        return await ExecuteRenameAsync(filePath, newPath, cancellationToken);
    }
    
    public async Task<IReadOnlyList<RenameResult>> UndoAsync(string? undoId = null, CancellationToken cancellationToken = default)
    {
        RenameUndoInfo? undoInfo;
        
        lock (_undoLock)
        {
            if (undoId != null)
            {
                undoInfo = _undoHistory.FirstOrDefault(u => u.Id == undoId);
                if (undoInfo != null)
                {
                    _undoHistory.Remove(undoInfo);
                }
            }
            else
            {
                undoInfo = _undoHistory.FirstOrDefault();
                if (undoInfo != null)
                {
                    _undoHistory.RemoveAt(0);
                }
            }
        }
        
        if (undoInfo == null)
        {
            return Array.Empty<RenameResult>();
        }
        
        var results = new List<RenameResult>();
        
        // Undo in reverse order
        foreach (var (newPath, originalPath) in undoInfo.Renames.Reverse())
        {
            var result = await ExecuteRenameAsync(newPath, originalPath, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    public IReadOnlyList<RenameUndoInfo> GetUndoHistory()
    {
        lock (_undoLock)
        {
            return _undoHistory.ToList();
        }
    }
    
    public void ClearUndoHistory()
    {
        lock (_undoLock)
        {
            _undoHistory.Clear();
        }
    }
    
    // ======= Rule Application =======
    
    private async Task<string> ApplyRuleAsync(string currentName, string filePath, RenameRule rule, int counter, CancellationToken cancellationToken)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(currentName);
        var extension = Path.GetExtension(currentName);
        
        switch (rule.Type)
        {
            case RenameOperationType.Replace:
                return ApplyReplaceRule(currentName, rule);
                
            case RenameOperationType.Case:
                return ApplyCaseRule(currentName, rule);
                
            case RenameOperationType.Counter:
                return ApplyCounterRule(currentName, rule, counter);
                
            case RenameOperationType.DateTime:
                return await ApplyDateTimeRuleAsync(currentName, filePath, rule, cancellationToken);
                
            case RenameOperationType.RegexReplace:
                return ApplyRegexRule(currentName, rule);
                
            case RenameOperationType.Extension:
                return ApplyExtensionRule(currentName, rule);
                
            case RenameOperationType.Insert:
                return ApplyInsertRule(currentName, rule);
                
            case RenameOperationType.Delete:
                return ApplyDeleteRule(currentName, rule);
                
            default:
                return currentName;
        }
    }
    
    private string ApplyReplaceRule(string name, RenameRule rule)
    {
        if (string.IsNullOrEmpty(rule.SearchText))
            return name;
        
        var nameWithoutExt = rule.ApplyToExtension ? name : Path.GetFileNameWithoutExtension(name);
        var extension = rule.ApplyToExtension ? "" : Path.GetExtension(name);
        
        if (rule.UseRegex)
        {
            var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            nameWithoutExt = Regex.Replace(nameWithoutExt, rule.SearchText, rule.ReplaceText ?? "", options);
        }
        else
        {
            var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            nameWithoutExt = nameWithoutExt.Replace(rule.SearchText, rule.ReplaceText ?? "", comparison);
        }
        
        return nameWithoutExt + extension;
    }
    
    private string ApplyCaseRule(string name, RenameRule rule)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        
        if (rule.CaseApplyToName)
        {
            nameWithoutExt = ApplyCase(nameWithoutExt, rule.CaseType);
        }
        
        if (rule.CaseApplyToExtension)
        {
            extension = ApplyCase(extension, rule.CaseType);
        }
        
        return nameWithoutExt + extension;
    }
    
    private string ApplyCase(string text, CaseChangeType caseType)
    {
        return caseType switch
        {
            CaseChangeType.Lowercase => text.ToLowerInvariant(),
            CaseChangeType.Uppercase => text.ToUpperInvariant(),
            CaseChangeType.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
            CaseChangeType.SentenceCase => char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant(),
            CaseChangeType.InvertCase => new string(text.Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).ToArray()),
            _ => text
        };
    }
    
    private string ApplyCounterRule(string name, RenameRule rule, int fileCounter)
    {
        var counterValue = rule.CounterStart + (fileCounter - 1) * rule.CounterStep;
        var counterStr = counterValue.ToString().PadLeft(rule.CounterWidth, '0');
        
        if (!string.IsNullOrEmpty(rule.CounterPrefix))
            counterStr = rule.CounterPrefix + counterStr;
        if (!string.IsNullOrEmpty(rule.CounterSuffix))
            counterStr = counterStr + rule.CounterSuffix;
        
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        
        if (rule.CounterPosition < 0)
        {
            // Insert at end
            return nameWithoutExt + counterStr + extension;
        }
        else if (rule.CounterPosition == 0)
        {
            // Insert at beginning
            return counterStr + nameWithoutExt + extension;
        }
        else
        {
            // Insert at specific position
            var pos = Math.Min(rule.CounterPosition, nameWithoutExt.Length);
            return nameWithoutExt[..pos] + counterStr + nameWithoutExt[pos..] + extension;
        }
    }
    
    private async Task<string> ApplyDateTimeRuleAsync(string name, string filePath, RenameRule rule, CancellationToken cancellationToken)
    {
        DateTime dateTime;
        
        try
        {
            var fileInfo = new FileInfo(filePath);
            dateTime = rule.DateTimeSource switch
            {
                DateTimeSource.Modified => fileInfo.LastWriteTime,
                DateTimeSource.Created => fileInfo.CreationTime,
                DateTimeSource.Accessed => fileInfo.LastAccessTime,
                DateTimeSource.Current => DateTime.Now,
                _ => fileInfo.LastWriteTime
            };
        }
        catch
        {
            dateTime = DateTime.Now;
        }
        
        var dateStr = dateTime.ToString(rule.DateTimeFormat ?? "yyyyMMdd");
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        
        if (rule.DateTimePosition == 0)
        {
            return dateStr + nameWithoutExt + extension;
        }
        else
        {
            return nameWithoutExt + dateStr + extension;
        }
    }
    
    private string ApplyRegexRule(string name, RenameRule rule)
    {
        if (string.IsNullOrEmpty(rule.SearchText))
            return name;
        
        try
        {
            var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.Replace(name, rule.SearchText, rule.ReplaceText ?? "", options);
        }
        catch
        {
            return name;
        }
    }
    
    private string ApplyExtensionRule(string name, RenameRule rule)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var currentExt = Path.GetExtension(name);
        
        if (rule.RemoveExtension)
        {
            return nameWithoutExt;
        }
        
        var newExt = rule.NewExtension ?? "";
        if (!newExt.StartsWith('.') && !string.IsNullOrEmpty(newExt))
        {
            newExt = "." + newExt;
        }
        
        if (rule.AddExtension)
        {
            return name + newExt;
        }
        
        return nameWithoutExt + newExt;
    }
    
    private string ApplyInsertRule(string name, RenameRule rule)
    {
        if (string.IsNullOrEmpty(rule.InsertText))
            return name;
        
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        
        var pos = rule.FromEnd 
            ? Math.Max(0, nameWithoutExt.Length - rule.Position)
            : Math.Min(rule.Position, nameWithoutExt.Length);
        
        return nameWithoutExt[..pos] + rule.InsertText + nameWithoutExt[pos..] + extension;
    }
    
    private string ApplyDeleteRule(string name, RenameRule rule)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        
        if (rule.Length <= 0 || nameWithoutExt.Length == 0)
            return name;
        
        int startPos, endPos;
        
        if (rule.FromEnd)
        {
            endPos = nameWithoutExt.Length - rule.Position;
            startPos = Math.Max(0, endPos - rule.Length);
        }
        else
        {
            startPos = Math.Min(rule.Position, nameWithoutExt.Length);
            endPos = Math.Min(startPos + rule.Length, nameWithoutExt.Length);
        }
        
        if (startPos >= endPos)
            return name;
        
        return nameWithoutExt[..startPos] + nameWithoutExt[endPos..] + extension;
    }
    
    // ======= Preset Management =======
    
    public IReadOnlyList<RenamePreset> GetPresets()
    {
        return _presets.Values.OrderBy(p => p.IsBuiltIn ? 0 : 1).ThenBy(p => p.Name).ToList();
    }
    
    public RenamePreset? GetPreset(string presetId)
    {
        return _presets.TryGetValue(presetId, out var preset) ? preset : null;
    }
    
    public async Task<RenamePreset> SavePresetAsync(RenamePreset preset, CancellationToken cancellationToken = default)
    {
        var toSave = preset with { IsBuiltIn = false };
        _presets[toSave.Id] = toSave;
        await SavePresetsAsync(cancellationToken);
        OnPresetsChanged();
        return toSave;
    }
    
    public async Task<bool> DeletePresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        if (_presets.TryGetValue(presetId, out var preset) && preset.IsBuiltIn)
        {
            return false;
        }
        
        if (_presets.TryRemove(presetId, out _))
        {
            await SavePresetsAsync(cancellationToken);
            OnPresetsChanged();
            return true;
        }
        
        return false;
    }
    
    public async Task<IReadOnlyList<RenamePreset>> ImportPresetsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var presets = JsonSerializer.Deserialize<List<RenamePreset>>(json) ?? new();
        
        var imported = new List<RenamePreset>();
        foreach (var preset in presets)
        {
            var newPreset = preset with { Id = Guid.NewGuid().ToString(), IsBuiltIn = false };
            _presets[newPreset.Id] = newPreset;
            imported.Add(newPreset);
        }
        
        await SavePresetsAsync(cancellationToken);
        OnPresetsChanged();
        return imported;
    }
    
    public async Task ExportPresetsAsync(IEnumerable<string> presetIds, string filePath, CancellationToken cancellationToken = default)
    {
        var toExport = presetIds
            .Select(id => GetPreset(id))
            .Where(p => p != null)
            .Cast<RenamePreset>()
            .Select(p => p with { IsBuiltIn = false })
            .ToList();
        
        var json = JsonSerializer.Serialize(toExport, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    // ======= Rule Helpers =======
    
    public RenameRule CreateReplaceRule(string search, string replace, bool caseSensitive = false, bool useRegex = false)
    {
        return new RenameRule
        {
            Type = useRegex ? RenameOperationType.RegexReplace : RenameOperationType.Replace,
            SearchText = search,
            ReplaceText = replace,
            CaseSensitive = caseSensitive,
            UseRegex = useRegex
        };
    }
    
    public RenameRule CreateCaseRule(CaseChangeType caseType, bool applyToExtension = false)
    {
        return new RenameRule
        {
            Type = RenameOperationType.Case,
            CaseType = caseType,
            CaseApplyToName = true,
            CaseApplyToExtension = applyToExtension
        };
    }
    
    public RenameRule CreateCounterRule(int start = 1, int step = 1, int width = 3, string? prefix = null, string? suffix = null)
    {
        return new RenameRule
        {
            Type = RenameOperationType.Counter,
            CounterStart = start,
            CounterStep = step,
            CounterWidth = width,
            CounterPrefix = prefix,
            CounterSuffix = suffix,
            CounterPosition = -1
        };
    }
    
    public RenameRule CreateDateTimeRule(string format, DateTimeSource source = DateTimeSource.Modified, int position = 0)
    {
        return new RenameRule
        {
            Type = RenameOperationType.DateTime,
            DateTimeFormat = format,
            DateTimeSource = source,
            DateTimePosition = position
        };
    }
    
    public RenameRule CreateInsertRule(string text, int position, bool fromEnd = false)
    {
        return new RenameRule
        {
            Type = RenameOperationType.Insert,
            InsertText = text,
            Position = position,
            FromEnd = fromEnd
        };
    }
    
    public RenameRule CreateDeleteRule(int position, int length, bool fromEnd = false)
    {
        return new RenameRule
        {
            Type = RenameOperationType.Delete,
            Position = position,
            Length = length,
            FromEnd = fromEnd
        };
    }
    
    public RenameRule CreateExtensionRule(string newExtension, bool add = false)
    {
        return new RenameRule
        {
            Type = RenameOperationType.Extension,
            NewExtension = newExtension,
            AddExtension = add
        };
    }
    
    // ======= Pattern Helpers =======
    
    public async Task<string> ApplyPatternAsync(string pattern, string filePath, int counter = 0, CancellationToken cancellationToken = default)
    {
        var result = pattern;
        var fileInfo = new FileInfo(filePath);
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath).TrimStart('.');
        
        // [N] - name
        result = result.Replace("[N]", name);
        
        // [E] - extension
        result = result.Replace("[E]", ext);
        
        // [N1-5] - substring
        result = Regex.Replace(result, @"\[N(\d+)-(\d+)\]", m =>
        {
            var start = int.Parse(m.Groups[1].Value) - 1;
            var end = int.Parse(m.Groups[2].Value);
            if (start < name.Length)
            {
                var len = Math.Min(end - start, name.Length - start);
                return name.Substring(start, len);
            }
            return "";
        });
        
        // [C] or [C:3] - counter
        result = Regex.Replace(result, @"\[C(?::(\d+))?\]", m =>
        {
            var width = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 3;
            return counter.ToString().PadLeft(width, '0');
        });
        
        // Date/time placeholders
        var modified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.Now;
        result = result.Replace("[Y]", modified.ToString("yyyy"));
        result = result.Replace("[M]", modified.ToString("MM"));
        result = result.Replace("[D]", modified.ToString("dd"));
        result = result.Replace("[h]", modified.ToString("HH"));
        result = result.Replace("[m]", modified.ToString("mm"));
        result = result.Replace("[s]", modified.ToString("ss"));
        
        // [P] - parent folder
        var parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parent))
        {
            result = result.Replace("[P]", Path.GetFileName(parent));
            
            // [G] - grandparent folder
            var grandparent = Path.GetDirectoryName(parent);
            if (!string.IsNullOrEmpty(grandparent))
            {
                result = result.Replace("[G]", Path.GetFileName(grandparent));
            }
        }
        
        return result;
    }
    
    public IReadOnlyList<PatternPlaceholder> GetPatternPlaceholders()
    {
        return _placeholders;
    }
    
    public (bool IsValid, string? ErrorMessage) ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return (false, "Pattern cannot be empty");
        }
        
        // Check for unbalanced brackets
        var openCount = pattern.Count(c => c == '[');
        var closeCount = pattern.Count(c => c == ']');
        
        if (openCount != closeCount)
        {
            return (false, "Unbalanced brackets in pattern");
        }
        
        // Check for invalid characters in filename
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            if (pattern.Contains(c))
            {
                return (false, $"Pattern contains invalid character: {c}");
            }
        }
        
        return (true, null);
    }
    
    // ======= Events =======
    
    private void OnRenameCompleted(IReadOnlyList<RenameResult> results, RenameUndoInfo? undoInfo)
    {
        RenameCompleted?.Invoke(this, new RenameCompletedEventArgs(results, undoInfo));
    }
    
    private void OnPresetsChanged()
    {
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }
}
