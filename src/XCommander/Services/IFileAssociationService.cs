using System;

namespace XCommander.Services;

/// <summary>
/// Resolves file association commands based on file extension.
/// </summary>
public interface IFileAssociationService
{
    /// <summary>
    /// Gets the open command for a file, or null when system default should be used.
    /// </summary>
    string? GetOpenCommand(string filePath);

    /// <summary>
    /// Gets the viewer command for a file, or null when system default should be used.
    /// </summary>
    string? GetViewerCommand(string filePath);

    /// <summary>
    /// Gets the editor command for a file, or null when system default should be used.
    /// </summary>
    string? GetEditorCommand(string filePath);
}
