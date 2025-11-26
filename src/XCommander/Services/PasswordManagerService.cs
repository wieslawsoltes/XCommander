// PasswordManagerService.cs - Secure Password Manager Implementation

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of secure password storage.
/// </summary>
public class PasswordManagerService : IPasswordManagerService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;
    private const string CredentialsFileName = "credentials.enc";
    
    private readonly string _storagePath;
    private Dictionary<string, StoredCredential> _credentials = new();
    private byte[]? _derivedKey;
    private byte[]? _salt;
    private bool _isUnlocked;
    
    public bool IsUnlocked => _isUnlocked;
    
    public PasswordManagerService(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander",
            CredentialsFileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
    }
    
    public async Task StoreCredentialAsync(StoredCredential credential, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        _credentials[credential.Key] = credential with
        {
            LastModified = DateTime.UtcNow
        };
        
        await SaveCredentialsAsync(cancellationToken);
    }
    
    public Task<StoredCredential?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        _credentials.TryGetValue(key, out var credential);
        
        // Update last used
        if (credential != null)
        {
            _credentials[key] = credential with { LastUsed = DateTime.UtcNow };
        }
        
        return Task.FromResult(credential);
    }
    
    public Task<StoredCredential?> GetCredentialForTargetAsync(string target, CredentialType type, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        var credential = _credentials.Values
            .FirstOrDefault(c => c.Target.Equals(target, StringComparison.OrdinalIgnoreCase) && c.Type == type);
        
        if (credential != null)
        {
            _credentials[credential.Key] = credential with { LastUsed = DateTime.UtcNow };
        }
        
        return Task.FromResult(credential);
    }
    
    public Task<IReadOnlyList<StoredCredential>> GetCredentialsAsync(CredentialType? type = null, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        IEnumerable<StoredCredential> result = _credentials.Values;
        
        if (type.HasValue)
        {
            result = result.Where(c => c.Type == type.Value);
        }
        
        return Task.FromResult<IReadOnlyList<StoredCredential>>(result.ToList());
    }
    
    public async Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        if (_credentials.Remove(key))
        {
            await SaveCredentialsAsync(cancellationToken);
        }
    }
    
    public Task<bool> IsMasterPasswordSetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(_storagePath));
    }
    
    public async Task SetMasterPasswordAsync(string masterPassword, CancellationToken cancellationToken = default)
    {
        if (await IsMasterPasswordSetAsync(cancellationToken))
        {
            throw new InvalidOperationException("Master password is already set. Use ChangeMasterPasswordAsync to change it.");
        }
        
        _salt = RandomNumberGenerator.GetBytes(SaltSize);
        _derivedKey = DeriveKey(masterPassword, _salt);
        _isUnlocked = true;
        
        await SaveCredentialsAsync(cancellationToken);
    }
    
    public async Task ChangeMasterPasswordAsync(string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        // Verify old password
        if (!await UnlockAsync(oldPassword, cancellationToken))
        {
            throw new InvalidOperationException("Invalid old password.");
        }
        
        // Generate new key
        _salt = RandomNumberGenerator.GetBytes(SaltSize);
        _derivedKey = DeriveKey(newPassword, _salt);
        
        await SaveCredentialsAsync(cancellationToken);
    }
    
    public async Task<bool> UnlockAsync(string masterPassword, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return false;
        }
        
        try
        {
            var fileData = await File.ReadAllBytesAsync(_storagePath, cancellationToken);
            
            if (fileData.Length < SaltSize + 12 + 16) // salt + nonce + tag minimum
            {
                return false;
            }
            
            // Extract salt
            _salt = new byte[SaltSize];
            Array.Copy(fileData, 0, _salt, 0, SaltSize);
            
            // Derive key
            _derivedKey = DeriveKey(masterPassword, _salt);
            
            // Extract nonce
            var nonce = new byte[12];
            Array.Copy(fileData, SaltSize, nonce, 0, 12);
            
            // Extract tag
            var tag = new byte[16];
            Array.Copy(fileData, fileData.Length - 16, tag, 0, 16);
            
            // Extract ciphertext
            var ciphertextLength = fileData.Length - SaltSize - 12 - 16;
            var ciphertext = new byte[ciphertextLength];
            Array.Copy(fileData, SaltSize + 12, ciphertext, 0, ciphertextLength);
            
            // Decrypt
            var plaintext = new byte[ciphertextLength];
            using var aes = new AesGcm(_derivedKey, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            
            // Deserialize
            var json = Encoding.UTF8.GetString(plaintext);
            var credentials = JsonSerializer.Deserialize<List<StoredCredential>>(json);
            
            _credentials = credentials?.ToDictionary(c => c.Key) ?? new();
            _isUnlocked = true;
            
            return true;
        }
        catch (CryptographicException)
        {
            _derivedKey = null;
            _salt = null;
            _isUnlocked = false;
            return false;
        }
    }
    
    public void Lock()
    {
        _credentials.Clear();
        _derivedKey = null;
        _isUnlocked = false;
    }
    
    public async Task<byte[]> ExportCredentialsAsync(string exportPassword, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(exportPassword, salt);
        
        var json = JsonSerializer.Serialize(_credentials.Values.ToList());
        var plaintext = Encoding.UTF8.GetBytes(json);
        
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        
        // Combine: salt + nonce + ciphertext + tag
        var result = new byte[salt.Length + nonce.Length + ciphertext.Length + tag.Length];
        var offset = 0;
        Array.Copy(salt, 0, result, offset, salt.Length);
        offset += salt.Length;
        Array.Copy(nonce, 0, result, offset, nonce.Length);
        offset += nonce.Length;
        Array.Copy(ciphertext, 0, result, offset, ciphertext.Length);
        offset += ciphertext.Length;
        Array.Copy(tag, 0, result, offset, tag.Length);
        
        return result;
    }
    
    public async Task ImportCredentialsAsync(byte[] data, string exportPassword, CancellationToken cancellationToken = default)
    {
        EnsureUnlocked();
        
        if (data.Length < SaltSize + 12 + 16)
        {
            throw new InvalidOperationException("Invalid export data.");
        }
        
        // Extract salt
        var salt = new byte[SaltSize];
        Array.Copy(data, 0, salt, 0, SaltSize);
        
        // Derive key
        var key = DeriveKey(exportPassword, salt);
        
        // Extract nonce
        var nonce = new byte[12];
        Array.Copy(data, SaltSize, nonce, 0, 12);
        
        // Extract tag
        var tag = new byte[16];
        Array.Copy(data, data.Length - 16, tag, 0, 16);
        
        // Extract ciphertext
        var ciphertextLength = data.Length - SaltSize - 12 - 16;
        var ciphertext = new byte[ciphertextLength];
        Array.Copy(data, SaltSize + 12, ciphertext, 0, ciphertextLength);
        
        // Decrypt
        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        
        // Deserialize
        var json = Encoding.UTF8.GetString(plaintext);
        var credentials = JsonSerializer.Deserialize<List<StoredCredential>>(json);
        
        if (credentials != null)
        {
            foreach (var cred in credentials)
            {
                _credentials[cred.Key] = cred;
            }
        }
        
        await SaveCredentialsAsync(cancellationToken);
    }
    
    private async Task SaveCredentialsAsync(CancellationToken cancellationToken)
    {
        EnsureUnlocked();
        
        var json = JsonSerializer.Serialize(_credentials.Values.ToList());
        var plaintext = Encoding.UTF8.GetBytes(json);
        
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        
        using var aes = new AesGcm(_derivedKey!, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        
        // Combine: salt + nonce + ciphertext + tag
        var result = new byte[_salt!.Length + nonce.Length + ciphertext.Length + tag.Length];
        var offset = 0;
        Array.Copy(_salt, 0, result, offset, _salt.Length);
        offset += _salt.Length;
        Array.Copy(nonce, 0, result, offset, nonce.Length);
        offset += nonce.Length;
        Array.Copy(ciphertext, 0, result, offset, ciphertext.Length);
        offset += ciphertext.Length;
        Array.Copy(tag, 0, result, offset, tag.Length);
        
        await File.WriteAllBytesAsync(_storagePath, result, cancellationToken);
    }
    
    private void EnsureUnlocked()
    {
        if (!_isUnlocked || _derivedKey == null)
        {
            throw new InvalidOperationException("Password store is locked. Call UnlockAsync first.");
        }
    }
    
    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256);
        
        return pbkdf2.GetBytes(KeySize);
    }
}
