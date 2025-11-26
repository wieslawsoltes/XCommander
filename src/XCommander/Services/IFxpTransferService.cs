using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// FXP transfer direction
/// </summary>
public enum FxpTransferDirection
{
    SourceToTarget,
    TargetToSource
}

/// <summary>
/// FXP connection information
/// </summary>
public record FxpServerConnection
{
    /// <summary>Server host</summary>
    public string Host { get; init; } = string.Empty;
    
    /// <summary>Server port (default 21)</summary>
    public int Port { get; init; } = 21;
    
    /// <summary>Username</summary>
    public string Username { get; init; } = string.Empty;
    
    /// <summary>Password</summary>
    public string Password { get; init; } = string.Empty;
    
    /// <summary>Use SSL/TLS</summary>
    public bool UseSsl { get; init; }
    
    /// <summary>Passive mode</summary>
    public bool PassiveMode { get; init; } = true;
    
    /// <summary>Current directory</summary>
    public string CurrentDirectory { get; init; } = "/";
}

/// <summary>
/// FXP file to transfer
/// </summary>
public record FxpFileInfo
{
    /// <summary>Remote path on source server</summary>
    public string SourcePath { get; init; } = string.Empty;
    
    /// <summary>Remote path on target server</summary>
    public string TargetPath { get; init; } = string.Empty;
    
    /// <summary>File size</summary>
    public long Size { get; init; }
    
    /// <summary>Is directory</summary>
    public bool IsDirectory { get; init; }
}

/// <summary>
/// FXP transfer result
/// </summary>
public record FxpTransferResult
{
    /// <summary>Transfer succeeded</summary>
    public bool Success { get; init; }
    
    /// <summary>File that was transferred</summary>
    public FxpFileInfo File { get; init; } = new();
    
    /// <summary>Transfer start time</summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>Transfer end time</summary>
    public DateTime EndTime { get; init; }
    
    /// <summary>Bytes transferred</summary>
    public long BytesTransferred { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// FXP transfer progress
/// </summary>
public record FxpTransferProgress
{
    /// <summary>Current file being transferred</summary>
    public string CurrentFile { get; init; } = string.Empty;
    
    /// <summary>Files completed</summary>
    public int FilesCompleted { get; init; }
    
    /// <summary>Total files</summary>
    public int TotalFiles { get; init; }
    
    /// <summary>Bytes transferred for current file</summary>
    public long CurrentFileBytes { get; init; }
    
    /// <summary>Total bytes for current file</summary>
    public long CurrentFileTotalBytes { get; init; }
    
    /// <summary>Total bytes transferred</summary>
    public long TotalBytesTransferred { get; init; }
    
    /// <summary>Total bytes to transfer</summary>
    public long TotalBytes { get; init; }
    
    /// <summary>Overall progress percentage</summary>
    public double ProgressPercent => TotalBytes > 0 
        ? (double)TotalBytesTransferred / TotalBytes * 100 
        : 0;
    
    /// <summary>Transfer speed in bytes per second</summary>
    public long SpeedBytesPerSecond { get; init; }
    
    /// <summary>Estimated time remaining</summary>
    public TimeSpan? EstimatedRemaining { get; init; }
}

/// <summary>
/// Service for FXP (File eXchange Protocol) server-to-server transfers.
/// TC equivalent: Direct FTP-to-FTP transfers without downloading to local.
/// </summary>
public interface IFxpTransferService
{
    /// <summary>
    /// Connect to source server
    /// </summary>
    Task<bool> ConnectSourceAsync(
        FxpServerConnection connection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect to target server
    /// </summary>
    Task<bool> ConnectTargetAsync(
        FxpServerConnection connection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from source server
    /// </summary>
    Task DisconnectSourceAsync();
    
    /// <summary>
    /// Disconnect from target server
    /// </summary>
    Task DisconnectTargetAsync();
    
    /// <summary>
    /// Check if FXP is supported between the two servers
    /// </summary>
    Task<bool> IsFxpSupportedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List files on source server
    /// </summary>
    Task<IReadOnlyList<FxpFileInfo>> ListSourceAsync(
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List files on target server
    /// </summary>
    Task<IReadOnlyList<FxpFileInfo>> ListTargetAsync(
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transfer single file via FXP
    /// </summary>
    Task<FxpTransferResult> TransferFileAsync(
        FxpFileInfo file,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transfer multiple files via FXP
    /// </summary>
    Task<IReadOnlyList<FxpTransferResult>> TransferFilesAsync(
        IEnumerable<FxpFileInfo> files,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transfer directory recursively via FXP
    /// </summary>
    Task<IReadOnlyList<FxpTransferResult>> TransferDirectoryAsync(
        string sourcePath,
        string targetPath,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create directory on source server
    /// </summary>
    Task<bool> CreateSourceDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create directory on target server
    /// </summary>
    Task<bool> CreateTargetDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Is source connected
    /// </summary>
    bool IsSourceConnected { get; }
    
    /// <summary>
    /// Is target connected
    /// </summary>
    bool IsTargetConnected { get; }
    
    /// <summary>
    /// Source connection info
    /// </summary>
    FxpServerConnection? SourceConnection { get; }
    
    /// <summary>
    /// Target connection info
    /// </summary>
    FxpServerConnection? TargetConnection { get; }
    
    /// <summary>
    /// Event raised when transfer progress updates
    /// </summary>
    event EventHandler<FxpTransferProgress>? TransferProgress;
    
    /// <summary>
    /// Event raised when file transfer completes
    /// </summary>
    event EventHandler<FxpTransferResult>? FileTransferred;
    
    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<(bool SourceConnected, bool TargetConnected)>? ConnectionStatusChanged;
}
