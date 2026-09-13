using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omni.Core;
public enum JobState { PendingChoice, Queued, Analyzing, Downloading, Processing, RetryWait, Paused, Completed, Failed, Cancelled }
public enum DownloadMode { Mp4, Mp3 }
public sealed record BrowserCookie(string Name, string Value, string Domain, string Path, bool Secure, bool HttpOnly, bool HostOnly, double? ExpirationDate);
public sealed record IntakeRequest(string RequestId, string Url, string Mode, List<BrowserCookie>? Cookies = null);
public sealed record IntakeAck(bool Ok, string RequestId, string State, string? Error = null);
public sealed class Preferences
{
    public int Concurrency { get; set; } = 5;
    public int VideoHeight { get; set; } = 1080; // 0 = best
    public int AudioKbps { get; set; } = 320;
    public bool CleanTitle { get; set; } = true;
    public bool MusicBrainz { get; set; }
    public bool WifiOnly { get; set; } = true;
    public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Omni");
    public string? Mp4Directory { get; set; }
    public string? Mp3Directory { get; set; }
    public string DirectoryFor(DownloadMode mode) => (mode == DownloadMode.Mp3 ? Mp3Directory : Mp4Directory) is { Length: > 0 } path ? path : DownloadDirectory;
    public void SetDirectory(DownloadMode mode, string path) { if (mode == DownloadMode.Mp3) Mp3Directory = path; else Mp4Directory = path; }
    public string ReleaseRepository { get; set; } = "";
    public string ExtensionId { get; set; } = "";
    public void Validate()
    {
        if (Concurrency is < 1 or > 5 || !new[] { 128, 192, 256, 320 }.Contains(AudioKbps) || !new[] { 0, 720, 1080, 1440, 2160 }.Contains(VideoHeight)) throw new ArgumentException("品質或併發設定無效");
        if (new[] { DirectoryFor(DownloadMode.Mp4), DirectoryFor(DownloadMode.Mp3) }.Any(p => !Path.IsPathFullyQualified(p))) throw new ArgumentException("請選擇絕對儲存路徑");
    }
}
public sealed class DownloadJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RequestId { get; set; } = "";
    public string? GroupId { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string? Thumbnail { get; set; }
    public double? Duration { get; set; }
    public DownloadMode Mode { get; set; }
    public JobState State { get; set; } = JobState.Queued;
    public int Height { get; set; }
    public int AudioKbps { get; set; }
    public string Directory { get; set; } = "";
    public string? FilePath { get; set; }
    public double Progress { get; set; }
    public long Bytes { get; set; }
    public long? TotalBytes { get; set; }
    public double Speed { get; set; }
    public long Eta { get; set; }
    public int Retry { get; set; }
    public DateTimeOffset? RetryAt { get; set; }
    public string? Error { get; set; }
    public string? Stderr { get; set; }
    public bool IsUserEdited { get; set; }
    public bool HadCredentials { get; set; }
    public bool IsGroupRoot { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    [JsonIgnore] public string Quality => Mode == DownloadMode.Mp3 ? $"{AudioKbps} kbps" : Height == 0 ? "最佳" : $"{Height}p";
    [JsonIgnore] public string Format => Mode.ToString().ToUpperInvariant();
    [JsonIgnore] public string StatusText => State switch { JobState.PendingChoice => "等待選擇", JobState.Queued => "排隊中", JobState.Analyzing => "分析中", JobState.Downloading => $"下載中 {Progress:F0}%", JobState.Processing => "處理中", JobState.RetryWait => $"連線異常，將在 {Math.Max(0, (int)((RetryAt ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow).TotalSeconds)} 秒後重試 ({Retry}/3)", JobState.Paused => "已暫停", JobState.Completed => "已完成", JobState.Cancelled => "已取消", _ => "下載失敗" };
    [JsonIgnore] public string SpeedText => Speed <= 0 ? "0 MB/s" : $"{Speed / 1_000_000:F2} MB/s";
    [JsonIgnore] public string SizeText => TotalBytes.HasValue ? $"{TotalBytes / 1_000_000.0:F1} MB" : "—";
}
public sealed class DownloadGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public bool DiscoveryComplete { get; set; }
    public bool Notified { get; set; }
    public int DiscoveryFailures { get; set; }
}
public sealed record MediaInfo(string Title, string Artist, string Album, string? Thumbnail, double? Duration, MediaFormat[]? Formats = null);
public sealed record MediaFormat(string Extension, int? Height, bool Video, bool Audio, long? FileSize, double? Bitrate, string? Id = null, string? Codec = null, bool Approximate = false);
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() }, WriteIndented = false };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("空資料");
}
