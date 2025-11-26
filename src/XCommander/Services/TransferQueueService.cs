using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XCommander.Models;

namespace XCommander.Services;

/// <summary>
/// Transfer item status
/// </summary>
public enum TransferStatus
{
    Pending,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Transfer direction
/// </summary>
public enum TransferDirection
{
    Download,
    Upload
}

/// <summary>
/// Transfer protocol type
/// </summary>
public enum TransferProtocol
{
    FTP,
    FTPS,
    SFTP
}

/// <summary>
/// Represents a single transfer item in the queue
/// </summary>
public partial class TransferItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string LocalPath { get; init; } = string.Empty;
    public string RemotePath { get; init; } = string.Empty;
    public TransferDirection Direction { get; init; }
    public TransferProtocol Protocol { get; init; }
    public string ConnectionName { get; init; } = string.Empty;
    public DateTime QueuedTime { get; } = DateTime.Now;
    
    [ObservableProperty]
    private TransferStatus _status = TransferStatus.Pending;
    
    [ObservableProperty]
    private long _totalBytes;
    
    [ObservableProperty]
    private long _transferredBytes;
    
    [ObservableProperty]
    private double _progressPercent;
    
    [ObservableProperty]
    private long _speedBytesPerSecond;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private DateTime? _startTime;
    
    [ObservableProperty]
    private DateTime? _completedTime;
    
    public string FileName => Path.GetFileName(Direction == TransferDirection.Download ? RemotePath : LocalPath);
    
    public string DisplaySpeed => FormatSpeed(SpeedBytesPerSecond);
    
    public string DisplayProgress => $"{FormatSize(TransferredBytes)} / {FormatSize(TotalBytes)}";
    
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (SpeedBytesPerSecond <= 0 || TotalBytes <= 0)
                return null;
            var remaining = TotalBytes - TransferredBytes;
            return TimeSpan.FromSeconds(remaining / SpeedBytesPerSecond);
        }
    }
    
    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return suffixIndex == 0 
            ? $"{size:N0} {suffixes[suffixIndex]}" 
            : $"{size:N2} {suffixes[suffixIndex]}";
    }
    
    private static string FormatSpeed(long bytesPerSecond)
    {
        return $"{FormatSize(bytesPerSecond)}/s";
    }
}

/// <summary>
/// Service for managing background file transfers with queue support
/// </summary>
public interface ITransferQueueService
{
    /// <summary>
    /// Observable collection of all transfer items
    /// </summary>
    ObservableCollection<TransferItem> Transfers { get; }
    
    /// <summary>
    /// Maximum number of concurrent transfers
    /// </summary>
    int MaxConcurrentTransfers { get; set; }
    
    /// <summary>
    /// Whether the queue is currently processing transfers
    /// </summary>
    bool IsProcessing { get; }
    
    /// <summary>
    /// Add a download to the queue
    /// </summary>
    TransferItem QueueDownload(string remotePath, string localPath, TransferProtocol protocol, string connectionName);
    
    /// <summary>
    /// Add an upload to the queue
    /// </summary>
    TransferItem QueueUpload(string localPath, string remotePath, TransferProtocol protocol, string connectionName);
    
    /// <summary>
    /// Start processing the queue
    /// </summary>
    void StartProcessing();
    
    /// <summary>
    /// Stop processing the queue (completes current transfers)
    /// </summary>
    void StopProcessing();
    
    /// <summary>
    /// Pause a specific transfer
    /// </summary>
    void PauseTransfer(string transferId);
    
    /// <summary>
    /// Resume a paused transfer
    /// </summary>
    void ResumeTransfer(string transferId);
    
    /// <summary>
    /// Cancel a transfer
    /// </summary>
    void CancelTransfer(string transferId);
    
    /// <summary>
    /// Remove completed/failed/cancelled transfers from the list
    /// </summary>
    void ClearCompleted();
    
    /// <summary>
    /// Event raised when a transfer completes
    /// </summary>
    event EventHandler<TransferItem>? TransferCompleted;
    
    /// <summary>
    /// Event raised when a transfer fails
    /// </summary>
    event EventHandler<TransferItem>? TransferFailed;
}

public class TransferQueueService : ITransferQueueService, IDisposable
{
    private readonly IFtpService _ftpService;
    private readonly ISftpService _sftpService;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _processingCts = new();
    private Task? _processingTask;
    private bool _isProcessing;
    private int _maxConcurrentTransfers = 2;
    
    public ObservableCollection<TransferItem> Transfers { get; } = [];
    
    public int MaxConcurrentTransfers
    {
        get => _maxConcurrentTransfers;
        set
        {
            if (value > 0)
                _maxConcurrentTransfers = value;
        }
    }
    
    public bool IsProcessing => _isProcessing;
    
    public event EventHandler<TransferItem>? TransferCompleted;
    public event EventHandler<TransferItem>? TransferFailed;
    
    public TransferQueueService(IFtpService ftpService, ISftpService sftpService)
    {
        _ftpService = ftpService;
        _sftpService = sftpService;
        _semaphore = new SemaphoreSlim(_maxConcurrentTransfers, _maxConcurrentTransfers);
    }
    
    public TransferItem QueueDownload(string remotePath, string localPath, TransferProtocol protocol, string connectionName)
    {
        var item = new TransferItem
        {
            LocalPath = localPath,
            RemotePath = remotePath,
            Direction = TransferDirection.Download,
            Protocol = protocol,
            ConnectionName = connectionName
        };
        
        Transfers.Add(item);
        return item;
    }
    
    public TransferItem QueueUpload(string localPath, string remotePath, TransferProtocol protocol, string connectionName)
    {
        var fileInfo = new FileInfo(localPath);
        
        var item = new TransferItem
        {
            LocalPath = localPath,
            RemotePath = remotePath,
            Direction = TransferDirection.Upload,
            Protocol = protocol,
            ConnectionName = connectionName,
            TotalBytes = fileInfo.Exists ? fileInfo.Length : 0
        };
        
        Transfers.Add(item);
        return item;
    }
    
    public void StartProcessing()
    {
        if (_isProcessing)
            return;
            
        _isProcessing = true;
        _processingTask = ProcessQueueAsync(_processingCts.Token);
    }
    
    public void StopProcessing()
    {
        _isProcessing = false;
    }
    
    public void PauseTransfer(string transferId)
    {
        var item = Transfers.FirstOrDefault(t => t.Id == transferId);
        if (item != null && item.Status == TransferStatus.InProgress)
        {
            if (_cancellationTokens.TryGetValue(transferId, out var cts))
            {
                cts.Cancel();
            }
            item.Status = TransferStatus.Paused;
        }
    }
    
    public void ResumeTransfer(string transferId)
    {
        var item = Transfers.FirstOrDefault(t => t.Id == transferId);
        if (item != null && item.Status == TransferStatus.Paused)
        {
            item.Status = TransferStatus.Pending;
        }
    }
    
    public void CancelTransfer(string transferId)
    {
        var item = Transfers.FirstOrDefault(t => t.Id == transferId);
        if (item != null)
        {
            if (_cancellationTokens.TryGetValue(transferId, out var cts))
            {
                cts.Cancel();
            }
            item.Status = TransferStatus.Cancelled;
        }
    }
    
    public void ClearCompleted()
    {
        var toRemove = Transfers
            .Where(t => t.Status is TransferStatus.Completed or TransferStatus.Failed or TransferStatus.Cancelled)
            .ToList();
            
        foreach (var item in toRemove)
        {
            Transfers.Remove(item);
            _cancellationTokens.TryRemove(item.Id, out _);
        }
    }
    
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (_isProcessing && !cancellationToken.IsCancellationRequested)
        {
            var pendingItem = Transfers.FirstOrDefault(t => t.Status == TransferStatus.Pending);
            
            if (pendingItem == null)
            {
                await Task.Delay(500, cancellationToken);
                continue;
            }
            
            await _semaphore.WaitAsync(cancellationToken);
            
            // Process transfer in background
            _ = ProcessTransferAsync(pendingItem, cancellationToken);
        }
    }
    
    private async Task ProcessTransferAsync(TransferItem item, CancellationToken globalCancellation)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCancellation);
        _cancellationTokens[item.Id] = cts;
        
        try
        {
            item.Status = TransferStatus.InProgress;
            item.StartTime = DateTime.Now;
            
            var progress = new Progress<FileOperationProgress>(p =>
            {
                item.TransferredBytes = p.ProcessedBytes;
                if (p.TotalBytes > 0)
                    item.TotalBytes = p.TotalBytes;
                item.ProgressPercent = item.TotalBytes > 0 
                    ? (double)item.TransferredBytes / item.TotalBytes * 100 
                    : 0;
                item.SpeedBytesPerSecond = p.TransferSpeedBytesPerSecond;
            });
            
            bool success;
            
            // Get file size for downloads
            if (item.Direction == TransferDirection.Download && item.TotalBytes == 0)
            {
                item.TotalBytes = await GetRemoteFileSize(item);
            }
            
            // Check if we need to resume
            var shouldResume = item.TransferredBytes > 0;
            
            if (item.Protocol == TransferProtocol.SFTP)
            {
                success = item.Direction == TransferDirection.Download
                    ? await _sftpService.DownloadFileAsync(item.RemotePath, item.LocalPath, progress, cts.Token)
                    : await _sftpService.UploadFileAsync(item.LocalPath, item.RemotePath, progress, cts.Token);
            }
            else // FTP or FTPS
            {
                if (shouldResume)
                {
                    success = item.Direction == TransferDirection.Download
                        ? await _ftpService.ResumeDownloadAsync(item.RemotePath, item.LocalPath, progress, cts.Token)
                        : await _ftpService.ResumeUploadAsync(item.LocalPath, item.RemotePath, progress, cts.Token);
                }
                else
                {
                    success = item.Direction == TransferDirection.Download
                        ? await _ftpService.DownloadFileAsync(item.RemotePath, item.LocalPath, progress, cts.Token)
                        : await _ftpService.UploadFileAsync(item.LocalPath, item.RemotePath, progress, cts.Token);
                }
            }
            
            if (cts.Token.IsCancellationRequested)
            {
                if (item.Status != TransferStatus.Paused)
                    item.Status = TransferStatus.Cancelled;
            }
            else if (success)
            {
                item.Status = TransferStatus.Completed;
                item.CompletedTime = DateTime.Now;
                item.ProgressPercent = 100;
                TransferCompleted?.Invoke(this, item);
            }
            else
            {
                item.Status = TransferStatus.Failed;
                item.ErrorMessage = "Transfer failed";
                TransferFailed?.Invoke(this, item);
            }
        }
        catch (OperationCanceledException)
        {
            if (item.Status != TransferStatus.Paused)
                item.Status = TransferStatus.Cancelled;
        }
        catch (Exception ex)
        {
            item.Status = TransferStatus.Failed;
            item.ErrorMessage = ex.Message;
            TransferFailed?.Invoke(this, item);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task<long> GetRemoteFileSize(TransferItem item)
    {
        try
        {
            if (item.Protocol == TransferProtocol.SFTP)
            {
                // SFTP doesn't have a direct file size API, would need to get attributes
                return 0;
            }
            else
            {
                return await _ftpService.GetFileSizeAsync(item.RemotePath);
            }
        }
        catch
        {
            return 0;
        }
    }
    
    public void Dispose()
    {
        _processingCts.Cancel();
        _processingCts.Dispose();
        
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _cancellationTokens.Clear();
        
        _semaphore.Dispose();
    }
}
