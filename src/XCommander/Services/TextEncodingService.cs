// TextEncodingService.cs - TC-style text character encoding conversion implementation
// Full encoding detection, conversion, and line ending handling

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public sealed class TextEncodingService : ITextEncodingService
{
    private readonly ILongPathService _longPathService;
    private readonly Dictionary<int, CharacterEncodingInfo> _encodings;
    private readonly List<CharacterEncodingInfo> _commonEncodings;
    
    public TextEncodingService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
        _encodings = InitializeEncodings();
        _commonEncodings = InitializeCommonEncodings();
    }
    
    private static Dictionary<int, CharacterEncodingInfo> InitializeEncodings()
    {
        var encodings = new Dictionary<int, CharacterEncodingInfo>();
        
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            foreach (var enc in Encoding.GetEncodings())
            {
                try
                {
                    var encoding = enc.GetEncoding();
                    encodings[enc.CodePage] = new CharacterEncodingInfo
                    {
                        Name = enc.Name,
                        DisplayName = enc.DisplayName,
                        CodePage = enc.CodePage,
                        WebName = encoding.WebName,
                        IsSingleByte = encoding.IsSingleByte,
                        IsUnicode = encoding is UnicodeEncoding or UTF8Encoding or UTF32Encoding,
                        Category = GetEncodingCategory(enc.CodePage)
                    };
                }
                catch { }
            }
        }
        catch { }
        
        return encodings;
    }
    
    private static string GetEncodingCategory(int codePage)
    {
        return codePage switch
        {
            65001 or 65000 or 1200 or 1201 or 12000 or 12001 => "Unicode",
            1252 or 1250 or 1251 or 1253 or 1254 or 1255 or 1256 or 1257 or 1258 => "Windows",
            28591 or 28592 or 28593 or 28594 or 28595 or 28596 or 28597 or 28598 or 28599 or 28605 => "ISO",
            20127 => "ASCII",
            437 or 850 or 852 or 855 or 857 or 860 or 861 or 862 or 863 or 865 or 866 => "DOS/OEM",
            932 or 936 or 949 or 950 or 20932 or 51932 or 51936 or 51949 or 52936 or 54936 => "East Asian",
            _ => "Other"
        };
    }
    
    private static List<CharacterEncodingInfo> InitializeCommonEncodings()
    {
        return new List<CharacterEncodingInfo>
        {
            new() { Name = "utf-8", DisplayName = "UTF-8", CodePage = 65001, IsUnicode = true, Category = "Unicode" },
            new() { Name = "utf-8-bom", DisplayName = "UTF-8 with BOM", CodePage = 65001, IsUnicode = true, BomBytes = "EFBBBF", Category = "Unicode" },
            new() { Name = "utf-16", DisplayName = "UTF-16 LE", CodePage = 1200, IsUnicode = true, BomBytes = "FFFE", Category = "Unicode" },
            new() { Name = "utf-16be", DisplayName = "UTF-16 BE", CodePage = 1201, IsUnicode = true, BomBytes = "FEFF", Category = "Unicode" },
            new() { Name = "ascii", DisplayName = "ASCII", CodePage = 20127, IsSingleByte = true, Category = "ASCII" },
            new() { Name = "windows-1252", DisplayName = "Windows-1252 (Western)", CodePage = 1252, IsSingleByte = true, Category = "Windows" },
            new() { Name = "windows-1250", DisplayName = "Windows-1250 (Central European)", CodePage = 1250, IsSingleByte = true, Category = "Windows" },
            new() { Name = "windows-1251", DisplayName = "Windows-1251 (Cyrillic)", CodePage = 1251, IsSingleByte = true, Category = "Windows" },
            new() { Name = "iso-8859-1", DisplayName = "ISO-8859-1 (Latin 1)", CodePage = 28591, IsSingleByte = true, Category = "ISO" },
            new() { Name = "iso-8859-2", DisplayName = "ISO-8859-2 (Latin 2)", CodePage = 28592, IsSingleByte = true, Category = "ISO" },
            new() { Name = "shift_jis", DisplayName = "Shift-JIS (Japanese)", CodePage = 932, Category = "East Asian" },
            new() { Name = "gb2312", DisplayName = "GB2312 (Simplified Chinese)", CodePage = 936, Category = "East Asian" },
            new() { Name = "ibm437", DisplayName = "DOS/OEM (US)", CodePage = 437, IsSingleByte = true, Category = "DOS/OEM" }
        };
    }
    
    public IReadOnlyList<CharacterEncodingInfo> GetSupportedEncodings() => _encodings.Values.ToList();
    
    public IReadOnlyList<CharacterEncodingInfo> GetCommonEncodings() => _commonEncodings;
    
    public CharacterEncodingInfo? GetEncoding(string nameOrCodepage)
    {
        if (int.TryParse(nameOrCodepage, out var codePage))
            return GetEncoding(codePage);
        
        var normalized = nameOrCodepage.ToLowerInvariant().Replace("-", "").Replace("_", "");
        return _encodings.Values.FirstOrDefault(e => 
            e.Name.Replace("-", "").Replace("_", "").ToLowerInvariant() == normalized ||
            e.WebName?.Replace("-", "").Replace("_", "").ToLowerInvariant() == normalized);
    }
    
    public CharacterEncodingInfo? GetEncoding(int codepage)
    {
        return _encodings.TryGetValue(codepage, out var info) ? info : null;
    }
    
    public async Task<CharacterEncodingDetectionResult> DetectEncodingAsync(
        string filePath,
        int bytesToRead = 65536,
        CancellationToken cancellationToken = default)
    {
        var longPath = _longPathService.NormalizePath(filePath);
        var fileInfo = new FileInfo(longPath);
        
        var readSize = (int)Math.Min(bytesToRead, fileInfo.Length);
        var buffer = new byte[readSize];
        
        await using var stream = new FileStream(longPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);
        
        if (bytesRead < readSize)
            Array.Resize(ref buffer, bytesRead);
        
        return DetectEncoding(buffer);
    }
    
    public CharacterEncodingDetectionResult DetectEncoding(byte[] bytes)
    {
        var (bomEncoding, bomType) = DetectBom(bytes);
        if (bomEncoding != null)
        {
            var encInfo = GetEncoding(bomEncoding.CodePage) ?? new CharacterEncodingInfo
            {
                Name = bomEncoding.WebName,
                DisplayName = bomEncoding.EncodingName,
                CodePage = bomEncoding.CodePage,
                IsUnicode = true
            };
            
            return new CharacterEncodingDetectionResult
            {
                DetectedEncoding = encInfo,
                Confidence = 1.0,
                HasBom = true,
                BomType = bomType,
                Candidates = new[] { new CharacterEncodingCandidate { Encoding = encInfo, Confidence = 1.0 } }
            };
        }
        
        var candidates = new List<CharacterEncodingCandidate>();
        
        // Check UTF-8
        var utf8Score = ScoreUtf8(bytes);
        if (utf8Score > 0)
        {
            var utf8Info = GetEncoding(65001) ?? _commonEncodings.First(e => e.CodePage == 65001);
            candidates.Add(new CharacterEncodingCandidate
            {
                Encoding = utf8Info,
                Confidence = utf8Score,
                ValidCharacters = CountValidUtf8Chars(bytes)
            });
        }
        
        // Check ASCII
        if (bytes.All(b => b <= 127))
        {
            var asciiInfo = GetEncoding(20127) ?? new CharacterEncodingInfo
            {
                Name = "ascii",
                DisplayName = "ASCII",
                CodePage = 20127,
                IsSingleByte = true
            };
            candidates.Add(new CharacterEncodingCandidate
            {
                Encoding = asciiInfo,
                Confidence = 1.0,
                ValidCharacters = bytes.Length
            });
        }
        
        // Try Windows encodings
        foreach (var enc in new[] { 1252, 1250, 1251 })
        {
            try
            {
                var encoding = Encoding.GetEncoding(enc);
                var score = ScoreEncoding(bytes, encoding);
                if (score > 0.5)
                {
                    var info = GetEncoding(enc);
                    if (info != null)
                        candidates.Add(new CharacterEncodingCandidate { Encoding = info, Confidence = score });
                }
            }
            catch { }
        }
        
        candidates = candidates.OrderByDescending(c => c.Confidence).ToList();
        
        var best = candidates.FirstOrDefault() ?? new CharacterEncodingCandidate
        {
            Encoding = GetEncoding(65001) ?? _commonEncodings.First(e => e.CodePage == 65001),
            Confidence = 0.5
        };
        
        return new CharacterEncodingDetectionResult
        {
            DetectedEncoding = best.Encoding,
            Confidence = best.Confidence,
            HasBom = false,
            Candidates = candidates
        };
    }
    
    private static (Encoding?, string?) DetectBom(byte[] bytes)
    {
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return (Encoding.UTF32, "UTF-32 LE");
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return (new UTF32Encoding(true, true), "UTF-32 BE");
        }
        
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8, "UTF-8");
        
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE) return (Encoding.Unicode, "UTF-16 LE");
            if (bytes[0] == 0xFE && bytes[1] == 0xFF) return (Encoding.BigEndianUnicode, "UTF-16 BE");
        }
        
        return (null, null);
    }
    
    private static double ScoreUtf8(byte[] bytes)
    {
        int valid = 0, invalid = 0, multibyte = 0;
        
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            
            if (b <= 0x7F) { valid++; }
            else if ((b & 0xE0) == 0xC0 && i + 1 < bytes.Length && (bytes[i + 1] & 0xC0) == 0x80)
            {
                valid += 2; multibyte++; i++;
            }
            else if ((b & 0xF0) == 0xE0 && i + 2 < bytes.Length && 
                     (bytes[i + 1] & 0xC0) == 0x80 && (bytes[i + 2] & 0xC0) == 0x80)
            {
                valid += 3; multibyte++; i += 2;
            }
            else if ((b & 0xF8) == 0xF0 && i + 3 < bytes.Length &&
                     (bytes[i + 1] & 0xC0) == 0x80 && (bytes[i + 2] & 0xC0) == 0x80 && (bytes[i + 3] & 0xC0) == 0x80)
            {
                valid += 4; multibyte++; i += 3;
            }
            else { invalid++; }
        }
        
        if (invalid > 0) return 0;
        if (multibyte == 0) return 0.5;
        return 0.9 + (0.1 * multibyte / bytes.Length);
    }
    
    private static int CountValidUtf8Chars(byte[] bytes)
    {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b <= 0x7F) count++;
            else if ((b & 0xE0) == 0xC0) { count++; i++; }
            else if ((b & 0xF0) == 0xE0) { count++; i += 2; }
            else if ((b & 0xF8) == 0xF0) { count++; i += 3; }
        }
        return count;
    }
    
    private static double ScoreEncoding(byte[] bytes, Encoding encoding)
    {
        try
        {
            var decoded = encoding.GetString(bytes);
            int printable = 0, control = 0, replacement = 0;
            
            foreach (var c in decoded)
            {
                if (c == '\uFFFD') replacement++;
                else if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') control++;
                else printable++;
            }
            
            if (replacement > 0) return 0;
            return (double)printable / (printable + control + 1);
        }
        catch { return 0; }
    }
    
    public async Task<CharacterEncodingConversionResult> ConvertEncodingAsync(
        string filePath,
        CharacterEncodingConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var tempPath = filePath + ".tmp";
        
        try
        {
            var result = await ConvertEncodingAsync(filePath, tempPath, options, cancellationToken);
            
            if (result.Success)
            {
                var longPath = _longPathService.NormalizePath(filePath);
                var longTempPath = _longPathService.NormalizePath(tempPath);
                
                if (options.CreateBackup)
                {
                    var backupPath = filePath + options.BackupExtension;
                    File.Move(longPath, _longPathService.NormalizePath(backupPath), true);
                    result = result with { BackupPath = backupPath };
                }
                else
                {
                    File.Delete(longPath);
                }
                
                File.Move(longTempPath, longPath);
                result = result with { FilePath = filePath };
            }
            
            return result;
        }
        finally
        {
            var longTempPath = _longPathService.NormalizePath(tempPath);
            if (File.Exists(longTempPath))
                try { File.Delete(longTempPath); } catch { }
        }
    }
    
    public async Task<CharacterEncodingConversionResult> ConvertEncodingAsync(
        string sourcePath,
        string targetPath,
        CharacterEncodingConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var longSourcePath = _longPathService.NormalizePath(sourcePath);
        var longTargetPath = _longPathService.NormalizePath(targetPath);
        
        Encoding sourceEncoding;
        CharacterEncodingInfo sourceInfo;
        
        if (options.SourceEncoding != null)
        {
            sourceEncoding = options.SourceEncoding;
            sourceInfo = GetEncoding(sourceEncoding.CodePage) ?? new CharacterEncodingInfo
            {
                Name = sourceEncoding.WebName,
                DisplayName = sourceEncoding.EncodingName,
                CodePage = sourceEncoding.CodePage
            };
        }
        else
        {
            var detection = await DetectEncodingAsync(sourcePath, cancellationToken: cancellationToken);
            sourceEncoding = Encoding.GetEncoding(detection.DetectedEncoding.CodePage);
            sourceInfo = detection.DetectedEncoding;
        }
        
        var targetEncoding = options.TargetEncoding;
        var targetInfo = GetEncoding(targetEncoding.CodePage) ?? new CharacterEncodingInfo
        {
            Name = targetEncoding.WebName,
            DisplayName = targetEncoding.EncodingName,
            CodePage = targetEncoding.CodePage
        };
        
        var sourceBytes = await File.ReadAllBytesAsync(longSourcePath, cancellationToken);
        var originalSize = sourceBytes.Length;
        
        var bomLength = GetBomLength(sourceBytes);
        if (bomLength > 0)
            sourceBytes = sourceBytes.Skip(bomLength).ToArray();
        
        var text = sourceEncoding.GetString(sourceBytes);
        
        var lineStats = DetectLineEndings(text);
        var targetLineEnding = options.TargetLineEnding ?? lineStats.DominantStyle;
        
        if (options.TargetLineEnding.HasValue && !options.PreserveLineEndings)
            text = ConvertLineEndings(text, targetLineEnding);
        
        var unrepresentable = GetUnrepresentableCharacters(text, targetEncoding);
        int charsLost = 0;
        
        if (unrepresentable.Count > 0)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (unrepresentable.Contains(c))
                {
                    sb.Append(options.FallbackChar);
                    charsLost++;
                }
                else
                {
                    sb.Append(c);
                }
            }
            text = sb.ToString();
        }
        
        byte[] targetBytes;
        if (options.WriteBom && targetInfo.IsUnicode)
        {
            var preamble = targetEncoding.GetPreamble();
            var content = targetEncoding.GetBytes(text);
            targetBytes = new byte[preamble.Length + content.Length];
            preamble.CopyTo(targetBytes, 0);
            content.CopyTo(targetBytes, preamble.Length);
        }
        else
        {
            targetBytes = targetEncoding.GetBytes(text);
        }
        
        await File.WriteAllBytesAsync(longTargetPath, targetBytes, cancellationToken);
        
        return new CharacterEncodingConversionResult
        {
            FilePath = targetPath,
            SourceEncoding = sourceInfo,
            TargetEncoding = targetInfo,
            Success = true,
            OriginalSize = originalSize,
            ConvertedSize = targetBytes.Length,
            CharactersConverted = text.Length,
            CharactersLost = charsLost,
            DetectedLineEnding = lineStats.DominantStyle,
            ResultLineEnding = targetLineEnding
        };
    }
    
    private static int GetBomLength(byte[] bytes)
    {
        if (bytes.Length >= 4)
        {
            if ((bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00) ||
                (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF))
                return 4;
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return 3;
        if (bytes.Length >= 2 &&
            ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            return 2;
        return 0;
    }
    
    private static TextLineEndingStatistics DetectLineEndings(string text)
    {
        int crlf = 0, lf = 0, cr = 0;
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                else { cr++; }
            }
            else if (text[i] == '\n') { lf++; }
        }
        
        var dominant = TextLineEndingStyle.Unix;
        var max = lf;
        if (crlf > max) { dominant = TextLineEndingStyle.Windows; max = crlf; }
        if (cr > max) { dominant = TextLineEndingStyle.ClassicMac; }
        
        return new TextLineEndingStatistics
        {
            CrLfCount = crlf,
            LfCount = lf,
            CrCount = cr,
            DominantStyle = dominant
        };
    }
    
    private static string ConvertLineEndings(string text, TextLineEndingStyle style)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return style switch
        {
            TextLineEndingStyle.Windows => normalized.Replace("\n", "\r\n"),
            TextLineEndingStyle.ClassicMac => normalized.Replace("\n", "\r"),
            _ => normalized
        };
    }
    
    public string ConvertString(string text, Encoding sourceEncoding, Encoding targetEncoding)
    {
        var bytes = sourceEncoding.GetBytes(text);
        return targetEncoding.GetString(bytes);
    }
    
    public byte[] ConvertBytes(byte[] bytes, Encoding sourceEncoding, Encoding targetEncoding)
    {
        var text = sourceEncoding.GetString(bytes);
        return targetEncoding.GetBytes(text);
    }
    
    public async Task<TextLineEndingStatistics> DetectLineEndingsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(_longPathService.NormalizePath(filePath), cancellationToken);
        return DetectLineEndings(text);
    }
    
    public async Task<bool> ConvertLineEndingsAsync(
        string filePath,
        TextLineEndingStyle targetStyle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var longPath = _longPathService.NormalizePath(filePath);
            var text = await File.ReadAllTextAsync(longPath, cancellationToken);
            var converted = ConvertLineEndings(text, targetStyle);
            await File.WriteAllTextAsync(longPath, converted, cancellationToken);
            return true;
        }
        catch { return false; }
    }
    
    public bool CanEncode(string text, Encoding encoding)
    {
        try
        {
            var encoder = encoding.GetEncoder();
            encoder.Fallback = EncoderFallback.ExceptionFallback;
            encoder.GetByteCount(text.ToCharArray(), 0, text.Length, true);
            return true;
        }
        catch { return false; }
    }
    
    public IReadOnlyList<char> GetUnrepresentableCharacters(string text, Encoding encoding)
    {
        var result = new HashSet<char>();
        
        foreach (var c in text.Distinct())
        {
            try
            {
                var encoder = encoding.GetEncoder();
                encoder.Fallback = EncoderFallback.ExceptionFallback;
                encoder.GetByteCount(new[] { c }, 0, 1, true);
            }
            catch { result.Add(c); }
        }
        
        return result.ToList();
    }
}
