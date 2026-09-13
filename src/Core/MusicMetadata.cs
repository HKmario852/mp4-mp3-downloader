using System.Text.Json;
namespace Omni.Core;
public sealed class MusicMetadata
{
    static readonly HttpClient Http = new(new SocketsHttpHandler { MaxConnectionsPerServer = 5 }) { Timeout = TimeSpan.FromSeconds(20) };
    static readonly SemaphoreSlim Pool = new(5), Rate = new(1); static DateTimeOffset next;
    static MusicMetadata() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("OmniDownloader/0.1.0 (local open-source music tagger)");
    public async Task<Cover?> Find(string title, string artist, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(artist)) return null;
        await Pool.WaitAsync(ct);
        try
        {
            await Rate.WaitAsync(ct);
            string json;
            try
            {
                var delay = next - DateTimeOffset.UtcNow; if (delay > TimeSpan.Zero) await Task.Delay(delay, ct); next = DateTimeOffset.UtcNow.AddSeconds(1);
                var query = Uri.EscapeDataString($"recording:\"{title.Replace("\"", "")}\" AND artist:\"{artist.Replace("\"", "")}\"");
                using var response = await Http.GetAsync("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=5&query=" + query, ct);
                if ((int)response.StatusCode is 429 or 503) { next = DateTimeOffset.UtcNow.AddSeconds(5); return null; }
                if (!response.IsSuccessStatusCode) return null; json = await response.Content.ReadAsStringAsync(ct);
            }
            finally { Rate.Release(); }
            using var doc = JsonDocument.Parse(json);
            var matches = doc.RootElement.GetProperty("recordings").EnumerateArray().Where(r => r.GetProperty("title").GetString()?.Equals(title, StringComparison.OrdinalIgnoreCase) == true && r.TryGetProperty("artist-credit", out var credits) && credits.EnumerateArray().Any(a => a.GetProperty("name").GetString()?.Equals(artist, StringComparison.OrdinalIgnoreCase) == true)).ToArray();
            if (matches.Length != 1 || !matches[0].TryGetProperty("releases", out var releases)) return null;
            var release = releases.EnumerateArray().FirstOrDefault(); if (release.ValueKind == JsonValueKind.Undefined) return null;
            using var caa = await Http.GetAsync($"https://coverartarchive.org/release/{release.GetProperty("id").GetString()}", ct); if (!caa.IsSuccessStatusCode) return null;
            using var covers = JsonDocument.Parse(await caa.Content.ReadAsStringAsync(ct));
            foreach (var img in covers.RootElement.GetProperty("images").EnumerateArray())
                if (img.GetProperty("front").GetBoolean() && img.GetProperty("approved").GetBoolean()) return await Fetch(img.GetProperty("image").GetString()!, "Album front", 3, ct);
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested) { return null; }
        finally { Pool.Release(); }
    }
    public static async Task<Cover?> Fetch(string url, string description, byte type, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return null;
        if (u.Scheme == "http") u = new UriBuilder(u) { Scheme = "https", Port = -1 }.Uri;
        using var response = await Http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 32 * 1024 * 1024) throw new IOException("封面大於 32MB");
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[65536]; int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + n > 32 * 1024 * 1024) throw new IOException("封面大於 32MB"); output.Write(buffer, 0, n); }
        var bytes = output.ToArray(); var mime = bytes.Length > 3 && bytes[0] == 255 && bytes[1] == 216 ? "image/jpeg" : bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 ? "image/png" : bytes.Length > 12 && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? "image/webp" : null;
        return mime is null ? null : new(bytes, mime, description, type);
    }
}
