using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Proxy type
/// </summary>
public enum ProxyType
{
    None,
    Http,
    Https,
    Socks4,
    Socks4A,
    Socks5,
    Ftp
}

/// <summary>
/// Proxy authentication method
/// </summary>
public enum ProxyAuthMethod
{
    None,
    Basic,
    Digest,
    Ntlm,
    Negotiate
}

/// <summary>
/// Proxy server configuration
/// </summary>
public record ProxyConfig
{
    /// <summary>Unique identifier</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>Display name for this proxy</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Proxy type</summary>
    public ProxyType Type { get; init; } = ProxyType.None;
    
    /// <summary>Proxy host</summary>
    public string Host { get; init; } = string.Empty;
    
    /// <summary>Proxy port</summary>
    public int Port { get; init; } = 8080;
    
    /// <summary>Requires authentication</summary>
    public bool RequiresAuth { get; init; }
    
    /// <summary>Authentication method</summary>
    public ProxyAuthMethod AuthMethod { get; init; } = ProxyAuthMethod.Basic;
    
    /// <summary>Username (if auth required)</summary>
    public string? Username { get; init; }
    
    /// <summary>Password (encrypted, if auth required)</summary>
    public string? EncryptedPassword { get; init; }
    
    /// <summary>Domain (for NTLM auth)</summary>
    public string? Domain { get; init; }
    
    /// <summary>Bypass proxy for local addresses</summary>
    public bool BypassLocal { get; init; } = true;
    
    /// <summary>Addresses that bypass proxy</summary>
    public List<string> BypassList { get; init; } = new();
    
    /// <summary>Is this the default proxy</summary>
    public bool IsDefault { get; init; }
    
    /// <summary>Use system proxy settings</summary>
    public bool UseSystemProxy { get; init; }
    
    /// <summary>Additional custom settings</summary>
    public Dictionary<string, string> CustomSettings { get; init; } = new();
}

/// <summary>
/// Proxy test result
/// </summary>
public record ProxyTestResult
{
    /// <summary>Test was successful</summary>
    public bool Success { get; init; }
    
    /// <summary>Response time in milliseconds</summary>
    public long ResponseTimeMs { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Proxy IP as seen by test server</summary>
    public string? ExternalIp { get; init; }
}

/// <summary>
/// Service for managing proxy configurations.
/// TC equivalent: Proxy settings for FTP connections.
/// </summary>
public interface IProxyService
{
    /// <summary>
    /// Get all configured proxies
    /// </summary>
    Task<IReadOnlyList<ProxyConfig>> GetProxiesAsync();
    
    /// <summary>
    /// Get proxy by ID
    /// </summary>
    Task<ProxyConfig?> GetProxyAsync(Guid id);
    
    /// <summary>
    /// Get proxy by name
    /// </summary>
    Task<ProxyConfig?> GetProxyByNameAsync(string name);
    
    /// <summary>
    /// Get the default proxy
    /// </summary>
    Task<ProxyConfig?> GetDefaultProxyAsync();
    
    /// <summary>
    /// Add a new proxy configuration
    /// </summary>
    Task<Guid> AddProxyAsync(ProxyConfig config);
    
    /// <summary>
    /// Update a proxy configuration
    /// </summary>
    Task UpdateProxyAsync(ProxyConfig config);
    
    /// <summary>
    /// Delete a proxy configuration
    /// </summary>
    Task<bool> DeleteProxyAsync(Guid id);
    
    /// <summary>
    /// Set proxy as default
    /// </summary>
    Task SetDefaultProxyAsync(Guid id);
    
    /// <summary>
    /// Test proxy connection
    /// </summary>
    Task<ProxyTestResult> TestProxyAsync(ProxyConfig config, string? testUrl = null);
    
    /// <summary>
    /// Create WebProxy from config
    /// </summary>
    IWebProxy? CreateWebProxy(ProxyConfig config);
    
    /// <summary>
    /// Get system proxy settings
    /// </summary>
    Task<ProxyConfig?> GetSystemProxyAsync();
    
    /// <summary>
    /// Check if address should bypass proxy
    /// </summary>
    bool ShouldBypassProxy(ProxyConfig config, string address);
    
    /// <summary>
    /// Encrypt password for storage
    /// </summary>
    string EncryptPassword(string password);
    
    /// <summary>
    /// Decrypt stored password
    /// </summary>
    string DecryptPassword(string encryptedPassword);
    
    /// <summary>
    /// Import proxy settings from file
    /// </summary>
    Task<int> ImportProxiesAsync(string filePath);
    
    /// <summary>
    /// Export proxy settings to file
    /// </summary>
    Task ExportProxiesAsync(string filePath);
    
    /// <summary>
    /// Event raised when proxies change
    /// </summary>
    event EventHandler? ProxiesChanged;
}
