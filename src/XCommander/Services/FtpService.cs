using FluentFTP;
using XCommander.Models;

namespace XCommander.Services;

/// <summary>
/// FTP encryption mode for secure connections
/// </summary>
public enum FtpEncryptionMode
{
    /// <summary>
    /// Plain FTP without encryption
    /// </summary>
    None,
    
    /// <summary>
    /// Implicit FTPS (connects on port 990 with immediate SSL/TLS)
    /// </summary>
    Implicit,
    
    /// <summary>
    /// Explicit FTPS (uses AUTH TLS command after connection)
    /// </summary>
    Explicit
}

public interface IFtpService
{
    Task<bool> ConnectAsync(string host, int port, string username, string password, FtpEncryptionMode encryption = FtpEncryptionMode.None);
    Task DisconnectAsync();
    Task<IEnumerable<FtpItem>> ListDirectoryAsync(string path);
    Task<bool> DownloadFileAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> UploadFileAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> CreateDirectoryAsync(string path);
    Task<bool> DeleteFileAsync(string path);
    Task<bool> DeleteDirectoryAsync(string path);
    Task<bool> RenameAsync(string oldPath, string newPath);
    Task<bool> ResumeDownloadAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> ResumeUploadAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(string remotePath);
    bool IsConnected { get; }
    string CurrentDirectory { get; }
    FtpEncryptionMode EncryptionMode { get; }
}

public class FtpItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime DateModified { get; set; }
    public string Permissions { get; set; } = string.Empty;
    
    public string Icon => IsDirectory ? "📁" : "📄";
    public string DisplaySize => IsDirectory ? "<DIR>" : FormatSize(Size);
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
}

public class FtpService : IFtpService
{
    private AsyncFtpClient? _client;
    private string _currentDirectory = "/";
    private FtpEncryptionMode _encryptionMode = FtpEncryptionMode.None;
    
    public bool IsConnected => _client?.IsConnected ?? false;
    public string CurrentDirectory => _currentDirectory;
    public FtpEncryptionMode EncryptionMode => _encryptionMode;
    
    public async Task<bool> ConnectAsync(string host, int port, string username, string password, FtpEncryptionMode encryption = FtpEncryptionMode.None)
    {
        try
        {
            await DisconnectAsync();
            
            _encryptionMode = encryption;
            _client = new AsyncFtpClient(host, username, password, port);
            
            // Configure encryption mode
            _client.Config.EncryptionMode = encryption switch
            {
                FtpEncryptionMode.Implicit => FluentFTP.FtpEncryptionMode.Implicit,
                FtpEncryptionMode.Explicit => FluentFTP.FtpEncryptionMode.Explicit,
                _ => FluentFTP.FtpEncryptionMode.None
            };
            
            // Accept self-signed certificates for testing (can be made configurable)
            _client.Config.ValidateAnyCertificate = true;
            
            // Set timeout
            _client.Config.ConnectTimeout = 10000;
            _client.Config.DataConnectionConnectTimeout = 10000;
            _client.Config.DataConnectionReadTimeout = 30000;
            
            await _client.Connect();
            _currentDirectory = "/";
            
            return _client.IsConnected;
        }
        catch
        {
            await DisconnectAsync();
            return false;
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            try
            {
                if (_client.IsConnected)
                {
                    await _client.Disconnect();
                }
            }
            finally
            {
                _client.Dispose();
                _client = null;
            }
        }
        _currentDirectory = "/";
    }
    
    public async Task<IEnumerable<FtpItem>> ListDirectoryAsync(string path)
    {
        var items = new List<FtpItem>();
        
        if (_client == null || !_client.IsConnected)
            return items;
            
        try
        {
            _currentDirectory = path;
            var listing = await _client.GetListing(path);
            
            foreach (var item in listing)
            {
                if (item.Name == "." || item.Name == "..")
                    continue;
                    
                items.Add(new FtpItem
                {
                    Name = item.Name,
                    FullPath = item.FullName,
                    IsDirectory = item.Type == FtpObjectType.Directory,
                    Size = item.Size,
                    DateModified = item.Modified,
                    Permissions = item.RawPermissions ?? string.Empty
                });
            }
        }
        catch
        {
            // Return empty list on error
        }
        
        return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name);
    }
    
    public async Task<bool> DownloadFileAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var fileName = Path.GetFileName(remotePath);
            
            // Get file size for progress reporting
            var fileSize = await _client.GetFileSize(remotePath, -1, cancellationToken);
            
            var ftpProgress = progress != null 
                ? new Progress<FtpProgress>(p => progress.Report(new FileOperationProgress
                {
                    CurrentItem = fileName,
                    ProcessedBytes = (long)(p.Progress * fileSize / 100),
                    TotalBytes = fileSize,
                    TransferSpeedBytesPerSecond = (long)p.TransferSpeed
                }))
                : null;
            
            var status = await _client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite, FtpVerify.None, ftpProgress, cancellationToken);
            return status == FtpStatus.Success;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> UploadFileAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var fileInfo = new FileInfo(localPath);
            var fileName = fileInfo.Name;
            var fileSize = fileInfo.Length;
            
            var ftpProgress = progress != null 
                ? new Progress<FtpProgress>(p => progress.Report(new FileOperationProgress
                {
                    CurrentItem = fileName,
                    ProcessedBytes = (long)(p.Progress * fileSize / 100),
                    TotalBytes = fileSize,
                    TransferSpeedBytesPerSecond = (long)p.TransferSpeed
                }))
                : null;
            
            var status = await _client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, ftpProgress, cancellationToken);
            return status == FtpStatus.Success;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> ResumeDownloadAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var fileName = Path.GetFileName(remotePath);
            var fileSize = await _client.GetFileSize(remotePath, -1, cancellationToken);
            
            var ftpProgress = progress != null 
                ? new Progress<FtpProgress>(p => progress.Report(new FileOperationProgress
                {
                    CurrentItem = fileName,
                    ProcessedBytes = (long)(p.Progress * fileSize / 100),
                    TotalBytes = fileSize,
                    TransferSpeedBytesPerSecond = (long)p.TransferSpeed
                }))
                : null;
            
            // Use Resume mode for partial downloads
            var status = await _client.DownloadFile(localPath, remotePath, FtpLocalExists.Resume, FtpVerify.None, ftpProgress, cancellationToken);
            return status == FtpStatus.Success;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> ResumeUploadAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var fileInfo = new FileInfo(localPath);
            var fileName = fileInfo.Name;
            var fileSize = fileInfo.Length;
            
            var ftpProgress = progress != null 
                ? new Progress<FtpProgress>(p => progress.Report(new FileOperationProgress
                {
                    CurrentItem = fileName,
                    ProcessedBytes = (long)(p.Progress * fileSize / 100),
                    TotalBytes = fileSize,
                    TransferSpeedBytesPerSecond = (long)p.TransferSpeed
                }))
                : null;
            
            // Use Resume mode for partial uploads
            var status = await _client.UploadFile(localPath, remotePath, FtpRemoteExists.Resume, true, FtpVerify.None, ftpProgress, cancellationToken);
            return status == FtpStatus.Success;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<long> GetFileSizeAsync(string remotePath)
    {
        if (_client == null || !_client.IsConnected)
            return -1;
            
        try
        {
            return await _client.GetFileSize(remotePath);
        }
        catch
        {
            return -1;
        }
    }
    
    public async Task<bool> CreateDirectoryAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            return await _client.CreateDirectory(path);
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> DeleteFileAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await _client.DeleteFile(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> DeleteDirectoryAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await _client.DeleteDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RenameAsync(string oldPath, string newPath)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await _client.Rename(oldPath, newPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
