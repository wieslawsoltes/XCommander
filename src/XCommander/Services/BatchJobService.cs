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
    private readonly IEncodingService? _encodingService;
    private readonly ISplitMergeService? _splitMergeService;
    private readonly IFileChecksumService? _fileChecksumService;
    private readonly IDuplicateFinderService? _duplicateFinderService;
    
    public bool IsRecording => _currentRecording != null;
    
    public BatchJobService(IFileSystemService? fileService = null,
        IArchiveService? archiveService = null,
        IEncodingService? encodingService = null,
        ISplitMergeService? splitMergeService = null,
        IFileChecksumService? fileChecksumService = null,
        IDuplicateFinderService? duplicateFinderService = null)
    {
        _fileService = fileService;
        _archiveService = archiveService;
        _encodingService = encodingService;
        _splitMergeService = splitMergeService;
        _fileChecksumService = fileChecksumService;
        _duplicateFinderService = duplicateFinderService;
        
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
        var loopStates = new Dictionary<string, LoopState>();
        
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
            
            BatchOperationResult operationResult;
            int? jumpTo = null;
            
            if (operation.Type == BatchOperationType.Condition)
            {
                var controlResult = ExecuteConditionOperation(operation, variables, sortedOperations, i);
                operationResult = controlResult.Result;
                jumpTo = controlResult.JumpTo;
            }
            else if (operation.Type == BatchOperationType.Loop)
            {
                var controlResult = ExecuteLoopOperation(operation, variables, sortedOperations, i, loopStates);
                operationResult = controlResult.Result;
                jumpTo = controlResult.JumpTo;
            }
            else
            {
                // Execute with retry
                operationResult = await ExecuteOperationWithRetryAsync(operation, variables, 
                    options.DryRun, cancellationToken);
            }
            
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
            
            if (jumpTo.HasValue)
            {
                var target = Math.Clamp(jumpTo.Value, 0, sortedOperations.Count);
                if (target >= 0 && target < sortedOperations.Count)
                {
                    i = target - 1;
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
                        if (param == "source")
                        {
                            if ((op.Type == BatchOperationType.CalculateChecksum || op.Type == BatchOperationType.VerifyChecksum)
                                && op.Parameters.ContainsKey("path"))
                            {
                                continue;
                            }
                            
                            if (op.Type == BatchOperationType.Combine && op.Parameters.ContainsKey("parts"))
                            {
                                continue;
                            }
                        }

                        if (param == "path" && op.Type == BatchOperationType.FindDuplicates)
                        {
                            if (op.Parameters.ContainsKey("paths")
                                || op.Parameters.ContainsKey("directories")
                                || op.Parameters.ContainsKey("directory"))
                            {
                                continue;
                            }
                        }

                        if (param == "script" && op.Type == BatchOperationType.RunScript)
                        {
                            if (op.Parameters.ContainsKey("path")
                                || op.Parameters.ContainsKey("file"))
                            {
                                continue;
                            }
                        }
                        
                        errors.Add(new BatchValidationError
                        {
                            OperationIndex = i,
                            OperationId = op.Id,
                            Message = $"Missing required parameter: {param}"
                        });
                    }
                }

                if (op.Type == BatchOperationType.VerifyChecksum)
                {
                    var hasChecksumFile = op.Parameters.ContainsKey("checksumFile")
                        || op.Parameters.ContainsKey("checksumFilePath");
                    var hasExpected = op.Parameters.ContainsKey("expected")
                        || op.Parameters.ContainsKey("hash")
                        || op.Parameters.ContainsKey("checksum");
                    
                    if (!hasChecksumFile && !hasExpected)
                    {
                        errors.Add(new BatchValidationError
                        {
                            OperationIndex = i,
                            OperationId = op.Id,
                            Message = "Verify checksum requires expected hash or checksumFile"
                        });
                    }
                }
                
                if (op.Type == BatchOperationType.Combine && op.Parameters.ContainsKey("parts")
                    && !op.Parameters.ContainsKey("destination"))
                {
                    errors.Add(new BatchValidationError
                    {
                        OperationIndex = i,
                        OperationId = op.Id,
                        Message = "Combine operation with explicit parts requires destination"
                    });
                }

                if (op.Type == BatchOperationType.RunScript)
                {
                    var scriptPath = op.Parameters.GetValueOrDefault("script")
                        ?? op.Parameters.GetValueOrDefault("path")
                        ?? op.Parameters.GetValueOrDefault("file");
                    
                    if (!string.IsNullOrWhiteSpace(scriptPath)
                        && !scriptPath.StartsWith("$")
                        && !File.Exists(scriptPath))
                    {
                        warnings.Add(new BatchValidationWarning
                        {
                            OperationIndex = i,
                            OperationId = op.Id,
                            Message = $"Script path may not exist: {scriptPath}"
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
                
            case BatchOperationType.Encode:
                await ExecuteEncodeAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Decode:
                await ExecuteDecodeAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.Split:
                await ExecuteSplitAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.Combine:
                await ExecuteCombineAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.CalculateChecksum:
                await ExecuteCalculateChecksumAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.VerifyChecksum:
                await ExecuteVerifyChecksumAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.FindDuplicates:
                await ExecuteFindDuplicatesAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.RunCommand:
                await ExecuteRunCommandAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.RunScript:
                await ExecuteRunScriptAsync(parameters, output, cancellationToken);
                break;
                
            case BatchOperationType.Delay:
                await ExecuteDelayAsync(parameters, cancellationToken);
                break;
                
            case BatchOperationType.FindFiles:
                await ExecuteFindFilesAsync(parameters, output, cancellationToken);
                break;

            case BatchOperationType.Custom:
                await ExecuteCustomAsync(parameters, output, cancellationToken);
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

    private async Task ExecuteFindDuplicatesAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_duplicateFinderService == null)
            throw new InvalidOperationException("Duplicate finder service is not available");
        
        var pathsRaw = parameters.GetValueOrDefault("paths")
            ?? parameters.GetValueOrDefault("directories")
            ?? parameters.GetValueOrDefault("path")
            ?? parameters.GetValueOrDefault("directory");
        
        if (string.IsNullOrWhiteSpace(pathsRaw))
            throw new InvalidOperationException("FindDuplicates requires a path or directories parameter");
        
        var directories = SplitList(pathsRaw);
        var mode = ParseDuplicateSearchMode(parameters.GetValueOrDefault("mode"));
        var minSize = ResolveSize(parameters.GetValueOrDefault("minSize"), 0);
        var maxSize = ResolveSize(parameters.GetValueOrDefault("maxSize"), long.MaxValue);
        var pattern = parameters.GetValueOrDefault("pattern");
        var recursive = ParseBool(parameters.GetValueOrDefault("recursive"), true);
        
        var groups = (await _duplicateFinderService.FindDuplicatesAsync(
            directories,
            mode,
            minSize,
            maxSize,
            pattern,
            recursive,
            null,
            cancellationToken)).ToList();
        
        output["groupCount"] = groups.Count;
        output["fileCount"] = groups.Sum(g => g.FileCount);
        output["wastedSpace"] = groups.Sum(g => g.WastedSpace);
        output["groups"] = groups
            .Select(g => g.Files.Select(f => f.FullPath).ToArray())
            .ToArray();
    }

    private async Task ExecuteRunScriptAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var scriptPath = parameters.GetValueOrDefault("script")
            ?? parameters.GetValueOrDefault("path")
            ?? parameters.GetValueOrDefault("file");
        
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new InvalidOperationException("RunScript requires a script path");
        
        var arguments = parameters.GetValueOrDefault("arguments", "");
        var workingDirectory = parameters.GetValueOrDefault("workingDirectory")
            ?? Path.GetDirectoryName(scriptPath)
            ?? Environment.CurrentDirectory;
        var wait = ParseBool(parameters.GetValueOrDefault("wait"), true);
        var interpreterOverride = parameters.GetValueOrDefault("interpreter");
        
        await Task.Run(() =>
        {
            var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
            string interpreter;
            string interpreterArgs;
            
            if (!string.IsNullOrWhiteSpace(interpreterOverride))
            {
                interpreter = interpreterOverride!;
                interpreterArgs = $"\"{scriptPath}\"";
            }
            else
            {
                switch (extension)
                {
                    case ".ps1":
                        interpreter = "pwsh";
                        interpreterArgs = $"-File \"{scriptPath}\"";
                        break;
                    case ".py":
                        interpreter = "python";
                        interpreterArgs = $"\"{scriptPath}\"";
                        break;
                    case ".sh":
                        interpreter = "/bin/bash";
                        interpreterArgs = $"\"{scriptPath}\"";
                        break;
                    case ".bat":
                    case ".cmd":
                        interpreter = "cmd.exe";
                        interpreterArgs = $"/c \"{scriptPath}\"";
                        break;
                    default:
                        interpreter = scriptPath;
                        interpreterArgs = "";
                        break;
                }
            }
            
            var finalArgs = string.IsNullOrWhiteSpace(interpreterArgs)
                ? arguments
                : $"{interpreterArgs} {arguments}".Trim();
            
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = interpreter,
                Arguments = finalArgs,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                if (wait)
                {
                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    output["exitCode"] = process.ExitCode;
                    output["stdout"] = stdout;
                    output["stderr"] = stderr;
                }
                else
                {
                    output["processId"] = process.Id;
                }
            }
        }, cancellationToken);
    }

    private async Task ExecuteCustomAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        var mode = parameters.GetValueOrDefault("mode")
            ?? parameters.GetValueOrDefault("type");
        
        if (!string.IsNullOrWhiteSpace(mode))
        {
            switch (mode.Trim().ToLowerInvariant())
            {
                case "command":
                    await ExecuteRunCommandAsync(parameters, output, cancellationToken);
                    return;
                case "script":
                    await ExecuteRunScriptAsync(parameters, output, cancellationToken);
                    return;
                case "noop":
                    output["status"] = "noop";
                    return;
            }
        }
        
        if (parameters.ContainsKey("command"))
        {
            await ExecuteRunCommandAsync(parameters, output, cancellationToken);
            return;
        }
        
        if (parameters.ContainsKey("script") || parameters.ContainsKey("path") || parameters.ContainsKey("file"))
        {
            await ExecuteRunScriptAsync(parameters, output, cancellationToken);
            return;
        }
        
        output["status"] = "noop";
    }

    private async Task ExecuteEncodeAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_encodingService == null)
            throw new InvalidOperationException("Encoding service is not available");
        
        var source = parameters["source"];
        var format = ParseEncodingFormat(parameters.TryGetValue("format", out var fmt) ? fmt : null);
        var destination = ResolveEncodeDestination(parameters, source, format);
        
        var result = await _encodingService.EncodeFileAsync(source, destination, format, null, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Encode operation failed");
        
        output["outputPath"] = destination;
        output["format"] = format.ToString();
        output["inputBytes"] = result.InputSize;
        output["outputBytes"] = result.OutputSize;
    }
    
    private async Task ExecuteDecodeAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_encodingService == null)
            throw new InvalidOperationException("Encoding service is not available");
        
        var source = parameters["source"];
        var format = ParseEncodingFormat(parameters.TryGetValue("format", out var fmt) ? fmt : null);
        var destination = ResolveDecodeDestination(parameters, source);
        
        var result = await _encodingService.DecodeFileAsync(source, destination, format, null, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Decode operation failed");
        
        output["outputPath"] = destination;
        output["format"] = format.ToString();
        output["inputBytes"] = result.InputSize;
        output["outputBytes"] = result.OutputSize;
    }

    private async Task ExecuteSplitAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_splitMergeService == null)
            throw new InvalidOperationException("Split/merge service is not available");
        
        var source = parameters["source"];
        var partSize = ResolvePartSize(parameters);
        var targetDir = parameters.GetValueOrDefault("destination")
            ?? parameters.GetValueOrDefault("targetDirectory")
            ?? parameters.GetValueOrDefault("targetDir");
        var namingPattern = ParseNamingPattern(parameters.GetValueOrDefault("namingPattern"));
        var startNumber = ParseInt(parameters.GetValueOrDefault("startNumber"), 1);
        var createChecksum = ParseBool(parameters.GetValueOrDefault("createChecksumFile"), true);
        var deleteOriginal = ParseBool(parameters.GetValueOrDefault("deleteOriginalAfterSplit"), false);
        var verifyAfterSplit = ParseBool(parameters.GetValueOrDefault("verifyAfterSplit"), true);
        var createBatchFile = ParseBool(parameters.GetValueOrDefault("createBatchFile"), false);
        var createShellScript = ParseBool(parameters.GetValueOrDefault("createShellScript"), false);
        
        var options = new SplitOptions
        {
            PartSize = partSize,
            TargetDirectory = string.IsNullOrWhiteSpace(targetDir) ? null : targetDir,
            NamingPattern = namingPattern,
            StartNumber = startNumber,
            CreateChecksumFile = createChecksum,
            DeleteOriginalAfterSplit = deleteOriginal,
            VerifyAfterSplit = verifyAfterSplit,
            CreateBatchFile = createBatchFile,
            CreateShellScript = createShellScript
        };
        
        var result = await _splitMergeService.SplitFileAsync(source, options, null, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Split operation failed");
        
        output["originalFile"] = result.OriginalFile;
        output["originalSize"] = result.OriginalSize;
        output["totalParts"] = result.TotalParts;
        output["parts"] = result.Parts.Select(p => p.FilePath).ToArray();
        output["checksumFile"] = result.ChecksumFilePath ?? string.Empty;
        output["batchFile"] = result.BatchFilePath ?? string.Empty;
        output["shellScript"] = result.ShellScriptPath ?? string.Empty;
    }
    
    private async Task ExecuteCombineAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_splitMergeService == null)
            throw new InvalidOperationException("Split/merge service is not available");
        
        var verify = ParseBool(parameters.GetValueOrDefault("verifyChecksums"), true);
        var deleteParts = ParseBool(parameters.GetValueOrDefault("deletePartsAfterMerge"), false);
        var resume = ParseBool(parameters.GetValueOrDefault("resumeIfExists"), false);
        
        var options = new MergeOptions
        {
            TargetPath = parameters.GetValueOrDefault("destination"),
            VerifyChecksums = verify,
            DeletePartsAfterMerge = deleteParts,
            ResumeIfExists = resume
        };
        
        MergeResult result;
        var partsRaw = parameters.GetValueOrDefault("parts");
        if (!string.IsNullOrWhiteSpace(partsRaw))
        {
            var parts = SplitList(partsRaw);
            var destination = parameters.GetValueOrDefault("destination");
            if (string.IsNullOrWhiteSpace(destination))
                throw new InvalidOperationException("Combine operation requires destination when using explicit parts list");
            
            result = await _splitMergeService.MergeFilesAsync(parts, destination, options, null, cancellationToken);
        }
        else
        {
            var source = parameters.GetValueOrDefault("source") ?? parameters.GetValueOrDefault("path");
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("Combine operation requires a source part or checksum file");
            
            result = await _splitMergeService.MergeFilesAsync(source, options, null, cancellationToken);
        }
        
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Combine operation failed");
        
        output["mergedFile"] = result.MergedFile;
        output["mergedSize"] = result.MergedSize;
        output["checksum"] = result.Checksum ?? string.Empty;
        output["checksumVerified"] = result.ChecksumVerified;
        output["partsProcessed"] = result.PartsProcessed;
        output["partFiles"] = result.PartFiles.ToArray();
    }
    
    private async Task ExecuteCalculateChecksumAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_fileChecksumService == null)
            throw new InvalidOperationException("Checksum service is not available");
        
        var source = parameters.GetValueOrDefault("source") ?? parameters.GetValueOrDefault("path");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("Checksum operation requires a source path");
        
        var algorithm = ParseChecksumAlgorithm(parameters.GetValueOrDefault("algorithm"));
        var result = await _fileChecksumService.CalculateChecksumAsync(source, algorithm, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException(result.ErrorMessage ?? "Checksum calculation failed");
        
        output["hash"] = result.Hash;
        output["algorithm"] = result.Algorithm.ToString();
        output["fileSize"] = result.FileSize;
    }
    
    private async Task ExecuteVerifyChecksumAsync(Dictionary<string, string> parameters,
        Dictionary<string, object> output, CancellationToken cancellationToken)
    {
        if (_fileChecksumService == null)
            throw new InvalidOperationException("Checksum service is not available");
        
        var checksumFile = parameters.GetValueOrDefault("checksumFile")
            ?? parameters.GetValueOrDefault("checksumFilePath");
        if (!string.IsNullOrWhiteSpace(checksumFile))
        {
            var results = await _fileChecksumService.VerifyChecksumFileAsync(checksumFile, null, cancellationToken);
            output["totalFiles"] = results.Count;
            output["matched"] = results.Count(r => r.IsMatch);
            output["missing"] = results.Count(r => !r.FileExists);
            output["failed"] = results.Count(r => r.FileExists && !r.IsMatch);
            output["files"] = results.Select(r => r.FilePath).ToArray();
            output["isMatch"] = results.All(r => r.FileExists && r.IsMatch);
            return;
        }
        
        var source = parameters.GetValueOrDefault("source") ?? parameters.GetValueOrDefault("path");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("Verify checksum operation requires a source path");
        
        var expected = parameters.GetValueOrDefault("expected")
            ?? parameters.GetValueOrDefault("hash")
            ?? parameters.GetValueOrDefault("checksum");
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("Verify checksum operation requires an expected hash or checksum file");
        
        var algorithmValue = parameters.GetValueOrDefault("algorithm");
        var algorithm = string.IsNullOrWhiteSpace(algorithmValue)
            ? GuessChecksumAlgorithm(expected)
            : ParseChecksumAlgorithm(algorithmValue);
        
        var isMatch = await _fileChecksumService.VerifyChecksumAsync(source, expected, algorithm, cancellationToken);
        output["isMatch"] = isMatch;
        output["algorithm"] = algorithm.ToString();
        output["expected"] = expected;
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
        if (string.IsNullOrWhiteSpace(condition))
            return true;
        
        var ops = new[] { ">=", "<=", "==", "!=", ">", "<" };
        foreach (var op in ops)
        {
            var index = condition.IndexOf(op, StringComparison.Ordinal);
            if (index < 0)
                continue;
            
            var left = condition[..index].Trim();
            var right = condition[(index + op.Length)..].Trim().Trim('"');
            var varName = left.TrimStart('$').Trim('{', '}');
            variables.TryGetValue(varName, out var actualValue);
            
            if (TryParseNumber(actualValue, out var actualNumber)
                && TryParseNumber(right, out var expectedNumber))
            {
                return op switch
                {
                    "==" => actualNumber.Equals(expectedNumber),
                    "!=" => !actualNumber.Equals(expectedNumber),
                    ">" => actualNumber > expectedNumber,
                    "<" => actualNumber < expectedNumber,
                    ">=" => actualNumber >= expectedNumber,
                    "<=" => actualNumber <= expectedNumber,
                    _ => false
                };
            }
            
            actualValue ??= string.Empty;
            return op switch
            {
                "==" => actualValue == right,
                "!=" => actualValue != right,
                ">" => string.Compare(actualValue, right, StringComparison.Ordinal) > 0,
                "<" => string.Compare(actualValue, right, StringComparison.Ordinal) < 0,
                ">=" => string.Compare(actualValue, right, StringComparison.Ordinal) >= 0,
                "<=" => string.Compare(actualValue, right, StringComparison.Ordinal) <= 0,
                _ => false
            };
        }
        
        var key = condition.Trim().TrimStart('$').Trim('{', '}');
        return variables.TryGetValue(key, out var value) && IsTruthy(value);
    }

    private ControlFlowResult ExecuteConditionOperation(
        BatchOperation operation,
        Dictionary<string, string> variables,
        IReadOnlyList<BatchOperation> operations,
        int currentIndex)
    {
        try
        {
            var parameters = ResolveVariables(operation.Parameters, variables);
            var expression = parameters.GetValueOrDefault("condition")
                ?? parameters.GetValueOrDefault("expression")
                ?? parameters.GetValueOrDefault("if")
                ?? string.Empty;
            
            var isTrue = string.IsNullOrWhiteSpace(expression) || EvaluateCondition(expression, variables);
            var setVar = parameters.GetValueOrDefault("set")
                ?? parameters.GetValueOrDefault("variable")
                ?? parameters.GetValueOrDefault("name");
            
            if (!string.IsNullOrWhiteSpace(setVar))
            {
                variables[setVar] = isTrue ? "true" : "false";
            }
            
            variables["_lastCondition"] = isTrue ? "true" : "false";
            
            var output = new Dictionary<string, object>
            {
                ["condition"] = expression,
                ["isTrue"] = isTrue
            };
            
            int? jumpTo = ResolveConditionJump(parameters, operations, currentIndex, isTrue);
            
            return new ControlFlowResult(new BatchOperationResult
            {
                OperationId = operation.Id,
                Type = operation.Type,
                Success = true,
                Duration = TimeSpan.Zero,
                Output = output
            }, jumpTo);
        }
        catch (Exception ex)
        {
            return new ControlFlowResult(new BatchOperationResult
            {
                OperationId = operation.Id,
                Type = operation.Type,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            }, null);
        }
    }
    
    private ControlFlowResult ExecuteLoopOperation(
        BatchOperation operation,
        Dictionary<string, string> variables,
        IReadOnlyList<BatchOperation> operations,
        int currentIndex,
        Dictionary<string, LoopState> loopStates)
    {
        try
        {
            var parameters = ResolveVariables(operation.Parameters, variables);
            var count = ParseInt(parameters.GetValueOrDefault("count"), 1);
            if (count <= 1)
            {
                return new ControlFlowResult(new BatchOperationResult
                {
                    OperationId = operation.Id,
                    Type = operation.Type,
                    Success = true,
                    Duration = TimeSpan.Zero,
                    Output = new Dictionary<string, object>
                    {
                        ["total"] = count,
                        ["remaining"] = 0,
                        ["targetIndex"] = -1
                    }
                }, null);
            }
            
            var targetIndex = ResolveLoopTargetIndex(parameters, operations, currentIndex);
            if (targetIndex < 0 || targetIndex >= currentIndex)
            {
                return new ControlFlowResult(new BatchOperationResult
                {
                    OperationId = operation.Id,
                    Type = operation.Type,
                    Success = false,
                    ErrorMessage = "Loop target index is invalid or not before loop operation",
                    Duration = TimeSpan.Zero
                }, null);
            }
            
            if (!loopStates.TryGetValue(operation.Id, out var state))
            {
                state = new LoopState
                {
                    Remaining = count - 1,
                    TargetIndex = targetIndex,
                    Total = count
                };
            }
            else
            {
                state = state with { TargetIndex = targetIndex, Total = count };
            }
            
            int? jumpTo = null;
            if (state.Remaining > 0)
            {
                state = state with { Remaining = state.Remaining - 1 };
                loopStates[operation.Id] = state;
                jumpTo = state.TargetIndex;
            }
            else
            {
                loopStates.Remove(operation.Id);
            }
            
            var output = new Dictionary<string, object>
            {
                ["total"] = state.Total,
                ["remaining"] = state.Remaining,
                ["targetIndex"] = state.TargetIndex
            };
            
            return new ControlFlowResult(new BatchOperationResult
            {
                OperationId = operation.Id,
                Type = operation.Type,
                Success = true,
                Duration = TimeSpan.Zero,
                Output = output
            }, jumpTo);
        }
        catch (Exception ex)
        {
            return new ControlFlowResult(new BatchOperationResult
            {
                OperationId = operation.Id,
                Type = operation.Type,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            }, null);
        }
    }

    private static int? ResolveConditionJump(
        Dictionary<string, string> parameters,
        IReadOnlyList<BatchOperation> operations,
        int currentIndex,
        bool isTrue)
    {
        var branch = isTrue ? "True" : "False";
        var gotoKey = $"gotoIf{branch}";
        var jumpKey = $"jumpIf{branch}";
        var skipKey = $"skipIf{branch}";
        
        if (parameters.TryGetValue(gotoKey, out var gotoValue) && !string.IsNullOrWhiteSpace(gotoValue))
        {
            var index = FindOperationIndex(operations, gotoValue);
            if (index.HasValue)
            {
                return index.Value;
            }
        }
        
        if (parameters.TryGetValue(jumpKey, out var jumpValue) && int.TryParse(jumpValue, out var offset))
        {
            return currentIndex + offset;
        }
        
        if (parameters.TryGetValue(skipKey, out var skipValue) && int.TryParse(skipValue, out var skipCount))
        {
            if (skipCount > 0)
                return currentIndex + 1 + skipCount;
        }
        
        return null;
    }
    
    private static int ResolveLoopTargetIndex(
        Dictionary<string, string> parameters,
        IReadOnlyList<BatchOperation> operations,
        int currentIndex)
    {
        if (parameters.TryGetValue("startId", out var startId) && !string.IsNullOrWhiteSpace(startId))
        {
            var index = FindOperationIndex(operations, startId);
            if (index.HasValue)
                return index.Value;
        }
        
        if (parameters.TryGetValue("startOrder", out var startOrder) && int.TryParse(startOrder, out var order))
        {
            var index = operations.Select((op, idx) => (op, idx))
                .FirstOrDefault(p => p.op.Order == order);
            return index.op != null ? index.idx : -1;
        }
        
        if (parameters.TryGetValue("startIndex", out var startIndex) && int.TryParse(startIndex, out var explicitIndex))
        {
            return explicitIndex;
        }
        
        var offset = ParseInt(parameters.GetValueOrDefault("offset"), -1);
        return currentIndex + offset;
    }
    
    private static int? FindOperationIndex(IReadOnlyList<BatchOperation> operations, string id)
    {
        for (int i = 0; i < operations.Count; i++)
        {
            if (string.Equals(operations[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        
        return null;
    }

    private static EncodingFormat ParseEncodingFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EncodingFormat.Base64;
        
        if (Enum.TryParse<EncodingFormat>(value, true, out var parsed))
            return parsed;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "b64" or "base64" => EncodingFormat.Base64,
            "uu" or "uue" or "uuencode" => EncodingFormat.UUEncode,
            "xx" or "xxe" or "xxencode" => EncodingFormat.XXEncode,
            "yenc" or "y-enc" => EncodingFormat.YEnc,
            "qp" or "quoted-printable" or "quotedprintable" => EncodingFormat.QuotedPrintable,
            "hex" => EncodingFormat.Hex,
            "rot" or "rot13" => EncodingFormat.Rot13,
            "mime" => EncodingFormat.Mime,
            _ => throw new ArgumentException($"Unsupported encoding format: {value}", nameof(value))
        };
    }
    
    private string ResolveEncodeDestination(Dictionary<string, string> parameters, string source, EncodingFormat format)
    {
        if (parameters.TryGetValue("destination", out var destination) && !string.IsNullOrWhiteSpace(destination))
            return destination;
        
        if (_encodingService == null)
            throw new InvalidOperationException("Encoding service is not available");
        
        var extension = _encodingService.GetFileExtension(format);
        return source + extension;
    }
    
    private static string ResolveDecodeDestination(Dictionary<string, string> parameters, string source)
    {
        if (parameters.TryGetValue("destination", out var destination) && !string.IsNullOrWhiteSpace(destination))
            return destination;
        
        var extension = Path.GetExtension(source);
        if (!string.IsNullOrWhiteSpace(extension) && IsEncodedExtension(extension))
        {
            return Path.Combine(Path.GetDirectoryName(source) ?? string.Empty,
                Path.GetFileNameWithoutExtension(source));
        }
        
        return source + ".decoded";
    }
    
    private static bool IsEncodedExtension(string extension)
    {
        return extension.Equals(".b64", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".uue", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xxe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yenc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".qp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".hex", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rot13", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mim", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".enc", StringComparison.OrdinalIgnoreCase);
    }

    private static long ResolvePartSize(Dictionary<string, string> parameters)
    {
        var raw = parameters.GetValueOrDefault("partSize")
            ?? parameters.GetValueOrDefault("size");
        if (TryParseSize(raw, out var size))
            return size;
        
        var preset = parameters.GetValueOrDefault("preset");
        if (TryResolveSplitPreset(preset, out var presetSize))
            return presetSize;
        
        return new SplitOptions().PartSize;
    }
    
    private static bool TryParseSize(string? raw, out long size)
    {
        size = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        
        var value = raw.Trim();
        if (long.TryParse(value, out size))
            return size > 0;
        
        var suffix = value[^1];
        var numberPart = value.Substring(0, value.Length - 1).Trim();
        if (!double.TryParse(numberPart, out var numeric))
            return false;
        
        var multiplier = suffix switch
        {
            'k' or 'K' => 1024d,
            'm' or 'M' => 1024d * 1024d,
            'g' or 'G' => 1024d * 1024d * 1024d,
            't' or 'T' => 1024d * 1024d * 1024d * 1024d,
            _ => 0d
        };
        
        if (multiplier <= 0)
            return false;
        
        size = (long)(numeric * multiplier);
        return size > 0;
    }
    
    private static bool TryResolveSplitPreset(string? preset, out long size)
    {
        size = 0;
        if (string.IsNullOrWhiteSpace(preset))
            return false;
        
        return preset.Trim().ToLowerInvariant() switch
        {
            "floppy360k" => SetPreset(out size, SplitOptions.Presets.Floppy360K),
            "floppy720k" => SetPreset(out size, SplitOptions.Presets.Floppy720K),
            "floppy144m" or "floppy1.44m" => SetPreset(out size, SplitOptions.Presets.Floppy144M),
            "zip100m" => SetPreset(out size, SplitOptions.Presets.Zip100M),
            "zip250m" => SetPreset(out size, SplitOptions.Presets.Zip250M),
            "cd650m" => SetPreset(out size, SplitOptions.Presets.CD650M),
            "cd700m" => SetPreset(out size, SplitOptions.Presets.CD700M),
            "dvd47g" or "dvd4.7g" => SetPreset(out size, SplitOptions.Presets.DVD47G),
            "dvd85g" or "dvd8.5g" => SetPreset(out size, SplitOptions.Presets.DVD85G),
            "bluray25g" or "br25g" => SetPreset(out size, SplitOptions.Presets.BluRay25G),
            "bluray50g" or "br50g" => SetPreset(out size, SplitOptions.Presets.BluRay50G),
            "usb1g" => SetPreset(out size, SplitOptions.Presets.USB1G),
            "usb2g" => SetPreset(out size, SplitOptions.Presets.USB2G),
            "usb4g" => SetPreset(out size, SplitOptions.Presets.USB4G),
            "usb8g" => SetPreset(out size, SplitOptions.Presets.USB8G),
            _ => false
        };
    }
    
    private static bool SetPreset(out long size, long value)
    {
        size = value;
        return true;
    }
    
    private static SplitNamingPattern ParseNamingPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return SplitNamingPattern.NumberedExtension;
        
        if (Enum.TryParse<SplitNamingPattern>(value, true, out var parsed))
            return parsed;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "numbered" or "numberedextension" => SplitNamingPattern.NumberedExtension,
            "preserve" or "preserveextension" => SplitNamingPattern.PreserveExtension,
            "partsuffix" or "suffix" => SplitNamingPattern.PartSuffix,
            _ => SplitNamingPattern.NumberedExtension
        };
    }
    
    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
    
    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        
        if (bool.TryParse(value, out var parsed))
            return parsed;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "yes" or "y" or "on" => true,
            "0" or "no" or "n" or "off" => false,
            _ => fallback
        };
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "on" => true,
            "0" or "false" or "no" or "n" or "off" => false,
            _ => true
        };
    }
    
    private static bool TryParseNumber(string? value, out double number)
    {
        return double.TryParse(value, out number);
    }

    private static long ResolveSize(string? raw, long fallback)
    {
        return TryParseSize(raw, out var size) ? size : fallback;
    }
    
    private static IReadOnlyList<string> SplitList(string raw)
    {
        return raw.Split(new[] { ';', ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private static DuplicateSearchMode ParseDuplicateSearchMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DuplicateSearchMode.BySizeAndContent;
        
        if (Enum.TryParse<DuplicateSearchMode>(value, true, out var parsed))
            return parsed;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "name" or "byname" => DuplicateSearchMode.ByName,
            "size" or "bysize" => DuplicateSearchMode.BySize,
            "content" or "bycontent" => DuplicateSearchMode.ByContent,
            "nameandsize" or "bynameandsize" => DuplicateSearchMode.ByNameAndSize,
            "sizeandcontent" or "bysizeandcontent" => DuplicateSearchMode.BySizeAndContent,
            _ => DuplicateSearchMode.BySizeAndContent
        };
    }
    
    private static ChecksumAlgorithm ParseChecksumAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ChecksumAlgorithm.SHA256;
        
        if (Enum.TryParse<ChecksumAlgorithm>(value, true, out var parsed))
            return parsed;
        
        return value.Trim().ToLowerInvariant() switch
        {
            "crc" or "crc32" => ChecksumAlgorithm.CRC32,
            "md5" => ChecksumAlgorithm.MD5,
            "sha1" => ChecksumAlgorithm.SHA1,
            "sha256" => ChecksumAlgorithm.SHA256,
            "sha384" => ChecksumAlgorithm.SHA384,
            "sha512" => ChecksumAlgorithm.SHA512,
            _ => throw new ArgumentException($"Unsupported checksum algorithm: {value}", nameof(value))
        };
    }
    
    private static ChecksumAlgorithm GuessChecksumAlgorithm(string hash)
    {
        var normalized = hash.Trim();
        return normalized.Length switch
        {
            8 => ChecksumAlgorithm.CRC32,
            32 => ChecksumAlgorithm.MD5,
            40 => ChecksumAlgorithm.SHA1,
            64 => ChecksumAlgorithm.SHA256,
            96 => ChecksumAlgorithm.SHA384,
            128 => ChecksumAlgorithm.SHA512,
            _ => ChecksumAlgorithm.SHA256
        };
    }

    private sealed record LoopState
    {
        public int Remaining { get; init; }
        public int TargetIndex { get; init; }
        public int Total { get; init; }
    }

    private readonly record struct ControlFlowResult(BatchOperationResult Result, int? JumpTo);
    
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
            BatchOperationType.Encode => new[] { "source" },
            BatchOperationType.Decode => new[] { "source" },
            BatchOperationType.Split => new[] { "source" },
            BatchOperationType.Combine => new[] { "source" },
            BatchOperationType.CalculateChecksum => new[] { "source" },
            BatchOperationType.VerifyChecksum => new[] { "source" },
            BatchOperationType.FindDuplicates => new[] { "path" },
            BatchOperationType.RunCommand => new[] { "command" },
            BatchOperationType.RunScript => new[] { "script" },
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
