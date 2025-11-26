// IFTPClientService.cs - TC-style FTP/SFTP client service
// Full-featured FTP/SFTP client with bookmarks, queue, and resume support

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// FTP transfer modes
/// </summary>
public enum FtpTransferMode
{
    Binary,
    ASCII,
    Auto     // Detect based on file extension
}

/// <summary>
/// FTP connection types
/// </summary>
public enum FtpConnectionType
{
    FTP,         // Plain FTP
    FTPS,        // FTP over TLS (explicit)
    FTPES,       // FTP over TLS (implicit)
    SFTP         // SSH File Transfer Protocol
}

/// <summary>
/// FTP connection status
/// </summary>
public enum FtpConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Authenticating,
    Ready,
    Busy,
    Error
}

/// <summary>
/// FTP connection settings
/// </summary>
public record FtpConnectionSettings
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 21;
    public FtpConnectionType ConnectionType { get; init; } = FtpConnectionType.FTP;
    public string? Username { get; init; }
    public string? Password { get; init; }  // Encrypted
    public string? PrivateKeyPath { get; init; }  // For SFTP
    public string? PrivateKeyPassphrase { get; init; }
    public string? InitialRemotePath { get; init; }
    public string? InitialLocalPath { get; init; }
    public FtpTransferMode TransferMode { get; init; } = FtpTransferMode.Binary;
    public bool PassiveMode { get; init; } = true;
    public int Timeout { get; init; } = 30;  // seconds
    public bool KeepAlive { get; init; } = true;
    public int KeepAliveInterval { get; init; } = 60;  // seconds
    public bool UseProxy { get; init; }
    public FtpProxySettings? ProxySettings { get; init; }
    public bool ValidateCertificate { get; init; } = true;
    public string? ClientCertificatePath { get; init; }
    public int MaxConnections { get; init; } = 2;  // For parallel transfers
    public bool EnableCompression { get; init; }
    public string? Encoding { get; init; } = "UTF-8";
    public DateTime? LastConnected { get; init; }
    public string? Notes { get; init; }
    public Dictionary<string, string> CustomProperties { get; init; } = new();
}

/// <summary>
/// FTP proxy settings
/// </summary>
public record FtpProxySettings
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public FtpProxyType Type { get; init; }
}

/// <summary>
/// FTP proxy types
/// </summary>
public enum FtpProxyType
{
    None,
    HTTP,
    SOCKS4,
    SOCKS5
}

/// <summary>
/// FTP file/directory listing entry
/// </summary>
public record FtpListEntry
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime Modified { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsSymlink { get; init; }
    public string? LinkTarget { get; init; }
    public string? Permissions { get; init; }    // Unix-style: rwxr-xr-x
    public string? Owner { get; init; }
    public string? Group { get; init; }
    public int? PermissionValue { get; init; }   // Octal: 755
}

/// <summary>
/// FTP transfer progress information
/// </summary>
public record FtpTransferProgress
{
    public string? CurrentFile { get; init; }
    public long TotalBytes { get; init; }
    public long TransferredBytes { get; init; }
    public double SpeedBytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public int TotalFiles { get; init; }
    public int TransferredFiles { get; init; }
    public double PercentComplete => TotalBytes > 0 ? TransferredBytes * 100.0 / TotalBytes : 0;
}

/// <summary>
/// FTP transfer item in queue
/// </summary>
public record FtpTransferItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string LocalPath { get; init; } = string.Empty;
    public string RemotePath { get; init; } = string.Empty;
    public bool IsUpload { get; init; }
    public long Size { get; init; }
    public long TransferredBytes { get; init; }
    public FtpTransferItemStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// FTP transfer item status
/// </summary>
public enum FtpTransferItemStatus
{
    Pending,
    Transferring,
    Paused,
    Completed,
    Failed,
    Skipped,
    Cancelled
}

/// <summary>
/// Bookmark (saved connection)
/// </summary>
public record FtpBookmark
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? FolderPath { get; init; }  // For organizing bookmarks
    public FtpConnectionSettings Settings { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? LastUsedAt { get; init; }
    public int UseCount { get; init; }
    public bool IsFavorite { get; init; }
    public string? IconPath { get; init; }
}

/// <summary>
/// FTP session information
/// </summary>
public record FtpSessionInfo
{
    public string SessionId { get; init; } = string.Empty;
    public FtpConnectionSettings Settings { get; init; } = new();
    public FtpConnectionStatus Status { get; init; }
    public string CurrentRemotePath { get; init; } = "/";
    public DateTime? ConnectedAt { get; init; }
    public string? ServerType { get; init; }
    public IReadOnlyList<string> ServerFeatures { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Progress callback for FTP transfers
/// </summary>
public delegate void FtpProgressCallback(FtpTransferProgress progress);

/// <summary>
/// Service for FTP/SFTP operations
/// </summary>
public interface IFTPClientService
{
    // ======= Connection Management =======
    
    /// <summary>
    /// Connect to FTP server
    /// </summary>
    Task<FtpSessionInfo> ConnectAsync(FtpConnectionSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from server
    /// </summary>
    Task DisconnectAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get connection status
    /// </summary>
    FtpConnectionStatus GetConnectionStatus(string sessionId);
    
    /// <summary>
    /// Get active sessions
    /// </summary>
    IReadOnlyList<FtpSessionInfo> GetActiveSessions();
    
    /// <summary>
    /// Reconnect to server
    /// </summary>
    Task<bool> ReconnectAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test connection settings
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(FtpConnectionSettings settings, CancellationToken cancellationToken = default);
    
    // ======= Directory Operations =======
    
    /// <summary>
    /// List directory contents
    /// </summary>
    Task<IReadOnlyList<FtpListEntry>> ListDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current directory
    /// </summary>
    Task<string> GetCurrentDirectoryAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Change current directory
    /// </summary>
    Task<bool> ChangeDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create directory
    /// </summary>
    Task<bool> CreateDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove directory
    /// </summary>
    Task<bool> RemoveDirectoryAsync(string sessionId, string path, bool recursive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if path exists
    /// </summary>
    Task<bool> ExistsAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file info
    /// </summary>
    Task<FtpListEntry?> GetFileInfoAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    // ======= File Operations =======
    
    /// <summary>
    /// Download file
    /// </summary>
    Task<bool> DownloadFileAsync(
        string sessionId,
        string remotePath,
        string localPath,
        FtpProgressCallback? progress = null,
        bool resume = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Upload file
    /// </summary>
    Task<bool> UploadFileAsync(
        string sessionId,
        string localPath,
        string remotePath,
        FtpProgressCallback? progress = null,
        bool resume = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download multiple files
    /// </summary>
    Task<int> DownloadFilesAsync(
        string sessionId,
        IEnumerable<(string Remote, string Local)> files,
        FtpProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Upload multiple files
    /// </summary>
    Task<int> UploadFilesAsync(
        string sessionId,
        IEnumerable<(string Local, string Remote)> files,
        FtpProgressCallback? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete file
    /// </summary>
    Task<bool> DeleteFileAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rename file or directory
    /// </summary>
    Task<bool> RenameAsync(string sessionId, string oldPath, string newPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy file on server (if supported)
    /// </summary>
    Task<bool> CopyAsync(string sessionId, string sourcePath, string destPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set file permissions (chmod)
    /// </summary>
    Task<bool> SetPermissionsAsync(string sessionId, string path, int permissions, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file size
    /// </summary>
    Task<long> GetFileSizeAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file modification time
    /// </summary>
    Task<DateTime?> GetFileTimeAsync(string sessionId, string path, CancellationToken cancellationToken = default);
    
    // ======= Transfer Queue =======
    
    /// <summary>
    /// Add transfer to queue
    /// </summary>
    Task<FtpTransferItem> QueueTransferAsync(string sessionId, string localPath, string remotePath, bool isUpload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple transfers to queue
    /// </summary>
    Task<IReadOnlyList<FtpTransferItem>> QueueTransfersAsync(
        string sessionId,
        IEnumerable<(string Local, string Remote, bool IsUpload)> transfers,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get queue items
    /// </summary>
    IReadOnlyList<FtpTransferItem> GetQueueItems(string sessionId);
    
    /// <summary>
    /// Start queue processing
    /// </summary>
    Task StartQueueAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause queue processing
    /// </summary>
    Task PauseQueueAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear completed/failed items from queue
    /// </summary>
    Task ClearQueueAsync(string sessionId, bool clearAll = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove item from queue
    /// </summary>
    Task<bool> RemoveFromQueueAsync(string sessionId, string itemId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retry failed item
    /// </summary>
    Task<bool> RetryTransferAsync(string sessionId, string itemId, CancellationToken cancellationToken = default);
    
    // ======= Bookmarks =======
    
    /// <summary>
    /// Get all bookmarks
    /// </summary>
    IReadOnlyList<FtpBookmark> GetBookmarks();
    
    /// <summary>
    /// Get bookmark by ID
    /// </summary>
    FtpBookmark? GetBookmark(string bookmarkId);
    
    /// <summary>
    /// Save bookmark
    /// </summary>
    Task<FtpBookmark> SaveBookmarkAsync(FtpBookmark bookmark, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete bookmark
    /// </summary>
    Task<bool> DeleteBookmarkAsync(string bookmarkId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect using bookmark
    /// </summary>
    Task<FtpSessionInfo> ConnectFromBookmarkAsync(string bookmarkId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import bookmarks from file
    /// </summary>
    Task<IReadOnlyList<FtpBookmark>> ImportBookmarksAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export bookmarks to file
    /// </summary>
    Task ExportBookmarksAsync(string filePath, IEnumerable<string>? bookmarkIds = null, CancellationToken cancellationToken = default);
    
    // ======= Raw Commands =======
    
    /// <summary>
    /// Execute raw FTP command
    /// </summary>
    Task<string> ExecuteCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get server features (FEAT response)
    /// </summary>
    Task<IReadOnlyList<string>> GetServerFeaturesAsync(string sessionId, CancellationToken cancellationToken = default);
    
    // ======= Events =======
    
    /// <summary>
    /// Connection status changed
    /// </summary>
    event EventHandler<FtpConnectionEventArgs>? ConnectionStatusChanged;
    
    /// <summary>
    /// Transfer progress updated
    /// </summary>
    event EventHandler<FtpTransferProgressEventArgs>? TransferProgressChanged;
    
    /// <summary>
    /// Transfer completed
    /// </summary>
    event EventHandler<FtpTransferCompletedEventArgs>? TransferCompleted;
    
    /// <summary>
    /// Directory listing received
    /// </summary>
    event EventHandler<FtpListEventArgs>? DirectoryListReceived;
}

/// <summary>
/// FTP connection event args
/// </summary>
public class FtpConnectionEventArgs : EventArgs
{
    public string SessionId { get; }
    public FtpConnectionStatus Status { get; }
    public string? ErrorMessage { get; }
    
    public FtpConnectionEventArgs(string sessionId, FtpConnectionStatus status, string? errorMessage = null)
    {
        SessionId = sessionId;
        Status = status;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// FTP transfer progress event args
/// </summary>
public class FtpTransferProgressEventArgs : EventArgs
{
    public string SessionId { get; }
    public FtpTransferItem Item { get; }
    public FtpTransferProgress Progress { get; }
    
    public FtpTransferProgressEventArgs(string sessionId, FtpTransferItem item, FtpTransferProgress progress)
    {
        SessionId = sessionId;
        Item = item;
        Progress = progress;
    }
}

/// <summary>
/// FTP transfer completed event args
/// </summary>
public class FtpTransferCompletedEventArgs : EventArgs
{
    public string SessionId { get; }
    public FtpTransferItem Item { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }
    
    public FtpTransferCompletedEventArgs(string sessionId, FtpTransferItem item, bool success, string? errorMessage = null)
    {
        SessionId = sessionId;
        Item = item;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// FTP directory list event args
/// </summary>
public class FtpListEventArgs : EventArgs
{
    public string SessionId { get; }
    public string Path { get; }
    public IReadOnlyList<FtpListEntry> Entries { get; }
    
    public FtpListEventArgs(string sessionId, string path, IReadOnlyList<FtpListEntry> entries)
    {
        SessionId = sessionId;
        Path = path;
        Entries = entries;
    }
}
