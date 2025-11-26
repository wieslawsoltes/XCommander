using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of IProxyService for managing proxy configurations.
/// </summary>
public class ProxyService : IProxyService
{
    private readonly List<ProxyConfig> _proxies = new();
    private readonly string _configPath;
    private readonly byte[] _encryptionKey;
    
    public event EventHandler? ProxiesChanged;

    public ProxyService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "proxies.json");
        
        // Generate machine-specific encryption key
        var keyBase = $"{Environment.MachineName}-{Environment.UserName}-XCommanderProxy";
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(keyBase));
        
        LoadProxies();
    }

    private void LoadProxies()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<List<ProxyConfig>>(json);
                if (loaded != null)
                {
                    _proxies.Clear();
                    _proxies.AddRange(loaded);
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private async Task SaveProxiesAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var json = JsonSerializer.Serialize(_proxies, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public Task<IReadOnlyList<ProxyConfig>> GetProxiesAsync()
    {
        return Task.FromResult<IReadOnlyList<ProxyConfig>>(_proxies.ToList());
    }

    public Task<ProxyConfig?> GetProxyAsync(Guid id)
    {
        return Task.FromResult(_proxies.FirstOrDefault(p => p.Id == id));
    }

    public Task<ProxyConfig?> GetProxyByNameAsync(string name)
    {
        return Task.FromResult(_proxies.FirstOrDefault(p => 
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<ProxyConfig?> GetDefaultProxyAsync()
    {
        return Task.FromResult(_proxies.FirstOrDefault(p => p.IsDefault));
    }

    public async Task<Guid> AddProxyAsync(ProxyConfig config)
    {
        _proxies.Add(config);
        
        if (config.IsDefault)
        {
            // Clear default from others
            foreach (var proxy in _proxies.Where(p => p.Id != config.Id && p.IsDefault))
            {
                var index = _proxies.IndexOf(proxy);
                _proxies[index] = proxy with { IsDefault = false };
            }
        }
        
        await SaveProxiesAsync();
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
        
        return config.Id;
    }

    public async Task UpdateProxyAsync(ProxyConfig config)
    {
        var index = _proxies.FindIndex(p => p.Id == config.Id);
        if (index >= 0)
        {
            _proxies[index] = config;
            
            if (config.IsDefault)
            {
                foreach (var proxy in _proxies.Where(p => p.Id != config.Id && p.IsDefault))
                {
                    var i = _proxies.IndexOf(proxy);
                    _proxies[i] = proxy with { IsDefault = false };
                }
            }
            
            await SaveProxiesAsync();
            ProxiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<bool> DeleteProxyAsync(Guid id)
    {
        var removed = _proxies.RemoveAll(p => p.Id == id) > 0;
        if (removed)
        {
            await SaveProxiesAsync();
            ProxiesChanged?.Invoke(this, EventArgs.Empty);
        }
        return removed;
    }

    public async Task SetDefaultProxyAsync(Guid id)
    {
        foreach (var proxy in _proxies)
        {
            var index = _proxies.IndexOf(proxy);
            _proxies[index] = proxy with { IsDefault = proxy.Id == id };
        }
        
        await SaveProxiesAsync();
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ProxyTestResult> TestProxyAsync(ProxyConfig config, string? testUrl = null)
    {
        testUrl ??= "https://httpbin.org/ip";
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            var handler = new HttpClientHandler();
            var webProxy = CreateWebProxy(config);
            
            if (webProxy != null)
            {
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }
            
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await client.GetStringAsync(testUrl);
            sw.Stop();
            
            // Try to extract IP from response (if using httpbin.org)
            string? externalIp = null;
            try
            {
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("origin", out var origin))
                {
                    externalIp = origin.GetString();
                }
            }
            catch
            {
                // Not JSON or no origin property
            }
            
            return new ProxyTestResult
            {
                Success = true,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ExternalIp = externalIp
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProxyTestResult
            {
                Success = false,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    public IWebProxy? CreateWebProxy(ProxyConfig config)
    {
        if (config.Type == ProxyType.None)
            return null;
            
        if (config.UseSystemProxy)
            return WebRequest.GetSystemWebProxy();
        
        var proxyUri = new UriBuilder
        {
            Scheme = config.Type switch
            {
                ProxyType.Https => "https",
                ProxyType.Socks4 or ProxyType.Socks4A or ProxyType.Socks5 => "socks5",
                _ => "http"
            },
            Host = config.Host,
            Port = config.Port
        }.Uri;
        
        var proxy = new WebProxy(proxyUri)
        {
            BypassProxyOnLocal = config.BypassLocal,
            BypassList = config.BypassList.ToArray()
        };
        
        if (config.RequiresAuth && !string.IsNullOrEmpty(config.Username))
        {
            var password = !string.IsNullOrEmpty(config.EncryptedPassword)
                ? DecryptPassword(config.EncryptedPassword)
                : string.Empty;
                
            proxy.Credentials = string.IsNullOrEmpty(config.Domain)
                ? new NetworkCredential(config.Username, password)
                : new NetworkCredential(config.Username, password, config.Domain);
        }
        
        return proxy;
    }

    public Task<ProxyConfig?> GetSystemProxyAsync()
    {
        try
        {
            var systemProxy = WebRequest.GetSystemWebProxy();
            var testUri = new Uri("http://www.example.com");
            var proxyUri = systemProxy.GetProxy(testUri);
            
            if (proxyUri != null && proxyUri != testUri)
            {
                return Task.FromResult<ProxyConfig?>(new ProxyConfig
                {
                    Name = "System Proxy",
                    Type = proxyUri.Scheme.ToLowerInvariant() switch
                    {
                        "https" => ProxyType.Https,
                        "socks" or "socks5" => ProxyType.Socks5,
                        _ => ProxyType.Http
                    },
                    Host = proxyUri.Host,
                    Port = proxyUri.Port,
                    UseSystemProxy = true
                });
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return Task.FromResult<ProxyConfig?>(null);
    }

    public bool ShouldBypassProxy(ProxyConfig config, string address)
    {
        if (config.Type == ProxyType.None)
            return true;
            
        if (config.BypassLocal)
        {
            if (address.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                address == "127.0.0.1" ||
                address == "::1")
            {
                return true;
            }
        }
        
        foreach (var bypass in config.BypassList)
        {
            if (bypass.StartsWith('*'))
            {
                // Wildcard match
                if (address.EndsWith(bypass[1..], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (bypass.EndsWith('*'))
            {
                if (address.StartsWith(bypass[..^1], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (address.Equals(bypass, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }

    public string EncryptPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;
            
        try
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey[..32]; // Use first 32 bytes (256 bits)
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(password);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            // Combine IV and encrypted data
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        catch
        {
            return string.Empty;
        }
    }

    public string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            return string.Empty;
            
        try
        {
            var combined = Convert.FromBase64String(encryptedPassword);
            
            using var aes = Aes.Create();
            aes.Key = _encryptionKey[..32];
            
            // Extract IV
            var iv = new byte[aes.BlockSize / 8];
            Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
            aes.IV = iv;
            
            // Extract encrypted data
            var encryptedBytes = new byte[combined.Length - iv.Length];
            Buffer.BlockCopy(combined, iv.Length, encryptedBytes, 0, encryptedBytes.Length);
            
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<int> ImportProxiesAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var imported = JsonSerializer.Deserialize<List<ProxyConfig>>(json);
            
            if (imported != null)
            {
                var count = 0;
                foreach (var proxy in imported)
                {
                    // Generate new ID to avoid conflicts
                    var newProxy = proxy with { Id = Guid.NewGuid(), IsDefault = false };
                    _proxies.Add(newProxy);
                    count++;
                }
                
                await SaveProxiesAsync();
                ProxiesChanged?.Invoke(this, EventArgs.Empty);
                return count;
            }
        }
        catch
        {
            // Ignore import errors
        }
        
        return 0;
    }

    public async Task ExportProxiesAsync(string filePath)
    {
        // Export without encrypted passwords for security
        var exportable = _proxies.Select(p => p with { EncryptedPassword = null }).ToList();
        
        var json = JsonSerializer.Serialize(exportable, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(filePath, json);
    }
}
