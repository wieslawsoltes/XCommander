using System.Text.Json;
using XCommander.Services;

namespace XCommander.Models;

/// <summary>
/// Represents a saved FTP/SFTP connection.
/// </summary>
public class FtpConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 21;
    public string Username { get; set; } = string.Empty;
    public string? EncryptedPassword { get; set; }
    public string RemotePath { get; set; } = "/";
    public string LocalPath { get; set; } = string.Empty;
    public ConnectionProtocol Protocol { get; set; } = ConnectionProtocol.Ftp;
    public FtpEncryptionMode EncryptionMode { get; set; } = FtpEncryptionMode.None;
    public bool UsePassiveMode { get; set; } = true;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? LastUsed { get; set; }
    public int Order { get; set; }
    
    /// <summary>
    /// Simple password obfuscation (not secure encryption, just prevents casual viewing).
    /// For real security, consider using platform-specific credential storage.
    /// </summary>
    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            EncryptedPassword = null;
            return;
        }
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        EncryptedPassword = Convert.ToBase64String(bytes);
    }
    
    public string GetPassword()
    {
        if (string.IsNullOrEmpty(EncryptedPassword))
            return string.Empty;
            
        try
        {
            var bytes = Convert.FromBase64String(EncryptedPassword);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}

public enum ConnectionProtocol
{
    Ftp,
    Ftps,
    Sftp
}

/// <summary>
/// Container for all saved FTP connections.
/// </summary>
public class FtpConnectionsData
{
    public List<FtpConnection> Connections { get; set; } = new();
    
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "ftp_connections.json");
    
    public static FtpConnectionsData Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<FtpConnectionsData>(json) ?? new FtpConnectionsData();
            }
        }
        catch
        {
            // Return default on error
        }
        return new FtpConnectionsData();
    }
    
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
