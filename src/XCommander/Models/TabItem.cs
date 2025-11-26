namespace XCommander.Models;

public class TabItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Path { get; set; }
    public string Title => System.IO.Path.GetFileName(Path) is { Length: > 0 } name ? name : Path;
    public bool IsLocked { get; set; }
    public List<string> History { get; } = [];
    public int HistoryIndex { get; set; } = -1;
}
