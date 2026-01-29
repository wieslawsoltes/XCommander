using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XCommander.Services;

/// <summary>
/// Loads and resolves file associations from the user configuration.
/// </summary>
public sealed class FileAssociationService : IFileAssociationService
{
    private static readonly string AssociationsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander",
        "file_associations.json");

    private readonly List<FileAssociationEntry> _associations = new();
    private readonly string _associationsFilePath;
    private DateTime? _lastWriteTimeUtc;
    private bool _loaded;

    public FileAssociationService(string? associationsFilePath = null)
    {
        _associationsFilePath = associationsFilePath ?? AssociationsFilePath;
    }

    public string? GetOpenCommand(string filePath)
    {
        var assoc = GetAssociation(filePath);
        if (assoc == null || assoc.UseSystemDefault)
            return null;

        return string.IsNullOrWhiteSpace(assoc.OpenCommand) ? null : assoc.OpenCommand;
    }

    public string? GetViewerCommand(string filePath)
    {
        var assoc = GetAssociation(filePath);
        if (assoc == null || assoc.UseSystemDefault)
            return null;

        return string.IsNullOrWhiteSpace(assoc.ViewerCommand) ? null : assoc.ViewerCommand;
    }

    public string? GetEditorCommand(string filePath)
    {
        var assoc = GetAssociation(filePath);
        if (assoc == null || assoc.UseSystemDefault)
            return null;

        return string.IsNullOrWhiteSpace(assoc.EditorCommand) ? null : assoc.EditorCommand;
    }

    private FileAssociationEntry? GetAssociation(string filePath)
    {
        EnsureLoaded();

        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(ext))
            return null;

        return _associations.FirstOrDefault(a =>
            a.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureLoaded()
    {
        var fileInfo = new FileInfo(_associationsFilePath);
        if (fileInfo.Exists)
        {
            var lastWrite = fileInfo.LastWriteTimeUtc;
            if (!_loaded || _lastWriteTimeUtc != lastWrite)
            {
                LoadFromFile();
                _lastWriteTimeUtc = lastWrite;
                _loaded = true;
            }
            return;
        }

        if (!_loaded || _lastWriteTimeUtc.HasValue)
        {
            LoadDefaults();
            _lastWriteTimeUtc = null;
            _loaded = true;
        }
    }

    private void LoadFromFile()
    {
        try
        {
            var json = File.ReadAllText(_associationsFilePath);
            var associations = JsonSerializer.Deserialize<List<FileAssociationEntry>>(json);

            _associations.Clear();
            if (associations != null && associations.Count > 0)
            {
                _associations.AddRange(associations);
                return;
            }
        }
        catch
        {
            // Fall back to defaults when file is invalid.
        }

        LoadDefaults();
    }

    private void LoadDefaults()
    {
        _associations.Clear();
        _associations.AddRange(FileAssociationDefaults.Build());
    }

    internal static class FileAssociationDefaults
    {
        public static List<FileAssociationEntry> Build()
        {
            return new List<FileAssociationEntry>
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
                new() { Extension = ".gz", Description = "GZip Archive", OpenCommand = "internal:archive" }
            };
        }

        private static string GetDefaultCodeEditor()
        {
            if (OperatingSystem.IsWindows())
                return "code \"%s\"";
            if (OperatingSystem.IsMacOS())
                return "code \"%s\"";
            return "code \"%s\"";
        }
    }

    internal sealed class FileAssociationEntry
    {
        public string Extension { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ViewerCommand { get; set; } = string.Empty;
        public string EditorCommand { get; set; } = string.Empty;
        public string OpenCommand { get; set; } = string.Empty;
        public bool UseSystemDefault { get; set; }
    }
}
