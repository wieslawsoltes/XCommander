using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

/// <summary>
/// Represents a file association (extension to application mapping).
/// </summary>
public partial class FileAssociation : ObservableObject
{
    [ObservableProperty]
    private string _extension = string.Empty;
    
    [ObservableProperty]
    private string _description = string.Empty;
    
    [ObservableProperty]
    private string _viewerCommand = string.Empty;
    
    [ObservableProperty]
    private string _editorCommand = string.Empty;
    
    [ObservableProperty]
    private string _openCommand = string.Empty;
    
    [ObservableProperty]
    private bool _useSystemDefault;
}

/// <summary>
/// Manages file associations for custom viewers and editors.
/// </summary>
public partial class FileAssociationManager : ObservableObject
{
    private static readonly string AssociationsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "file_associations.json");
    
    public ObservableCollection<FileAssociation> Associations { get; } = [];
    
    [ObservableProperty]
    private FileAssociation? _selectedAssociation;
    
    [ObservableProperty]
    private string _newExtension = string.Empty;
    
    public FileAssociationManager()
    {
        LoadAssociations();
        
        // Add defaults if empty
        if (Associations.Count == 0)
        {
            InitializeDefaults();
        }
    }
    
    private void InitializeDefaults()
    {
        var defaultAssociations = new List<FileAssociation>
        {
            // Text files
            new() { Extension = ".txt", Description = "Text File", UseSystemDefault = true },
            new() { Extension = ".log", Description = "Log File", UseSystemDefault = true },
            new() { Extension = ".md", Description = "Markdown", UseSystemDefault = true },
            
            // Code files
            new() { Extension = ".cs", Description = "C# Source", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".js", Description = "JavaScript", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".ts", Description = "TypeScript", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".py", Description = "Python", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".json", Description = "JSON", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".xml", Description = "XML", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".html", Description = "HTML", EditorCommand = GetDefaultCodeEditor() },
            new() { Extension = ".css", Description = "CSS", EditorCommand = GetDefaultCodeEditor() },
            
            // Images
            new() { Extension = ".png", Description = "PNG Image", UseSystemDefault = true },
            new() { Extension = ".jpg", Description = "JPEG Image", UseSystemDefault = true },
            new() { Extension = ".jpeg", Description = "JPEG Image", UseSystemDefault = true },
            new() { Extension = ".gif", Description = "GIF Image", UseSystemDefault = true },
            new() { Extension = ".bmp", Description = "Bitmap Image", UseSystemDefault = true },
            new() { Extension = ".svg", Description = "SVG Image", UseSystemDefault = true },
            
            // Documents
            new() { Extension = ".pdf", Description = "PDF Document", UseSystemDefault = true },
            new() { Extension = ".docx", Description = "Word Document", UseSystemDefault = true },
            new() { Extension = ".xlsx", Description = "Excel Spreadsheet", UseSystemDefault = true },
            new() { Extension = ".pptx", Description = "PowerPoint Presentation", UseSystemDefault = true },
            
            // Media
            new() { Extension = ".mp3", Description = "MP3 Audio", UseSystemDefault = true },
            new() { Extension = ".mp4", Description = "MP4 Video", UseSystemDefault = true },
            new() { Extension = ".avi", Description = "AVI Video", UseSystemDefault = true },
            new() { Extension = ".mkv", Description = "MKV Video", UseSystemDefault = true },
            
            // Archives
            new() { Extension = ".zip", Description = "ZIP Archive", OpenCommand = "internal:archive" },
            new() { Extension = ".7z", Description = "7-Zip Archive", OpenCommand = "internal:archive" },
            new() { Extension = ".rar", Description = "RAR Archive", OpenCommand = "internal:archive" },
            new() { Extension = ".tar", Description = "TAR Archive", OpenCommand = "internal:archive" },
            new() { Extension = ".gz", Description = "GZip Archive", OpenCommand = "internal:archive" },
        };
        
        foreach (var assoc in defaultAssociations)
        {
            Associations.Add(assoc);
        }
    }
    
    private static string GetDefaultCodeEditor()
    {
        if (OperatingSystem.IsWindows())
            return "code \"%s\""; // VS Code
        if (OperatingSystem.IsMacOS())
            return "code \"%s\"";
        return "code \"%s\"";
    }
    
    public void LoadAssociations()
    {
        try
        {
            if (File.Exists(AssociationsFilePath))
            {
                var json = File.ReadAllText(AssociationsFilePath);
                var associations = JsonSerializer.Deserialize<List<FileAssociation>>(json);
                
                if (associations != null)
                {
                    Associations.Clear();
                    foreach (var assoc in associations)
                    {
                        Associations.Add(assoc);
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors, use defaults
        }
    }
    
    [RelayCommand]
    public void SaveAssociations()
    {
        try
        {
            var directory = Path.GetDirectoryName(AssociationsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(Associations.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AssociationsFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    [RelayCommand]
    public void AddAssociation()
    {
        if (string.IsNullOrWhiteSpace(NewExtension)) return;
        
        var ext = NewExtension.StartsWith(".") ? NewExtension : "." + NewExtension;
        
        // Check if already exists
        if (Associations.Any(a => a.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            return;
        
        Associations.Add(new FileAssociation
        {
            Extension = ext.ToLowerInvariant(),
            Description = $"{ext.TrimStart('.').ToUpperInvariant()} File",
            UseSystemDefault = true
        });
        
        NewExtension = string.Empty;
    }
    
    [RelayCommand]
    public void RemoveAssociation(FileAssociation? association)
    {
        if (association != null)
        {
            Associations.Remove(association);
        }
    }
    
    [RelayCommand]
    public void ResetToDefaults()
    {
        Associations.Clear();
        InitializeDefaults();
    }
    
    /// <summary>
    /// Gets the association for a given file extension.
    /// </summary>
    public FileAssociation? GetAssociation(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        
        var ext = extension.StartsWith(".") ? extension : "." + extension;
        return Associations.FirstOrDefault(a => a.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Gets the command to view a file.
    /// </summary>
    public string? GetViewerCommand(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        var assoc = GetAssociation(ext);
        
        if (assoc == null || assoc.UseSystemDefault)
            return null;
        
        return !string.IsNullOrEmpty(assoc.ViewerCommand)
            ? assoc.ViewerCommand.Replace("%s", filePath)
            : null;
    }
    
    /// <summary>
    /// Gets the command to edit a file.
    /// </summary>
    public string? GetEditorCommand(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        var assoc = GetAssociation(ext);
        
        if (assoc == null || assoc.UseSystemDefault)
            return null;
        
        return !string.IsNullOrEmpty(assoc.EditorCommand)
            ? assoc.EditorCommand.Replace("%s", filePath)
            : null;
    }
    
    /// <summary>
    /// Gets the command to open a file (for double-click actions).
    /// </summary>
    public string? GetOpenCommand(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        var assoc = GetAssociation(ext);
        
        if (assoc == null || assoc.UseSystemDefault)
            return null;
        
        return !string.IsNullOrEmpty(assoc.OpenCommand)
            ? assoc.OpenCommand.Replace("%s", filePath)
            : null;
    }
    
    /// <summary>
    /// Checks if a file should be handled internally (e.g., archives).
    /// </summary>
    public bool IsInternalHandler(string filePath)
    {
        var command = GetOpenCommand(filePath);
        return command?.StartsWith("internal:") ?? false;
    }
}
