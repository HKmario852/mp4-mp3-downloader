using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Omni.Core;

public enum MusicLookupState { Matched, MatchedNoCover, NoMatch, Ambiguous, Unavailable }
public sealed record MusicLookup(MusicLookupState State, string? Title = null, string? Artist = null, string? Album = null, Cover? Cover = null)
{
    public string Message => State switch {
        MusicLookupState.Matched => "MusicBrainz：已配對，專輯封面作為備用封面",
        MusicLookupState.MatchedNoCover => "MusicBrainz：已配對標籤，未取得專輯封面",
        MusicLookupState.Ambiguous => "MusicBrainz：有多個可能結果，已保留來源資料",
        MusicLookupState.Unavailable => "MusicBrainz：服務暫時無法使用，已保留來源資料",
        _ => "MusicBrainz：查無可靠配對，已保留來源資料"
    };
}
public sealed record MusicQuery(string Title, string Artist, double? Duration);
public sealed class MusicMetadata
{
    static readonly HttpClient Http = new(new SocketsHttpHandler { MaxConnectionsPerServer = 5 }) { Timeout = TimeSpan.FromSeconds(20) };
    static readonly SemaphoreSlim Pool = new(5), Rate = new(1); static DateTimeOffset next;
    readonly HttpClient client;
    static MusicMetadata() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("MP4MP3Downloader/0.1.3 (https://github.com/HKmario852)");
    public MusicMetadata(HttpClient? client = null) => this.client = client ?? Http;
    public static MusicQuery Prepare(string title, string artist, double? duration)
    {
        var original = title.Trim();
        title = Regex.Replace(original, @"\s*[\(\[【](?:(?:official|music|lyrics?|audio|video|mv|hd|4k|visuali[sz]er|字幕|歌詞|官方)\s*)+[\)\]】]\s*$", "", RegexOptions.IgnoreCase).Trim();
        var parts = Regex.Split(title, @"\s+[-–—]\s+", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (parts.Length == 2 && parts.All(p => p.Length > 0) && (string.IsNullOrWhiteSpace(artist) || Normalize(parts[0]) == Normalize(artist))) { artist = parts[0]; title = parts[1]; }
        return new(title.Length > 0 ? title : original, artist.Trim(), duration);
    }
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToUpperInvariant();
    static string Text(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    static double? Number(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
    static JsonElement[] Array(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Object).ToArray() : [];
    static string[] Artists(JsonElement r) => Array(r, "artist-credit").Select(a => Text(a, "name") is { Length: > 0 } name ? name : a.TryGetProperty("artist", out var artist) ? Text(artist, "name") : "").Where(s => s.Length > 0).ToArray();
    static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    public static JsonElement[] Candidates(JsonElement root, MusicQuery query)
    {
        return Array(root, "recordings").Where(r => {
            if (Normalize(Text(r, "title")) != Normalize(query.Title)) return false;
            var artists = Artists(r); if (artists.Length == 0) return false;
            if (query.Artist.Length > 0) return artists.Any(a => Normalize(a) == Normalize(query.Artist)) || Normalize(string.Join(" & ", artists)) == Normalize(query.Artist);
            // Title-only searches need duration as additional evidence; a single search hit alone is insufficient.
            return (Number(r,"score") ?? 0) >= 95 && query.Duration is > 0 && Number(r,"length") is double ms && Math.Abs(ms / 1000 - query.Duration.Value) <= Math.Max(8, query.Duration.Value * .04);
        }).OrderBy(r => query.Duration is double d && Number(r,"length") is double ms ? Math.Abs(ms / 1000 - d) : double.MaxValue).ToArray();
    }
    async Task<string> Search(MusicQuery query, CancellationToken ct)
    {
        var q = $"recording:\"{Escape(query.Title)}\"" + (query.Artist.Length == 0 ? "" : $" AND artist:\"{Escape(query.Artist)}\"");
        for (int attempt = 0; ; attempt++)
        {
            await Rate.WaitAsync(ct);
            try {
                var delay = next - DateTimeOffset.UtcNow; if (delay > TimeSpan.Zero) await Task.Delay(delay,ct);
                next = DateTimeOffset.UtcNow.AddSeconds(1);
                using var response = await client.GetAsync("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=25&query=" + Uri.EscapeDataString(q),ct);
                if ((int)response.StatusCode is 429 or 503) {
                    var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(5);
                    next = DateTimeOffset.UtcNow + (retry < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : retry);
                    if (attempt == 0 && retry <= TimeSpan.FromSeconds(30)) continue;
                }
                response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync(ct);
            } finally { Rate.Release(); }
        }
    }
    public async Task<MusicLookup> Lookup(string title, string artist, double? duration, CancellationToken ct)
    {
        await Pool.WaitAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(60)); var token = deadline.Token;
        try {
            var query = Prepare(title,artist,duration);
            using var doc = JsonDocument.Parse(await Search(query,token));
            var matches = Candidates(doc.RootElement,query);
            if (matches.Length == 0) return new(MusicLookupState.NoMatch);
            if (matches.Select(r => Normalize(string.Join(" & ",Artists(r)))).Distinct().Count() != 1) return new(MusicLookupState.Ambiguous);
            var match = matches[0]; var matchTitle = Text(match,"title"); var matchArtist = string.Join(" & ",Artists(match));
            // Recordings may have several releases; a missing first release cover must not stop the search.
            var releases = matches.SelectMany(r => Array(r,"releases")).Where(r => Text(r,"status") is "" or "Official")
                .OrderByDescending(r => r.TryGetProperty("release-group",out var group) && Text(group,"primary-type") == "Album")
                .DistinctBy(r => Text(r,"id")).Take(5).ToArray();
            foreach (var release in releases) {
                if (!Guid.TryParse(Text(release,"id"),out var id)) continue;
                try {
                    using var response = await client.GetAsync($"https://coverartarchive.org/release/{id}",token);
                    if (!response.IsSuccessStatusCode) continue;
                    using var images = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    foreach (var image in Array(images.RootElement,"images")) {
                        if (!image.TryGetProperty("front",out var front) || front.ValueKind != JsonValueKind.True || !image.TryGetProperty("approved",out var approved) || approved.ValueKind != JsonValueKind.True) continue;
                        var cover = await FetchWith(client,Text(image,"image"),"Album front",3,token);
                        if (cover is not null) return new(MusicLookupState.Matched,matchTitle,matchArtist,Text(release,"title"),cover);
                    }
                } catch (Exception e) when (e is HttpRequestException or IOException or JsonException) { }
            }
            return new(MusicLookupState.MatchedNoCover,matchTitle,matchArtist,releases.Length > 0 ? Text(releases[0],"title") : null);
        }
        catch (Exception e) when ((e is HttpRequestException or IOException or JsonException or OperationCanceledException) && !ct.IsCancellationRequested) { return new(MusicLookupState.Unavailable); }
        finally { Pool.Release(); }
    }
    public static Task<Cover?> Fetch(string url,string description,byte type,CancellationToken ct) => FetchWith(Http,url,description,type,ct);
    static async Task<Cover?> FetchWith(HttpClient client,string url, string description, byte type, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return null;
        if (u.Scheme == "http") u = new UriBuilder(u) { Scheme = "https", Port = -1 }.Uri;
        using var response = await client.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 32 * 1024 * 1024) throw new IOException("封面大於 32MB");
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[65536]; int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + n > 32 * 1024 * 1024) throw new IOException("封面大於 32MB"); output.Write(buffer, 0, n); }
        var bytes = output.ToArray(); var mime = bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216 ? "image/jpeg" : bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 ? "image/png" : bytes.Length > 12 && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? "image/webp" : null;
        return mime is null ? null : new(bytes, mime, description, type);
    }
}
