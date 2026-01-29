using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class FileCompareViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _leftPath = string.Empty;

    [ObservableProperty]
    private string _rightPath = string.Empty;

    [ObservableProperty]
    private bool _isComparing;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private bool _ignoreWhitespace;

    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    private bool _ignoreEmptyLines;

    [ObservableProperty]
    private bool _isBinaryMode;

    [ObservableProperty]
    private string _leftFileName = string.Empty;

    [ObservableProperty]
    private string _rightFileName = string.Empty;

    [ObservableProperty]
    private string _leftFileInfo = string.Empty;

    [ObservableProperty]
    private string _rightFileInfo = string.Empty;

    [ObservableProperty]
    private int _diffCount;

    [ObservableProperty]
    private int _currentDiffIndex;

    public ObservableCollection<DiffLine> LeftLines { get; } = new();
    public ObservableCollection<DiffLine> RightLines { get; } = new();

    public ObservableCollection<DataGridColumnDefinition> LeftColumnDefinitions { get; }
    public FilteringModel LeftFilteringModel { get; }
    public SortingModel LeftSortingModel { get; }
    public SearchModel LeftSearchModel { get; }

    public ObservableCollection<DataGridColumnDefinition> RightColumnDefinitions { get; }
    public FilteringModel RightFilteringModel { get; }
    public SortingModel RightSortingModel { get; }
    public SearchModel RightSearchModel { get; }

    public FileCompareViewModel()
    {
        LeftFilteringModel = new FilteringModel { OwnsViewFilter = true };
        LeftSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        LeftSearchModel = new SearchModel();
        LeftColumnDefinitions = BuildLineColumnDefinitions();

        RightFilteringModel = new FilteringModel { OwnsViewFilter = true };
        RightSortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        RightSearchModel = new SearchModel();
        RightColumnDefinitions = BuildLineColumnDefinitions();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildLineColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<DiffLine>();

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "#",
                cellTemplateKey: "DiffLineNumberTemplate",
                configure: column =>
                {
                    column.ColumnKey = "line-number";
                    column.Width = new DataGridLength(50);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<DiffLine, string>(
                        item => item.LineNumberDisplay);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<DiffLine, int>(
                            item => item.LineNumber)
                    };
                }),
            builder.Template(
                header: "Content",
                cellTemplateKey: "DiffLineContentTemplate",
                configure: column =>
                {
                    column.ColumnKey = "content";
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<DiffLine, string>(
                        item => item.Content);
                    column.ValueType = typeof(string);
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<DiffLine, string>(
                            item => item.Content),
                        SearchTextProvider = item =>
                        {
                            if (item is not DiffLine line)
                                return string.Empty;
                            return line.Content;
                        }
                    };
                })
        };
    }

    public void Initialize(string? leftPath, string? rightPath)
    {
        if (!string.IsNullOrEmpty(leftPath))
        {
            LeftPath = leftPath;
            LeftFileName = Path.GetFileName(leftPath);
            UpdateFileInfo(leftPath, true);
        }

        if (!string.IsNullOrEmpty(rightPath))
        {
            RightPath = rightPath;
            RightFileName = Path.GetFileName(rightPath);
            UpdateFileInfo(rightPath, false);
        }
    }

    private void UpdateFileInfo(string path, bool isLeft)
    {
        try
        {
            var info = new FileInfo(path);
            var fileInfo = $"Size: {FormatSize(info.Length)}, Modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            if (isLeft)
                LeftFileInfo = fileInfo;
            else
                RightFileInfo = fileInfo;
        }
        catch
        {
            if (isLeft)
                LeftFileInfo = "Unable to read file info";
            else
                RightFileInfo = "Unable to read file info";
        }
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        if (string.IsNullOrWhiteSpace(LeftPath) || string.IsNullOrWhiteSpace(RightPath))
        {
            Status = "Please specify both files";
            return;
        }

        if (!File.Exists(LeftPath))
        {
            Status = $"Left file does not exist: {LeftPath}";
            return;
        }

        if (!File.Exists(RightPath))
        {
            Status = $"Right file does not exist: {RightPath}";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsComparing = true;
            LeftLines.Clear();
            RightLines.Clear();
            DiffCount = 0;
            CurrentDiffIndex = -1;
            Status = "Comparing files...";

            await Task.Run(() => CompareFiles(_cancellationTokenSource.Token));

            Status = DiffCount == 0 
                ? "Files are identical" 
                : $"Comparison complete. {DiffCount} difference(s) found";
        }
        catch (OperationCanceledException)
        {
            Status = "Comparison cancelled";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsComparing = false;
        }
    }

    private void CompareFiles(CancellationToken cancellationToken)
    {
        // Check if binary mode should be used
        if (IsBinaryMode || IsBinaryFile(LeftPath) || IsBinaryFile(RightPath))
        {
            CompareBinaryFiles(cancellationToken);
        }
        else
        {
            CompareTextFiles(cancellationToken);
        }
    }

    private bool IsBinaryFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var buffer = new byte[8192];
            var read = stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < read; i++)
            {
                // Check for null bytes (common in binary files)
                if (buffer[i] == 0)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void CompareTextFiles(CancellationToken cancellationToken)
    {
        var leftLines = File.ReadAllLines(LeftPath);
        var rightLines = File.ReadAllLines(RightPath);

        // Use LCS (Longest Common Subsequence) algorithm for diff
        var diff = ComputeLCS(leftLines, rightLines, cancellationToken);

        int leftIndex = 0, rightIndex = 0;
        int lineNumber = 1;

        foreach (var segment in diff)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (segment.Type)
            {
                case DiffType.Equal:
                    for (int i = 0; i < segment.Count; i++)
                    {
                        AddLine(LeftLines, lineNumber, leftLines[leftIndex + i], DiffType.Equal);
                        AddLine(RightLines, lineNumber, rightLines[rightIndex + i], DiffType.Equal);
                        lineNumber++;
                    }
                    leftIndex += segment.Count;
                    rightIndex += segment.Count;
                    break;

                case DiffType.Deleted:
                    for (int i = 0; i < segment.Count; i++)
                    {
                        AddLine(LeftLines, lineNumber, leftLines[leftIndex + i], DiffType.Deleted);
                        AddLine(RightLines, lineNumber, "", DiffType.Empty);
                        DiffCount++;
                        lineNumber++;
                    }
                    leftIndex += segment.Count;
                    break;

                case DiffType.Added:
                    for (int i = 0; i < segment.Count; i++)
                    {
                        AddLine(LeftLines, lineNumber, "", DiffType.Empty);
                        AddLine(RightLines, lineNumber, rightLines[rightIndex + i], DiffType.Added);
                        DiffCount++;
                        lineNumber++;
                    }
                    rightIndex += segment.Count;
                    break;

                case DiffType.Modified:
                    for (int i = 0; i < segment.LeftCount; i++)
                    {
                        AddLine(LeftLines, lineNumber, leftLines[leftIndex + i], DiffType.Modified);
                        if (i < segment.RightCount)
                        {
                            AddLine(RightLines, lineNumber, rightLines[rightIndex + i], DiffType.Modified);
                        }
                        else
                        {
                            AddLine(RightLines, lineNumber, "", DiffType.Empty);
                        }
                        DiffCount++;
                        lineNumber++;
                    }
                    for (int i = segment.LeftCount; i < segment.RightCount; i++)
                    {
                        AddLine(LeftLines, lineNumber, "", DiffType.Empty);
                        AddLine(RightLines, lineNumber, rightLines[rightIndex + i], DiffType.Added);
                        DiffCount++;
                        lineNumber++;
                    }
                    leftIndex += segment.LeftCount;
                    rightIndex += segment.RightCount;
                    break;
            }
        }
    }

    private List<DiffSegment> ComputeLCS(string[] left, string[] right, CancellationToken cancellationToken)
    {
        var result = new List<DiffSegment>();
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        int i = 0, j = 0;

        while (i < left.Length || j < right.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Count equal lines
            int equalCount = 0;
            while (i + equalCount < left.Length && j + equalCount < right.Length &&
                   LinesEqual(left[i + equalCount], right[j + equalCount], comparison))
            {
                equalCount++;
            }

            if (equalCount > 0)
            {
                result.Add(new DiffSegment { Type = DiffType.Equal, Count = equalCount });
                i += equalCount;
                j += equalCount;
                continue;
            }

            // Find next equal line
            int leftSkip = FindNextMatch(left, i, right, j, comparison, out int rightSkip);

            if (leftSkip == -1)
            {
                // No more matches - rest is different
                if (i < left.Length && j < right.Length)
                {
                    result.Add(new DiffSegment
                    {
                        Type = DiffType.Modified,
                        LeftCount = left.Length - i,
                        RightCount = right.Length - j
                    });
                }
                else if (i < left.Length)
                {
                    result.Add(new DiffSegment { Type = DiffType.Deleted, Count = left.Length - i });
                }
                else if (j < right.Length)
                {
                    result.Add(new DiffSegment { Type = DiffType.Added, Count = right.Length - j });
                }
                break;
            }
            else
            {
                // Add differences before match
                if (leftSkip > 0 && rightSkip > 0)
                {
                    result.Add(new DiffSegment
                    {
                        Type = DiffType.Modified,
                        LeftCount = leftSkip,
                        RightCount = rightSkip
                    });
                }
                else if (leftSkip > 0)
                {
                    result.Add(new DiffSegment { Type = DiffType.Deleted, Count = leftSkip });
                }
                else if (rightSkip > 0)
                {
                    result.Add(new DiffSegment { Type = DiffType.Added, Count = rightSkip });
                }

                i += leftSkip;
                j += rightSkip;
            }
        }

        return result;
    }

    private bool LinesEqual(string left, string right, StringComparison comparison)
    {
        if (IgnoreWhitespace)
        {
            left = string.Join(" ", left.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
            right = string.Join(" ", right.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
        }

        if (IgnoreEmptyLines && string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left, right, comparison);
    }

    private int FindNextMatch(string[] left, int leftStart, string[] right, int rightStart,
        StringComparison comparison, out int rightSkip)
    {
        rightSkip = 0;

        // Search for next matching line within a window
        const int maxWindow = 1000;
        int leftWindow = Math.Min(maxWindow, left.Length - leftStart);
        int rightWindow = Math.Min(maxWindow, right.Length - rightStart);

        for (int distance = 1; distance < leftWindow + rightWindow; distance++)
        {
            for (int li = 0; li <= Math.Min(distance, leftWindow - 1); li++)
            {
                int ri = distance - li;
                if (ri >= 0 && ri < rightWindow)
                {
                    if (LinesEqual(left[leftStart + li], right[rightStart + ri], comparison))
                    {
                        rightSkip = ri;
                        return li;
                    }
                }
            }
        }

        return -1; // No match found
    }

    private void CompareBinaryFiles(CancellationToken cancellationToken)
    {
        const int bytesPerLine = 16;
        
        using var leftStream = new FileStream(LeftPath, FileMode.Open, FileAccess.Read);
        using var rightStream = new FileStream(RightPath, FileMode.Open, FileAccess.Read);

        var leftBuffer = new byte[bytesPerLine];
        var rightBuffer = new byte[bytesPerLine];

        int lineNumber = 0;
        long offset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leftRead = leftStream.Read(leftBuffer, 0, bytesPerLine);
            var rightRead = rightStream.Read(rightBuffer, 0, bytesPerLine);

            if (leftRead == 0 && rightRead == 0)
                break;

            lineNumber++;

            var leftHex = FormatHexLine(offset, leftBuffer, leftRead);
            var rightHex = FormatHexLine(offset, rightBuffer, rightRead);

            bool isDifferent = leftRead != rightRead || 
                               !leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead));

            var diffType = isDifferent ? DiffType.Modified : DiffType.Equal;

            if (isDifferent)
                DiffCount++;

            AddLine(LeftLines, lineNumber, leftHex, leftRead > 0 ? diffType : DiffType.Empty);
            AddLine(RightLines, lineNumber, rightHex, rightRead > 0 ? diffType : DiffType.Empty);

            offset += bytesPerLine;
        }
    }

    private string FormatHexLine(long offset, byte[] buffer, int length)
    {
        if (length == 0)
            return "";

        var sb = new StringBuilder();
        sb.Append($"{offset:X8}  ");

        for (int i = 0; i < 16; i++)
        {
            if (i < length)
                sb.Append($"{buffer[i]:X2} ");
            else
                sb.Append("   ");

            if (i == 7)
                sb.Append(' ');
        }

        sb.Append(" |");
        for (int i = 0; i < length; i++)
        {
            var b = buffer[i];
            sb.Append(b >= 32 && b < 127 ? (char)b : '.');
        }
        sb.Append('|');

        return sb.ToString();
    }

    private void AddLine(ObservableCollection<DiffLine> collection, int lineNumber, string content, DiffType type)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            collection.Add(new DiffLine
            {
                LineNumber = lineNumber,
                Content = content,
                Type = type
            });
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void NextDiff()
    {
        if (DiffCount == 0)
            return;

        var diffLines = LeftLines.Select((line, index) => (line, index))
            .Where(x => x.line.Type != DiffType.Equal && x.line.Type != DiffType.Empty)
            .ToList();

        if (diffLines.Count == 0)
            return;

        CurrentDiffIndex = (CurrentDiffIndex + 1) % diffLines.Count;
        // Navigation will be handled by the view
    }

    [RelayCommand]
    private void PreviousDiff()
    {
        if (DiffCount == 0)
            return;

        var diffLines = LeftLines.Select((line, index) => (line, index))
            .Where(x => x.line.Type != DiffType.Equal && x.line.Type != DiffType.Empty)
            .ToList();

        if (diffLines.Count == 0)
            return;

        CurrentDiffIndex = CurrentDiffIndex <= 0 ? diffLines.Count - 1 : CurrentDiffIndex - 1;
        // Navigation will be handled by the view
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

public enum DiffType
{
    Equal,
    Added,
    Deleted,
    Modified,
    Empty
}

public class DiffLine
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DiffType Type { get; set; }

    public string LineNumberDisplay => Type == DiffType.Empty ? "" : LineNumber.ToString();
}

public class DiffSegment
{
    public DiffType Type { get; set; }
    public int Count { get; set; }
    public int LeftCount { get; set; }
    public int RightCount { get; set; }
}
