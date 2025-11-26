// IPasswordManagerService.cs - Secure Password Manager
// Provides encrypted password storage for FTP, plugins, and network connections

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for securely storing and retrieving passwords.
/// </summary>
public interface IPasswordManagerService
{
    /// <summary>
    /// Stores a credential securely.
    /// </summary>
    Task StoreCredentialAsync(StoredCredential credential, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a credential by key.
    /// </summary>
    Task<StoredCredential?> GetCredentialAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves credentials for a specific target (e.g., FTP server).
    /// </summary>
    Task<StoredCredential?> GetCredentialForTargetAsync(string target, CredentialType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all stored credentials of a specific type.
    /// </summary>
    Task<IReadOnlyList<StoredCredential>> GetCredentialsAsync(CredentialType? type = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a stored credential.
    /// </summary>
    Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if master password is set.
    /// </summary>
    Task<bool> IsMasterPasswordSetAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the master password.
    /// </summary>
    Task SetMasterPasswordAsync(string masterPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Changes the master password.
    /// </summary>
    Task ChangeMasterPasswordAsync(string oldPassword, string newPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unlocks the password store with the master password.
    /// </summary>
    Task<bool> UnlockAsync(string masterPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Locks the password store.
    /// </summary>
    void Lock();
    
    /// <summary>
    /// Gets whether the store is unlocked.
    /// </summary>
    bool IsUnlocked { get; }
    
    /// <summary>
    /// Exports credentials (encrypted).
    /// </summary>
    Task<byte[]> ExportCredentialsAsync(string exportPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports credentials (encrypted).
    /// </summary>
    Task ImportCredentialsAsync(byte[] data, string exportPassword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Type of stored credential.
/// </summary>
public enum CredentialType
{
    Ftp,
    Sftp,
    WebDav,
    CloudStorage,
    NetworkShare,
    Plugin,
    Archive,
    Other
}

/// <summary>
/// A stored credential.
/// </summary>
public record StoredCredential
{
    /// <summary>
    /// Unique key for this credential.
    /// </summary>
    public required string Key { get; init; }
    
    /// <summary>
    /// Target system (e.g., server URL, share path).
    /// </summary>
    public required string Target { get; init; }
    
    /// <summary>
    /// Type of credential.
    /// </summary>
    public required CredentialType Type { get; init; }
    
    /// <summary>
    /// Username.
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Password (encrypted in storage).
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// Domain (for Windows authentication).
    /// </summary>
    public string? Domain { get; init; }
    
    /// <summary>
    /// Display name for the credential.
    /// </summary>
    public string? DisplayName { get; init; }
    
    /// <summary>
    /// Additional properties (port, options, etc.).
    /// </summary>
    public Dictionary<string, string>? Properties { get; init; }
    
    /// <summary>
    /// When the credential was created.
    /// </summary>
    public DateTime Created { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the credential was last modified.
    /// </summary>
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the credential was last used.
    /// </summary>
    public DateTime? LastUsed { get; init; }
}
