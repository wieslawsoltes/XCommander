using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace XCommander.Services;

/// <summary>
/// Batch job service for recording, playback, and scheduling file operations.
/// </summary>
public class BatchJobService : IBatchJobService
{
    private static readonly string JobsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XCommander", "BatchJobs");
    
    private static readonly string SchedulesFile = Path.Combine(JobsDirectory, "schedules.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    private BatchJob? _currentRecording;
    private int _operationOrder;
    private readonly List<ScheduledJob> _scheduledJobs = new();
    private readonly IFileSystemService? _fileService;
    private readonly IArchiveService? _archiveService;
    
    public bool IsRecording => _currentRecording != null;
    
    public BatchJobService(IFileSystemService? fileService = null, IArchiveService? archiveService = null)
    {
        _fileService = fileService;
        _archiveService = archiveService;
        
        Directory.CreateDirectory(JobsDirectory);
        _ = LoadSchedulesAsync();
    }
    
    public void StartRecording(string? jobName = null)
    {
        if (_currentRecording != null)
        {
            throw new InvalidOperationException("Recording is already in progress");
        }
        
        _currentRecording = new BatchJob
        {
            Name = jobName ?? $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        _operationOrder = 0;
    }
    
    public BatchJob StopRecording()
    {
        if (_currentRecording == null)
        {
            throw new InvalidOperationException("No recording in progress");
        }
        
        var job = _currentRecording;
        _currentRecording = null;
        _operationOrder = 0;
        
        return job;
    }
    
    public void RecordOperation(BatchOperation operation)
    {
        if (_currentRecording == null)
        {
            throw new InvalidOperationException("No recording in progress");
        }
        
        operation.Order = _operationOrder++;
        _currentRecording.Operations.Add(operation);
    }
    
    public async Task<BatchJobResult> ExecuteAsync(BatchJob job, BatchExecutionOptions? options = null,
        IProgress<BatchJobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchExecutionOptions();
        var results = new List<BatchOperationResult>();
        var startedAt = DateTime.Now;
        var successful = 0;
        var failed = 0;
        var skipped = 0;
        
        // Merge variables
        var variables = new Dictionary<string, string>(job.Variables);
        if (options.VariableOverrides != null)
        {
            foreach (var (key, value) in options.VariableOverrides)
            {
                variables[key] = value;
            }
        }
        
        var sortedOperations = job.Operations.OrderBy(o => o.Order).ToList();
        var totalOperations = sortedOperations.Count;
        
        for (int i = 0; i < sortedOperations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var operation = sortedOperations[i];
            
            progress?.Report(new BatchJobProgress
            {
                CurrentOperation = i + 1,
                TotalOperations = totalOperations,
                CurrentOperationType = operation.Type,
                CurrentDescription = operation.Description ?? operation.Type.ToString()
            });
            
            // Check condition
            if (operation.Condition != null && !EvaluateCondition(operation.Condition, variables))
            {
                skipped++;
                results.Add(new BatchOperationResult
                {
                    OperationId = operation.Id,
                    Type = operation.Type,
                    Success = true,
                    Duration = TimeSpan.Zero
                });
                continue;
            }
            
            // Execute with retry
            var operationResult = await ExecuteOperationWithRetryAsync(operation, variables, 
                options.DryRun, cancellationToken);
            
            results.Add(operationResult);
            
            if (operationResult.Success)
            {
                successful++;
                
                // Store output in variables
                foreach (var (key, value) in operationResult.Output)
                {
                    variables[$"_{operation.Id}_{key}"] = value?.ToString() ?? string.Empty;
                }
            }
            else
            {
                failed++;
                
                if (job.Options.StopOnFirstError && !operation.ContinueOnError)
                {
                    // Skip remaining operations
                    skipped += sortedOperations.Count - i - 1;
                    break;
                }
            }
        }
        
        return new BatchJobResult
        {
            JobId = job.Id,
            Success = failed == 0,
            StartedAt = startedAt,
            CompletedAt = DateTime.Now,
            TotalOperations = totalOperations,
            SuccessfulOperations = successful,
            FailedOperations = failed,
            SkippedOperations = skipped,
            OperationResults = results
        };
    }
    
    public async Task SaveJobAsync(BatchJob job, string filePath, CancellationToken cancellationToken = default)
    {
        job.ModifiedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(job, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
    
    public async Task<BatchJob> LoadJobAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<BatchJob>(json, JsonOptions) 
               ?? throw new InvalidDataException("Invalid batch job file");
    }
    
    public async Task<IReadOnlyList<BatchJobInfo>> GetSavedJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = new List<BatchJobInfo>();
        
        await Task.Run(() =>
        {
            if (!Directory.Exists(JobsDirectory)) return;
            
            foreach (var file in Directory.EnumerateFiles(JobsDirectory, "*.json"))
            {
                if (file.EndsWith("schedules.json", StringComparison.OrdinalIgnoreCase)) continue;
                
                try
                {
                    var json = File.ReadAllText(file);
                    var job = JsonSerializer.Deserialize<BatchJob>(json, JsonOptions);
                    
                    if (job != null)
                    {
                        jobs.Add(new BatchJobInfo
                        {
                            FilePath = file,
                            Name = job.Name,
                            Description = job.Description,
                            CreatedAt = job.CreatedAt,
                            OperationCount = job.Operations.Count
                        });
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }
        }, cancellationToken);
        
        return jobs.OrderByDescending(j => j.CreatedAt).ToList();
    }
    
    public async Task<BatchJobValidation> ValidateAsync(BatchJob job, CancellationToken cancellationToken = default)
    {
        var errors = new List<BatchValidationError>();
        var warnings = new List<BatchValidationWarning>();
        
        await Task.Run(() =>
        {
            for (int i = 0; i < job.Operations.Count; i++)
            {
                var op = job.Operations[i];
                
                // Validate required parameters
                var requiredParams = GetRequiredParameters(op.Type);
                foreach (var param in requiredParams)
                {
                    if (!op.Parameters.ContainsKey(param))
                    {
                        errors.Add(new BatchValidationError
                        {
                            OperationIndex = i,
                            OperationId = op.Id,
                            Message = $"Missing required parameter: {param}"
                        });
                    }
                }
                
                // Validate file paths exist (for non-create operations)
                if (op.Parameters.TryGetValue("source", out var sourcePath))
                {
                    if (!sourcePath.StartsWith("$") && 
                        !File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                    {
                        warnings.Add(new BatchValidationWarning
                        {
                            OperationIndex = i,
                            OperationId = op.Id,
                            Message = $"Source path may not exist: {sourcePath}"
                        });
                    }
                }
                
                // Check for negative timeout
                if (op.Timeout.HasValue && op.Timeout.Value < TimeSpan.Zero)
                {
                    errors.Add(new BatchValidationError
                    {
                        OperationIndex = i,
                        OperationId = op.Id,
                        Message = "Timeout cannot be negative"
                    });
                }
            }
        }, cancellationToken);
        
        return new BatchJobValidation
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
    
    public async Task<string> ScheduleJobAsync(BatchJob job, BatchSchedule schedule, 
        CancellationToken cancellationToken = default)
    {
        var scheduledJob = new ScheduledJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Job = job,
            Schedule = schedule,
            CreatedAt = DateTime.Now,
            NextRunAt = CalculateNextRun(schedule),
            IsActive = true
        };
        
        _scheduledJobs.Add(scheduledJob);
        await SaveSchedulesAsync(cancellationToken);
        
        return scheduledJob.Id;
    }
    
    public async Task CancelScheduledJobAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        var job = _scheduledJobs.FirstOrDefault(j => j.Id == scheduleId);
        if (job != null)
        {
            _scheduledJobs.Remove(job);
            await SaveSchedulesAsync(cancellationToken);
        }
    }
    
    public Task<IReadOnlyList<ScheduledJob>> GetScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScheduledJob>>(_scheduledJobs.ToList());
    }
    
    #region Private Helpers
    
    private async Task<BatchOperationResult> ExecuteOperationWithRetryAsync(BatchOperation operation,
        Dictionary<string, string> variables, bool dryRun, CancellationToken cancellationToken)
    {
        var maxAttempts = operation.RetryCount + 1;
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var output = dryRun 
                    ? new Dictionary<string, object> { ["dryRun"] = true }
                    : await ExecuteOperationAsync(operation, variables, cancellationToken);
                
                stopwatch.Stop();
                return new BatchOperationResult
                {
                    OperationId = operation.Id,
                    Type = operation.Type,
                    Success = true,
                    Duration = stopwatch.Elapsed,
                    Output = output
                };
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        
        stopwatch.Stop();
        return new BatchOperationResult
        {
            OperationId = operation.Id,
            Type = operation.Type,
            Success = false,
            ErrorMessage = lastException?.Message ?? "Unknown error",
            Duration = stopwatch.Elapsed
        };
    }
    
    private async Task<Dictionary<string, object>> ExecuteOperationAsync(BatchOperation operation,
        Dictionary<string, string> variables, CancellationToken cancellationToken)
    {
        var output = new Dictionary<string, object>();
        var parameters = ResolveVariables(operation.Parameters, variables);
        
        switch (operation.Type)
        {
            case BatchOperationType.Copy:
                await ExecuteCopyAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Move:
                await ExecuteMoveAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Delete:
                await ExecuteDeleteAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Rename:
                await ExecuteRenameAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.CreateDirectory:
                await ExecuteCreateDirectoryAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.DeleteDirectory:
                await ExecuteDeleteDirectoryAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.CreateArchive:
                await ExecuteCreateArchiveAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.ExtractArchive:
                await ExecuteExtractArchiveAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.RunCommand:
                await ExecuteRunCommandAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Delay:
                await ExecuteDelayAsync(parameters, cancellationToken);
                break;
                
            case BatchOperationType.FindFiles:
                await ExecuteFindFilesAsync(parameters, output, cancellationToken);
                break;
                
            default:
                throw new NotSupportedException($"Operation type not supported: {operation.Type}");
        }
        
        return output;
    }
    
    private async Task ExecuteCopyAsync(Dictionary<string, string> parameters, 
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var source = parameters["source"];
        var destination = parameters["destination"];
        var overwrite = parameters.TryGetValue("overwrite", out var ow) && ow == "true";
        
        await Task.Run(() =>
        {
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite);
                output["copiedFile"] = destination;
            }
            else if (Directory.Exists(source))
            {
                CopyDirectory(source, destination, overwrite);
                output["copiedDirectory"] = destination;
            }
        }, cancellationToken);
    }
    
    private async Task ExecuteMoveAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var source = parameters["source"];
        var destination = parameters["destination"];
        
        await Task.Run(() =>
        {
            if (File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(source, destination, true);
                output["movedFile"] = destination;
            }
            else if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
                output["movedDirectory"] = destination;
            }
        }, cancellationToken);
    }
    
    private async Task ExecuteDeleteAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var path = parameters["path"];
        
        await Task.Run(() =>
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                output["deleted"] = path;
            }
        }, cancellationToken);
    }
    
    private async Task ExecuteRenameAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var source = parameters["source"];
        var newName = parameters["newName"];
        
        await Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(source)!;
            var destination = Path.Combine(directory, newName);
            
            if (File.Exists(source))
            {
                File.Move(source, destination);
                output["renamed"] = destination;
            }
            else if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
                output["renamed"] = destination;
            }
        }, cancellationToken);
    }
    
    private async Task ExecuteCreateDirectoryAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var path = parameters["path"];
        
        await Task.Run(() =>
        {
            Directory.CreateDirectory(path);
            output["created"] = path;
        }, cancellationToken);
    }
    
    private async Task ExecuteDeleteDirectoryAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var path = parameters["path"];
        var recursive = parameters.TryGetValue("recursive", out var r) && r == "true";
        
        await Task.Run(() =>
        {
            Directory.Delete(path, recursive);
            output["deleted"] = path;
        }, cancellationToken);
    }
    
    private async Task ExecuteCreateArchiveAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_archiveService == null)
        {
            throw new InvalidOperationException("Archive service not available");
        }
        
        var destination = parameters["destination"];
        var sources = parameters["sources"].Split(';');
        var type = Enum.TryParse<ArchiveType>(parameters.GetValueOrDefault("type", "Zip"), out var t) 
            ? t : ArchiveType.Zip;
        
        await _archiveService.CreateArchiveAsync(destination, sources, type, 
            CompressionLevel.Normal, null, cancellationToken);
        output["archive"] = destination;
    }
    
    private async Task ExecuteExtractArchiveAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_archiveService == null)
        {
            throw new InvalidOperationException("Archive service not available");
        }
        
        var source = parameters["source"];
        var destination = parameters["destination"];
        
        await _archiveService.ExtractAllAsync(source, destination, null, cancellationToken);
        output["extracted"] = destination;
    }
    
    private async Task ExecuteRunCommandAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var command = parameters["command"];
        var arguments = parameters.GetValueOrDefault("arguments", "");
        var workingDirectory = parameters.GetValueOrDefault("workingDirectory", Environment.CurrentDirectory);
        
        await Task.Run(() =>
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                output["exitCode"] = process.ExitCode;
                output["stdout"] = stdout;
                output["stderr"] = stderr;
            }
        }, cancellationToken);
    }
    
    private async Task ExecuteDelayAsync(Dictionary<string, string> parameters, 
        CancellationToken cancellationToken)
    {
        var milliseconds = int.Parse(parameters.GetValueOrDefault("milliseconds", "1000"));
        await Task.Delay(milliseconds, cancellationToken);
    }
    
    private async Task ExecuteFindFilesAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var path = parameters["path"];
        var pattern = parameters.GetValueOrDefault("pattern", "*");
        var recursive = parameters.TryGetValue("recursive", out var r) && r == "true";
        
        await Task.Run(() =>
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(path, pattern, searchOption);
            output["files"] = files;
            output["count"] = files.Length;
        }, cancellationToken);
    }
    
    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }
        
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir, overwrite);
        }
    }
    
    private static Dictionary<string, string> ResolveVariables(Dictionary<string, string> parameters,
        Dictionary<string, string> variables)
    {
        var resolved = new Dictionary<string, string>();
        
        foreach (var (key, value) in parameters)
        {
            var resolvedValue = value;
            foreach (var (varName, varValue) in variables)
            {
                resolvedValue = resolvedValue.Replace($"${{{varName}}}", varValue);
                resolvedValue = resolvedValue.Replace($"${varName}", varValue);
            }
            resolved[key] = resolvedValue;
        }
        
        return resolved;
    }
    
    private static bool EvaluateCondition(string condition, Dictionary<string, string> variables)
    {
        // Simple condition evaluation
        // Format: "variable==value" or "variable!=value"
        if (condition.Contains("=="))
        {
            var parts = condition.Split("==");
            if (parts.Length == 2)
            {
                var varName = parts[0].Trim().TrimStart('$').Trim('{', '}');
                var expectedValue = parts[1].Trim().Trim('"');
                return variables.TryGetValue(varName, out var actualValue) && actualValue == expectedValue;
            }
        }
        else if (condition.Contains("!="))
        {
            var parts = condition.Split("!=");
            if (parts.Length == 2)
            {
                var varName = parts[0].Trim().TrimStart('$').Trim('{', '}');
                var expectedValue = parts[1].Trim().Trim('"');
                return !variables.TryGetValue(varName, out var actualValue) || actualValue != expectedValue;
            }
        }
        
        return true;
    }
    
    private static string[] GetRequiredParameters(BatchOperationType type)
    {
        return type switch
        {
            BatchOperationType.Copy => new[] { "source", "destination" },
            BatchOperationType.Move => new[] { "source", "destination" },
            BatchOperationType.Delete => new[] { "path" },
            BatchOperationType.Rename => new[] { "source", "newName" },
            BatchOperationType.CreateDirectory => new[] { "path" },
            BatchOperationType.DeleteDirectory => new[] { "path" },
            BatchOperationType.CreateArchive => new[] { "destination", "sources" },
            BatchOperationType.ExtractArchive => new[] { "source", "destination" },
            BatchOperationType.RunCommand => new[] { "command" },
            BatchOperationType.FindFiles => new[] { "path" },
            _ => Array.Empty<string>()
        };
    }
    
    private static DateTime? CalculateNextRun(BatchSchedule schedule)
    {
        var now = DateTime.Now;
        
        return schedule.Type switch
        {
            ScheduleType.Once => schedule.RunAt,
            ScheduleType.Interval when schedule.Interval.HasValue => now + schedule.Interval.Value,
            ScheduleType.Daily when schedule.TimeOfDay.HasValue => 
                now.Date + schedule.TimeOfDay.Value > now 
                    ? now.Date + schedule.TimeOfDay.Value 
                    : now.Date.AddDays(1) + schedule.TimeOfDay.Value,
            ScheduleType.OnStartup => now,
            _ => null
        };
    }
    
    private async Task LoadSchedulesAsync()
    {
        try
        {
            if (File.Exists(SchedulesFile))
            {
                var json = await File.ReadAllTextAsync(SchedulesFile);
                var loaded = JsonSerializer.Deserialize<List<ScheduledJob>>(json, JsonOptions);
                if (loaded != null)
                {
                    _scheduledJobs.Clear();
                    _scheduledJobs.AddRange(loaded);
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }
    
    private async Task SaveSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(_scheduledJobs, JsonOptions);
        await File.WriteAllTextAsync(SchedulesFile, json, cancellationToken);
    }
    
    #endregion
}
