// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Network browser service implementation for SMB/CIFS shares.
/// </summary>
public class NetworkBrowserService : INetworkBrowserService
{
    private readonly List<NetworkConnection> _savedConnections;
    private readonly List<NetworkConnection> _activeConnections;
    private readonly string _configPath;
    private readonly object _lock = new();
    private bool _loaded;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public IReadOnlyList<NetworkConnection> SavedConnections
    {
        get
        {
            lock (_lock)
            {
                return _savedConnections.ToList().AsReadOnly();
            }
        }
    }
    
    public IReadOnlyList<NetworkConnection> ActiveConnections
    {
        get
        {
            lock (_lock)
            {
                return _activeConnections.ToList().AsReadOnly();
            }
        }
    }
    
    public event EventHandler<NetworkConnectionEventArgs>? ConnectionChanged;
    public event EventHandler<NetworkDiscoveryEventArgs>? ComputerDiscovered;
    
    public NetworkBrowserService()
    {
        _savedConnections = new List<NetworkConnection>();
        _activeConnections = new List<NetworkConnection>();
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "XCommander", "network-connections.json");
    }
    
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded) return;
        
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                var connections = JsonSerializer.Deserialize<List<NetworkConnection>>(json, JsonOptions);
                if (connections != null)
                {
                    lock (_lock)
                    {
                        _savedConnections.Clear();
                        _savedConnections.AddRange(connections);
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
            
            List<NetworkConnection> connectionsToSave;
            lock (_lock)
            {
                connectionsToSave = _savedConnections.ToList();
            }
            
            var json = JsonSerializer.Serialize(connectionsToSave, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    public async Task<IReadOnlyList<NetworkComputer>> DiscoverComputersAsync(CancellationToken cancellationToken = default)
    {
        var computers = new List<NetworkComputer>();
        
        await Task.Run(() =>
        {
            try
            {
                // Get local network information
                var hostName = Dns.GetHostName();
                var localAddresses = Dns.GetHostAddresses(hostName);
                
                // Add local computer
                computers.Add(new NetworkComputer
                {
                    Name = hostName,
                    IPAddress = localAddresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString(),
                    Type = NetworkComputerType.Workstation,
                    IsReachable = true
                });
                
                // Scan local subnet for active computers
                foreach (var localAddress in localAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var addressBytes = localAddress.GetAddressBytes();
                    var baseAddress = $"{addressBytes[0]}.{addressBytes[1]}.{addressBytes[2]}";
                    
                    // Scan first 10 addresses as a sample (full scan would be too slow)
                    var tasks = new List<Task>();
                    for (int i = 1; i <= 10 && !cancellationToken.IsCancellationRequested; i++)
                    {
                        var ip = $"{baseAddress}.{i}";
                        if (ip == localAddress.ToString()) continue;
                        
                        var pingTask = PingHostAsync(ip, cancellationToken);
                        tasks.Add(pingTask.ContinueWith(t =>
                        {
                            if (t.Result != null)
                            {
                                lock (computers)
                                {
                                    computers.Add(t.Result);
                                }
                                
                                ComputerDiscovered?.Invoke(this, new NetworkDiscoveryEventArgs
                                {
                                    Computer = t.Result,
                                    IsNewDiscovery = true
                                });
                            }
                        }, cancellationToken));
                    }
                    
                    Task.WaitAll(tasks.ToArray(), cancellationToken);
                }
            }
            catch
            {
                // Ignore discovery errors
            }
        }, cancellationToken);
        
        return computers;
    }
    
    private async Task<NetworkComputer?> PingHostAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 100);
            
            if (reply.Status == IPStatus.Success)
            {
                string? hostName = null;
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                    hostName = hostEntry.HostName;
                }
                catch
                {
                    // Could not resolve hostname
                }
                
                return new NetworkComputer
                {
                    Name = hostName ?? ipAddress,
                    IPAddress = ipAddress,
                    Type = NetworkComputerType.Unknown,
                    IsReachable = true
                };
            }
        }
        catch
        {
            // Ignore ping errors
        }
        
        return null;
    }
    
    public async Task<IReadOnlyList<NetworkShare>> GetSharesAsync(string computerName, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        var shares = new List<NetworkShare>();
        
        await Task.Run(() =>
        {
            try
            {
                var uncPath = $@"\\{computerName}";
                
                if (!Directory.Exists(uncPath))
                {
                    return;
                }
                
                // Get directory listing which shows shares
                var directories = Directory.GetDirectories(uncPath);
                foreach (var dir in directories)
                {
                    var shareName = Path.GetFileName(dir);
                    
                    long? totalSpace = null;
                    long? freeSpace = null;
                    bool isAccessible = true;
                    
                    try
                    {
                        var driveInfo = new DriveInfo(dir);
                        if (driveInfo.IsReady)
                        {
                            totalSpace = driveInfo.TotalSize;
                            freeSpace = driveInfo.AvailableFreeSpace;
                        }
                    }
                    catch
                    {
                        isAccessible = false;
                    }
                    
                    shares.Add(new NetworkShare
                    {
                        ComputerName = computerName,
                        ShareName = shareName,
                        Type = DetermineShareType(shareName),
                        IsAccessible = isAccessible,
                        TotalSpace = totalSpace,
                        FreeSpace = freeSpace
                    });
                }
            }
            catch
            {
                // Ignore errors
            }
        }, cancellationToken);
        
        return shares;
    }
    
    private static NetworkShareType DetermineShareType(string shareName)
    {
        if (shareName.EndsWith("$"))
        {
            if (shareName.Equals("IPC$", StringComparison.OrdinalIgnoreCase))
                return NetworkShareType.IPC;
            if (shareName.Equals("ADMIN$", StringComparison.OrdinalIgnoreCase))
                return NetworkShareType.Admin;
            if (shareName.Length == 2 && char.IsLetter(shareName[0]))
                return NetworkShareType.Admin; // Hidden drive share like C$
            return NetworkShareType.Special;
        }
        
        return NetworkShareType.Disk;
    }
    
    public async Task<NetworkConnection> ConnectAsync(string uncPath, NetworkCredentials? credentials = null, bool persistent = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        var connection = new NetworkConnection
        {
            Name = Path.GetFileName(uncPath.TrimEnd('\\')),
            UncPath = uncPath,
            Domain = credentials?.Domain,
            Username = credentials?.Username,
            IsPersistent = persistent,
            Status = NetworkConnectionStatus.Connecting
        };
        
        try
        {
            // Test access to the path
            await Task.Run(() =>
            {
                if (!Directory.Exists(uncPath))
                {
                    throw new DirectoryNotFoundException($"Cannot access {uncPath}");
                }
            }, cancellationToken);
            
            connection.Status = NetworkConnectionStatus.Connected;
            connection.LastConnected = DateTime.Now;
            
            lock (_lock)
            {
                _activeConnections.Add(connection);
            }
            
            ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
            {
                ConnectionId = connection.Id,
                EventType = NetworkConnectionEventType.Connected,
                Connection = connection
            });
        }
        catch (Exception ex)
        {
            connection.Status = NetworkConnectionStatus.Error;
            
            ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
            {
                ConnectionId = connection.Id,
                EventType = NetworkConnectionEventType.Error,
                Connection = connection,
                ErrorMessage = ex.Message
            });
            
            throw;
        }
        
        return connection;
    }
    
    public async Task DisconnectAsync(string uncPath, bool force = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        NetworkConnection? connection;
        lock (_lock)
        {
            connection = _activeConnections.FirstOrDefault(c => c.UncPath.Equals(uncPath, StringComparison.OrdinalIgnoreCase));
            if (connection != null)
            {
                _activeConnections.Remove(connection);
            }
        }
        
        if (connection != null)
        {
            connection.Status = NetworkConnectionStatus.Disconnected;
            
            ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
            {
                ConnectionId = connection.Id,
                EventType = NetworkConnectionEventType.Disconnected,
                Connection = connection
            });
        }
    }
    
    public async Task<string> MapDriveAsync(string uncPath, string? driveLetter = null, NetworkCredentials? credentials = null, bool persistent = true, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        // Find available drive letter if not specified
        if (string.IsNullOrEmpty(driveLetter))
        {
            driveLetter = await Task.Run(() =>
            {
                var usedLetters = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
                for (char letter = 'Z'; letter >= 'D'; letter--)
                {
                    if (!usedLetters.Contains(letter))
                    {
                        return $"{letter}:";
                    }
                }
                throw new InvalidOperationException("No available drive letters");
            }, cancellationToken);
        }
        
        // Platform-specific drive mapping would go here
        // On Windows: WNetAddConnection2
        // On macOS/Linux: mount command
        
        // For now, this is a placeholder implementation
        var connection = new NetworkConnection
        {
            Name = Path.GetFileName(uncPath.TrimEnd('\\')),
            UncPath = uncPath,
            MappedDrive = driveLetter,
            IsPersistent = persistent,
            Status = NetworkConnectionStatus.Connected,
            LastConnected = DateTime.Now
        };
        
        lock (_lock)
        {
            _activeConnections.Add(connection);
        }
        
        ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
        {
            ConnectionId = connection.Id,
            EventType = NetworkConnectionEventType.DriveMapped,
            Connection = connection
        });
        
        return driveLetter;
    }
    
    public async Task UnmapDriveAsync(string driveLetter, bool force = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        NetworkConnection? connection;
        lock (_lock)
        {
            connection = _activeConnections.FirstOrDefault(c => 
                c.MappedDrive?.Equals(driveLetter, StringComparison.OrdinalIgnoreCase) == true);
            
            if (connection != null)
            {
                _activeConnections.Remove(connection);
            }
        }
        
        // Platform-specific drive unmapping would go here
        
        if (connection != null)
        {
            connection.Status = NetworkConnectionStatus.Disconnected;
            connection.MappedDrive = null;
            
            ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
            {
                ConnectionId = connection.Id,
                EventType = NetworkConnectionEventType.DriveUnmapped,
                Connection = connection
            });
        }
    }
    
    public async Task<IReadOnlyList<MappedDrive>> GetMappedDrivesAsync(CancellationToken cancellationToken = default)
    {
        var drives = new List<MappedDrive>();
        
        await Task.Run(() =>
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                if (drive.DriveType == DriveType.Network)
                {
                    try
                    {
                        drives.Add(new MappedDrive
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            UncPath = drive.Name, // Would need P/Invoke to get actual UNC path
                            IsPersistent = true, // Would need to check registry on Windows
                            IsAvailable = drive.IsReady,
                            TotalSpace = drive.IsReady ? drive.TotalSize : null,
                            FreeSpace = drive.IsReady ? drive.AvailableFreeSpace : null
                        });
                    }
                    catch
                    {
                        // Drive not accessible
                        drives.Add(new MappedDrive
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            UncPath = drive.Name,
                            IsAvailable = false
                        });
                    }
                }
            }
        }, cancellationToken);
        
        return drives;
    }
    
    public async Task<IReadOnlyList<NetworkItem>> ListAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        var items = new List<NetworkItem>();
        
        await Task.Run(() =>
        {
            try
            {
                var directory = new DirectoryInfo(uncPath);
                
                if (!directory.Exists)
                {
                    return;
                }
                
                // Get directories
                foreach (var dir in directory.GetDirectories())
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    try
                    {
                        items.Add(new NetworkItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            CreatedAt = dir.CreationTime,
                            ModifiedAt = dir.LastWriteTime,
                            AccessedAt = dir.LastAccessTime,
                            Attributes = ConvertAttributes(dir.Attributes)
                        });
                    }
                    catch
                    {
                        // Skip inaccessible directories
                    }
                }
                
                // Get files
                foreach (var file in directory.GetFiles())
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    try
                    {
                        items.Add(new NetworkItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            CreatedAt = file.CreationTime,
                            ModifiedAt = file.LastWriteTime,
                            AccessedAt = file.LastAccessTime,
                            Attributes = ConvertAttributes(file.Attributes)
                        });
                    }
                    catch
                    {
                        // Skip inaccessible files
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }, cancellationToken);
        
        return items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name).ToList();
    }
    
    private static NetworkItemAttributes ConvertAttributes(FileAttributes attrs)
    {
        var result = NetworkItemAttributes.None;
        
        if (attrs.HasFlag(FileAttributes.ReadOnly))
            result |= NetworkItemAttributes.ReadOnly;
        if (attrs.HasFlag(FileAttributes.Hidden))
            result |= NetworkItemAttributes.Hidden;
        if (attrs.HasFlag(FileAttributes.System))
            result |= NetworkItemAttributes.System;
        if (attrs.HasFlag(FileAttributes.Directory))
            result |= NetworkItemAttributes.Directory;
        if (attrs.HasFlag(FileAttributes.Archive))
            result |= NetworkItemAttributes.Archive;
        if (attrs.HasFlag(FileAttributes.Encrypted))
            result |= NetworkItemAttributes.Encrypted;
        if (attrs.HasFlag(FileAttributes.Compressed))
            result |= NetworkItemAttributes.Compressed;
        
        return result;
    }
    
    public async Task SaveConnectionAsync(NetworkConnection connection, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        lock (_lock)
        {
            var existing = _savedConnections.FirstOrDefault(c => c.Id == connection.Id);
            if (existing != null)
            {
                var index = _savedConnections.IndexOf(existing);
                _savedConnections[index] = connection;
            }
            else
            {
                _savedConnections.Add(connection);
            }
        }
        
        await SaveAsync(cancellationToken);
        
        ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
        {
            ConnectionId = connection.Id,
            EventType = NetworkConnectionEventType.Saved,
            Connection = connection
        });
    }
    
    public async Task RemoveSavedConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        
        NetworkConnection? removed;
        lock (_lock)
        {
            removed = _savedConnections.FirstOrDefault(c => c.Id == connectionId);
            if (removed != null)
            {
                _savedConnections.Remove(removed);
            }
        }
        
        if (removed != null)
        {
            await SaveAsync(cancellationToken);
            
            ConnectionChanged?.Invoke(this, new NetworkConnectionEventArgs
            {
                ConnectionId = connectionId,
                EventType = NetworkConnectionEventType.Removed,
                Connection = removed
            });
        }
    }
    
    public async Task<NetworkTestResult> TestConnectionAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await Task.Run(() =>
            {
                var pathExists = false;
                var hasReadAccess = false;
                var hasWriteAccess = false;
                var requiresAuth = false;
                
                try
                {
                    pathExists = Directory.Exists(uncPath);
                    
                    if (pathExists)
                    {
                        // Test read access
                        try
                        {
                            Directory.GetFiles(uncPath);
                            hasReadAccess = true;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            requiresAuth = true;
                        }
                        
                        // Test write access
                        if (hasReadAccess)
                        {
                            try
                            {
                                var testFile = Path.Combine(uncPath, $".xcommander_test_{Guid.NewGuid():N}");
                                File.WriteAllText(testFile, "test");
                                File.Delete(testFile);
                                hasWriteAccess = true;
                            }
                            catch
                            {
                                // No write access
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new NetworkTestResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }
                
                return new NetworkTestResult
                {
                    Success = pathExists,
                    PathExists = pathExists,
                    RequiresAuthentication = requiresAuth,
                    HasReadAccess = hasReadAccess,
                    HasWriteAccess = hasWriteAccess,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }, cancellationToken);
            
            stopwatch.Stop();
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new NetworkTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
    
    public async Task<NetworkPathInfo?> GetPathInfoAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Parse UNC path
                var parts = uncPath.TrimStart('\\').Split('\\');
                if (parts.Length < 2)
                {
                    return null;
                }
                
                var computerName = parts[0];
                var shareName = parts[1];
                var relativePath = parts.Length > 2 ? string.Join("\\", parts.Skip(2)) : null;
                
                long? totalSpace = null;
                long? freeSpace = null;
                string? fileSystem = null;
                bool supportsCompression = false;
                bool supportsEncryption = false;
                
                try
                {
                    var rootPath = $@"\\{computerName}\{shareName}";
                    var driveInfo = new DriveInfo(rootPath);
                    if (driveInfo.IsReady)
                    {
                        totalSpace = driveInfo.TotalSize;
                        freeSpace = driveInfo.AvailableFreeSpace;
                        fileSystem = driveInfo.DriveFormat;
                        
                        // NTFS supports compression and encryption
                        if (fileSystem?.Equals("NTFS", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            supportsCompression = true;
                            supportsEncryption = true;
                        }
                    }
                }
                catch
                {
                    // Cannot get drive info
                }
                
                return new NetworkPathInfo
                {
                    UncPath = uncPath,
                    ComputerName = computerName,
                    ShareName = shareName,
                    RelativePath = relativePath,
                    ShareType = DetermineShareType(shareName),
                    TotalSpace = totalSpace,
                    FreeSpace = freeSpace,
                    FileSystem = fileSystem,
                    SupportsCompression = supportsCompression,
                    SupportsEncryption = supportsEncryption
                };
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }
}
