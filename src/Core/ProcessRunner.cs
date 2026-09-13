using System.Diagnostics;
using System.Text;
namespace Omni.Core;
public static class ProcessRunner
{
    public static async Task<string> Run(string executable, IEnumerable<string> args, Action<string>? line, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var p = new Process { StartInfo = start };
        var output = new StringBuilder(); var error = new StringBuilder();
        if (!p.Start()) throw new IOException("無法啟動下載工具");
        using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { } });
        var stdout = Task.Run(async () => { while (await p.StandardOutput.ReadLineAsync() is { } s) { if (output.Length < 16_000_000) output.AppendLine(s); line?.Invoke(s); } });
        var stderr = Task.Run(async () => { while (await p.StandardError.ReadLineAsync() is { } s) { if (error.Length < 32_000) error.AppendLine(s); } });
        await p.WaitForExitAsync(CancellationToken.None); await Task.WhenAll(stdout, stderr); ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0) throw new DownloadException(Diagnose(error.ToString()), Redact(error.ToString()));
        return output.ToString();
    }
    public static string Redact(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"https?://\S+|(?i)(cookie|authorization|token)\s*[:=].*", "[已隱藏敏感資料]");
    public static string Diagnose(string s) => s.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) ? "網站的防機械人驗證阻擋下載，請改用可直接存取的來源；本工具不會繞過驗證。" : s.Contains("Sign in", StringComparison.OrdinalIgnoreCase) || s.Contains("403") ? "網站要求登入或拒絕存取，請重新登入並從擴充功能重送；會員權限仍由網站決定。" : s.Contains("No space", StringComparison.OrdinalIgnoreCase) ? "儲存空間不足，請釋放空間後重試。" : s.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase) ? "媒體處理失敗，請檢查 ffmpeg 與來源格式。" : "下載失敗，請檢查網路或更新 yt-dlp，詳細原因見原始診斷。";
}
public sealed class DownloadException(string message, string stderr) : Exception(message) { public string Stderr { get; } = stderr; }
