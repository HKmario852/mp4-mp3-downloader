namespace Omni.Core;

public sealed record TagLibrarySource(string Path, bool IsFolder);
public sealed record TagLibraryRead(DownloadJob[] Songs, string[] Unavailable);

public static class TagLibrary
{
    public static TagLibrarySource Source(string path) => new(
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)), Directory.Exists(path));

    // Sources are persisted independently of download history. Always read tags from disk.
    public static TagLibraryRead Read(IEnumerable<TagLibrarySource> sources)
    {
        var songs = new List<DownloadJob>();
        var unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            try
            {
                if (source.IsFolder && !Directory.Exists(source.Path)) { unavailable.Add(source.Path); continue; }
                var files = source.IsFolder
                    ? Directory.EnumerateFiles(source.Path, "*.mp3", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                    : new[] { source.Path };
                foreach (var file in files)
                {
                    var path = System.IO.Path.GetFullPath(file);
                    if (!System.IO.Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase) || !seen.Add(path)) continue;
                    try
                    {
                        var doc = Id3Document.Read(path);
                        songs.Add(new DownloadJob { FilePath = path, Title = string.IsNullOrEmpty(doc.Text("TIT2")) ? System.IO.Path.GetFileNameWithoutExtension(path) : doc.Text("TIT2"), Artist = doc.Text("TPE1"), Album = doc.Text("TALB"), Mode = DownloadMode.Mp3, OutputFormat = "mp3", State = JobState.Completed, CompletedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) });
                    }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { unavailable.Add(path); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { unavailable.Add(source.Path); }
        }
        return new(songs.ToArray(), unavailable.ToArray());
    }
}
