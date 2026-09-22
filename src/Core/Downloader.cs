using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
namespace Omni.Core;
public sealed partial class Downloader : IAsyncDisposable
{
    readonly Store store; readonly string binaryDir, workDir; readonly object gate = new(); readonly SemaphoreSlim intake = new(1);
    readonly List<DownloadJob> jobs; readonly List<DownloadGroup> groups;
    readonly ConcurrentDictionary<string, List<BrowserCookie>> credentials = new();
    readonly Dictionary<string, (CancellationTokenSource ct, Task task)> running = [];
    readonly CancellationTokenSource lifetime = new(); readonly Task loop; readonly MusicMetadata music = new();
    public Preferences Settings { get; private set; }
    public event Action<string>? Notify;
    public Func<MusicCandidate[],CancellationToken,Task<MusicCandidate?>>? ResolveMusic {get;set;}
    public string FfmpegPath=>Path.Combine(binaryDir,"ffmpeg.exe");
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
        if(Settings.ResumeOnStart) foreach(var j in jobs.Where(j=>j.State==JobState.Paused&&!j.HadCredentials)){j.State=JobState.Queued;store.Save(j);}
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
            j.OutputFormat=request.OutputFormat??request.Mode; j.Options=Json.Decode<Preferences>(Json.Encode(p));
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
                await ApplyNetworkPolicy();
                lock (gate)
                {
                    foreach (var id in running.Where(k => k.Value.task.IsCompleted).Select(k => k.Key).ToArray()) { running[id].ct.Dispose(); running.Remove(id); }
                    foreach (var j in jobs.Where(j => j.State == JobState.Queued && (NetworkPermitted?.Invoke(Settings)??true)).Take(Math.Max(0, Settings.Concurrency - running.Count)).ToArray())
                    {
                        j.State = JobState.Analyzing; var ct = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        running[j.Id] = (ct, Task.Run(() => Run(j, ct.Token)));
                    }
                    foreach (var g in groups.Where(g => g.DiscoveryComplete && !g.Notified))
                    {
                        var children = jobs.Where(j => j.GroupId == g.Id && !j.IsGroupRoot).ToArray();
                        if (children.Any(j => j.State is not (JobState.Completed or JobState.Failed or JobState.Cancelled))) continue;
                        int failed = children.Count(j => j.State == JobState.Failed) + g.DiscoveryFailures; g.Notified = true; store.SaveGroup(g);
                        if(Settings.NotifyAll) Notify?.Invoke($"播放清單完成：{g.Title}" + (failed > 0 ? $"（含 {failed} 個失敗項目）" : ""));
                    }
                }
                var busy=Jobs.Any(j=>j.State is JobState.Queued or JobState.Analyzing or JobState.Downloading or JobState.Processing or JobState.RetryWait);
                if(wasBusy&&!busy&&!Jobs.Any(j=>j.State is JobState.Paused or JobState.PendingChoice)&&Settings.NotifyAll)Notify?.Invoke("全部任務已結束 / All tasks finished"); wasBusy=busy;
            }
        }
        catch (OperationCanceledException) { }
    }
    async Task Run(DownloadJob j, CancellationToken ct)
    {
        var options=j.Options??Settings;
        var work = JobWork(j); j.WorkPath=work; store.Save(j); System.IO.Directory.CreateDirectory(work); var cookiePath = Path.Combine(work, "cookies.txt");
        try
        {
            if (!ToolsReady) throw new IOException("缺少 yt-dlp.exe 或 ffmpeg.exe，請先執行下載工具準備腳本");
            var auth = new List<string>();
            if (credentials.TryGetValue(j.Id, out var cookies)) { await File.WriteAllTextAsync(cookiePath, Validation.Netscape(cookies), ct); auth.AddRange(["--cookies", cookiePath]); }
            else if (j.HadCredentials) throw new IOException("登入憑證已過期，請從擴充功能重新傳送");
            else if(Settings.CookieFile.Length>0&&new[]{"youtube.com","www.youtube.com","m.youtube.com","youtu.be"}.Contains(new Uri(j.Url).Host)){if(!File.Exists(Settings.CookieFile))throw new IOException("Cookie 檔案不存在 / Cookie file missing");File.Copy(Settings.CookieFile,cookiePath,true);auth.AddRange(["--cookies",cookiePath]);}
            if (j.IsGroupRoot) { await RetryOperation(j,options,async()=>{await Discover(j,auth,ct);return true;},ct); return; }
            var info = await RetryOperation(j,options,()=>Analyze(j.Url,auth,ct),ct);
            j.Duration = info.Duration;
            if (!j.IsUserEdited) { j.Title = Settings.CleanTitle ? Validation.CleanTitle(info.Title) : info.Title; j.Artist = info.Artist; j.Album = info.Album; }
            j.Thumbnail = info.Thumbnail; store.Save(j);
            await RetryOperation(j,options,async()=>{await Download(j,info,work,auth,ct);return true;},ct);
            ct.ThrowIfCancellationRequested(); j.State = JobState.Processing; j.Speed = 0; j.Eta = 0; store.Save(j);
            var file = Path.Combine(work,"media."+j.Extension); if(!File.Exists(file))throw new IOException("下載完成但找不到輸出檔案");
            if (j.Extension == "mp3")
            {
                var doc = Id3Document.Read(file);
                var covers = new List<Cover>();
                if (Settings.MusicBrainz) {
                    j.MetadataStatus = "MusicBrainz：正在查詢歌曲及專輯封面…"; store.Save(j);
                    var metadata = await music.Recognize(j.Title,j.Artist,j.Duration,file,FfmpegPath,Settings.AcoustIdClientKey,ct);
                    if(metadata.State==MusicLookupState.Ambiguous&&metadata.Choices is {Length:>0} choices&&ResolveMusic is not null){var chosen=await ResolveMusic(choices,ct);if(chosen is not null)try{metadata=await music.Recording(chosen.RecordingId,chosen.ReleaseId,ct);}catch(Exception e)when(!ct.IsCancellationRequested&&e is HttpRequestException or IOException or System.Text.Json.JsonException or OperationCanceledException){metadata=new(MusicLookupState.Unavailable);}}
                    if(!j.IsUserEdited&&options.KeepMetadata&&metadata.Tags is not null)foreach(var tag in metadata.Tags)doc.SetText(tag.Key=="TYER"&&doc.Version==4?"TDRC":tag.Key,tag.Value);
                    j.MetadataStatus = metadata.Message; j.MetadataCheckedAt = DateTimeOffset.UtcNow;
                    if (!j.IsUserEdited) { if (metadata.Title is { Length: > 0 }) j.Title = metadata.Title; if (metadata.Artist is { Length: > 0 }) j.Artist = metadata.Artist; if (metadata.Album is { Length: > 0 }) j.Album = metadata.Album; }
                    if (metadata.Cover is not null) covers.Add(metadata.Cover);
                    store.Save(j);
                } else j.MetadataStatus = "MusicBrainz：已在設定關閉";
                if(options.KeepMetadata){doc.SetText("TIT2", j.Title); doc.SetText("TPE1", j.Artist); doc.SetText("TALB", j.Album);}
                covers=covers.Where(c=>AlbumArtwork.Accept(c.Bytes)).ToList();
                var sourceArt=await FindSourceArtwork(j.Url,info,auth,ct);
                if(sourceArt is null&&covers.Count==0&&!Settings.MusicBrainz){var fallback=await music.Lookup(j.Title,j.Artist,j.Duration,ct);if(fallback.Cover is not null)covers.Add(fallback.Cover);j.MetadataStatus=fallback.Message;}
                covers=CoverOrder.ThumbnailFirst(covers,sourceArt);
                if(options.EmbedThumbnail)doc.SetCovers(covers);
                j.MetadataStatus += covers.Count>0?" · 已取得近正方形專輯封面":" · 未找到專輯封面";store.Save(j);

                var tagged = file + ".tagged"; if (File.Exists(tagged)) File.Delete(tagged); await doc.Write(file, tagged, ct); File.Move(tagged, file, true);
                System.IO.Directory.CreateDirectory(j.Directory);
                if (options.KeepThumbnail && covers.Count > 0 && WriteExternalCover is not null) await WriteExternalCover(covers[0].Bytes, Path.Combine(j.Directory, "cover.jpg"));
            }
            ct.ThrowIfCancellationRequested(); System.IO.Directory.CreateDirectory(j.Directory);
            if(!await PublishFinal(j,file,work,options,ct))return;
            if (j.GroupId is null && Settings.NotifyComplete) Notify?.Invoke("下載完成：" + j.Title);
            Completed?.Invoke(j);
        }
        catch (OperationCanceledException) { lock (gate) { if (j.State != JobState.Completed && j.State != JobState.Cancelled) j.State = JobState.Paused; j.Speed = 0; j.Eta = 0; store.Save(j); } }
        catch (Exception e) { if (j.State is JobState.Cancelled or JobState.Completed) return; j.State = JobState.Failed; j.Error = e.Message; j.Stderr = e is DownloadException d ? d.Stderr : null;WriteFailureLog(j); j.Speed = 0; j.Eta = 0; store.Save(j); if (j.IsGroupRoot && j.GroupId is not null) { var g = Groups.First(x => x.Id == j.GroupId); g.DiscoveryComplete = true; g.DiscoveryFailures++; store.SaveGroup(g); } else if (j.GroupId is null && Settings.NotifyFailure) Notify?.Invoke("下載失敗：" + j.Title); }
        finally { try { if (File.Exists(cookiePath)) File.Delete(cookiePath); } catch(IOException) { } if (j.State is JobState.Completed or JobState.Cancelled or JobState.Failed) credentials.TryRemove(j.Id, out _); if(j.State==JobState.Completed&&j.Error is null || j.State==JobState.Cancelled || j.State==JobState.Failed&&Settings.CleanFailed) { try { CleanWork(j); } catch(IOException e) { j.Error="暫存清理未完成 / Temporary cleanup incomplete: "+e.Message;store.Save(j); } } }
    }
    async Task<T> RetryOperation<T>(DownloadJob j,Preferences p,Func<Task<T>> operation,CancellationToken ct) {
        for(int attempt=0;;attempt++)try{return await operation();}
        catch(DownloadException e)when(p.AutoRetry&&attempt<p.RetryCount&&!e.Stderr.Contains("403")&&!e.Stderr.Contains("Sign in",StringComparison.OrdinalIgnoreCase)){
            j.Retry=attempt+1;j.State=JobState.RetryWait;j.Speed=0;j.Eta=0;var seconds=Math.Min(3600,p.RetrySeconds*Math.Pow(2,attempt));j.RetryAt=DateTimeOffset.UtcNow.AddSeconds(seconds);store.Save(j);await Task.Delay(TimeSpan.FromSeconds(seconds),ct);
        }
    }
    public async Task<Cover?> FindSourceArtwork(string url,MediaInfo info,List<string>? auth=null,CancellationToken ct=default){
        var art=await AlbumArtwork.Source(info,ct);if(art is not null)return art;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be"))return null;
        var id=uri.Host=="youtu.be"?uri.AbsolutePath.Trim('/'):Validation.Query(uri).GetValueOrDefault("v");
        if(id is null||!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[A-Za-z0-9_-]{11}$"))return null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try{return await AlbumArtwork.Source(await Analyze("https://music.youtube.com/watch?v="+id,auth,timeout.Token),timeout.Token);}catch(Exception e)when(e is DownloadException or HttpRequestException or IOException or OperationCanceledException && !ct.IsCancellationRequested){return null;}
    }
    public async Task<MediaInfo> Analyze(string url, List<string>? auth = null, CancellationToken ct = default)
    {
        Validation.WebUrl(url); var text = await ProcessRunner.Run(Path.Combine(binaryDir, "yt-dlp.exe"), new[] { "--ignore-config", "--js-runtimes", "deno:" + Path.Combine(binaryDir, "deno.exe"), "--no-playlist", "--dump-single-json", "--skip-download", "--no-warnings" }.Concat(["--cache-dir",Path.Combine(workDir,"network-cache")]).Concat(DownloadOptions.Network(Settings)).Concat(auth ?? []).Concat(["--", url]), null, ct);
        using var doc = JsonDocument.Parse(text); var root = doc.RootElement;
        string Get(string key) => root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
        static double? Number(JsonElement element, string key) => element.TryGetProperty(key, out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var value) ? value : null;
        static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        var formats = root.TryGetProperty("formats", out var fs) && fs.ValueKind == JsonValueKind.Array ? fs.EnumerateArray().Select(f => new MediaFormat(Text(f, "ext"), Number(f, "height") is double h ? (int)h : null, Text(f, "vcodec") is not ("" or "none"), Text(f, "acodec") is not ("" or "none"), (Number(f, "filesize") ?? Number(f, "filesize_approx")) is double b ? (long)b : null, Number(f, "tbr"), Text(f, "format_id"), Text(f, "vcodec"), Number(f, "filesize") is null)).ToArray() : [];
        return new(Get("track") is { Length: > 0 } track ? track : Get("title"), Get("artist"), Get("album"), Get("thumbnail") is { Length: > 0 } t ? t : null, Number(root, "duration"), formats, AlbumArtwork.Candidates(root));
    }
    async Task Discover(DownloadJob parent, List<string> auth, CancellationToken ct)
    {
        var group = Groups.First(g => g.Id == parent.GroupId);
        var text = await ProcessRunner.Run(Path.Combine(binaryDir, "yt-dlp.exe"), new[] { "--ignore-config", "--flat-playlist", "--dump-single-json", "--yes-playlist", "--skip-download" }.Concat(["--cache-dir",Path.Combine(workDir,"network-cache")]).Concat(DownloadOptions.Network(Settings)).Concat(auth).Concat(["--", parent.Url]), null, ct);
        using var doc = JsonDocument.Parse(text); group.Title = doc.RootElement.GetProperty("title").GetString() ?? "播放清單";
        var directory = Path.Combine(parent.Directory, Validation.SafeName(group.Title)); var seen = new HashSet<string>();
        foreach (var item in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id)) { group.DiscoveryFailures++; continue; }
            var videoId = id.GetString() ?? ""; if (!seen.Add(videoId)) continue;
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (url is null || !url.StartsWith("https://")) url = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(videoId);
            var j = new DownloadJob { GroupId = group.Id, RequestId = parent.RequestId + ":" + videoId, Url = url, Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? url : url, Mode = parent.Mode, OutputFormat=parent.Extension, Options=parent.Options, Height = parent.Height, AudioKbps = parent.AudioKbps, Directory = directory, HadCredentials = parent.HadCredentials };
            if (credentials.TryGetValue(parent.Id, out var c)) credentials[j.Id] = c; store.Save(j); lock (gate) jobs.Add(j);
        }
        group.DiscoveryComplete = true; store.SaveGroup(group); parent.State = JobState.Completed; store.Save(parent);
    }
    async Task Download(DownloadJob j, MediaInfo info, string work, List<string> auth, CancellationToken ct)
    {
        j.State = JobState.Downloading; var args = new List<string> { "--ignore-config", "--js-runtimes", "deno:" + Path.Combine(binaryDir, "deno.exe"), "--no-playlist", "--newline", "--continue", "--no-warnings", "--progress-delta", "0.5", "--progress-template", "download:OMNI:%(progress.downloaded_bytes)s|%(progress.total_bytes,progress.total_bytes_estimate)s", "--ffmpeg-location", binaryDir, "-o", Path.Combine(work, "media.%(ext)s") };
        args.AddRange(DownloadOptions.Format(j,j.Options??Settings,info)); args.AddRange(DownloadOptions.Network(Settings)); args.AddRange(["--cache-dir",Path.Combine(workDir,"network-cache")]);
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
            Task? task = null; lock (gate) { var j = jobs.FirstOrDefault(x => x.Id == id); if (j is null || j.State is JobState.Completed or JobState.Cancelled) continue; j.State = JobState.Cancelled; store.Save(j); if (running.TryGetValue(id, out var r)) { r.ct.Cancel(); task = r.task; } }
            if (task is not null) await task;
            var removed=Jobs.First(j=>j.Id==id);CleanWork(removed);credentials.TryRemove(id,out _);
        }
    }
    public async ValueTask DisposeAsync() { lifetime.Cancel(); await loop; Task[] tasks; lock (gate) { foreach (var r in running.Values) r.ct.Cancel(); tasks = running.Values.Select(r => r.task).ToArray(); } await Task.WhenAll(tasks); credentials.Clear(); store.Checkpoint(); lifetime.Dispose(); }
}
