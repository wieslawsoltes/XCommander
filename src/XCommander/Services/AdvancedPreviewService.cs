using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Advanced content preview service implementation.
/// Provides preview support for HTML, Office documents, CAD files, databases, and fonts.
/// </summary>
public class AdvancedPreviewService : IAdvancedPreviewService
{
    private readonly List<IPreviewProvider> _providers = new();
    private readonly object _lock = new();
    
    public AdvancedPreviewService()
    {
        // Register built-in providers
        RegisterProvider(new HtmlPreviewProvider());
        RegisterProvider(new OfficePreviewProvider());
        RegisterProvider(new CadPreviewProvider());
        RegisterProvider(new DatabasePreviewProvider());
        RegisterProvider(new FontPreviewProvider());
    }
    
    public async Task<FilePreview> GetPreviewAsync(string filePath, PreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(filePath);
        if (provider == null)
        {
            return new FilePreview
            {
                FilePath = filePath,
                Type = PreviewType.None,
                IsSupported = false,
                ErrorMessage = "No preview provider available for this file type"
            };
        }
        
        try
        {
            return await provider.GeneratePreviewAsync(filePath, options, cancellationToken);
        }
        catch (Exception ex)
        {
            return new FilePreview
            {
                FilePath = filePath,
                Type = PreviewType.None,
                ProviderId = provider.Id,
                IsSupported = true,
                ErrorMessage = $"Preview generation failed: {ex.Message}"
            };
        }
    }
    
    public bool IsSupported(string filePath)
    {
        return GetProvider(filePath) != null;
    }
    
    public IPreviewProvider? GetProvider(string filePath)
    {
        lock (_lock)
        {
            return _providers
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault(p => p.IsSupported(filePath));
        }
    }
    
    public void RegisterProvider(IPreviewProvider provider)
    {
        lock (_lock)
        {
            _providers.RemoveAll(p => p.Id == provider.Id);
            _providers.Add(provider);
        }
    }
    
    public void UnregisterProvider(string providerId)
    {
        lock (_lock)
        {
            _providers.RemoveAll(p => p.Id == providerId);
        }
    }
    
    public IReadOnlyList<IPreviewProvider> GetProviders()
    {
        lock (_lock)
        {
            return _providers.OrderByDescending(p => p.Priority).ToList();
        }
    }
    
    public void SetProviderPriority(string providerId, int priority)
    {
        lock (_lock)
        {
            var provider = _providers.FirstOrDefault(p => p.Id == providerId);
            if (provider != null)
            {
                provider.Priority = priority;
            }
        }
    }
    
    public async Task<HtmlPreview> GetHtmlPreviewAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<HtmlPreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("HTML preview provider not available");
        
        var preview = await provider.GeneratePreviewAsync(filePath, null, cancellationToken);
        return preview as HtmlPreview ?? new HtmlPreview
        {
            FilePath = filePath,
            IsSupported = false,
            ErrorMessage = "Failed to generate HTML preview"
        };
    }
    
    public async Task<DocumentPreview> GetOfficePreviewAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<OfficePreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("Office preview provider not available");
        
        var preview = await provider.GeneratePreviewAsync(filePath, null, cancellationToken);
        return preview as DocumentPreview ?? new DocumentPreview
        {
            FilePath = filePath,
            IsSupported = false,
            ErrorMessage = "Failed to generate Office preview"
        };
    }
    
    public async Task<CadPreview> GetCadPreviewAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<CadPreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("CAD preview provider not available");
        
        var preview = await provider.GeneratePreviewAsync(filePath, null, cancellationToken);
        return preview as CadPreview ?? new CadPreview
        {
            FilePath = filePath,
            IsSupported = false,
            ErrorMessage = "Failed to generate CAD preview"
        };
    }
    
    public async Task<DatabasePreview> GetDatabasePreviewAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<DatabasePreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("Database preview provider not available");
        
        var preview = await provider.GeneratePreviewAsync(filePath, null, cancellationToken);
        return preview as DatabasePreview ?? new DatabasePreview
        {
            FilePath = filePath,
            IsSupported = false,
            ErrorMessage = "Failed to generate database preview"
        };
    }
    
    public async Task<FontPreview> GetFontPreviewAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<FontPreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("Font preview provider not available");
        
        var preview = await provider.GeneratePreviewAsync(filePath, null, cancellationToken);
        return preview as FontPreview ?? new FontPreview
        {
            FilePath = filePath,
            IsSupported = false,
            ErrorMessage = "Failed to generate font preview"
        };
    }
    
    public async Task<QueryResult> ExecuteDatabaseQueryAsync(string filePath, string query,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<DatabasePreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("Database preview provider not available");
        
        return await provider.ExecuteQueryAsync(filePath, query, cancellationToken);
    }
    
    public async Task<IReadOnlyList<TableSchema>> GetDatabaseSchemaAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.OfType<DatabasePreviewProvider>().FirstOrDefault();
        if (provider == null)
            throw new InvalidOperationException("Database preview provider not available");
        
        return await provider.GetSchemaAsync(filePath, cancellationToken);
    }
}

#region Built-in Providers

/// <summary>
/// HTML/Web content preview provider.
/// </summary>
public class HtmlPreviewProvider : IPreviewProvider
{
    public string Id => "html";
    public string Name => "HTML Preview";
    public string Description => "Preview HTML and web content files";
    public int Priority { get; set; } = 100;
    
    public IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".html", ".htm", ".xhtml", ".mhtml", ".mht", ".svg"
    };
    
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
    
    public async Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        
        // Extract resources
        var scripts = ExtractScripts(content);
        var stylesheets = ExtractStylesheets(content);
        var linkedResources = ExtractLinkedResources(content);
        var title = ExtractTitle(content);
        
        // Extract text content
        var textContent = StripHtmlTags(content);
        if (options?.MaxTextLength > 0 && textContent.Length > options.MaxTextLength)
        {
            textContent = textContent[..options.MaxTextLength];
        }
        
        return new HtmlPreview
        {
            FilePath = filePath,
            Type = PreviewType.Html,
            ProviderId = Id,
            IsSupported = true,
            HtmlContent = content,
            RenderedHtml = SanitizeHtml(content),
            LinkedResources = linkedResources,
            Scripts = scripts,
            Stylesheets = stylesheets,
            Title = title,
            BaseUrl = Path.GetDirectoryName(filePath),
            TextContent = textContent,
            Metadata = new Dictionary<string, string>
            {
                { "FileSize", new FileInfo(filePath).Length.ToString() },
                { "Encoding", DetectEncoding(content) },
                { "ScriptCount", scripts.Count.ToString() },
                { "StylesheetCount", stylesheets.Count.ToString() }
            }
        };
    }
    
    private static List<string> ExtractScripts(string html)
    {
        var scripts = new List<string>();
        var matches = Regex.Matches(html, @"<script[^>]*src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            scripts.Add(match.Groups[1].Value);
        }
        return scripts;
    }
    
    private static List<string> ExtractStylesheets(string html)
    {
        var stylesheets = new List<string>();
        var matches = Regex.Matches(html, @"<link[^>]*href=[""']([^""']+\.css[^""']*)[""']", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            stylesheets.Add(match.Groups[1].Value);
        }
        return stylesheets;
    }
    
    private static List<string> ExtractLinkedResources(string html)
    {
        var resources = new List<string>();
        var patterns = new[]
        {
            @"src=[""']([^""']+)[""']",
            @"href=[""']([^""']+)[""']"
        };
        
        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var resource = match.Groups[1].Value;
                if (!resource.StartsWith("#") && !resource.StartsWith("javascript:"))
                {
                    resources.Add(resource);
                }
            }
        }
        
        return resources.Distinct().ToList();
    }
    
    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
    
    private static string StripHtmlTags(string html)
    {
        // Remove script and style contents
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        
        // Remove tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        
        // Decode entities
        html = System.Net.WebUtility.HtmlDecode(html);
        
        // Normalize whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();
        
        return html;
    }
    
    private static string SanitizeHtml(string html)
    {
        // Remove potentially dangerous content for preview
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"on\w+=[""'][^""']*[""']", "", RegexOptions.IgnoreCase);
        return html;
    }
    
    private static string DetectEncoding(string content)
    {
        var match = Regex.Match(content, @"charset=[""']?([^""'\s>]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "UTF-8";
    }
}

/// <summary>
/// Office document preview provider.
/// </summary>
public class OfficePreviewProvider : IPreviewProvider
{
    public string Id => "office";
    public string Name => "Office Document Preview";
    public string Description => "Preview Microsoft Office and OpenDocument files";
    public int Priority { get; set; } = 90;
    
    public IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".odt", ".ods", ".odp", ".rtf"
    };
    
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
    
    public async Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();
        
        // For OOXML formats, we can extract some information
        var metadata = new Dictionary<string, string>
        {
            { "FileSize", fileInfo.Length.ToString() },
            { "Created", fileInfo.CreationTime.ToString() },
            { "Modified", fileInfo.LastWriteTime.ToString() }
        };
        
        string? textContent = null;
        int pageCount = 0;
        int wordCount = 0;
        
        if (ext is ".docx" or ".xlsx" or ".pptx")
        {
            // OOXML files are ZIP archives
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
                
                // Extract text from document.xml or similar
                var contentEntry = ext switch
                {
                    ".docx" => archive.GetEntry("word/document.xml"),
                    ".xlsx" => archive.GetEntry("xl/sharedStrings.xml"),
                    ".pptx" => archive.GetEntry("ppt/slides/slide1.xml"),
                    _ => null
                };
                
                if (contentEntry != null)
                {
                    using var stream = contentEntry.Open();
                    using var reader = new StreamReader(stream);
                    var xml = await reader.ReadToEndAsync(cancellationToken);
                    textContent = StripXmlTags(xml);
                    wordCount = textContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                
                // Try to get core properties
                var coreProps = archive.GetEntry("docProps/core.xml");
                if (coreProps != null)
                {
                    using var stream = coreProps.Open();
                    using var reader = new StreamReader(stream);
                    var xml = await reader.ReadToEndAsync(cancellationToken);
                    ExtractCoreProperties(xml, metadata);
                }
                
                // Count pages/slides
                if (ext == ".pptx")
                {
                    pageCount = archive.Entries.Count(e => 
                        e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"));
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
        else if (ext == ".rtf")
        {
            textContent = await ExtractRtfTextAsync(filePath, cancellationToken);
            wordCount = textContent?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        }
        
        if (options?.MaxTextLength > 0 && textContent?.Length > options.MaxTextLength)
        {
            textContent = textContent[..options.MaxTextLength];
        }
        
        return new DocumentPreview
        {
            FilePath = filePath,
            Type = ext switch
            {
                ".doc" or ".docx" or ".odt" or ".rtf" => PreviewType.Document,
                ".xls" or ".xlsx" or ".ods" => PreviewType.Spreadsheet,
                ".ppt" or ".pptx" or ".odp" => PreviewType.Presentation,
                _ => PreviewType.Document
            },
            ProviderId = Id,
            IsSupported = true,
            TextContent = textContent,
            Metadata = metadata,
            DocumentTitle = metadata.TryGetValue("Title", out var title) ? title : null,
            Author = metadata.TryGetValue("Author", out var author) ? author : null,
            PageCount = pageCount,
            WordCount = wordCount,
            CharacterCount = textContent?.Length ?? 0
        };
    }
    
    private static string StripXmlTags(string xml)
    {
        return Regex.Replace(xml, @"<[^>]+>", " ").Trim();
    }
    
    private static void ExtractCoreProperties(string xml, Dictionary<string, string> metadata)
    {
        var patterns = new Dictionary<string, string>
        {
            { "Title", @"<dc:title>([^<]+)</dc:title>" },
            { "Author", @"<dc:creator>([^<]+)</dc:creator>" },
            { "Subject", @"<dc:subject>([^<]+)</dc:subject>" },
            { "Description", @"<dc:description>([^<]+)</dc:description>" }
        };
        
        foreach (var (key, pattern) in patterns)
        {
            var match = Regex.Match(xml, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                metadata[key] = match.Groups[1].Value;
            }
        }
    }
    
    private static async Task<string?> ExtractRtfTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        
        // Simple RTF text extraction
        var sb = new StringBuilder();
        var inGroup = 0;
        var skipGroup = false;
        
        for (int i = 0; i < content.Length; i++)
        {
            var c = content[i];
            
            if (c == '{')
            {
                inGroup++;
                // Check for groups to skip
                if (i + 1 < content.Length && content.Substring(i, Math.Min(10, content.Length - i)).Contains("\\*"))
                {
                    skipGroup = true;
                }
            }
            else if (c == '}')
            {
                inGroup--;
                if (inGroup <= 0) skipGroup = false;
            }
            else if (!skipGroup && c == '\\')
            {
                // Skip control words
                while (i + 1 < content.Length && char.IsLetter(content[i + 1]))
                    i++;
                // Skip optional numeric parameter
                while (i + 1 < content.Length && (char.IsDigit(content[i + 1]) || content[i + 1] == '-'))
                    i++;
                // Skip optional space
                if (i + 1 < content.Length && content[i + 1] == ' ')
                    i++;
            }
            else if (!skipGroup && c != '\r' && c != '\n')
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString().Trim();
    }
}

/// <summary>
/// CAD file preview provider.
/// </summary>
public class CadPreviewProvider : IPreviewProvider
{
    public string Id => "cad";
    public string Name => "CAD Preview";
    public string Description => "Preview DWG, DXF, and other CAD files";
    public int Priority { get; set; } = 80;
    
    public IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".dwg", ".dxf", ".dgn", ".stl", ".obj"
    };
    
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
    
    public async Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();
        
        var layers = new List<CadLayer>();
        var entities = new List<CadEntity>();
        CadBounds? bounds = null;
        string? version = null;
        
        if (ext == ".dxf")
        {
            // Parse DXF file (ASCII format)
            await ParseDxfFileAsync(filePath, layers, entities, cancellationToken);
            
            // Calculate bounds
            if (entities.Count > 0)
            {
                bounds = CalculateBounds(entities);
            }
            
            version = await DetectDxfVersionAsync(filePath, cancellationToken);
        }
        else if (ext == ".stl")
        {
            // STL files - basic info
            var content = await File.ReadAllTextAsync(filePath, Encoding.ASCII, cancellationToken);
            if (content.StartsWith("solid"))
            {
                version = "ASCII STL";
            }
            else
            {
                version = "Binary STL";
            }
        }
        
        return new CadPreview
        {
            FilePath = filePath,
            Type = PreviewType.Cad,
            ProviderId = Id,
            IsSupported = true,
            CadFormat = ext.TrimStart('.').ToUpperInvariant(),
            Version = version,
            Bounds = bounds,
            LayerCount = layers.Count,
            EntityCount = entities.Count,
            Layers = layers,
            Entities = entities.Take(1000).ToList(), // Limit for preview
            Metadata = new Dictionary<string, string>
            {
                { "FileSize", fileInfo.Length.ToString() },
                { "Format", ext.TrimStart('.').ToUpperInvariant() }
            }
        };
    }
    
    private static async Task ParseDxfFileAsync(string filePath, List<CadLayer> layers, 
        List<CadEntity> entities, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var currentSection = "";
        var currentEntity = "";
        var entityData = new Dictionary<string, object>();
        var layerSet = new HashSet<string>();
        
        for (int i = 0; i < lines.Length - 1; i += 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!int.TryParse(lines[i].Trim(), out var code))
                continue;
            
            var value = lines[i + 1].Trim();
            
            if (code == 0)
            {
                // Save previous entity
                if (!string.IsNullOrEmpty(currentEntity) && entityData.Count > 0)
                {
                    var entity = new CadEntity
                    {
                        Type = currentEntity,
                        LayerName = entityData.TryGetValue("Layer", out var layer) ? layer?.ToString() : null,
                        Properties = new Dictionary<string, object>(entityData)
                    };
                    entities.Add(entity);
                    
                    if (entity.LayerName != null)
                        layerSet.Add(entity.LayerName);
                }
                
                currentEntity = value;
                entityData.Clear();
            }
            else if (code == 2 && value == "SECTION")
            {
                // Next line should be section name
            }
            else if (code == 2)
            {
                currentSection = value;
            }
            else if (code == 8)
            {
                entityData["Layer"] = value;
            }
            else if (code >= 10 && code <= 39)
            {
                // Coordinate values
                if (double.TryParse(value, out var coord))
                {
                    var coordName = code switch
                    {
                        10 => "X", 20 => "Y", 30 => "Z",
                        11 => "X2", 21 => "Y2", 31 => "Z2",
                        _ => $"Coord{code}"
                    };
                    entityData[coordName] = coord;
                }
            }
        }
        
        // Create layers from found layer names
        foreach (var layerName in layerSet)
        {
            layers.Add(new CadLayer
            {
                Name = layerName,
                IsVisible = true,
                EntityCount = entities.Count(e => e.LayerName == layerName)
            });
        }
    }
    
    private static CadBounds? CalculateBounds(List<CadEntity> entities)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool hasCoords = false;
        
        foreach (var entity in entities)
        {
            if (entity.Properties == null) continue;
            
            if (entity.Properties.TryGetValue("X", out var xObj) && xObj is double x)
            {
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                hasCoords = true;
            }
            
            if (entity.Properties.TryGetValue("Y", out var yObj) && yObj is double y)
            {
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
            
            if (entity.Properties.TryGetValue("Z", out var zObj) && zObj is double z)
            {
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }
        }
        
        if (!hasCoords) return null;
        
        return new CadBounds
        {
            MinX = minX == double.MaxValue ? 0 : minX,
            MinY = minY == double.MaxValue ? 0 : minY,
            MinZ = minZ == double.MaxValue ? 0 : minZ,
            MaxX = maxX == double.MinValue ? 0 : maxX,
            MaxY = maxY == double.MinValue ? 0 : maxY,
            MaxZ = maxZ == double.MinValue ? 0 : maxZ
        };
    }
    
    private static async Task<string?> DetectDxfVersionAsync(string filePath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        
        for (int i = 0; i < Math.Min(100, lines.Length - 1); i++)
        {
            if (lines[i].Trim() == "9" && lines[i + 1].Trim() == "$ACADVER")
            {
                if (i + 3 < lines.Length && lines[i + 2].Trim() == "1")
                {
                    return lines[i + 3].Trim();
                }
            }
        }
        
        return null;
    }
}

/// <summary>
/// Database file preview provider.
/// </summary>
public class DatabasePreviewProvider : IPreviewProvider
{
    public string Id => "database";
    public string Name => "Database Preview";
    public string Description => "Preview SQLite, DBF, and other database files";
    public int Priority { get; set; } = 85;
    
    public IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".sqlite", ".sqlite3", ".db", ".db3", ".s3db", ".dbf", ".mdb", ".accdb"
    };
    
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
    
    public async Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();
        
        string? dbType = ext switch
        {
            ".sqlite" or ".sqlite3" or ".db" or ".db3" or ".s3db" => "SQLite",
            ".dbf" => "dBASE",
            ".mdb" or ".accdb" => "Microsoft Access",
            _ => "Unknown"
        };
        
        var tables = new List<TableSchema>();
        var rowCounts = new Dictionary<string, int>();
        
        if (dbType == "SQLite")
        {
            tables = await GetSqliteSchemaAsync(filePath, rowCounts, cancellationToken);
        }
        else if (dbType == "dBASE")
        {
            tables = await GetDbfSchemaAsync(filePath, rowCounts, cancellationToken);
        }
        
        return new DatabasePreview
        {
            FilePath = filePath,
            Type = PreviewType.Database,
            ProviderId = Id,
            IsSupported = true,
            DatabaseType = dbType,
            FileSize = fileInfo.Length,
            TableCount = tables.Count,
            Tables = tables,
            TableRowCounts = rowCounts,
            Metadata = new Dictionary<string, string>
            {
                { "FileSize", fileInfo.Length.ToString() },
                { "DatabaseType", dbType }
            }
        };
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(string filePath, string query,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // This is a stub - real implementation would use Microsoft.Data.Sqlite
        // For now, return a placeholder result
        await Task.Delay(10, cancellationToken);
        
        stopwatch.Stop();
        
        return new QueryResult
        {
            Success = false,
            ErrorMessage = "Query execution requires Microsoft.Data.Sqlite package",
            ExecutionTime = stopwatch.Elapsed
        };
    }
    
    public async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        var rowCounts = new Dictionary<string, int>();
        
        if (ext is ".sqlite" or ".sqlite3" or ".db" or ".db3" or ".s3db")
        {
            return await GetSqliteSchemaAsync(filePath, rowCounts, cancellationToken);
        }
        else if (ext == ".dbf")
        {
            return await GetDbfSchemaAsync(filePath, rowCounts, cancellationToken);
        }
        
        return Array.Empty<TableSchema>();
    }
    
    private static async Task<List<TableSchema>> GetSqliteSchemaAsync(string filePath,
        Dictionary<string, int> rowCounts, CancellationToken cancellationToken)
    {
        var tables = new List<TableSchema>();
        
        // Read SQLite file header to verify it's a valid SQLite database
        var header = new byte[16];
        using (var stream = File.OpenRead(filePath))
        {
            await stream.ReadAsync(header, 0, 16, cancellationToken);
        }
        
        var headerStr = Encoding.ASCII.GetString(header);
        if (!headerStr.StartsWith("SQLite format"))
        {
            return tables;
        }
        
        // Without Microsoft.Data.Sqlite, we can only provide basic info
        // Real implementation would query sqlite_master table
        tables.Add(new TableSchema
        {
            Name = "(Schema requires Microsoft.Data.Sqlite)",
            Type = "INFO"
        });
        
        return tables;
    }
    
    private static async Task<List<TableSchema>> GetDbfSchemaAsync(string filePath,
        Dictionary<string, int> rowCounts, CancellationToken cancellationToken)
    {
        var tables = new List<TableSchema>();
        var columns = new List<ColumnSchema>();
        
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        
        // Read DBF header
        var version = reader.ReadByte();
        var year = reader.ReadByte() + 1900;
        var month = reader.ReadByte();
        var day = reader.ReadByte();
        var recordCount = reader.ReadInt32();
        var headerSize = reader.ReadInt16();
        var recordSize = reader.ReadInt16();
        
        // Skip to field descriptors (position 32)
        stream.Seek(32, SeekOrigin.Begin);
        
        // Read field descriptors
        while (stream.Position < headerSize - 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fieldBytes = reader.ReadBytes(32);
            if (fieldBytes[0] == 0x0D) // Header terminator
                break;
            
            var fieldName = Encoding.ASCII.GetString(fieldBytes, 0, 11).TrimEnd('\0');
            var fieldType = (char)fieldBytes[11];
            var fieldLength = fieldBytes[16];
            var fieldDecimals = fieldBytes[17];
            
            columns.Add(new ColumnSchema
            {
                Name = fieldName,
                DataType = fieldType switch
                {
                    'C' => "Character",
                    'N' => "Numeric",
                    'F' => "Float",
                    'D' => "Date",
                    'L' => "Logical",
                    'M' => "Memo",
                    _ => fieldType.ToString()
                },
                MaxLength = fieldLength,
                Scale = fieldDecimals
            });
        }
        
        var tableName = Path.GetFileNameWithoutExtension(filePath);
        tables.Add(new TableSchema
        {
            Name = tableName,
            Type = "TABLE",
            Columns = columns,
            RowCount = recordCount
        });
        
        rowCounts[tableName] = recordCount;
        
        return tables;
    }
}

/// <summary>
/// Font file preview provider.
/// </summary>
public class FontPreviewProvider : IPreviewProvider
{
    public string Id => "font";
    public string Name => "Font Preview";
    public string Description => "Preview TrueType, OpenType, and other font files";
    public int Priority { get; set; } = 75;
    
    public IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".ttf", ".otf", ".woff", ".woff2", ".eot", ".ttc"
    };
    
    public bool IsSupported(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }
    
    public async Task<FilePreview> GeneratePreviewAsync(string filePath, PreviewOptions? options,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.ToLowerInvariant();
        
        string? fontFamily = null;
        string? fullName = null;
        string? style = null;
        string? version = null;
        string? copyright = null;
        
        if (ext is ".ttf" or ".otf")
        {
            // Parse TrueType/OpenType font
            var fontInfo = await ParseOpenTypeFontAsync(filePath, cancellationToken);
            fontFamily = fontInfo.FontFamily;
            fullName = fontInfo.FullName;
            style = fontInfo.Style;
            version = fontInfo.Version;
            copyright = fontInfo.Copyright;
        }
        
        // Generate sample texts
        var samples = new List<FontSample>
        {
            new() { Text = "The quick brown fox jumps over the lazy dog", FontSize = 12 },
            new() { Text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ", FontSize = 16 },
            new() { Text = "abcdefghijklmnopqrstuvwxyz", FontSize = 16 },
            new() { Text = "0123456789 !@#$%^&*()", FontSize = 16 }
        };
        
        return new FontPreview
        {
            FilePath = filePath,
            Type = PreviewType.Font,
            ProviderId = Id,
            IsSupported = true,
            FontFamily = fontFamily,
            FullName = fullName,
            Style = style,
            Version = version,
            Copyright = copyright,
            Samples = samples,
            Metadata = new Dictionary<string, string>
            {
                { "FileSize", fileInfo.Length.ToString() },
                { "Format", ext.TrimStart('.').ToUpperInvariant() },
                { "FontFamily", fontFamily ?? "Unknown" }
            }
        };
    }
    
    private static Task<(string? FontFamily, string? FullName, string? Style, string? Version, string? Copyright)> 
        ParseOpenTypeFontAsync(string filePath, CancellationToken cancellationToken)
    {
        string? fontFamily = null;
        string? fullName = null;
        string? style = null;
        string? version = null;
        string? copyright = null;
        
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        
        try
        {
            // Read OpenType header
            var sfntVersion = ReadUInt32BE(reader);
            var numTables = ReadUInt16BE(reader);
            var searchRange = ReadUInt16BE(reader);
            var entrySelector = ReadUInt16BE(reader);
            var rangeShift = ReadUInt16BE(reader);
            
            // Find 'name' table
            uint nameOffset = 0;
            uint nameLength = 0;
            
            for (int i = 0; i < numTables; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var tag = ReadTag(reader);
                var checksum = ReadUInt32BE(reader);
                var offset = ReadUInt32BE(reader);
                var length = ReadUInt32BE(reader);
                
                if (tag == "name")
                {
                    nameOffset = offset;
                    nameLength = length;
                    break;
                }
            }
            
            if (nameOffset == 0) return Task.FromResult((fontFamily, fullName, style, version, copyright));
            
            // Read name table
            stream.Seek(nameOffset, SeekOrigin.Begin);
            var format = ReadUInt16BE(reader);
            var count = ReadUInt16BE(reader);
            var stringOffset = ReadUInt16BE(reader);
            
            var nameRecords = new List<(int nameId, int platformId, int encodingId, int offset, int length)>();
            
            for (int i = 0; i < count; i++)
            {
                var platformId = ReadUInt16BE(reader);
                var encodingId = ReadUInt16BE(reader);
                var languageId = ReadUInt16BE(reader);
                var nameId = ReadUInt16BE(reader);
                var length = ReadUInt16BE(reader);
                var offset = ReadUInt16BE(reader);
                
                nameRecords.Add((nameId, platformId, encodingId, offset, length));
            }
            
            var stringStorageOffset = nameOffset + stringOffset;
            
            foreach (var (nameId, platformId, encodingId, offset, length) in nameRecords)
            {
                if (length == 0) continue;
                
                stream.Seek(stringStorageOffset + offset, SeekOrigin.Begin);
                var bytes = reader.ReadBytes(length);
                
                var encoding = (platformId, encodingId) switch
                {
                    (3, 1) => Encoding.BigEndianUnicode,
                    (1, 0) => Encoding.ASCII,
                    _ => Encoding.BigEndianUnicode
                };
                
                var value = encoding.GetString(bytes).Trim('\0');
                
                switch (nameId)
                {
                    case 0: copyright ??= value; break;
                    case 1: fontFamily ??= value; break;
                    case 2: style ??= value; break;
                    case 4: fullName ??= value; break;
                    case 5: version ??= value; break;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        
        return Task.FromResult((fontFamily, fullName, style, version, copyright));
    }
    
    private static ushort ReadUInt16BE(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2);
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }
    
    private static uint ReadUInt32BE(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
    }
    
    private static string ReadTag(BinaryReader reader)
    {
        return Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
#endregion
