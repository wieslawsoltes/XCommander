using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XCommander.Services;

/// <summary>
/// Implementation of system tools service for process, service, and environment management
/// </summary>
public class SystemToolsService : ISystemToolsService
{
    private DateTime _lastRefresh = DateTime.MinValue;
    private double _lastCpuUsage;
    private Process? _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    
    public SystemToolsService()
    {
        _currentProcess = Process.GetCurrentProcess();
    }
    
    #region Process Management
    
    public async Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var processes = Process.GetProcesses();
            var result = new List<ProcessInfo>();
            
            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    result.Add(CreateProcessInfo(process));
                }
                catch
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }
            
            return result.OrderBy(p => p.Name).ToList();
        }, cancellationToken);
    }
    
    public ProcessInfo? GetProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return CreateProcessInfo(process);
        }
        catch
        {
            return null;
        }
    }
    
    public bool KillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public bool SetProcessPriority(int processId, ProcessPriorityClass priority)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.PriorityClass = priority;
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public int? StartProcess(string fileName, string? arguments = null, string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = true
            };
            
            var process = Process.Start(startInfo);
            return process?.Id;
        }
        catch
        {
            return null;
        }
    }
    
    private static ProcessInfo CreateProcessInfo(Process process)
    {
        string? filePath = null;
        DateTime startTime = default;
        TimeSpan cpuTime = default;
        string? windowTitle = null;
        bool responding = true;
        ProcessPriorityClass priority = ProcessPriorityClass.Normal;
        
        try { filePath = process.MainModule?.FileName; } catch { }
        try { startTime = process.StartTime; } catch { }
        try { cpuTime = process.TotalProcessorTime; } catch { }
        try { windowTitle = process.MainWindowTitle; } catch { }
        try { responding = process.Responding; } catch { }
        try { priority = process.PriorityClass; } catch { }
        
        return new ProcessInfo
        {
            Id = process.Id,
            Name = process.ProcessName,
            FilePath = filePath,
            MemoryUsage = process.WorkingSet64,
            CpuTime = cpuTime,
            StartTime = startTime,
            ThreadCount = process.Threads.Count,
            Priority = priority,
            MainWindowTitle = string.IsNullOrEmpty(windowTitle) ? null : windowTitle,
            Responding = responding
        };
    }
    
    #endregion
    
    #region Service Management
    
    public async Task<IReadOnlyList<ServiceInfo>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var services = new List<ServiceInfo>();
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On non-Windows, try to list systemd services
                return GetLinuxServicesInfo();
            }
            
            // On Windows, use sc query command
            return GetWindowsServicesInfo();
        }, cancellationToken);
    }
    
    private IReadOnlyList<ServiceInfo> GetWindowsServicesInfo()
    {
        var services = new List<ServiceInfo>();
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = "query state= all",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) return services;
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            var currentService = new ServiceInfo();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (trimmed.StartsWith("SERVICE_NAME:"))
                {
                    if (!string.IsNullOrEmpty(currentService.Name))
                    {
                        services.Add(currentService);
                    }
                    currentService = new ServiceInfo
                    {
                        Name = trimmed.Substring("SERVICE_NAME:".Length).Trim()
                    };
                }
                else if (trimmed.StartsWith("DISPLAY_NAME:"))
                {
                    currentService = currentService with
                    {
                        DisplayName = trimmed.Substring("DISPLAY_NAME:".Length).Trim()
                    };
                }
                else if (trimmed.StartsWith("STATE"))
                {
                    currentService = currentService with
                    {
                        Status = ParseServiceState(trimmed)
                    };
                }
            }
            
            if (!string.IsNullOrEmpty(currentService.Name))
            {
                services.Add(currentService);
            }
        }
        catch
        {
            // Silently fail if we can't query services
        }
        
        return services.OrderBy(s => s.DisplayName).ToList();
    }
    
    private static ServiceStatus ParseServiceState(string stateLine)
    {
        if (stateLine.Contains("RUNNING")) return ServiceStatus.Running;
        if (stateLine.Contains("STOPPED")) return ServiceStatus.Stopped;
        if (stateLine.Contains("PAUSED")) return ServiceStatus.Paused;
        if (stateLine.Contains("START_PENDING")) return ServiceStatus.StartPending;
        if (stateLine.Contains("STOP_PENDING")) return ServiceStatus.StopPending;
        if (stateLine.Contains("PAUSE_PENDING")) return ServiceStatus.PausePending;
        if (stateLine.Contains("CONTINUE_PENDING")) return ServiceStatus.ContinuePending;
        return ServiceStatus.Unknown;
    }
    
    private static IReadOnlyList<ServiceInfo> GetLinuxServicesInfo()
    {
        var services = new List<ServiceInfo>();
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "list-units --type=service --all --no-pager",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) return services;
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var headerPassed = false;
            
            foreach (var line in lines)
            {
                if (line.Contains("UNIT") && line.Contains("LOAD"))
                {
                    headerPassed = true;
                    continue;
                }
                
                if (!headerPassed || line.StartsWith("LOAD") || string.IsNullOrWhiteSpace(line))
                    continue;
                
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                
                var name = parts[0].Replace(".service", "");
                var active = parts[2];
                var status = active switch
                {
                    "active" => ServiceStatus.Running,
                    "inactive" => ServiceStatus.Stopped,
                    "failed" => ServiceStatus.Stopped,
                    _ => ServiceStatus.Unknown
                };
                
                var description = parts.Length > 4 
                    ? string.Join(" ", parts.Skip(4)) 
                    : name;
                
                services.Add(new ServiceInfo
                {
                    Name = name,
                    DisplayName = name,
                    Status = status,
                    Description = description
                });
            }
        }
        catch
        {
            // Silently fail
        }
        
        return services;
    }
    
    public ServiceInfo? GetService(string serviceName)
    {
        var services = GetWindowsServicesInfo();
        return services.FirstOrDefault(s => 
            s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task<bool> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        return await ExecuteServiceCommandAsync("start", serviceName, cancellationToken);
    }
    
    public async Task<bool> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        return await ExecuteServiceCommandAsync("stop", serviceName, cancellationToken);
    }
    
    public async Task<bool> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var stopped = await StopServiceAsync(serviceName, cancellationToken);
        if (!stopped) return false;
        
        await Task.Delay(1000, cancellationToken); // Give the service time to stop
        
        return await StartServiceAsync(serviceName, cancellationToken);
    }
    
    private static async Task<bool> ExecuteServiceCommandAsync(string command, string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            string fileName;
            string arguments;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = "sc";
                arguments = $"{command} \"{serviceName}\"";
            }
            else
            {
                fileName = "systemctl";
                arguments = $"{command} {serviceName}";
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    #endregion
    
    #region Environment Variables
    
    public IReadOnlyList<EnvironmentVariableInfo> GetEnvironmentVariables(EnvironmentVariableScope scope = EnvironmentVariableScope.Process)
    {
        var target = scope switch
        {
            EnvironmentVariableScope.User => EnvironmentVariableTarget.User,
            EnvironmentVariableScope.Machine => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.Process
        };
        
        var variables = new List<EnvironmentVariableInfo>();
        
        try
        {
            var envVars = Environment.GetEnvironmentVariables(target);
            foreach (var key in envVars.Keys)
            {
                variables.Add(new EnvironmentVariableInfo
                {
                    Name = key?.ToString() ?? string.Empty,
                    Value = envVars[key]?.ToString(),
                    Scope = scope
                });
            }
        }
        catch
        {
            // May not have access to machine-level variables
        }
        
        return variables.OrderBy(v => v.Name).ToList();
    }
    
    public string? GetEnvironmentVariable(string name, EnvironmentVariableScope scope = EnvironmentVariableScope.Process)
    {
        var target = scope switch
        {
            EnvironmentVariableScope.User => EnvironmentVariableTarget.User,
            EnvironmentVariableScope.Machine => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.Process
        };
        
        return Environment.GetEnvironmentVariable(name, target);
    }
    
    public bool SetEnvironmentVariable(string name, string? value, EnvironmentVariableScope scope = EnvironmentVariableScope.Process)
    {
        try
        {
            var target = scope switch
            {
                EnvironmentVariableScope.User => EnvironmentVariableTarget.User,
                EnvironmentVariableScope.Machine => EnvironmentVariableTarget.Machine,
                _ => EnvironmentVariableTarget.Process
            };
            
            Environment.SetEnvironmentVariable(name, value, target);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public bool DeleteEnvironmentVariable(string name, EnvironmentVariableScope scope = EnvironmentVariableScope.Process)
    {
        return SetEnvironmentVariable(name, null, scope);
    }
    
    #endregion
    
    #region System Information
    
    public SystemResourceInfo GetSystemResourceInfo()
    {
        var gcMemInfo = GC.GetGCMemoryInfo();
        
        // Estimate CPU usage from our own process as fallback
        double cpuUsage = 0;
        try
        {
            if (_currentProcess != null && _lastCpuCheck != DateTime.MinValue)
            {
                var elapsed = (DateTime.Now - _lastCpuCheck).TotalMilliseconds;
                var cpuTime = _currentProcess.TotalProcessorTime;
                var cpuUsedMs = (cpuTime - _lastCpuTime).TotalMilliseconds;
                cpuUsage = (cpuUsedMs / (elapsed * Environment.ProcessorCount)) * 100;
                _lastCpuTime = cpuTime;
            }
            else if (_currentProcess != null)
            {
                _lastCpuTime = _currentProcess.TotalProcessorTime;
            }
            _lastCpuCheck = DateTime.Now;
            _lastCpuUsage = Math.Max(0, Math.Min(100, cpuUsage));
        }
        catch
        {
            cpuUsage = _lastCpuUsage;
        }
        
        return new SystemResourceInfo
        {
            TotalPhysicalMemory = gcMemInfo.TotalAvailableMemoryBytes,
            AvailablePhysicalMemory = gcMemInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false),
            ProcessorCount = Environment.ProcessorCount,
            CpuUsagePercent = cpuUsage,
            SystemUptime = GetSystemUptime(),
            OsVersion = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName
        };
    }
    
    public TimeSpan GetSystemUptime()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount64);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read from /proc/uptime
                var uptime = File.ReadAllText("/proc/uptime");
                var seconds = double.Parse(uptime.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
                return TimeSpan.FromSeconds(seconds);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use sysctl on macOS
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n kern.boottime",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(startInfo);
                if (process == null) return TimeSpan.Zero;
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                // Parse boot time and calculate uptime
                // Format: { sec = 1234567890, usec = 0 }
                var secMatch = System.Text.RegularExpressions.Regex.Match(output, @"sec\s*=\s*(\d+)");
                if (secMatch.Success)
                {
                    var bootTimestamp = long.Parse(secMatch.Groups[1].Value);
                    var bootTime = DateTimeOffset.FromUnixTimeSeconds(bootTimestamp);
                    return DateTimeOffset.UtcNow - bootTime;
                }
            }
        }
        catch
        {
            // Fall back to .NET tick count
        }
        
        return TimeSpan.FromMilliseconds(Environment.TickCount64);
    }
    
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _lastRefresh = DateTime.Now;
        return Task.CompletedTask;
    }
    
    #endregion
}
