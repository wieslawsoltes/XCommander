using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of USB device transfers.
/// Supports mass storage devices directly, with MTP/PTP as future extension.
/// </summary>
public sealed class UsbTransferService : IUsbTransferService
{
    private readonly ConcurrentDictionary<string, UsbDeviceInfo> _connectedDevices = new();
    private readonly ILongPathService _longPathService;
    
    public event EventHandler<UsbDeviceInfo>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    public event EventHandler<UsbTransferProgress>? TransferProgress;
    
    public UsbTransferService(ILongPathService longPathService)
    {
        _longPathService = longPathService;
    }
    
    public Task<IReadOnlyList<UsbDeviceInfo>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = new List<UsbDeviceInfo>();
        
        // Get removable drives (USB mass storage)
        foreach (var drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    devices.Add(new UsbDeviceInfo
                    {
                        DeviceId = drive.Name,
                        Name = string.IsNullOrEmpty(drive.VolumeLabel) 
                            ? $"Removable Drive ({drive.Name.TrimEnd('\\')})" 
                            : drive.VolumeLabel,
                        Manufacturer = "Unknown",
                        DeviceType = UsbDeviceType.MassStorage,
                        MountPoint = drive.Name,
                        TotalSpace = drive.TotalSize,
                        FreeSpace = drive.AvailableFreeSpace,
                        IsConnected = true,
                        IsWritable = !drive.IsReadOnly()
                    });
                }
            }
            catch (IOException)
            {
                // Drive not ready
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied
            }
        }
        
        // On macOS, check /Volumes for USB drives
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var volumesPath = "/Volumes";
            if (Directory.Exists(volumesPath))
            {
                foreach (var volume in Directory.GetDirectories(volumesPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var volumeName = Path.GetFileName(volume);
                        
                        // Skip Macintosh HD and similar system volumes
                        if (volumeName.StartsWith("Macintosh", StringComparison.OrdinalIgnoreCase))
                            continue;
                        
                        var driveInfo = new DriveInfo(volume);
                        if (driveInfo.DriveType == DriveType.Removable && driveInfo.IsReady)
                        {
                            if (!devices.Any(d => d.MountPoint == volume))
                            {
                                devices.Add(new UsbDeviceInfo
                                {
                                    DeviceId = volume,
                                    Name = volumeName,
                                    Manufacturer = "Unknown",
                                    DeviceType = UsbDeviceType.MassStorage,
                                    MountPoint = volume,
                                    TotalSpace = driveInfo.TotalSize,
                                    FreeSpace = driveInfo.AvailableFreeSpace,
                                    IsConnected = true,
                                    IsWritable = true
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible volumes
                    }
                }
            }
        }
        
        // On Linux, check /media and /run/media
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var mediaPaths = new[] { "/media", $"/run/media/{Environment.UserName}" };
            
            foreach (var mediaPath in mediaPaths)
            {
                if (!Directory.Exists(mediaPath))
                    continue;
                
                foreach (var mount in Directory.GetDirectories(mediaPath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var mountName = Path.GetFileName(mount);
                        
                        if (!devices.Any(d => d.MountPoint == mount))
                        {
                            var driveInfo = new DriveInfo(mount);
                            
                            devices.Add(new UsbDeviceInfo
                            {
                                DeviceId = mount,
                                Name = mountName,
                                Manufacturer = "Unknown",
                                DeviceType = UsbDeviceType.MassStorage,
                                MountPoint = mount,
                                TotalSpace = driveInfo.IsReady ? driveInfo.TotalSize : 0,
                                FreeSpace = driveInfo.IsReady ? driveInfo.AvailableFreeSpace : 0,
                                IsConnected = true,
                                IsWritable = true
                            });
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible mounts
                    }
                }
            }
        }
        
        return Task.FromResult<IReadOnlyList<UsbDeviceInfo>>(devices);
    }
    
    public Task<bool> ConnectDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        // For mass storage, connection is automatic via mount point
        var mountPoint = deviceId;
        
        if (!Directory.Exists(mountPoint))
            return Task.FromResult(false);
        
        try
        {
            var driveInfo = new DriveInfo(mountPoint);
            
            var device = new UsbDeviceInfo
            {
                DeviceId = deviceId,
                Name = driveInfo.IsReady ? driveInfo.VolumeLabel : Path.GetFileName(mountPoint),
                DeviceType = UsbDeviceType.MassStorage,
                MountPoint = mountPoint,
                TotalSpace = driveInfo.IsReady ? driveInfo.TotalSize : 0,
                FreeSpace = driveInfo.IsReady ? driveInfo.AvailableFreeSpace : 0,
                IsConnected = true,
                IsWritable = driveInfo.IsReady && !driveInfo.IsReadOnly()
            };
            
            _connectedDevices[deviceId] = device;
            DeviceConnected?.Invoke(this, device);
            
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task DisconnectDeviceAsync(string deviceId)
    {
        if (_connectedDevices.TryRemove(deviceId, out _))
        {
            DeviceDisconnected?.Invoke(this, deviceId);
        }
        
        return Task.CompletedTask;
    }
    
    public Task<IReadOnlyList<UsbFileInfo>> ListFilesAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
            return Task.FromResult<IReadOnlyList<UsbFileInfo>>(Array.Empty<UsbFileInfo>());
        
        var fullPath = Path.Combine(device.MountPoint, path.TrimStart('/', '\\'));
        var normalizedPath = _longPathService.NormalizePath(fullPath);
        
        if (!Directory.Exists(normalizedPath))
            return Task.FromResult<IReadOnlyList<UsbFileInfo>>(Array.Empty<UsbFileInfo>());
        
        var result = new List<UsbFileInfo>();
        
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(normalizedPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var relativePath = Path.GetRelativePath(device.MountPoint, dir);
                    
                    result.Add(new UsbFileInfo
                    {
                        Path = relativePath,
                        Name = dirInfo.Name,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dirInfo.LastWriteTime,
                        ParentPath = path
                    });
                }
                catch { }
            }
            
            foreach (var file in Directory.EnumerateFiles(normalizedPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(device.MountPoint, file);
                    
                    result.Add(new UsbFileInfo
                    {
                        Path = relativePath,
                        Name = fileInfo.Name,
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        ParentPath = path
                    });
                }
                catch { }
            }
        }
        catch { }
        
        return Task.FromResult<IReadOnlyList<UsbFileInfo>>(result);
    }
    
    public async Task<UsbTransferResult> CopyToDeviceAsync(
        string deviceId,
        string localPath,
        string devicePath,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
        {
            return new UsbTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = devicePath,
                ErrorMessage = "Device not connected"
            };
        }
        
        var fullDevicePath = Path.Combine(device.MountPoint, devicePath.TrimStart('/', '\\'));
        var normalizedLocal = _longPathService.NormalizePath(localPath);
        var normalizedDevice = _longPathService.NormalizePath(fullDevicePath);
        
        try
        {
            var fileInfo = new FileInfo(normalizedLocal);
            var stopwatch = Stopwatch.StartNew();
            long bytesTransferred = 0;
            
            var directory = Path.GetDirectoryName(normalizedDevice);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            const int bufferSize = 1024 * 1024; // 1MB buffer
            using var sourceStream = new FileStream(normalizedLocal, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
            using var destStream = new FileStream(normalizedDevice, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough);
            
            var buffer = new byte[bufferSize];
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                bytesTransferred += bytesRead;
                
                TransferProgress?.Invoke(this, new UsbTransferProgress
                {
                    CurrentFile = localPath,
                    FilesCompleted = 0,
                    TotalFiles = 1,
                    BytesTransferred = bytesTransferred,
                    TotalBytes = fileInfo.Length,
                    SpeedBytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                        ? bytesTransferred * 1000 / stopwatch.ElapsedMilliseconds 
                        : 0
                });
            }
            
            return new UsbTransferResult
            {
                Success = true,
                SourcePath = localPath,
                DestinationPath = devicePath,
                BytesTransferred = bytesTransferred
            };
        }
        catch (Exception ex)
        {
            return new UsbTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = devicePath,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<UsbTransferResult> CopyFromDeviceAsync(
        string deviceId,
        string devicePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
        {
            return new UsbTransferResult
            {
                Success = false,
                SourcePath = devicePath,
                DestinationPath = localPath,
                ErrorMessage = "Device not connected"
            };
        }
        
        var fullDevicePath = Path.Combine(device.MountPoint, devicePath.TrimStart('/', '\\'));
        var normalizedDevice = _longPathService.NormalizePath(fullDevicePath);
        var normalizedLocal = _longPathService.NormalizePath(localPath);
        
        try
        {
            var fileInfo = new FileInfo(normalizedDevice);
            var stopwatch = Stopwatch.StartNew();
            long bytesTransferred = 0;
            
            var directory = Path.GetDirectoryName(normalizedLocal);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            const int bufferSize = 1024 * 1024; // 1MB buffer
            using var sourceStream = new FileStream(normalizedDevice, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
            using var destStream = new FileStream(normalizedLocal, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough);
            
            var buffer = new byte[bufferSize];
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                bytesTransferred += bytesRead;
                
                TransferProgress?.Invoke(this, new UsbTransferProgress
                {
                    CurrentFile = devicePath,
                    FilesCompleted = 0,
                    TotalFiles = 1,
                    BytesTransferred = bytesTransferred,
                    TotalBytes = fileInfo.Length,
                    SpeedBytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                        ? bytesTransferred * 1000 / stopwatch.ElapsedMilliseconds 
                        : 0
                });
            }
            
            return new UsbTransferResult
            {
                Success = true,
                SourcePath = devicePath,
                DestinationPath = localPath,
                BytesTransferred = bytesTransferred
            };
        }
        catch (Exception ex)
        {
            return new UsbTransferResult
            {
                Success = false,
                SourcePath = devicePath,
                DestinationPath = localPath,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<IReadOnlyList<UsbTransferResult>> CopyFilesToDeviceAsync(
        string deviceId,
        IEnumerable<(string LocalPath, string DevicePath)> files,
        CancellationToken cancellationToken = default)
    {
        var results = new List<UsbTransferResult>();
        
        foreach (var (localPath, devicePath) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var result = await CopyToDeviceAsync(deviceId, localPath, devicePath, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    public async Task<IReadOnlyList<UsbTransferResult>> CopyFilesFromDeviceAsync(
        string deviceId,
        IEnumerable<(string DevicePath, string LocalPath)> files,
        CancellationToken cancellationToken = default)
    {
        var results = new List<UsbTransferResult>();
        
        foreach (var (devicePath, localPath) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var result = await CopyFromDeviceAsync(deviceId, devicePath, localPath, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }
    
    public Task<bool> CreateDirectoryAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
            return Task.FromResult(false);
        
        try
        {
            var fullPath = Path.Combine(device.MountPoint, path.TrimStart('/', '\\'));
            var normalizedPath = _longPathService.NormalizePath(fullPath);
            
            Directory.CreateDirectory(normalizedPath);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<bool> DeleteAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
            return Task.FromResult(false);
        
        try
        {
            var fullPath = Path.Combine(device.MountPoint, path.TrimStart('/', '\\'));
            var normalizedPath = _longPathService.NormalizePath(fullPath);
            
            if (Directory.Exists(normalizedPath))
            {
                Directory.Delete(normalizedPath, true);
            }
            else if (File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
            }
            else
            {
                return Task.FromResult(false);
            }
            
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<bool> RenameAsync(
        string deviceId,
        string oldPath,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
            return Task.FromResult(false);
        
        try
        {
            var fullOldPath = Path.Combine(device.MountPoint, oldPath.TrimStart('/', '\\'));
            var fullNewPath = Path.Combine(device.MountPoint, newPath.TrimStart('/', '\\'));
            
            var normalizedOld = _longPathService.NormalizePath(fullOldPath);
            var normalizedNew = _longPathService.NormalizePath(fullNewPath);
            
            if (Directory.Exists(normalizedOld))
            {
                Directory.Move(normalizedOld, normalizedNew);
            }
            else if (File.Exists(normalizedOld))
            {
                File.Move(normalizedOld, normalizedNew);
            }
            else
            {
                return Task.FromResult(false);
            }
            
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    public Task<UsbFileInfo?> GetFileInfoAsync(
        string deviceId,
        string path,
        CancellationToken cancellationToken = default)
    {
        var device = GetConnectedDevice(deviceId);
        if (device?.MountPoint == null)
            return Task.FromResult<UsbFileInfo?>(null);
        
        try
        {
            var fullPath = Path.Combine(device.MountPoint, path.TrimStart('/', '\\'));
            var normalizedPath = _longPathService.NormalizePath(fullPath);
            
            if (Directory.Exists(normalizedPath))
            {
                var dirInfo = new DirectoryInfo(normalizedPath);
                return Task.FromResult<UsbFileInfo?>(new UsbFileInfo
                {
                    Path = path,
                    Name = dirInfo.Name,
                    IsDirectory = true,
                    Size = 0,
                    LastModified = dirInfo.LastWriteTime,
                    ParentPath = Path.GetDirectoryName(path)
                });
            }
            
            if (File.Exists(normalizedPath))
            {
                var fileInfo = new FileInfo(normalizedPath);
                return Task.FromResult<UsbFileInfo?>(new UsbFileInfo
                {
                    Path = path,
                    Name = fileInfo.Name,
                    IsDirectory = false,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    ParentPath = Path.GetDirectoryName(path)
                });
            }
            
            return Task.FromResult<UsbFileInfo?>(null);
        }
        catch
        {
            return Task.FromResult<UsbFileInfo?>(null);
        }
    }
    
    public bool IsDeviceConnected(string deviceId)
    {
        return _connectedDevices.ContainsKey(deviceId);
    }
    
    public UsbDeviceInfo? GetConnectedDevice(string deviceId)
    {
        return _connectedDevices.TryGetValue(deviceId, out var device) ? device : null;
    }
}

file static class DriveInfoExtensions
{
    public static bool IsReadOnly(this DriveInfo driveInfo)
    {
        try
        {
            // Try to get write access to root directory
            var testPath = Path.Combine(driveInfo.RootDirectory.FullName, $".write_test_{Guid.NewGuid()}");
            using var stream = File.Create(testPath, 1, FileOptions.DeleteOnClose);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
