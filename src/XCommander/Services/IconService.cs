using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for managing file and folder icons with advanced features.
/// Provides native system icons, overlays, custom assignments, and SVG support.
/// </summary>
public class IconService : IIconService
{
    private readonly ConcurrentDictionary<string, IconInfo> _cache = new();
    private readonly Dictionary<string, CustomIconAssignment> _customIcons = new();
    private readonly List<IconPack> _iconPacks = new();
    private string? _activePackId;
    private readonly string _configPath;
    private readonly object _lock = new();
    
    // Default extension to category mappings
    private static readonly Dictionary<string, FileTypeCategory> ExtensionCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        { ".doc", FileTypeCategory.Document }, { ".docx", FileTypeCategory.Document },
        { ".txt", FileTypeCategory.Document }, { ".rtf", FileTypeCategory.Document },
        { ".odt", FileTypeCategory.Document }, { ".md", FileTypeCategory.Document },
        
        // Spreadsheets
        { ".xls", FileTypeCategory.Spreadsheet }, { ".xlsx", FileTypeCategory.Spreadsheet },
        { ".csv", FileTypeCategory.Spreadsheet }, { ".ods", FileTypeCategory.Spreadsheet },
        
        // Presentations
        { ".ppt", FileTypeCategory.Presentation }, { ".pptx", FileTypeCategory.Presentation },
        { ".odp", FileTypeCategory.Presentation },
        
        // Images
        { ".jpg", FileTypeCategory.Image }, { ".jpeg", FileTypeCategory.Image },
        { ".png", FileTypeCategory.Image }, { ".gif", FileTypeCategory.Image },
        { ".bmp", FileTypeCategory.Image }, { ".svg", FileTypeCategory.Image },
        { ".ico", FileTypeCategory.Image }, { ".webp", FileTypeCategory.Image },
        { ".tiff", FileTypeCategory.Image }, { ".psd", FileTypeCategory.Image },
        
        // Videos
        { ".mp4", FileTypeCategory.Video }, { ".avi", FileTypeCategory.Video },
        { ".mkv", FileTypeCategory.Video }, { ".mov", FileTypeCategory.Video },
        { ".wmv", FileTypeCategory.Video }, { ".flv", FileTypeCategory.Video },
        { ".webm", FileTypeCategory.Video },
        
        // Audio
        { ".mp3", FileTypeCategory.Audio }, { ".wav", FileTypeCategory.Audio },
        { ".flac", FileTypeCategory.Audio }, { ".aac", FileTypeCategory.Audio },
        { ".ogg", FileTypeCategory.Audio }, { ".wma", FileTypeCategory.Audio },
        { ".m4a", FileTypeCategory.Audio },
        
        // Archives
        { ".zip", FileTypeCategory.Archive }, { ".rar", FileTypeCategory.Archive },
        { ".7z", FileTypeCategory.Archive }, { ".tar", FileTypeCategory.Archive },
        { ".gz", FileTypeCategory.Archive }, { ".bz2", FileTypeCategory.Archive },
        { ".xz", FileTypeCategory.Archive },
        
        // Executables
        { ".exe", FileTypeCategory.Executable }, { ".msi", FileTypeCategory.Executable },
        { ".app", FileTypeCategory.Executable }, { ".dmg", FileTypeCategory.Executable },
        { ".deb", FileTypeCategory.Executable }, { ".rpm", FileTypeCategory.Executable },
        
        // Scripts
        { ".bat", FileTypeCategory.Script }, { ".cmd", FileTypeCategory.Script },
        { ".sh", FileTypeCategory.Script }, { ".ps1", FileTypeCategory.Script },
        { ".py", FileTypeCategory.Script }, { ".rb", FileTypeCategory.Script },
        
        // Code
        { ".cs", FileTypeCategory.Code }, { ".java", FileTypeCategory.Code },
        { ".cpp", FileTypeCategory.Code }, { ".c", FileTypeCategory.Code },
        { ".h", FileTypeCategory.Code }, { ".js", FileTypeCategory.Code },
        { ".ts", FileTypeCategory.Code }, { ".html", FileTypeCategory.Code },
        { ".css", FileTypeCategory.Code }, { ".json", FileTypeCategory.Code },
        { ".xml", FileTypeCategory.Code }, { ".yaml", FileTypeCategory.Code },
        
        // Database
        { ".db", FileTypeCategory.Database }, { ".sqlite", FileTypeCategory.Database },
        { ".mdb", FileTypeCategory.Database }, { ".sql", FileTypeCategory.Database },
        
        // Fonts
        { ".ttf", FileTypeCategory.Font }, { ".otf", FileTypeCategory.Font },
        { ".woff", FileTypeCategory.Font }, { ".woff2", FileTypeCategory.Font },
        
        // PDF
        { ".pdf", FileTypeCategory.Pdf },
        
        // Config
        { ".ini", FileTypeCategory.Configuration }, { ".cfg", FileTypeCategory.Configuration },
        { ".config", FileTypeCategory.Configuration }, { ".env", FileTypeCategory.Configuration },
        
        // Links
        { ".lnk", FileTypeCategory.Shortcut }, { ".url", FileTypeCategory.Link }
    };
    
    // Default SVG icons for file types
    private static readonly Dictionary<FileTypeCategory, string> DefaultSvgIcons = new()
    {
        { FileTypeCategory.File, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#6B7280\" d=\"M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z\"/></svg>" },
        { FileTypeCategory.Folder, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#FCD34D\" d=\"M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z\"/></svg>" },
        { FileTypeCategory.FolderOpen, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#FCD34D\" d=\"M19,20H4C2.89,20 2,19.1 2,18V6C2,4.89 2.89,4 4,4H10L12,6H19A2,2 0 0,1 21,8H21L4,8V18L6.14,10H23.21L20.93,18.5C20.7,19.37 19.92,20 19,20Z\"/></svg>" },
        { FileTypeCategory.Document, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#3B82F6\" d=\"M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20M9,13V15H18V13H9M9,17V19H15V17H9Z\"/></svg>" },
        { FileTypeCategory.Spreadsheet, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#22C55E\" d=\"M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20M10,13H7V11H10V13M14,13H11V11H14V13M10,16H7V14H10V16M14,16H11V14H14V16Z\"/></svg>" },
        { FileTypeCategory.Image, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#A855F7\" d=\"M8.5,13.5L11,16.5L14.5,12L19,18H5M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19Z\"/></svg>" },
        { FileTypeCategory.Video, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#EF4444\" d=\"M17,10.5V7A1,1 0 0,0 16,6H4A1,1 0 0,0 3,7V17A1,1 0 0,0 4,18H16A1,1 0 0,0 17,17V13.5L21,17.5V6.5L17,10.5Z\"/></svg>" },
        { FileTypeCategory.Audio, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#F97316\" d=\"M12,3V13.55C11.41,13.21 10.73,13 10,13A4,4 0 0,0 6,17A4,4 0 0,0 10,21A4,4 0 0,0 14,17V7H18V3H12Z\"/></svg>" },
        { FileTypeCategory.Archive, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#78716C\" d=\"M3,3H21V7H3V3M4,8H20V21H4V8M9.5,11A0.5,0.5 0 0,0 9,11.5V13H15V11.5A0.5,0.5 0 0,0 14.5,11H9.5Z\"/></svg>" },
        { FileTypeCategory.Executable, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#14B8A6\" d=\"M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8M12,10A2,2 0 0,0 10,12A2,2 0 0,0 12,14A2,2 0 0,0 14,12A2,2 0 0,0 12,10M10,22C9.75,22 9.54,21.82 9.5,21.58L9.13,18.93C8.5,18.68 7.96,18.34 7.44,17.94L4.95,18.95C4.73,19.03 4.46,18.95 4.34,18.73L2.34,15.27C2.21,15.05 2.27,14.78 2.46,14.63L4.57,12.97L4.5,12L4.57,11L2.46,9.37C2.27,9.22 2.21,8.95 2.34,8.73L4.34,5.27C4.46,5.05 4.73,4.96 4.95,5.05L7.44,6.05C7.96,5.66 8.5,5.32 9.13,5.07L9.5,2.42C9.54,2.18 9.75,2 10,2H14C14.25,2 14.46,2.18 14.5,2.42L14.87,5.07C15.5,5.32 16.04,5.66 16.56,6.05L19.05,5.05C19.27,4.96 19.54,5.05 19.66,5.27L21.66,8.73C21.79,8.95 21.73,9.22 21.54,9.37L19.43,11L19.5,12L19.43,13L21.54,14.63C21.73,14.78 21.79,15.05 21.66,15.27L19.66,18.73C19.54,18.95 19.27,19.04 19.05,18.95L16.56,17.95C16.04,18.34 15.5,18.68 14.87,18.93L14.5,21.58C14.46,21.82 14.25,22 14,22H10Z\"/></svg>" },
        { FileTypeCategory.Code, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#6366F1\" d=\"M8,3A2,2 0 0,0 6,5V9A2,2 0 0,1 4,11H3V13H4A2,2 0 0,1 6,15V19A2,2 0 0,0 8,21H10V19H8V14A2,2 0 0,0 6,12A2,2 0 0,0 8,10V5H10V3M16,3A2,2 0 0,1 18,5V9A2,2 0 0,0 20,11H21V13H20A2,2 0 0,0 18,15V19A2,2 0 0,1 16,21H14V19H16V14A2,2 0 0,1 18,12A2,2 0 0,1 16,10V5H14V3H16Z\"/></svg>" },
        { FileTypeCategory.Pdf, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#DC2626\" d=\"M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M13,9V3.5L18.5,9H13M10.5,11.5C10.5,10.7 11.1,10 12,10C12.9,10 13.5,10.7 13.5,11.5C13.5,12.3 12.9,13 12,13C11.1,13 10.5,12.3 10.5,11.5M18,19H6V17L10,13L11.5,14.5L14.5,11.5L18,15V19Z\"/></svg>" },
        { FileTypeCategory.Database, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#0EA5E9\" d=\"M12,3C7.58,3 4,4.79 4,7C4,9.21 7.58,11 12,11C16.42,11 20,9.21 20,7C20,4.79 16.42,3 12,3M4,9V12C4,14.21 7.58,16 12,16C16.42,16 20,14.21 20,12V9C20,11.21 16.42,13 12,13C7.58,13 4,11.21 4,9M4,14V17C4,19.21 7.58,21 12,21C16.42,21 20,19.21 20,17V14C20,16.21 16.42,18 12,18C7.58,18 4,16.21 4,14Z\"/></svg>" },
        { FileTypeCategory.Font, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#EC4899\" d=\"M9.93,13.5H14.07L12,7.18L9.93,13.5M20,2H4A2,2 0 0,0 2,4V20A2,2 0 0,0 4,22H20A2,2 0 0,0 22,20V4A2,2 0 0,0 20,2M15.72,17.5L14.5,14H9.5L8.28,17.5H6.5L11.13,5H12.88L17.5,17.5H15.72Z\"/></svg>" },
        { FileTypeCategory.Drive, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#64748B\" d=\"M6,2H18A2,2 0 0,1 20,4V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2M12,4A6,6 0 0,0 6,10C6,13.31 8.69,16 12,16A6,6 0 0,0 18,10A6,6 0 0,0 12,4M8,19A1,1 0 0,0 7,20A1,1 0 0,0 8,21A1,1 0 0,0 9,20A1,1 0 0,0 8,19M12,6A4,4 0 0,1 16,10A4,4 0 0,1 12,14A4,4 0 0,1 8,10A4,4 0 0,1 12,6Z\"/></svg>" },
        { FileTypeCategory.Network, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#2563EB\" d=\"M17,3A2,2 0 0,1 19,5V15A2,2 0 0,1 17,17H13V19H14A1,1 0 0,1 15,20H22V22H15A1,1 0 0,1 14,23H10A1,1 0 0,1 9,22H2V20H9A1,1 0 0,1 10,19H11V17H7A2,2 0 0,1 5,15V5A2,2 0 0,1 7,3H17Z\"/></svg>" },
        { FileTypeCategory.Cloud, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path fill=\"#0EA5E9\" d=\"M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04Z\"/></svg>" }
    };
    
    // Default overlay SVGs
    private static readonly Dictionary<OverlayType, string> DefaultOverlaySvgs = new()
    {
        { OverlayType.Link, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#3B82F6\" d=\"M8,3A5,5 0 0,1 13,8A5,5 0 0,1 8,13A5,5 0 0,1 3,8A5,5 0 0,1 8,3M11,7H9V5L5,8L9,11V9H11V7Z\"/></svg>" },
        { OverlayType.Lock, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#EF4444\" d=\"M8,11A1,1 0 0,1 7,10A1,1 0 0,1 8,9A1,1 0 0,1 9,10A1,1 0 0,1 8,11M11.5,6H11V5A3,3 0 0,0 8,2A3,3 0 0,0 5,5V6H4.5C3.67,6 3,6.67 3,7.5V12.5C3,13.33 3.67,14 4.5,14H11.5C12.33,14 13,13.33 13,12.5V7.5C13,6.67 12.33,6 11.5,6M6,5A2,2 0 0,1 8,3A2,2 0 0,1 10,5V6H6V5Z\"/></svg>" },
        { OverlayType.Sync, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#22C55E\" d=\"M8,3A5,5 0 0,1 13,8A5,5 0 0,1 8,13A5,5 0 0,1 3,8A5,5 0 0,1 8,3M4.5,8A3.5,3.5 0 0,0 8,11.5A3.5,3.5 0 0,0 11.5,8H9V6L13,9L9,12V10H11A3,3 0 0,1 8,13A3,3 0 0,1 5,10H4A4,4 0 0,0 8,14A4,4 0 0,0 12,10H11.5A3.5,3.5 0 0,0 8,6.5A3.5,3.5 0 0,0 4.5,10H5A3,3 0 0,1 8,7A3,3 0 0,1 11,10H12A4,4 0 0,0 8,6A4,4 0 0,0 4,10H4.5A3.5,3.5 0 0,0 8,6.5\"/></svg>" },
        { OverlayType.SyncError, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#EF4444\" d=\"M8,3A5,5 0 0,1 13,8A5,5 0 0,1 8,13A5,5 0 0,1 3,8A5,5 0 0,1 8,3M7.25,5V10H8.75V5H7.25M7.25,11V12.5H8.75V11H7.25Z\"/></svg>" },
        { OverlayType.CloudOnly, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#0EA5E9\" d=\"M11.5,7C11.22,5.28 9.77,4 8,4C6.55,4 5.28,4.88 4.7,6.13C3.18,6.33 2,7.64 2,9.25C2,11 3.5,12.5 5.25,12.5H11.25C12.77,12.5 14,11.27 14,9.75C14,8.32 12.91,7.15 11.5,7Z\"/></svg>" },
        { OverlayType.Modified, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#F97316\" d=\"M8,3A5,5 0 0,1 13,8A5,5 0 0,1 8,13A5,5 0 0,1 3,8A5,5 0 0,1 8,3M6,6V10.5L9.5,12.5L10.25,11.25L7.5,9.75V6H6Z\"/></svg>" },
        { OverlayType.ReadOnly, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#6B7280\" d=\"M8,3A5,5 0 0,1 13,8A5,5 0 0,1 8,13A5,5 0 0,1 3,8A5,5 0 0,1 8,3M8,5A3,3 0 0,0 5,8A3,3 0 0,0 8,11A3,3 0 0,0 11,8A3,3 0 0,0 8,5M8,6.5A1.5,1.5 0 0,1 9.5,8A1.5,1.5 0 0,1 8,9.5A1.5,1.5 0 0,1 6.5,8A1.5,1.5 0 0,1 8,6.5Z\"/></svg>" },
        { OverlayType.Hidden, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"6\" fill=\"white\"/><path fill=\"#9CA3AF\" d=\"M3.27,5L2,6.27L5.18,9.45C5.07,9.78 5,10.13 5,10.5C5,12.43 6.57,14 8.5,14C8.87,14 9.22,13.93 9.55,13.82L11.73,16L13,14.73L3.27,5M8.5,12.5A2,2 0 0,1 6.5,10.5C6.5,10.34 6.53,10.19 6.57,10.04L8.96,12.43C8.81,12.47 8.66,12.5 8.5,12.5M14.97,9.45L13.55,8.03C13.81,8.49 14,9 14,9.5C14,10.33 13.33,11 12.5,11C12,11 11.53,10.79 11.21,10.45L9.79,9.03C10.13,8.71 10.5,8.5 11,8.5C11.17,8.5 11.33,8.53 11.47,8.57L13.57,6.47C13.22,6.31 12.87,6.16 12.5,6.07C12.5,6.07 12.5,6.04 12.5,6C12.5,4.62 11.38,3.5 10,3.5C9.63,3.5 9.28,3.59 8.97,3.74L7.55,2.32C8.09,2.12 8.68,2 9.5,2C11.43,2 13,3.57 13,5.5C13,5.82 12.95,6.12 12.88,6.41C14.27,6.95 15.31,8.08 14.97,9.45Z\"/></svg>" }
    };
    
    public event EventHandler? IconsChanged;
    
    public IconService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "custom-icons.json");
        LoadCustomIcons();
        RegisterDefaultIconPack();
    }
    
    public async Task<IconInfo> GetIconAsync(string path, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default)
    {
        // Check for custom icon first
        var customIcon = GetCustomIcon(path);
        if (customIcon != null)
        {
            return await LoadCustomIconAsync(customIcon, size, cancellationToken);
        }
        
        // Determine category
        FileTypeCategory category;
        if (Directory.Exists(path))
        {
            category = FileTypeCategory.Folder;
        }
        else
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            category = GetCategoryForExtension(extension);
        }
        
        // Check cache
        var cacheKey = $"{category}_{size}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        
        // Get icon from active pack or default
        var icon = await GetFileTypeIconAsync(category, size, cancellationToken);
        _cache.TryAdd(cacheKey, icon);
        return icon;
    }
    
    public async Task<IconInfo> GetSystemIconAsync(string extension, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default)
    {
        var category = GetCategoryForExtension(extension);
        return await GetFileTypeIconAsync(category, size, cancellationToken);
    }
    
    public Task<IconInfo> GetFileTypeIconAsync(FileTypeCategory category, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default)
    {
        // Check active icon pack
        var pack = GetActiveIconPack();
        if (pack != null && pack.CategoryMappings.TryGetValue(category, out var iconPath))
        {
            var fullPath = Path.Combine(pack.BasePath, iconPath);
            if (File.Exists(fullPath))
            {
                if (pack.IsSvgBased)
                {
                    return LoadSvgIconAsync(fullPath, size, cancellationToken);
                }
                // Load raster icon
                return Task.FromResult(new IconInfo
                {
                    ImageData = File.ReadAllBytes(fullPath),
                    Width = (int)size,
                    Height = (int)size,
                    Format = IconFormat.Png,
                    Category = category,
                    IsCached = false
                });
            }
        }
        
        // Fall back to default SVG icons
        if (DefaultSvgIcons.TryGetValue(category, out var svgContent))
        {
            return Task.FromResult(new IconInfo
            {
                SvgContent = svgContent,
                Width = (int)size,
                Height = (int)size,
                Format = IconFormat.Svg,
                Category = category,
                IsCached = false
            });
        }
        
        // Ultimate fallback - generic file icon
        return Task.FromResult(new IconInfo
        {
            SvgContent = DefaultSvgIcons[FileTypeCategory.File],
            Width = (int)size,
            Height = (int)size,
            Format = IconFormat.Svg,
            Category = FileTypeCategory.File,
            IsCached = false
        });
    }
    
    public Task<IReadOnlyList<IconOverlay>> GetOverlaysAsync(string path,
        CancellationToken cancellationToken = default)
    {
        var overlays = new List<IconOverlay>();
        
        try
        {
            var attributes = File.GetAttributes(path);
            
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                overlays.Add(CreateOverlay(OverlayType.ReadOnly, 10));
            }
            
            if ((attributes & FileAttributes.Hidden) != 0)
            {
                overlays.Add(CreateOverlay(OverlayType.Hidden, 20));
            }
            
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                overlays.Add(CreateOverlay(OverlayType.Link, 5));
            }
            
            if ((attributes & FileAttributes.Encrypted) != 0)
            {
                overlays.Add(CreateOverlay(OverlayType.Lock, 1));
            }
        }
        catch
        {
            // Ignore access errors
        }
        
        return Task.FromResult<IReadOnlyList<IconOverlay>>(overlays);
    }
    
    public Task<IconInfo> ApplyOverlaysAsync(IconInfo baseIcon, IEnumerable<IconOverlay> overlays,
        CancellationToken cancellationToken = default)
    {
        // For SVG icons, we can compose them
        if (baseIcon.Format == IconFormat.Svg && baseIcon.SvgContent != null)
        {
            var svgContent = baseIcon.SvgContent;
            var overlayList = overlays.OrderBy(o => o.Priority).ToList();
            
            if (overlayList.Count > 0)
            {
                // Simple composition - embed overlay SVGs
                // In a real implementation, would use proper SVG composition
                var overlay = overlayList.First();
                if (overlay.SvgContent != null)
                {
                    // This is simplified - real implementation would properly compose SVGs
                    svgContent = baseIcon.SvgContent;
                }
            }
            
            return Task.FromResult(baseIcon with { SvgContent = svgContent });
        }
        
        return Task.FromResult(baseIcon);
    }
    
    public CustomIconAssignment? GetCustomIcon(string path)
    {
        lock (_lock)
        {
            if (_customIcons.TryGetValue(path, out var assignment))
                return assignment;
            
            // Check parent folders with ApplyToSubfolders
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                if (_customIcons.TryGetValue(dir, out var parentAssignment) && parentAssignment.ApplyToSubfolders)
                    return parentAssignment;
                dir = Path.GetDirectoryName(dir);
            }
        }
        
        return null;
    }
    
    public async Task SetCustomIconAsync(string path, CustomIconAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _customIcons[path] = assignment with { Path = path };
        }
        
        await SaveCustomIconsAsync();
        InvalidateCacheForPath(path);
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public async Task RemoveCustomIconAsync(string path, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _customIcons.Remove(path);
        }
        
        await SaveCustomIconsAsync();
        InvalidateCacheForPath(path);
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public IReadOnlyDictionary<string, CustomIconAssignment> GetAllCustomIcons()
    {
        lock (_lock)
        {
            return new Dictionary<string, CustomIconAssignment>(_customIcons);
        }
    }
    
    public async Task<IconInfo> LoadSvgIconAsync(string svgPath, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(svgPath, cancellationToken);
        return await LoadSvgFromContentAsync(content, size, cancellationToken);
    }
    
    public Task<IconInfo> LoadSvgFromContentAsync(string svgContent, IconSize size = IconSize.Small,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new IconInfo
        {
            SvgContent = svgContent,
            Width = (int)size,
            Height = (int)size,
            Format = IconFormat.Svg,
            IsCached = false
        });
    }
    
    public void RegisterIconPack(IconPack pack)
    {
        lock (_lock)
        {
            _iconPacks.RemoveAll(p => p.Id == pack.Id);
            _iconPacks.Add(pack);
        }
    }
    
    public IReadOnlyList<IconPack> GetIconPacks()
    {
        lock (_lock)
        {
            return _iconPacks.ToList();
        }
    }
    
    public void SetActiveIconPack(string packId)
    {
        lock (_lock)
        {
            if (_iconPacks.Any(p => p.Id == packId))
            {
                _activePackId = packId;
                ClearCache();
                IconsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    public IconPack? GetActiveIconPack()
    {
        lock (_lock)
        {
            return _iconPacks.FirstOrDefault(p => p.Id == _activePackId);
        }
    }
    
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    public async Task PreloadCommonIconsAsync(CancellationToken cancellationToken = default)
    {
        var commonCategories = new[]
        {
            FileTypeCategory.File,
            FileTypeCategory.Folder,
            FileTypeCategory.Document,
            FileTypeCategory.Image,
            FileTypeCategory.Video,
            FileTypeCategory.Audio,
            FileTypeCategory.Archive,
            FileTypeCategory.Code,
            FileTypeCategory.Executable
        };
        
        var sizes = new[] { IconSize.Small, IconSize.Medium, IconSize.Large };
        
        foreach (var category in commonCategories)
        {
            foreach (var size in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var icon = await GetFileTypeIconAsync(category, size, cancellationToken);
                var cacheKey = $"{category}_{size}";
                _cache.TryAdd(cacheKey, icon with { IsCached = true });
            }
        }
    }
    
    private FileTypeCategory GetCategoryForExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return FileTypeCategory.File;
        
        if (ExtensionCategories.TryGetValue(extension, out var category))
            return category;
        
        return FileTypeCategory.File;
    }
    
    private IconOverlay CreateOverlay(OverlayType type, int priority)
    {
        DefaultOverlaySvgs.TryGetValue(type, out var svg);
        return new IconOverlay
        {
            Id = type.ToString().ToLower(),
            Name = type.ToString(),
            Type = type,
            Position = OverlayPosition.BottomLeft,
            SvgContent = svg,
            Priority = priority
        };
    }
    
    private async Task<IconInfo> LoadCustomIconAsync(CustomIconAssignment assignment, IconSize size,
        CancellationToken cancellationToken)
    {
        if (assignment.SvgContent != null)
        {
            return await LoadSvgFromContentAsync(assignment.SvgContent, size, cancellationToken);
        }
        
        if (assignment.ImageData != null)
        {
            return new IconInfo
            {
                ImageData = assignment.ImageData,
                Width = (int)size,
                Height = (int)size,
                Format = IconFormat.Png,
                IsCached = false
            };
        }
        
        if (!string.IsNullOrEmpty(assignment.IconPath) && File.Exists(assignment.IconPath))
        {
            var ext = Path.GetExtension(assignment.IconPath).ToLowerInvariant();
            if (ext == ".svg")
            {
                return await LoadSvgIconAsync(assignment.IconPath, size, cancellationToken);
            }
            
            return new IconInfo
            {
                ImageData = await File.ReadAllBytesAsync(assignment.IconPath, cancellationToken),
                Width = (int)size,
                Height = (int)size,
                Format = IconFormat.Png,
                SourcePath = assignment.IconPath,
                IsCached = false
            };
        }
        
        // Fall back to default
        return await GetFileTypeIconAsync(FileTypeCategory.File, size, cancellationToken);
    }
    
    private void InvalidateCacheForPath(string path)
    {
        // Simple implementation - clear cache entries that might be affected
        // More sophisticated implementation would track path->cache key mappings
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(Path.GetExtension(path) ?? "")).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }
    
    private void LoadCustomIcons()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var assignments = JsonSerializer.Deserialize<List<CustomIconAssignment>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (assignments != null)
                {
                    lock (_lock)
                    {
                        foreach (var assignment in assignments)
                        {
                            _customIcons[assignment.Path] = assignment;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }
    
    private async Task SaveCustomIconsAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            
            List<CustomIconAssignment> assignments;
            lock (_lock)
            {
                assignments = _customIcons.Values.ToList();
            }
            
            var json = JsonSerializer.Serialize(assignments, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    private void RegisterDefaultIconPack()
    {
        var defaultPack = new IconPack
        {
            Id = "default",
            Name = "Default",
            Description = "Built-in SVG icon pack",
            Author = "XCommander",
            Version = "1.0",
            IsSvgBased = true,
            CategoryMappings = new Dictionary<FileTypeCategory, string>()
        };
        
        RegisterIconPack(defaultPack);
        _activePackId = "default";
    }
}
