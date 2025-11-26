using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// USB device type
/// </summary>
public enum UsbDeviceType
{
    MassStorage,
    MTP,
    PTP,
    Unknown
}

/// <summary>
/// USB device information
/// </summary>
public record UsbDeviceInfo
{
    /// <summary>Device ID</summary>
    public string DeviceId { get; init; } = string.Empty;
    
    /// <summary>Device name</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Manufacturer</summary>
    public string Manufacturer { get; init; } = string.Empty;
    
    /// <summary>Device type</summary>
    public UsbDeviceType DeviceType { get; init; }
    
    /// <summary>Mount point or drive letter</summary>
    public string? MountPoint { get; init; }
    
    /// <summary>Total storage capacity</summary>
    public long TotalSpace { get; init; }
    
    /// <summary>Free storage space</summary>
    public long FreeSpace { get; init; }
    
    /// <summary>Is device connected</summary>
    public bool IsConnected { get; init; }
    
    /// <summary>Is device writable</summary>
    public bool IsWritable { get; init; }
}

/// <summary>
/// USB file information
/// </summary>
public record UsbFileInfo
{
    /// <summary>Full path on device</summary>
    public string Path { get; init; } = string.Empty;
    
    /// <summary>File or folder name</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Is directory</summary>
    public bool IsDirectory { get; init; }
    
    /// <summary>File size</summary>
    public long Size { get; init; }
    
    /// <summary>Last modified time</summary>
    public DateTime? LastModified { get; init; }
    
    /// <summary>Parent path</summary>
    public string? ParentPath { get; init; }
}

/// <summary>
/// USB transfer result
/// </summary>
public record UsbTransferResult
{
    /// <summary>Transfer succeeded</summary>
    public bool Success { get; init; }
    
    /// <summary>Source path</summary>
    public string SourcePath { get; init; } = string.Empty;
    
    /// <summary>Destination path</summary>
    public string DestinationPath { get; init; } = string.Empty;
    
    /// <summary>Bytes transferred</summary>
    public long BytesTransferred { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// USB transfer progress
/// </summary>
public record UsbTransferProgress
{
    /// <summary>Current file being transferred</summary>
    public string CurrentFile { get; init; } = string.Empty;
    
    /// <summary>Files completed</summary>
    public int FilesCompleted { get; init; }
    
    /// <summary>Total files</summary>
    public int TotalFiles { get; init; }
    
    /// <summary>Bytes transferred</summary>
    public long BytesTransferred { get; init; }
    
    /// <summary>Total bytes</summary>
    public long TotalBytes { get; init; }
    
    /// <summary>Progress percentage</summary>
    public double ProgressPercent => TotalBytes > 0 
        ? (double)BytesTransferred / TotalBytes * 100 
        : 0;
    
    /// <summary>Transfer speed in bytes per second</summary>
    public long SpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Service for direct USB device transfers.
/// TC equivalent: Direct USB cable transfers, MTP/PTP device access.
/// </summary>
public interface IUsbTransferService
{
    /// <summary>
    /// Get list of connected USB devices
    /// </summary>
    Task<IReadOnlyList<UsbDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect to a USB device
    /// </summary>
    Task<bool> ConnectDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from a USB device
    /// </summary>
    Task DisconnectDeviceAsync(string deviceId);
    
    /// <summary>
    /// List files on USB device
    /// </summary>
    Task<IReadOnlyList<UsbFileInfo>> ListFilesAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy file from local to USB device
    /// </summary>
    Task<UsbTransferResult> CopyToDeviceAsync(
        string deviceId,
        string localPath,
        string devicePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy file from USB device to local
    /// </summary>
    Task<UsbTransferResult> CopyFromDeviceAsync(
        string deviceId,
        string devicePath,
        string localPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy multiple files to device
    /// </summary>
    Task<IReadOnlyList<UsbTransferResult>> CopyFilesToDeviceAsync(
        string deviceId,
        IEnumerable<(string LocalPath, string DevicePath)> files,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy multiple files from device
    /// </summary>
    Task<IReadOnlyList<UsbTransferResult>> CopyFilesFromDeviceAsync(
        string deviceId,
        IEnumerable<(string DevicePath, string LocalPath)> files,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create directory on USB device
    /// </summary>
    Task<bool> CreateDirectoryAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete file or directory on USB device
    /// </summary>
    Task<bool> DeleteAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rename file or directory on USB device
    /// </summary>
    Task<bool> RenameAsync(
        string deviceId,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file info from USB device
    /// </summary>
    Task<UsbFileInfo?> GetFileInfoAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if device is connected
    /// </summary>
    bool IsDeviceConnected(string deviceId);
    
    /// <summary>
    /// Get connected device info
    /// </summary>
    UsbDeviceInfo? GetConnectedDevice(string deviceId);
    
    /// <summary>
    /// Event raised when device is connected
    /// </summary>
    event EventHandler<UsbDeviceInfo>? DeviceConnected;
    
    /// <summary>
    /// Event raised when device is disconnected
    /// </summary>
    event EventHandler<string>? DeviceDisconnected;
    
    /// <summary>
    /// Event raised when transfer progress updates
    /// </summary>
    event EventHandler<UsbTransferProgress>? TransferProgress;
}
