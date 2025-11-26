using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;

namespace XCommander.Services;

/// <summary>
/// Service for Right-to-Left (RTL) text support.
/// </summary>
public class RtlSupportService
{
    // RTL Unicode ranges
    private static readonly (int Start, int End)[] RtlRanges = 
    {
        (0x0590, 0x05FF),  // Hebrew
        (0x0600, 0x06FF),  // Arabic
        (0x0700, 0x074F),  // Syriac
        (0x0750, 0x077F),  // Arabic Supplement
        (0x0780, 0x07BF),  // Thaana
        (0x07C0, 0x07FF),  // NKo
        (0x0800, 0x083F),  // Samaritan
        (0x08A0, 0x08FF),  // Arabic Extended-A
        (0xFB00, 0xFDFF),  // Hebrew/Arabic Presentation Forms
        (0xFE70, 0xFEFF),  // Arabic Presentation Forms-B
    };
    
    // RTL language culture codes
    private static readonly string[] RtlLanguages =
    {
        "ar", // Arabic
        "he", // Hebrew
        "fa", // Persian (Farsi)
        "ur", // Urdu
        "yi", // Yiddish
        "ps", // Pashto
        "ku", // Kurdish
        "sd", // Sindhi
        "ug", // Uyghur
    };
    
    /// <summary>
    /// Checks if a character is an RTL character.
    /// </summary>
    public static bool IsRtlChar(char c)
    {
        var code = (int)c;
        foreach (var range in RtlRanges)
        {
            if (code >= range.Start && code <= range.End)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Checks if text contains any RTL characters.
    /// </summary>
    public static bool ContainsRtl(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        
        foreach (var c in text)
        {
            if (IsRtlChar(c))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Checks if text is predominantly RTL.
    /// </summary>
    public static bool IsPredominantlyRtl(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        
        int rtlCount = 0;
        int ltrCount = 0;
        
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                if (IsRtlChar(c))
                    rtlCount++;
                else
                    ltrCount++;
            }
        }
        
        return rtlCount > ltrCount;
    }
    
    /// <summary>
    /// Gets the appropriate FlowDirection for text.
    /// </summary>
    public static FlowDirection GetFlowDirection(string? text)
    {
        return IsPredominantlyRtl(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
    
    /// <summary>
    /// Checks if the current culture is RTL.
    /// </summary>
    public static bool IsCurrentCultureRtl()
    {
        var culture = CultureInfo.CurrentUICulture;
        return IsRtlCulture(culture);
    }
    
    /// <summary>
    /// Checks if a culture is RTL.
    /// </summary>
    public static bool IsRtlCulture(CultureInfo culture)
    {
        var langCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return RtlLanguages.Contains(langCode);
    }
    
    /// <summary>
    /// Gets FlowDirection based on current UI culture.
    /// </summary>
    public static FlowDirection GetUICultureFlowDirection()
    {
        return IsCurrentCultureRtl() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
    
    /// <summary>
    /// Detects the script type of a filename.
    /// </summary>
    public static ScriptType DetectScript(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return ScriptType.Unknown;
        
        int arabicCount = 0;
        int hebrewCount = 0;
        int latinCount = 0;
        int otherCount = 0;
        
        foreach (var c in text)
        {
            if (!char.IsLetter(c))
                continue;
            
            var code = (int)c;
            
            if (code >= 0x0600 && code <= 0x06FF || code >= 0xFE70 && code <= 0xFEFF)
                arabicCount++;
            else if (code >= 0x0590 && code <= 0x05FF || code >= 0xFB1D && code <= 0xFB4F)
                hebrewCount++;
            else if (code >= 0x0041 && code <= 0x007A || code >= 0x00C0 && code <= 0x024F)
                latinCount++;
            else
                otherCount++;
        }
        
        var max = Math.Max(Math.Max(arabicCount, hebrewCount), Math.Max(latinCount, otherCount));
        
        if (max == 0)
            return ScriptType.Unknown;
        if (max == arabicCount)
            return ScriptType.Arabic;
        if (max == hebrewCount)
            return ScriptType.Hebrew;
        if (max == latinCount)
            return ScriptType.Latin;
        
        return ScriptType.Other;
    }
    
    /// <summary>
    /// Wraps text with Unicode directional markers for proper display in mixed text.
    /// </summary>
    public static string WrapWithDirectionalMarkers(string text, bool forceRtl = false)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        var isRtl = forceRtl || IsPredominantlyRtl(text);
        
        if (isRtl)
        {
            // Right-to-Left Embedding (RLE) + text + Pop Directional Formatting (PDF)
            return "\u202B" + text + "\u202C";
        }
        else
        {
            // Left-to-Right Embedding (LRE) + text + Pop Directional Formatting (PDF)
            return "\u202A" + text + "\u202C";
        }
    }
    
    /// <summary>
    /// Normalizes file path for display in RTL context.
    /// </summary>
    public static string NormalizePathForRtl(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        
        // Add Left-to-Right Mark (LRM) after path separators to ensure correct display
        return path.Replace("/", "/\u200E").Replace("\\", "\\\u200E");
    }
}

/// <summary>
/// Script types for text detection.
/// </summary>
public enum ScriptType
{
    Unknown,
    Latin,
    Arabic,
    Hebrew,
    Other
}
