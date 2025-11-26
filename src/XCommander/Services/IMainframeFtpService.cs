using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Mainframe record format
/// </summary>
public enum MainframeRecordFormat
{
    Fixed,
    Variable,
    FixedBlocked,
    VariableBlocked,
    Undefined
}

/// <summary>
/// Mainframe data set type
/// </summary>
public enum MainframeDataSetType
{
    Sequential,
    Partitioned,
    Vsam,
    Unix,
    Unknown
}

/// <summary>
/// Mainframe dataset information
/// </summary>
public record MainframeDataSetInfo
{
    /// <summary>Dataset name</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Dataset type</summary>
    public MainframeDataSetType Type { get; init; }
    
    /// <summary>Volume serial</summary>
    public string Volume { get; init; } = string.Empty;
    
    /// <summary>Record format</summary>
    public MainframeRecordFormat RecordFormat { get; init; }
    
    /// <summary>Logical record length</summary>
    public int RecordLength { get; init; }
    
    /// <summary>Block size</summary>
    public int BlockSize { get; init; }
    
    /// <summary>Space used (tracks or cylinders)</summary>
    public int SpaceUsed { get; init; }
    
    /// <summary>Space units (TRK, CYL)</summary>
    public string SpaceUnits { get; init; } = "TRK";
    
    /// <summary>Created date</summary>
    public DateTime? Created { get; init; }
    
    /// <summary>Last referenced date</summary>
    public DateTime? LastReferenced { get; init; }
    
    /// <summary>Is it a PDS member</summary>
    public bool IsMember { get; init; }
    
    /// <summary>Parent PDS name (if member)</summary>
    public string? ParentPds { get; init; }
}

/// <summary>
/// Mainframe connection info
/// </summary>
public record MainframeConnection
{
    /// <summary>Host name or IP</summary>
    public string Host { get; init; } = string.Empty;
    
    /// <summary>FTP port (typically 21)</summary>
    public int Port { get; init; } = 21;
    
    /// <summary>User ID</summary>
    public string UserId { get; init; } = string.Empty;
    
    /// <summary>Password</summary>
    public string Password { get; init; } = string.Empty;
    
    /// <summary>Account info (optional)</summary>
    public string? Account { get; init; }
    
    /// <summary>Use SSL/TLS</summary>
    public bool UseSsl { get; init; }
    
    /// <summary>Site commands to execute after login</summary>
    public IReadOnlyList<string>? SiteCommands { get; init; }
}

/// <summary>
/// Mainframe transfer options
/// </summary>
public record MainframeTransferOptions
{
    /// <summary>Transfer mode (ASCII, BINARY, EBCDIC)</summary>
    public string TransferMode { get; init; } = "BINARY";
    
    /// <summary>Convert from EBCDIC to ASCII</summary>
    public bool ConvertEbcdic { get; init; } = true;
    
    /// <summary>Dataset allocation parameters for new datasets</summary>
    public MainframeAllocationParams? AllocationParams { get; init; }
    
    /// <summary>Replace existing dataset</summary>
    public bool Replace { get; init; }
}

/// <summary>
/// Dataset allocation parameters
/// </summary>
public record MainframeAllocationParams
{
    public MainframeRecordFormat RecordFormat { get; init; } = MainframeRecordFormat.FixedBlocked;
    public int RecordLength { get; init; } = 80;
    public int BlockSize { get; init; } = 27920;
    public int PrimarySpace { get; init; } = 5;
    public int SecondarySpace { get; init; } = 5;
    public string SpaceUnits { get; init; } = "TRK";
    public int DirectoryBlocks { get; init; } = 10;
}

/// <summary>
/// Mainframe transfer result
/// </summary>
public record MainframeTransferResult
{
    public bool Success { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public long BytesTransferred { get; init; }
    public int RecordsTransferred { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Service for mainframe FTP operations with z/OS and EBCDIC support.
/// TC equivalent: Mainframe file transfer via FTP with EBCDIC conversion.
/// </summary>
public interface IMainframeFtpService
{
    /// <summary>
    /// Connect to mainframe
    /// </summary>
    Task<bool> ConnectAsync(
        MainframeConnection connection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from mainframe
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// List datasets matching pattern
    /// </summary>
    Task<IReadOnlyList<MainframeDataSetInfo>> ListDatasetsAsync(
        string pattern,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List members of a PDS
    /// </summary>
    Task<IReadOnlyList<MainframeDataSetInfo>> ListMembersAsync(
        string pdsName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download dataset to local file
    /// </summary>
    Task<MainframeTransferResult> DownloadDatasetAsync(
        string datasetName,
        string localPath,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download PDS member to local file
    /// </summary>
    Task<MainframeTransferResult> DownloadMemberAsync(
        string pdsName,
        string memberName,
        string localPath,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Upload local file to dataset
    /// </summary>
    Task<MainframeTransferResult> UploadDatasetAsync(
        string localPath,
        string datasetName,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Upload local file to PDS member
    /// </summary>
    Task<MainframeTransferResult> UploadMemberAsync(
        string localPath,
        string pdsName,
        string memberName,
        MainframeTransferOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Submit JCL job
    /// </summary>
    Task<string?> SubmitJobAsync(
        string jclPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get job status
    /// </summary>
    Task<(string Status, int? ReturnCode)> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get job output
    /// </summary>
    Task<string?> GetJobOutputAsync(
        string jobId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete dataset
    /// </summary>
    Task<bool> DeleteDatasetAsync(
        string datasetName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete PDS member
    /// </summary>
    Task<bool> DeleteMemberAsync(
        string pdsName,
        string memberName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rename dataset
    /// </summary>
    Task<bool> RenameDatasetAsync(
        string oldName,
        string newName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Allocate new dataset
    /// </summary>
    Task<bool> AllocateDatasetAsync(
        string datasetName,
        MainframeAllocationParams allocationParams,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute SITE command
    /// </summary>
    Task<(bool Success, string Response)> ExecuteSiteCommandAsync(
        string command,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert EBCDIC to ASCII
    /// </summary>
    byte[] ConvertEbcdicToAscii(byte[] ebcdicData);
    
    /// <summary>
    /// Convert ASCII to EBCDIC
    /// </summary>
    byte[] ConvertAsciiToEbcdic(byte[] asciiData);
    
    /// <summary>
    /// Is connected
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Current connection info
    /// </summary>
    MainframeConnection? CurrentConnection { get; }
    
    /// <summary>
    /// Event raised when transfer progress updates
    /// </summary>
    event EventHandler<(string File, long BytesTransferred, long TotalBytes)>? TransferProgress;
}
