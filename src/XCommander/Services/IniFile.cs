using System.Text;

namespace XCommander.Services;

/// <summary>
/// Minimal INI reader/writer compatible with Total Commander wincmd.ini format.
/// Preserves unknown sections/keys but does not retain comments or ordering.
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Dictionary<string, string>> Sections => _sections;

    public static IniFile Load(string path, Encoding? encoding = null)
    {
        var ini = new IniFile();
        if (!File.Exists(path))
            return ini;

        var content = File.ReadAllText(path, encoding ?? Encoding.Default);
        ini.Parse(content);
        return ini;
    }

    public void Save(string path, Encoding? encoding = null)
    {
        var sb = new StringBuilder();
        foreach (var section in _sections.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append('[').Append(section.Key).Append(']').AppendLine();
            foreach (var kvp in section.Value.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(kvp.Key).Append('=').Append(kvp.Value).AppendLine();
            }
            sb.AppendLine();
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, sb.ToString(), encoding ?? Encoding.Default);
    }

    public Dictionary<string, string> GetSection(string sectionName, bool createIfMissing = false)
    {
        if (_sections.TryGetValue(sectionName, out var section))
            return section;

        if (!createIfMissing)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var created = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _sections[sectionName] = created;
        return created;
    }

    public string? GetValue(string sectionName, string key)
    {
        var section = GetSection(sectionName, false);
        return section.TryGetValue(key, out var value) ? value : null;
    }

    public void SetValue(string sectionName, string key, string value)
    {
        var section = GetSection(sectionName, true);
        section[key] = value;
    }

    private void Parse(string content)
    {
        var currentSectionName = string.Empty;
        var currentSection = GetSection(currentSectionName, true);

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSectionName = trimmed[1..^1].Trim();
                currentSection = GetSection(currentSectionName, true);
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
                continue;

            var key = trimmed[..equalsIndex].Trim();
            var value = trimmed[(equalsIndex + 1)..].Trim();
            if (key.Length == 0)
                continue;

            currentSection[key] = value;
        }
    }
}
