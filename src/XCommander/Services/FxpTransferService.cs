using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of FXP (File eXchange Protocol) transfers.
/// Enables direct server-to-server FTP file transfers without local download.
/// </summary>
public sealed class FxpTransferService : IFxpTransferService, IDisposable
{
    private TcpClient? _sourceClient;
    private StreamReader? _sourceReader;
    private StreamWriter? _sourceWriter;
    
    private TcpClient? _targetClient;
    private StreamReader? _targetReader;
    private StreamWriter? _targetWriter;
    
    private FxpServerConnection? _sourceConnection;
    private FxpServerConnection? _targetConnection;
    
    private bool _disposed;
    
    public event EventHandler<FxpTransferProgress>? TransferProgress;
    public event EventHandler<FxpTransferResult>? FileTransferred;
    public event EventHandler<(bool SourceConnected, bool TargetConnected)>? ConnectionStatusChanged;
    
    public bool IsSourceConnected => _sourceClient?.Connected ?? false;
    public bool IsTargetConnected => _targetClient?.Connected ?? false;
    public FxpServerConnection? SourceConnection => _sourceConnection;
    public FxpServerConnection? TargetConnection => _targetConnection;
    
    public async Task<bool> ConnectSourceAsync(
        FxpServerConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _sourceClient = new TcpClient();
            await _sourceClient.ConnectAsync(connection.Host, connection.Port, cancellationToken);
            
            var stream = _sourceClient.GetStream();
            _sourceReader = new StreamReader(stream, Encoding.UTF8);
            _sourceWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            
            // Read welcome message
            await ReadResponseAsync(_sourceReader, cancellationToken);
            
            // Login
            await SendCommandAsync(_sourceWriter, $"USER {connection.Username}", cancellationToken);
            await ReadResponseAsync(_sourceReader, cancellationToken);
            
            await SendCommandAsync(_sourceWriter, $"PASS {connection.Password}", cancellationToken);
            var loginResponse = await ReadResponseAsync(_sourceReader, cancellationToken);
            
            if (!loginResponse.StartsWith("230"))
                return false;
            
            // Set binary mode
            await SendCommandAsync(_sourceWriter, "TYPE I", cancellationToken);
            await ReadResponseAsync(_sourceReader, cancellationToken);
            
            // Change directory
            if (!string.IsNullOrEmpty(connection.CurrentDirectory))
            {
                await SendCommandAsync(_sourceWriter, $"CWD {connection.CurrentDirectory}", cancellationToken);
                await ReadResponseAsync(_sourceReader, cancellationToken);
            }
            
            _sourceConnection = connection;
            ConnectionStatusChanged?.Invoke(this, (true, IsTargetConnected));
            
            return true;
        }
        catch
        {
            await DisconnectSourceAsync();
            return false;
        }
    }
    
    public async Task<bool> ConnectTargetAsync(
        FxpServerConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _targetClient = new TcpClient();
            await _targetClient.ConnectAsync(connection.Host, connection.Port, cancellationToken);
            
            var stream = _targetClient.GetStream();
            _targetReader = new StreamReader(stream, Encoding.UTF8);
            _targetWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            
            // Read welcome message
            await ReadResponseAsync(_targetReader, cancellationToken);
            
            // Login
            await SendCommandAsync(_targetWriter, $"USER {connection.Username}", cancellationToken);
            await ReadResponseAsync(_targetReader, cancellationToken);
            
            await SendCommandAsync(_targetWriter, $"PASS {connection.Password}", cancellationToken);
            var loginResponse = await ReadResponseAsync(_targetReader, cancellationToken);
            
            if (!loginResponse.StartsWith("230"))
                return false;
            
            // Set binary mode
            await SendCommandAsync(_targetWriter, "TYPE I", cancellationToken);
            await ReadResponseAsync(_targetReader, cancellationToken);
            
            // Change directory
            if (!string.IsNullOrEmpty(connection.CurrentDirectory))
            {
                await SendCommandAsync(_targetWriter, $"CWD {connection.CurrentDirectory}", cancellationToken);
                await ReadResponseAsync(_targetReader, cancellationToken);
            }
            
            _targetConnection = connection;
            ConnectionStatusChanged?.Invoke(this, (IsSourceConnected, true));
            
            return true;
        }
        catch
        {
            await DisconnectTargetAsync();
            return false;
        }
    }
    
    public Task DisconnectSourceAsync()
    {
        try
        {
            if (_sourceWriter != null && _sourceClient?.Connected == true)
            {
                _sourceWriter.WriteLine("QUIT");
            }
        }
        catch { }
        
        _sourceReader?.Dispose();
        _sourceWriter?.Dispose();
        _sourceClient?.Dispose();
        
        _sourceReader = null;
        _sourceWriter = null;
        _sourceClient = null;
        _sourceConnection = null;
        
        ConnectionStatusChanged?.Invoke(this, (false, IsTargetConnected));
        
        return Task.CompletedTask;
    }
    
    public Task DisconnectTargetAsync()
    {
        try
        {
            if (_targetWriter != null && _targetClient?.Connected == true)
            {
                _targetWriter.WriteLine("QUIT");
            }
        }
        catch { }
        
        _targetReader?.Dispose();
        _targetWriter?.Dispose();
        _targetClient?.Dispose();
        
        _targetReader = null;
        _targetWriter = null;
        _targetClient = null;
        _targetConnection = null;
        
        ConnectionStatusChanged?.Invoke(this, (IsSourceConnected, false));
        
        return Task.CompletedTask;
    }
    
    public async Task<bool> IsFxpSupportedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSourceConnected || !IsTargetConnected)
            return false;
        
        try
        {
            // Test if target supports PASV
            await SendCommandAsync(_targetWriter!, "PASV", cancellationToken);
            var pasvResponse = await ReadResponseAsync(_targetReader!, cancellationToken);
            
            if (!pasvResponse.StartsWith("227"))
                return false;
            
            // Test if source supports PORT
            // Parse PASV response to get address
            var pasvData = ParsePasvResponse(pasvResponse);
            if (pasvData == null)
                return false;
            
            await SendCommandAsync(_sourceWriter!, $"PORT {pasvData}", cancellationToken);
            var portResponse = await ReadResponseAsync(_sourceReader!, cancellationToken);
            
            // 200 means PORT succeeded, FXP is supported
            return portResponse.StartsWith("200");
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<IReadOnlyList<FxpFileInfo>> ListSourceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return await ListDirectoryAsync(_sourceWriter!, _sourceReader!, path, cancellationToken);
    }
    
    public async Task<IReadOnlyList<FxpFileInfo>> ListTargetAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return await ListDirectoryAsync(_targetWriter!, _targetReader!, path, cancellationToken);
    }
    
    private async Task<IReadOnlyList<FxpFileInfo>> ListDirectoryAsync(
        StreamWriter writer,
        StreamReader reader,
        string path,
        CancellationToken cancellationToken)
    {
        var result = new List<FxpFileInfo>();
        
        // Enter passive mode
        await SendCommandAsync(writer, "PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(reader, cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return result;
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return result;
        
        // Open data connection
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        // Send LIST command
        await SendCommandAsync(writer, $"LIST {path}", cancellationToken);
        var listResponse = await ReadResponseAsync(reader, cancellationToken);
        
        if (!listResponse.StartsWith("150") && !listResponse.StartsWith("125"))
            return result;
        
        // Read directory listing
        using var dataReader = new StreamReader(dataClient.GetStream());
        while (await dataReader.ReadLineAsync() is { } line)
        {
            var fileInfo = ParseListLine(line, path);
            if (fileInfo != null)
            {
                result.Add(fileInfo);
            }
        }
        
        // Read completion response
        await ReadResponseAsync(reader, cancellationToken);
        
        return result;
    }
    
    public async Task<FxpTransferResult> TransferFileAsync(
        FxpFileInfo file,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var (sourceWriter, sourceReader, targetWriter, targetReader) = direction switch
            {
                FxpTransferDirection.SourceToTarget => (_sourceWriter!, _sourceReader!, _targetWriter!, _targetReader!),
                FxpTransferDirection.TargetToSource => (_targetWriter!, _targetReader!, _sourceWriter!, _sourceReader!),
                _ => throw new ArgumentException("Invalid direction")
            };
            
            var (sourcePath, targetPath) = direction switch
            {
                FxpTransferDirection.SourceToTarget => (file.SourcePath, file.TargetPath),
                FxpTransferDirection.TargetToSource => (file.TargetPath, file.SourcePath),
                _ => throw new ArgumentException("Invalid direction")
            };
            
            // Put target in PASV mode
            await SendCommandAsync(targetWriter, "PASV", cancellationToken);
            var pasvResponse = await ReadResponseAsync(targetReader, cancellationToken);
            
            if (!pasvResponse.StartsWith("227"))
            {
                return new FxpTransferResult
                {
                    Success = false,
                    File = file,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    ErrorMessage = "Target server does not support PASV mode"
                };
            }
            
            var pasvData = ParsePasvResponse(pasvResponse);
            if (pasvData == null)
            {
                return new FxpTransferResult
                {
                    Success = false,
                    File = file,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    ErrorMessage = "Failed to parse PASV response"
                };
            }
            
            // Tell source to connect to target using PORT
            await SendCommandAsync(sourceWriter, $"PORT {pasvData}", cancellationToken);
            var portResponse = await ReadResponseAsync(sourceReader, cancellationToken);
            
            if (!portResponse.StartsWith("200"))
            {
                return new FxpTransferResult
                {
                    Success = false,
                    File = file,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    ErrorMessage = "Source server does not support PORT command"
                };
            }
            
            // Start STOR on target
            await SendCommandAsync(targetWriter, $"STOR {targetPath}", cancellationToken);
            
            // Start RETR on source
            await SendCommandAsync(sourceWriter, $"RETR {sourcePath}", cancellationToken);
            
            // Wait for transfer to start on both sides
            var sourceStart = await ReadResponseAsync(sourceReader, cancellationToken);
            var targetStart = await ReadResponseAsync(targetReader, cancellationToken);
            
            // Wait for transfer to complete on both sides
            var sourceComplete = await ReadResponseAsync(sourceReader, cancellationToken);
            var targetComplete = await ReadResponseAsync(targetReader, cancellationToken);
            
            var success = sourceComplete.StartsWith("226") && targetComplete.StartsWith("226");
            
            var result = new FxpTransferResult
            {
                Success = success,
                File = file,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                BytesTransferred = file.Size,
                ErrorMessage = success ? null : $"Source: {sourceComplete}, Target: {targetComplete}"
            };
            
            FileTransferred?.Invoke(this, result);
            
            return result;
        }
        catch (Exception ex)
        {
            return new FxpTransferResult
            {
                Success = false,
                File = file,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<IReadOnlyList<FxpTransferResult>> TransferFilesAsync(
        IEnumerable<FxpFileInfo> files,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FxpTransferResult>();
        var fileList = files as IList<FxpFileInfo> ?? new List<FxpFileInfo>(files);
        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;
        long transferredBytes = 0;
        
        foreach (var file in fileList)
        {
            totalBytes += file.Size;
        }
        
        for (int i = 0; i < fileList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var file = fileList[i];
            
            TransferProgress?.Invoke(this, new FxpTransferProgress
            {
                CurrentFile = file.SourcePath,
                FilesCompleted = i,
                TotalFiles = fileList.Count,
                CurrentFileBytes = 0,
                CurrentFileTotalBytes = file.Size,
                TotalBytesTransferred = transferredBytes,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                    ? transferredBytes * 1000 / stopwatch.ElapsedMilliseconds 
                    : 0
            });
            
            var result = await TransferFileAsync(file, direction, cancellationToken);
            results.Add(result);
            
            if (result.Success)
            {
                transferredBytes += file.Size;
            }
        }
        
        return results;
    }
    
    public async Task<IReadOnlyList<FxpTransferResult>> TransferDirectoryAsync(
        string sourcePath,
        string targetPath,
        FxpTransferDirection direction = FxpTransferDirection.SourceToTarget,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FxpTransferResult>();
        
        // List source directory
        var files = direction == FxpTransferDirection.SourceToTarget
            ? await ListSourceAsync(sourcePath, cancellationToken)
            : await ListTargetAsync(sourcePath, cancellationToken);
        
        // Create target directory
        if (direction == FxpTransferDirection.SourceToTarget)
        {
            await CreateTargetDirectoryAsync(targetPath, cancellationToken);
        }
        else
        {
            await CreateSourceDirectoryAsync(targetPath, cancellationToken);
        }
        
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileName = Path.GetFileName(file.SourcePath);
            var newTargetPath = Path.Combine(targetPath, fileName).Replace('\\', '/');
            
            if (file.IsDirectory)
            {
                var subResults = await TransferDirectoryAsync(
                    file.SourcePath,
                    newTargetPath,
                    direction,
                    cancellationToken);
                results.AddRange(subResults);
            }
            else
            {
                var transferFile = file with { TargetPath = newTargetPath };
                var result = await TransferFileAsync(transferFile, direction, cancellationToken);
                results.Add(result);
            }
        }
        
        return results;
    }
    
    public async Task<bool> CreateSourceDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_sourceWriter == null || _sourceReader == null)
            return false;
        
        await SendCommandAsync(_sourceWriter, $"MKD {path}", cancellationToken);
        var response = await ReadResponseAsync(_sourceReader, cancellationToken);
        
        return response.StartsWith("257") || response.StartsWith("250");
    }
    
    public async Task<bool> CreateTargetDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_targetWriter == null || _targetReader == null)
            return false;
        
        await SendCommandAsync(_targetWriter, $"MKD {path}", cancellationToken);
        var response = await ReadResponseAsync(_targetReader, cancellationToken);
        
        return response.StartsWith("257") || response.StartsWith("250");
    }
    
    private static async Task SendCommandAsync(
        StreamWriter writer,
        string command,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
    }
    
    private static async Task<string> ReadResponseAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        string? line;
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            response.AppendLine(line);
            
            // Multi-line responses have a dash after code, single line has space
            if (line.Length >= 4 && char.IsDigit(line[0]) && line[3] == ' ')
                break;
        }
        
        return response.ToString();
    }
    
    private static string? ParsePasvResponse(string response)
    {
        // Parse: 227 Entering Passive Mode (h1,h2,h3,h4,p1,p2)
        var start = response.IndexOf('(');
        var end = response.IndexOf(')');
        
        if (start < 0 || end < 0 || end <= start)
            return null;
        
        return response.Substring(start + 1, end - start - 1);
    }
    
    private static (string? host, int port) ParsePasvAddress(string response)
    {
        var data = ParsePasvResponse(response);
        if (data == null)
            return (null, 0);
        
        var parts = data.Split(',');
        if (parts.Length != 6)
            return (null, 0);
        
        var host = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}";
        var port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);
        
        return (host, port);
    }
    
    private static FxpFileInfo? ParseListLine(string line, string path)
    {
        // Simple Unix-style listing parser
        // drwxr-xr-x   2 user group    4096 Jan 01 12:00 dirname
        // -rw-r--r--   1 user group   12345 Jan 01 12:00 filename
        
        if (string.IsNullOrWhiteSpace(line))
            return null;
        
        var isDirectory = line.StartsWith('d');
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 9)
            return null;
        
        var name = string.Join(' ', parts[8..]);
        if (name == "." || name == "..")
            return null;
        
        long.TryParse(parts[4], out var size);
        
        var fullPath = path.EndsWith('/') 
            ? path + name 
            : path + "/" + name;
        
        return new FxpFileInfo
        {
            SourcePath = fullPath,
            TargetPath = name,
            Size = size,
            IsDirectory = isDirectory
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _ = DisconnectSourceAsync();
        _ = DisconnectTargetAsync();
    }
}
