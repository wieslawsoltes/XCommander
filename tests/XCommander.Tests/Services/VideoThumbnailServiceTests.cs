using XCommander.Services;

namespace XCommander.Tests.Services;

public class VideoThumbnailServiceTests
{
    [Theory]
    [InlineData("test.mp4", true)]
    [InlineData("test.MP4", true)]
    [InlineData("test.avi", true)]
    [InlineData("test.mkv", true)]
    [InlineData("test.mov", true)]
    [InlineData("test.wmv", true)]
    [InlineData("test.webm", true)]
    [InlineData("test.flv", true)]
    [InlineData("test.m4v", true)]
    [InlineData("test.txt", false)]
    [InlineData("test.pdf", false)]
    [InlineData("test.jpg", false)]
    public void IsVideoFile_ReturnsCorrectResult(string path, bool expected)
    {
        var result = VideoThumbnailService.IsVideoFile(path);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public async Task GenerateThumbnailAsync_ReturnsNullForNonExistentFile()
    {
        var service = new VideoThumbnailService();
        var result = await service.GenerateThumbnailAsync("/nonexistent/file.mp4");
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetVideoDurationAsync_ReturnsZeroForNonExistentFile()
    {
        var service = new VideoThumbnailService();
        var result = await service.GetVideoDurationAsync("/nonexistent/file.mp4");
        Assert.Equal(0, result);
    }
    
    [Fact]
    public async Task GetVideoInfoAsync_ReturnsNullForNonExistentFile()
    {
        var service = new VideoThumbnailService();
        var result = await service.GetVideoInfoAsync("/nonexistent/file.mp4");
        Assert.Null(result);
    }
}
