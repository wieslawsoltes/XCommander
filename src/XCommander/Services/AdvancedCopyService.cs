using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace XCommander.Services;

/// <summary>
/// Implementation of advanced copy/move service with verification and queue
/// </summary>
public class AdvancedCopyService : IAdvancedCopyService
{
    private readonly ConcurrentQueue<CopyTransferOperation> _queue = new();
    private readonly List<CopyTransferOperation> _history = new();
    private readonly string _historyPath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private CopyTransferOperation? _currentOperation;
    private CancellationTokenSource? _cancellationSource;
    private bool _isPaused;
    private bool _isRunning;
    
    private long _currentSpeed;
    private readonly object _speedLock = new();
    private DateTime _lastSpeedUpdate = DateTime.Now;
    private long _lastBytesTransferred;
    
    private const int BufferSize = 81920; // 80KB buffer
    
    public event EventHandler<CopyTransferProgressEventArgs>? Progress;
    public event EventHandler<CopyTransferConflictEventArgs>? ConflictOccurred;
    public event EventHandler<CopyTransferOperation>? OperationCompleted;
    
    public AdvancedCopyService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander");
        Directory.CreateDirectory(appData);
        
        _historyPath = Path.Combine(appData, "transfer_history.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        LoadHistory();
    }
    
    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                var history = JsonSerializer.Deserialize<List<CopyTransferOperation>>(json, _jsonOptions);
                if (history != null)
                {
                    _history.AddRange(history.Take(100));
                }
            }
        }
        catch { }
    }
    
    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history.Take(100).ToList(), _jsonOptions);
            File.WriteAllText(_historyPath, json);
        }
        catch { }
    }
    
    #region Queue Management
    
    public IReadOnlyList<CopyTransferOperation> GetQueue()
    {
        return _queue.ToList();
    }
    
    public CopyTransferOperation? GetCurrentOperation()
    {
        return _currentOperation;
    }
    
    public Task<CopyTransferOperation> QueueOperationAsync(CopyTransferOperation operation)
    {
        _queue.Enqueue(operation);
        return Task.FromResult(operation);
    }
    
    public Task RemoveFromQueueAsync(string operationId)
    {
        var operations = _queue.ToList();
        _queue.Clear();
        foreach (var op in operations.Where(o => o.Id != operationId))
        {
            _queue.Enqueue(op);
        }
        return Task.CompletedTask;
    }
    
    public Task ClearCompletedAsync()
    {
        var operations = _queue.ToList();
        _queue.Clear();
        foreach (var op in operations.Where(o => o.Status != CopyTransferStatus.Completed))
        {
            _queue.Enqueue(op);
        }
        return Task.CompletedTask;
    }
    
    public Task SetPriorityAsync(string operationId, QueuePriority priority)
    {
        var operations = _queue.ToList();
        var operation = operations.FirstOrDefault(o => o.Id == operationId);
        if (operation != null)
        {
            operation.Priority = priority;
            
            // Reorder by priority
            _queue.Clear();
            foreach (var op in operations.OrderByDescending(o => o.Priority))
            {
                _queue.Enqueue(op);
            }
        }
        return Task.CompletedTask;
    }
    
    public Task MoveUpInQueueAsync(string operationId)
    {
        var operations = _queue.ToList();
        var index = operations.FindIndex(o => o.Id == operationId);
        if (index > 0)
        {
            (operations[index], operations[index - 1]) = (operations[index - 1], operations[index]);
            _queue.Clear();
            foreach (var op in operations)
            {
                _queue.Enqueue(op);
            }
        }
        return Task.CompletedTask;
    }
    
    public Task MoveDownInQueueAsync(string operationId)
    {
        var operations = _queue.ToList();
        var index = operations.FindIndex(o => o.Id == operationId);
        if (index >= 0 && index < operations.Count - 1)
        {
            (operations[index], operations[index + 1]) = (operations[index + 1], operations[index]);
            _queue.Clear();
            foreach (var op in operations)
            {
                _queue.Enqueue(op);
            }
        }
        return Task.CompletedTask;
    }
    
    #endregion
    
    #region Operation Control
    
    public async Task StartQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            while (_queue.TryDequeue(out var operation) && !_cancellationSource.Token.IsCancellationRequested)
            {
                _currentOperation = operation;
                await ExecuteOperationAsync(operation, _cancellationSource.Token);
                
                _history.Insert(0, operation);
                while (_history.Count > 100) _history.RemoveAt(_history.Count - 1);
                SaveHistory();
                
                OperationCompleted?.Invoke(this, operation);
                _currentOperation = null;
            }
        }
        finally
        {
            _isRunning = false;
            _cancellationSource = null;
        }
    }
    
    public Task PauseAsync()
    {
        _isPaused = true;
        if (_currentOperation != null)
        {
            _currentOperation.Status = CopyTransferStatus.Paused;
        }
        return Task.CompletedTask;
    }
    
    public Task ResumeAsync()
    {
        _isPaused = false;
        if (_currentOperation != null)
        {
            _currentOperation.Status = CopyTransferStatus.InProgress;
        }
        return Task.CompletedTask;
    }
    
    public Task CancelCurrentAsync()
    {
        _cancellationSource?.Cancel();
        if (_currentOperation != null)
        {
            _currentOperation.Status = CopyTransferStatus.Cancelled;
        }
        return Task.CompletedTask;
    }
    
    public Task CancelAllAsync()
    {
        _cancellationSource?.Cancel();
        
        while (_queue.TryDequeue(out var op))
        {
            op.Status = CopyTransferStatus.Cancelled;
        }
        
        if (_currentOperation != null)
        {
            _currentOperation.Status = CopyTransferStatus.Cancelled;
        }
        
        return Task.CompletedTask;
    }
    
    #endregion
    
    #region Direct Operations
    
    public async Task<CopyTransferOperation> CopyAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.None,
        ConflictResolution conflictHandling = ConflictResolution.Ask,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = CreateOperation(sourcePaths, destinationPath, CopyTransferMode.Copy, verification, conflictHandling);
        await ExecuteOperationAsync(operation, cancellationToken, progress);
        return operation;
    }
    
    public async Task<CopyTransferOperation> MoveAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.None,
        ConflictResolution conflictHandling = ConflictResolution.Ask,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = CreateOperation(sourcePaths, destinationPath, CopyTransferMode.Move, verification, conflictHandling);
        await ExecuteOperationAsync(operation, cancellationToken, progress);
        return operation;
    }
    
    public async Task<CopyTransferOperation> CopyWithVerificationAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        VerificationMethod verification = VerificationMethod.MD5,
        IProgress<CopyTransferProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await CopyAsync(sourcePaths, destinationPath, verification, ConflictResolution.Ask, progress, cancellationToken);
    }
    
    private CopyTransferOperation CreateOperation(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        CopyTransferMode mode,
        VerificationMethod verification,
        ConflictResolution conflictHandling)
    {
        var items = new List<CopyTransferItem>();
        
        foreach (var source in sourcePaths)
        {
            if (File.Exists(source))
            {
                var info = new FileInfo(source);
                var destPath = Path.Combine(destinationPath, info.Name);
                
                items.Add(new CopyTransferItem
                {
                    SourcePath = source,
                    DestinationPath = destPath,
                    Mode = mode,
                    Size = info.Length
                });
            }
            else if (Directory.Exists(source))
            {
                var dirInfo = new DirectoryInfo(source);
                var destDir = Path.Combine(destinationPath, dirInfo.Name);
                
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(source, file.FullName);
                    var destPath = Path.Combine(destDir, relativePath);
                    
                    items.Add(new CopyTransferItem
                    {
                        SourcePath = file.FullName,
                        DestinationPath = destPath,
                        Mode = mode,
                        Size = file.Length
                    });
                }
            }
        }
        
        return new CopyTransferOperation
        {
            Mode = mode,
            Items = items,
            Verification = verification,
            ConflictHandling = conflictHandling
        };
    }
    
    #endregion
    
    #region Execution
    
    private async Task ExecuteOperationAsync(
        CopyTransferOperation operation,
        CancellationToken cancellationToken,
        IProgress<CopyTransferProgressEventArgs>? progress = null)
    {
        operation.Status = CopyTransferStatus.InProgress;
        operation.StartTime = DateTime.Now;
        
        _lastBytesTransferred = 0;
        _lastSpeedUpdate = DateTime.Now;
        
        ConflictResolution currentConflictResolution = operation.ConflictHandling;
        
        try
        {
            foreach (var item in operation.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                while (_isPaused)
                {
                    await Task.Delay(100, cancellationToken);
                }
                
                try
                {
                    // Check for conflict
                    if (File.Exists(item.DestinationPath))
                    {
                        var resolution = currentConflictResolution;
                        
                        if (resolution == ConflictResolution.Ask)
                        {
                            var args = new CopyTransferConflictEventArgs
                            {
                                Item = item,
                                SourceFile = new FileInfo(item.SourcePath),
                                DestinationFile = new FileInfo(item.DestinationPath)
                            };
                            
                            ConflictOccurred?.Invoke(this, args);
                            resolution = args.Resolution;
                            
                            if (args.ApplyToAll)
                            {
                                currentConflictResolution = resolution;
                            }
                        }
                        
                        switch (resolution)
                        {
                            case ConflictResolution.Skip:
                                item.Status = CopyTransferStatus.Completed;
                                continue;
                            
                            case ConflictResolution.OverwriteIfNewer:
                                var sourceTime = File.GetLastWriteTime(item.SourcePath);
                                var destTime = File.GetLastWriteTime(item.DestinationPath);
                                if (sourceTime <= destTime)
                                {
                                    item.Status = CopyTransferStatus.Completed;
                                    continue;
                                }
                                break;
                            
                            case ConflictResolution.OverwriteIfSizeDiffers:
                                var sourceSize = new FileInfo(item.SourcePath).Length;
                                var destSize = new FileInfo(item.DestinationPath).Length;
                                if (sourceSize == destSize)
                                {
                                    item.Status = CopyTransferStatus.Completed;
                                    continue;
                                }
                                break;
                            
                            case ConflictResolution.Rename:
                            case ConflictResolution.RenameWithNumber:
                                var newPath = GetUniqueDestinationPath(item.DestinationPath);
                                // Update destination path directly
                                item.DestinationPath = newPath;
                                break;
                        }
                    }
                    
                    await TransferFileAsync(item, operation, cancellationToken, progress);
                    
                    // Verify if requested
                    if (operation.Verification != VerificationMethod.None)
                    {
                        item.Status = CopyTransferStatus.Verifying;
                        var verified = await VerifyTransferAsync(item, operation.Verification, cancellationToken);
                        item.VerificationPassed = verified;
                        
                        if (!verified)
                        {
                            item.Status = CopyTransferStatus.Failed;
                            item.ErrorMessage = "Verification failed - file hash mismatch";
                            continue;
                        }
                    }
                    
                    // Delete source after move (or verified copy with delete flag)
                    if (item.Mode == CopyTransferMode.Move || operation.DeleteSourceAfterVerification)
                    {
                        File.Delete(item.SourcePath);
                    }
                    
                    item.Status = CopyTransferStatus.Completed;
                    item.EndTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    item.Status = CopyTransferStatus.Failed;
                    item.ErrorMessage = ex.Message;
                }
                
                ReportProgress(operation, item, progress);
            }
            
            operation.Status = operation.Items.All(i => i.Status == CopyTransferStatus.Completed)
                ? CopyTransferStatus.Completed
                : operation.Items.All(i => i.Status == CopyTransferStatus.Failed)
                    ? CopyTransferStatus.Failed
                    : CopyTransferStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            operation.Status = CopyTransferStatus.Cancelled;
            foreach (var item in operation.Items.Where(i => i.Status == CopyTransferStatus.InProgress || i.Status == CopyTransferStatus.Pending))
            {
                item.Status = CopyTransferStatus.Cancelled;
            }
        }
        finally
        {
            operation.EndTime = DateTime.Now;
        }
    }
    
    private async Task TransferFileAsync(
        CopyTransferItem item,
        CopyTransferOperation operation,
        CancellationToken cancellationToken,
        IProgress<CopyTransferProgressEventArgs>? progress)
    {
        item.Status = CopyTransferStatus.InProgress;
        item.StartTime = DateTime.Now;
        
        // Ensure destination directory exists
        var destDir = Path.GetDirectoryName(item.DestinationPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        // Calculate source hash if verification is enabled
        if (operation.Verification != VerificationMethod.None)
        {
            item.SourceHash = await CalculateHashAsync(item.SourcePath, operation.Verification, cancellationToken);
        }
        
        var buffer = new byte[BufferSize];
        var speedDelay = GetSpeedDelay(operation.SpeedMode, operation.SpeedLimitBytesPerSecond);
        
        await using var source = File.OpenRead(item.SourcePath);
        await using var dest = File.Create(item.DestinationPath);
        
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            item.BytesTransferred += bytesRead;
            
            UpdateSpeed(item.BytesTransferred);
            ReportProgress(operation, item, progress);
            
            if (speedDelay > 0)
            {
                await Task.Delay(speedDelay, cancellationToken);
            }
            
            while (_isPaused)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
        
        // Preserve timestamps
        if (operation.PreserveTimestamps)
        {
            var sourceInfo = new FileInfo(item.SourcePath);
            File.SetCreationTime(item.DestinationPath, sourceInfo.CreationTime);
            File.SetLastWriteTime(item.DestinationPath, sourceInfo.LastWriteTime);
            File.SetLastAccessTime(item.DestinationPath, sourceInfo.LastAccessTime);
        }
        
        // Preserve attributes
        if (operation.PreserveAttributes)
        {
            var sourceAttrs = File.GetAttributes(item.SourcePath);
            File.SetAttributes(item.DestinationPath, sourceAttrs);
        }
    }
    
    private static int GetSpeedDelay(TransferSpeedMode mode, long? limitBytesPerSecond)
    {
        return mode switch
        {
            TransferSpeedMode.Slow => 10,
            TransferSpeedMode.VerySlow => 50,
            TransferSpeedMode.Throttled when limitBytesPerSecond.HasValue => 
                (int)(BufferSize * 1000.0 / limitBytesPerSecond.Value),
            _ => 0
        };
    }
    
    private static string GetUniqueDestinationPath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        
        var counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
            counter++;
        } while (File.Exists(newPath));
        
        return newPath;
    }
    
    private void UpdateSpeed(long currentBytes)
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
        
        if (elapsed >= 1)
        {
            lock (_speedLock)
            {
                _currentSpeed = (long)((currentBytes - _lastBytesTransferred) / elapsed);
                _lastBytesTransferred = currentBytes;
                _lastSpeedUpdate = now;
            }
        }
    }
    
    private void ReportProgress(CopyTransferOperation operation, CopyTransferItem? item, IProgress<CopyTransferProgressEventArgs>? progress)
    {
        var args = new CopyTransferProgressEventArgs
        {
            Operation = operation,
            CurrentItem = item,
            BytesPerSecond = GetCurrentSpeed(),
            EstimatedTimeRemaining = GetEstimatedTimeRemaining()
        };
        
        progress?.Report(args);
        Progress?.Invoke(this, args);
    }
    
    #endregion
    
    #region Verification
    
    public async Task<bool> VerifyTransferAsync(CopyTransferItem item, VerificationMethod method, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(item.DestinationPath))
            return false;
        
        var sourceHash = item.SourceHash ?? await CalculateHashAsync(item.SourcePath, method, cancellationToken);
        var destHash = await CalculateHashAsync(item.DestinationPath, method, cancellationToken);
        
        item.SourceHash = sourceHash;
        item.DestinationHash = destHash;
        
        return sourceHash.Equals(destHash, StringComparison.OrdinalIgnoreCase);
    }
    
    public async Task<IReadOnlyList<CopyTransferItem>> VerifyOperationAsync(CopyTransferOperation operation, VerificationMethod method, CancellationToken cancellationToken = default)
    {
        var failed = new List<CopyTransferItem>();
        
        foreach (var item in operation.Items.Where(i => i.Status == CopyTransferStatus.Completed))
        {
            var verified = await VerifyTransferAsync(item, method, cancellationToken);
            item.VerificationPassed = verified;
            
            if (!verified)
            {
                failed.Add(item);
            }
        }
        
        return failed;
    }
    
    private static async Task<string> CalculateHashAsync(string filePath, VerificationMethod method, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var algorithm = method switch
        {
            VerificationMethod.Crc32 => new Crc32HashAlgorithm() as HashAlgorithm,
            VerificationMethod.SHA1 => SHA1.Create(),
            VerificationMethod.SHA256 => SHA256.Create(),
            _ => MD5.Create()
        };
        
        var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    #endregion
    
    #region Retry
    
    public async Task RetryFailedAsync(string operationId)
    {
        var operation = _history.FirstOrDefault(o => o.Id == operationId);
        if (operation == null) return;
        
        foreach (var item in operation.Items.Where(i => i.Status == CopyTransferStatus.Failed))
        {
            item.Status = CopyTransferStatus.Pending;
            item.ErrorMessage = null;
            item.BytesTransferred = 0;
        }
        
        await QueueOperationAsync(operation);
    }
    
    public Task RetryItemAsync(string operationId, string itemId)
    {
        var operation = _history.FirstOrDefault(o => o.Id == operationId);
        var item = operation?.Items.FirstOrDefault(i => i.Id == itemId);
        
        if (item != null)
        {
            item.Status = CopyTransferStatus.Pending;
            item.ErrorMessage = null;
            item.BytesTransferred = 0;
        }
        
        return Task.CompletedTask;
    }
    
    #endregion
    
    #region Statistics
    
    public long GetCurrentSpeed()
    {
        lock (_speedLock)
        {
            return _currentSpeed;
        }
    }
    
    public TimeSpan GetEstimatedTimeRemaining()
    {
        var operation = _currentOperation;
        if (operation == null) return TimeSpan.Zero;
        
        var speed = GetCurrentSpeed();
        if (speed <= 0) return TimeSpan.MaxValue;
        
        var remaining = operation.TotalBytes - operation.BytesTransferred;
        return TimeSpan.FromSeconds(remaining / (double)speed);
    }
    
    public Task<IReadOnlyList<CopyTransferOperation>> GetHistoryAsync(int maxCount = 50)
    {
        return Task.FromResult<IReadOnlyList<CopyTransferOperation>>(
            _history.Take(maxCount).ToList());
    }
    
    #endregion
}
