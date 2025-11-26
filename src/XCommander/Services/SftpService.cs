using Renci.SshNet;
using Renci.SshNet.Sftp;
using XCommander.Models;

namespace XCommander.Services;

public interface ISftpService
{
    Task<bool> ConnectAsync(string host, int port, string username, string password, string? privateKeyPath = null);
    Task DisconnectAsync();
    Task<IEnumerable<SftpItem>> ListDirectoryAsync(string path);
    Task<bool> DownloadFileAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> UploadFileAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> CreateDirectoryAsync(string path);
    Task<bool> DeleteFileAsync(string path);
    Task<bool> DeleteDirectoryAsync(string path);
    Task<bool> RenameAsync(string oldPath, string newPath);
    bool IsConnected { get; }
    string CurrentDirectory { get; }
}

public class SftpItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsSymLink { get; set; }
    public long Size { get; set; }
    public DateTime DateModified { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    
    public string Icon => IsDirectory ? "📁" : IsSymLink ? "🔗" : "📄";
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

public class SftpService : ISftpService, IDisposable
{
    private SftpClient? _client;
    private string _currentDirectory = "/";
    
    public bool IsConnected => _client?.IsConnected ?? false;
    public string CurrentDirectory => _currentDirectory;
    
    public async Task<bool> ConnectAsync(string host, int port, string username, string password, string? privateKeyPath = null)
    {
        try
        {
            // Disconnect if already connected
            await DisconnectAsync();
            
            ConnectionInfo connectionInfo;
            
            if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
            {
                // Use private key authentication
                var privateKeyFile = new PrivateKeyFile(privateKeyPath);
                connectionInfo = new ConnectionInfo(host, port, username,
                    new PrivateKeyAuthenticationMethod(username, privateKeyFile));
            }
            else if (!string.IsNullOrEmpty(privateKeyPath))
            {
                // Private key with passphrase (password field used as passphrase)
                var privateKeyFile = new PrivateKeyFile(privateKeyPath, password);
                connectionInfo = new ConnectionInfo(host, port, username,
                    new PrivateKeyAuthenticationMethod(username, privateKeyFile));
            }
            else
            {
                // Password authentication
                connectionInfo = new ConnectionInfo(host, port, username,
                    new PasswordAuthenticationMethod(username, password));
            }
            
            _client = new SftpClient(connectionInfo);
            
            await Task.Run(() => _client.Connect());
            _currentDirectory = _client.WorkingDirectory;
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP Connect error: {ex.Message}");
            return false;
        }
    }
    
    public Task DisconnectAsync()
    {
        if (_client != null)
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
            _client.Dispose();
            _client = null;
        }
        _currentDirectory = "/";
        return Task.CompletedTask;
    }
    
    public async Task<IEnumerable<SftpItem>> ListDirectoryAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return [];
            
        try
        {
            var items = new List<SftpItem>();
            
            var files = await Task.Run(() => _client.ListDirectory(path));
            
            foreach (var file in files)
            {
                // Skip . and ..
                if (file.Name == "." || file.Name == "..")
                    continue;
                    
                items.Add(new SftpItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = file.IsDirectory,
                    IsSymLink = file.IsSymbolicLink,
                    Size = file.Length,
                    DateModified = file.LastWriteTime,
                    Permissions = GetPermissionsString(file),
                    Owner = file.UserId.ToString(),
                    Group = file.GroupId.ToString()
                });
            }
            
            _currentDirectory = path;
            return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP ListDirectory error: {ex.Message}");
            return [];
        }
    }
    
    private static string GetPermissionsString(ISftpFile file)
    {
        var permissions = new char[10];
        
        // File type
        permissions[0] = file.IsDirectory ? 'd' : file.IsSymbolicLink ? 'l' : '-';
        
        // Owner permissions
        permissions[1] = (file.Attributes.OwnerCanRead) ? 'r' : '-';
        permissions[2] = (file.Attributes.OwnerCanWrite) ? 'w' : '-';
        permissions[3] = (file.Attributes.OwnerCanExecute) ? 'x' : '-';
        
        // Group permissions
        permissions[4] = (file.Attributes.GroupCanRead) ? 'r' : '-';
        permissions[5] = (file.Attributes.GroupCanWrite) ? 'w' : '-';
        permissions[6] = (file.Attributes.GroupCanExecute) ? 'x' : '-';
        
        // Others permissions
        permissions[7] = (file.Attributes.OthersCanRead) ? 'r' : '-';
        permissions[8] = (file.Attributes.OthersCanWrite) ? 'w' : '-';
        permissions[9] = (file.Attributes.OthersCanExecute) ? 'x' : '-';
        
        return new string(permissions);
    }
    
    public async Task<bool> DownloadFileAsync(string remotePath, string localPath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var fileInfo = _client.Get(remotePath);
            var totalBytes = fileInfo.Length;
            long downloadedBytes = 0;
            
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            
            using var localStream = File.Create(localPath);
            
            await Task.Run(() =>
            {
                _client.DownloadFile(remotePath, localStream, downloaded =>
                {
                    downloadedBytes = (long)downloaded;
                    progress?.Report(new FileOperationProgress
                    {
                        CurrentItem = Path.GetFileName(remotePath),
                        ProcessedBytes = downloadedBytes,
                        TotalBytes = totalBytes
                    });
                });
            }, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP Download error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> UploadFileAsync(string localPath, string remotePath, IProgress<FileOperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            var localInfo = new FileInfo(localPath);
            var totalBytes = localInfo.Length;
            long uploadedBytes = 0;
            
            using var localStream = File.OpenRead(localPath);
            
            await Task.Run(() =>
            {
                _client.UploadFile(localStream, remotePath, uploaded =>
                {
                    uploadedBytes = (long)uploaded;
                    progress?.Report(new FileOperationProgress
                    {
                        CurrentItem = Path.GetFileName(localPath),
                        ProcessedBytes = uploadedBytes,
                        TotalBytes = totalBytes
                    });
                });
            }, cancellationToken);
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP Upload error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> CreateDirectoryAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await Task.Run(() => _client.CreateDirectory(path));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP CreateDirectory error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> DeleteFileAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await Task.Run(() => _client.DeleteFile(path));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP DeleteFile error: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> DeleteDirectoryAsync(string path)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await Task.Run(() => DeleteDirectoryRecursive(path));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP DeleteDirectory error: {ex.Message}");
            return false;
        }
    }
    
    private void DeleteDirectoryRecursive(string path)
    {
        if (_client == null) return;
        
        foreach (var file in _client.ListDirectory(path))
        {
            if (file.Name == "." || file.Name == "..")
                continue;
                
            if (file.IsDirectory)
            {
                DeleteDirectoryRecursive(file.FullName);
            }
            else
            {
                _client.DeleteFile(file.FullName);
            }
        }
        
        _client.DeleteDirectory(path);
    }
    
    public async Task<bool> RenameAsync(string oldPath, string newPath)
    {
        if (_client == null || !_client.IsConnected)
            return false;
            
        try
        {
            await Task.Run(() => _client.RenameFile(oldPath, newPath));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SFTP Rename error: {ex.Message}");
            return false;
        }
    }
    
    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
