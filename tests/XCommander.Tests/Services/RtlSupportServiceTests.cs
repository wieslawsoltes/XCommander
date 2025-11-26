using XCommander.Services;

namespace XCommander.Tests.Services;

public class RtlSupportServiceTests
{
    [Theory]
    [InlineData("א", true)]  // Hebrew letter Aleph
    [InlineData("ش", true)]  // Arabic letter Sheen
    [InlineData("A", false)] // Latin letter A
    [InlineData("1", false)] // Digit
    public void IsRtlChar_ReturnsCorrectResult(string input, bool expected)
    {
        var result = RtlSupportService.IsRtlChar(input[0]);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello World", false)]
    [InlineData("שלום עולם", true)]      // Hebrew: "Hello World"
    [InlineData("مرحبا بالعالم", true)]  // Arabic: "Hello World"
    [InlineData("Hello שלום", true)]     // Mixed: Contains RTL
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsRtl_ReturnsCorrectResult(string? input, bool expected)
    {
        var result = RtlSupportService.ContainsRtl(input);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello World", false)]
    [InlineData("שלום עולם", true)]        // Hebrew text (4+4=8 Hebrew chars)
    [InlineData("مرحبا بالعالم", true)]    // Arabic text
    [InlineData("Hello שלום", false)]      // 5 Latin vs 4 Hebrew - Latin wins
    [InlineData("Hi שלום עולם", true)]     // 2 Latin vs 8 Hebrew - Hebrew wins
    [InlineData("", false)]
    public void IsPredominantlyRtl_ReturnsCorrectResult(string input, bool expected)
    {
        var result = RtlSupportService.IsPredominantlyRtl(input);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello World", Avalonia.Media.FlowDirection.LeftToRight)]
    [InlineData("שלום עולם", Avalonia.Media.FlowDirection.RightToLeft)]
    [InlineData("مرحبا", Avalonia.Media.FlowDirection.RightToLeft)]
    public void GetFlowDirection_ReturnsCorrectResult(string input, Avalonia.Media.FlowDirection expected)
    {
        var result = RtlSupportService.GetFlowDirection(input);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("Hello", ScriptType.Latin)]
    [InlineData("שלום", ScriptType.Hebrew)]
    [InlineData("مرحبا", ScriptType.Arabic)]
    [InlineData("", ScriptType.Unknown)]
    [InlineData("123", ScriptType.Unknown)]
    public void DetectScript_ReturnsCorrectResult(string input, ScriptType expected)
    {
        var result = RtlSupportService.DetectScript(input);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void WrapWithDirectionalMarkers_AddsCorrectMarkers()
    {
        var rtlText = "שלום";
        var ltrText = "Hello";
        
        var wrappedRtl = RtlSupportService.WrapWithDirectionalMarkers(rtlText);
        var wrappedLtr = RtlSupportService.WrapWithDirectionalMarkers(ltrText);
        
        // RLE marker for RTL
        Assert.StartsWith("\u202B", wrappedRtl);
        Assert.EndsWith("\u202C", wrappedRtl);
        
        // LRE marker for LTR
        Assert.StartsWith("\u202A", wrappedLtr);
        Assert.EndsWith("\u202C", wrappedLtr);
    }
    
    [Fact]
    public void NormalizePathForRtl_AddsLrmMarkers()
    {
        var path = "/Users/test/folder";
        var normalized = RtlSupportService.NormalizePathForRtl(path);
        
        // Should contain LRM markers after path separators
        Assert.Contains("\u200E", normalized);
    }
}
