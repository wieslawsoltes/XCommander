using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class EncodingToolViewModelTests
{
    private readonly EncodingToolViewModel _viewModel;
    
    public EncodingToolViewModelTests()
    {
        _viewModel = new EncodingToolViewModel();
    }
    
    [Fact]
    public void EncodeText_Base64_EncodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.Base64;
        _viewModel.IsDecodeMode = false;
        
        // Act
        _viewModel.InputText = "Hello, World!";
        
        // Assert
        Assert.Equal("SGVsbG8sIFdvcmxkIQ==", _viewModel.OutputText);
    }
    
    [Fact]
    public void DecodeText_Base64_DecodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.Base64;
        _viewModel.IsDecodeMode = true;
        
        // Act
        _viewModel.InputText = "SGVsbG8sIFdvcmxkIQ==";
        
        // Assert
        Assert.Equal("Hello, World!", _viewModel.OutputText);
    }
    
    [Fact]
    public void EncodeText_UrlEncode_EncodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.UrlEncode;
        _viewModel.IsDecodeMode = false;
        
        // Act
        _viewModel.InputText = "Hello World & More";
        
        // Assert - Uri.EscapeDataString uses %20 for spaces
        Assert.Contains("%20", _viewModel.OutputText);
        Assert.Contains("%26", _viewModel.OutputText);
    }
    
    [Fact]
    public void DecodeText_UrlEncode_DecodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.UrlEncode;
        _viewModel.IsDecodeMode = true;
        
        // Act - Uri.UnescapeDataString handles %20
        _viewModel.InputText = "Hello%20World%20%26%20More";
        
        // Assert
        Assert.Equal("Hello World & More", _viewModel.OutputText);
    }
    
    [Fact]
    public void EncodeText_HtmlEncode_EncodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.HtmlEncode;
        _viewModel.IsDecodeMode = false;
        
        // Act
        _viewModel.InputText = "<div>Hello & World</div>";
        
        // Assert
        Assert.Contains("&lt;", _viewModel.OutputText);
        Assert.Contains("&gt;", _viewModel.OutputText);
        Assert.Contains("&amp;", _viewModel.OutputText);
    }
    
    [Fact]
    public void DecodeText_HtmlEncode_DecodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.HtmlEncode;
        _viewModel.IsDecodeMode = true;
        
        // Act
        _viewModel.InputText = "&lt;div&gt;Hello &amp; World&lt;/div&gt;";
        
        // Assert
        Assert.Equal("<div>Hello & World</div>", _viewModel.OutputText);
    }
    
    [Fact]
    public void EncodeText_Hex_EncodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.Hex;
        _viewModel.IsDecodeMode = false;
        
        // Act
        _viewModel.InputText = "ABC";
        
        // Assert
        Assert.Equal("414243", _viewModel.OutputText);
    }
    
    [Fact]
    public void DecodeText_Hex_DecodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.Hex;
        _viewModel.IsDecodeMode = true;
        
        // Act
        _viewModel.InputText = "414243";
        
        // Assert
        Assert.Equal("ABC", _viewModel.OutputText);
    }
    
    [Theory]
    [InlineData(EncodingMode.Base64)]
    [InlineData(EncodingMode.UrlEncode)]
    [InlineData(EncodingMode.HtmlEncode)]
    [InlineData(EncodingMode.Hex)]
    public void EncodeAndDecode_ReturnsOriginalText(EncodingMode mode)
    {
        // Arrange
        var originalText = "Test String 123!";
        _viewModel.SelectedMode = mode;
        _viewModel.IsDecodeMode = false;
        
        // Act - Encode
        _viewModel.InputText = originalText;
        var encoded = _viewModel.OutputText;
        
        // Act - Decode
        _viewModel.IsDecodeMode = true;
        _viewModel.InputText = encoded;
        
        // Assert
        Assert.Equal(originalText, _viewModel.OutputText);
    }
    
    [Fact]
    public void SwapInputOutput_SwapsValues()
    {
        // Arrange
        _viewModel.InputText = "Input";
        // Wait for transform to complete
        var output = _viewModel.OutputText;
        
        // Act
        _viewModel.SwapInputOutputCommand.Execute(null);
        
        // Assert
        Assert.Equal(output, _viewModel.InputText);
    }
    
    [Fact]
    public void ClearAll_ClearsBothFields()
    {
        // Arrange
        _viewModel.InputText = "Input";
        // Wait for transform
        _ = _viewModel.OutputText;
        
        // Act
        _viewModel.ClearAllCommand.Execute(null);
        
        // Assert
        Assert.Equal(string.Empty, _viewModel.InputText);
        Assert.Equal(string.Empty, _viewModel.OutputText);
    }
    
    [Fact]
    public void AvailableModes_ContainsExpectedModes()
    {
        // Assert
        Assert.Contains("Base64", _viewModel.AvailableModes);
        Assert.Contains("URL Encode", _viewModel.AvailableModes);
        Assert.Contains("HTML Encode", _viewModel.AvailableModes);
        Assert.Contains("Hex", _viewModel.AvailableModes);
        Assert.Contains("UUEncode", _viewModel.AvailableModes);
    }
    
    [Fact]
    public void EncodeText_UuEncode_EncodesCorrectly()
    {
        // Arrange
        _viewModel.SelectedMode = EncodingMode.UuEncode;
        _viewModel.IsDecodeMode = false;
        
        // Act
        _viewModel.InputText = "Hello";
        
        // Assert
        Assert.StartsWith("begin ", _viewModel.OutputText);
        Assert.Contains("end", _viewModel.OutputText);
    }
    
    [Fact]
    public void EmptyInput_ProducesEmptyOutput()
    {
        // Arrange
        _viewModel.InputText = "test";
        
        // Act
        _viewModel.InputText = string.Empty;
        
        // Assert
        Assert.Equal(string.Empty, _viewModel.OutputText);
    }
}
