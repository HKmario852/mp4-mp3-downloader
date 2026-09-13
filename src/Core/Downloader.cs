using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
namespace Omni.Core;
public sealed class Downloader : IAsyncDisposable
{
    readonly Store store; readonly string binaryDir, workDir; readonly object gate = new(); readonly SemaphoreSlim intake = new(1);
    readonly List<DownloadJob> jobs; readonly List<DownloadGroup> groups;
    readonly ConcurrentDictionary<string, List<BrowserCookie>> credentials = new();
    readonly Dictionary<string, (CancellationTokenSource ct, Task task)> running = [];
    readonly CancellationTokenSource lifetime = new(); readonly Task loop; readonly MusicMetadata music = new();
    public Preferences Settings { get; private set; }
    public event Action<string>? Notify;
    public event Action<DownloadJob>? ChoiceRequested;
    public Func<string, Task<bool>>? RetryCover;
    public Func<byte[], string, Task>? WriteExternalCover;
    public Downloader(Store store, string binaryDir, string workDir)
    {
        this.store = store; this.binaryDir = binaryDir; this.workDir = workDir; System.IO.Directory.CreateDirectory(workDir);
        Settings = store.Preferences(); jobs = store.Load(); groups = store.Groups();
        foreach (var j in jobs.Where(j => j.State is not (JobState.Completed or JobState.Cancelled or JobState.Failed or JobState.PendingChoice))) { j.State = JobState.Paused; j.Speed = 0; j.Eta = 0; if (j.HadCredentials) j.Error = "請從擴充功能重新傳送登入憑證"; store.Save(j); }
        // Credentials are intentionally not persisted. Remove only our credential files after a crash.
        foreach (var f in System.IO.Directory.EnumerateFiles(workDir, "cookies.txt", SearchOption.AllDirectories)) File.Delete(f);
        loop = Task.Run(Pump);
    }
    public DownloadJob[] Jobs { get { lock (gate) return jobs.ToArray(); } }
    public DownloadGroup[] Groups { get { lock (gate) return groups.ToArray(); } }
    public void SaveSettings(Preferences p) { p.Validate(); store.SavePreferences(p); Settings = p; }
    public void ReloadEditedJobs(IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(); var saved = store.Load().Where(j => selected.Contains(j.Id)).ToArray();
        lock (gate) foreach (var fresh in saved) { var index = jobs.FindIndex(j => j.Id == fresh.Id && j.State == JobState.Completed); if (index >= 0) jobs[index] = fresh; }
    }
    public bool ToolsReady => File.Exists(Path.Combine(binaryDir, "yt-dlp.exe")) && File.Exists(Path.Combine(binaryDir, "ffmpeg.exe"));
    public async Task<IntakeAck> Accept(IntakeRequest request)
    {
        Validation.Request(request); await intake.WaitAsync();
        try
        {
            DownloadJob? existing; lock (gate) existing = jobs.FirstOrDefault(j => j.RequestId == request.RequestId);
            if (existing is not null) return new(true, request.RequestId, existing.State.ToString());
            var p = Settings; p.Validate();
            var j = new DownloadJob { RequestId = request.RequestId, Url = request.Url, Title = request.Url, Mode = request.Mode == "mp3" ? DownloadMode.Mp3 : DownloadMode.Mp4, Height = p.VideoHeight, AudioKbps = p.AudioKbps, Directory = p.DirectoryFor(request.Mode == "mp3" ? DownloadMode.Mp3 : DownloadMode.Mp4), HadCredentials = request.Cookies?.Count > 0, State = Validation.Compound(request.Url) ? JobState.PendingChoice : JobState.Queued };
            if (Validation.WebUrl(j.Url).AbsolutePath == "/playlist" && Validation.Query(Validation.WebUrl(j.Url)).ContainsKey("list")) { var g = new DownloadGroup { Url = j.Url, Title = "正在載入播放清單" }; j.IsGroupRoot = true; j.GroupId = g.Id; lock (gate) groups.Add(g); store.SaveGroup(g); }
            store.Save(j); if (request.Cookies?.Count > 0) credentials[j.Id] = request.Cookies;
            lock (gate) jobs.Add(j);
            if (j.State == JobState.PendingChoice) ChoiceRequested?.Invoke(j); else Notify?.Invoke("已加入下載佇列：" + j.Title);
            return new(true, request.RequestId, j.State.ToString());
        }
        finally { intake.Release(); }
    }
    public async Task Choose(string id, bool playlist)
    {
        DownloadJob j; lock (gate) { j = jobs.First(x => x.Id == id); if (j.State != JobState.PendingChoice) return; j.State = JobState.Queued; j.IsGroupRoot = playlist; }
        if (playlist)
        {
            var group = new DownloadGroup { Url = j.Url, Title = "正在載入播放清單" }; j.GroupId = group.Id;
            lock (gate) groups.Add(group); store.SaveGroup(group);
        }
        store.Save(j); await Task.CompletedTask;
    }
    async Task Pump()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(lifetime.Token))
            {
                lock (gate)
                {
                    foreach (var id in running.Where(k => k.Value.task.IsCompleted).Select(k => k.Key).ToArray()) { running[id].ct.Dispose(); running.Remove(id); }
                    foreach (var j in jobs.Where(j => j.State == JobState.Queued).Take(Math.Max(0, Settings.Concurrency - running.Count)).ToArray())
                    {
                        j.State = JobState.Analyzing; var ct = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        running[j.Id] = (ct, Task.Run(() => Run(j, ct.Token)));
                    }
                    foreach (var g in groups.Where(g => g.DiscoveryComplete && !g.Notified))
                    {
                        var children = jobs.Where(j => j.GroupId == g.Id && !j.IsGroupRoot).ToArray();
                        if (children.Any(j => j.State is not (JobState.Completed or JobState.Failed or JobState.Cancelled))) continue;
                        int failed = children.Count(j => j.State == JobState.Failed) + g.DiscoveryFailures; g.Notified = true; store.SaveGroup(g);
                        Notify?.Invoke($"播放清單完成：{g.Title}" + (failed > 0 ? $"（含 {failed} 個失敗項目）" : ""));
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
    async Task Run(DownloadJob j, CancellationToken ct)
    {
        var work = Path.Combine(workDir, j.Id); System.IO.Directory.CreateDirectory(work); var cookiePath = Path.Combine(work, "cookies.txt");
        try
        {
            if (!ToolsReady) throw new IOException("缺少 yt-dlp.exe 或 ffmpeg.exe，請先執行下載工具準備腳本");
            var auth = new List<string>();
            if (credentials.TryGetValue(j.Id, out var cookies)) { await File.WriteAllTextAsync(cookiePath, Validation.Netscape(cookies), ct); auth.AddRange(["--cookies", cookiePath]); }
            else if (j.HadCredentials) throw new IOException("登入憑證已過期，請從擴充功能重新傳送");
            if (j.IsGroupRoot) { await Discover(j, auth, ct); return; }
            var info = await Analyze(j.Url, auth, ct);
            j.Duration = info.Duration;
            if (!j.IsUserEdited) { j.Title = Settings.CleanTitle ? Validation.CleanTitle(info.Title) : info.Title; j.Artist = info.Artist; j.Album = info.Album; }
            j.Thumbnail = info.Thumbnail; store.Save(j);
            for (int attempt = 0; ; attempt++)
            {
                try { await Download(j, info, work, auth, ct); break; }
                catch (DownloadException e) when (attempt < 3 && !e.Stderr.Contains("403") && !e.Stderr.Contains("Sign in", StringComparison.OrdinalIgnoreCase))
                {
                    j.Retry = attempt + 1; j.State = JobState.RetryWait; j.Speed = 0; j.Eta = 0; int seconds = 1 << (attempt + 1); j.RetryAt = DateTimeOffset.UtcNow.AddSeconds(seconds); store.Save(j); await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
                }
            }
            ct.ThrowIfCancellationRequested(); j.State = JobState.Processing; j.Speed = 0; j.Eta = 0; store.Save(j);
            var file = System.IO.Directory.GetFiles(work, "media.*").FirstOrDefault(f => Path.GetExtension(f) == (j.Mode == DownloadMode.Mp3 ? ".mp3" : ".mp4")) ?? throw new IOException("下載完成但找不到輸出檔案");
            if (j.Mode == DownloadMode.Mp3)
            {
                var doc = Id3Document.Read(file);
                var covers = new List<Cover>();
                if (Settings.MusicBrainz) {
                    j.MetadataStatus = "MusicBrainz：正在查詢歌曲及專輯封面…"; store.Save(j);
                    var metadata = await music.Lookup(j.Title, j.Artist, j.Duration, ct);
                    j.MetadataStatus = metadata.Message; j.MetadataCheckedAt = DateTimeOffset.UtcNow;
                    if (!j.IsUserEdited) { if (metadata.Title is { Length: > 0 }) j.Title = metadata.Title; if (metadata.Artist is { Length: > 0 }) j.Artist = metadata.Artist; if (metadata.Album is { Length: > 0 }) j.Album = metadata.Album; }
                    if (metadata.Cover is not null) covers.Add(metadata.Cover);
                    store.Save(j);
                } else j.MetadataStatus = "MusicBrainz：已在設定關閉";
                doc.SetText("TIT2", j.Title); doc.SetText("TPE1", j.Artist); doc.SetText("TALB", j.Album);
                if (info.Thumbnail is not null) { try { var thumb = await MusicMetadata.Fetch(info.Thumbnail, "Video thumbnail", covers.Count == 0 ? (byte)3 : (byte)0, ct); if (thumb is not null) covers.Add(thumb); } catch (HttpRequestException) { } }
                if (covers.Count > 0) doc.SetCovers(covers);
                var tagged = file + ".tagged"; if (File.Exists(tagged)) File.Delete(tagged); await doc.Write(file, tagged, ct); File.Move(tagged, file, true);
                System.IO.Directory.CreateDirectory(j.Directory);
                if (covers.Count > 0 && WriteExternalCover is not null) await WriteExternalCover(covers[0].Bytes, Path.Combine(j.Directory, "cover.jpg"));
            }
            ct.ThrowIfCancellationRequested(); System.IO.Directory.CreateDirectory(j.Directory);
            // Final destination reservation is serialized to prevent same-title races.
            lock (gate)
            {
                ct.ThrowIfCancellationRequested(); var destination = Validation.UniquePath(j.Directory, Validation.SafeName(j.Title), Path.GetExtension(file));
                File.Move(file, destination); j.FilePath = destination; j.State = JobState.Completed; j.Progress = 100; j.TotalBytes = new FileInfo(destination).Length; j.CompletedAt = DateTimeOffset.UtcNow; store.Save(j);
            }
            if (j.GroupId is null) Notify?.Invoke("下載完成：" + j.Title);
        }
        catch (OperationCanceledException) { lock (gate) { if (j.State != JobState.Completed && j.State != JobState.Cancelled) j.State = JobState.Paused; j.Speed = 0; j.Eta = 0; store.Save(j); } }
        catch (Exception e) { j.State = JobState.Failed; j.Error = e.Message; j.Stderr = e is DownloadException d ? d.Stderr : null; j.Speed = 0; j.Eta = 0; store.Save(j); if (j.IsGroupRoot && j.GroupId is not null) { var g = Groups.First(x => x.Id == j.GroupId); g.DiscoveryComplete = true; g.DiscoveryFailures++; store.SaveGroup(g); } else if (j.GroupId is null) Notify?.Invoke("下載失敗：" + j.Title); }
        finally { if (File.Exists(cookiePath)) File.Delete(cookiePath); if (j.State is JobState.Completed or JobState.Cancelled or JobState.Failed) credentials.TryRemove(j.Id, out _); }
    }
    public async Task<MediaInfo> Analyze(string url, List<string>? auth = null, CancellationToken ct = default)
    {
        Validation.WebUrl(url); var text = await ProcessRunner.Run(Path.Combine(binaryDir, "yt-dlp.exe"), new[] { "--ignore-config", "--js-runtimes", "deno:" + Path.Combine(binaryDir, "deno.exe"), "--no-playlist", "--dump-single-json", "--skip-download", "--no-warnings" }.Concat(auth ?? []).Concat(["--", url]), null, ct);
        using var doc = JsonDocument.Parse(text); var root = doc.RootElement;
        string Get(string key) => root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
        static double? Number(JsonElement element, string key) => element.TryGetProperty(key, out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var value) ? value : null;
        static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        var formats = root.TryGetProperty("formats", out var fs) && fs.ValueKind == JsonValueKind.Array ? fs.EnumerateArray().Select(f => new MediaFormat(Text(f, "ext"), Number(f, "height") is double h ? (int)h : null, Text(f, "vcodec") is not ("" or "none"), Text(f, "acodec") is not ("" or "none"), (Number(f, "filesize") ?? Number(f, "filesize_approx")) is double b ? (long)b : null, Number(f, "tbr"), Text(f, "format_id"), Text(f, "vcodec"), Number(f, "filesize") is null)).ToArray() : [];
        return new(Get("track") is { Length: > 0 } track ? track : Get("title"), Get("artist"), Get("album"), Get("thumbnail") is { Length: > 0 } t ? t : null, Number(root, "duration"), formats);
    }
    async Task Discover(DownloadJob parent, List<string> auth, CancellationToken ct)
    {
        var group = Groups.First(g => g.Id == parent.GroupId);
        var text = await ProcessRunner.Run(Path.Combine(binaryDir, "yt-dlp.exe"), new[] { "--ignore-config", "--flat-playlist", "--dump-single-json", "--yes-playlist", "--skip-download" }.Concat(auth).Concat(["--", parent.Url]), null, ct);
        using var doc = JsonDocument.Parse(text); group.Title = doc.RootElement.GetProperty("title").GetString() ?? "播放清單";
        var directory = Path.Combine(parent.Directory, Validation.SafeName(group.Title)); var seen = new HashSet<string>();
        foreach (var item in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id)) { group.DiscoveryFailures++; continue; }
            var videoId = id.GetString() ?? ""; if (!seen.Add(videoId)) continue;
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (url is null || !url.StartsWith("https://")) url = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(videoId);
            var j = new DownloadJob { GroupId = group.Id, RequestId = parent.RequestId + ":" + videoId, Url = url, Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? url : url, Mode = parent.Mode, Height = parent.Height, AudioKbps = parent.AudioKbps, Directory = directory, HadCredentials = parent.HadCredentials };
            if (credentials.TryGetValue(parent.Id, out var c)) credentials[j.Id] = c; store.Save(j); lock (gate) jobs.Add(j);
        }
        group.DiscoveryComplete = true; store.SaveGroup(group); parent.State = JobState.Completed; store.Save(parent);
    }
    async Task Download(DownloadJob j, MediaInfo info, string work, List<string> auth, CancellationToken ct)
    {
        j.State = JobState.Downloading; var args = new List<string> { "--ignore-config", "--js-runtimes", "deno:" + Path.Combine(binaryDir, "deno.exe"), "--no-playlist", "--newline", "--continue", "--no-warnings", "--progress-delta", "0.5", "--progress-template", "download:OMNI:%(progress.downloaded_bytes)s|%(progress.total_bytes,progress.total_bytes_estimate)s", "--ffmpeg-location", binaryDir, "-o", Path.Combine(work, "media.%(ext)s") };
        if (j.Mode == DownloadMode.Mp3) args.AddRange(["-f", "bestaudio/best", "-x", "--audio-format", "mp3", "--audio-quality", j.AudioKbps + "k", "--postprocessor-args", "ExtractAudio+ffmpeg_o:-ar 44100", "--embed-metadata"]);
        else { var plan = VideoSelection.Select(info, j.Height); args.AddRange(["-f", plan?.Selector ?? VideoSelection.Fallback(j.Height), "--merge-output-format", "mp4", "--remux-video", "mp4"]); }
        args.AddRange(auth); args.AddRange(["--", j.Url]); var ema = new SpeedEma(); var watch = Stopwatch.StartNew(); long previous = 0;
        using var sampleCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sampler = Task.Run(async () => { using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500)); try { while (await timer.WaitForNextTickAsync(sampleCt.Token)) { var now = watch.ElapsedMilliseconds; var speed = ema.Sample(j.Bytes, j.TotalBytes, (now - previous) / 1000.0, j.State == JobState.Downloading); previous = now; j.Speed = speed.speed; j.Eta = speed.eta; } } catch (OperationCanceledException) { } });
        try { await ProcessRunner.Run(Path.Combine(binaryDir, "yt-dlp.exe"), args, line => { if (!line.StartsWith("OMNI:")) return; var fields = line[5..].Split('|'); if (long.TryParse(fields[0], out var b)) j.Bytes = b; if (fields.Length > 1 && long.TryParse(fields[1], out var total)) j.TotalBytes = total; j.Progress = j.TotalBytes > 0 ? Math.Min(100, 100.0 * j.Bytes / j.TotalBytes.Value) : 0; }, ct); }
        finally { sampleCt.Cancel(); await sampler; }
    }
    public async Task Pause(string id)
    {
        Task? task = null; lock (gate) { var j = jobs.First(x => x.Id == id); if (j.State is JobState.Completed or JobState.Cancelled or JobState.Failed or JobState.PendingChoice) return; j.State = JobState.Paused; j.Speed = 0; j.Eta = 0; store.Save(j); if (running.TryGetValue(id, out var r)) { r.ct.Cancel(); task = r.task; } }
        if (task is not null) await task;
    }
    public void Resume(string id)
    {
        lock (gate) { var j = jobs.First(x => x.Id == id); if (j.State is not (JobState.Paused or JobState.Failed)) return; if (running.TryGetValue(id, out var r) && !r.task.IsCompleted) return; j.State = JobState.Queued; j.Error = null; j.Retry = 0; store.Save(j); }
    }
    public async Task Cancel(IEnumerable<string> ids)
    {
        foreach (var id in ids.Distinct())
        {
            Task? task = null; lock (gate) { var j = jobs.FirstOrDefault(x => x.Id == id); if (j is null || j.State is not (JobState.Queued or JobState.Downloading or JobState.Analyzing or JobState.RetryWait)) continue; j.State = JobState.Cancelled; store.Save(j); if (running.TryGetValue(id, out var r)) { r.ct.Cancel(); task = r.task; } }
            if (task is not null) await task;
            var dir = Path.GetFullPath(Path.Combine(workDir, id)); var root = Path.GetFullPath(workDir) + Path.DirectorySeparatorChar;
            if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("暫存路徑無效");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); credentials.TryRemove(id, out _);
        }
    }
    public async ValueTask DisposeAsync() { lifetime.Cancel(); await loop; Task[] tasks; lock (gate) { foreach (var r in running.Values) r.ct.Cancel(); tasks = running.Values.Select(r => r.task).ToArray(); } await Task.WhenAll(tasks); credentials.Clear(); store.Checkpoint(); lifetime.Dispose(); }
}
