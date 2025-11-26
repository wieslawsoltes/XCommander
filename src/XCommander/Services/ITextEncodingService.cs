// ITextEncodingService.cs - TC-style text character encoding conversion
// Convert between different character sets (UTF-8, Windows-1252, etc.), detect encoding

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Information about a text character encoding
/// </summary>
public record CharacterEncodingInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int CodePage { get; init; }
    public string? WebName { get; init; }
    public bool IsSingleByte { get; init; }
    public bool IsUnicode { get; init; }
    public string? BomBytes { get; init; }
    public string Category { get; init; } = string.Empty;
}

/// <summary>
/// Result of character encoding detection
/// </summary>
public record CharacterEncodingDetectionResult
{
    public CharacterEncodingInfo DetectedEncoding { get; init; } = new();
    public double Confidence { get; init; }
    public bool HasBom { get; init; }
    public string? BomType { get; init; }
    public IReadOnlyList<CharacterEncodingCandidate> Candidates { get; init; } = Array.Empty<CharacterEncodingCandidate>();
}

/// <summary>
/// Candidate character encoding with confidence score
/// </summary>
public record CharacterEncodingCandidate
{
    public CharacterEncodingInfo Encoding { get; init; } = new();
    public double Confidence { get; init; }
    public int ValidCharacters { get; init; }
    public int InvalidCharacters { get; init; }
}

/// <summary>
/// Options for character encoding conversion
/// </summary>
public record CharacterEncodingConversionOptions
{
    /// <summary>
    /// Source encoding (null = auto-detect)
    /// </summary>
    public Encoding? SourceEncoding { get; init; }
    
    /// <summary>
    /// Target encoding
    /// </summary>
    public Encoding TargetEncoding { get; init; } = Encoding.UTF8;
    
    /// <summary>
    /// Write BOM (Byte Order Mark) for Unicode encodings
    /// </summary>
    public bool WriteBom { get; init; } = true;
    
    /// <summary>
    /// Fallback character for unconvertible characters
    /// </summary>
    public char FallbackChar { get; init; } = '?';
    
    /// <summary>
    /// Preserve original line endings
    /// </summary>
    public bool PreserveLineEndings { get; init; } = true;
    
    /// <summary>
    /// Target line ending style (null = preserve original)
    /// </summary>
    public TextLineEndingStyle? TargetLineEnding { get; init; }
    
    /// <summary>
    /// Create backup of original file
    /// </summary>
    public bool CreateBackup { get; init; }
    
    /// <summary>
    /// Backup file extension
    /// </summary>
    public string BackupExtension { get; init; } = ".bak";
}

/// <summary>
/// Line ending styles for text files
/// </summary>
public enum TextLineEndingStyle
{
    /// <summary>Windows (CR+LF)</summary>
    Windows,
    /// <summary>Unix/Linux/macOS (LF)</summary>
    Unix,
    /// <summary>Classic Mac (CR)</summary>
    ClassicMac,
    /// <summary>Mixed - preserve original</summary>
    Mixed
}

/// <summary>
/// Result of character encoding conversion
/// </summary>
public record CharacterEncodingConversionResult
{
    public string FilePath { get; init; } = string.Empty;
    public CharacterEncodingInfo SourceEncoding { get; init; } = new();
    public CharacterEncodingInfo TargetEncoding { get; init; } = new();
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long OriginalSize { get; init; }
    public long ConvertedSize { get; init; }
    public int CharactersConverted { get; init; }
    public int CharactersLost { get; init; }
    public string? BackupPath { get; init; }
    public TextLineEndingStyle DetectedLineEnding { get; init; }
    public TextLineEndingStyle ResultLineEnding { get; init; }
}

/// <summary>
/// Line ending statistics for text files
/// </summary>
public record TextLineEndingStatistics
{
    public int CrLfCount { get; init; }
    public int LfCount { get; init; }
    public int CrCount { get; init; }
    public int TotalLines => CrLfCount + LfCount + CrCount + 1;
    public TextLineEndingStyle DominantStyle { get; init; }
    public bool IsMixed => (CrLfCount > 0 ? 1 : 0) + (LfCount > 0 ? 1 : 0) + (CrCount > 0 ? 1 : 0) > 1;
}

/// <summary>
/// Service for text character encoding detection and conversion
/// </summary>
public interface ITextEncodingService
{
    /// <summary>
    /// Get all supported character encodings
    /// </summary>
    IReadOnlyList<CharacterEncodingInfo> GetSupportedEncodings();
    
    /// <summary>
    /// Get common character encodings (subset for UI)
    /// </summary>
    IReadOnlyList<CharacterEncodingInfo> GetCommonEncodings();
    
    /// <summary>
    /// Get character encoding by name or codepage
    /// </summary>
    CharacterEncodingInfo? GetEncoding(string nameOrCodepage);
    
    /// <summary>
    /// Get character encoding by codepage
    /// </summary>
    CharacterEncodingInfo? GetEncoding(int codepage);
    
    /// <summary>
    /// Detect file character encoding
    /// </summary>
    Task<CharacterEncodingDetectionResult> DetectEncodingAsync(
        string filePath,
        int bytesToRead = 65536,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect character encoding from bytes
    /// </summary>
    CharacterEncodingDetectionResult DetectEncoding(byte[] bytes);
    
    /// <summary>
    /// Convert file character encoding
    /// </summary>
    Task<CharacterEncodingConversionResult> ConvertEncodingAsync(
        string filePath,
        CharacterEncodingConversionOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert file character encoding to new file
    /// </summary>
    Task<CharacterEncodingConversionResult> ConvertEncodingAsync(
        string sourcePath,
        string targetPath,
        CharacterEncodingConversionOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert string between character encodings
    /// </summary>
    string ConvertString(string text, Encoding sourceEncoding, Encoding targetEncoding);
    
    /// <summary>
    /// Convert bytes between character encodings
    /// </summary>
    byte[] ConvertBytes(byte[] bytes, Encoding sourceEncoding, Encoding targetEncoding);
    
    /// <summary>
    /// Detect line endings in text file
    /// </summary>
    Task<TextLineEndingStatistics> DetectLineEndingsAsync(
        string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert line endings in text file
    /// </summary>
    Task<bool> ConvertLineEndingsAsync(
        string filePath,
        TextLineEndingStyle targetStyle,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if character encoding can represent all characters in text
    /// </summary>
    bool CanEncode(string text, Encoding encoding);
    
    /// <summary>
    /// Get characters that cannot be represented in character encoding
    /// </summary>
    IReadOnlyList<char> GetUnrepresentableCharacters(string text, Encoding encoding);
}
