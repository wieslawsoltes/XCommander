using System.Diagnostics;

namespace XCommander.Services;

/// <summary>
/// Information about a running process
/// </summary>
public class ProcessInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? FilePath { get; init; }
    public long MemoryUsage { get; init; }
    public TimeSpan CpuTime { get; init; }
    public DateTime StartTime { get; init; }
    public int ThreadCount { get; init; }
    public ProcessPriorityClass Priority { get; init; }
    public string? MainWindowTitle { get; init; }
    public bool Responding { get; init; }
    public string? UserName { get; init; }
}

/// <summary>
/// Information about a system service (Windows-specific)
/// </summary>
public record ServiceInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ServiceStatus Status { get; init; }
    public ServiceStartType StartType { get; init; }
    public string? Description { get; init; }
    public string? ServiceAccount { get; init; }
    public string? Path { get; init; }
}

public enum ServiceStatus
{
    Unknown,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}

public enum ServiceStartType
{
    Automatic,
    Manual,
    Disabled,
    AutomaticDelayed,
    Unknown
}

/// <summary>
/// Environment variable scope
/// </summary>
public enum EnvironmentVariableScope
{
    Process,
    User,
    Machine
}

/// <summary>
/// Environment variable information
/// </summary>
public class EnvironmentVariableInfo
{
    public string Name { get; init; } = string.Empty;
    public string? Value { get; init; }
    public EnvironmentVariableScope Scope { get; init; }
}

/// <summary>
/// System resource information
/// </summary>
public class SystemResourceInfo
{
    public long TotalPhysicalMemory { get; init; }
    public long AvailablePhysicalMemory { get; init; }
    public long UsedPhysicalMemory => TotalPhysicalMemory - AvailablePhysicalMemory;
    public double MemoryUsagePercent => TotalPhysicalMemory > 0 
        ? (double)UsedPhysicalMemory / TotalPhysicalMemory * 100 : 0;
    
    public int ProcessorCount { get; init; }
    public double CpuUsagePercent { get; init; }
    
    public long TotalVirtualMemory { get; init; }
    public long AvailableVirtualMemory { get; init; }
    
    public TimeSpan SystemUptime { get; init; }
    public string OsVersion { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
}

/// <summary>
/// Service for system tools functionality
/// </summary>
public interface ISystemToolsService
{
    // Process management
    
    /// <summary>
    /// Get all running processes
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get process by ID
    /// </summary>
    ProcessInfo? GetProcess(int processId);
    
    /// <summary>
    /// Kill a process
    /// </summary>
    bool KillProcess(int processId);
    
    /// <summary>
    /// Set process priority
    /// </summary>
    bool SetProcessPriority(int processId, ProcessPriorityClass priority);
    
    /// <summary>
    /// Start a new process
    /// </summary>
    int? StartProcess(string fileName, string? arguments = null, string? workingDirectory = null);
    
    // Service management (Windows)
    
    /// <summary>
    /// Get all services
    /// </summary>
    Task<IReadOnlyList<ServiceInfo>> GetServicesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service by name
    /// </summary>
    ServiceInfo? GetService(string serviceName);
    
    /// <summary>
    /// Start a service
    /// </summary>
    Task<bool> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop a service
    /// </summary>
    Task<bool> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restart a service
    /// </summary>
    Task<bool> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    
    // Environment variables
    
    /// <summary>
    /// Get all environment variables
    /// </summary>
    IReadOnlyList<EnvironmentVariableInfo> GetEnvironmentVariables(EnvironmentVariableScope scope = EnvironmentVariableScope.Process);
    
    /// <summary>
    /// Get specific environment variable
    /// </summary>
    string? GetEnvironmentVariable(string name, EnvironmentVariableScope scope = EnvironmentVariableScope.Process);
    
    /// <summary>
    /// Set environment variable
    /// </summary>
    bool SetEnvironmentVariable(string name, string? value, EnvironmentVariableScope scope = EnvironmentVariableScope.Process);
    
    /// <summary>
    /// Delete environment variable
    /// </summary>
    bool DeleteEnvironmentVariable(string name, EnvironmentVariableScope scope = EnvironmentVariableScope.Process);
    
    // System information
    
    /// <summary>
    /// Get system resource information
    /// </summary>
    SystemResourceInfo GetSystemResourceInfo();
    
    /// <summary>
    /// Get system uptime
    /// </summary>
    TimeSpan GetSystemUptime();
    
    /// <summary>
    /// Refresh system information
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
