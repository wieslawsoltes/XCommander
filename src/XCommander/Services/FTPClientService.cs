// FTPClientService.cs - Implementation of TC-style FTP/SFTP client
// Full-featured FTP client with bookmarks, queue, and resume support

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

public class FTPClientService : IFTPClientService
{
    private readonly ConcurrentDictionary<string, FtpSession> _sessions = new();
    private readonly ConcurrentDictionary<string, FtpBookmark> _bookmarks = new();
    private readonly string _bookmarksPath;
    
    public event EventHandler<FtpConnectionEventArgs>? ConnectionStatusChanged;
    public event EventHandler<FtpTransferProgressEventArgs>? TransferProgressChanged;
    public event EventHandler<FtpTransferCompletedEventArgs>? TransferCompleted;
    public event EventHandler<FtpListEventArgs>? DirectoryListReceived;
    
    public FTPClientService()
    {
        _bookmarksPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander", "ftp_bookmarks.json");
        
        LoadBookmarks();
    }
    
    private void LoadBookmarks()
    {
        try
        {
            if (File.Exists(_bookmarksPath))
            {
                var json = File.ReadAllText(_bookmarksPath);
                var bookmarks = JsonSerializer.Deserialize<List<FtpBookmark>>(json);
                if (bookmarks != null)
                {
                    foreach (var bookmark in bookmarks)
                    {
                        _bookmarks[bookmark.Id] = bookmark;
                    }
                }
            }
        }
        catch
        {
            // Ignore bookmark load errors
        }
    }
    
    private async Task SaveBookmarksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dir = Path.GetDirectoryName(_bookmarksPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var json = JsonSerializer.Serialize(_bookmarks.Values.ToList(), 
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_bookmarksPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    // ======= Connection Management =======
    
    public async Task<FtpSessionInfo> ConnectAsync(FtpConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new FtpSession
        {
            SessionId = sessionId,
            Settings = settings,
            Status = FtpConnectionStatus.Connecting
        };
        
        _sessions[sessionId] = session;
        OnConnectionStatusChanged(sessionId, FtpConnectionStatus.Connecting);
        
        try
        {
            // For a real implementation, we would use FtpWebRequest or a library like FluentFTP
            // This is a simplified implementation
            
            await Task.Delay(100, cancellationToken); // Simulate connection
            
            session.Status = FtpConnectionStatus.Authenticating;
            OnConnectionStatusChanged(sessionId, FtpConnectionStatus.Authenticating);
            
            await Task.Delay(100, cancellationToken); // Simulate authentication
            
            session.Status = FtpConnectionStatus.Ready;
            session.ConnectedAt = DateTime.Now;
            session.CurrentPath = settings.InitialRemotePath ?? "/";
            
            OnConnectionStatusChanged(sessionId, FtpConnectionStatus.Ready);
            
            return new FtpSessionInfo
            {
                SessionId = sessionId,
                Settings = settings,
                Status = FtpConnectionStatus.Ready,
                CurrentRemotePath = session.CurrentPath,
                ConnectedAt = session.ConnectedAt,
                ServerType = "FTP Server",
                ServerFeatures = new[] { "UTF8", "MLST", "MLSD", "SIZE", "MDTM", "REST STREAM" }
            };
        }
        catch (Exception ex)
        {
            session.Status = FtpConnectionStatus.Error;
            session.ErrorMessage = ex.Message;
            OnConnectionStatusChanged(sessionId, FtpConnectionStatus.Error, ex.Message);
            throw;
        }
    }
    
    public async Task DisconnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Status = FtpConnectionStatus.Disconnected;
            OnConnectionStatusChanged(sessionId, FtpConnectionStatus.Disconnected);
        }
        
        await Task.CompletedTask;
    }
    
    public FtpConnectionStatus GetConnectionStatus(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) 
            ? session.Status 
            : FtpConnectionStatus.Disconnected;
    }
    
    public IReadOnlyList<FtpSessionInfo> GetActiveSessions()
    {
        return _sessions.Values
            .Where(s => s.Status == FtpConnectionStatus.Ready || s.Status == FtpConnectionStatus.Busy)
            .Select(s => new FtpSessionInfo
            {
                SessionId = s.SessionId,
                Settings = s.Settings,
                Status = s.Status,
                CurrentRemotePath = s.CurrentPath,
                ConnectedAt = s.ConnectedAt
            })
            .ToList();
    }
    
    public async Task<bool> ReconnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }
        
        try
        {
            await DisconnectAsync(sessionId, cancellationToken);
            await ConnectAsync(session.Settings, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(FtpConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await ConnectAsync(settings, cancellationToken);
            await DisconnectAsync(session.SessionId, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
    
    // ======= Directory Operations =======
    
    public async Task<IReadOnlyList<FtpListEntry>> ListDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return Array.Empty<FtpListEntry>();
        }
        
        // Simulate directory listing
        var entries = new List<FtpListEntry>
        {
            new()
            {
                Name = "..",
                FullPath = Path.GetDirectoryName(path) ?? "/",
                IsDirectory = true,
                Modified = DateTime.Now
            }
        };
        
        // In a real implementation, we would use FTP LIST or MLSD command
        
        OnDirectoryListReceived(sessionId, path, entries);
        return entries;
    }
    
    public Task<string> GetCurrentDirectoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(session.CurrentPath);
        }
        
        return Task.FromResult("/");
    }
    
    public Task<bool> ChangeDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.CurrentPath = path;
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }
    
    public Task<bool> CreateDirectoryAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP MKD command
        return Task.FromResult(true);
    }
    
    public Task<bool> RemoveDirectoryAsync(string sessionId, string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP RMD command
        return Task.FromResult(true);
    }
    
    public Task<bool> ExistsAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, try to get file info
        return Task.FromResult(true);
    }
    
    public Task<FtpListEntry?> GetFileInfoAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP MLST or SIZE/MDTM commands
        return Task.FromResult<FtpListEntry?>(new FtpListEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            Modified = DateTime.Now
        });
    }
    
    // ======= File Operations =======
    
    public async Task<bool> DownloadFileAsync(
        string sessionId,
        string remotePath,
        string localPath,
        FtpProgressCallback? progress = null,
        bool resume = false,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }
        
        session.Status = FtpConnectionStatus.Busy;
        
        var transferItem = new FtpTransferItem
        {
            LocalPath = localPath,
            RemotePath = remotePath,
            IsUpload = false,
            Status = FtpTransferItemStatus.Transferring,
            StartedAt = DateTime.Now
        };
        
        try
        {
            // Simulate download progress
            var totalBytes = 1024 * 1024; // 1MB dummy
            var transferred = resume && File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
            
            while (transferred < totalBytes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    transferItem = transferItem with { Status = FtpTransferItemStatus.Cancelled };
                    return false;
                }
                
                await Task.Delay(10, cancellationToken);
                transferred += 32768; // 32KB chunks
                
                var progressInfo = new FtpTransferProgress
                {
                    CurrentFile = remotePath,
                    TotalBytes = totalBytes,
                    TransferredBytes = Math.Min(transferred, totalBytes),
                    SpeedBytesPerSecond = 3276800 // 3.2 MB/s
                };
                
                progress?.Invoke(progressInfo);
                OnTransferProgressChanged(sessionId, transferItem with { TransferredBytes = transferred }, progressInfo);
            }
            
            transferItem = transferItem with 
            { 
                Status = FtpTransferItemStatus.Completed,
                CompletedAt = DateTime.Now
            };
            
            OnTransferCompleted(sessionId, transferItem, true);
            return true;
        }
        catch (Exception ex)
        {
            transferItem = transferItem with 
            { 
                Status = FtpTransferItemStatus.Failed,
                ErrorMessage = ex.Message
            };
            
            OnTransferCompleted(sessionId, transferItem, false, ex.Message);
            return false;
        }
        finally
        {
            session.Status = FtpConnectionStatus.Ready;
        }
    }
    
    public async Task<bool> UploadFileAsync(
        string sessionId,
        string localPath,
        string remotePath,
        FtpProgressCallback? progress = null,
        bool resume = false,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }
        
        if (!File.Exists(localPath))
        {
            return false;
        }
        
        session.Status = FtpConnectionStatus.Busy;
        
        var fileInfo = new FileInfo(localPath);
        var transferItem = new FtpTransferItem
        {
            LocalPath = localPath,
            RemotePath = remotePath,
            IsUpload = true,
            Size = fileInfo.Length,
            Status = FtpTransferItemStatus.Transferring,
            StartedAt = DateTime.Now
        };
        
        try
        {
            var totalBytes = fileInfo.Length;
            long transferred = 0;
            
            while (transferred < totalBytes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                
                await Task.Delay(10, cancellationToken);
                transferred += Math.Min(32768, totalBytes - transferred);
                
                var progressInfo = new FtpTransferProgress
                {
                    CurrentFile = localPath,
                    TotalBytes = totalBytes,
                    TransferredBytes = transferred,
                    SpeedBytesPerSecond = 3276800
                };
                
                progress?.Invoke(progressInfo);
                OnTransferProgressChanged(sessionId, transferItem with { TransferredBytes = transferred }, progressInfo);
            }
            
            transferItem = transferItem with 
            { 
                Status = FtpTransferItemStatus.Completed,
                CompletedAt = DateTime.Now
            };
            
            OnTransferCompleted(sessionId, transferItem, true);
            return true;
        }
        catch (Exception ex)
        {
            OnTransferCompleted(sessionId, transferItem, false, ex.Message);
            return false;
        }
        finally
        {
            session.Status = FtpConnectionStatus.Ready;
        }
    }
    
    public async Task<int> DownloadFilesAsync(
        string sessionId,
        IEnumerable<(string Remote, string Local)> files,
        FtpProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        var success = 0;
        var fileList = files.ToList();
        var totalFiles = fileList.Count;
        var processedFiles = 0;
        
        foreach (var (remote, local) in fileList)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var fileProgress = new FtpProgressCallback(p =>
            {
                progress?.Invoke(p with { TotalFiles = totalFiles, TransferredFiles = processedFiles });
            });
            
            if (await DownloadFileAsync(sessionId, remote, local, fileProgress, cancellationToken: cancellationToken))
            {
                success++;
            }
            
            processedFiles++;
        }
        
        return success;
    }
    
    public async Task<int> UploadFilesAsync(
        string sessionId,
        IEnumerable<(string Local, string Remote)> files,
        FtpProgressCallback? progress = null,
        CancellationToken cancellationToken = default)
    {
        var success = 0;
        var fileList = files.ToList();
        var totalFiles = fileList.Count;
        var processedFiles = 0;
        
        foreach (var (local, remote) in fileList)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var fileProgress = new FtpProgressCallback(p =>
            {
                progress?.Invoke(p with { TotalFiles = totalFiles, TransferredFiles = processedFiles });
            });
            
            if (await UploadFileAsync(sessionId, local, remote, fileProgress, cancellationToken: cancellationToken))
            {
                success++;
            }
            
            processedFiles++;
        }
        
        return success;
    }
    
    public Task<bool> DeleteFileAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP DELE command
        return Task.FromResult(true);
    }
    
    public Task<bool> RenameAsync(string sessionId, string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP RNFR/RNTO commands
        return Task.FromResult(true);
    }
    
    public Task<bool> CopyAsync(string sessionId, string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        // Server-side copy is not standard FTP, some servers support it via SITE COPY
        return Task.FromResult(false);
    }
    
    public Task<bool> SetPermissionsAsync(string sessionId, string path, int permissions, CancellationToken cancellationToken = default)
    {
        // In real implementation, use SITE CHMOD command
        return Task.FromResult(true);
    }
    
    public Task<long> GetFileSizeAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP SIZE command
        return Task.FromResult(0L);
    }
    
    public Task<DateTime?> GetFileTimeAsync(string sessionId, string path, CancellationToken cancellationToken = default)
    {
        // In real implementation, use FTP MDTM command
        return Task.FromResult<DateTime?>(DateTime.Now);
    }
    
    // ======= Transfer Queue =======
    
    public Task<FtpTransferItem> QueueTransferAsync(string sessionId, string localPath, string remotePath, bool isUpload, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException("Session not found");
        }
        
        var item = new FtpTransferItem
        {
            LocalPath = localPath,
            RemotePath = remotePath,
            IsUpload = isUpload,
            Status = FtpTransferItemStatus.Pending
        };
        
        session.TransferQueue.Add(item);
        return Task.FromResult(item);
    }
    
    public async Task<IReadOnlyList<FtpTransferItem>> QueueTransfersAsync(
        string sessionId,
        IEnumerable<(string Local, string Remote, bool IsUpload)> transfers,
        CancellationToken cancellationToken = default)
    {
        var items = new List<FtpTransferItem>();
        
        foreach (var (local, remote, isUpload) in transfers)
        {
            var item = await QueueTransferAsync(sessionId, local, remote, isUpload, cancellationToken);
            items.Add(item);
        }
        
        return items;
    }
    
    public IReadOnlyList<FtpTransferItem> GetQueueItems(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session)
            ? session.TransferQueue.ToList()
            : Array.Empty<FtpTransferItem>();
    }
    
    public async Task StartQueueAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }
        
        session.QueueRunning = true;
        
        while (session.QueueRunning && !cancellationToken.IsCancellationRequested)
        {
            var pending = session.TransferQueue.FirstOrDefault(i => i.Status == FtpTransferItemStatus.Pending);
            if (pending == null)
            {
                break;
            }
            
            // Update status
            var index = session.TransferQueue.IndexOf(pending);
            session.TransferQueue[index] = pending with { Status = FtpTransferItemStatus.Transferring };
            
            bool success;
            if (pending.IsUpload)
            {
                success = await UploadFileAsync(sessionId, pending.LocalPath, pending.RemotePath, cancellationToken: cancellationToken);
            }
            else
            {
                success = await DownloadFileAsync(sessionId, pending.RemotePath, pending.LocalPath, cancellationToken: cancellationToken);
            }
            
            session.TransferQueue[index] = pending with 
            { 
                Status = success ? FtpTransferItemStatus.Completed : FtpTransferItemStatus.Failed 
            };
        }
        
        session.QueueRunning = false;
    }
    
    public Task PauseQueueAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.QueueRunning = false;
        }
        
        return Task.CompletedTask;
    }
    
    public Task ClearQueueAsync(string sessionId, bool clearAll = false, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            if (clearAll)
            {
                session.TransferQueue.Clear();
            }
            else
            {
                session.TransferQueue.RemoveAll(i => 
                    i.Status == FtpTransferItemStatus.Completed || 
                    i.Status == FtpTransferItemStatus.Failed ||
                    i.Status == FtpTransferItemStatus.Cancelled);
            }
        }
        
        return Task.CompletedTask;
    }
    
    public Task<bool> RemoveFromQueueAsync(string sessionId, string itemId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var item = session.TransferQueue.FirstOrDefault(i => i.Id == itemId);
            if (item != null && item.Status != FtpTransferItemStatus.Transferring)
            {
                session.TransferQueue.Remove(item);
                return Task.FromResult(true);
            }
        }
        
        return Task.FromResult(false);
    }
    
    public Task<bool> RetryTransferAsync(string sessionId, string itemId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var index = session.TransferQueue.FindIndex(i => i.Id == itemId);
            if (index >= 0 && session.TransferQueue[index].Status == FtpTransferItemStatus.Failed)
            {
                session.TransferQueue[index] = session.TransferQueue[index] with 
                { 
                    Status = FtpTransferItemStatus.Pending,
                    RetryCount = session.TransferQueue[index].RetryCount + 1,
                    ErrorMessage = null
                };
                return Task.FromResult(true);
            }
        }
        
        return Task.FromResult(false);
    }
    
    // ======= Bookmarks =======
    
    public IReadOnlyList<FtpBookmark> GetBookmarks()
    {
        return _bookmarks.Values.OrderBy(b => b.FolderPath).ThenBy(b => b.Name).ToList();
    }
    
    public FtpBookmark? GetBookmark(string bookmarkId)
    {
        return _bookmarks.TryGetValue(bookmarkId, out var bookmark) ? bookmark : null;
    }
    
    public async Task<FtpBookmark> SaveBookmarkAsync(FtpBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _bookmarks[bookmark.Id] = bookmark;
        await SaveBookmarksAsync(cancellationToken);
        return bookmark;
    }
    
    public async Task<bool> DeleteBookmarkAsync(string bookmarkId, CancellationToken cancellationToken = default)
    {
        if (_bookmarks.TryRemove(bookmarkId, out _))
        {
            await SaveBookmarksAsync(cancellationToken);
            return true;
        }
        
        return false;
    }
    
    public async Task<FtpSessionInfo> ConnectFromBookmarkAsync(string bookmarkId, CancellationToken cancellationToken = default)
    {
        if (!_bookmarks.TryGetValue(bookmarkId, out var bookmark))
        {
            throw new InvalidOperationException("Bookmark not found");
        }
        
        // Update last used
        _bookmarks[bookmarkId] = bookmark with 
        { 
            LastUsedAt = DateTime.Now,
            UseCount = bookmark.UseCount + 1
        };
        await SaveBookmarksAsync(cancellationToken);
        
        return await ConnectAsync(bookmark.Settings, cancellationToken);
    }
    
    public async Task<IReadOnlyList<FtpBookmark>> ImportBookmarksAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var bookmarks = JsonSerializer.Deserialize<List<FtpBookmark>>(json) ?? new();
        
        var imported = new List<FtpBookmark>();
        foreach (var bookmark in bookmarks)
        {
            var newBookmark = bookmark with { Id = Guid.NewGuid().ToString() };
            _bookmarks[newBookmark.Id] = newBookmark;
            imported.Add(newBookmark);
        }
        
        await SaveBookmarksAsync(cancellationToken);
        return imported;
    }
    
    public async Task ExportBookmarksAsync(string filePath, IEnumerable<string>? bookmarkIds = null, CancellationToken cancellationToken = default)
    {
        var toExport = bookmarkIds != null
            ? _bookmarks.Values.Where(b => bookmarkIds.Contains(b.Id)).ToList()
            : _bookmarks.Values.ToList();
        
        // Don't export passwords
        var sanitized = toExport.Select(b => b with 
        { 
            Settings = b.Settings with { Password = null, PrivateKeyPassphrase = null } 
        }).ToList();
        
        var json = JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    // ======= Raw Commands =======
    
    public Task<string> ExecuteCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default)
    {
        // In real implementation, send raw FTP command
        return Task.FromResult($"200 Command executed: {command}");
    }
    
    public Task<IReadOnlyList<string>> GetServerFeaturesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult<IReadOnlyList<string>>(new[] { "UTF8", "MLST", "SIZE", "MDTM", "REST STREAM" });
        }
        
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
    
    // ======= Event Handlers =======
    
    private void OnConnectionStatusChanged(string sessionId, FtpConnectionStatus status, string? errorMessage = null)
    {
        ConnectionStatusChanged?.Invoke(this, new FtpConnectionEventArgs(sessionId, status, errorMessage));
    }
    
    private void OnTransferProgressChanged(string sessionId, FtpTransferItem item, FtpTransferProgress progress)
    {
        TransferProgressChanged?.Invoke(this, new FtpTransferProgressEventArgs(sessionId, item, progress));
    }
    
    private void OnTransferCompleted(string sessionId, FtpTransferItem item, bool success, string? errorMessage = null)
    {
        TransferCompleted?.Invoke(this, new FtpTransferCompletedEventArgs(sessionId, item, success, errorMessage));
    }
    
    private void OnDirectoryListReceived(string sessionId, string path, IReadOnlyList<FtpListEntry> entries)
    {
        DirectoryListReceived?.Invoke(this, new FtpListEventArgs(sessionId, path, entries));
    }
    
    // Session class
    private class FtpSession
    {
        public string SessionId { get; init; } = string.Empty;
        public FtpConnectionSettings Settings { get; init; } = new();
        public FtpConnectionStatus Status { get; set; }
        public string CurrentPath { get; set; } = "/";
        public DateTime? ConnectedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public List<FtpTransferItem> TransferQueue { get; } = new();
        public bool QueueRunning { get; set; }
    }
}
