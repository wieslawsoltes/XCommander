using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace XCommander.Services;

/// <summary>
/// Service for generating video thumbnails using ffmpeg.
/// </summary>
public class VideoThumbnailService
{
    private static readonly string[] VideoExtensions = 
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", 
        ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".m2ts"
    };
    
    private string? _ffmpegPath;
    
    public VideoThumbnailService()
    {
        _ffmpegPath = FindFfmpeg();
    }
    
    /// <summary>
    /// Checks if ffmpeg is available on the system.
    /// </summary>
    public bool IsAvailable => !string.IsNullOrEmpty(_ffmpegPath);
    
    /// <summary>
    /// Checks if a file is a video file based on extension.
    /// </summary>
    public static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return VideoExtensions.Contains(ext);
    }
    
    /// <summary>
    /// Generates a thumbnail for a video file.
    /// </summary>
    /// <param name="videoPath">Path to the video file</param>
    /// <param name="maxSize">Maximum size of the thumbnail (width or height)</param>
    /// <param name="position">Position in the video to capture (default: 10%)</param>
    /// <returns>Bitmap thumbnail or null if generation fails</returns>
    public async Task<Bitmap?> GenerateThumbnailAsync(string videoPath, int maxSize = 128, double position = 0.1)
    {
        if (!IsAvailable || !File.Exists(videoPath))
            return null;
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"xc_thumb_{Guid.NewGuid()}.jpg");
        
        try
        {
            // Get video duration first
            var duration = await GetVideoDurationAsync(videoPath);
            var seekTime = duration > 0 ? TimeSpan.FromSeconds(duration * position) : TimeSpan.FromSeconds(5);
            
            // Generate thumbnail using ffmpeg
            var args = $"-y -ss {seekTime:hh\\:mm\\:ss} -i \"{videoPath}\" -vframes 1 -vf \"scale='min({maxSize},iw)':'min({maxSize},ih)':force_original_aspect_ratio=decrease\" \"{tempFile}\"";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath!,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0 && File.Exists(tempFile))
            {
                await using var stream = File.OpenRead(tempFile);
                return new Bitmap(stream);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Video thumbnail generation failed: {ex.Message}");
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch { }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the duration of a video file in seconds.
    /// </summary>
    public async Task<double> GetVideoDurationAsync(string videoPath)
    {
        if (!IsAvailable || !File.Exists(videoPath))
            return 0;
        
        try
        {
            var ffprobePath = GetFfprobePath();
            if (string.IsNullOrEmpty(ffprobePath))
                return 0;
            
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (double.TryParse(output.Trim(), out var duration))
                return duration;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get video duration: {ex.Message}");
        }
        
        return 0;
    }
    
    /// <summary>
    /// Gets video information (duration, resolution, codec).
    /// </summary>
    public async Task<VideoInfo?> GetVideoInfoAsync(string videoPath)
    {
        if (!IsAvailable || !File.Exists(videoPath))
            return null;
        
        try
        {
            var ffprobePath = GetFfprobePath();
            if (string.IsNullOrEmpty(ffprobePath))
                return null;
            
            var args = $"-v error -select_streams v:0 -show_entries stream=width,height,codec_name,duration,r_frame_rate -of json \"{videoPath}\"";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            var json = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            // Parse JSON manually to avoid dependency
            var info = new VideoInfo();
            
            if (json.Contains("\"width\":"))
            {
                var widthMatch = System.Text.RegularExpressions.Regex.Match(json, "\"width\":\\s*(\\d+)");
                if (widthMatch.Success)
                    info.Width = int.Parse(widthMatch.Groups[1].Value);
            }
            
            if (json.Contains("\"height\":"))
            {
                var heightMatch = System.Text.RegularExpressions.Regex.Match(json, "\"height\":\\s*(\\d+)");
                if (heightMatch.Success)
                    info.Height = int.Parse(heightMatch.Groups[1].Value);
            }
            
            if (json.Contains("\"codec_name\":"))
            {
                var codecMatch = System.Text.RegularExpressions.Regex.Match(json, "\"codec_name\":\\s*\"([^\"]+)\"");
                if (codecMatch.Success)
                    info.Codec = codecMatch.Groups[1].Value;
            }
            
            // Get duration from format
            info.Duration = await GetVideoDurationAsync(videoPath);
            
            return info;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get video info: {ex.Message}");
        }
        
        return null;
    }
    
    private static string? FindFfmpeg()
    {
        // Check common locations
        var possiblePaths = new List<string>();
        
        if (OperatingSystem.IsWindows())
        {
            possiblePaths.AddRange(new[]
            {
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe"),
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            possiblePaths.AddRange(new[]
            {
                "/opt/homebrew/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/usr/bin/ffmpeg"
            });
        }
        else // Linux
        {
            possiblePaths.AddRange(new[]
            {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg"
            });
        }
        
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        
        // Try to find in PATH
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where" : "which",
                    Arguments = "ffmpeg",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            
            if (!string.IsNullOrEmpty(output) && File.Exists(output.Split('\n')[0]))
                return output.Split('\n')[0].Trim();
        }
        catch { }
        
        return null;
    }
    
    private string? GetFfprobePath()
    {
        if (string.IsNullOrEmpty(_ffmpegPath))
            return null;
        
        var dir = Path.GetDirectoryName(_ffmpegPath);
        var ffprobe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        
        if (!string.IsNullOrEmpty(dir))
        {
            var ffprobePath = Path.Combine(dir, ffprobe);
            if (File.Exists(ffprobePath))
                return ffprobePath;
        }
        
        // Try same methods as ffmpeg
        return FindFfprobe();
    }
    
    private static string? FindFfprobe()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where" : "which",
                    Arguments = "ffprobe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            
            if (!string.IsNullOrEmpty(output) && File.Exists(output.Split('\n')[0]))
                return output.Split('\n')[0].Trim();
        }
        catch { }
        
        return null;
    }
}

/// <summary>
/// Video file information.
/// </summary>
public class VideoInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public string Codec { get; set; } = string.Empty;
    
    public string Resolution => $"{Width}x{Height}";
    public string DurationDisplay => TimeSpan.FromSeconds(Duration).ToString(@"hh\:mm\:ss");
}
