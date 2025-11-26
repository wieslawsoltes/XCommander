// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for WebDAV operations.
/// Similar to Total Commander's WebDAV client.
/// </summary>
public interface IWebDAVService
{
    /// <summary>
    /// Gets registered WebDAV connections.
    /// </summary>
    IReadOnlyList<WebDAVConnection> Connections { get; }
    
    /// <summary>
    /// Adds a new WebDAV connection.
    /// </summary>
    Task<WebDAVConnection> AddConnectionAsync(WebDAVConnectionOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a WebDAV connection.
    /// </summary>
    Task UpdateConnectionAsync(WebDAVConnection connection, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a WebDAV connection.
    /// </summary>
    Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a connection by ID.
    /// </summary>
    Task<WebDAVConnection?> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to a WebDAV server.
    /// </summary>
    Task<bool> ConnectAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnects from a WebDAV server.
    /// </summary>
    Task DisconnectAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tests a WebDAV connection.
    /// </summary>
    Task<WebDAVTestResult> TestConnectionAsync(WebDAVConnectionOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists items in a WebDAV directory.
    /// </summary>
    Task<IReadOnlyList<WebDAVItem>> ListAsync(string connectionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about a WebDAV item.
    /// </summary>
    Task<WebDAVItem?> GetItemAsync(string connectionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file from WebDAV.
    /// </summary>
    Task<Stream> DownloadAsync(string connectionId, string path, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file to a local path.
    /// </summary>
    Task DownloadToFileAsync(string connectionId, string remotePath, string localPath, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads a file to WebDAV.
    /// </summary>
    Task<WebDAVItem> UploadAsync(string connectionId, string path, Stream content, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads a file from a local path.
    /// </summary>
    Task<WebDAVItem> UploadFromFileAsync(string connectionId, string remotePath, string localPath, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a directory on the WebDAV server.
    /// </summary>
    Task<WebDAVItem> CreateDirectoryAsync(string connectionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an item from the WebDAV server.
    /// </summary>
    Task DeleteAsync(string connectionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves/renames an item on the WebDAV server.
    /// </summary>
    Task<WebDAVItem> MoveAsync(string connectionId, string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copies an item on the WebDAV server.
    /// </summary>
    Task<WebDAVItem> CopyAsync(string connectionId, string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets WebDAV properties for an item.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string connectionId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets WebDAV properties for an item.
    /// </summary>
    Task SetPropertiesAsync(string connectionId, string path, IReadOnlyDictionary<string, string> properties, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Locks a WebDAV resource.
    /// </summary>
    Task<WebDAVLock> LockAsync(string connectionId, string path, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unlocks a WebDAV resource.
    /// </summary>
    Task UnlockAsync(string connectionId, string path, string lockToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets quota information.
    /// </summary>
    Task<WebDAVQuota?> GetQuotaAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when connection status changes.
    /// </summary>
    event EventHandler<WebDAVConnectionEventArgs>? ConnectionChanged;
    
    /// <summary>
    /// Event raised during transfer progress.
    /// </summary>
    event EventHandler<WebDAVTransferProgress>? TransferProgress;
}

/// <summary>
/// Options for creating a WebDAV connection.
/// </summary>
public class WebDAVConnectionOptions
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public WebDAVAuthType AuthType { get; set; } = WebDAVAuthType.Basic;
    public bool UseSSL { get; set; } = true;
    public bool IgnoreCertificateErrors { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? ProxyUrl { get; set; }
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }
    public Dictionary<string, string> CustomHeaders { get; init; } = new();
}

/// <summary>
/// A registered WebDAV connection.
/// </summary>
public class WebDAVConnection
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public WebDAVAuthType AuthType { get; set; }
    public bool UseSSL { get; set; }
    public bool IgnoreCertificateErrors { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? ProxyUrl { get; set; }
    public string? ProxyUsername { get; set; }
    public string? ProxyPassword { get; set; }
    public Dictionary<string, string> CustomHeaders { get; init; } = new();
    public WebDAVConnectionStatus Status { get; set; }
    public DateTime? LastConnected { get; set; }
    public string? ServerType { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// WebDAV authentication types.
/// </summary>
public enum WebDAVAuthType
{
    None,
    Basic,
    Digest,
    NTLM,
    Negotiate,
    OAuth2
}

/// <summary>
/// WebDAV connection status.
/// </summary>
public enum WebDAVConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>
/// Result of testing a WebDAV connection.
/// </summary>
public class WebDAVTestResult
{
    public bool Success { get; init; }
    public string? ServerType { get; init; }
    public string? ServerVersion { get; init; }
    public IReadOnlyList<string> SupportedMethods { get; init; } = Array.Empty<string>();
    public bool SupportsLocking { get; init; }
    public bool SupportsVersioning { get; init; }
    public string? ErrorMessage { get; init; }
    public int ResponseTimeMs { get; init; }
}

/// <summary>
/// A file or directory on a WebDAV server.
/// </summary>
public class WebDAVItem
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public bool IsCollection { get; init; }
    public long ContentLength { get; init; }
    public string? ContentType { get; init; }
    public string? ETag { get; init; }
    public DateTime? CreationDate { get; init; }
    public DateTime? LastModified { get; init; }
    public string? DisplayName { get; init; }
    public bool IsLocked { get; init; }
    public string? LockOwner { get; init; }
    public IReadOnlyDictionary<string, string>? CustomProperties { get; init; }
    
    public string SizeDisplay => IsCollection ? string.Empty : FormatSize(ContentLength);
    public string LastModifiedDisplay => LastModified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// WebDAV lock information.
/// </summary>
public class WebDAVLock
{
    public string LockToken { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public WebDAVLockScope Scope { get; init; }
    public WebDAVLockType Type { get; init; }
    public string? Owner { get; init; }
    public DateTime? Timeout { get; init; }
    public int Depth { get; init; }
}

/// <summary>
/// WebDAV lock scope.
/// </summary>
public enum WebDAVLockScope
{
    Exclusive,
    Shared
}

/// <summary>
/// WebDAV lock type.
/// </summary>
public enum WebDAVLockType
{
    Write
}

/// <summary>
/// WebDAV quota information.
/// </summary>
public class WebDAVQuota
{
    public long UsedBytes { get; init; }
    public long AvailableBytes { get; init; }
    public long? TotalBytes => UsedBytes + AvailableBytes;
    
    public string UsedDisplay => FormatSize(UsedBytes);
    public string AvailableDisplay => FormatSize(AvailableBytes);
    public string? TotalDisplay => TotalBytes.HasValue ? FormatSize(TotalBytes.Value) : null;
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// WebDAV transfer progress information.
/// </summary>
public class WebDAVTransferProgress
{
    public string ConnectionId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    public string RemotePath { get; init; } = string.Empty;
    public WebDAVTransferDirection Direction { get; init; }
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public WebDAVTransferStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// WebDAV transfer direction.
/// </summary>
public enum WebDAVTransferDirection
{
    Upload,
    Download
}

/// <summary>
/// WebDAV transfer status.
/// </summary>
public enum WebDAVTransferStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Event args for WebDAV connection changes.
/// </summary>
public class WebDAVConnectionEventArgs : EventArgs
{
    public string ConnectionId { get; init; } = string.Empty;
    public WebDAVConnectionEventType EventType { get; init; }
    public WebDAVConnection? Connection { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Types of WebDAV connection events.
/// </summary>
public enum WebDAVConnectionEventType
{
    Added,
    Removed,
    Updated,
    Connected,
    Disconnected,
    Error
}
