// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for cloud storage integration (OneDrive, Google Drive, Dropbox, etc.).
/// Similar to Total Commander's cloud plugin architecture.
/// </summary>
public interface ICloudStorageService
{
    /// <summary>
    /// Gets available cloud providers.
    /// </summary>
    IReadOnlyList<CloudProvider> Providers { get; }
    
    /// <summary>
    /// Gets registered accounts.
    /// </summary>
    IReadOnlyList<CloudAccount> Accounts { get; }
    
    /// <summary>
    /// Registers a new cloud account.
    /// </summary>
    Task<CloudAccount> AddAccountAsync(CloudProviderType provider, string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a cloud account.
    /// </summary>
    Task RemoveAccountAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an account by ID.
    /// </summary>
    Task<CloudAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates account settings.
    /// </summary>
    Task UpdateAccountAsync(CloudAccount account, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Authenticates a cloud account.
    /// </summary>
    Task<bool> AuthenticateAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnects from a cloud account.
    /// </summary>
    Task DisconnectAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists files in a cloud folder.
    /// </summary>
    Task<IReadOnlyList<CloudItem>> ListAsync(string accountId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about a cloud item.
    /// </summary>
    Task<CloudItem?> GetItemAsync(string accountId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file from cloud storage.
    /// </summary>
    Task<Stream> DownloadAsync(string accountId, string path, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file to a local path.
    /// </summary>
    Task DownloadToFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads a file to cloud storage.
    /// </summary>
    Task<CloudItem> UploadAsync(string accountId, string cloudPath, Stream content, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads a file from a local path.
    /// </summary>
    Task<CloudItem> UploadFromFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a folder in cloud storage.
    /// </summary>
    Task<CloudItem> CreateFolderAsync(string accountId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an item from cloud storage.
    /// </summary>
    Task DeleteAsync(string accountId, string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Moves/renames an item in cloud storage.
    /// </summary>
    Task<CloudItem> MoveAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copies an item in cloud storage.
    /// </summary>
    Task<CloudItem> CopyAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a shareable link for an item.
    /// </summary>
    Task<string?> GetShareLinkAsync(string accountId, string path, ShareLinkOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for items in cloud storage.
    /// </summary>
    Task<IReadOnlyList<CloudItem>> SearchAsync(string accountId, string query, string? folderPath = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets storage quota information.
    /// </summary>
    Task<CloudQuota> GetQuotaAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synchronizes a local folder with a cloud folder.
    /// </summary>
    Task SynchronizeAsync(string accountId, string localPath, string cloudPath, SyncDirection direction, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets recent files from cloud storage.
    /// </summary>
    Task<IReadOnlyList<CloudItem>> GetRecentAsync(string accountId, int count = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets shared items.
    /// </summary>
    Task<IReadOnlyList<CloudItem>> GetSharedWithMeAsync(string accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when account status changes.
    /// </summary>
    event EventHandler<CloudAccountEventArgs>? AccountChanged;
    
    /// <summary>
    /// Event raised during transfer progress.
    /// </summary>
    event EventHandler<CloudTransferProgress>? TransferProgress;
}

/// <summary>
/// Supported cloud providers.
/// </summary>
public enum CloudProviderType
{
    OneDrive,
    OneDriveBusiness,
    GoogleDrive,
    Dropbox,
    Box,
    AmazonS3,
    AzureBlob,
    SFTP,
    FTP,
    WebDAV,
    Custom
}

/// <summary>
/// Cloud provider information.
/// </summary>
public class CloudProvider
{
    public CloudProviderType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public bool SupportsOAuth { get; init; }
    public bool SupportsSync { get; init; }
    public bool SupportsSharing { get; init; }
    public bool SupportsSearch { get; init; }
    public long MaxFileSize { get; init; }
    public IReadOnlyList<string> SupportedFeatures { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A registered cloud account.
/// </summary>
public class CloudAccount
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public CloudProviderType Provider { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public CloudAccountStatus Status { get; set; }
    public DateTime? LastConnected { get; set; }
    public DateTime? TokenExpires { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Dictionary<string, string> Settings { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Cloud account status.
/// </summary>
public enum CloudAccountStatus
{
    Disconnected,
    Connecting,
    Connected,
    TokenExpired,
    Error
}

/// <summary>
/// A file or folder in cloud storage.
/// </summary>
public class CloudItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public string? ParentPath { get; init; }
    public bool IsFolder { get; init; }
    public long Size { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public string? MimeType { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsShared { get; init; }
    public bool IsTrashed { get; init; }
    public string? Checksum { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    public string SizeDisplay => IsFolder ? string.Empty : FormatSize(Size);
    public string ModifiedDisplay => ModifiedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    
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
/// Options for sharing a link.
/// </summary>
public class ShareLinkOptions
{
    public ShareLinkType Type { get; set; } = ShareLinkType.View;
    public DateTime? ExpiresAt { get; set; }
    public string? Password { get; set; }
    public bool AllowDownload { get; set; } = true;
    public bool NotifyRecipients { get; set; }
    public IReadOnlyList<string>? Recipients { get; set; }
}

/// <summary>
/// Types of share links.
/// </summary>
public enum ShareLinkType
{
    View,
    Edit,
    Download,
    Upload
}

/// <summary>
/// Cloud storage quota information.
/// </summary>
public class CloudQuota
{
    public long UsedBytes { get; init; }
    public long TotalBytes { get; init; }
    public long RemainingBytes => TotalBytes - UsedBytes;
    public double UsedPercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
    
    public string UsedDisplay => FormatSize(UsedBytes);
    public string TotalDisplay => FormatSize(TotalBytes);
    public string RemainingDisplay => FormatSize(RemainingBytes);
    
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
/// Synchronization direction.
/// </summary>
public enum SyncDirection
{
    Upload,
    Download,
    Bidirectional
}

/// <summary>
/// Cloud transfer progress information.
/// </summary>
public class CloudTransferProgress
{
    public string AccountId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;
    public string CloudPath { get; init; } = string.Empty;
    public CloudTransferDirection Direction { get; init; }
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public CloudTransferStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Cloud transfer direction.
/// </summary>
public enum CloudTransferDirection
{
    Upload,
    Download
}

/// <summary>
/// Transfer status.
/// </summary>
public enum CloudTransferStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Event args for cloud account changes.
/// </summary>
public class CloudAccountEventArgs : EventArgs
{
    public string AccountId { get; init; } = string.Empty;
    public CloudAccountEventType EventType { get; init; }
    public CloudAccount? Account { get; init; }
}

/// <summary>
/// Types of account events.
/// </summary>
public enum CloudAccountEventType
{
    Added,
    Removed,
    Updated,
    Connected,
    Disconnected,
    TokenRefreshed,
    Error
}
