namespace XCommander.Services;

/// <summary>
/// File attribute flags for editing
/// </summary>
[Flags]
public enum EditableFileAttributes
{
    None = 0,
    ReadOnly = 1,
    Hidden = 2,
    System = 4,
    Archive = 8
}

/// <summary>
/// Result of an attribute change operation
/// </summary>
public class AttributeChangeResult
{
    public string FilePath { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public FileAttributes? OldAttributes { get; init; }
    public FileAttributes? NewAttributes { get; init; }
}

/// <summary>
/// Timestamp type to modify
/// </summary>
public enum TimestampType
{
    Created,
    Modified,
    Accessed
}

/// <summary>
/// Service for editing file attributes and timestamps
/// </summary>
public interface IFileAttributeService
{
    /// <summary>
    /// Get file attributes
    /// </summary>
    FileAttributes GetAttributes(string path);
    
    /// <summary>
    /// Set file attributes
    /// </summary>
    AttributeChangeResult SetAttributes(string path, FileAttributes attributes);
    
    /// <summary>
    /// Add attributes to a file (OR operation)
    /// </summary>
    AttributeChangeResult AddAttributes(string path, FileAttributes attributesToAdd);
    
    /// <summary>
    /// Remove attributes from a file
    /// </summary>
    AttributeChangeResult RemoveAttributes(string path, FileAttributes attributesToRemove);
    
    /// <summary>
    /// Set specific editable attributes
    /// </summary>
    AttributeChangeResult SetEditableAttributes(string path, EditableFileAttributes attributes, bool value);
    
    /// <summary>
    /// Batch set attributes on multiple files
    /// </summary>
    IEnumerable<AttributeChangeResult> SetAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributes,
        IProgress<int>? progress = null);
    
    /// <summary>
    /// Batch add attributes to multiple files
    /// </summary>
    IEnumerable<AttributeChangeResult> AddAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributesToAdd,
        IProgress<int>? progress = null);
    
    /// <summary>
    /// Batch remove attributes from multiple files
    /// </summary>
    IEnumerable<AttributeChangeResult> RemoveAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributesToRemove,
        IProgress<int>? progress = null);
    
    /// <summary>
    /// Get file timestamp
    /// </summary>
    DateTime GetTimestamp(string path, TimestampType type);
    
    /// <summary>
    /// Set file timestamp
    /// </summary>
    bool SetTimestamp(string path, TimestampType type, DateTime timestamp);
    
    /// <summary>
    /// Set all timestamps at once
    /// </summary>
    bool SetTimestamps(string path, DateTime? created, DateTime? modified, DateTime? accessed);
    
    /// <summary>
    /// Touch file (update modified timestamp to current time)
    /// </summary>
    bool TouchFile(string path);
    
    /// <summary>
    /// Touch multiple files
    /// </summary>
    IEnumerable<(string Path, bool Success)> TouchFiles(IEnumerable<string> paths, IProgress<int>? progress = null);
    
    /// <summary>
    /// Copy timestamps from one file to another
    /// </summary>
    bool CopyTimestamps(string sourcePath, string targetPath, bool copyCreated = true, bool copyModified = true, bool copyAccessed = true);
    
    /// <summary>
    /// Copy attributes from one file to another
    /// </summary>
    AttributeChangeResult CopyAttributes(string sourcePath, string targetPath);
}

public class FileAttributeService : IFileAttributeService
{
    public FileAttributes GetAttributes(string path)
    {
        if (File.Exists(path))
            return File.GetAttributes(path);
        if (Directory.Exists(path))
            return new DirectoryInfo(path).Attributes;
        throw new FileNotFoundException("File or directory not found", path);
    }
    
    public AttributeChangeResult SetAttributes(string path, FileAttributes attributes)
    {
        try
        {
            var oldAttributes = GetAttributes(path);
            File.SetAttributes(path, attributes);
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = true,
                OldAttributes = oldAttributes,
                NewAttributes = attributes
            };
        }
        catch (Exception ex)
        {
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public AttributeChangeResult AddAttributes(string path, FileAttributes attributesToAdd)
    {
        try
        {
            var oldAttributes = GetAttributes(path);
            var newAttributes = oldAttributes | attributesToAdd;
            File.SetAttributes(path, newAttributes);
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = true,
                OldAttributes = oldAttributes,
                NewAttributes = newAttributes
            };
        }
        catch (Exception ex)
        {
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public AttributeChangeResult RemoveAttributes(string path, FileAttributes attributesToRemove)
    {
        try
        {
            var oldAttributes = GetAttributes(path);
            var newAttributes = oldAttributes & ~attributesToRemove;
            File.SetAttributes(path, newAttributes);
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = true,
                OldAttributes = oldAttributes,
                NewAttributes = newAttributes
            };
        }
        catch (Exception ex)
        {
            return new AttributeChangeResult
            {
                FilePath = path,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public AttributeChangeResult SetEditableAttributes(string path, EditableFileAttributes attributes, bool value)
    {
        var fileAttributes = FileAttributes.Normal;
        
        if (attributes.HasFlag(EditableFileAttributes.ReadOnly))
            fileAttributes |= FileAttributes.ReadOnly;
        if (attributes.HasFlag(EditableFileAttributes.Hidden))
            fileAttributes |= FileAttributes.Hidden;
        if (attributes.HasFlag(EditableFileAttributes.System))
            fileAttributes |= FileAttributes.System;
        if (attributes.HasFlag(EditableFileAttributes.Archive))
            fileAttributes |= FileAttributes.Archive;
        
        return value ? AddAttributes(path, fileAttributes) : RemoveAttributes(path, fileAttributes);
    }
    
    public IEnumerable<AttributeChangeResult> SetAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributes,
        IProgress<int>? progress = null)
    {
        var results = new List<AttributeChangeResult>();
        var count = 0;
        
        foreach (var path in paths)
        {
            results.Add(SetAttributes(path, attributes));
            count++;
            progress?.Report(count);
        }
        
        return results;
    }
    
    public IEnumerable<AttributeChangeResult> AddAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributesToAdd,
        IProgress<int>? progress = null)
    {
        var results = new List<AttributeChangeResult>();
        var count = 0;
        
        foreach (var path in paths)
        {
            results.Add(AddAttributes(path, attributesToAdd));
            count++;
            progress?.Report(count);
        }
        
        return results;
    }
    
    public IEnumerable<AttributeChangeResult> RemoveAttributesBatch(
        IEnumerable<string> paths, 
        FileAttributes attributesToRemove,
        IProgress<int>? progress = null)
    {
        var results = new List<AttributeChangeResult>();
        var count = 0;
        
        foreach (var path in paths)
        {
            results.Add(RemoveAttributes(path, attributesToRemove));
            count++;
            progress?.Report(count);
        }
        
        return results;
    }
    
    public DateTime GetTimestamp(string path, TimestampType type)
    {
        return type switch
        {
            TimestampType.Created => File.GetCreationTime(path),
            TimestampType.Modified => File.GetLastWriteTime(path),
            TimestampType.Accessed => File.GetLastAccessTime(path),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
    
    public bool SetTimestamp(string path, TimestampType type, DateTime timestamp)
    {
        try
        {
            switch (type)
            {
                case TimestampType.Created:
                    File.SetCreationTime(path, timestamp);
                    break;
                case TimestampType.Modified:
                    File.SetLastWriteTime(path, timestamp);
                    break;
                case TimestampType.Accessed:
                    File.SetLastAccessTime(path, timestamp);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public bool SetTimestamps(string path, DateTime? created, DateTime? modified, DateTime? accessed)
    {
        try
        {
            if (created.HasValue)
                File.SetCreationTime(path, created.Value);
            if (modified.HasValue)
                File.SetLastWriteTime(path, modified.Value);
            if (accessed.HasValue)
                File.SetLastAccessTime(path, accessed.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public bool TouchFile(string path)
    {
        return SetTimestamp(path, TimestampType.Modified, DateTime.Now);
    }
    
    public IEnumerable<(string Path, bool Success)> TouchFiles(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        var results = new List<(string, bool)>();
        var count = 0;
        
        foreach (var path in paths)
        {
            results.Add((path, TouchFile(path)));
            count++;
            progress?.Report(count);
        }
        
        return results;
    }
    
    public bool CopyTimestamps(string sourcePath, string targetPath, bool copyCreated = true, bool copyModified = true, bool copyAccessed = true)
    {
        try
        {
            if (copyCreated)
                File.SetCreationTime(targetPath, File.GetCreationTime(sourcePath));
            if (copyModified)
                File.SetLastWriteTime(targetPath, File.GetLastWriteTime(sourcePath));
            if (copyAccessed)
                File.SetLastAccessTime(targetPath, File.GetLastAccessTime(sourcePath));
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public AttributeChangeResult CopyAttributes(string sourcePath, string targetPath)
    {
        try
        {
            var sourceAttributes = GetAttributes(sourcePath);
            return SetAttributes(targetPath, sourceAttributes);
        }
        catch (Exception ex)
        {
            return new AttributeChangeResult
            {
                FilePath = targetPath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
