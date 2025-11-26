// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XCommander.Services;

/// <summary>
/// WebDAV service implementation using HTTP client.
/// </summary>
public class WebDAVService : IWebDAVService, IDisposable
{
    private readonly Dictionary<string, HttpClient> _clients;
    private readonly List<WebDAVConnection> _connections;
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
    
    // WebDAV XML namespaces
    private static readonly XNamespace DavNs = "DAV:";
    
    public IReadOnlyList<WebDAVConnection> Connections
    {
        get
        {
            lock (_lock)
            {
                return _connections.ToList().AsReadOnly();
            }
        }
    }
    
    public event EventHandler<WebDAVConnectionEventArgs>? ConnectionChanged;
    public event EventHandler<WebDAVTransferProgress>? TransferProgress;
    
    public WebDAVService()
    {
        _clients = new Dictionary<string, HttpClient>();
        _connections = new List<WebDAVConnection>();
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "webdav-connections.json");
    }
    
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded) return;
        
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                var connections = JsonSerializer.Deserialize<List<WebDAVConnection>>(json, JsonOptions);
                if (connections != null)
                {
                    lock (_lock)
                    {
                        _connections.Clear();
                        _connections.AddRange(connections);
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
            
            List<WebDAVConnection> connectionsToSave;
            lock (_lock)
            {
                // Don't save passwords in plain text
                connectionsToSave = _connections.Select(c => new WebDAVConnection
                {
                    Id = c.Id,
                    Name = c.Name,
                    Url = c.Url,
                    Username = c.Username,
                    // Password not saved
                    AuthType = c.AuthType,
                    UseSSL = c.UseSSL,
                    IgnoreCertificateErrors = c.IgnoreCertificateErrors,
                    TimeoutSeconds = c.TimeoutSeconds,
                    ProxyUrl = c.ProxyUrl,
                    ProxyUsername = c.ProxyUsername,
                    // Proxy password not saved
                    CustomHeaders = c.CustomHeaders,
                    Status = WebDAVConnectionStatus.Disconnected,
                    LastConnected = c.LastConnected,
                    ServerType = c.ServerType,
                    CreatedAt = c.CreatedAt
                }).ToList();
            }
            
            var json = JsonSerializer.Serialize(connectionsToSave, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    private HttpClient GetOrCreateClient(WebDAVConnection connection)
    {
        lock (_lock)
        {
            if (_clients.TryGetValue(connection.Id, out var existingClient))
            {
                return existingClient;
            }
            
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            
            if (connection.IgnoreCertificateErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
            
            if (!string.IsNullOrEmpty(connection.ProxyUrl))
            {
                handler.Proxy = new WebProxy(connection.ProxyUrl);
                if (!string.IsNullOrEmpty(connection.ProxyUsername))
                {
                    handler.Proxy.Credentials = new NetworkCredential(
                        connection.ProxyUsername,
                        connection.ProxyPassword ?? string.Empty
                    );
                }
            }
            
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(connection.Url),
                Timeout = TimeSpan.FromSeconds(connection.TimeoutSeconds > 0 ? connection.TimeoutSeconds : 30)
            };
            
            // Set authentication
            if (!string.IsNullOrEmpty(connection.Username))
            {
                switch (connection.AuthType)
                {
                    case WebDAVAuthType.Basic:
                        var credentials = Convert.ToBase64String(
                            Encoding.ASCII.GetBytes($"{connection.Username}:{connection.Password ?? string.Empty}")
                        );
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                        break;
                    case WebDAVAuthType.Digest:
                        handler.Credentials = new NetworkCredential(connection.Username, connection.Password);
                        break;
                    case WebDAVAuthType.NTLM:
                    case WebDAVAuthType.Negotiate:
                        handler.Credentials = new NetworkCredential(connection.Username, connection.Password);
                        break;
                }
            }
            
            // Add custom headers
            foreach (var header in connection.CustomHeaders)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            _clients[connection.Id] = client;
            return client;
        }
    }
    
    public async Task<WebDAVConnection> AddConnectionAsync(WebDAVConnectionOptions options, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var connection = new WebDAVConnection
        {
            Name = options.Name,
            Url = options.Url.TrimEnd('/'),
            Username = options.Username,
            Password = options.Password,
            AuthType = options.AuthType,
            UseSSL = options.UseSSL,
            IgnoreCertificateErrors = options.IgnoreCertificateErrors,
            TimeoutSeconds = options.TimeoutSeconds,
            ProxyUrl = options.ProxyUrl,
            ProxyUsername = options.ProxyUsername,
            ProxyPassword = options.ProxyPassword,
            CustomHeaders = new Dictionary<string, string>(options.CustomHeaders),
            Status = WebDAVConnectionStatus.Disconnected
        };
        
        lock (_lock)
        {
            _connections.Add(connection);
        }
        
        await SaveAsync(cancellationToken);
        
        ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
        {
            ConnectionId = connection.Id,
            EventType = WebDAVConnectionEventType.Added,
            Connection = connection
        });
        
        return connection;
    }
    
    public async Task UpdateConnectionAsync(WebDAVConnection connection, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var existing = _connections.FirstOrDefault(c => c.Id == connection.Id);
            if (existing != null)
            {
                var index = _connections.IndexOf(existing);
                _connections[index] = connection;
                
                // Remove cached client so it will be recreated with new settings
                if (_clients.TryGetValue(connection.Id, out var client))
                {
                    client.Dispose();
                    _clients.Remove(connection.Id);
                }
            }
        }
        
        await SaveAsync(cancellationToken);
        
        ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
        {
            ConnectionId = connection.Id,
            EventType = WebDAVConnectionEventType.Updated,
            Connection = connection
        });
    }
    
    public async Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? removed;
        lock (_lock)
        {
            removed = _connections.FirstOrDefault(c => c.Id == connectionId);
            if (removed != null)
            {
                _connections.Remove(removed);
                
                if (_clients.TryGetValue(connectionId, out var client))
                {
                    client.Dispose();
                    _clients.Remove(connectionId);
                }
            }
        }
        
        if (removed != null)
        {
            await SaveAsync(cancellationToken);
            
            ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
            {
                ConnectionId = connectionId,
                EventType = WebDAVConnectionEventType.Removed,
                Connection = removed
            });
        }
    }
    
    public async Task<WebDAVConnection?> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            return _connections.FirstOrDefault(c => c.Id == connectionId);
        }
    }
    
    public async Task<bool> ConnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null)
        {
            return false;
        }
        
        connection.Status = WebDAVConnectionStatus.Connecting;
        ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
        {
            ConnectionId = connectionId,
            EventType = WebDAVConnectionEventType.Updated,
            Connection = connection
        });
        
        try
        {
            var client = GetOrCreateClient(connection);
            
            // Test connection with OPTIONS request
            var request = new HttpRequestMessage(HttpMethod.Options, "/");
            var response = await client.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                connection.Status = WebDAVConnectionStatus.Connected;
                connection.LastConnected = DateTime.Now;
                
                // Try to detect server type from headers
                if (response.Headers.TryGetValues("Server", out var serverValues))
                {
                    connection.ServerType = serverValues.FirstOrDefault();
                }
                
                ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
                {
                    ConnectionId = connectionId,
                    EventType = WebDAVConnectionEventType.Connected,
                    Connection = connection
                });
                
                return true;
            }
            else
            {
                connection.Status = WebDAVConnectionStatus.Error;
                ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
                {
                    ConnectionId = connectionId,
                    EventType = WebDAVConnectionEventType.Error,
                    Connection = connection,
                    ErrorMessage = $"Server returned {response.StatusCode}"
                });
                
                return false;
            }
        }
        catch (Exception ex)
        {
            connection.Status = WebDAVConnectionStatus.Error;
            ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
            {
                ConnectionId = connectionId,
                EventType = WebDAVConnectionEventType.Error,
                Connection = connection,
                ErrorMessage = ex.Message
            });
            
            return false;
        }
    }
    
    public async Task DisconnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
            
            if (_clients.TryGetValue(connectionId, out var client))
            {
                client.Dispose();
                _clients.Remove(connectionId);
            }
        }
        
        if (connection != null)
        {
            connection.Status = WebDAVConnectionStatus.Disconnected;
            
            ConnectionChanged?.Invoke(this, new WebDAVConnectionEventArgs
            {
                ConnectionId = connectionId,
                EventType = WebDAVConnectionEventType.Disconnected,
                Connection = connection
            });
        }
    }
    
    public async Task<WebDAVTestResult> TestConnectionAsync(WebDAVConnectionOptions options, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };
            
            if (options.IgnoreCertificateErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
            
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(options.Url),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30)
            };
            
            if (!string.IsNullOrEmpty(options.Username) && options.AuthType == WebDAVAuthType.Basic)
            {
                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{options.Username}:{options.Password ?? string.Empty}")
                );
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
            
            var request = new HttpRequestMessage(HttpMethod.Options, "/");
            var response = await client.SendAsync(request, cancellationToken);
            
            stopwatch.Stop();
            
            if (!response.IsSuccessStatusCode)
            {
                return new WebDAVTestResult
                {
                    Success = false,
                    ErrorMessage = $"Server returned {response.StatusCode}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            
            var supportedMethods = new List<string>();
            if (response.Headers.TryGetValues("Allow", out var allowValues))
            {
                var allow = allowValues.FirstOrDefault() ?? string.Empty;
                supportedMethods.AddRange(allow.Split(',').Select(m => m.Trim()));
            }
            
            string? serverType = null;
            string? serverVersion = null;
            if (response.Headers.TryGetValues("Server", out var serverValues))
            {
                serverType = serverValues.FirstOrDefault();
            }
            
            var supportsLocking = supportedMethods.Contains("LOCK") && supportedMethods.Contains("UNLOCK");
            var supportsVersioning = response.Headers.TryGetValues("DAV", out var davValues) &&
                                     davValues.Any(v => v.Contains("version"));
            
            return new WebDAVTestResult
            {
                Success = true,
                ServerType = serverType,
                ServerVersion = serverVersion,
                SupportedMethods = supportedMethods,
                SupportsLocking = supportsLocking,
                SupportsVersioning = supportsVersioning,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new WebDAVTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    public async Task<IReadOnlyList<WebDAVItem>> ListAsync(string connectionId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            return Array.Empty<WebDAVItem>();
        }
        
        try
        {
            var client = GetOrCreateClient(connection);
            
            var propfindContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<D:propfind xmlns:D=""DAV:"">
    <D:prop>
        <D:displayname/>
        <D:resourcetype/>
        <D:getcontentlength/>
        <D:getcontenttype/>
        <D:getetag/>
        <D:creationdate/>
        <D:getlastmodified/>
        <D:lockdiscovery/>
    </D:prop>
</D:propfind>";
            
            var requestPath = string.IsNullOrEmpty(path) || path == "/" ? "/" : "/" + path.TrimStart('/');
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestPath)
            {
                Content = new StringContent(propfindContent, Encoding.UTF8, "application/xml")
            };
            request.Headers.Add("Depth", "1");
            
            var response = await client.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<WebDAVItem>();
            }
            
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParsePropfindResponse(xml, requestPath);
        }
        catch
        {
            return Array.Empty<WebDAVItem>();
        }
    }
    
    private List<WebDAVItem> ParsePropfindResponse(string xml, string requestPath)
    {
        var items = new List<WebDAVItem>();
        
        try
        {
            var doc = XDocument.Parse(xml);
            var responses = doc.Descendants(DavNs + "response");
            
            foreach (var resp in responses)
            {
                var href = resp.Element(DavNs + "href")?.Value ?? string.Empty;
                var propstat = resp.Element(DavNs + "propstat");
                var prop = propstat?.Element(DavNs + "prop");
                
                if (prop == null) continue;
                
                // Skip the current directory
                var normalizedHref = Uri.UnescapeDataString(href).TrimEnd('/');
                var normalizedRequestPath = requestPath.TrimEnd('/');
                if (normalizedHref == normalizedRequestPath) continue;
                
                var resourceType = prop.Element(DavNs + "resourcetype");
                var isCollection = resourceType?.Element(DavNs + "collection") != null;
                
                var displayName = prop.Element(DavNs + "displayname")?.Value;
                var contentLength = prop.Element(DavNs + "getcontentlength")?.Value;
                var contentType = prop.Element(DavNs + "getcontenttype")?.Value;
                var etag = prop.Element(DavNs + "getetag")?.Value;
                var creationDate = prop.Element(DavNs + "creationdate")?.Value;
                var lastModified = prop.Element(DavNs + "getlastmodified")?.Value;
                
                var lockDiscovery = prop.Element(DavNs + "lockdiscovery");
                var isLocked = lockDiscovery?.Element(DavNs + "activelock") != null;
                string? lockOwner = null;
                if (isLocked)
                {
                    lockOwner = lockDiscovery?.Element(DavNs + "activelock")?
                        .Element(DavNs + "owner")?.Value;
                }
                
                var name = displayName ?? Path.GetFileName(Uri.UnescapeDataString(href).TrimEnd('/'));
                var itemPath = Uri.UnescapeDataString(href).TrimStart('/');
                
                items.Add(new WebDAVItem
                {
                    Name = name,
                    Path = itemPath,
                    Href = href,
                    IsCollection = isCollection,
                    ContentLength = long.TryParse(contentLength, out var length) ? length : 0,
                    ContentType = contentType,
                    ETag = etag?.Trim('"'),
                    CreationDate = DateTime.TryParse(creationDate, out var created) ? created : null,
                    LastModified = DateTime.TryParse(lastModified, out var modified) ? modified : null,
                    DisplayName = displayName,
                    IsLocked = isLocked,
                    LockOwner = lockOwner
                });
            }
        }
        catch
        {
            // Parsing failed, return empty list
        }
        
        return items.OrderBy(i => !i.IsCollection).ThenBy(i => i.Name).ToList();
    }
    
    public async Task<WebDAVItem?> GetItemAsync(string connectionId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            return null;
        }
        
        try
        {
            var client = GetOrCreateClient(connection);
            
            var propfindContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<D:propfind xmlns:D=""DAV:"">
    <D:allprop/>
</D:propfind>";
            
            var requestPath = "/" + path.TrimStart('/');
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestPath)
            {
                Content = new StringContent(propfindContent, Encoding.UTF8, "application/xml")
            };
            request.Headers.Add("Depth", "0");
            
            var response = await client.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = ParsePropfindResponse(xml, "");
            return items.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<Stream> DownloadAsync(string connectionId, string path, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var transferProgress = new WebDAVTransferProgress
        {
            ConnectionId = connectionId,
            FileName = Path.GetFileName(path),
            RemotePath = path,
            Direction = WebDAVTransferDirection.Download,
            Status = WebDAVTransferStatus.InProgress
        };
        
        progress?.Report(transferProgress);
        TransferProgress?.Invoke(this, transferProgress);
        
        var response = await client.GetAsync(requestPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var contentLength = response.Content.Headers.ContentLength ?? 0;
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        // For progress reporting, wrap the stream
        // This is a simplified implementation
        return stream;
    }
    
    public async Task DownloadToFileAsync(string connectionId, string remotePath, string localPath, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var stream = await DownloadAsync(connectionId, remotePath, progress, cancellationToken);
        
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var fileStream = File.Create(localPath);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }
    
    public async Task<WebDAVItem> UploadAsync(string connectionId, string path, Stream content, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var transferProgress = new WebDAVTransferProgress
        {
            ConnectionId = connectionId,
            FileName = Path.GetFileName(path),
            RemotePath = path,
            Direction = WebDAVTransferDirection.Upload,
            TotalBytes = content.Length,
            Status = WebDAVTransferStatus.InProgress
        };
        
        progress?.Report(transferProgress);
        TransferProgress?.Invoke(this, transferProgress);
        
        var request = new HttpRequestMessage(HttpMethod.Put, requestPath)
        {
            Content = new StreamContent(content)
        };
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return new WebDAVItem
        {
            Name = Path.GetFileName(path),
            Path = path.TrimStart('/'),
            Href = requestPath,
            IsCollection = false,
            ContentLength = content.Length,
            LastModified = DateTime.Now
        };
    }
    
    public async Task<WebDAVItem> UploadFromFileAsync(string connectionId, string remotePath, string localPath, IProgress<WebDAVTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var fileStream = File.OpenRead(localPath);
        return await UploadAsync(connectionId, remotePath, fileStream, progress, cancellationToken);
    }
    
    public async Task<WebDAVItem> CreateDirectoryAsync(string connectionId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/').TrimEnd('/') + "/";
        
        var request = new HttpRequestMessage(new HttpMethod("MKCOL"), requestPath);
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return new WebDAVItem
        {
            Name = Path.GetFileName(path.TrimEnd('/')),
            Path = path.TrimStart('/'),
            Href = requestPath,
            IsCollection = true,
            CreationDate = DateTime.Now,
            LastModified = DateTime.Now
        };
    }
    
    public async Task DeleteAsync(string connectionId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var response = await client.DeleteAsync(requestPath, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<WebDAVItem> MoveAsync(string connectionId, string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var sourceRequestPath = "/" + sourcePath.TrimStart('/');
        var destRequestPath = new Uri(client.BaseAddress!, "/" + destinationPath.TrimStart('/')).ToString();
        
        var request = new HttpRequestMessage(new HttpMethod("MOVE"), sourceRequestPath);
        request.Headers.Add("Destination", destRequestPath);
        request.Headers.Add("Overwrite", overwrite ? "T" : "F");
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return new WebDAVItem
        {
            Name = Path.GetFileName(destinationPath.TrimEnd('/')),
            Path = destinationPath.TrimStart('/'),
            Href = "/" + destinationPath.TrimStart('/'),
            LastModified = DateTime.Now
        };
    }
    
    public async Task<WebDAVItem> CopyAsync(string connectionId, string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var sourceRequestPath = "/" + sourcePath.TrimStart('/');
        var destRequestPath = new Uri(client.BaseAddress!, "/" + destinationPath.TrimStart('/')).ToString();
        
        var request = new HttpRequestMessage(new HttpMethod("COPY"), sourceRequestPath);
        request.Headers.Add("Destination", destRequestPath);
        request.Headers.Add("Overwrite", overwrite ? "T" : "F");
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return new WebDAVItem
        {
            Name = Path.GetFileName(destinationPath.TrimEnd('/')),
            Path = destinationPath.TrimStart('/'),
            Href = "/" + destinationPath.TrimStart('/'),
            CreationDate = DateTime.Now,
            LastModified = DateTime.Now
        };
    }
    
    public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string connectionId, string path, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            return new Dictionary<string, string>();
        }
        
        var item = await GetItemAsync(connectionId, path, cancellationToken);
        return item?.CustomProperties ?? new Dictionary<string, string>();
    }
    
    public async Task SetPropertiesAsync(string connectionId, string path, IReadOnlyDictionary<string, string> properties, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var proppatchContent = new StringBuilder();
        proppatchContent.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        proppatchContent.AppendLine(@"<D:propertyupdate xmlns:D=""DAV:"">");
        proppatchContent.AppendLine(@"    <D:set>");
        proppatchContent.AppendLine(@"        <D:prop>");
        
        foreach (var prop in properties)
        {
            proppatchContent.AppendLine($@"            <{prop.Key}>{prop.Value}</{prop.Key}>");
        }
        
        proppatchContent.AppendLine(@"        </D:prop>");
        proppatchContent.AppendLine(@"    </D:set>");
        proppatchContent.AppendLine(@"</D:propertyupdate>");
        
        var request = new HttpRequestMessage(new HttpMethod("PROPPATCH"), requestPath)
        {
            Content = new StringContent(proppatchContent.ToString(), Encoding.UTF8, "application/xml")
        };
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<WebDAVLock> LockAsync(string connectionId, string path, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var lockContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<D:lockinfo xmlns:D=""DAV:"">
    <D:lockscope><D:exclusive/></D:lockscope>
    <D:locktype><D:write/></D:locktype>
    <D:owner>
        <D:href>XCommander</D:href>
    </D:owner>
</D:lockinfo>";
        
        var request = new HttpRequestMessage(new HttpMethod("LOCK"), requestPath)
        {
            Content = new StringContent(lockContent, Encoding.UTF8, "application/xml")
        };
        
        if (timeout.HasValue)
        {
            request.Headers.Add("Timeout", $"Second-{(int)timeout.Value.TotalSeconds}");
        }
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        // Parse lock token from response
        string lockToken = string.Empty;
        if (response.Headers.TryGetValues("Lock-Token", out var lockTokenValues))
        {
            lockToken = lockTokenValues.FirstOrDefault() ?? string.Empty;
        }
        
        return new WebDAVLock
        {
            LockToken = lockToken.Trim('<', '>'),
            Path = path,
            Scope = WebDAVLockScope.Exclusive,
            Type = WebDAVLockType.Write,
            Owner = "XCommander",
            Timeout = timeout.HasValue ? DateTime.Now.Add(timeout.Value) : null
        };
    }
    
    public async Task UnlockAsync(string connectionId, string path, string lockToken, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            throw new InvalidOperationException("Connection not established");
        }
        
        var client = GetOrCreateClient(connection);
        var requestPath = "/" + path.TrimStart('/');
        
        var request = new HttpRequestMessage(new HttpMethod("UNLOCK"), requestPath);
        request.Headers.Add("Lock-Token", $"<{lockToken}>");
        
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<WebDAVQuota?> GetQuotaAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        WebDAVConnection? connection;
        lock (_lock)
        {
            connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        
        if (connection == null || connection.Status != WebDAVConnectionStatus.Connected)
        {
            return null;
        }
        
        try
        {
            var client = GetOrCreateClient(connection);
            
            var propfindContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<D:propfind xmlns:D=""DAV:"">
    <D:prop>
        <D:quota-available-bytes/>
        <D:quota-used-bytes/>
    </D:prop>
</D:propfind>";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/")
            {
                Content = new StringContent(propfindContent, Encoding.UTF8, "application/xml")
            };
            request.Headers.Add("Depth", "0");
            
            var response = await client.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);
            
            var prop = doc.Descendants(DavNs + "prop").FirstOrDefault();
            if (prop == null) return null;
            
            var usedStr = prop.Element(DavNs + "quota-used-bytes")?.Value;
            var availableStr = prop.Element(DavNs + "quota-available-bytes")?.Value;
            
            if (long.TryParse(usedStr, out var used) && long.TryParse(availableStr, out var available))
            {
                return new WebDAVQuota
                {
                    UsedBytes = used,
                    AvailableBytes = available
                };
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                foreach (var client in _clients.Values)
                {
                    client.Dispose();
                }
                _clients.Clear();
            }
            _disposed = true;
        }
    }
}
