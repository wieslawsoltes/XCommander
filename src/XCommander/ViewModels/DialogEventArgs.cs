namespace XCommander.ViewModels;

/// <summary>
/// Event arguments for copy/move dialog requests.
/// </summary>
public class CopyMoveDialogEventArgs : EventArgs
{
    /// <summary>
    /// Source file paths to copy or move.
    /// </summary>
    public required List<string> SourcePaths { get; init; }
    
    /// <summary>
    /// Default destination folder.
    /// </summary>
    public required string DestinationFolder { get; init; }
    
    /// <summary>
    /// True if this is a copy operation, false for move.
    /// </summary>
    public bool IsCopy { get; init; }
    
    /// <summary>
    /// Callback to provide the dialog result.
    /// </summary>
    public required Action<CopyMoveDialogResult?> Callback { get; init; }
}

/// <summary>
/// Result from copy/move dialog.
/// </summary>
public class CopyMoveDialogResult
{
    /// <summary>
    /// Whether the operation was confirmed.
    /// </summary>
    public bool Confirmed { get; set; }
    
    /// <summary>
    /// The destination path (may be modified by user).
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to preserve date/time attributes.
    /// </summary>
    public bool PreserveDateTime { get; set; } = true;
    
    /// <summary>
    /// Whether to verify after copy.
    /// </summary>
    public bool VerifyAfterCopy { get; set; }
    
    /// <summary>
    /// Overwrite mode for existing files.
    /// </summary>
    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Ask;
}

/// <summary>
/// Event arguments for delete confirmation dialog requests.
/// </summary>
public class DeleteConfirmationEventArgs : EventArgs
{
    /// <summary>
    /// File paths to delete.
    /// </summary>
    public required List<string> SourcePaths { get; init; }
    
    /// <summary>
    /// Callback to provide the dialog result.
    /// </summary>
    public required Action<DeleteConfirmationResult?> Callback { get; init; }
}

/// <summary>
/// Result from delete confirmation dialog.
/// </summary>
public class DeleteConfirmationResult
{
    /// <summary>
    /// Whether the delete was confirmed.
    /// </summary>
    public bool Confirmed { get; set; }
    
    /// <summary>
    /// Delete mode (recycle bin, permanent, secure).
    /// Uses DeleteMode enum from DeleteConfirmationViewModel.
    /// </summary>
    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBin;
    
    /// <summary>
    /// Number of wipe passes for secure delete.
    /// </summary>
    public int WipePassCount { get; set; } = 3;
    
    /// <summary>
    /// Whether to delete read-only files.
    /// </summary>
    public bool DeleteReadOnly { get; set; }
    
    /// <summary>
    /// Whether to delete hidden files.
    /// </summary>
    public bool DeleteHidden { get; set; } = true;
}

/// <summary>
/// Event arguments for overwrite confirmation dialog.
/// </summary>
public class OverwriteDialogEventArgs : EventArgs
{
    /// <summary>
    /// Source file path.
    /// </summary>
    public required string SourcePath { get; init; }
    
    /// <summary>
    /// Target file path that exists.
    /// </summary>
    public required string TargetPath { get; init; }
    
    /// <summary>
    /// Whether there are more files to process.
    /// </summary>
    public bool HasMoreFiles { get; init; }
    
    /// <summary>
    /// Callback to provide the dialog result.
    /// </summary>
    public required Action<OverwriteConfirmationResult?> Callback { get; init; }
}

/// <summary>
/// Result from overwrite confirmation dialog.
/// </summary>
public class OverwriteConfirmationResult
{
    /// <summary>
    /// The action to take.
    /// </summary>
    public OverwriteAction Action { get; set; }
    
    /// <summary>
    /// Whether to remember this choice for remaining files.
    /// </summary>
    public bool RememberChoice { get; set; }
}

/// <summary>
/// Overwrite actions for individual files.
/// </summary>
public enum OverwriteAction
{
    Overwrite,
    Skip,
    Rename,
    Cancel
}
