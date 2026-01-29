// BackgroundTransferService.cs - TC-style background file transfer queue implementation
// Full queue management with priority, throttling, pause/resume

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public sealed class BackgroundTransferService : IBackgroundTransferService, IDisposable
{
    private readonly ConcurrentDictionary<string, TransferOperation> _operations = new();
    private readonly ConcurrentQueue<string> _pendingQueue = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly ILongPathService _longPathService;
    private readonly IFileSystemService? _fileSystemService;
    private readonly ICloudStorageService? _cloudStorageService;
    private readonly IDirectorySyncService? _directorySyncService;
    private readonly IArchiveService? _archiveService;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly object _lock = new();
    
    private CancellationTokenSource? _queueCts;
    private Task? _queueTask;
    private long? _globalSpeedLimit;
    private int _maxConcurrentTransfers = 2;
    private volatile bool _isRunning;
    
    public bool IsRunning => _isRunning;
    
    public event EventHandler<TransferEventArgs>? OperationStarted;
    public event EventHandler<TransferEventArgs>? OperationCompleted;
    public event EventHandler<TransferEventArgs>? OperationFailed;
    public event EventHandler<TransferEventArgs>? OperationPaused;
    public event EventHandler<TransferEventArgs>? OperationCancelled;
    public event EventHandler<TransferProgressEventArgs>? ProgressChanged;
    public event EventHandler<EventArgs>? QueueEmpty;
    
    public BackgroundTransferService(ILongPathService longPathService,
        IFileSystemService? fileSystemService = null,
        ICloudStorageService? cloudStorageService = null,
        IDirectorySyncService? directorySyncService = null,
        IArchiveService? archiveService = null)
    {
        _longPathService = longPathService;
        _fileSystemService = fileSystemService;
        _cloudStorageService = cloudStorageService;
        _directorySyncService = directorySyncService;
        _archiveService = archiveService;
        _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentTransfers, _maxConcurrentTransfers);
    }
    
    public async Task<TransferOperation> QueueOperationAsync(
        TransferOperationType type,
        string sourcePath,
        string? targetPath,
        IReadOnlyList<string>? files = null,
        TransferOptions? options = null,
        TransferPriority priority = TransferPriority.Normal,
        DateTime? scheduledFor = null,
        CancellationToken cancellationToken = default)
    {
        var fileList = files?.ToList() ?? new List<string>();
        
        // Calculate total size
        long totalBytes = 0;
        int totalFiles = 0;
        
        if (fileList.Count > 0)
        {
            foreach (var file in fileList)
            {
                try
                {
                    var info = new FileInfo(_longPathService.NormalizePath(file));
                    if (info.Exists)
                    {
                        totalBytes += info.Length;
                        totalFiles++;
                    }
                }
                catch { totalFiles++; }
            }
        }
        else if (!string.IsNullOrEmpty(sourcePath))
        {
            try
            {
                var info = new FileInfo(_longPathService.NormalizePath(sourcePath));
                if (info.Exists)
                {
                    totalBytes = info.Length;
                    totalFiles = 1;
                }
            }
            catch { totalFiles = 1; }
        }
        
        var operation = new TransferOperation
        {
            Type = type,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Files = fileList,
            Status = scheduledFor.HasValue && scheduledFor.Value > DateTime.Now 
                ? BackgroundTransferStatus.Pending 
                : BackgroundTransferStatus.Queued,
            Priority = priority,
            ScheduledFor = scheduledFor,
            TotalBytes = totalBytes,
            TotalFiles = totalFiles,
            Options = options ?? new TransferOptions()
        };
        
        _operations[operation.Id] = operation;
        
        if (operation.Status == BackgroundTransferStatus.Queued)
        {
            _pendingQueue.Enqueue(operation.Id);
        }
        
        return operation;
    }
    
    public IReadOnlyList<TransferOperation> GetAllOperations()
    {
        return _operations.Values
            .OrderByDescending(o => o.Priority)
            .ThenBy(o => o.CreatedAt)
            .ToList();
    }
    
    public IReadOnlyList<TransferOperation> GetOperationsByStatus(params BackgroundTransferStatus[] statuses)
    {
        var statusSet = new HashSet<BackgroundTransferStatus>(statuses);
        return _operations.Values
            .Where(o => statusSet.Contains(o.Status))
            .OrderByDescending(o => o.Priority)
            .ThenBy(o => o.CreatedAt)
            .ToList();
    }
    
    public TransferOperation? GetOperation(string operationId)
    {
        return _operations.TryGetValue(operationId, out var op) ? op : null;
    }
    
    public async Task<bool> PauseOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        
        if (operation.Status != BackgroundTransferStatus.Running && operation.Status != BackgroundTransferStatus.Queued)
            return false;
        
        // Cancel the operation's token to pause it
        if (_cancellationTokens.TryRemove(operationId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        
        var previousStatus = operation.Status;
        _operations[operationId] = operation with { Status = BackgroundTransferStatus.Paused };
        
        OperationPaused?.Invoke(this, new TransferEventArgs(
            _operations[operationId], previousStatus));
        
        return true;
    }
    
    public async Task<bool> ResumeOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        
        if (operation.Status != BackgroundTransferStatus.Paused)
            return false;
        
        _operations[operationId] = operation with { Status = BackgroundTransferStatus.Queued };
        _pendingQueue.Enqueue(operationId);
        
        return true;
    }
    
    public async Task<bool> CancelOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        
        if (operation.Status == BackgroundTransferStatus.Completed || operation.Status == BackgroundTransferStatus.Cancelled)
            return false;
        
        // Cancel running operation
        if (_cancellationTokens.TryRemove(operationId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        
        var previousStatus = operation.Status;
        _operations[operationId] = operation with { Status = BackgroundTransferStatus.Cancelled };
        
        OperationCancelled?.Invoke(this, new TransferEventArgs(
            _operations[operationId], previousStatus));
        
        return true;
    }
    
    public async Task<bool> RetryOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        
        if (operation.Status != BackgroundTransferStatus.Failed)
            return false;
        
        _operations[operationId] = operation with 
        { 
            Status = BackgroundTransferStatus.Queued,
            RetryCount = operation.RetryCount + 1,
            ErrorMessage = null
        };
        
        _pendingQueue.Enqueue(operationId);
        return true;
    }
    
    public Task<bool> RemoveOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (_operations.TryRemove(operationId, out var operation))
        {
            // Cancel if running
            if (_cancellationTokens.TryRemove(operationId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
    
    public Task ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var completedIds = _operations
            .Where(kvp => kvp.Value.Status == BackgroundTransferStatus.Completed || 
                         kvp.Value.Status == BackgroundTransferStatus.Cancelled)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var id in completedIds)
        {
            _operations.TryRemove(id, out _);
        }
        
        return Task.CompletedTask;
    }
    
    public Task<bool> SetPriorityAsync(string operationId, TransferPriority priority, CancellationToken cancellationToken = default)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return Task.FromResult(false);
        
        _operations[operationId] = operation with { Priority = priority };
        return Task.FromResult(true);
    }
    
    public Task<bool> MoveUpAsync(string operationId, CancellationToken cancellationToken = default)
    {
        // Since we use a ConcurrentQueue, we need to rebuild it
        return ReorderQueueAsync(operationId, -1, cancellationToken);
    }
    
    public Task<bool> MoveDownAsync(string operationId, CancellationToken cancellationToken = default)
    {
        return ReorderQueueAsync(operationId, 1, cancellationToken);
    }
    
    public Task<bool> MoveToTopAsync(string operationId, CancellationToken cancellationToken = default)
    {
        return ReorderQueueAsync(operationId, int.MinValue, cancellationToken);
    }
    
    public Task<bool> MoveToBottomAsync(string operationId, CancellationToken cancellationToken = default)
    {
        return ReorderQueueAsync(operationId, int.MaxValue, cancellationToken);
    }
    
    private Task<bool> ReorderQueueAsync(string operationId, int offset, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var items = new List<string>();
            while (_pendingQueue.TryDequeue(out var id))
            {
                items.Add(id);
            }
            
            var index = items.IndexOf(operationId);
            if (index < 0) return Task.FromResult(false);
            
            items.RemoveAt(index);
            
            int newIndex;
            if (offset == int.MinValue) newIndex = 0;
            else if (offset == int.MaxValue) newIndex = items.Count;
            else newIndex = Math.Max(0, Math.Min(items.Count, index + offset));
            
            items.Insert(newIndex, operationId);
            
            foreach (var item in items)
            {
                _pendingQueue.Enqueue(item);
            }
        }
        
        return Task.FromResult(true);
    }
    
    public async Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        var runningOps = GetOperationsByStatus(BackgroundTransferStatus.Running, BackgroundTransferStatus.Queued);
        foreach (var op in runningOps)
        {
            await PauseOperationAsync(op.Id, cancellationToken);
        }
    }
    
    public async Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        var pausedOps = GetOperationsByStatus(BackgroundTransferStatus.Paused);
        foreach (var op in pausedOps)
        {
            await ResumeOperationAsync(op.Id, cancellationToken);
        }
    }
    
    public TransferQueueStatistics GetStatistics()
    {
        var ops = _operations.Values.ToList();
        var runningOps = ops.Where(o => o.Status == BackgroundTransferStatus.Running).ToList();
        
        var totalSpeed = runningOps.Sum(o => o.SpeedBytesPerSecond);
        var totalBytes = ops.Sum(o => o.TotalBytes);
        var processedBytes = ops.Sum(o => o.ProcessedBytes);
        var remainingBytes = totalBytes - processedBytes;
        
        var estimatedTime = totalSpeed > 0 
            ? TimeSpan.FromSeconds(remainingBytes / totalSpeed) 
            : (TimeSpan?)null;
        
        return new TransferQueueStatistics
        {
            TotalOperations = ops.Count,
            PendingOperations = ops.Count(o => o.Status == BackgroundTransferStatus.Pending || o.Status == BackgroundTransferStatus.Queued),
            RunningOperations = runningOps.Count,
            CompletedOperations = ops.Count(o => o.Status == BackgroundTransferStatus.Completed),
            FailedOperations = ops.Count(o => o.Status == BackgroundTransferStatus.Failed),
            TotalBytes = totalBytes,
            ProcessedBytes = processedBytes,
            AverageSpeedBytesPerSecond = totalSpeed,
            EstimatedTotalTimeRemaining = estimatedTime,
            ConcurrentTransfers = runningOps.Count
        };
    }
    
    public void SetGlobalSpeedLimit(long? bytesPerSecond) => _globalSpeedLimit = bytesPerSecond;
    public long? GetGlobalSpeedLimit() => _globalSpeedLimit;
    public void SetMaxConcurrentTransfers(int count) => _maxConcurrentTransfers = Math.Max(1, count);
    public int GetMaxConcurrentTransfers() => _maxConcurrentTransfers;
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;
        
        _queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;
        _queueTask = ProcessQueueAsync(_queueCts.Token);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        
        if (_queueCts != null)
        {
            await _queueCts.CancelAsync();
            _queueCts.Dispose();
            _queueCts = null;
        }
        
        if (_queueTask != null)
        {
            try { await _queueTask; } catch (OperationCanceledException) { }
            _queueTask = null;
        }
        
        await PauseAllAsync(cancellationToken);
    }
    
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check scheduled operations
                CheckScheduledOperations();
                
                // Try to get next operation
                if (_pendingQueue.TryDequeue(out var operationId))
                {
                    if (_operations.TryGetValue(operationId, out var operation) &&
                        operation.Status == BackgroundTransferStatus.Queued)
                    {
                        // Wait for available slot
                        await _concurrencyLimiter.WaitAsync(cancellationToken);
                        
                        // Start operation in background
                        _ = ExecuteOperationAsync(operationId, cancellationToken);
                    }
                }
                else
                {
                    // No pending operations, check if queue is empty
                    var stats = GetStatistics();
                    if (stats.PendingOperations == 0 && stats.RunningOperations == 0)
                    {
                        QueueEmpty?.Invoke(this, EventArgs.Empty);
                    }
                    
                    // Wait a bit before checking again
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Log error and continue
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
    
    private void CheckScheduledOperations()
    {
        var now = DateTime.Now;
        var scheduledOps = _operations.Values
            .Where(o => o.Status == BackgroundTransferStatus.Pending && 
                       o.ScheduledFor.HasValue && 
                       o.ScheduledFor.Value <= now)
            .ToList();
        
        foreach (var op in scheduledOps)
        {
            _operations[op.Id] = op with { Status = BackgroundTransferStatus.Queued };
            _pendingQueue.Enqueue(op.Id);
        }
    }
    
    private async Task ExecuteOperationAsync(string operationId, CancellationToken queueCancellation)
    {
        try
        {
            if (!_operations.TryGetValue(operationId, out var operation))
                return;
            
            // Create operation-specific cancellation token
            var opCts = new CancellationTokenSource();
            _cancellationTokens[operationId] = opCts;
            
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                queueCancellation, opCts.Token);
            var cancellationToken = linkedCts.Token;
            
            // Update status
            var previousStatus = operation.Status;
            operation = operation with 
            { 
                Status = BackgroundTransferStatus.Running,
                StartedAt = DateTime.Now
            };
            _operations[operationId] = operation;
            
            OperationStarted?.Invoke(this, new TransferEventArgs(operation, previousStatus));
            
            var stopwatch = Stopwatch.StartNew();
            long processedBytes = operation.ProcessedBytes;
            int processedFiles = operation.ProcessedFiles;
            
            try
            {
                // Execute the actual transfer
                switch (operation.Type)
                {
                    case TransferOperationType.Copy:
                        await ExecuteCopyAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Move:
                        await ExecuteMoveAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Delete:
                        await ExecuteDeleteAsync(operation, (file) =>
                        {
                            processedFiles++;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Upload:
                        await ExecuteUploadAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Download:
                        await ExecuteDownloadAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Sync:
                        await ExecuteSyncAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Archive:
                        await ExecuteArchiveAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    case TransferOperationType.Extract:
                        await ExecuteExtractAsync(operation, (bytes, file) =>
                        {
                            processedBytes += bytes;
                            UpdateProgress(operationId, processedBytes, processedFiles, file, stopwatch.Elapsed);
                        }, cancellationToken);
                        break;
                    
                    default:
                        throw new NotSupportedException($"Operation type {operation.Type} not yet implemented");
                }
                
                // Mark as completed
                operation = _operations[operationId];
                _operations[operationId] = operation with 
                { 
                    Status = BackgroundTransferStatus.Completed,
                    CompletedAt = DateTime.Now,
                    ProcessedBytes = operation.TotalBytes,
                    ProcessedFiles = operation.TotalFiles
                };
                
                OperationCompleted?.Invoke(this, new TransferEventArgs(
                    _operations[operationId], BackgroundTransferStatus.Running));
            }
            catch (OperationCanceledException)
            {
                // Already handled by pause/cancel
            }
            catch (Exception ex)
            {
                operation = _operations[operationId];
                
                // Check if we should retry
                if (operation.RetryCount < operation.Options.MaxRetries)
                {
                    _operations[operationId] = operation with 
                    { 
                        Status = BackgroundTransferStatus.Queued,
                        RetryCount = operation.RetryCount + 1,
                        ErrorMessage = ex.Message
                    };
                    _pendingQueue.Enqueue(operationId);
                    await Task.Delay(operation.Options.RetryDelay, CancellationToken.None);
                }
                else
                {
                    _operations[operationId] = operation with 
                    { 
                        Status = BackgroundTransferStatus.Failed,
                        ErrorMessage = ex.Message
                    };
                    
                    OperationFailed?.Invoke(this, new TransferEventArgs(
                        _operations[operationId], BackgroundTransferStatus.Running));
                }
            }
            finally
            {
                _cancellationTokens.TryRemove(operationId, out _);
                opCts.Dispose();
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
    
    private void UpdateProgress(string operationId, long processedBytes, int processedFiles, 
        string? currentFile, TimeSpan elapsed)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return;
        
        var speed = elapsed.TotalSeconds > 0 ? processedBytes / elapsed.TotalSeconds : 0;
        var remainingBytes = operation.TotalBytes - processedBytes;
        var estimatedRemaining = speed > 0 
            ? TimeSpan.FromSeconds(remainingBytes / speed) 
            : (TimeSpan?)null;
        
        operation = operation with
        {
            ProcessedBytes = processedBytes,
            ProcessedFiles = processedFiles,
            CurrentFile = currentFile,
            SpeedBytesPerSecond = speed,
            EstimatedTimeRemaining = estimatedRemaining
        };
        
        _operations[operationId] = operation;
        
        ProgressChanged?.Invoke(this, new TransferProgressEventArgs(
            operation, processedBytes, currentFile));
    }
    
    private async Task ExecuteCopyAsync(TransferOperation operation, 
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        var files = operation.Files.Count > 0 
            ? operation.Files 
            : new[] { operation.SourcePath };
        
        var buffer = new byte[81920];
        
        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var relativePath = Path.GetFileName(sourceFile);
            var targetFile = Path.Combine(operation.TargetPath!, relativePath);
            
            var longSource = _longPathService.NormalizePath(sourceFile);
            var longTarget = _longPathService.NormalizePath(targetFile);
            
            Directory.CreateDirectory(Path.GetDirectoryName(longTarget)!);
            
            await using var sourceStream = new FileStream(longSource, FileMode.Open, FileAccess.Read);
            await using var targetStream = new FileStream(longTarget, FileMode.Create, FileAccess.Write);
            
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                // Apply speed limiting
                if (_globalSpeedLimit.HasValue || operation.Options.SpeedLimit.HasValue)
                {
                    var limit = Math.Min(
                        _globalSpeedLimit ?? long.MaxValue,
                        operation.Options.SpeedLimit ?? long.MaxValue);
                    
                    var delay = (int)(bytesRead * 1000.0 / limit);
                    if (delay > 0)
                        await Task.Delay(delay, cancellationToken);
                }
                
                await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                progressCallback(bytesRead, sourceFile);
            }
            
            // Preserve attributes
            if (operation.Options.PreserveAttributes || operation.Options.PreserveTimestamps)
            {
                var sourceInfo = new FileInfo(longSource);
                var targetInfo = new FileInfo(longTarget);
                
                if (operation.Options.PreserveTimestamps)
                {
                    targetInfo.CreationTime = sourceInfo.CreationTime;
                    targetInfo.LastWriteTime = sourceInfo.LastWriteTime;
                    targetInfo.LastAccessTime = sourceInfo.LastAccessTime;
                }
                
                if (operation.Options.PreserveAttributes)
                {
                    targetInfo.Attributes = sourceInfo.Attributes;
                }
            }
        }
    }
    
    private async Task ExecuteMoveAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        // Copy first
        await ExecuteCopyAsync(operation, progressCallback, cancellationToken);
        
        // Then delete source files
        var files = operation.Files.Count > 0 
            ? operation.Files 
            : new[] { operation.SourcePath };
        
        foreach (var file in files)
        {
            File.Delete(_longPathService.NormalizePath(file));
        }
    }
    
    private async Task ExecuteDeleteAsync(TransferOperation operation,
        Action<string> progressCallback, CancellationToken cancellationToken)
    {
        var files = operation.Files.Count > 0 
            ? operation.Files 
            : new[] { operation.SourcePath };

        if (_fileSystemService != null)
        {
            var progress = new Progress<FileOperationProgress>(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.CurrentItem))
                    progressCallback(p.CurrentItem);
            });

            await _fileSystemService.DeleteAsync(
                files,
                permanent: !operation.Options.UseRecycleBin,
                progress,
                cancellationToken);
            return;
        }
        
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var longPath = _longPathService.NormalizePath(file);

            if (Directory.Exists(longPath))
                Directory.Delete(longPath, true);
            else
                File.Delete(longPath);
            
            progressCallback(file);
        }
    }

    private async Task ExecuteUploadAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        if (_cloudStorageService == null)
            throw new InvalidOperationException("Cloud storage service is not available");
        
        if (!TryParseCloudLocation(operation.TargetPath, out var accountId, out var cloudTarget))
            throw new InvalidOperationException("Upload requires target path in format cloud://{accountId}/{path}");
        
        var localFiles = ResolveLocalFiles(operation);
        if (localFiles.Count == 0)
            throw new InvalidOperationException("Upload requires source file(s)");
        var isDirectoryUpload = Directory.Exists(operation.SourcePath) || localFiles.Count > 1;
        var targetIsFolder = cloudTarget.EndsWith("/", StringComparison.Ordinal);
        
        foreach (var localPath in localFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var relative = Directory.Exists(operation.SourcePath)
                ? Path.GetRelativePath(operation.SourcePath, localPath)
                : Path.GetFileName(localPath);
            var cloudPath = targetIsFolder || isDirectoryUpload
                ? CombineCloudPath(cloudTarget, relative)
                : cloudTarget;
            
            if (operation.Options.SkipExisting || !operation.Options.OverwriteExisting)
            {
                var existing = await _cloudStorageService.GetItemAsync(accountId, cloudPath, cancellationToken);
                if (existing != null)
                {
                    if (operation.Options.SkipExisting)
                        continue;
                    if (!operation.Options.OverwriteExisting)
                        throw new IOException($"Cloud target already exists: {cloudPath}");
                }
            }
            
            long lastTransferred = 0;
            var progress = new Progress<CloudTransferProgress>(p =>
            {
                var delta = p.BytesTransferred - lastTransferred;
                if (delta > 0)
                {
                    progressCallback(delta, localPath);
                    lastTransferred = p.BytesTransferred;
                }
            });
            
            await _cloudStorageService.UploadFromFileAsync(accountId, cloudPath, localPath, progress, cancellationToken);
        }
    }
    
    private async Task ExecuteDownloadAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        if (_cloudStorageService == null)
            throw new InvalidOperationException("Cloud storage service is not available");
        
        if (!TryParseCloudLocation(operation.SourcePath, out var accountId, out var cloudSource))
            throw new InvalidOperationException("Download requires source path in format cloud://{accountId}/{path}");
        
        var targetRoot = operation.TargetPath ?? Environment.CurrentDirectory;
        var cloudFiles = ResolveCloudFiles(operation, cloudSource)
            .Select(path => NormalizeCloudPath(path, accountId))
            .ToList();
        
        if (cloudFiles.Count == 0)
        {
            var item = await _cloudStorageService.GetItemAsync(accountId, cloudSource, cancellationToken);
            if (item != null && item.IsFolder)
            {
                await DownloadCloudDirectoryAsync(accountId, cloudSource, targetRoot, operation, progressCallback, cancellationToken);
                return;
            }
            
            await DownloadCloudFileAsync(accountId, cloudSource, targetRoot, operation, progressCallback, cancellationToken);
            return;
        }
        
        foreach (var cloudPath in cloudFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadCloudFileAsync(accountId, cloudPath, targetRoot, operation, progressCallback, cancellationToken);
        }
    }
    
    private async Task ExecuteSyncAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        var sourceIsCloud = TryParseCloudLocation(operation.SourcePath, out var sourceAccount, out var sourceCloud);
        var targetIsCloud = TryParseCloudLocation(operation.TargetPath, out var targetAccount, out var targetCloud);
        
        if (sourceIsCloud && targetIsCloud)
            throw new NotSupportedException("Cloud-to-cloud sync is not supported");
        
        if (sourceIsCloud || targetIsCloud)
        {
            if (_cloudStorageService == null)
                throw new InvalidOperationException("Cloud storage service is not available");
            
            var accountId = sourceIsCloud ? sourceAccount : targetAccount;
            var cloudPath = sourceIsCloud ? sourceCloud : targetCloud;
            var localPath = sourceIsCloud ? operation.TargetPath : operation.SourcePath;
            
            if (string.IsNullOrWhiteSpace(localPath))
                throw new InvalidOperationException("Sync requires a local path");
            
            var direction = ResolveCloudSyncDirection(operation.Options, sourceIsCloud);
            long lastTransferred = 0;
            var progress = new Progress<CloudTransferProgress>(p =>
            {
                var delta = p.BytesTransferred - lastTransferred;
                if (delta > 0)
                {
                    progressCallback(delta, p.FileName);
                    lastTransferred = p.BytesTransferred;
                }
            });
            
            await _cloudStorageService.SynchronizeAsync(accountId, localPath!, cloudPath, direction, progress, cancellationToken);
            return;
        }
        
        if (_directorySyncService == null)
            throw new InvalidOperationException("Directory sync service is not available");
        
        if (string.IsNullOrWhiteSpace(operation.TargetPath))
            throw new InvalidOperationException("Sync requires source and target paths");
        
        var options = new SyncOptions();
        long lastBytes = 0;
        var progressInfo = new Progress<SyncProgressInfo>(p =>
        {
            var delta = p.ProcessedBytes - lastBytes;
            if (delta > 0)
            {
                progressCallback(delta, p.CurrentFile);
                lastBytes = p.ProcessedBytes;
            }
        });
        
        var entries = await _directorySyncService.CompareDirectoriesAsync(
            operation.SourcePath,
            operation.TargetPath,
            options,
            progressInfo,
            cancellationToken);
        
        entries = _directorySyncService.AutoAssignActions(entries, options);
        await _directorySyncService.SynchronizeAsync(entries, options, progressInfo, cancellationToken);
    }
    
    private async Task ExecuteArchiveAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        if (_archiveService == null)
            throw new InvalidOperationException("Archive service is not available");
        
        var destination = operation.TargetPath;
        if (string.IsNullOrWhiteSpace(destination))
            throw new InvalidOperationException("Archive operation requires a target archive path");
        
        var sources = (operation.Files.Count > 0
                ? operation.Files
                : new[] { operation.SourcePath })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        
        if (sources.Count == 0)
            throw new InvalidOperationException("Archive operation requires source files");
        
        long lastBytes = 0;
        var progress = new Progress<ArchiveProgress>(p =>
        {
            var delta = p.BytesProcessed - lastBytes;
            if (delta > 0)
            {
                progressCallback(delta, p.CurrentEntry);
                lastBytes = p.BytesProcessed;
            }
        });
        
        var type = InferArchiveType(destination);
        await _archiveService.CreateArchiveAsync(destination, sources, type, CompressionLevel.Normal, progress, cancellationToken);
    }
    
    private async Task ExecuteExtractAsync(TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        if (_archiveService == null)
            throw new InvalidOperationException("Archive service is not available");
        
        if (string.IsNullOrWhiteSpace(operation.SourcePath))
            throw new InvalidOperationException("Extract operation requires a source archive path");
        
        var destination = operation.TargetPath ?? Path.GetDirectoryName(operation.SourcePath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(destination);
        
        long lastBytes = 0;
        var progress = new Progress<ArchiveProgress>(p =>
        {
            var delta = p.BytesProcessed - lastBytes;
            if (delta > 0)
            {
                progressCallback(delta, p.CurrentEntry);
                lastBytes = p.BytesProcessed;
            }
        });
        
        await _archiveService.ExtractAllAsync(operation.SourcePath, destination, progress, cancellationToken);
    }

    private static IReadOnlyList<string> ResolveLocalFiles(TransferOperation operation)
    {
        if (operation.Files.Count > 0)
            return operation.Files.ToList();
        
        if (!string.IsNullOrWhiteSpace(operation.SourcePath))
        {
            if (Directory.Exists(operation.SourcePath))
            {
                return Directory.EnumerateFiles(operation.SourcePath, "*", SearchOption.AllDirectories).ToList();
            }
            
            return new[] { operation.SourcePath };
        }
        
        return Array.Empty<string>();
    }
    
    private static IReadOnlyList<string> ResolveCloudFiles(TransferOperation operation, string cloudSource)
    {
        if (operation.Files.Count > 0)
            return operation.Files.ToList();
        
        if (!string.IsNullOrWhiteSpace(operation.SourcePath))
            return Array.Empty<string>();
        
        return new[] { cloudSource };
    }
    
    private async Task DownloadCloudDirectoryAsync(string accountId, string cloudPath, string localRoot,
        TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(localRoot);
        
        var items = await _cloudStorageService!.ListAsync(accountId, cloudPath, cancellationToken);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var relative = GetRelativeCloudPath(cloudPath, item.Path);
            var localPath = Path.Combine(localRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            
            if (item.IsFolder)
            {
                await DownloadCloudDirectoryAsync(accountId, item.Path, localPath, operation, progressCallback, cancellationToken);
            }
            else
            {
                await DownloadCloudFileAsync(accountId, item.Path, localPath, operation, progressCallback, cancellationToken);
            }
        }
    }
    
    private async Task DownloadCloudFileAsync(string accountId, string cloudPath, string localTarget,
        TransferOperation operation,
        Action<long, string> progressCallback, CancellationToken cancellationToken)
    {
        var finalPath = ResolveLocalTargetPath(localTarget, cloudPath);
        
        if (File.Exists(finalPath))
        {
            if (operation.Options.SkipExisting)
                return;
            if (!operation.Options.OverwriteExisting)
                throw new IOException($"Target already exists: {finalPath}");
        }
        
        var targetDir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        
        long lastTransferred = 0;
        var progress = new Progress<CloudTransferProgress>(p =>
        {
            var delta = p.BytesTransferred - lastTransferred;
            if (delta > 0)
            {
                progressCallback(delta, finalPath);
                lastTransferred = p.BytesTransferred;
            }
        });
        
        await _cloudStorageService!.DownloadToFileAsync(accountId, cloudPath, finalPath, progress, cancellationToken);
    }
    
    private static bool TryParseCloudLocation(string? value, out string accountId, out string cloudPath)
    {
        accountId = string.Empty;
        cloudPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        
        var trimmed = value.Trim();
        if (trimmed.StartsWith("cloud://", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed.Substring("cloud://".Length);
            var slashIndex = remainder.IndexOf('/');
            if (slashIndex < 0)
            {
                accountId = remainder;
                cloudPath = string.Empty;
                return !string.IsNullOrWhiteSpace(accountId);
            }
            
            accountId = remainder[..slashIndex];
            cloudPath = remainder[(slashIndex + 1)..];
            cloudPath = cloudPath.TrimStart('/');
            return !string.IsNullOrWhiteSpace(accountId);
        }
        
        if (trimmed.StartsWith("cloud:", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = trimmed.Substring("cloud:".Length);
            var slashIndex = remainder.IndexOf('/');
            if (slashIndex < 0)
            {
                accountId = remainder;
                cloudPath = string.Empty;
                return !string.IsNullOrWhiteSpace(accountId);
            }
            
            accountId = remainder[..slashIndex];
            cloudPath = remainder[(slashIndex + 1)..];
            cloudPath = cloudPath.TrimStart('/');
            return !string.IsNullOrWhiteSpace(accountId);
        }
        
        return false;
    }

    private static string NormalizeCloudPath(string value, string expectedAccountId)
    {
        if (TryParseCloudLocation(value, out var accountId, out var cloudPath))
        {
            if (!string.Equals(accountId, expectedAccountId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cloud path account mismatch: {accountId}");
            return cloudPath;
        }
        
        return value;
    }
    
    private static string CombineCloudPath(string basePath, string relativePath)
    {
        var sanitizedBase = basePath.Replace('\\', '/').Trim('/');
        var sanitizedRelative = relativePath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrEmpty(sanitizedBase))
            return sanitizedRelative;
        if (string.IsNullOrEmpty(sanitizedRelative))
            return sanitizedBase;
        return $"{sanitizedBase}/{sanitizedRelative}";
    }
    
    private static string GetRelativeCloudPath(string rootPath, string itemPath)
    {
        var normalizedRoot = rootPath.Replace('\\', '/').Trim('/');
        var normalizedItem = itemPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalizedRoot))
            return normalizedItem;
        if (normalizedItem.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            return normalizedItem[(normalizedRoot.Length + 1)..];
        return normalizedItem;
    }
    
    private static string ResolveLocalTargetPath(string targetPath, string cloudPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return Path.GetFileName(cloudPath);
        
        var path = targetPath;
        if (Directory.Exists(path) || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(path, Path.GetFileName(cloudPath));
        }
        
        return path;
    }
    
    private static SyncDirection ResolveCloudSyncDirection(TransferOptions options, bool sourceIsCloud)
    {
        if (!string.IsNullOrWhiteSpace(options.ConflictResolution))
        {
            return options.ConflictResolution.Trim().ToLowerInvariant() switch
            {
                "upload" => SyncDirection.Upload,
                "download" => SyncDirection.Download,
                "bidirectional" or "both" => SyncDirection.Bidirectional,
                _ => sourceIsCloud ? SyncDirection.Download : SyncDirection.Upload
            };
        }
        
        return sourceIsCloud ? SyncDirection.Download : SyncDirection.Upload;
    }
    
    private static ArchiveType InferArchiveType(string archivePath)
    {
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        return extension switch
        {
            ".7z" => ArchiveType.SevenZip,
            ".tar" => ArchiveType.Tar,
            ".gz" or ".gzip" => ArchiveType.GZip,
            ".bz2" => ArchiveType.BZip2,
            ".rar" => ArchiveType.Rar,
            _ => ArchiveType.Zip
        };
    }
    
    public void Dispose()
    {
        _queueCts?.Cancel();
        _queueCts?.Dispose();
        _concurrencyLimiter.Dispose();
        
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
