// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private const string SettingsRootKey = "RootPath";
    private const string SettingsRootAltKey = "LocalRoot";
    private const string SettingsRootLegacyKey = "Root";

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
            // Basic implementation uses a local root folder per account.
            // Providers can store a custom root in account.Settings["RootPath"].
            _ = GetAccountRootPath(account);
            
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
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        return await Task.Run<IReadOnlyList<CloudItem>>(() =>
        {
            var items = new List<CloudItem>();
            
            if (Directory.Exists(fullPath))
            {
                var dirInfo = new DirectoryInfo(fullPath);
                foreach (var dir in dirInfo.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(CreateCloudItem(dir, root));
                }
                
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(CreateCloudItem(file, root));
                }
            }
            else if (File.Exists(fullPath))
            {
                items.Add(CreateCloudItem(new FileInfo(fullPath), root));
            }
            
            return items;
        }, cancellationToken);
    }
    
    public async Task<CloudItem?> GetItemAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        return await Task.Run<CloudItem?>(() =>
        {
            if (Directory.Exists(fullPath))
            {
                return CreateCloudItem(new DirectoryInfo(fullPath), root);
            }
            
            if (File.Exists(fullPath))
            {
                return CreateCloudItem(new FileInfo(fullPath), root);
            }
            
            return null;
        }, cancellationToken);
    }
    
    public async Task<Stream> DownloadAsync(string accountId, string path, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Cloud file not found.", path);
        }
        
        if (progress == null)
        {
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        
        await using var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var memory = new MemoryStream();
        await CopyWithProgressAsync(accountId, path, source, memory, CloudTransferDirection.Download, progress, cancellationToken);
        memory.Position = 0;
        return memory;
    }
    
    public async Task DownloadToFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, cloudPath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Cloud file not found.", cloudPath);
        }
        
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await using var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await CopyWithProgressAsync(accountId, cloudPath, source, destination, CloudTransferDirection.Download, progress, cancellationToken);
    }
    
    public async Task<CloudItem> UploadAsync(string accountId, string cloudPath, Stream content, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, cloudPath);
        
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await CopyWithProgressAsync(accountId, cloudPath, content, destination, CloudTransferDirection.Upload, progress, cancellationToken);
        
        var fileInfo = new FileInfo(fullPath);
        return CreateCloudItem(fileInfo, root);
    }
    
    public async Task<CloudItem> UploadFromFileAsync(string accountId, string cloudPath, string localPath, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(localPath);
        return await UploadAsync(accountId, cloudPath, fileStream, progress, cancellationToken);
    }
    
    public async Task<CloudItem> CreateFolderAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        Directory.CreateDirectory(fullPath);
        return CreateCloudItem(new DirectoryInfo(fullPath), root);
    }
    
    public async Task DeleteAsync(string accountId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        await Task.Run(() =>
        {
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                return;
            }
            
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }, cancellationToken);
    }
    
    public async Task<CloudItem> MoveAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var sourceFull = ResolveCloudPath(root, sourcePath);
        var destinationFull = ResolveCloudPath(root, destinationPath);
        
        var movedIsDirectory = false;
        await Task.Run(() =>
        {
            if (Directory.Exists(destinationFull))
            {
                Directory.Delete(destinationFull, true);
            }
            else if (File.Exists(destinationFull))
            {
                File.Delete(destinationFull);
            }
            
            if (Directory.Exists(sourceFull))
            {
                movedIsDirectory = true;
                Directory.Move(sourceFull, destinationFull);
                return;
            }
            
            File.Move(sourceFull, destinationFull);
        }, cancellationToken);
        
        return movedIsDirectory
            ? CreateCloudItem(new DirectoryInfo(destinationFull), root)
            : CreateCloudItem(new FileInfo(destinationFull), root);
    }
    
    public async Task<CloudItem> CopyAsync(string accountId, string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var sourceFull = ResolveCloudPath(root, sourcePath);
        var destinationFull = ResolveCloudPath(root, destinationPath);
        
        var copyIsDirectory = false;
        await Task.Run(() =>
        {
            if (Directory.Exists(sourceFull))
            {
                copyIsDirectory = true;
                CopyDirectory(sourceFull, destinationFull);
                return;
            }
            
            var destinationDir = Path.GetDirectoryName(destinationFull);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            File.Copy(sourceFull, destinationFull, true);
        }, cancellationToken);

        return copyIsDirectory
            ? CreateCloudItem(new DirectoryInfo(destinationFull), root)
            : CreateCloudItem(new FileInfo(destinationFull), root);
    }
    
    public async Task<string?> GetShareLinkAsync(string accountId, string path, ShareLinkOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var fullPath = ResolveCloudPath(root, path);
        
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            return new Uri(fullPath).AbsoluteUri;
        }
        
        return null;
    }
    
    public async Task<IReadOnlyList<CloudItem>> SearchAsync(string accountId, string query, string? folderPath = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var basePath = ResolveCloudPath(root, folderPath ?? string.Empty);
        var comparison = GetPathComparison();
        
        return await Task.Run<IReadOnlyList<CloudItem>>(() =>
        {
            if (!Directory.Exists(basePath))
            {
                return Array.Empty<CloudItem>();
            }
            
            var results = new List<CloudItem>();
            foreach (var entry in Directory.EnumerateFileSystemEntries(basePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(entry);
                if (name.Contains(query, comparison))
                {
                    var info = Directory.Exists(entry)
                        ? (FileSystemInfo)new DirectoryInfo(entry)
                        : new FileInfo(entry);
                    results.Add(CreateCloudItem(info, root));
                }
            }
            
            return results;
        }, cancellationToken);
    }
    
    public async Task<CloudQuota> GetQuotaAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        
        return await Task.Run(() =>
        {
            var used = CalculateDirectorySize(root);
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            return new CloudQuota
            {
                UsedBytes = used,
                TotalBytes = drive.TotalSize
            };
        }, cancellationToken);
    }
    
    public async Task SynchronizeAsync(string accountId, string localPath, string cloudPath, SyncDirection direction, IProgress<CloudTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var cloudRoot = ResolveCloudPath(root, cloudPath);
        
        await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            switch (direction)
            {
                case SyncDirection.Upload:
                    await SyncCopyDirectoryAsync(accountId, localPath, cloudRoot, CloudTransferDirection.Upload, progress, cancellationToken);
                    break;
                case SyncDirection.Download:
                    await SyncCopyDirectoryAsync(accountId, cloudRoot, localPath, CloudTransferDirection.Download, progress, cancellationToken);
                    break;
                case SyncDirection.Bidirectional:
                    await SyncBidirectionalAsync(accountId, localPath, cloudRoot, progress, cancellationToken);
                    break;
            }
        }, cancellationToken);
    }
    
    public async Task<IReadOnlyList<CloudItem>> GetRecentAsync(string accountId, int count = 50, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        
        return await Task.Run<IReadOnlyList<CloudItem>>(() =>
        {
            if (!Directory.Exists(root))
            {
                return Array.Empty<CloudItem>();
            }
            
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Take(count)
                .Select(info => CreateCloudItem(info, root))
                .ToList();
            
            return files;
        }, cancellationToken);
    }
    
    public async Task<IReadOnlyList<CloudItem>> GetSharedWithMeAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var account = GetConnectedAccount(accountId);
        var root = GetAccountRootPath(account);
        var sharedPath = Path.Combine(root, "Shared");
        
        if (!Directory.Exists(sharedPath))
        {
            return Array.Empty<CloudItem>();
        }
        
        return await Task.Run<IReadOnlyList<CloudItem>>(() =>
        {
            var items = new List<CloudItem>();
            var dir = new DirectoryInfo(sharedPath);
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = CreateCloudItem(entry, root);
                items.Add(new CloudItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Path = item.Path,
                    ParentId = item.ParentId,
                    ParentPath = item.ParentPath,
                    IsFolder = item.IsFolder,
                    Size = item.Size,
                    CreatedAt = item.CreatedAt,
                    ModifiedAt = item.ModifiedAt,
                    MimeType = item.MimeType,
                    DownloadUrl = item.DownloadUrl,
                    ThumbnailUrl = item.ThumbnailUrl,
                    IsShared = true,
                    IsTrashed = item.IsTrashed,
                    Checksum = item.Checksum,
                    Metadata = item.Metadata
                });
            }
            
            return items;
        }, cancellationToken);
    }

    private CloudAccount GetConnectedAccount(string accountId)
    {
        CloudAccount? account;
        lock (_lock)
        {
            account = _accounts.FirstOrDefault(a => a.Id == accountId);
        }
        
        if (account == null)
        {
            throw new InvalidOperationException("Account not found");
        }
        
        if (account.Status != CloudAccountStatus.Connected)
        {
            throw new InvalidOperationException("Account not connected");
        }
        
        return account;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private string GetAccountRootPath(CloudAccount account)
    {
        var root = GetSetting(account, SettingsRootKey)
                   ?? GetSetting(account, SettingsRootAltKey)
                   ?? GetSetting(account, SettingsRootLegacyKey);

        if (string.IsNullOrWhiteSpace(root))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            root = Path.Combine(appData, "XCommander", "Cloud", account.Id);
        }

        Directory.CreateDirectory(root);
        return root;
    }

    private static string? GetSetting(CloudAccount account, string key)
    {
        return account.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string ResolveCloudPath(string root, string? path)
    {
        var relative = (path ?? string.Empty).Trim();
        relative = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(relative))
        {
            return root;
        }

        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root);
        var comparison = GetPathComparison();
        
        if (!combined.StartsWith(rootFull, comparison))
        {
            throw new InvalidOperationException("Invalid cloud path.");
        }
        
        return combined;
    }

    private static CloudItem CreateCloudItem(FileSystemInfo info, string root)
    {
        var path = GetRelativePath(root, info.FullName);
        return new CloudItem
        {
            Id = info.FullName,
            Name = info.Name,
            Path = path,
            ParentPath = GetRelativePath(root, Path.GetDirectoryName(info.FullName) ?? root),
            IsFolder = info is DirectoryInfo,
            Size = info is FileInfo fileInfo ? fileInfo.Length : 0,
            CreatedAt = info.CreationTime,
            ModifiedAt = info.LastWriteTime,
            Metadata = new Dictionary<string, object>
            {
                ["FullPath"] = info.FullName
            }
        };
    }

    private static string GetRelativePath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == "." || relative == string.Empty)
        {
            return string.Empty;
        }
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private async Task CopyWithProgressAsync(
        string accountId,
        string cloudPath,
        Stream source,
        Stream destination,
        CloudTransferDirection direction,
        IProgress<CloudTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var totalBytes = source.CanSeek ? source.Length : 0;
        var buffer = new byte[81920];
        long transferred = 0;
        var stopwatch = Stopwatch.StartNew();

        ReportProgress(accountId, cloudPath, direction, 0, totalBytes, CloudTransferStatus.InProgress, null, progress, stopwatch);

        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;
            ReportProgress(accountId, cloudPath, direction, transferred, totalBytes, CloudTransferStatus.InProgress, null, progress, stopwatch);
        }

        ReportProgress(accountId, cloudPath, direction, transferred, totalBytes, CloudTransferStatus.Completed, null, progress, stopwatch);
    }

    private void ReportProgress(
        string accountId,
        string cloudPath,
        CloudTransferDirection direction,
        long transferred,
        long total,
        CloudTransferStatus status,
        string? error,
        IProgress<CloudTransferProgress>? progress,
        Stopwatch stopwatch)
    {
        var elapsed = stopwatch.Elapsed.TotalSeconds;
        var bytesPerSecond = elapsed > 0 ? transferred / elapsed : 0;

        var transferProgress = new CloudTransferProgress
        {
            AccountId = accountId,
            FileName = Path.GetFileName(cloudPath),
            CloudPath = cloudPath,
            Direction = direction,
            BytesTransferred = transferred,
            TotalBytes = total,
            BytesPerSecond = bytesPerSecond,
            Status = status,
            ErrorMessage = error
        };

        progress?.Report(transferProgress);
        TransferProgress?.Invoke(this, transferProgress);
    }

    private static long CalculateDirectorySize(string path)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch
            {
                // Ignore unreadable files.
            }
        }
        return total;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, target);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, target, true);
        }
    }

    private async Task SyncCopyDirectoryAsync(
        string accountId,
        string sourceDir,
        string destinationDir,
        CloudTransferDirection direction,
        IProgress<CloudTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            await SyncCopyDirectoryAsync(accountId, directory, targetDir, direction, progress, cancellationToken);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetFile = Path.Combine(destinationDir, Path.GetFileName(file));
            await using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await CopyWithProgressAsync(accountId, GetRelativePath(destinationDir, targetFile), sourceStream, targetStream, direction, progress, cancellationToken);
        }
    }

    private async Task SyncBidirectionalAsync(
        string accountId,
        string localPath,
        string cloudPath,
        IProgress<CloudTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(localPath))
        {
            Directory.CreateDirectory(localPath);
        }

        if (!Directory.Exists(cloudPath))
        {
            Directory.CreateDirectory(cloudPath);
        }

        var localFiles = Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(localPath, path), path => new FileInfo(path));
        var cloudFiles = Directory.EnumerateFiles(cloudPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(cloudPath, path), path => new FileInfo(path));

        var allKeys = new HashSet<string>(localFiles.Keys);
        allKeys.UnionWith(cloudFiles.Keys);

        foreach (var relative in allKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localFiles.TryGetValue(relative, out var localInfo);
            cloudFiles.TryGetValue(relative, out var cloudInfo);

            if (localInfo == null && cloudInfo != null)
            {
                var destination = Path.Combine(localPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var sourceStream = new FileStream(cloudInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var targetStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await CopyWithProgressAsync(accountId, relative, sourceStream, targetStream, CloudTransferDirection.Download, progress, cancellationToken);
                continue;
            }

            if (cloudInfo == null && localInfo != null)
            {
                var destination = Path.Combine(cloudPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var sourceStream = new FileStream(localInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var targetStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await CopyWithProgressAsync(accountId, relative, sourceStream, targetStream, CloudTransferDirection.Upload, progress, cancellationToken);
                continue;
            }

            if (localInfo == null || cloudInfo == null)
            {
                continue;
            }

            if (localInfo.LastWriteTimeUtc > cloudInfo.LastWriteTimeUtc)
            {
                var destination = Path.Combine(cloudPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var sourceStream = new FileStream(localInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var targetStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await CopyWithProgressAsync(accountId, relative, sourceStream, targetStream, CloudTransferDirection.Upload, progress, cancellationToken);
            }
            else if (cloudInfo.LastWriteTimeUtc > localInfo.LastWriteTimeUtc)
            {
                var destination = Path.Combine(localPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var sourceStream = new FileStream(cloudInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var targetStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await CopyWithProgressAsync(accountId, relative, sourceStream, targetStream, CloudTransferDirection.Download, progress, cancellationToken);
            }
        }
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
