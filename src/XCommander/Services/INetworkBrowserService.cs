// Copyright (c) XCommander. All rights reserved.
// Licensed under the MIT License. See LICENSE file for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Service for browsing network resources (SMB shares, network neighborhood).
/// Similar to Total Commander's Network Neighborhood feature.
/// </summary>
public interface INetworkBrowserService
{
    /// <summary>
    /// Gets saved network connections.
    /// </summary>
    IReadOnlyList<NetworkConnection> SavedConnections { get; }
    
    /// <summary>
    /// Gets currently connected shares.
    /// </summary>
    IReadOnlyList<NetworkConnection> ActiveConnections { get; }
    
    /// <summary>
    /// Discovers network computers in the local network.
    /// </summary>
    Task<IReadOnlyList<NetworkComputer>> DiscoverComputersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets shares available on a computer.
    /// </summary>
    Task<IReadOnlyList<NetworkShare>> GetSharesAsync(string computerName, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects to a network share.
    /// </summary>
    Task<NetworkConnection> ConnectAsync(string uncPath, NetworkCredentials? credentials = null, bool persistent = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnects from a network share.
    /// </summary>
    Task DisconnectAsync(string uncPath, bool force = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Maps a network drive.
    /// </summary>
    Task<string> MapDriveAsync(string uncPath, string? driveLetter = null, NetworkCredentials? credentials = null, bool persistent = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unmaps a network drive.
    /// </summary>
    Task UnmapDriveAsync(string driveLetter, bool force = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets mapped network drives.
    /// </summary>
    Task<IReadOnlyList<MappedDrive>> GetMappedDrivesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists files and directories in a network path.
    /// </summary>
    Task<IReadOnlyList<NetworkItem>> ListAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves a network connection for later use.
    /// </summary>
    Task SaveConnectionAsync(NetworkConnection connection, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a saved connection.
    /// </summary>
    Task RemoveSavedConnectionAsync(string connectionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tests connectivity to a network path.
    /// </summary>
    Task<NetworkTestResult> TestConnectionAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about a network path.
    /// </summary>
    Task<NetworkPathInfo?> GetPathInfoAsync(string uncPath, NetworkCredentials? credentials = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when connection status changes.
    /// </summary>
    event EventHandler<NetworkConnectionEventArgs>? ConnectionChanged;
    
    /// <summary>
    /// Event raised when network discovery finds new computers.
    /// </summary>
    event EventHandler<NetworkDiscoveryEventArgs>? ComputerDiscovered;
}

/// <summary>
/// Credentials for network authentication.
/// </summary>
public class NetworkCredentials
{
    public string? Domain { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool SaveCredentials { get; set; }
}

/// <summary>
/// A computer discovered on the network.
/// </summary>
public class NetworkComputer
{
    public string Name { get; init; } = string.Empty;
    public string? IPAddress { get; init; }
    public string? Domain { get; init; }
    public string? Description { get; init; }
    public NetworkComputerType Type { get; init; }
    public bool IsReachable { get; init; }
    public DateTime DiscoveredAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Types of network computers.
/// </summary>
public enum NetworkComputerType
{
    Unknown,
    Workstation,
    Server,
    DomainController,
    NAS,
    Printer,
    Router
}

/// <summary>
/// A share available on a network computer.
/// </summary>
public class NetworkShare
{
    public string ComputerName { get; init; } = string.Empty;
    public string ShareName { get; init; } = string.Empty;
    public string UncPath => $@"\\{ComputerName}\{ShareName}";
    public string? Description { get; init; }
    public NetworkShareType Type { get; init; }
    public bool IsAccessible { get; init; }
    public long? TotalSpace { get; init; }
    public long? FreeSpace { get; init; }
}

/// <summary>
/// Types of network shares.
/// </summary>
public enum NetworkShareType
{
    Disk,
    Printer,
    Device,
    IPC,
    Admin,
    Special
}

/// <summary>
/// A connection to a network share.
/// </summary>
public class NetworkConnection
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string UncPath { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Username { get; set; }
    public bool IsPersistent { get; set; }
    public string? MappedDrive { get; set; }
    public NetworkConnectionStatus Status { get; set; }
    public DateTime? LastConnected { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// Network connection status.
/// </summary>
public enum NetworkConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>
/// A mapped network drive.
/// </summary>
public class MappedDrive
{
    public string DriveLetter { get; init; } = string.Empty;
    public string UncPath { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public bool IsPersistent { get; init; }
    public bool IsAvailable { get; init; }
    public long? TotalSpace { get; init; }
    public long? FreeSpace { get; init; }
}

/// <summary>
/// A file or directory in a network path.
/// </summary>
public class NetworkItem
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? AccessedAt { get; init; }
    public NetworkItemAttributes Attributes { get; init; }
    public string? Owner { get; init; }
    
    public string SizeDisplay => IsDirectory ? string.Empty : FormatSize(Size);
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
/// Network item attributes.
/// </summary>
[Flags]
public enum NetworkItemAttributes
{
    None = 0,
    ReadOnly = 1,
    Hidden = 2,
    System = 4,
    Directory = 8,
    Archive = 16,
    Encrypted = 32,
    Compressed = 64
}

/// <summary>
/// Result of testing a network connection.
/// </summary>
public class NetworkTestResult
{
    public bool Success { get; init; }
    public int ResponseTimeMs { get; init; }
    public bool RequiresAuthentication { get; init; }
    public bool PathExists { get; init; }
    public bool HasReadAccess { get; init; }
    public bool HasWriteAccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ServerType { get; init; }
}

/// <summary>
/// Information about a network path.
/// </summary>
public class NetworkPathInfo
{
    public string UncPath { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string ShareName { get; init; } = string.Empty;
    public string? RelativePath { get; init; }
    public NetworkShareType ShareType { get; init; }
    public long? TotalSpace { get; init; }
    public long? FreeSpace { get; init; }
    public long? UsedSpace => TotalSpace.HasValue && FreeSpace.HasValue 
        ? TotalSpace.Value - FreeSpace.Value 
        : null;
    public string? FileSystem { get; init; }
    public bool SupportsCompression { get; init; }
    public bool SupportsEncryption { get; init; }
    public bool SupportsSparseFiles { get; init; }
}

/// <summary>
/// Event args for network connection changes.
/// </summary>
public class NetworkConnectionEventArgs : EventArgs
{
    public string ConnectionId { get; init; } = string.Empty;
    public NetworkConnectionEventType EventType { get; init; }
    public NetworkConnection? Connection { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Types of network connection events.
/// </summary>
public enum NetworkConnectionEventType
{
    Connected,
    Disconnected,
    Saved,
    Removed,
    DriveMapped,
    DriveUnmapped,
    Error
}

/// <summary>
/// Event args for network discovery.
/// </summary>
public class NetworkDiscoveryEventArgs : EventArgs
{
    public NetworkComputer Computer { get; init; } = new();
    public bool IsNewDiscovery { get; init; }
}
