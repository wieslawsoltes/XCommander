using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace XCommander.Services;

/// <summary>
/// Implementation of background archive operations.
/// </summary>
public sealed class BackgroundArchiveService : IBackgroundArchiveService, IDisposable
{
    private readonly ConcurrentDictionary<string, BackgroundArchiveOperation> _operations = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly ConcurrentDictionary<string, bool> _pauseFlags = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly PriorityQueue<string, int> _queue = new();
    private readonly object _queueLock = new();
    private int _maxConcurrent;
    private bool _disposed;
    
    public event EventHandler<BackgroundArchiveProgress>? OperationProgress;
    public event EventHandler<BackgroundArchiveOperation>? OperationCompleted;
    public event EventHandler<BackgroundArchiveOperation>? OperationFailed;
    
    public BackgroundArchiveService(int maxConcurrent = 2)
    {
        _maxConcurrent = maxConcurrent;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }
    
    public async Task<string> QueueCreateAsync(
        string archivePath,
        IEnumerable<string> sourcePaths,
        int compressionLevel = -1,
        int priority = 0)
    {
        var operation = new BackgroundArchiveOperation
        {
            OperationType = BackgroundArchiveOperationType.Create,
            ArchivePath = archivePath,
            SourcePaths = sourcePaths.ToList(),
            CompressionLevel = compressionLevel,
            Priority = priority,
            Status = BackgroundArchiveStatus.Queued
        };
        
        return await QueueOperationAsync(operation);
    }
    
    public async Task<string> QueueExtractAsync(
        string archivePath,
        string destinationPath,
        int priority = 0)
    {
        var operation = new BackgroundArchiveOperation
        {
            OperationType = BackgroundArchiveOperationType.Extract,
            ArchivePath = archivePath,
            DestinationPath = destinationPath,
            Priority = priority,
            Status = BackgroundArchiveStatus.Queued
        };
        
        return await QueueOperationAsync(operation);
    }
    
    public async Task<string> QueueAddAsync(
        string archivePath,
        IEnumerable<string> sourcePaths,
        int compressionLevel = -1,
        int priority = 0)
    {
        var operation = new BackgroundArchiveOperation
        {
            OperationType = BackgroundArchiveOperationType.Add,
            ArchivePath = archivePath,
            SourcePaths = sourcePaths.ToList(),
            CompressionLevel = compressionLevel,
            Priority = priority,
            Status = BackgroundArchiveStatus.Queued
        };
        
        return await QueueOperationAsync(operation);
    }
    
    public async Task<string> QueueTestAsync(string archivePath, int priority = 0)
    {
        var operation = new BackgroundArchiveOperation
        {
            OperationType = BackgroundArchiveOperationType.Test,
            ArchivePath = archivePath,
            Priority = priority,
            Status = BackgroundArchiveStatus.Queued
        };
        
        return await QueueOperationAsync(operation);
    }
    
    public async Task<string> QueueConvertAsync(
        string sourceArchive,
        string destinationArchive,
        int compressionLevel = -1,
        int priority = 0)
    {
        var operation = new BackgroundArchiveOperation
        {
            OperationType = BackgroundArchiveOperationType.Convert,
            ArchivePath = sourceArchive,
            DestinationPath = destinationArchive,
            CompressionLevel = compressionLevel,
            Priority = priority,
            Status = BackgroundArchiveStatus.Queued
        };
        
        return await QueueOperationAsync(operation);
    }
    
    private async Task<string> QueueOperationAsync(BackgroundArchiveOperation operation)
    {
        _operations[operation.Id] = operation;
        _cancellationTokens[operation.Id] = new CancellationTokenSource();
        _pauseFlags[operation.Id] = false;
        
        lock (_queueLock)
        {
            _queue.Enqueue(operation.Id, -operation.Priority); // Negative for higher priority first
        }
        
        _ = ProcessQueueAsync();
        
        return await Task.FromResult(operation.Id);
    }
    
    private async Task ProcessQueueAsync()
    {
        string? operationId = null;
        
        lock (_queueLock)
        {
            if (_queue.Count == 0)
                return;
            
            if (!_concurrencyLimiter.Wait(0))
                return;
            
            operationId = _queue.Dequeue();
        }
        
        if (operationId == null)
        {
            _concurrencyLimiter.Release();
            return;
        }
        
        try
        {
            await ExecuteOperationAsync(operationId);
        }
        finally
        {
            _concurrencyLimiter.Release();
            _ = ProcessQueueAsync(); // Process next queued item
        }
    }
    
    private async Task ExecuteOperationAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return;
        
        if (!_cancellationTokens.TryGetValue(operationId, out var cts))
            return;
        
        operation.Status = BackgroundArchiveStatus.Running;
        operation.StartedAt = DateTime.UtcNow;
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            switch (operation.OperationType)
            {
                case BackgroundArchiveOperationType.Create:
                    await ExecuteCreateAsync(operation, cts.Token, stopwatch);
                    break;
                case BackgroundArchiveOperationType.Extract:
                    await ExecuteExtractAsync(operation, cts.Token, stopwatch);
                    break;
                case BackgroundArchiveOperationType.Add:
                    await ExecuteAddAsync(operation, cts.Token, stopwatch);
                    break;
                case BackgroundArchiveOperationType.Test:
                    await ExecuteTestAsync(operation, cts.Token, stopwatch);
                    break;
                case BackgroundArchiveOperationType.Convert:
                    await ExecuteConvertAsync(operation, cts.Token, stopwatch);
                    break;
            }
            
            operation.Status = BackgroundArchiveStatus.Completed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ProgressPercent = 100;
            
            OperationCompleted?.Invoke(this, operation);
        }
        catch (OperationCanceledException)
        {
            operation.Status = BackgroundArchiveStatus.Cancelled;
            operation.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            operation.Status = BackgroundArchiveStatus.Failed;
            operation.ErrorMessage = ex.Message;
            operation.CompletedAt = DateTime.UtcNow;
            
            OperationFailed?.Invoke(this, operation);
        }
    }
    
    private async Task ExecuteCreateAsync(
        BackgroundArchiveOperation operation,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        var allFiles = new List<string>();
        
        foreach (var path in operation.SourcePaths)
        {
            if (Directory.Exists(path))
            {
                allFiles.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                allFiles.Add(path);
            }
        }
        
        operation.TotalFiles = allFiles.Count;
        operation.TotalBytes = allFiles.Sum(f => new FileInfo(f).Length);
        
        var basePath = operation.SourcePaths.Count == 1 && Directory.Exists(operation.SourcePaths[0])
            ? operation.SourcePaths[0]
            : Path.GetDirectoryName(operation.SourcePaths[0]) ?? "";
        
        using var archive = ZipArchive.Create();
        
        foreach (var file in allFiles)
        {
            await WaitIfPausedAsync(operation.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            var relativePath = Path.GetRelativePath(basePath, file);
            operation.CurrentFile = relativePath;
            
            using var stream = File.OpenRead(file);
            archive.AddEntry(relativePath, stream, true);
            
            var fileInfo = new FileInfo(file);
            operation.BytesProcessed += fileInfo.Length;
            operation.FilesProcessed++;
            operation.ProgressPercent = operation.TotalBytes > 0
                ? (double)operation.BytesProcessed / operation.TotalBytes * 100
                : 0;
            
            ReportProgress(operation, stopwatch.Elapsed);
        }
        
        var directory = Path.GetDirectoryName(operation.ArchivePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        archive.SaveTo(operation.ArchivePath, CompressionType.Deflate);
    }
    
    private async Task ExecuteExtractAsync(
        BackgroundArchiveOperation operation,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        using var archive = ArchiveFactory.Open(operation.ArchivePath);
        
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        operation.TotalFiles = entries.Count;
        operation.TotalBytes = entries.Sum(e => e.Size);
        
        if (!string.IsNullOrEmpty(operation.DestinationPath) && !Directory.Exists(operation.DestinationPath))
        {
            Directory.CreateDirectory(operation.DestinationPath);
        }
        
        foreach (var entry in entries)
        {
            await WaitIfPausedAsync(operation.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            operation.CurrentFile = entry.Key ?? "";
            
            if (!string.IsNullOrEmpty(operation.DestinationPath))
            {
                entry.WriteToDirectory(operation.DestinationPath, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
            
            operation.BytesProcessed += entry.Size;
            operation.FilesProcessed++;
            operation.ProgressPercent = operation.TotalBytes > 0
                ? (double)operation.BytesProcessed / operation.TotalBytes * 100
                : 0;
            
            ReportProgress(operation, stopwatch.Elapsed);
        }
    }
    
    private async Task ExecuteAddAsync(
        BackgroundArchiveOperation operation,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        // For add, we extract to temp, add files, and repack
        var allFiles = new List<string>();
        
        foreach (var path in operation.SourcePaths)
        {
            if (Directory.Exists(path))
            {
                allFiles.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                allFiles.Add(path);
            }
        }
        
        operation.TotalFiles = allFiles.Count;
        operation.TotalBytes = allFiles.Sum(f => new FileInfo(f).Length);
        
        using var archive = ZipArchive.Open(operation.ArchivePath);
        
        var basePath = operation.SourcePaths.Count == 1 && Directory.Exists(operation.SourcePaths[0])
            ? operation.SourcePaths[0]
            : Path.GetDirectoryName(operation.SourcePaths[0]) ?? "";
        
        foreach (var file in allFiles)
        {
            await WaitIfPausedAsync(operation.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            var relativePath = Path.GetRelativePath(basePath, file);
            operation.CurrentFile = relativePath;
            
            using var stream = File.OpenRead(file);
            archive.AddEntry(relativePath, stream, true);
            
            var fileInfo = new FileInfo(file);
            operation.BytesProcessed += fileInfo.Length;
            operation.FilesProcessed++;
            operation.ProgressPercent = operation.TotalBytes > 0
                ? (double)operation.BytesProcessed / operation.TotalBytes * 100
                : 0;
            
            ReportProgress(operation, stopwatch.Elapsed);
        }
        
        archive.SaveTo(operation.ArchivePath, CompressionType.Deflate);
    }
    
    private async Task ExecuteTestAsync(
        BackgroundArchiveOperation operation,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        using var archive = ArchiveFactory.Open(operation.ArchivePath);
        
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        operation.TotalFiles = entries.Count;
        operation.TotalBytes = entries.Sum(e => e.Size);
        
        foreach (var entry in entries)
        {
            await WaitIfPausedAsync(operation.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            operation.CurrentFile = entry.Key ?? "";
            
            // Test by reading the entry
            using var stream = entry.OpenEntryStream();
            using var memStream = new MemoryStream();
            await stream.CopyToAsync(memStream, cancellationToken);
            
            operation.BytesProcessed += entry.Size;
            operation.FilesProcessed++;
            operation.ProgressPercent = operation.TotalBytes > 0
                ? (double)operation.BytesProcessed / operation.TotalBytes * 100
                : 0;
            
            ReportProgress(operation, stopwatch.Elapsed);
        }
    }
    
    private async Task ExecuteConvertAsync(
        BackgroundArchiveOperation operation,
        CancellationToken cancellationToken,
        Stopwatch stopwatch)
    {
        using var sourceArchive = ArchiveFactory.Open(operation.ArchivePath);
        
        var entries = sourceArchive.Entries.Where(e => !e.IsDirectory).ToList();
        operation.TotalFiles = entries.Count;
        operation.TotalBytes = entries.Sum(e => e.Size);
        
        var directory = Path.GetDirectoryName(operation.DestinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var destArchive = ZipArchive.Create();
        
        foreach (var entry in entries)
        {
            await WaitIfPausedAsync(operation.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            operation.CurrentFile = entry.Key ?? "";
            
            using var stream = entry.OpenEntryStream();
            using var memStream = new MemoryStream();
            await stream.CopyToAsync(memStream, cancellationToken);
            memStream.Position = 0;
            
            destArchive.AddEntry(entry.Key ?? "", memStream, true);
            
            operation.BytesProcessed += entry.Size;
            operation.FilesProcessed++;
            operation.ProgressPercent = operation.TotalBytes > 0
                ? (double)operation.BytesProcessed / operation.TotalBytes * 100
                : 0;
            
            ReportProgress(operation, stopwatch.Elapsed);
        }
        
        if (!string.IsNullOrEmpty(operation.DestinationPath))
        {
            destArchive.SaveTo(operation.DestinationPath, CompressionType.Deflate);
        }
    }
    
    private async Task WaitIfPausedAsync(string operationId, CancellationToken cancellationToken)
    {
        while (_pauseFlags.TryGetValue(operationId, out var paused) && paused)
        {
            await Task.Delay(100, cancellationToken);
        }
    }
    
    private void ReportProgress(BackgroundArchiveOperation operation, TimeSpan elapsed)
    {
        TimeSpan? remaining = null;
        if (operation.ProgressPercent > 0)
        {
            var totalEstimated = TimeSpan.FromTicks((long)(elapsed.Ticks / operation.ProgressPercent * 100));
            remaining = totalEstimated - elapsed;
        }
        
        var progress = new BackgroundArchiveProgress
        {
            OperationId = operation.Id,
            Status = operation.Status,
            ProgressPercent = operation.ProgressPercent,
            CurrentFile = operation.CurrentFile,
            FilesProcessed = operation.FilesProcessed,
            TotalFiles = operation.TotalFiles,
            BytesProcessed = operation.BytesProcessed,
            TotalBytes = operation.TotalBytes,
            Elapsed = elapsed,
            EstimatedRemaining = remaining
        };
        
        OperationProgress?.Invoke(this, progress);
    }
    
    public BackgroundArchiveOperation? GetOperation(string operationId)
    {
        return _operations.TryGetValue(operationId, out var operation) ? operation : null;
    }
    
    public IReadOnlyList<BackgroundArchiveOperation> GetAllOperations()
    {
        return _operations.Values.ToList();
    }
    
    public IReadOnlyList<BackgroundArchiveOperation> GetQueuedOperations()
    {
        return _operations.Values.Where(o => o.Status == BackgroundArchiveStatus.Queued).ToList();
    }
    
    public IReadOnlyList<BackgroundArchiveOperation> GetRunningOperations()
    {
        return _operations.Values.Where(o => o.Status == BackgroundArchiveStatus.Running).ToList();
    }
    
    public IReadOnlyList<BackgroundArchiveOperation> GetCompletedOperations()
    {
        return _operations.Values.Where(o =>
            o.Status is BackgroundArchiveStatus.Completed or BackgroundArchiveStatus.Failed or BackgroundArchiveStatus.Cancelled
        ).ToList();
    }
    
    public Task<bool> PauseOperationAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return Task.FromResult(false);
        
        if (operation.Status != BackgroundArchiveStatus.Running)
            return Task.FromResult(false);
        
        _pauseFlags[operationId] = true;
        operation.Status = BackgroundArchiveStatus.Paused;
        
        return Task.FromResult(true);
    }
    
    public Task<bool> ResumeOperationAsync(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return Task.FromResult(false);
        
        if (operation.Status != BackgroundArchiveStatus.Paused)
            return Task.FromResult(false);
        
        _pauseFlags[operationId] = false;
        operation.Status = BackgroundArchiveStatus.Running;
        
        return Task.FromResult(true);
    }
    
    public Task<bool> CancelOperationAsync(string operationId)
    {
        if (!_cancellationTokens.TryGetValue(operationId, out var cts))
            return Task.FromResult(false);
        
        cts.Cancel();
        return Task.FromResult(true);
    }
    
    public bool RemoveOperation(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            return false;
        
        if (operation.Status is BackgroundArchiveStatus.Running or BackgroundArchiveStatus.Paused)
            return false;
        
        _operations.TryRemove(operationId, out _);
        _cancellationTokens.TryRemove(operationId, out _);
        _pauseFlags.TryRemove(operationId, out _);
        
        return true;
    }
    
    public void ClearCompletedOperations()
    {
        var completed = GetCompletedOperations();
        foreach (var operation in completed)
        {
            RemoveOperation(operation.Id);
        }
    }
    
    public void SetMaxConcurrent(int max)
    {
        _maxConcurrent = Math.Max(1, max);
    }
    
    public int GetMaxConcurrent() => _maxConcurrent;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        
        _concurrencyLimiter.Dispose();
    }
}
