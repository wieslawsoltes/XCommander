// IAccessibilityService.cs - Accessibility Support
// Provides screen reader and accessibility features

using System;
using System.Collections.Generic;

namespace XCommander.Services;

/// <summary>
/// Service for accessibility features.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>
    /// Announces text to screen readers.
    /// </summary>
    void Announce(string text, AnnounceUrgency urgency = AnnounceUrgency.Normal);
    
    /// <summary>
    /// Announces a file operation.
    /// </summary>
    void AnnounceOperation(string operation, string item);
    
    /// <summary>
    /// Announces navigation.
    /// </summary>
    void AnnounceNavigation(string path);
    
    /// <summary>
    /// Announces selection change.
    /// </summary>
    void AnnounceSelection(int selectedCount, int totalCount);
    
    /// <summary>
    /// Gets whether high contrast mode is enabled.
    /// </summary>
    bool IsHighContrastEnabled { get; }
    
    /// <summary>
    /// Gets whether reduce motion is enabled.
    /// </summary>
    bool IsReducedMotionEnabled { get; }
    
    /// <summary>
    /// Gets the current UI scale factor.
    /// </summary>
    double UIScaleFactor { get; set; }
    
    /// <summary>
    /// Gets accessibility settings.
    /// </summary>
    AccessibilitySettings Settings { get; }
    
    /// <summary>
    /// Updates accessibility settings.
    /// </summary>
    void UpdateSettings(AccessibilitySettings settings);
}

/// <summary>
/// Urgency level for announcements.
/// </summary>
public enum AnnounceUrgency
{
    /// <summary>Normal priority, may be interrupted.</summary>
    Normal,
    
    /// <summary>Assertive priority, interrupts other announcements.</summary>
    Assertive,
    
    /// <summary>Polite priority, waits for other announcements.</summary>
    Polite
}

/// <summary>
/// Accessibility settings.
/// </summary>
public record AccessibilitySettings
{
    /// <summary>
    /// Whether to enable screen reader support.
    /// </summary>
    public bool ScreenReaderEnabled { get; init; } = true;
    
    /// <summary>
    /// Whether to enable keyboard navigation indicators.
    /// </summary>
    public bool KeyboardIndicatorsEnabled { get; init; } = true;
    
    /// <summary>
    /// Whether to enable high contrast mode.
    /// </summary>
    public bool HighContrastEnabled { get; init; }
    
    /// <summary>
    /// Whether to reduce motion/animations.
    /// </summary>
    public bool ReducedMotion { get; init; }
    
    /// <summary>
    /// UI scale factor (1.0 = 100%).
    /// </summary>
    public double ScaleFactor { get; init; } = 1.0;
    
    /// <summary>
    /// Minimum font size in points.
    /// </summary>
    public double MinimumFontSize { get; init; } = 12;
    
    /// <summary>
    /// Whether to announce all actions.
    /// </summary>
    public bool VerboseAnnouncements { get; init; }
    
    /// <summary>
    /// Delay in ms before announcing hover items.
    /// </summary>
    public int HoverAnnouncementDelay { get; init; } = 500;
    
    /// <summary>
    /// Whether to use high contrast icons.
    /// </summary>
    public bool HighContrastIcons { get; init; }
    
    /// <summary>
    /// Focus visible outline width.
    /// </summary>
    public double FocusOutlineWidth { get; init; } = 2;
}

/// <summary>
/// Implementation of accessibility service.
/// </summary>
public class AccessibilityService : IAccessibilityService
{
    private AccessibilitySettings _settings = new();
    private readonly Queue<(string text, AnnounceUrgency urgency)> _announcementQueue = new();
    
    public bool IsHighContrastEnabled => _settings.HighContrastEnabled || DetectSystemHighContrast();
    public bool IsReducedMotionEnabled => _settings.ReducedMotion || DetectSystemReducedMotion();
    
    public double UIScaleFactor
    {
        get => _settings.ScaleFactor;
        set => _settings = _settings with { ScaleFactor = Math.Clamp(value, 0.5, 3.0) };
    }
    
    public AccessibilitySettings Settings => _settings;
    
    public void Announce(string text, AnnounceUrgency urgency = AnnounceUrgency.Normal)
    {
        if (!_settings.ScreenReaderEnabled || string.IsNullOrEmpty(text))
            return;
        
        _announcementQueue.Enqueue((text, urgency));
        ProcessAnnouncements();
    }
    
    public void AnnounceOperation(string operation, string item)
    {
        if (!_settings.ScreenReaderEnabled)
            return;
        
        var announcement = $"{operation}: {item}";
        Announce(announcement, AnnounceUrgency.Assertive);
    }
    
    public void AnnounceNavigation(string path)
    {
        if (!_settings.ScreenReaderEnabled)
            return;
        
        var announcement = $"Navigated to: {path}";
        Announce(announcement);
    }
    
    public void AnnounceSelection(int selectedCount, int totalCount)
    {
        if (!_settings.ScreenReaderEnabled)
            return;
        
        string announcement;
        if (selectedCount == 0)
            announcement = "No items selected";
        else if (selectedCount == 1)
            announcement = "1 item selected";
        else
            announcement = $"{selectedCount} of {totalCount} items selected";
        
        Announce(announcement, AnnounceUrgency.Polite);
    }
    
    public void UpdateSettings(AccessibilitySettings settings)
    {
        _settings = settings;
    }
    
    private void ProcessAnnouncements()
    {
        // In a real implementation, this would use platform-specific
        // accessibility APIs (Windows UI Automation, macOS Accessibility, etc.)
        while (_announcementQueue.Count > 0)
        {
            var (text, urgency) = _announcementQueue.Dequeue();
            
            // Platform-specific announcement would go here
            // For now, we just consume the queue
            System.Diagnostics.Debug.WriteLine($"[Accessibility:{urgency}] {text}");
        }
    }
    
    private static bool DetectSystemHighContrast()
    {
        // In a real implementation, detect from system settings
        return false;
    }
    
    private static bool DetectSystemReducedMotion()
    {
        // In a real implementation, detect from system settings
        return false;
    }
}

/// <summary>
/// Extensions for accessible file information.
/// </summary>
public static class AccessibilityExtensions
{
    /// <summary>
    /// Gets an accessible description of a file item.
    /// </summary>
    public static string GetAccessibleDescription(string name, bool isDirectory, long size, DateTime modified)
    {
        var type = isDirectory ? "Folder" : "File";
        var sizeText = isDirectory ? "" : $", {FormatSize(size)}";
        var dateText = modified.ToString("MMMM d, yyyy");
        
        return $"{name}, {type}{sizeText}, Modified {dateText}";
    }
    
    /// <summary>
    /// Gets an accessible description of an operation result.
    /// </summary>
    public static string GetOperationResultDescription(string operation, int succeeded, int failed, int skipped)
    {
        var parts = new List<string>();
        
        if (succeeded > 0)
            parts.Add($"{succeeded} succeeded");
        if (failed > 0)
            parts.Add($"{failed} failed");
        if (skipped > 0)
            parts.Add($"{skipped} skipped");
        
        return $"{operation} complete: {string.Join(", ", parts)}";
    }
    
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
