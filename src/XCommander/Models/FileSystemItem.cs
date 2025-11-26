namespace XCommander.Models;

public enum FileSystemItemType
{
    File,
    Directory,
    Drive,
    ParentDirectory
}

public class FileSystemItem
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public FileSystemItemType ItemType { get; init; }
    public long Size { get; init; }
    public DateTime DateModified { get; init; }
    public DateTime DateCreated { get; init; }
    public string Extension { get; init; } = string.Empty;
    public FileAttributes Attributes { get; init; }
    public bool IsHidden => Attributes.HasFlag(FileAttributes.Hidden);
    public bool IsSystem => Attributes.HasFlag(FileAttributes.System);
    public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);
    
    public bool IsDirectory => ItemType == FileSystemItemType.Directory || 
                               ItemType == FileSystemItemType.Drive ||
                               ItemType == FileSystemItemType.ParentDirectory;
    
    public string DisplaySize => ItemType == FileSystemItemType.Directory || 
                                 ItemType == FileSystemItemType.ParentDirectory 
        ? "<DIR>" 
        : FormatFileSize(Size);
    
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
}
