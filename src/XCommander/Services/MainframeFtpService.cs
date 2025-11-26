using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Implementation of mainframe FTP with z/OS and EBCDIC support.
/// </summary>
public sealed class MainframeFtpService : IMainframeFtpService, IDisposable
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private MainframeConnection? _connection;
    private bool _disposed;
    
    // IBM EBCDIC US-Canada code page 037
    private static readonly Encoding EbcdicEncoding = Encoding.GetEncoding(37);
    
    public event EventHandler<(string File, long BytesTransferred, long TotalBytes)>? TransferProgress;
    
    public bool IsConnected => _client?.Connected ?? false;
    public MainframeConnection? CurrentConnection => _connection;
    
    public async Task<bool> ConnectAsync(
        MainframeConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(connection.Host, connection.Port, cancellationToken);
            
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            
            // Read welcome message
            await ReadResponseAsync(cancellationToken);
            
            // Login
            await SendCommandAsync($"USER {connection.UserId}", cancellationToken);
            var userResponse = await ReadResponseAsync(cancellationToken);
            
            if (userResponse.StartsWith("331"))
            {
                await SendCommandAsync($"PASS {connection.Password}", cancellationToken);
                var passResponse = await ReadResponseAsync(cancellationToken);
                
                if (!passResponse.StartsWith("230"))
                    return false;
            }
            else if (!userResponse.StartsWith("230"))
            {
                return false;
            }
            
            // Execute site commands if specified
            if (connection.SiteCommands != null)
            {
                foreach (var siteCmd in connection.SiteCommands)
                {
                    await ExecuteSiteCommandAsync(siteCmd, cancellationToken);
                }
            }
            
            _connection = connection;
            return true;
        }
        catch
        {
            await DisconnectAsync();
            return false;
        }
    }
    
    public Task DisconnectAsync()
    {
        try
        {
            if (_writer != null && _client?.Connected == true)
            {
                _writer.WriteLine("QUIT");
            }
        }
        catch { }
        
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        
        _reader = null;
        _writer = null;
        _client = null;
        _connection = null;
        
        return Task.CompletedTask;
    }
    
    public async Task<IReadOnlyList<MainframeDataSetInfo>> ListDatasetsAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MainframeDataSetInfo>();
        
        if (!IsConnected)
            return result;
        
        // Enter passive mode
        await SendCommandAsync("PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return result;
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return result;
        
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        // Set dataset listing mode
        await SendCommandAsync("SITE FILETYPE=SEQ", cancellationToken);
        await ReadResponseAsync(cancellationToken);
        
        // List datasets
        await SendCommandAsync($"LIST '{pattern}'", cancellationToken);
        var listResponse = await ReadResponseAsync(cancellationToken);
        
        if (!listResponse.StartsWith("150") && !listResponse.StartsWith("125"))
            return result;
        
        using var dataReader = new StreamReader(dataClient.GetStream());
        while (await dataReader.ReadLineAsync() is { } line)
        {
            var dsInfo = ParseDatasetLine(line);
            if (dsInfo != null)
            {
                result.Add(dsInfo);
            }
        }
        
        await ReadResponseAsync(cancellationToken);
        
        return result;
    }
    
    public async Task<IReadOnlyList<MainframeDataSetInfo>> ListMembersAsync(
        string pdsName,
        CancellationToken cancellationToken = default)
    {
        var result = new List<MainframeDataSetInfo>();
        
        if (!IsConnected)
            return result;
        
        // Enter passive mode
        await SendCommandAsync("PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return result;
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return result;
        
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        // List PDS members
        await SendCommandAsync($"LIST '{pdsName}'", cancellationToken);
        var listResponse = await ReadResponseAsync(cancellationToken);
        
        if (!listResponse.StartsWith("150") && !listResponse.StartsWith("125"))
            return result;
        
        using var dataReader = new StreamReader(dataClient.GetStream());
        while (await dataReader.ReadLineAsync() is { } line)
        {
            var memberInfo = ParseMemberLine(line, pdsName);
            if (memberInfo != null)
            {
                result.Add(memberInfo);
            }
        }
        
        await ReadResponseAsync(cancellationToken);
        
        return result;
    }
    
    public async Task<MainframeTransferResult> DownloadDatasetAsync(
        string datasetName,
        string localPath,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MainframeTransferOptions();
        
        if (!IsConnected)
        {
            return new MainframeTransferResult
            {
                Success = false,
                SourcePath = datasetName,
                DestinationPath = localPath,
                ErrorMessage = "Not connected"
            };
        }
        
        try
        {
            // Set transfer mode
            await SendCommandAsync($"TYPE {(options.TransferMode == "BINARY" ? "I" : "A")}", cancellationToken);
            await ReadResponseAsync(cancellationToken);
            
            // Enter passive mode
            await SendCommandAsync("PASV", cancellationToken);
            var pasvResponse = await ReadResponseAsync(cancellationToken);
            
            if (!pasvResponse.StartsWith("227"))
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = datasetName,
                    DestinationPath = localPath,
                    ErrorMessage = "PASV mode failed"
                };
            }
            
            var (host, port) = ParsePasvAddress(pasvResponse);
            if (host == null)
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = datasetName,
                    DestinationPath = localPath,
                    ErrorMessage = "Failed to parse PASV response"
                };
            }
            
            using var dataClient = new TcpClient();
            await dataClient.ConnectAsync(host, port, cancellationToken);
            
            await SendCommandAsync($"RETR '{datasetName}'", cancellationToken);
            var retrResponse = await ReadResponseAsync(cancellationToken);
            
            if (!retrResponse.StartsWith("150") && !retrResponse.StartsWith("125"))
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = datasetName,
                    DestinationPath = localPath,
                    ErrorMessage = retrResponse
                };
            }
            
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            long bytesTransferred = 0;
            using (var dataStream = dataClient.GetStream())
            using (var fileStream = File.Create(localPath))
            {
                var buffer = new byte[65536];
                int bytesRead;
                
                while ((bytesRead = await dataStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    byte[] dataToWrite;
                    
                    if (options.ConvertEbcdic && options.TransferMode != "BINARY")
                    {
                        var ebcdicData = new byte[bytesRead];
                        Array.Copy(buffer, ebcdicData, bytesRead);
                        dataToWrite = ConvertEbcdicToAscii(ebcdicData);
                    }
                    else
                    {
                        dataToWrite = new byte[bytesRead];
                        Array.Copy(buffer, dataToWrite, bytesRead);
                    }
                    
                    await fileStream.WriteAsync(dataToWrite, 0, dataToWrite.Length, cancellationToken);
                    bytesTransferred += bytesRead;
                    
                    TransferProgress?.Invoke(this, (datasetName, bytesTransferred, -1));
                }
            }
            
            await ReadResponseAsync(cancellationToken);
            
            return new MainframeTransferResult
            {
                Success = true,
                SourcePath = datasetName,
                DestinationPath = localPath,
                BytesTransferred = bytesTransferred
            };
        }
        catch (Exception ex)
        {
            return new MainframeTransferResult
            {
                Success = false,
                SourcePath = datasetName,
                DestinationPath = localPath,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<MainframeTransferResult> DownloadMemberAsync(
        string pdsName,
        string memberName,
        string localPath,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var fullName = $"{pdsName}({memberName})";
        return await DownloadDatasetAsync(fullName, localPath, options, cancellationToken);
    }
    
    public async Task<MainframeTransferResult> UploadDatasetAsync(
        string localPath,
        string datasetName,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MainframeTransferOptions();
        
        if (!IsConnected)
        {
            return new MainframeTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = datasetName,
                ErrorMessage = "Not connected"
            };
        }
        
        if (!File.Exists(localPath))
        {
            return new MainframeTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = datasetName,
                ErrorMessage = "Local file not found"
            };
        }
        
        try
        {
            // Set allocation parameters if creating new dataset
            if (options.AllocationParams != null)
            {
                var allocParams = options.AllocationParams;
                var recfm = allocParams.RecordFormat switch
                {
                    MainframeRecordFormat.Fixed => "F",
                    MainframeRecordFormat.Variable => "V",
                    MainframeRecordFormat.FixedBlocked => "FB",
                    MainframeRecordFormat.VariableBlocked => "VB",
                    _ => "FB"
                };
                
                await ExecuteSiteCommandAsync(
                    $"RECFM={recfm} LRECL={allocParams.RecordLength} BLKSIZE={allocParams.BlockSize} " +
                    $"PRIMARY={allocParams.PrimarySpace} SECONDARY={allocParams.SecondarySpace} " +
                    $"{allocParams.SpaceUnits} DIRECTORY={allocParams.DirectoryBlocks}",
                    cancellationToken);
            }
            
            // Set transfer mode
            await SendCommandAsync($"TYPE {(options.TransferMode == "BINARY" ? "I" : "A")}", cancellationToken);
            await ReadResponseAsync(cancellationToken);
            
            // Enter passive mode
            await SendCommandAsync("PASV", cancellationToken);
            var pasvResponse = await ReadResponseAsync(cancellationToken);
            
            if (!pasvResponse.StartsWith("227"))
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = localPath,
                    DestinationPath = datasetName,
                    ErrorMessage = "PASV mode failed"
                };
            }
            
            var (host, port) = ParsePasvAddress(pasvResponse);
            if (host == null)
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = localPath,
                    DestinationPath = datasetName,
                    ErrorMessage = "Failed to parse PASV response"
                };
            }
            
            using var dataClient = new TcpClient();
            await dataClient.ConnectAsync(host, port, cancellationToken);
            
            await SendCommandAsync($"STOR '{datasetName}'", cancellationToken);
            var storResponse = await ReadResponseAsync(cancellationToken);
            
            if (!storResponse.StartsWith("150") && !storResponse.StartsWith("125"))
            {
                return new MainframeTransferResult
                {
                    Success = false,
                    SourcePath = localPath,
                    DestinationPath = datasetName,
                    ErrorMessage = storResponse
                };
            }
            
            var fileInfo = new FileInfo(localPath);
            long bytesTransferred = 0;
            
            using (var fileStream = File.OpenRead(localPath))
            using (var dataStream = dataClient.GetStream())
            {
                var buffer = new byte[65536];
                int bytesRead;
                
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    byte[] dataToSend;
                    
                    if (options.ConvertEbcdic && options.TransferMode != "BINARY")
                    {
                        var asciiData = new byte[bytesRead];
                        Array.Copy(buffer, asciiData, bytesRead);
                        dataToSend = ConvertAsciiToEbcdic(asciiData);
                    }
                    else
                    {
                        dataToSend = new byte[bytesRead];
                        Array.Copy(buffer, dataToSend, bytesRead);
                    }
                    
                    await dataStream.WriteAsync(dataToSend, 0, dataToSend.Length, cancellationToken);
                    bytesTransferred += bytesRead;
                    
                    TransferProgress?.Invoke(this, (localPath, bytesTransferred, fileInfo.Length));
                }
            }
            
            await ReadResponseAsync(cancellationToken);
            
            return new MainframeTransferResult
            {
                Success = true,
                SourcePath = localPath,
                DestinationPath = datasetName,
                BytesTransferred = bytesTransferred
            };
        }
        catch (Exception ex)
        {
            return new MainframeTransferResult
            {
                Success = false,
                SourcePath = localPath,
                DestinationPath = datasetName,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<MainframeTransferResult> UploadMemberAsync(
        string localPath,
        string pdsName,
        string memberName,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var fullName = $"{pdsName}({memberName})";
        return await UploadDatasetAsync(localPath, fullName, options, cancellationToken);
    }
    
    public async Task<string?> SubmitJobAsync(
        string jclPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return null;
        
        // Submit JCL for execution
        await ExecuteSiteCommandAsync("FILETYPE=JES", cancellationToken);
        
        // Upload JCL
        var result = await UploadDatasetAsync(jclPath, "JCL", new MainframeTransferOptions
        {
            TransferMode = "ASCII",
            ConvertEbcdic = true
        }, cancellationToken);
        
        if (!result.Success)
            return null;
        
        // The response should contain the job ID
        // Format: "JOB JOB12345 submitted"
        // This is simplified - actual implementation would parse the submit response
        
        return result.DestinationPath;
    }
    
    public async Task<(string Status, int? ReturnCode)> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return ("UNKNOWN", null);
        
        await ExecuteSiteCommandAsync("FILETYPE=JES", cancellationToken);
        await ExecuteSiteCommandAsync($"JESJOBNAME={jobId}", cancellationToken);
        
        await SendCommandAsync("PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return ("UNKNOWN", null);
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return ("UNKNOWN", null);
        
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        await SendCommandAsync("LIST", cancellationToken);
        await ReadResponseAsync(cancellationToken);
        
        using var dataReader = new StreamReader(dataClient.GetStream());
        var status = "UNKNOWN";
        int? returnCode = null;
        
        while (await dataReader.ReadLineAsync() is { } line)
        {
            // Parse job status from JES listing
            if (line.Contains(jobId))
            {
                if (line.Contains("OUTPUT"))
                    status = "COMPLETED";
                else if (line.Contains("ACTIVE"))
                    status = "ACTIVE";
                else if (line.Contains("INPUT"))
                    status = "QUEUED";
                
                // Try to extract return code
                var rcIndex = line.IndexOf("RC=", StringComparison.OrdinalIgnoreCase);
                if (rcIndex >= 0)
                {
                    var rcStr = line.Substring(rcIndex + 3);
                    var space = rcStr.IndexOf(' ');
                    if (space > 0)
                        rcStr = rcStr.Substring(0, space);
                    
                    if (int.TryParse(rcStr, out var rc))
                        returnCode = rc;
                }
            }
        }
        
        await ReadResponseAsync(cancellationToken);
        
        return (status, returnCode);
    }
    
    public async Task<string?> GetJobOutputAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return null;
        
        await ExecuteSiteCommandAsync("FILETYPE=JES", cancellationToken);
        await ExecuteSiteCommandAsync($"JESJOBNAME={jobId}", cancellationToken);
        
        await SendCommandAsync("PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return null;
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return null;
        
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        await SendCommandAsync($"RETR {jobId}", cancellationToken);
        var retrResponse = await ReadResponseAsync(cancellationToken);
        
        if (!retrResponse.StartsWith("150") && !retrResponse.StartsWith("125"))
            return null;
        
        using var dataReader = new StreamReader(dataClient.GetStream());
        var output = await dataReader.ReadToEndAsync();
        
        await ReadResponseAsync(cancellationToken);
        
        return output;
    }
    
    public async Task<bool> DeleteDatasetAsync(
        string datasetName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return false;
        
        await SendCommandAsync($"DELE '{datasetName}'", cancellationToken);
        var response = await ReadResponseAsync(cancellationToken);
        
        return response.StartsWith("250");
    }
    
    public async Task<bool> DeleteMemberAsync(
        string pdsName,
        string memberName,
        CancellationToken cancellationToken = default)
    {
        var fullName = $"{pdsName}({memberName})";
        return await DeleteDatasetAsync(fullName, cancellationToken);
    }
    
    public async Task<bool> RenameDatasetAsync(
        string oldName,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return false;
        
        await SendCommandAsync($"RNFR '{oldName}'", cancellationToken);
        var rnfrResponse = await ReadResponseAsync(cancellationToken);
        
        if (!rnfrResponse.StartsWith("350"))
            return false;
        
        await SendCommandAsync($"RNTO '{newName}'", cancellationToken);
        var rntoResponse = await ReadResponseAsync(cancellationToken);
        
        return rntoResponse.StartsWith("250");
    }
    
    public async Task<bool> AllocateDatasetAsync(
        string datasetName,
        MainframeAllocationParams allocationParams,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return false;
        
        var recfm = allocationParams.RecordFormat switch
        {
            MainframeRecordFormat.Fixed => "F",
            MainframeRecordFormat.Variable => "V",
            MainframeRecordFormat.FixedBlocked => "FB",
            MainframeRecordFormat.VariableBlocked => "VB",
            _ => "FB"
        };
        
        var (success, _) = await ExecuteSiteCommandAsync(
            $"RECFM={recfm} LRECL={allocationParams.RecordLength} BLKSIZE={allocationParams.BlockSize} " +
            $"PRIMARY={allocationParams.PrimarySpace} SECONDARY={allocationParams.SecondarySpace} " +
            $"{allocationParams.SpaceUnits} DIRECTORY={allocationParams.DirectoryBlocks}",
            cancellationToken);
        
        if (!success)
            return false;
        
        // Create empty dataset
        await SendCommandAsync("PASV", cancellationToken);
        var pasvResponse = await ReadResponseAsync(cancellationToken);
        
        if (!pasvResponse.StartsWith("227"))
            return false;
        
        var (host, port) = ParsePasvAddress(pasvResponse);
        if (host == null)
            return false;
        
        using var dataClient = new TcpClient();
        await dataClient.ConnectAsync(host, port, cancellationToken);
        
        await SendCommandAsync($"STOR '{datasetName}'", cancellationToken);
        var storResponse = await ReadResponseAsync(cancellationToken);
        
        if (!storResponse.StartsWith("150") && !storResponse.StartsWith("125"))
            return false;
        
        // Close data connection to create empty dataset
        dataClient.Close();
        
        await ReadResponseAsync(cancellationToken);
        
        return true;
    }
    
    public async Task<(bool Success, string Response)> ExecuteSiteCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return (false, "Not connected");
        
        await SendCommandAsync($"SITE {command}", cancellationToken);
        var response = await ReadResponseAsync(cancellationToken);
        
        return (response.StartsWith("200") || response.StartsWith("250"), response);
    }
    
    public byte[] ConvertEbcdicToAscii(byte[] ebcdicData)
    {
        var ebcdicString = EbcdicEncoding.GetString(ebcdicData);
        return Encoding.ASCII.GetBytes(ebcdicString);
    }
    
    public byte[] ConvertAsciiToEbcdic(byte[] asciiData)
    {
        var asciiString = Encoding.ASCII.GetString(asciiData);
        return EbcdicEncoding.GetBytes(asciiString);
    }
    
    private async Task SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (_writer == null)
            throw new InvalidOperationException("Not connected");
        
        await _writer.WriteLineAsync(command.AsMemory(), cancellationToken);
    }
    
    private async Task<string> ReadResponseAsync(CancellationToken cancellationToken)
    {
        if (_reader == null)
            throw new InvalidOperationException("Not connected");
        
        var response = new StringBuilder();
        string? line;
        
        while ((line = await _reader.ReadLineAsync()) != null)
        {
            response.AppendLine(line);
            
            if (line.Length >= 4 && char.IsDigit(line[0]) && line[3] == ' ')
                break;
        }
        
        return response.ToString();
    }
    
    private static (string? host, int port) ParsePasvAddress(string response)
    {
        var start = response.IndexOf('(');
        var end = response.IndexOf(')');
        
        if (start < 0 || end < 0 || end <= start)
            return (null, 0);
        
        var data = response.Substring(start + 1, end - start - 1);
        var parts = data.Split(',');
        
        if (parts.Length != 6)
            return (null, 0);
        
        var host = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}";
        var port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);
        
        return (host, port);
    }
    
    private static MainframeDataSetInfo? ParseDatasetLine(string line)
    {
        // Parse z/OS dataset listing format
        // Volume Referred Ext Used Recfm Lrecl BlkSz Dsorg Dsname
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 9)
            return null;
        
        var volume = parts[0];
        var referred = parts[1];
        var recfmStr = parts.Length > 4 ? parts[4] : "FB";
        int.TryParse(parts.Length > 5 ? parts[5] : "80", out var lrecl);
        int.TryParse(parts.Length > 6 ? parts[6] : "0", out var blksize);
        var dsorg = parts.Length > 7 ? parts[7] : "PS";
        var dsname = parts.Length > 8 ? parts[8] : "";
        
        var recfm = recfmStr switch
        {
            "F" => MainframeRecordFormat.Fixed,
            "V" => MainframeRecordFormat.Variable,
            "FB" => MainframeRecordFormat.FixedBlocked,
            "VB" => MainframeRecordFormat.VariableBlocked,
            _ => MainframeRecordFormat.Undefined
        };
        
        var dstype = dsorg switch
        {
            "PO" => MainframeDataSetType.Partitioned,
            "PS" => MainframeDataSetType.Sequential,
            "VS" => MainframeDataSetType.Vsam,
            _ => MainframeDataSetType.Unknown
        };
        
        DateTime? lastRef = null;
        if (DateTime.TryParseExact(referred, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var refDate))
        {
            lastRef = refDate;
        }
        
        return new MainframeDataSetInfo
        {
            Name = dsname,
            Type = dstype,
            Volume = volume,
            RecordFormat = recfm,
            RecordLength = lrecl,
            BlockSize = blksize,
            LastReferenced = lastRef
        };
    }
    
    private static MainframeDataSetInfo? ParseMemberLine(string line, string pdsName)
    {
        // Parse PDS member listing
        // Name VV.MM Created Changed Size Init Mod Id
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 1)
            return null;
        
        var memberName = parts[0];
        
        if (memberName.StartsWith("-") || memberName.ToUpperInvariant() == "NAME")
            return null; // Skip header lines
        
        return new MainframeDataSetInfo
        {
            Name = memberName,
            Type = MainframeDataSetType.Partitioned,
            IsMember = true,
            ParentPds = pdsName
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _ = DisconnectAsync();
    }
}
