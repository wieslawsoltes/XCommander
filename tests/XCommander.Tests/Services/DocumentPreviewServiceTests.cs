using XCommander.Services;

namespace XCommander.Tests.Services;

public class DocumentPreviewServiceTests
{
    [Theory]
    [InlineData("test.pdf", true)]
    [InlineData("test.PDF", true)]
    [InlineData("test.txt", false)]
    [InlineData("test.docx", false)]
    public void IsPdfFile_ReturnsCorrectResult(string path, bool expected)
    {
        var result = DocumentPreviewService.IsPdfFile(path);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("test.docx", true)]
    [InlineData("test.doc", true)]
    [InlineData("test.xlsx", true)]
    [InlineData("test.xls", true)]
    [InlineData("test.pptx", true)]
    [InlineData("test.ppt", true)]
    [InlineData("test.odt", true)]
    [InlineData("test.ods", true)]
    [InlineData("test.odp", true)]
    [InlineData("test.rtf", true)]
    [InlineData("test.pdf", false)]
    [InlineData("test.txt", false)]
    public void IsOfficeDocument_ReturnsCorrectResult(string path, bool expected)
    {
        var result = DocumentPreviewService.IsOfficeDocument(path);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("test.pdf", true)]
    [InlineData("test.docx", true)]
    [InlineData("test.xlsx", true)]
    [InlineData("test.txt", false)]
    [InlineData("test.exe", false)]
    public void CanPreview_ReturnsCorrectResult(string path, bool expected)
    {
        var result = DocumentPreviewService.CanPreview(path);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public async Task GetPdfInfoAsync_ReturnsNullForNonExistentFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.GetPdfInfoAsync("/nonexistent/file.pdf");
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetPdfInfoAsync_ReturnsNullForNonPdfFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.GetPdfInfoAsync("/some/file.txt");
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetOfficeDocumentInfoAsync_ReturnsNullForNonExistentFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.GetOfficeDocumentInfoAsync("/nonexistent/file.docx");
        Assert.Null(result);
    }
    
    [Fact]
    public async Task ExtractTextAsync_ReturnsEmptyForNonExistentFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.ExtractTextAsync("/nonexistent/file.pdf");
        Assert.Equal(string.Empty, result);
    }
    
    [Fact]
    public async Task ExtractTextAsync_ReturnsEmptyForUnsupportedFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.ExtractTextAsync("/some/file.exe");
        Assert.Equal(string.Empty, result);
    }
    
    [Fact]
    public async Task GeneratePdfThumbnailAsync_ReturnsNullForNonExistentFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.GeneratePdfThumbnailAsync("/nonexistent/file.pdf");
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GeneratePdfThumbnailAsync_ReturnsNullForNonPdfFile()
    {
        var service = new DocumentPreviewService();
        var result = await service.GeneratePdfThumbnailAsync("/some/file.txt");
        Assert.Null(result);
    }
}
