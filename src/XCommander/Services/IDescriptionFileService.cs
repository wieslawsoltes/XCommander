using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// A file description entry (from descript.ion file)
/// </summary>
public record FileDescription
{
    /// <summary>File name (not full path)</summary>
    public string FileName { get; init; } = string.Empty;
    
    /// <summary>Description/comment text</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>Original line from descript.ion file</summary>
    public string? OriginalLine { get; init; }
}

/// <summary>
/// Description file format
/// </summary>
public enum DescriptionFileFormat
{
    /// <summary>Standard descript.ion format (default for TC)</summary>
    DescriptIon,
    
    /// <summary>files.bbs format (BBS-style)</summary>
    FilesBbs,
    
    /// <summary>00index.txt format</summary>
    IndexTxt,
    
    /// <summary>Custom format</summary>
    Custom
}

/// <summary>
/// Service for reading/writing file description files (descript.ion).
/// TC equivalent: Comments view, descript.ion files
/// </summary>
public interface IDescriptionFileService
{
    /// <summary>
    /// Get description for a specific file
    /// </summary>
    Task<string?> GetDescriptionAsync(string filePath);
    
    /// <summary>
    /// Set description for a specific file
    /// </summary>
    Task SetDescriptionAsync(string filePath, string description);
    
    /// <summary>
    /// Remove description for a file
    /// </summary>
    Task RemoveDescriptionAsync(string filePath);
    
    /// <summary>
    /// Get all descriptions in a directory
    /// </summary>
    Task<IReadOnlyList<FileDescription>> GetDirectoryDescriptionsAsync(string directoryPath);
    
    /// <summary>
    /// Set multiple descriptions at once
    /// </summary>
    Task SetDescriptionsAsync(string directoryPath, IEnumerable<FileDescription> descriptions);
    
    /// <summary>
    /// Check if description file exists in directory
    /// </summary>
    Task<bool> HasDescriptionFileAsync(string directoryPath);
    
    /// <summary>
    /// Get the description file path for a directory
    /// </summary>
    string GetDescriptionFilePath(string directoryPath, DescriptionFileFormat format = DescriptionFileFormat.DescriptIon);
    
    /// <summary>
    /// Create empty description file
    /// </summary>
    Task CreateDescriptionFileAsync(string directoryPath, DescriptionFileFormat format = DescriptionFileFormat.DescriptIon);
    
    /// <summary>
    /// Copy descriptions when copying files
    /// </summary>
    Task CopyDescriptionsAsync(string sourceDir, string destDir, IEnumerable<string> fileNames);
    
    /// <summary>
    /// Move descriptions when moving files
    /// </summary>
    Task MoveDescriptionsAsync(string sourceDir, string destDir, IEnumerable<string> fileNames);
    
    /// <summary>
    /// Rename description entry when file is renamed
    /// </summary>
    Task RenameDescriptionAsync(string directoryPath, string oldFileName, string newFileName);
    
    /// <summary>
    /// Cleanup orphaned descriptions (files that no longer exist)
    /// </summary>
    Task<int> CleanupOrphanedDescriptionsAsync(string directoryPath);
    
    /// <summary>
    /// Import descriptions from another format
    /// </summary>
    Task ImportDescriptionsAsync(string sourceFile, string targetDirectory, DescriptionFileFormat sourceFormat);
    
    /// <summary>
    /// Export descriptions to another format
    /// </summary>
    Task ExportDescriptionsAsync(string directoryPath, string targetFile, DescriptionFileFormat targetFormat);
    
    /// <summary>
    /// Event raised when descriptions are modified
    /// </summary>
    event EventHandler<string>? DescriptionsChanged;
}
