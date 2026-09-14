using System.Globalization;
using System.Text.RegularExpressions;
namespace Omni.Core;

public sealed partial class Preferences
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "zh-Hant";
    public bool StartAtLogin { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public bool ResumeOnStart { get; set; }
    public bool MonitorClipboard { get; set; }
    public string TempDirectory { get; set; } = "";
    public string DuplicateAction { get; set; } = "rename";
    public bool AutoRetry { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetrySeconds { get; set; } = 2;
    public bool CleanFailed { get; set; }
    public string CompletionAction { get; set; } = "none";
    public string DefaultType { get; set; } = "video";
    public string VideoFormat { get; set; } = "mp4";
    public string AudioFormat { get; set; } = "mp3";
    public string VideoCodec { get; set; } = "auto";
    public bool DownloadSubtitles { get; set; }
    public string SubtitleLanguages { get; set; } = "en,zh-Hant";
    public string SubtitleFormat { get; set; } = "srt";
    public bool EmbedSubtitles { get; set; }
    public bool KeepThumbnail { get; set; } = true;
    public bool EmbedThumbnail { get; set; } = true;
    public bool KeepMetadata { get; set; } = true;
    public string VideoNaming { get; set; } = "{title}";
    public string AudioNaming { get; set; } = "{title}";
    public string ProxyMode { get; set; } = "system";
    public string ProxyUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int ConnectionRetries { get; set; } = 3;
    public int Fragments { get; set; } = 1;
    public int LimitKiB { get; set; }
    public bool ScheduleLimit { get; set; }
    public string LimitStart { get; set; } = "18:00";
    public string LimitEnd { get; set; } = "23:00";
    public int ScheduledKiB { get; set; } = 1024;
    public string AllowedNetwork { get; set; } = "any";
    public string CookieFile { get; set; } = "";
    public bool NotifyComplete { get; set; } = true;
    public bool NotifyFailure { get; set; } = true;
    public bool NotifyAll { get; set; } = true;
    public bool SystemNotifications { get; set; } = true;
    public bool Sound { get; set; }
    public string SoundName { get; set; } = "default";
    public bool TaskbarProgress { get; set; } = true;
    public bool QuietHours { get; set; }
    public string QuietStart { get; set; } = "22:00";
    public string QuietEnd { get; set; } = "08:00";
    public bool IsQuiet(DateTime now) => QuietHours && DownloadOptions.InPeriod(now, QuietStart, QuietEnd);
    public int EffectiveLimit(DateTime now) => ScheduleLimit && DownloadOptions.InPeriod(now, LimitStart, LimitEnd) ? ScheduledKiB : LimitKiB;
    void ValidateOptions()
    {
        static void One(string value, params string[] allowed) { if (!allowed.Contains(value)) throw new ArgumentException("設定選項無效 / Invalid setting: " + value); }
        One(Theme,"dark","light","system"); One(Language,"zh-Hant","en"); One(DuplicateAction,"ask","overwrite","rename","skip");
        One(DefaultType,"video","audio","ask"); One(VideoFormat,"mp4","mkv","webm"); One(AudioFormat,"mp3","m4a","flac","wav");
        One(VideoCodec,"auto","h264","h265","av1"); One(ProxyMode,"off","system","custom"); One(SubtitleFormat,"srt","vtt");
        One(CompletionAction,"none","file","folder"); One(AllowedNetwork,"any","wifi","ethernet"); One(SoundName,"default","asterisk","exclamation");
        if (RetryCount is < 0 or > 20 || RetrySeconds is < 1 or > 3600 || TimeoutSeconds is < 5 or > 600 || ConnectionRetries is < 0 or > 20 || Fragments is < 1 or > 10 || LimitKiB is < 0 or > 1000000 || ScheduledKiB is < 1 or > 1000000) throw new ArgumentException("數值超出允許範圍 / Value out of range");
        foreach (var t in new[] { LimitStart, LimitEnd, QuietStart, QuietEnd }) if (!TimeOnly.TryParseExact(t,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) throw new ArgumentException("時間格式必須為 HH:mm / Use HH:mm");
        if (ProxyMode=="custom" && (!Uri.TryCreate(ProxyUrl,UriKind.Absolute,out var u) || u.Scheme is not ("http" or "https" or "socks5") || string.IsNullOrWhiteSpace(u.Host) || !string.IsNullOrEmpty(u.UserInfo))) throw new ArgumentException("代理網址格式錯誤；不接受內含密碼 / Invalid proxy URL; credentials are not supported");
        if (SubtitleLanguages.Length > 120 || !Regex.IsMatch(SubtitleLanguages,"^[a-zA-Z0-9.,_-]+$")) throw new ArgumentException("字幕語言格式錯誤 / Invalid subtitle languages");
        foreach (var p in new[] { TempDirectory, CookieFile }) if (p.Length>0 && !Path.IsPathFullyQualified(p)) throw new ArgumentException("請使用絕對路徑 / Use an absolute path");
        DownloadOptions.ValidateNaming(VideoNaming); DownloadOptions.ValidateNaming(AudioNaming);
        if (VideoFormat=="webm" && VideoCodec is "h264" or "h265") throw new ArgumentException("WebM 不支援 H.264/H.265；請選 AV1 或自動 / WebM requires AV1 or Auto");
        if (EmbedSubtitles && VideoFormat=="webm") throw new ArgumentException("WebM 請保存外部字幕，或改用 MKV / Use external subtitles for WebM or select MKV");
    }
}
public static class DownloadOptions
{
    public static bool InPeriod(DateTime now,string start,string end) { var t=TimeOnly.FromDateTime(now); var a=TimeOnly.ParseExact(start,"HH:mm",CultureInfo.InvariantCulture); var b=TimeOnly.ParseExact(end,"HH:mm",CultureInfo.InvariantCulture); return a==b || (a<b ? t>=a&&t<b : t>=a||t<b); }
    public static void ValidateNaming(string value) { if (string.IsNullOrWhiteSpace(value)||value.Length>160||Regex.IsMatch(value,@"[\\/:*?""<>|\x00-\x1f]")) throw new ArgumentException("檔名格式包含非法字元 / Invalid filename template"); var remainder=Regex.Replace(value,@"\{(title|artist|album|quality|date)\}",""); if(remainder.Contains('{')||remainder.Contains('}'))throw new ArgumentException("僅支援 {title} {artist} {album} {quality} {date}"); }
    public static string FileStem(DownloadJob j, Preferences p) => Validation.SafeName((j.Mode==DownloadMode.Mp3?p.AudioNaming:p.VideoNaming).Replace("{title}",j.Title).Replace("{artist}",j.Artist).Replace("{album}",j.Album).Replace("{quality}",j.Quality).Replace("{date}",j.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")));
    public static List<string> Network(Preferences p, DateTime? now=null)
    {
        var a=new List<string>{"--socket-timeout",p.TimeoutSeconds.ToString(),"--retries",p.ConnectionRetries.ToString(),"--fragment-retries",p.ConnectionRetries.ToString(),"--concurrent-fragments",p.Fragments.ToString(),"--encoding","utf-8"};
        if(p.ProxyMode!="system")a.AddRange(["--proxy",p.ProxyMode=="off"?"":p.ProxyUrl]);
        var rate=p.EffectiveLimit(now??DateTime.Now); if(rate>0)a.AddRange(["--limit-rate",$"{rate}K"]);
        return a;
    }
    public static List<string> Format(DownloadJob j, Preferences p, MediaInfo? info=null)
    {
        var a=new List<string>();
        if(j.Mode==DownloadMode.Mp3) { a.AddRange(["-f","bestaudio/best","-x","--audio-format",j.Extension]); if(j.Extension is "mp3" or "m4a")a.AddRange(["--audio-quality",$"{j.AudioKbps}k"]); if(j.Extension!="mp3" && p.EmbedThumbnail && j.Extension!="wav")a.Add("--embed-thumbnail"); }
        else {
            var cap=$"[height<=?{VideoSelection.Cap(j.Height)}]";
            var codec=p.VideoCodec switch {"h264"=>"[vcodec^=avc]","h265"=>"[vcodec^=hev]","av1"=>"[vcodec^=av01]",_=>""};
            var selector=j.Extension=="mp4"&&p.VideoCodec=="auto" ? (info is null?null:VideoSelection.Select(info,j.Height)?.Selector)??VideoSelection.Fallback(j.Height) : j.Extension=="webm"?$"bv{cap}{codec}[ext=webm]+ba[ext=webm]/b{cap}{codec}[ext=webm]":$"bv{cap}{codec}+ba/b{cap}{codec}";
            a.AddRange(["-f",selector,"--merge-output-format",j.Extension,"--remux-video",j.Extension]);
            if(p.DownloadSubtitles){a.AddRange(["--write-subs","--write-auto-subs","--sub-langs",p.SubtitleLanguages,"--sub-format",$"{p.SubtitleFormat}/best","--convert-subs",p.SubtitleFormat]);if(p.EmbedSubtitles)a.Add("--embed-subs");}
        }
        if(p.KeepMetadata)a.Add("--embed-metadata");
        if(p.KeepThumbnail)a.AddRange(["--write-thumbnail","--convert-thumbnails","jpg"]);
        return a;
    }
}
