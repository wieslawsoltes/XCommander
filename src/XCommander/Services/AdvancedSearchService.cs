using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XCommander.Services;

/// <summary>
/// Implementation of advanced file search service
/// </summary>
public class AdvancedSearchService : IAdvancedSearchService
{
    private readonly string _savedQueriesPath;
    private readonly string _historyPath;
    private List<SavedSearchQuery> _savedQueries = new();
    private List<SearchHistoryEntry> _searchHistory = new();
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Image extensions for EXIF reading
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".tiff", ".tif", ".png", ".gif", ".bmp"
    };
    
    // Audio extensions for tag reading
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".flac", ".ogg", ".wav", ".wma", ".aac"
    };
    
    public AdvancedSearchService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XCommander");
        Directory.CreateDirectory(appData);
        
        _savedQueriesPath = Path.Combine(appData, "saved_searches.json");
        _historyPath = Path.Combine(appData, "search_history.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        LoadData();
    }
    
    private void LoadData()
    {
        try
        {
            if (File.Exists(_savedQueriesPath))
            {
                var json = File.ReadAllText(_savedQueriesPath);
                _savedQueries = JsonSerializer.Deserialize<List<SavedSearchQuery>>(json, _jsonOptions) ?? new();
            }
        }
        catch { _savedQueries = new(); }
        
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                _searchHistory = JsonSerializer.Deserialize<List<SearchHistoryEntry>>(json, _jsonOptions) ?? new();
            }
        }
        catch { _searchHistory = new(); }
    }
    
    #region Search Operations
    
    public async IAsyncEnumerable<AdvancedSearchResult> SearchAsync(
        AdvancedSearchCriteria criteria,
        IProgress<SearchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchPath = criteria.SearchPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        if (!Directory.Exists(searchPath))
            yield break;
        
        var filesScanned = 0;
        var foldersScanned = 0;
        var matchesFound = 0;
        
        // Build regex for file name pattern
        Regex? nameRegex = null;
        if (!string.IsNullOrEmpty(criteria.FileNamePattern))
        {
            var pattern = WildcardToRegex(criteria.FileNamePattern);
            nameRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        
        // Build regex for content search
        Regex? contentRegex = null;
        if (!string.IsNullOrEmpty(criteria.ContentPattern))
        {
            var pattern = criteria.ContentRegex 
                ? criteria.ContentPattern 
                : Regex.Escape(criteria.ContentPattern);
            var options = RegexOptions.Compiled;
            if (!criteria.ContentCaseSensitive) options |= RegexOptions.IgnoreCase;
            contentRegex = new Regex(pattern, options);
        }
        
        // Build exclude patterns
        var excludeRegexes = criteria.ExcludePatterns?
            .Select(p => new Regex(WildcardToRegex(p), RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList() ?? new List<Regex>();
        
        var excludeFolders = criteria.ExcludeFolders?.ToHashSet(StringComparer.OrdinalIgnoreCase) 
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        var searchOption = criteria.IncludeSubdirectories 
            ? SearchOption.AllDirectories 
            : SearchOption.TopDirectoryOnly;
        
        IEnumerable<string> files;
        try
        {
            files = EnumerateFilesWithExclusions(searchPath, searchOption, excludeFolders, cancellationToken);
        }
        catch (Exception)
        {
            yield break;
        }
        
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesScanned++;
            
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) continue;
            }
            catch
            {
                continue;
            }
            
            // Apply filters
            if (!MatchesCriteria(fileInfo, criteria, nameRegex, excludeRegexes))
                continue;
            
            // Content search
            List<ContentMatch>? contentMatches = null;
            if (contentRegex != null)
            {
                contentMatches = await SearchFileContentAsync(filePath, contentRegex, cancellationToken);
                if (contentMatches.Count == 0)
                    continue;
            }
            
            // Hash matching
            string? hash = null;
            if (!string.IsNullOrEmpty(criteria.FileHash))
            {
                hash = await CalculateHashAsync(filePath, criteria.HashAlgorithm, cancellationToken);
                if (!hash.Equals(criteria.FileHash, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            
            // EXIF matching
            ExifData? exif = null;
            if (criteria.ExifCriteria != null && ImageExtensions.Contains(fileInfo.Extension))
            {
                exif = ReadExifData(filePath);
                if (exif == null || !MatchesExifCriteria(exif, criteria.ExifCriteria))
                    continue;
            }
            
            // Audio tag matching
            AudioTags? audio = null;
            if (criteria.AudioCriteria != null && AudioExtensions.Contains(fileInfo.Extension))
            {
                audio = ReadAudioTags(filePath);
                if (audio == null || !MatchesAudioCriteria(audio, criteria.AudioCriteria))
                    continue;
            }
            
            matchesFound++;
            
            progress?.Report(new SearchProgress
            {
                CurrentPath = filePath,
                FilesScanned = filesScanned,
                FoldersScanned = foldersScanned,
                MatchesFound = matchesFound
            });
            
            yield return new AdvancedSearchResult
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                Size = fileInfo.Length,
                Modified = fileInfo.LastWriteTime,
                Created = fileInfo.CreationTime,
                Attributes = fileInfo.Attributes,
                FileHash = hash,
                Exif = exif,
                Audio = audio,
                ContentMatches = contentMatches
            };
        }
    }
    
    private static IEnumerable<string> EnumerateFilesWithExclusions(
        string path, 
        SearchOption option, 
        HashSet<string> excludeFolders,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(path);
        
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var currentDir = stack.Pop();
            
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                continue;
            }
            
            foreach (var file in files)
            {
                yield return file;
            }
            
            if (option == SearchOption.AllDirectories)
            {
                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                catch
                {
                    continue;
                }
                
                foreach (var subDir in subDirs)
                {
                    var dirName = Path.GetFileName(subDir);
                    if (!excludeFolders.Contains(dirName))
                    {
                        stack.Push(subDir);
                    }
                }
            }
        }
    }
    
    private bool MatchesCriteria(FileInfo file, AdvancedSearchCriteria criteria, Regex? nameRegex, List<Regex> excludeRegexes)
    {
        // Name pattern
        if (nameRegex != null && !nameRegex.IsMatch(file.Name))
            return false;
        
        // Exclude patterns
        if (excludeRegexes.Any(r => r.IsMatch(file.Name)))
            return false;
        
        // Size
        if (criteria.MinSize.HasValue && file.Length < criteria.MinSize.Value)
            return false;
        if (criteria.MaxSize.HasValue && file.Length > criteria.MaxSize.Value)
            return false;
        
        // Modified time
        if (criteria.ModifiedAfter.HasValue && file.LastWriteTime < criteria.ModifiedAfter.Value)
            return false;
        if (criteria.ModifiedBefore.HasValue && file.LastWriteTime > criteria.ModifiedBefore.Value)
            return false;
        
        // Created time
        if (criteria.CreatedAfter.HasValue && file.CreationTime < criteria.CreatedAfter.Value)
            return false;
        if (criteria.CreatedBefore.HasValue && file.CreationTime > criteria.CreatedBefore.Value)
            return false;
        
        // Extensions
        if (criteria.AllowedExtensions?.Count > 0)
        {
            if (!criteria.AllowedExtensions.Any(e => 
                file.Extension.Equals(e, StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals("." + e.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        
        if (criteria.ExcludedExtensions?.Count > 0)
        {
            if (criteria.ExcludedExtensions.Any(e => 
                file.Extension.Equals(e, StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals("." + e.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        
        // Attributes
        if (criteria.IsReadOnly.HasValue && ((file.Attributes & FileAttributes.ReadOnly) != 0) != criteria.IsReadOnly.Value)
            return false;
        if (criteria.IsHidden.HasValue && ((file.Attributes & FileAttributes.Hidden) != 0) != criteria.IsHidden.Value)
            return false;
        if (criteria.IsSystem.HasValue && ((file.Attributes & FileAttributes.System) != 0) != criteria.IsSystem.Value)
            return false;
        if (criteria.IsArchive.HasValue && ((file.Attributes & FileAttributes.Archive) != 0) != criteria.IsArchive.Value)
            return false;
        
        return true;
    }
    
    private static async Task<List<ContentMatch>> SearchFileContentAsync(string filePath, Regex regex, CancellationToken cancellationToken)
    {
        var matches = new List<ContentMatch>();
        
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            
            for (var i = 0; i < lines.Length; i++)
            {
                var match = regex.Match(lines[i]);
                if (match.Success)
                {
                    matches.Add(new ContentMatch
                    {
                        LineNumber = i + 1,
                        LineContent = lines[i],
                        MatchStart = match.Index,
                        MatchLength = match.Length
                    });
                }
            }
        }
        catch
        {
            // Binary file or access denied
        }
        
        return matches;
    }
    
    private static bool MatchesExifCriteria(ExifData exif, ExifSearchCriteria criteria)
    {
        if (!string.IsNullOrEmpty(criteria.CameraMake) && 
            (exif.CameraMake == null || !exif.CameraMake.Contains(criteria.CameraMake, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (!string.IsNullOrEmpty(criteria.CameraModel) && 
            (exif.CameraModel == null || !exif.CameraModel.Contains(criteria.CameraModel, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (criteria.DateTakenAfter.HasValue && (!exif.DateTaken.HasValue || exif.DateTaken < criteria.DateTakenAfter))
            return false;
        if (criteria.DateTakenBefore.HasValue && (!exif.DateTaken.HasValue || exif.DateTaken > criteria.DateTakenBefore))
            return false;
        
        if (criteria.MinWidth.HasValue && (!exif.Width.HasValue || exif.Width < criteria.MinWidth))
            return false;
        if (criteria.MaxWidth.HasValue && (!exif.Width.HasValue || exif.Width > criteria.MaxWidth))
            return false;
        
        if (criteria.MinHeight.HasValue && (!exif.Height.HasValue || exif.Height < criteria.MinHeight))
            return false;
        if (criteria.MaxHeight.HasValue && (!exif.Height.HasValue || exif.Height > criteria.MaxHeight))
            return false;
        
        return true;
    }
    
    private static bool MatchesAudioCriteria(AudioTags audio, AudioTagSearchCriteria criteria)
    {
        if (!string.IsNullOrEmpty(criteria.Title) && 
            (audio.Title == null || !audio.Title.Contains(criteria.Title, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (!string.IsNullOrEmpty(criteria.Artist) && 
            (audio.Artist == null || !audio.Artist.Contains(criteria.Artist, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (!string.IsNullOrEmpty(criteria.Album) && 
            (audio.Album == null || !audio.Album.Contains(criteria.Album, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (!string.IsNullOrEmpty(criteria.Genre) && 
            (audio.Genre == null || !audio.Genre.Contains(criteria.Genre, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        if (criteria.Year.HasValue && audio.Year != criteria.Year)
            return false;
        if (criteria.MinYear.HasValue && (!audio.Year.HasValue || audio.Year < criteria.MinYear))
            return false;
        if (criteria.MaxYear.HasValue && (!audio.Year.HasValue || audio.Year > criteria.MaxYear))
            return false;
        
        if (criteria.MinDurationSeconds.HasValue && 
            (!audio.Duration.HasValue || audio.Duration.Value.TotalSeconds < criteria.MinDurationSeconds))
            return false;
        if (criteria.MaxDurationSeconds.HasValue && 
            (!audio.Duration.HasValue || audio.Duration.Value.TotalSeconds > criteria.MaxDurationSeconds))
            return false;
        
        if (criteria.MinBitrate.HasValue && (!audio.Bitrate.HasValue || audio.Bitrate < criteria.MinBitrate))
            return false;
        if (criteria.MaxBitrate.HasValue && (!audio.Bitrate.HasValue || audio.Bitrate > criteria.MaxBitrate))
            return false;
        
        return true;
    }
    
    private static string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".")
            + "$";
    }
    
    #endregion
    
    #region Duplicate Search
    
    public async Task<IReadOnlyList<DuplicateFileGroup>> FindDuplicatesAsync(
        string searchPath,
        bool includeSubdirectories = true,
        long? minSize = null,
        IProgress<SearchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var filesBySize = new Dictionary<long, List<string>>();
        var filesScanned = 0;
        
        // Group files by size
        var searchOption = includeSubdirectories 
            ? SearchOption.AllDirectories 
            : SearchOption.TopDirectoryOnly;
        
        foreach (var file in EnumerateFilesWithExclusions(searchPath, searchOption, new HashSet<string>(), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesScanned++;
            
            try
            {
                var info = new FileInfo(file);
                if (minSize.HasValue && info.Length < minSize.Value)
                    continue;
                
                if (!filesBySize.TryGetValue(info.Length, out var list))
                {
                    list = new List<string>();
                    filesBySize[info.Length] = list;
                }
                list.Add(file);
                
                progress?.Report(new SearchProgress
                {
                    CurrentPath = file,
                    FilesScanned = filesScanned,
                    MatchesFound = filesBySize.Values.Count(l => l.Count > 1)
                });
            }
            catch { }
        }
        
        // Now hash files with same size
        var duplicates = new List<DuplicateFileGroup>();
        
        foreach (var (size, files) in filesBySize.Where(kvp => kvp.Value.Count > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filesByHash = new Dictionary<string, List<string>>();
            
            foreach (var file in files)
            {
                try
                {
                    var hash = await CalculateHashAsync(file, HashAlgorithmType.MD5, cancellationToken);
                    
                    if (!filesByHash.TryGetValue(hash, out var hashFiles))
                    {
                        hashFiles = new List<string>();
                        filesByHash[hash] = hashFiles;
                    }
                    hashFiles.Add(file);
                }
                catch { }
            }
            
            foreach (var (hash, hashFiles) in filesByHash.Where(kvp => kvp.Value.Count > 1))
            {
                duplicates.Add(new DuplicateFileGroup
                {
                    Hash = hash,
                    FileSize = size,
                    FilePaths = hashFiles
                });
            }
        }
        
        return duplicates.OrderByDescending(d => d.WastedSpace).ToList();
    }
    
    #endregion
    
    #region Hash Calculation
    
    public async Task<string> CalculateHashAsync(string filePath, HashAlgorithmType algorithm, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filePath);
        using var hashAlgorithm = algorithm switch
        {
            HashAlgorithmType.MD5 => (HashAlgorithm)MD5.Create(),
            HashAlgorithmType.SHA1 => SHA1.Create(),
            HashAlgorithmType.SHA256 => SHA256.Create(),
            HashAlgorithmType.SHA512 => SHA512.Create(),
            HashAlgorithmType.CRC32 => new Crc32HashAlgorithm(),
            _ => MD5.Create()
        };
        
        var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    #endregion
    
    #region EXIF Reading
    
    public ExifData? ReadExifData(string filePath)
    {
        try
        {
            // Simple EXIF reader - reads basic JPEG EXIF data
            // In a production app, you'd use a library like MetadataExtractor
            
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            
            // Check for JPEG
            if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
                return null;
            
            var exif = new ExifData();
            
            while (stream.Position < stream.Length - 2)
            {
                if (reader.ReadByte() != 0xFF) continue;
                
                var marker = reader.ReadByte();
                
                if (marker == 0xE1) // APP1 (EXIF)
                {
                    var length = (reader.ReadByte() << 8) | reader.ReadByte();
                    var data = reader.ReadBytes(length - 2);
                    
                    // Parse EXIF header
                    var exifStr = Encoding.ASCII.GetString(data, 0, Math.Min(4, data.Length));
                    if (exifStr == "Exif")
                    {
                        // Basic parsing - in production use proper library
                        var text = Encoding.ASCII.GetString(data);
                        
                        // Try to extract some basic info
                        exif = new ExifData
                        {
                            Width = ExtractInt(text, "ImageWidth"),
                            Height = ExtractInt(text, "ImageLength")
                        };
                    }
                    break;
                }
                else if (marker == 0xC0 || marker == 0xC2) // SOF0 or SOF2
                {
                    var length = (reader.ReadByte() << 8) | reader.ReadByte();
                    reader.ReadByte(); // precision
                    var height = (reader.ReadByte() << 8) | reader.ReadByte();
                    var width = (reader.ReadByte() << 8) | reader.ReadByte();
                    
                    exif = new ExifData
                    {
                        Width = width,
                        Height = height
                    };
                    break;
                }
                else if (marker >= 0xE0 && marker <= 0xEF) // APP markers
                {
                    var length = (reader.ReadByte() << 8) | reader.ReadByte();
                    stream.Seek(length - 2, SeekOrigin.Current);
                }
            }
            
            return exif;
        }
        catch
        {
            return null;
        }
    }
    
    private static int? ExtractInt(string text, string fieldName)
    {
        var idx = text.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        
        // Simple extraction - not production quality
        return null;
    }
    
    #endregion
    
    #region Audio Tags Reading
    
    public AudioTags? ReadAudioTags(string filePath)
    {
        try
        {
            // Simple ID3v1 reader for MP3 files
            // In production, use a library like TagLibSharp
            
            using var stream = File.OpenRead(filePath);
            
            // Check for ID3v1 at end of file
            if (stream.Length < 128) return null;
            
            stream.Seek(-128, SeekOrigin.End);
            using var reader = new BinaryReader(stream);
            
            var tag = Encoding.ASCII.GetString(reader.ReadBytes(3));
            if (tag != "TAG") return null;
            
            var title = Encoding.ASCII.GetString(reader.ReadBytes(30)).TrimEnd('\0', ' ');
            var artist = Encoding.ASCII.GetString(reader.ReadBytes(30)).TrimEnd('\0', ' ');
            var album = Encoding.ASCII.GetString(reader.ReadBytes(30)).TrimEnd('\0', ' ');
            var yearStr = Encoding.ASCII.GetString(reader.ReadBytes(4)).TrimEnd('\0', ' ');
            var comment = reader.ReadBytes(30);
            var genreByte = reader.ReadByte();
            
            int? track = null;
            if (comment[28] == 0 && comment[29] != 0)
            {
                track = comment[29];
            }
            
            int? year = int.TryParse(yearStr, out var y) ? y : null;
            
            return new AudioTags
            {
                Title = string.IsNullOrEmpty(title) ? null : title,
                Artist = string.IsNullOrEmpty(artist) ? null : artist,
                Album = string.IsNullOrEmpty(album) ? null : album,
                Year = year,
                Track = track,
                Genre = genreByte < GenreNames.Length ? GenreNames[genreByte] : null
            };
        }
        catch
        {
            return null;
        }
    }
    
    private static readonly string[] GenreNames = new[]
    {
        "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop",
        "Jazz", "Metal", "New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae",
        "Rock", "Techno", "Industrial", "Alternative", "Ska", "Death Metal", "Pranks",
        "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk", "Fusion",
        "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip",
        "Gospel", "Noise", "AlternRock", "Bass", "Soul", "Punk", "Space", "Meditative",
        "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic", "Darkwave",
        "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock",
        "Comedy", "Cult", "Gangsta", "Top 40", "Christian Rap", "Pop/Funk", "Jungle",
        "Native American", "Cabaret", "New Wave", "Psychedelic", "Rave", "Showtunes",
        "Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro",
        "Musical", "Rock & Roll", "Hard Rock"
    };
    
    #endregion
    
    #region Saved Queries
    
    public Task SaveQueryAsync(SavedSearchQuery query)
    {
        var existing = _savedQueries.FirstOrDefault(q => q.Id == query.Id);
        if (existing != null)
        {
            _savedQueries.Remove(existing);
        }
        _savedQueries.Add(query);
        return SaveQueriesAsync();
    }
    
    public Task<IReadOnlyList<SavedSearchQuery>> GetSavedQueriesAsync()
    {
        return Task.FromResult<IReadOnlyList<SavedSearchQuery>>(
            _savedQueries.OrderByDescending(q => q.LastUsed).ToList());
    }
    
    public Task DeleteQueryAsync(string queryId)
    {
        _savedQueries.RemoveAll(q => q.Id == queryId);
        return SaveQueriesAsync();
    }
    
    public Task UpdateQueryUsageAsync(string queryId)
    {
        var query = _savedQueries.FirstOrDefault(q => q.Id == queryId);
        if (query != null)
        {
            var index = _savedQueries.IndexOf(query);
            _savedQueries[index] = query with
            {
                LastUsed = DateTime.Now,
                UseCount = query.UseCount + 1
            };
            return SaveQueriesAsync();
        }
        return Task.CompletedTask;
    }
    
    private Task SaveQueriesAsync()
    {
        var json = JsonSerializer.Serialize(_savedQueries, _jsonOptions);
        return File.WriteAllTextAsync(_savedQueriesPath, json);
    }
    
    #endregion
    
    #region Search History
    
    public Task AddToHistoryAsync(SearchHistoryEntry entry)
    {
        _searchHistory.Insert(0, entry);
        
        // Keep only last 100 entries
        while (_searchHistory.Count > 100)
        {
            _searchHistory.RemoveAt(_searchHistory.Count - 1);
        }
        
        return SaveHistoryAsync();
    }
    
    public Task<IReadOnlyList<SearchHistoryEntry>> GetSearchHistoryAsync(int maxEntries = 100)
    {
        return Task.FromResult<IReadOnlyList<SearchHistoryEntry>>(
            _searchHistory.Take(maxEntries).ToList());
    }
    
    public Task ClearHistoryAsync()
    {
        _searchHistory.Clear();
        return SaveHistoryAsync();
    }
    
    private Task SaveHistoryAsync()
    {
        var json = JsonSerializer.Serialize(_searchHistory, _jsonOptions);
        return File.WriteAllTextAsync(_historyPath, json);
    }
    
    #endregion
}

/// <summary>
/// Simple CRC32 implementation as HashAlgorithm
/// </summary>
internal class Crc32HashAlgorithm : HashAlgorithm
{
    private static readonly uint[] Table = CreateTable();
    private uint _hash = 0xFFFFFFFF;
    
    public override int HashSize => 32;
    
    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }
    
    public override void Initialize()
    {
        _hash = 0xFFFFFFFF;
    }
    
    protected override void HashCore(byte[] array, int ibStart, int cbSize)
    {
        for (var i = ibStart; i < ibStart + cbSize; i++)
        {
            _hash = Table[(_hash ^ array[i]) & 0xFF] ^ (_hash >> 8);
        }
    }
    
    protected override byte[] HashFinal()
    {
        _hash ^= 0xFFFFFFFF;
        return BitConverter.GetBytes(_hash);
    }
}
