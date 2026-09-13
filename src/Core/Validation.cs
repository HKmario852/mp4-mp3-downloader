using System.Text;
using System.Text.RegularExpressions;

namespace Omni.Core;
public static partial class Validation
{
    [GeneratedRegex("[\\\\/:*?\"<>|\\x00-\\x1f]")] private static partial Regex InvalidName();
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)] private static partial Regex DeviceName();
    [GeneratedRegex(@"\s*[\[(【](?:official\s*(?:music\s*)?video|official audio|lyrics?|歌詞|官方MV|MV|HD|4K)[\])】]\s*$", RegexOptions.IgnoreCase)] private static partial Regex Suffix();
    public static string? TitleError(string title)
    {
        if (InvalidName().IsMatch(title)) return "不可包含字元：\\ / : * ? \" < > | 或控制字元";
        if (title.Length > 180) return "歌曲名過長（最多 180 字元）";
        if (DeviceName().IsMatch(title) || title is "." or ".." || title.EndsWith('.') || title.EndsWith(' ')) return "不可使用系統保留檔名、結尾空格或句號";
        return null;
    }
    public static string CleanTitle(string title) => Suffix().Replace(title, "").Trim();
    // Automatic source imports may sanitize; the tag editor must never use this method.
    public static string SafeName(string name)
    {
        var s = InvalidName().Replace(name, " ").Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(s)) s = "未命名";
        if (DeviceName().IsMatch(s)) s = "_" + s;
        return s.Length > 160 ? s[..160] : s;
    }
    public static Uri WebUrl(string raw, bool youtubeOnly = false)
    {
        if (raw.Length > 8192 || !Uri.TryCreate(raw, UriKind.Absolute, out var u) || u.Scheme != "https" || !string.IsNullOrEmpty(u.UserInfo) || !u.IsDefaultPort || u.HostNameType != UriHostNameType.Dns) throw new ArgumentException("請輸入有效 HTTPS 影片網址");
        if (youtubeOnly && u.Host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be")) throw new ArgumentException("擴充功能只接受 YouTube 網址");
        return u;
    }
    public static Dictionary<string, string> Query(Uri u)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pair.Split('=', 2); var key = Uri.UnescapeDataString(p[0]);
            if (!result.TryAdd(key, p.Length == 2 ? Uri.UnescapeDataString(p[1]) : "")) throw new ArgumentException("重複網址參數");
        }
        return result;
    }
    public static bool Compound(string url) { var q = Query(WebUrl(url)); return q.ContainsKey("v") && q.ContainsKey("list"); }
    public static IntakeRequest ParseProtocol(string raw)
    {
        if (raw.Length > 12000 || !Uri.TryCreate(raw, UriKind.Absolute, out var u) || u.Scheme != "ytdl" || u.Host != "download" || u.AbsolutePath is not ("" or "/") || u.UserInfo != "" || !u.IsDefaultPort) throw new ArgumentException("無效協定");
        var q = Query(u);
        if (q.Keys.Any(k => k is not ("url" or "mode"))) throw new ArgumentException("協定不接受憑證或其他參數");
        var url = q.GetValueOrDefault("url") ?? throw new ArgumentException("缺少網址"); WebUrl(url, true);
        var mode = q.GetValueOrDefault("mode"); if (mode is not ("mp3" or "mp4")) throw new ArgumentException("無效格式");
        return new(Guid.NewGuid().ToString("N"), url, mode);
    }
    public static void Request(IntakeRequest r)
    {
        if (!Guid.TryParse(r.RequestId, out _) || r.Mode is not ("mp3" or "mp4")) throw new ArgumentException("無效請求");
        WebUrl(r.Url, r.Cookies?.Count > 0);
        if (r.Cookies is { Count: > 200 }) throw new ArgumentException("Cookie 數量超限");
        foreach (var c in r.Cookies ?? [])
        {
            if (c.Domain.TrimStart('.') is not ("youtube.com" or "www.youtube.com" or "m.youtube.com") || !c.Path.StartsWith('/') || new[] { c.Name, c.Value, c.Domain, c.Path }.Any(s => s.Length > 8192 || s.Any(ch => ch < 32 || ch == 127))) throw new ArgumentException("無效 Cookie");
        }
    }
    public static string Netscape(IEnumerable<BrowserCookie> cookies)
    {
        var s = new StringBuilder("# Netscape HTTP Cookie File\n");
        foreach (var c in cookies) s.AppendJoin('\t', (c.HttpOnly ? "#HttpOnly_" : "") + c.Domain, c.HostOnly ? "FALSE" : "TRUE", c.Path, c.Secure ? "TRUE" : "FALSE", ((long)(c.ExpirationDate ?? 0)).ToString(), c.Name, c.Value).Append('\n');
        return s.ToString();
    }
    public static string UniquePath(string directory, string name, string extension)
    {
        if (TitleError(name) is string error || name.Length == 0) throw new ArgumentException(TitleError(name) ?? "空檔名");
        var path = Path.Combine(directory, name + extension); int i = 1;
        while (File.Exists(path)) path = Path.Combine(directory, $"{name} ({i++}){extension}");
        return path;
    }
}
public sealed class SpeedEma
{
    private long previous; private double value;
    public (double speed, long eta) Sample(long bytes, long? total, double elapsedSeconds, bool active)
    {
        var raw = elapsedSeconds > 0 ? Math.Max(0, bytes - previous) / elapsedSeconds : 0; previous = bytes;
        value = !active || raw == 0 ? 0 : value == 0 ? raw : .25 * raw + .75 * value;
        return (value, value > 0 && total.HasValue ? (long)Math.Ceiling(Math.Max(0, total.Value - bytes) / value) : 0);
    }
}
public static class HistorySearch
{
    public static bool Matches(DownloadJob j, string search, DownloadMode? mode = null)
    {
        var fields = new[] { j.Title, j.Url, Path.GetFileName(j.FilePath ?? ""), j.Artist, j.Album };
        return (mode is null || j.Mode == mode) && search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(k => fields.Any(f => f.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }
}
