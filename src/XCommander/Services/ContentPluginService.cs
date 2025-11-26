using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Content plugin service for extracting metadata from files.
/// Manages content plugins and provides unified access to file metadata.
/// </summary>
public class ContentPluginService : IContentPluginService
{
    private readonly List<ContentPlugin> _plugins = new();
    private readonly object _lock = new();
    
    public ContentPluginService()
    {
        // Register built-in plugins
        RegisterPlugin(new ImageContentPlugin());
        RegisterPlugin(new AudioContentPlugin());
        RegisterPlugin(new DocumentContentPlugin());
        RegisterPlugin(new ExecutableContentPlugin());
        RegisterPlugin(new ArchiveContentPlugin());
    }
    
    public IReadOnlyList<ContentPlugin> GetPlugins()
    {
        lock (_lock)
        {
            return _plugins.OrderByDescending(p => p.Priority).ToList();
        }
    }
    
    public void RegisterPlugin(ContentPlugin plugin)
    {
        lock (_lock)
        {
            _plugins.RemoveAll(p => p.Id == plugin.Id);
            _plugins.Add(plugin);
        }
    }
    
    public void UnregisterPlugin(string pluginId)
    {
        lock (_lock)
        {
            _plugins.RemoveAll(p => p.Id == pluginId);
        }
    }
    
    public async Task<IReadOnlyList<ContentField>> GetFieldsForFileAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var fields = new List<ContentField>();
        var plugins = GetPlugins();
        
        foreach (var plugin in plugins)
        {
            if (plugin.IsSupported(filePath))
            {
                foreach (var fieldDef in plugin.Fields)
                {
                    fields.Add(new ContentField
                    {
                        Name = fieldDef.Name,
                        DisplayName = fieldDef.DisplayName,
                        PluginId = plugin.Id,
                        Type = fieldDef.Type,
                        IsEditable = fieldDef.IsEditable && plugin.SupportsEditing
                    });
                }
            }
        }
        
        return await Task.FromResult(fields);
    }
    
    public async Task<ContentFieldValue?> GetFieldValueAsync(string filePath, string fieldName,
        CancellationToken cancellationToken = default)
    {
        var plugins = GetPlugins();
        
        foreach (var plugin in plugins)
        {
            if (plugin.IsSupported(filePath) && plugin.Fields.Any(f => f.Name == fieldName))
            {
                var values = await plugin.GetValuesAsync(filePath, cancellationToken);
                if (values.TryGetValue(fieldName, out var value))
                {
                    return value;
                }
            }
        }
        
        return null;
    }
    
    public async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetFieldValuesAsync(string filePath,
        IEnumerable<string> fieldNames, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ContentFieldValue>();
        var fieldNameSet = new HashSet<string>(fieldNames);
        var plugins = GetPlugins();
        
        foreach (var plugin in plugins)
        {
            if (!plugin.IsSupported(filePath)) continue;
            
            var pluginFields = plugin.Fields.Where(f => fieldNameSet.Contains(f.Name)).ToList();
            if (pluginFields.Count == 0) continue;
            
            var values = await plugin.GetValuesAsync(filePath, cancellationToken);
            foreach (var (name, value) in values)
            {
                if (fieldNameSet.Contains(name) && !results.ContainsKey(name))
                {
                    results[name] = value;
                }
            }
        }
        
        return results;
    }
    
    public async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetAllFieldValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ContentFieldValue>();
        var plugins = GetPlugins();
        
        foreach (var plugin in plugins)
        {
            if (!plugin.IsSupported(filePath)) continue;
            
            try
            {
                var values = await plugin.GetValuesAsync(filePath, cancellationToken);
                foreach (var (name, value) in values)
                {
                    var qualifiedName = $"{plugin.Id}.{name}";
                    results[qualifiedName] = value;
                }
            }
            catch
            {
                // Ignore plugin errors
            }
        }
        
        return results;
    }
    
    public async Task<bool> SetFieldValueAsync(string filePath, string fieldName, object value,
        CancellationToken cancellationToken = default)
    {
        var plugins = GetPlugins();
        
        foreach (var plugin in plugins)
        {
            if (plugin.IsSupported(filePath) && 
                plugin.SupportsEditing &&
                plugin.Fields.Any(f => f.Name == fieldName && f.IsEditable))
            {
                return await plugin.SetValueAsync(filePath, fieldName, value, cancellationToken);
            }
        }
        
        return false;
    }
    
    public bool IsFileSupported(string filePath)
    {
        return GetPlugins().Any(p => p.IsSupported(filePath));
    }
}

#region Built-in Plugins

/// <summary>
/// Content plugin for image files (EXIF, dimensions, etc.).
/// </summary>
public class ImageContentPlugin : ContentPlugin
{
    public override string Id => "image";
    public override string Name => "Image Properties";
    public override string Description => "Extracts image dimensions, EXIF data, and color information";
    public override int Priority => 100;
    
    public override IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".ico", ".svg"
    };
    
    public override IReadOnlyList<ContentFieldDefinition> Fields => new[]
    {
        new ContentFieldDefinition { Name = "Width", DisplayName = "Width", Category = "Dimensions", Type = ContentFieldType.Integer, Unit = "px" },
        new ContentFieldDefinition { Name = "Height", DisplayName = "Height", Category = "Dimensions", Type = ContentFieldType.Integer, Unit = "px" },
        new ContentFieldDefinition { Name = "Dimensions", DisplayName = "Dimensions", Category = "Dimensions", Type = ContentFieldType.Dimensions },
        new ContentFieldDefinition { Name = "BitDepth", DisplayName = "Bit Depth", Category = "Image", Type = ContentFieldType.Integer, Unit = "bit" },
        new ContentFieldDefinition { Name = "ColorType", DisplayName = "Color Type", Category = "Image", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "DpiX", DisplayName = "DPI X", Category = "Resolution", Type = ContentFieldType.Float },
        new ContentFieldDefinition { Name = "DpiY", DisplayName = "DPI Y", Category = "Resolution", Type = ContentFieldType.Float },
        new ContentFieldDefinition { Name = "Camera", DisplayName = "Camera", Category = "EXIF", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "DateTaken", DisplayName = "Date Taken", Category = "EXIF", Type = ContentFieldType.DateTime },
        new ContentFieldDefinition { Name = "ExposureTime", DisplayName = "Exposure Time", Category = "EXIF", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "FNumber", DisplayName = "F-Number", Category = "EXIF", Type = ContentFieldType.Float },
        new ContentFieldDefinition { Name = "ISO", DisplayName = "ISO", Category = "EXIF", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "FocalLength", DisplayName = "Focal Length", Category = "EXIF", Type = ContentFieldType.Float, Unit = "mm" },
        new ContentFieldDefinition { Name = "GPS", DisplayName = "GPS Location", Category = "EXIF", Type = ContentFieldType.String }
    };
    
    public override async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, ContentFieldValue>();
        
        await Task.Run(() =>
        {
            try
            {
                // Read basic image info from file header
                // Note: Full EXIF support would require additional library like MetadataExtractor
                using var stream = File.OpenRead(filePath);
                var (width, height, bitDepth) = ReadImageBasicInfo(stream, Path.GetExtension(filePath));
                
                if (width > 0 && height > 0)
                {
                    values["Width"] = new ContentFieldValue
                    {
                        FieldName = "Width",
                        Value = width,
                        DisplayValue = $"{width} px",
                        Type = ContentFieldType.Integer,
                        Unit = "px"
                    };
                    
                    values["Height"] = new ContentFieldValue
                    {
                        FieldName = "Height",
                        Value = height,
                        DisplayValue = $"{height} px",
                        Type = ContentFieldType.Integer,
                        Unit = "px"
                    };
                    
                    values["Dimensions"] = new ContentFieldValue
                    {
                        FieldName = "Dimensions",
                        Value = (width, height),
                        DisplayValue = $"{width} x {height}",
                        Type = ContentFieldType.Dimensions
                    };
                }
                
                if (bitDepth > 0)
                {
                    values["BitDepth"] = new ContentFieldValue
                    {
                        FieldName = "BitDepth",
                        Value = bitDepth,
                        DisplayValue = $"{bitDepth} bit",
                        Type = ContentFieldType.Integer,
                        Unit = "bit"
                    };
                }
            }
            catch
            {
                // Ignore errors reading image
            }
        }, cancellationToken);
        
        return values;
    }
    
    private static (int width, int height, int bitDepth) ReadImageBasicInfo(Stream stream, string extension)
    {
        var buffer = new byte[32];
        stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;
        
        extension = extension.ToLowerInvariant();
        
        // PNG
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
        {
            // PNG header: width at offset 16, height at offset 20 (big endian)
            stream.Position = 16;
            var dimBuffer = new byte[8];
            stream.Read(dimBuffer, 0, 8);
            var width = (dimBuffer[0] << 24) | (dimBuffer[1] << 16) | (dimBuffer[2] << 8) | dimBuffer[3];
            var height = (dimBuffer[4] << 24) | (dimBuffer[5] << 16) | (dimBuffer[6] << 8) | dimBuffer[7];
            
            stream.Position = 24;
            var bitDepth = stream.ReadByte();
            
            return (width, height, bitDepth);
        }
        
        // JPEG
        if (buffer[0] == 0xFF && buffer[1] == 0xD8)
        {
            stream.Position = 2;
            while (stream.Position < stream.Length - 8)
            {
                if (stream.ReadByte() != 0xFF) continue;
                var marker = stream.ReadByte();
                
                // SOF markers (Start of Frame)
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    stream.Position += 3; // Skip length and precision
                    var heightBuffer = new byte[4];
                    stream.Read(heightBuffer, 0, 4);
                    var height = (heightBuffer[0] << 8) | heightBuffer[1];
                    var width = (heightBuffer[2] << 8) | heightBuffer[3];
                    return (width, height, 8);
                }
                
                // Skip other markers
                var lenBuffer = new byte[2];
                stream.Read(lenBuffer, 0, 2);
                var length = (lenBuffer[0] << 8) | lenBuffer[1];
                stream.Position += length - 2;
            }
        }
        
        // GIF
        if (buffer[0] == 'G' && buffer[1] == 'I' && buffer[2] == 'F')
        {
            var width = buffer[6] | (buffer[7] << 8);
            var height = buffer[8] | (buffer[9] << 8);
            return (width, height, 8);
        }
        
        // BMP
        if (buffer[0] == 'B' && buffer[1] == 'M')
        {
            stream.Position = 18;
            var dimBuffer = new byte[8];
            stream.Read(dimBuffer, 0, 8);
            var width = dimBuffer[0] | (dimBuffer[1] << 8) | (dimBuffer[2] << 16) | (dimBuffer[3] << 24);
            var height = dimBuffer[4] | (dimBuffer[5] << 8) | (dimBuffer[6] << 16) | (dimBuffer[7] << 24);
            
            stream.Position = 28;
            var bitDepth = stream.ReadByte() | (stream.ReadByte() << 8);
            
            return (width, Math.Abs(height), bitDepth);
        }
        
        return (0, 0, 0);
    }
}

/// <summary>
/// Content plugin for audio files (ID3 tags, duration, bitrate, etc.).
/// </summary>
public class AudioContentPlugin : ContentPlugin
{
    public override string Id => "audio";
    public override string Name => "Audio Properties";
    public override string Description => "Extracts audio tags, duration, bitrate, and format information";
    public override int Priority => 90;
    
    public override IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".mp3", ".flac", ".ogg", ".wav", ".wma", ".m4a", ".aac", ".opus", ".aiff"
    };
    
    public override IReadOnlyList<ContentFieldDefinition> Fields => new[]
    {
        new ContentFieldDefinition { Name = "Title", DisplayName = "Title", Category = "Tags", Type = ContentFieldType.String, IsEditable = true },
        new ContentFieldDefinition { Name = "Artist", DisplayName = "Artist", Category = "Tags", Type = ContentFieldType.String, IsEditable = true },
        new ContentFieldDefinition { Name = "Album", DisplayName = "Album", Category = "Tags", Type = ContentFieldType.String, IsEditable = true },
        new ContentFieldDefinition { Name = "Year", DisplayName = "Year", Category = "Tags", Type = ContentFieldType.Integer, IsEditable = true },
        new ContentFieldDefinition { Name = "Genre", DisplayName = "Genre", Category = "Tags", Type = ContentFieldType.String, IsEditable = true },
        new ContentFieldDefinition { Name = "TrackNumber", DisplayName = "Track", Category = "Tags", Type = ContentFieldType.Integer, IsEditable = true },
        new ContentFieldDefinition { Name = "Duration", DisplayName = "Duration", Category = "Audio", Type = ContentFieldType.Duration },
        new ContentFieldDefinition { Name = "BitRate", DisplayName = "Bit Rate", Category = "Audio", Type = ContentFieldType.BitRate, Unit = "kbps" },
        new ContentFieldDefinition { Name = "SampleRate", DisplayName = "Sample Rate", Category = "Audio", Type = ContentFieldType.SampleRate, Unit = "Hz" },
        new ContentFieldDefinition { Name = "Channels", DisplayName = "Channels", Category = "Audio", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "Comment", DisplayName = "Comment", Category = "Tags", Type = ContentFieldType.String, IsEditable = true }
    };
    
    public override bool SupportsEditing => true;
    
    public override async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, ContentFieldValue>();
        
        await Task.Run(() =>
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (extension == ".mp3")
                {
                    ReadMp3Info(filePath, values);
                }
                else if (extension == ".wav")
                {
                    ReadWavInfo(filePath, values);
                }
                // Other formats would require additional library like NAudio or TagLib#
            }
            catch
            {
                // Ignore errors reading audio
            }
        }, cancellationToken);
        
        return values;
    }
    
    private static void ReadMp3Info(string filePath, Dictionary<string, ContentFieldValue> values)
    {
        using var stream = File.OpenRead(filePath);
        
        // Try to read ID3v2 tag
        var header = new byte[10];
        stream.Read(header, 0, 10);
        
        if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            // ID3v2 tag found
            var version = header[3];
            var size = ((header[6] & 0x7F) << 21) | ((header[7] & 0x7F) << 14) | 
                       ((header[8] & 0x7F) << 7) | (header[9] & 0x7F);
            
            // Read tag data (simplified - full implementation would parse frames)
            var tagData = new byte[Math.Min(size, 1024)];
            stream.Read(tagData, 0, tagData.Length);
        }
        
        // Try to read ID3v1 tag at end of file
        if (stream.Length > 128)
        {
            stream.Position = stream.Length - 128;
            var id3v1 = new byte[128];
            stream.Read(id3v1, 0, 128);
            
            if (id3v1[0] == 'T' && id3v1[1] == 'A' && id3v1[2] == 'G')
            {
                var title = System.Text.Encoding.ASCII.GetString(id3v1, 3, 30).Trim('\0', ' ');
                var artist = System.Text.Encoding.ASCII.GetString(id3v1, 33, 30).Trim('\0', ' ');
                var album = System.Text.Encoding.ASCII.GetString(id3v1, 63, 30).Trim('\0', ' ');
                var year = System.Text.Encoding.ASCII.GetString(id3v1, 93, 4).Trim('\0', ' ');
                
                if (!string.IsNullOrEmpty(title))
                    values["Title"] = new ContentFieldValue { FieldName = "Title", Value = title, DisplayValue = title, Type = ContentFieldType.String };
                if (!string.IsNullOrEmpty(artist))
                    values["Artist"] = new ContentFieldValue { FieldName = "Artist", Value = artist, DisplayValue = artist, Type = ContentFieldType.String };
                if (!string.IsNullOrEmpty(album))
                    values["Album"] = new ContentFieldValue { FieldName = "Album", Value = album, DisplayValue = album, Type = ContentFieldType.String };
                if (int.TryParse(year, out var yearNum) && yearNum > 0)
                    values["Year"] = new ContentFieldValue { FieldName = "Year", Value = yearNum, DisplayValue = year, Type = ContentFieldType.Integer };
            }
        }
        
        // Estimate duration from file size and bitrate (rough approximation)
        var fileSizeBytes = stream.Length;
        var estimatedBitrate = 128; // Assume 128 kbps if unknown
        var estimatedDuration = TimeSpan.FromSeconds(fileSizeBytes * 8.0 / (estimatedBitrate * 1000));
        
        values["Duration"] = new ContentFieldValue
        {
            FieldName = "Duration",
            Value = estimatedDuration,
            DisplayValue = FormatDuration(estimatedDuration),
            Type = ContentFieldType.Duration
        };
        
        values["BitRate"] = new ContentFieldValue
        {
            FieldName = "BitRate",
            Value = estimatedBitrate,
            DisplayValue = $"{estimatedBitrate} kbps",
            Type = ContentFieldType.BitRate,
            Unit = "kbps"
        };
    }
    
    private static void ReadWavInfo(string filePath, Dictionary<string, ContentFieldValue> values)
    {
        using var stream = File.OpenRead(filePath);
        var header = new byte[44];
        stream.Read(header, 0, 44);
        
        // RIFF header
        if (header[0] != 'R' || header[1] != 'I' || header[2] != 'F' || header[3] != 'F') return;
        if (header[8] != 'W' || header[9] != 'A' || header[10] != 'V' || header[11] != 'E') return;
        
        // Format chunk
        var channels = header[22] | (header[23] << 8);
        var sampleRate = header[24] | (header[25] << 8) | (header[26] << 16) | (header[27] << 24);
        var byteRate = header[28] | (header[29] << 8) | (header[30] << 16) | (header[31] << 24);
        var bitsPerSample = header[34] | (header[35] << 8);
        
        values["SampleRate"] = new ContentFieldValue
        {
            FieldName = "SampleRate",
            Value = sampleRate,
            DisplayValue = $"{sampleRate} Hz",
            Type = ContentFieldType.SampleRate,
            Unit = "Hz"
        };
        
        values["Channels"] = new ContentFieldValue
        {
            FieldName = "Channels",
            Value = channels,
            DisplayValue = channels == 1 ? "Mono" : channels == 2 ? "Stereo" : $"{channels}",
            Type = ContentFieldType.Integer
        };
        
        var bitRate = byteRate * 8 / 1000;
        values["BitRate"] = new ContentFieldValue
        {
            FieldName = "BitRate",
            Value = bitRate,
            DisplayValue = $"{bitRate} kbps",
            Type = ContentFieldType.BitRate,
            Unit = "kbps"
        };
        
        // Calculate duration
        var dataSize = stream.Length - 44;
        var duration = TimeSpan.FromSeconds((double)dataSize / byteRate);
        values["Duration"] = new ContentFieldValue
        {
            FieldName = "Duration",
            Value = duration,
            DisplayValue = FormatDuration(duration),
            Type = ContentFieldType.Duration
        };
    }
    
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        return $"{duration.Minutes}:{duration.Seconds:D2}";
    }
}

/// <summary>
/// Content plugin for document files.
/// </summary>
public class DocumentContentPlugin : ContentPlugin
{
    public override string Id => "document";
    public override string Name => "Document Properties";
    public override string Description => "Extracts document metadata like page count, word count, etc.";
    public override int Priority => 80;
    
    public override IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".txt"
    };
    
    public override IReadOnlyList<ContentFieldDefinition> Fields => new[]
    {
        new ContentFieldDefinition { Name = "Title", DisplayName = "Title", Category = "Document", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "Author", DisplayName = "Author", Category = "Document", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "Subject", DisplayName = "Subject", Category = "Document", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "PageCount", DisplayName = "Pages", Category = "Document", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "WordCount", DisplayName = "Words", Category = "Document", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "LineCount", DisplayName = "Lines", Category = "Document", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "CharCount", DisplayName = "Characters", Category = "Document", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "CreatedDate", DisplayName = "Created", Category = "Document", Type = ContentFieldType.DateTime },
        new ContentFieldDefinition { Name = "ModifiedDate", DisplayName = "Modified", Category = "Document", Type = ContentFieldType.DateTime }
    };
    
    public override async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, ContentFieldValue>();
        
        await Task.Run(() =>
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (extension == ".txt" || extension == ".rtf")
            {
                ReadTextFileInfo(filePath, values);
            }
            // PDF, Office formats would require additional libraries
        }, cancellationToken);
        
        return values;
    }
    
    private static void ReadTextFileInfo(string filePath, Dictionary<string, ContentFieldValue> values)
    {
        var content = File.ReadAllText(filePath);
        var lines = content.Split('\n');
        var words = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        values["LineCount"] = new ContentFieldValue
        {
            FieldName = "LineCount",
            Value = lines.Length,
            DisplayValue = lines.Length.ToString("N0"),
            Type = ContentFieldType.Integer
        };
        
        values["WordCount"] = new ContentFieldValue
        {
            FieldName = "WordCount",
            Value = words.Length,
            DisplayValue = words.Length.ToString("N0"),
            Type = ContentFieldType.Integer
        };
        
        values["CharCount"] = new ContentFieldValue
        {
            FieldName = "CharCount",
            Value = content.Length,
            DisplayValue = content.Length.ToString("N0"),
            Type = ContentFieldType.Integer
        };
    }
}

/// <summary>
/// Content plugin for executable files.
/// </summary>
public class ExecutableContentPlugin : ContentPlugin
{
    public override string Id => "executable";
    public override string Name => "Executable Properties";
    public override string Description => "Extracts executable version info, company, description, etc.";
    public override int Priority => 70;
    
    public override IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".exe", ".dll", ".sys", ".ocx"
    };
    
    public override IReadOnlyList<ContentFieldDefinition> Fields => new[]
    {
        new ContentFieldDefinition { Name = "FileVersion", DisplayName = "File Version", Category = "Version", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "ProductVersion", DisplayName = "Product Version", Category = "Version", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "ProductName", DisplayName = "Product", Category = "Info", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "CompanyName", DisplayName = "Company", Category = "Info", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "FileDescription", DisplayName = "Description", Category = "Info", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "Copyright", DisplayName = "Copyright", Category = "Info", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "Architecture", DisplayName = "Architecture", Category = "Technical", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "IsSigned", DisplayName = "Signed", Category = "Technical", Type = ContentFieldType.Boolean }
    };
    
    public override async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, ContentFieldValue>();
        
        await Task.Run(() =>
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
                
                if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    values["FileVersion"] = new ContentFieldValue { FieldName = "FileVersion", Value = versionInfo.FileVersion, DisplayValue = versionInfo.FileVersion, Type = ContentFieldType.String };
                
                if (!string.IsNullOrEmpty(versionInfo.ProductVersion))
                    values["ProductVersion"] = new ContentFieldValue { FieldName = "ProductVersion", Value = versionInfo.ProductVersion, DisplayValue = versionInfo.ProductVersion, Type = ContentFieldType.String };
                
                if (!string.IsNullOrEmpty(versionInfo.ProductName))
                    values["ProductName"] = new ContentFieldValue { FieldName = "ProductName", Value = versionInfo.ProductName, DisplayValue = versionInfo.ProductName, Type = ContentFieldType.String };
                
                if (!string.IsNullOrEmpty(versionInfo.CompanyName))
                    values["CompanyName"] = new ContentFieldValue { FieldName = "CompanyName", Value = versionInfo.CompanyName, DisplayValue = versionInfo.CompanyName, Type = ContentFieldType.String };
                
                if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                    values["FileDescription"] = new ContentFieldValue { FieldName = "FileDescription", Value = versionInfo.FileDescription, DisplayValue = versionInfo.FileDescription, Type = ContentFieldType.String };
                
                if (!string.IsNullOrEmpty(versionInfo.LegalCopyright))
                    values["Copyright"] = new ContentFieldValue { FieldName = "Copyright", Value = versionInfo.LegalCopyright, DisplayValue = versionInfo.LegalCopyright, Type = ContentFieldType.String };
            }
            catch
            {
                // Ignore errors
            }
        }, cancellationToken);
        
        return values;
    }
}

/// <summary>
/// Content plugin for archive files.
/// </summary>
public class ArchiveContentPlugin : ContentPlugin
{
    public override string Id => "archive";
    public override string Name => "Archive Properties";
    public override string Description => "Shows archive contents summary, compression ratio, etc.";
    public override int Priority => 60;
    
    public override IReadOnlyList<string> SupportedExtensions => new[]
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz"
    };
    
    public override IReadOnlyList<ContentFieldDefinition> Fields => new[]
    {
        new ContentFieldDefinition { Name = "FileCount", DisplayName = "Files", Category = "Contents", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "FolderCount", DisplayName = "Folders", Category = "Contents", Type = ContentFieldType.Integer },
        new ContentFieldDefinition { Name = "UncompressedSize", DisplayName = "Uncompressed", Category = "Size", Type = ContentFieldType.Size },
        new ContentFieldDefinition { Name = "CompressionRatio", DisplayName = "Ratio", Category = "Size", Type = ContentFieldType.Ratio },
        new ContentFieldDefinition { Name = "ArchiveType", DisplayName = "Type", Category = "Archive", Type = ContentFieldType.String },
        new ContentFieldDefinition { Name = "IsEncrypted", DisplayName = "Encrypted", Category = "Archive", Type = ContentFieldType.Boolean }
    };
    
    public override async Task<IReadOnlyDictionary<string, ContentFieldValue>> GetValuesAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, ContentFieldValue>();
        
        await Task.Run(() =>
        {
            try
            {
                using var archive = SharpCompress.Archives.ArchiveFactory.Open(filePath);
                var entries = archive.Entries.ToList();
                
                var fileCount = entries.Count(e => !e.IsDirectory);
                var folderCount = entries.Count(e => e.IsDirectory);
                var uncompressedSize = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
                var compressedSize = entries.Where(e => !e.IsDirectory).Sum(e => e.CompressedSize > 0 ? e.CompressedSize : e.Size);
                var isEncrypted = entries.Any(e => e.IsEncrypted);
                
                values["FileCount"] = new ContentFieldValue
                {
                    FieldName = "FileCount",
                    Value = fileCount,
                    DisplayValue = fileCount.ToString("N0"),
                    Type = ContentFieldType.Integer
                };
                
                values["FolderCount"] = new ContentFieldValue
                {
                    FieldName = "FolderCount",
                    Value = folderCount,
                    DisplayValue = folderCount.ToString("N0"),
                    Type = ContentFieldType.Integer
                };
                
                values["UncompressedSize"] = new ContentFieldValue
                {
                    FieldName = "UncompressedSize",
                    Value = uncompressedSize,
                    DisplayValue = FormatSize(uncompressedSize),
                    Type = ContentFieldType.Size
                };
                
                var ratio = uncompressedSize > 0 ? (double)compressedSize / uncompressedSize * 100 : 100;
                values["CompressionRatio"] = new ContentFieldValue
                {
                    FieldName = "CompressionRatio",
                    Value = ratio,
                    DisplayValue = $"{ratio:F1}%",
                    Type = ContentFieldType.Ratio
                };
                
                values["IsEncrypted"] = new ContentFieldValue
                {
                    FieldName = "IsEncrypted",
                    Value = isEncrypted,
                    DisplayValue = isEncrypted ? "Yes" : "No",
                    Type = ContentFieldType.Boolean
                };
            }
            catch
            {
                // Ignore errors reading archive
            }
        }, cancellationToken);
        
        return values;
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int index = 0;
        double size = bytes;
        
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }
        
        return index == 0 ? $"{size:N0} {suffixes[index]}" : $"{size:N2} {suffixes[index]}";
    }
}

#endregion
