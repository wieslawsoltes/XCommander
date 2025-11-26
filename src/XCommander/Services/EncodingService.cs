using System.Text;

namespace XCommander.Services;

/// <summary>
/// Supported encoding formats
/// </summary>
public enum EncodingFormat
{
    Base64,
    UUEncode,
    XXEncode,
    YEnc,
    QuotedPrintable,
    Hex,
    Rot13,
    Mime
}

/// <summary>
/// Result of encoding/decoding operation
/// </summary>
public class EncodingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public byte[]? Data { get; init; }
    public string? Text { get; init; }
    public long InputSize { get; init; }
    public long OutputSize { get; init; }
    public EncodingFormat Format { get; init; }
}

/// <summary>
/// Progress for encoding operations
/// </summary>
public class EncodingProgress
{
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 0;
}

/// <summary>
/// Service for various text/binary encodings
/// </summary>
public interface IEncodingService
{
    /// <summary>
    /// Encode data to specified format
    /// </summary>
    EncodingResult Encode(byte[] data, EncodingFormat format);
    
    /// <summary>
    /// Decode data from specified format
    /// </summary>
    EncodingResult Decode(string encodedData, EncodingFormat format);
    
    /// <summary>
    /// Encode a file to specified format
    /// </summary>
    Task<EncodingResult> EncodeFileAsync(
        string inputPath,
        string outputPath,
        EncodingFormat format,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Decode a file from specified format
    /// </summary>
    Task<EncodingResult> DecodeFileAsync(
        string inputPath,
        string outputPath,
        EncodingFormat format,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Auto-detect encoding format of a file
    /// </summary>
    EncodingFormat? DetectFormat(string filePath);
    
    /// <summary>
    /// Auto-detect encoding format of text
    /// </summary>
    EncodingFormat? DetectFormat(byte[] data);
    
    /// <summary>
    /// Get file extension for encoded format
    /// </summary>
    string GetFileExtension(EncodingFormat format);
    
    /// <summary>
    /// Get description of encoding format
    /// </summary>
    string GetFormatDescription(EncodingFormat format);
}

public class EncodingService : IEncodingService
{
    // UUEncode character set
    private const string UUEncodeChars = "`!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_";
    
    // XXEncode character set (printable ASCII subset)
    private const string XXEncodeChars = "+-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    
    // yEnc escape character
    private const byte YEncEscape = 0x3D; // '='
    
    public EncodingResult Encode(byte[] data, EncodingFormat format)
    {
        try
        {
            var encoded = format switch
            {
                EncodingFormat.Base64 => EncodeBase64(data),
                EncodingFormat.UUEncode => EncodeUU(data, "file"),
                EncodingFormat.XXEncode => EncodeXX(data, "file"),
                EncodingFormat.YEnc => EncodeYEnc(data, "file"),
                EncodingFormat.QuotedPrintable => EncodeQuotedPrintable(data),
                EncodingFormat.Hex => EncodeHex(data),
                EncodingFormat.Rot13 => EncodeRot13(Encoding.UTF8.GetString(data)),
                EncodingFormat.Mime => EncodeMime(data, "file"),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
            
            return new EncodingResult
            {
                Success = true,
                Text = encoded,
                InputSize = data.Length,
                OutputSize = encoded.Length,
                Format = format
            };
        }
        catch (Exception ex)
        {
            return new EncodingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Format = format
            };
        }
    }
    
    public EncodingResult Decode(string encodedData, EncodingFormat format)
    {
        try
        {
            var decoded = format switch
            {
                EncodingFormat.Base64 => DecodeBase64(encodedData),
                EncodingFormat.UUEncode => DecodeUU(encodedData),
                EncodingFormat.XXEncode => DecodeXX(encodedData),
                EncodingFormat.YEnc => DecodeYEnc(encodedData),
                EncodingFormat.QuotedPrintable => DecodeQuotedPrintable(encodedData),
                EncodingFormat.Hex => DecodeHex(encodedData),
                EncodingFormat.Rot13 => Encoding.UTF8.GetBytes(EncodeRot13(encodedData)), // ROT13 is self-inverse
                EncodingFormat.Mime => DecodeMime(encodedData),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
            
            return new EncodingResult
            {
                Success = true,
                Data = decoded,
                InputSize = encodedData.Length,
                OutputSize = decoded.Length,
                Format = format
            };
        }
        catch (Exception ex)
        {
            return new EncodingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Format = format
            };
        }
    }
    
    public async Task<EncodingResult> EncodeFileAsync(
        string inputPath,
        string outputPath,
        EncodingFormat format,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(inputPath, cancellationToken);
            
            progress?.Report(new EncodingProgress
            {
                BytesProcessed = data.Length / 2,
                TotalBytes = data.Length
            });
            
            var result = Encode(data, format);
            
            if (result.Success && result.Text != null)
            {
                await File.WriteAllTextAsync(outputPath, result.Text, cancellationToken);
            }
            
            progress?.Report(new EncodingProgress
            {
                BytesProcessed = data.Length,
                TotalBytes = data.Length
            });
            
            return result;
        }
        catch (Exception ex)
        {
            return new EncodingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Format = format
            };
        }
    }
    
    public async Task<EncodingResult> DecodeFileAsync(
        string inputPath,
        string outputPath,
        EncodingFormat format,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedText = await File.ReadAllTextAsync(inputPath, cancellationToken);
            
            progress?.Report(new EncodingProgress
            {
                BytesProcessed = encodedText.Length / 2,
                TotalBytes = encodedText.Length
            });
            
            var result = Decode(encodedText, format);
            
            if (result.Success && result.Data != null)
            {
                await File.WriteAllBytesAsync(outputPath, result.Data, cancellationToken);
            }
            
            progress?.Report(new EncodingProgress
            {
                BytesProcessed = encodedText.Length,
                TotalBytes = encodedText.Length
            });
            
            return result;
        }
        catch (Exception ex)
        {
            return new EncodingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Format = format
            };
        }
    }
    
    public EncodingFormat? DetectFormat(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            return DetectFormatFromText(content);
        }
        catch
        {
            return null;
        }
    }
    
    public EncodingFormat? DetectFormat(byte[] data)
    {
        try
        {
            var text = Encoding.UTF8.GetString(data);
            return DetectFormatFromText(text);
        }
        catch
        {
            return null;
        }
    }
    
    public string GetFileExtension(EncodingFormat format)
    {
        return format switch
        {
            EncodingFormat.Base64 => ".b64",
            EncodingFormat.UUEncode => ".uue",
            EncodingFormat.XXEncode => ".xxe",
            EncodingFormat.YEnc => ".yenc",
            EncodingFormat.QuotedPrintable => ".qp",
            EncodingFormat.Hex => ".hex",
            EncodingFormat.Rot13 => ".rot13",
            EncodingFormat.Mime => ".mim",
            _ => ".enc"
        };
    }
    
    public string GetFormatDescription(EncodingFormat format)
    {
        return format switch
        {
            EncodingFormat.Base64 => "Base64 - Standard binary-to-text encoding used in MIME, email attachments",
            EncodingFormat.UUEncode => "UUEncode - Unix-to-Unix encoding, legacy format for email attachments",
            EncodingFormat.XXEncode => "XXEncode - Similar to UUEncode but with safer character set",
            EncodingFormat.YEnc => "yEnc - Binary encoding optimized for Usenet (8-bit clean)",
            EncodingFormat.QuotedPrintable => "Quoted-Printable - Email encoding that preserves readable text",
            EncodingFormat.Hex => "Hexadecimal - Simple hex representation of bytes",
            EncodingFormat.Rot13 => "ROT13 - Simple letter substitution cipher (rotate 13 positions)",
            EncodingFormat.Mime => "MIME - Multipurpose Internet Mail Extensions encoding",
            _ => "Unknown encoding format"
        };
    }
    
    // Base64 encoding/decoding
    private static string EncodeBase64(byte[] data) => Convert.ToBase64String(data);
    private static byte[] DecodeBase64(string data) => Convert.FromBase64String(data.Trim());
    
    // UUEncode
    private static string EncodeUU(byte[] data, string filename)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"begin 644 {filename}");
        
        for (var i = 0; i < data.Length; i += 45)
        {
            var lineLength = Math.Min(45, data.Length - i);
            sb.Append((char)(lineLength + 32));
            
            for (var j = 0; j < lineLength; j += 3)
            {
                var b0 = data[i + j];
                var b1 = (j + 1 < lineLength) ? data[i + j + 1] : (byte)0;
                var b2 = (j + 2 < lineLength) ? data[i + j + 2] : (byte)0;
                
                sb.Append(UUEncodeChars[(b0 >> 2) & 0x3F]);
                sb.Append(UUEncodeChars[((b0 << 4) | (b1 >> 4)) & 0x3F]);
                sb.Append(UUEncodeChars[((b1 << 2) | (b2 >> 6)) & 0x3F]);
                sb.Append(UUEncodeChars[b2 & 0x3F]);
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("`");
        sb.AppendLine("end");
        return sb.ToString();
    }
    
    private static byte[] DecodeUU(string data)
    {
        var lines = data.Split('\n', '\r').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var result = new List<byte>();
        
        foreach (var line in lines)
        {
            if (line.StartsWith("begin ") || line == "end" || line == "`")
                continue;
            
            if (line.Length < 1) continue;
            
            var length = (line[0] - 32) & 0x3F;
            if (length == 0) continue;
            
            for (var i = 1; i < line.Length - 3 && result.Count < length + result.Count; i += 4)
            {
                var c0 = (UUEncodeChars.IndexOf(line[i]) & 0x3F);
                var c1 = (UUEncodeChars.IndexOf(line[i + 1]) & 0x3F);
                var c2 = (UUEncodeChars.IndexOf(line[i + 2]) & 0x3F);
                var c3 = (UUEncodeChars.IndexOf(line[i + 3]) & 0x3F);
                
                result.Add((byte)((c0 << 2) | (c1 >> 4)));
                if (result.Count < length) result.Add((byte)((c1 << 4) | (c2 >> 2)));
                if (result.Count < length) result.Add((byte)((c2 << 6) | c3));
            }
        }
        
        return result.ToArray();
    }
    
    // XXEncode
    private static string EncodeXX(byte[] data, string filename)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"begin 644 {filename}");
        
        for (var i = 0; i < data.Length; i += 45)
        {
            var lineLength = Math.Min(45, data.Length - i);
            sb.Append(XXEncodeChars[lineLength]);
            
            for (var j = 0; j < lineLength; j += 3)
            {
                var b0 = data[i + j];
                var b1 = (j + 1 < lineLength) ? data[i + j + 1] : (byte)0;
                var b2 = (j + 2 < lineLength) ? data[i + j + 2] : (byte)0;
                
                sb.Append(XXEncodeChars[(b0 >> 2) & 0x3F]);
                sb.Append(XXEncodeChars[((b0 << 4) | (b1 >> 4)) & 0x3F]);
                sb.Append(XXEncodeChars[((b1 << 2) | (b2 >> 6)) & 0x3F]);
                sb.Append(XXEncodeChars[b2 & 0x3F]);
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("+");
        sb.AppendLine("end");
        return sb.ToString();
    }
    
    private static byte[] DecodeXX(string data)
    {
        var lines = data.Split('\n', '\r').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var result = new List<byte>();
        
        foreach (var line in lines)
        {
            if (line.StartsWith("begin ") || line == "end" || line == "+")
                continue;
            
            if (line.Length < 1) continue;
            
            var length = XXEncodeChars.IndexOf(line[0]);
            if (length <= 0) continue;
            
            for (var i = 1; i < line.Length - 3; i += 4)
            {
                var c0 = XXEncodeChars.IndexOf(line[i]) & 0x3F;
                var c1 = XXEncodeChars.IndexOf(line[i + 1]) & 0x3F;
                var c2 = XXEncodeChars.IndexOf(line[i + 2]) & 0x3F;
                var c3 = XXEncodeChars.IndexOf(line[i + 3]) & 0x3F;
                
                result.Add((byte)((c0 << 2) | (c1 >> 4)));
                if (result.Count < length) result.Add((byte)((c1 << 4) | (c2 >> 2)));
                if (result.Count < length) result.Add((byte)((c2 << 6) | c3));
            }
        }
        
        return result.ToArray();
    }
    
    // yEnc encoding
    private static string EncodeYEnc(byte[] data, string filename)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=ybegin line=128 size={data.Length} name={filename}");
        
        var lineLength = 0;
        foreach (var b in data)
        {
            var encoded = (byte)((b + 42) % 256);
            
            // Escape special characters
            if (encoded == 0x00 || encoded == 0x0A || encoded == 0x0D || encoded == 0x3D)
            {
                sb.Append((char)YEncEscape);
                sb.Append((char)((encoded + 64) % 256));
                lineLength += 2;
            }
            else
            {
                sb.Append((char)encoded);
                lineLength++;
            }
            
            if (lineLength >= 128)
            {
                sb.AppendLine();
                lineLength = 0;
            }
        }
        
        if (lineLength > 0) sb.AppendLine();
        sb.AppendLine($"=yend size={data.Length}");
        
        return sb.ToString();
    }
    
    private static byte[] DecodeYEnc(string data)
    {
        var result = new List<byte>();
        var inData = false;
        var escaped = false;
        
        foreach (var line in data.Split('\n'))
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("=ybegin"))
            {
                inData = true;
                continue;
            }
            
            if (trimmed.StartsWith("=yend"))
                break;
            
            if (!inData) continue;
            
            foreach (var c in trimmed)
            {
                if (escaped)
                {
                    result.Add((byte)((c - 64 - 42 + 256) % 256));
                    escaped = false;
                }
                else if (c == YEncEscape)
                {
                    escaped = true;
                }
                else
                {
                    result.Add((byte)((c - 42 + 256) % 256));
                }
            }
        }
        
        return result.ToArray();
    }
    
    // Quoted-Printable
    private static string EncodeQuotedPrintable(byte[] data)
    {
        var sb = new StringBuilder();
        var lineLength = 0;
        
        foreach (var b in data)
        {
            if ((b >= 33 && b <= 126 && b != 61) || b == 9 || b == 32)
            {
                if (lineLength >= 73)
                {
                    sb.AppendLine("=");
                    lineLength = 0;
                }
                sb.Append((char)b);
                lineLength++;
            }
            else
            {
                if (lineLength >= 71)
                {
                    sb.AppendLine("=");
                    lineLength = 0;
                }
                sb.Append($"={b:X2}");
                lineLength += 3;
            }
        }
        
        return sb.ToString();
    }
    
    private static byte[] DecodeQuotedPrintable(string data)
    {
        var result = new List<byte>();
        var i = 0;
        var text = data.Replace("=\r\n", "").Replace("=\n", "");
        
        while (i < text.Length)
        {
            if (text[i] == '=' && i + 2 < text.Length)
            {
                var hex = text.Substring(i + 1, 2);
                if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    result.Add(b);
                    i += 3;
                    continue;
                }
            }
            
            result.Add((byte)text[i]);
            i++;
        }
        
        return result.ToArray();
    }
    
    // Hex encoding
    private static string EncodeHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");
    
    private static byte[] DecodeHex(string data)
    {
        var hex = data.Replace(" ", "").Replace("-", "").Replace("\n", "").Replace("\r", "");
        var bytes = new byte[hex.Length / 2];
        
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        
        return bytes;
    }
    
    // ROT13
    private static string EncodeRot13(string text)
    {
        var sb = new StringBuilder();
        
        foreach (var c in text)
        {
            if (c >= 'a' && c <= 'z')
                sb.Append((char)('a' + (c - 'a' + 13) % 26));
            else if (c >= 'A' && c <= 'Z')
                sb.Append((char)('A' + (c - 'A' + 13) % 26));
            else
                sb.Append(c);
        }
        
        return sb.ToString();
    }
    
    // MIME encoding (simplified - base64 with headers)
    private static string EncodeMime(byte[] data, string filename)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: application/octet-stream");
        sb.AppendLine($"Content-Disposition: attachment; filename=\"{filename}\"");
        sb.AppendLine("Content-Transfer-Encoding: base64");
        sb.AppendLine();
        
        var base64 = Convert.ToBase64String(data);
        for (var i = 0; i < base64.Length; i += 76)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(76, base64.Length - i)));
        }
        
        return sb.ToString();
    }
    
    private static byte[] DecodeMime(string data)
    {
        var lines = data.Split('\n');
        var inBody = false;
        var base64Lines = new StringBuilder();
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) && !inBody)
            {
                inBody = true;
                continue;
            }
            
            if (inBody)
            {
                base64Lines.Append(line.Trim());
            }
        }
        
        return Convert.FromBase64String(base64Lines.ToString());
    }
    
    private static EncodingFormat? DetectFormatFromText(string text)
    {
        var trimmed = text.Trim();
        
        if (trimmed.StartsWith("begin ") && trimmed.Contains("\nend"))
        {
            // Check if it's XXEncode or UUEncode
            if (trimmed.Contains("+\n") || trimmed.Contains("+\r"))
                return EncodingFormat.XXEncode;
            return EncodingFormat.UUEncode;
        }
        
        if (trimmed.StartsWith("=ybegin"))
            return EncodingFormat.YEnc;
        
        if (trimmed.StartsWith("MIME-Version:"))
            return EncodingFormat.Mime;
        
        // Check for Base64 (only valid base64 chars)
        var base64Regex = new System.Text.RegularExpressions.Regex(@"^[A-Za-z0-9+/\s=]+$");
        if (base64Regex.IsMatch(trimmed) && trimmed.Length > 20)
            return EncodingFormat.Base64;
        
        // Check for Quoted-Printable (contains =XX patterns)
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"=[0-9A-F]{2}"))
            return EncodingFormat.QuotedPrintable;
        
        // Check for Hex (only hex chars)
        var hexRegex = new System.Text.RegularExpressions.Regex(@"^[0-9A-Fa-f\s-]+$");
        if (hexRegex.IsMatch(trimmed) && trimmed.Length > 10)
            return EncodingFormat.Hex;
        
        return null;
    }
}
