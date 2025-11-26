using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of IDescriptionFileService for TC-style file descriptions.
/// </summary>
public class DescriptionFileService : IDescriptionFileService
{
    private const string DescriptIonFileName = "descript.ion";
    private const string FilesBbsFileName = "files.bbs";
    private const string IndexTxtFileName = "00index.txt";
    
    public event EventHandler<string>? DescriptionsChanged;

    public async Task<string?> GetDescriptionAsync(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        
        if (string.IsNullOrEmpty(dir))
            return null;
            
        var descriptions = await GetDirectoryDescriptionsAsync(dir);
        return descriptions.FirstOrDefault(d => 
            d.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))?.Description;
    }

    public async Task SetDescriptionAsync(string filePath, string description)
    {
        var dir = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        
        if (string.IsNullOrEmpty(dir))
            return;
            
        var descriptions = (await GetDirectoryDescriptionsAsync(dir)).ToList();
        var existing = descriptions.FindIndex(d => 
            d.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            
        if (string.IsNullOrWhiteSpace(description))
        {
            // Remove description
            if (existing >= 0)
            {
                descriptions.RemoveAt(existing);
            }
        }
        else
        {
            // Add or update
            var newDesc = new FileDescription { FileName = fileName, Description = description };
            if (existing >= 0)
            {
                descriptions[existing] = newDesc;
            }
            else
            {
                descriptions.Add(newDesc);
            }
        }
        
        await WriteDescriptionsAsync(dir, descriptions);
        DescriptionsChanged?.Invoke(this, dir);
    }

    public async Task RemoveDescriptionAsync(string filePath)
    {
        await SetDescriptionAsync(filePath, string.Empty);
    }

    public async Task<IReadOnlyList<FileDescription>> GetDirectoryDescriptionsAsync(string directoryPath)
    {
        var results = new List<FileDescription>();
        
        // Try descript.ion first, then other formats
        var descriptIonPath = Path.Combine(directoryPath, DescriptIonFileName);
        if (File.Exists(descriptIonPath))
        {
            results.AddRange(await ParseDescriptIonFileAsync(descriptIonPath));
        }
        else
        {
            var filesBbsPath = Path.Combine(directoryPath, FilesBbsFileName);
            if (File.Exists(filesBbsPath))
            {
                results.AddRange(await ParseFilesBbsAsync(filesBbsPath));
            }
        }
        
        return results;
    }

    private static async Task<IReadOnlyList<FileDescription>> ParseDescriptIonFileAsync(string filePath)
    {
        var results = new List<FileDescription>();
        
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                    
                var parsed = ParseDescriptIonLine(line);
                if (parsed != null)
                {
                    results.Add(parsed);
                }
            }
        }
        catch
        {
            // File might be locked or unreadable
        }
        
        return results;
    }

    private static FileDescription? ParseDescriptIonLine(string line)
    {
        // Format: filename description
        // Or: "filename with spaces" description
        // The description can contain special chars at the end for attributes
        
        if (string.IsNullOrWhiteSpace(line))
            return null;
            
        string fileName;
        string description;
        
        if (line.StartsWith('"'))
        {
            // Quoted filename
            var endQuote = line.IndexOf('"', 1);
            if (endQuote < 0)
                return null;
                
            fileName = line[1..endQuote];
            description = line[(endQuote + 1)..].TrimStart();
        }
        else
        {
            // Unquoted filename - first whitespace separates
            var spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
            if (spaceIndex < 0)
            {
                fileName = line;
                description = string.Empty;
            }
            else
            {
                fileName = line[..spaceIndex];
                description = line[(spaceIndex + 1)..].TrimStart();
            }
        }
        
        // Remove trailing attribute char (usually Ctrl+D = 0x04)
        if (description.Length > 0 && description[^1] < 32)
        {
            description = description[..^1];
        }
        
        return new FileDescription
        {
            FileName = fileName,
            Description = description,
            OriginalLine = line
        };
    }

    private static async Task<IReadOnlyList<FileDescription>> ParseFilesBbsAsync(string filePath)
    {
        var results = new List<FileDescription>();
        
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
                    continue;
                    
                // Format: filename size date description
                var match = Regex.Match(line, @"^(\S+)\s+(\d+)\s+(\S+)\s+(.*)$");
                if (match.Success)
                {
                    results.Add(new FileDescription
                    {
                        FileName = match.Groups[1].Value,
                        Description = match.Groups[4].Value.Trim(),
                        OriginalLine = line
                    });
                }
                else
                {
                    // Simple format: filename description
                    var parsed = ParseDescriptIonLine(line);
                    if (parsed != null)
                    {
                        results.Add(parsed);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return results;
    }

    public async Task SetDescriptionsAsync(string directoryPath, IEnumerable<FileDescription> descriptions)
    {
        await WriteDescriptionsAsync(directoryPath, descriptions.ToList());
        DescriptionsChanged?.Invoke(this, directoryPath);
    }

    private static async Task WriteDescriptionsAsync(string directoryPath, List<FileDescription> descriptions)
    {
        var filePath = Path.Combine(directoryPath, DescriptIonFileName);
        
        if (descriptions.Count == 0)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return;
        }
        
        var lines = descriptions.Select(d =>
        {
            var fileName = d.FileName.Contains(' ') ? $"\"{d.FileName}\"" : d.FileName;
            return $"{fileName} {d.Description}";
        });
        
        await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);
        
        // Make the file hidden (like TC does)
        try
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
        }
        catch
        {
            // Ignore if we can't set attributes
        }
    }

    public Task<bool> HasDescriptionFileAsync(string directoryPath)
    {
        var hasFile = File.Exists(Path.Combine(directoryPath, DescriptIonFileName)) ||
                      File.Exists(Path.Combine(directoryPath, FilesBbsFileName)) ||
                      File.Exists(Path.Combine(directoryPath, IndexTxtFileName));
        return Task.FromResult(hasFile);
    }

    public string GetDescriptionFilePath(string directoryPath, DescriptionFileFormat format = DescriptionFileFormat.DescriptIon)
    {
        var fileName = format switch
        {
            DescriptionFileFormat.DescriptIon => DescriptIonFileName,
            DescriptionFileFormat.FilesBbs => FilesBbsFileName,
            DescriptionFileFormat.IndexTxt => IndexTxtFileName,
            _ => DescriptIonFileName
        };
        
        return Path.Combine(directoryPath, fileName);
    }

    public async Task CreateDescriptionFileAsync(string directoryPath, DescriptionFileFormat format = DescriptionFileFormat.DescriptIon)
    {
        var filePath = GetDescriptionFilePath(directoryPath, format);
        
        if (!File.Exists(filePath))
        {
            await File.WriteAllTextAsync(filePath, string.Empty);
            
            try
            {
                File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
            }
            catch
            {
                // Ignore
            }
        }
    }

    public async Task CopyDescriptionsAsync(string sourceDir, string destDir, IEnumerable<string> fileNames)
    {
        var sourceDescriptions = await GetDirectoryDescriptionsAsync(sourceDir);
        var destDescriptions = (await GetDirectoryDescriptionsAsync(destDir)).ToList();
        var fileNameSet = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        
        foreach (var desc in sourceDescriptions.Where(d => fileNameSet.Contains(d.FileName)))
        {
            var existingIndex = destDescriptions.FindIndex(d => 
                d.FileName.Equals(desc.FileName, StringComparison.OrdinalIgnoreCase));
                
            if (existingIndex >= 0)
            {
                destDescriptions[existingIndex] = desc;
            }
            else
            {
                destDescriptions.Add(desc);
            }
        }
        
        await WriteDescriptionsAsync(destDir, destDescriptions);
        DescriptionsChanged?.Invoke(this, destDir);
    }

    public async Task MoveDescriptionsAsync(string sourceDir, string destDir, IEnumerable<string> fileNames)
    {
        // Copy descriptions to destination
        await CopyDescriptionsAsync(sourceDir, destDir, fileNames);
        
        // Remove from source
        var fileNameSet = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        var sourceDescriptions = (await GetDirectoryDescriptionsAsync(sourceDir))
            .Where(d => !fileNameSet.Contains(d.FileName))
            .ToList();
            
        await WriteDescriptionsAsync(sourceDir, sourceDescriptions);
        DescriptionsChanged?.Invoke(this, sourceDir);
    }

    public async Task RenameDescriptionAsync(string directoryPath, string oldFileName, string newFileName)
    {
        var descriptions = (await GetDirectoryDescriptionsAsync(directoryPath)).ToList();
        var index = descriptions.FindIndex(d => 
            d.FileName.Equals(oldFileName, StringComparison.OrdinalIgnoreCase));
            
        if (index >= 0)
        {
            descriptions[index] = descriptions[index] with { FileName = newFileName };
            await WriteDescriptionsAsync(directoryPath, descriptions);
            DescriptionsChanged?.Invoke(this, directoryPath);
        }
    }

    public async Task<int> CleanupOrphanedDescriptionsAsync(string directoryPath)
    {
        var descriptions = (await GetDirectoryDescriptionsAsync(directoryPath)).ToList();
        var originalCount = descriptions.Count;
        
        descriptions = descriptions.Where(d =>
        {
            var fullPath = Path.Combine(directoryPath, d.FileName);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }).ToList();
        
        var removedCount = originalCount - descriptions.Count;
        
        if (removedCount > 0)
        {
            await WriteDescriptionsAsync(directoryPath, descriptions);
            DescriptionsChanged?.Invoke(this, directoryPath);
        }
        
        return removedCount;
    }

    public async Task ImportDescriptionsAsync(string sourceFile, string targetDirectory, DescriptionFileFormat sourceFormat)
    {
        IReadOnlyList<FileDescription> descriptions = sourceFormat switch
        {
            DescriptionFileFormat.DescriptIon => await ParseDescriptIonFileAsync(sourceFile),
            DescriptionFileFormat.FilesBbs => await ParseFilesBbsAsync(sourceFile),
            _ => await ParseDescriptIonFileAsync(sourceFile)
        };
        
        await SetDescriptionsAsync(targetDirectory, descriptions);
    }

    public async Task ExportDescriptionsAsync(string directoryPath, string targetFile, DescriptionFileFormat targetFormat)
    {
        var descriptions = await GetDirectoryDescriptionsAsync(directoryPath);
        
        var lines = targetFormat switch
        {
            DescriptionFileFormat.FilesBbs => descriptions.Select(d =>
            {
                var fullPath = Path.Combine(directoryPath, d.FileName);
                var size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                var date = File.Exists(fullPath) ? File.GetLastWriteTime(fullPath).ToString("MM-dd-yy") : "00-00-00";
                return $"{d.FileName,-12} {size,10} {date} {d.Description}";
            }),
            _ => descriptions.Select(d =>
            {
                var fileName = d.FileName.Contains(' ') ? $"\"{d.FileName}\"" : d.FileName;
                return $"{fileName} {d.Description}";
            })
        };
        
        await File.WriteAllLinesAsync(targetFile, lines, Encoding.UTF8);
    }
}
