namespace XCommander.Plugins.BuiltIn;

/// <summary>
/// Built-in command plugin that provides additional utility commands.
/// </summary>
public class UtilityCommandPlugin : ICommandPlugin
{
    public string Id => "xcommander.commands.utility";
    public string Name => "Utility Commands";
    public string Description => "Provides additional utility commands such as open terminal, copy path, etc.";
    public Version Version => new(1, 0, 0);
    public string Author => "XCommander Team";

    private IPluginContext? _context;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        
        // Register menu items
        context.RegisterMenuItem(new PluginMenuItem
        {
            Id = "utility.openTerminal",
            Text = "Open Terminal Here",
            ParentMenuId = "Tools",
            Order = 100,
            Action = async ctx =>
            {
                await OpenTerminalAsync(ctx.ActivePanelPath);
            }
        });

        context.RegisterMenuItem(new PluginMenuItem
        {
            Id = "utility.copyPath",
            Text = "Copy Path to Clipboard",
            ParentMenuId = "Edit",
            Order = 200,
            Action = async ctx =>
            {
                var paths = ctx.SelectedPaths;
                if (paths.Count > 0)
                {
                    await CopyToClipboardAsync(string.Join(Environment.NewLine, paths));
                }
                else
                {
                    await CopyToClipboardAsync(ctx.ActivePanelPath);
                }
            }
        });

        context.RegisterMenuItem(new PluginMenuItem
        {
            Id = "utility.openFileLocation",
            Text = "Open File Location",
            ParentMenuId = "Files",
            Order = 150,
            Action = async ctx =>
            {
                var paths = ctx.SelectedPaths;
                if (paths.Count > 0)
                {
                    await OpenInFileManagerAsync(paths[0]);
                }
            },
            IsEnabled = ctx => ctx.SelectedPaths.Count > 0
        });

        // Register keyboard shortcuts
        context.RegisterKeyboardShortcut(new PluginKeyboardShortcut
        {
            Key = "T",
            Ctrl = true,
            Shift = true,
            CommandId = "utility.openTerminal"
        });

        context.RegisterKeyboardShortcut(new PluginKeyboardShortcut
        {
            Key = "C",
            Ctrl = true,
            Shift = true,
            CommandId = "utility.copyPath"
        });

        context.Log(PluginLogLevel.Info, $"{Name} initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(PluginLogLevel.Info, $"{Name} shutdown");
        return Task.CompletedTask;
    }

    public IEnumerable<PluginCommand> GetCommands()
    {
        yield return new PluginCommand
        {
            Id = "utility.openTerminal",
            Name = "Open Terminal Here",
            Description = "Opens a terminal in the current directory",
            Category = "Utility",
            KeyboardShortcut = "Ctrl+Shift+T"
        };

        yield return new PluginCommand
        {
            Id = "utility.copyPath",
            Name = "Copy Path to Clipboard",
            Description = "Copies the current path or selected file paths to clipboard",
            Category = "Utility",
            KeyboardShortcut = "Ctrl+Shift+C"
        };

        yield return new PluginCommand
        {
            Id = "utility.openFileLocation",
            Name = "Open File Location",
            Description = "Opens the file location in the system file manager",
            Category = "Utility"
        };

        yield return new PluginCommand
        {
            Id = "utility.calculateSize",
            Name = "Calculate Folder Size",
            Description = "Calculates the total size of selected folders",
            Category = "Utility"
        };

        yield return new PluginCommand
        {
            Id = "utility.openWith",
            Name = "Open With...",
            Description = "Opens the selected file with a specific application",
            Category = "Utility"
        };
    }

    public async Task ExecuteCommandAsync(string commandId, IPluginContext context, CancellationToken cancellationToken = default)
    {
        switch (commandId)
        {
            case "utility.openTerminal":
                await OpenTerminalAsync(context.ActivePanelPath);
                break;

            case "utility.copyPath":
                var paths = context.SelectedPaths;
                if (paths.Count > 0)
                {
                    await CopyToClipboardAsync(string.Join(Environment.NewLine, paths));
                }
                else
                {
                    await CopyToClipboardAsync(context.ActivePanelPath);
                }
                break;

            case "utility.openFileLocation":
                if (context.SelectedPaths.Count > 0)
                {
                    await OpenInFileManagerAsync(context.SelectedPaths[0]);
                }
                break;

            case "utility.calculateSize":
                await CalculateFolderSizeAsync(context);
                break;

            case "utility.openWith":
                if (context.SelectedPaths.Count > 0)
                {
                    await OpenWithDialogAsync(context.SelectedPaths[0]);
                }
                break;
        }
    }

    private static Task OpenTerminalAsync(string workingDirectory)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo();

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "cmd.exe";
                psi.WorkingDirectory = workingDirectory;
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "open";
                psi.Arguments = $"-a Terminal \"{workingDirectory}\"";
            }
            else // Linux
            {
                // Try common terminal emulators
                var terminals = new[] { "gnome-terminal", "konsole", "xfce4-terminal", "xterm" };
                foreach (var terminal in terminals)
                {
                    try
                    {
                        psi.FileName = terminal;
                        psi.Arguments = terminal == "gnome-terminal" ? $"--working-directory=\"{workingDirectory}\"" : "";
                        psi.WorkingDirectory = workingDirectory;
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            psi.UseShellExecute = true;
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening terminal: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            // Use Avalonia's clipboard
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
        }
    }

    private static Task OpenInFileManagerAsync(string path)
    {
        try
        {
            var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            
            var psi = new System.Diagnostics.ProcessStartInfo();

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "explorer.exe";
                psi.Arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{directory}\"";
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "open";
                psi.Arguments = File.Exists(path) ? $"-R \"{path}\"" : $"\"{directory}\"";
            }
            else // Linux
            {
                psi.FileName = "xdg-open";
                psi.Arguments = $"\"{directory}\"";
            }

            psi.UseShellExecute = true;
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening file manager: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task CalculateFolderSizeAsync(IPluginContext context)
    {
        var paths = context.SelectedPaths;
        if (paths.Count == 0)
            return;

        long totalSize = 0;
        int fileCount = 0;
        int folderCount = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                folderCount++;
                var (size, files, folders) = CalculateDirectorySize(path);
                totalSize += size;
                fileCount += files;
                folderCount += folders;
            }
            else if (File.Exists(path))
            {
                fileCount++;
                totalSize += new FileInfo(path).Length;
            }
        }

        var sizeStr = FormatSize(totalSize);
        await context.ShowMessageAsync("Folder Size", 
            $"Total size: {sizeStr}\nFiles: {fileCount}\nFolders: {folderCount}");
    }

    private static (long size, int files, int folders) CalculateDirectorySize(string path)
    {
        long size = 0;
        int files = 0;
        int folders = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                    files++;
                }
                catch
                {
                    // Ignore files we can't access
                }
            }

            folders = Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            // Ignore directories we can't access
        }

        return (size, files, folders);
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private static Task OpenWithDialogAsync(string filePath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                Verb = "openas"
            };

            if (!OperatingSystem.IsWindows())
            {
                // On non-Windows, just use xdg-open
                psi.FileName = "xdg-open";
                psi.Arguments = $"\"{filePath}\"";
                psi.Verb = "";
            }

            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening with dialog: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
