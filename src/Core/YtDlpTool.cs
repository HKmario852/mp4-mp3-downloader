using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
namespace Omni.Core;

/// <summary>
/// Picks which yt-dlp.exe to run and keeps it current between app releases. Sites change often enough that a
/// bundled copy goes stale within weeks, so newer yt-dlp releases are downloaded into the data folder (never into
/// the install folder, which app updates replace). Whichever copy is newer, bundled or downloaded, is used.
/// </summary>
public sealed class YtDlpTool
{
    public const string Repository = "yt-dlp/yt-dlp";
    static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    readonly string bundledPath, toolsDir;
    readonly HttpClient client;
    readonly Func<string, CancellationToken, Task<string?>> probe;
    readonly SemaphoreSlim updating = new(1);
    volatile string path;
    public YtDlpTool(string bundledDir, string toolsDir, HttpClient? client = null, Func<string, CancellationToken, Task<string?>>? probe = null)
    {
        bundledPath = Path.Combine(bundledDir, "yt-dlp.exe"); this.toolsDir = toolsDir; path = bundledPath;
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        if (client is null) this.client.DefaultRequestHeaders.UserAgent.ParseAdd("OmniDownloader");
        this.probe = probe ?? ProbeVersion;
    }
    string UpdatedPath => Path.Combine(toolsDir, "yt-dlp.exe");
    string UpdatedVersionFile => Path.Combine(toolsDir, "yt-dlp.version");
    string CheckedStamp => Path.Combine(toolsDir, "yt-dlp.checked");
    /// <summary>The yt-dlp.exe to run.</summary>
    public string ExecutablePath => path;
    public string? Version { get; private set; }

    /// <summary>Reads both versions and picks the newer copy. Cheap after the first call per copy.</summary>
    public async Task Refresh(CancellationToken ct = default)
    {
        var bundled = File.Exists(bundledPath) ? await probe(bundledPath, ct) : null;
        var updated = File.Exists(UpdatedPath) && File.Exists(UpdatedVersionFile) ? File.ReadAllText(UpdatedVersionFile).Trim() : null;
        if (updated is not null && IsNewer(updated, bundled)) { path = UpdatedPath; Version = updated; }
        else { path = bundledPath; Version = bundled; }
    }

    /// <summary>Checks GitHub at most once a day (used at startup).</summary>
    public async Task<YtDlpUpdate?> UpdateIfDue(CancellationToken ct = default)
    {
        if (File.Exists(CheckedStamp) && DateTime.UtcNow - File.GetLastWriteTimeUtc(CheckedStamp) < CheckInterval) return null;
        return await Update(ct);
    }

    /// <summary>Downloads the latest yt-dlp release if it is newer than the copy in use.</summary>
    public async Task<YtDlpUpdate> Update(CancellationToken ct = default)
    {
        await updating.WaitAsync(ct);
        try
        {
            if (Version is null) await Refresh(ct);
            using var release = JsonDocument.Parse(await client.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct));
            var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
            Directory.CreateDirectory(toolsDir); File.WriteAllText(CheckedStamp, tag);
            if (!IsNewer(tag, Version)) return new(false, Version);
            var name = RuntimeInformation.ProcessArchitecture switch { Architecture.Arm64 => "yt-dlp_arm64.exe", Architecture.X86 => "yt-dlp_x86.exe", _ => "yt-dlp.exe" };
            var asset = release.RootElement.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == name);
            if (asset.ValueKind != JsonValueKind.Object) throw new IOException("yt-dlp release has no " + name);
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            // Same rule as the app updater and Prepare-Tools.ps1: no GitHub SHA-256, no download.
            if (digest is null || !System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) throw new IOException("yt-dlp release asset has no SHA-256; update stopped");
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!url.StartsWith($"https://github.com/{Repository}/releases/download/", StringComparison.Ordinal)) throw new IOException("yt-dlp download URL is not from the yt-dlp releases");
            var temp = UpdatedPath + ".tmp";
            try
            {
                await using (var input = await client.GetStreamAsync(url, ct))
                await using (var output = File.Create(temp)) await input.CopyToAsync(output, ct);
                string actual; await using (var check = File.OpenRead(temp)) actual = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
                if (!actual.Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new IOException("yt-dlp SHA-256 mismatch; update stopped");
                var downloaded = await probe(temp, ct);
                if (downloaded is null || !IsNewer(downloaded, Version)) throw new IOException("Downloaded yt-dlp did not report a newer version");
                // Fails while a download is using the old downloaded copy; the next check retries.
                File.Move(temp, UpdatedPath, true); File.WriteAllText(UpdatedVersionFile, downloaded);
                path = UpdatedPath; Version = downloaded;
                return new(true, downloaded);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { updating.Release(); }
    }

    /// <summary>yt-dlp versions are dates with an optional build number: 2026.08.19 or 2026.08.19.1.</summary>
    public static bool IsNewer(string candidate, string? current)
    {
        static int[]? Parts(string? v) { if (string.IsNullOrWhiteSpace(v)) return null; var p = v.Trim().TrimStart('v').Split('.'); var n = new int[p.Length]; for (int i = 0; i < p.Length; i++) if (!int.TryParse(p[i], out n[i])) return null; return n; }
        var a = Parts(candidate); var b = Parts(current);
        if (a is null) return false; if (b is null) return true;
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++) { int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0; if (x != y) return x > y; }
        return false;
    }

    static async Task<string?> ProbeVersion(string exe, CancellationToken ct)
    {
        try { var text = (await ProcessRunner.Run(exe, ["--version"], null, ct)).Trim(); return Parts(text) ? text : null; }
        catch (Exception e) when (e is IOException or DownloadException or System.ComponentModel.Win32Exception) { return null; }
        static bool Parts(string v) => System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d{4}\.\d{2}\.\d{2}(\.\d+)?$");
    }
}
/// <summary>Result of an update check: whether a newer yt-dlp was installed, and the version now in use.</summary>
public sealed record YtDlpUpdate(bool Updated, string? Version);
