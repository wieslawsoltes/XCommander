// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Cloud storage service with support for multiple providers.
/// </summary>
public class CloudStorageService : ICloudStorageService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<CloudProvider> _providers;
    private readonly List<CloudAccount> _accounts;
    private readonly string _configPath;
    private readonly object _lock = new();
    private bool _loaded;
    private bool _disposed;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public IReadOnlyList<CloudProvider> Providers => _providers.AsReadOnly();
    public IReadOnlyList<CloudAccount> Accounts
    {
        get
        {
            lock (_lock)
            {
                return _accounts.ToList().AsReadOnly();
            }
        }
    }
    
    public event EventHandler<CloudAccountEventArgs>? AccountChanged;
    public event EventHandler<CloudTransferProgress>? TransferProgress;
    
    public CloudStorageService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _providers = InitializeProviders();
        _accounts = new List<CloudAccount>();
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "cloud-accounts.json");
    }
    
    private static List<CloudProvider> InitializeProviders()
    {
        return new List<CloudProvider>
        {
            new CloudProvider
            {
                Type = CloudProviderType.OneDrive,
                Name = "OneDrive",
                Description = "Microsoft OneDrive personal cloud storage",
                SupportsOAuth = true,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = true,
                MaxFileSize = 250L * 1024 * 1024 * 1024, // 250 GB
                SupportedFeatures = new[] { "OAuth", "Sync", "Sharing", "Search", "Thumbnails", "Versions" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.OneDriveBusiness,
                Name = "OneDrive for Business",
                Description = "Microsoft OneDrive for business accounts",
                SupportsOAuth = true,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = true,
                MaxFileSize = 250L * 1024 * 1024 * 1024,
                SupportedFeatures = new[] { "OAuth", "Sync", "Sharing", "Search", "Thumbnails", "Versions", "Business" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.GoogleDrive,
                Name = "Google Drive",
                Description = "Google Drive cloud storage",
                SupportsOAuth = true,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = true,
                MaxFileSize = 5L * 1024 * 1024 * 1024 * 1024, // 5 TB
                SupportedFeatures = new[] { "OAuth", "Sync", "Sharing", "Search", "Thumbnails", "Versions", "GoogleDocs" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.Dropbox,
                Name = "Dropbox",
                Description = "Dropbox cloud storage",
                SupportsOAuth = true,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = true,
                MaxFileSize = 2L * 1024 * 1024 * 1024, // 2 GB for free
                SupportedFeatures = new[] { "OAuth", "Sync", "Sharing", "Search", "Thumbnails", "Versions" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.Box,
                Name = "Box",
                Description = "Box cloud storage for business",
                SupportsOAuth = true,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = true,
                MaxFileSize = 5L * 1024 * 1024 * 1024, // 5 GB
                SupportedFeatures = new[] { "OAuth", "Sync", "Sharing", "Search", "Versions", "Business" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.AmazonS3,
                Name = "Amazon S3",
                Description = "Amazon Simple Storage Service",
                SupportsOAuth = false,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = false,
                MaxFileSize = 5L * 1024 * 1024 * 1024 * 1024, // 5 TB
                SupportedFeatures = new[] { "Sync", "Sharing", "Versioning", "ACL" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.AzureBlob,
                Name = "Azure Blob Storage",
                Description = "Microsoft Azure Blob Storage",
                SupportsOAuth = false,
                SupportsSync = true,
                SupportsSharing = true,
                SupportsSearch = false,
                MaxFileSize = 190_000L * 1024 * 1024 * 1024, // ~190 TB
                SupportedFeatures = new[] { "Sync", "Sharing", "Versioning", "Containers" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.SFTP,
                Name = "SFTP",
                Description = "Secure File Transfer Protocol",
                SupportsOAuth = false,
                SupportsSync = true,
                SupportsSharing = false,
                SupportsSearch = false,
                MaxFileSize = long.MaxValue,
                SupportedFeatures = new[] { "Sync", "SSH", "KeyAuth" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.FTP,
                Name = "FTP",
                Description = "File Transfer Protocol",
                SupportsOAuth = false,
                SupportsSync = true,
                SupportsSharing = false,
                SupportsSearch = false,
                MaxFileSize = long.MaxValue,
                SupportedFeatures = new[] { "Sync", "FTPS", "Passive" }
            },
            new CloudProvider
            {
                Type = CloudProviderType.WebDAV,
                Name = "WebDAV",
                Description = "Web Distributed Authoring and Versioning",
                SupportsOAuth = false,
                SupportsSync = true,
                SupportsSharing = false,
                SupportsSearch = false,
                MaxFileSize = long.MaxValue,
                SupportedFeatures = new[] { "Sync", "HTTPS", "BasicAuth" }
            }
        };
    }
    
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded) return;
        
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                var accounts = JsonSerializer.Deserialize<List<CloudAccount>>(json, JsonOptions);
                if (accounts != null)
                {
                    lock (_lock)
                    {
                        _accounts.Clear();
                        _accounts.AddRange(accounts);
                    }
                }
            }
            catch
            {
                // Ignore load errors
            }
        }
        
        _loaded = true;
    }
    
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            List<CloudAccount> accountsToSave;
            lock (_lock)
            {
                // Don't save tokens to disk for security
                accountsToSave = _accounts.Select(a => new CloudAccount
                {
                    Id = a.Id,
                    Provider = a.Provider,
                    Name = a.Name,
                    Email = a.Email,
                    DisplayName = a.DisplayName,
                    Status = CloudAccountStatus.Disconnected, // Always save as disconnected
                    LastConnected = a.LastConnected,
                    Settings = a.Settings,
                    CreatedAt = a.CreatedAt
                }).ToList();
            }
            
            var json = JsonSerializer.Serialize(accountsToSave, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    public async Task<CloudAccount> AddAccountAsync(CloudProviderType provider, string name, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = new CloudAccount
        {
            Provider = provider,
            Name = name,
            Status = CloudAccountStatus.Disconnected
        };
        
        lock (_lock)
        {
            _accounts.Add(account);
        }
        
        await SaveAsync(cancellationToken);
        
        AccountChanged?.Invoke(this, new CloudAccountEventArgs
        {
            AccountId = account.Id,
            EventType = CloudAccountEventType.Added,
            Account = account
        });
        
        return account;
    }
    
    public async Task RemoveAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? removed;
        lock (_lock)
        {
            removed = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (removed != null)
            {
                _accounts.Remove(removed);
            }
        }
        
        if (removed != null)
        {
            await SaveAsync(cancellationToken);
            
            AccountChanged?.Invoke(this, new CloudAccountEventArgs
            {
                AccountId = accountId,
                EventType = CloudAccountEventType.Removed,
                Account = removed
            });
        }
    }
    
    public async Task<CloudAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            return _accounts.FirstOrDefault(a => a.Id == accountId);
        }
    }
    
    public async Task UpdateAccountAsync(CloudAccount account, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var existing = _accounts.FirstOrDefault(a => a.Id == account.Id);
            if (existing != null)
            {
                var index = _accounts.IndexOf(existing);
                _accounts[index] = account;
            }
        }
        
        await SaveAsync(cancellationToken);
        
        AccountChanged?.Invoke(this, new CloudAccountEventArgs
        {
            AccountId = account.Id,
            EventType = CloudAccountEventType.Updated,
            Account = account
        });
    }
    
    public async Task<bool> AuthenticateAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null)
        {
            return false;
        }
        
        account.Status = CloudAccountStatus.Connecting;
        AccountChanged?.Invoke(this, new CloudAccountEventArgs
        {
            AccountId = accountId,
            EventType = CloudAccountEventType.Updated,
            Account = account
        });
        
        try
        {
            // OAuth authentication would go here based on provider
            // For now, this is a placeholder implementation
            switch (account.Provider)
            {
                case CloudProviderType.OneDrive:
                case CloudProviderType.OneDriveBusiness:
                    // Would use Microsoft Identity for OAuth
                    break;
                case CloudProviderType.GoogleDrive:
                    // Would use Google OAuth
                    break;
                case CloudProviderType.Dropbox:
                    // Would use Dropbox OAuth
                    break;
                default:
                    // Non-OAuth providers would use stored credentials
                    break;
            }
            
            // Simulate successful authentication
            account.Status = CloudAccountStatus.Connected;
            account.LastConnected = DateTime.Now;
            
            AccountChanged?.Invoke(this, new CloudAccountEventArgs
            {
                AccountId = accountId,
                EventType = CloudAccountEventType.Connected,
                Account = account
            });
            
            return true;
        }
        catch
        {
            account.Status = CloudAccountStatus.Error;
            AccountChanged?.Invoke(this, new CloudAccountEventArgs
            {
                AccountId = accountId,
                EventType = CloudAccountEventType.Error,
                Account = account
            });
            
            return false;
        }
    }
    
    public async Task DisconnectAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account != null)
        {
            account.Status = CloudAccountStatus.Disconnected;
            account.AccessToken = null;
            account.RefreshToken = null;
            
            AccountChanged?.Invoke(this, new CloudAccountEventArgs
            {
                AccountId = accountId,
                EventType = CloudAccountEventType.Disconnected,
                Account = account
            });
        }
    }
    
    public async Task<IReadOnlyList<CloudItem>> ListAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return Array.Empty<CloudItem>();
        }
        
        // Implementation would vary by provider
        // This is a placeholder that returns empty
        return await Task.FromResult<IReadOnlyList<CloudItem>>(Array.Empty<CloudItem>());
    }
    
    public async Task<CloudItem?> GetItemAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return null;
        }
        
        // Implementation would vary by provider
        return await Task.FromResult<CloudItem?>(null);
    }
    
    public async Task<Stream> DownloadAsync(string accountId, string path, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        // This is a placeholder
        var transferProgress = new CloudTransferProgress
        {
            AccountId = accountId,
            FileName = Path.GetFileName(path),
            CloudPath = path,
            Direction = CloudTransferDirection.Download,
            Status = CloudTransferStatus.InProgress
        };
        
        progress?.Report(transferProgress);
        TransferProgress?.Invoke(this, transferProgress);
        
        // Return empty stream as placeholder
        return new MemoryStream();
    }
    
    public async Task DownloadToFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var stream = await DownloadAsync(accountId, cloudPath, progress, cancellationToken);
        
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var fileStream = File.Create(localPath);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }
    
    public async Task<CloudItem> UploadAsync(string accountId, string cloudPath, Stream content, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        var transferProgress = new CloudTransferProgress
        {
            AccountId = accountId,
            FileName = Path.GetFileName(cloudPath),
            CloudPath = cloudPath,
            Direction = CloudTransferDirection.Upload,
            TotalBytes = content.Length,
            Status = CloudTransferStatus.InProgress
        };
        
        progress?.Report(transferProgress);
        TransferProgress?.Invoke(this, transferProgress);
        
        // Return placeholder item
        return new CloudItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = Path.GetFileName(cloudPath),
            Path = cloudPath,
            IsFolder = false,
            Size = content.Length,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
    }
    
    public async Task<CloudItem> UploadFromFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var fileStream = File.OpenRead(localPath);
        return await UploadAsync(accountId, cloudPath, fileStream, progress, cancellationToken);
    }
    
    public async Task<CloudItem> CreateFolderAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        return new CloudItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = Path.GetFileName(path),
            Path = path,
            IsFolder = true,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
    }
    
    public async Task DeleteAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        await Task.CompletedTask;
    }
    
    public async Task<CloudItem> MoveAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        return new CloudItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = Path.GetFileName(destinationPath),
            Path = destinationPath,
            ModifiedAt = DateTime.Now
        };
    }
    
    public async Task<CloudItem> CopyAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would vary by provider
        return new CloudItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = Path.GetFileName(destinationPath),
            Path = destinationPath,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now
        };
    }
    
    public async Task<string?> GetShareLinkAsync(string accountId, string path, ShareLinkOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return null;
        }
        
        var provider = _providers.FirstOrDefault(p => p.Type == account.Provider);
        if (provider?.SupportsSharing != true)
        {
            return null;
        }
        
        // Implementation would vary by provider
        return await Task.FromResult<string?>(null);
    }
    
    public async Task<IReadOnlyList<CloudItem>> SearchAsync(string accountId, string query, string? folderPath = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return Array.Empty<CloudItem>();
        }
        
        var provider = _providers.FirstOrDefault(p => p.Type == account.Provider);
        if (provider?.SupportsSearch != true)
        {
            return Array.Empty<CloudItem>();
        }
        
        // Implementation would vary by provider
        return await Task.FromResult<IReadOnlyList<CloudItem>>(Array.Empty<CloudItem>());
    }
    
    public async Task<CloudQuota> GetQuotaAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return new CloudQuota { UsedBytes = 0, TotalBytes = 0 };
        }
        
        // Implementation would vary by provider
        return await Task.FromResult(new CloudQuota
        {
            UsedBytes = 0,
            TotalBytes = 0
        });
    }
    
    public async Task SynchronizeAsync(string accountId, string localPath, string cloudPath, SyncDirection direction, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        // Implementation would:
        // 1. Compare local and cloud files
        // 2. Determine which files need syncing based on direction
        // 3. Upload/download files as needed
        // 4. Report progress
        
        await Task.CompletedTask;
    }
    
    public async Task<IReadOnlyList<CloudItem>> GetRecentAsync(string accountId, int count = 50, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return Array.Empty<CloudItem>();
        }
        
        // Implementation would vary by provider
        return await Task.FromResult<IReadOnlyList<CloudItem>>(Array.Empty<CloudItem>());
    }
    
    public async Task<IReadOnlyList<CloudItem>> GetSharedWithMeAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null || account.Status != CloudAccountStatus.Connected)
        {
            return Array.Empty<CloudItem>();
        }
        
        // Implementation would vary by provider
        return await Task.FromResult<IReadOnlyList<CloudItem>>(Array.Empty<CloudItem>());
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
