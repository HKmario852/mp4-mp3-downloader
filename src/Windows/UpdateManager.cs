using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
namespace Omni.Windows;
public static class UpdateManager
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    public static Version CurrentVersion => typeof(UpdateManager).Assembly.GetName().Version ?? new Version(0,0,0);
    public static Func<Task>? ExitForUpdate { get; set; }
    static UpdateManager() => Client.DefaultRequestHeaders.UserAgent.ParseAdd("OmniDownloader/" + CurrentVersion);
    public static async Task Check(string repository)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) return;
        try
        {
            using var doc = JsonDocument.Parse(await Client.GetStringAsync($"https://api.github.com/repos/{repository}/releases/latest"));
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (Version.TryParse(tag.TrimStart('v'), out var version) && version > new Version(CurrentVersion.Major, CurrentVersion.Minor, CurrentVersion.Build)) Notifications.Show($"有新版本 {tag}，請在設定按「下載並安裝最新版本」。");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { Notifications.Show("暫時無法檢查更新，請稍後再試"); }
    }
    public static void Launch(string zip, string sha256, string releasesUrl, bool elevated = false, bool restart = false)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(sha256, "^[a-fA-F0-9]{64}$")) throw new ArgumentException("請提供發布頁的 SHA256");
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = elevated ? "runas" : "", WorkingDirectory = AppContext.BaseDirectory, WindowStyle = restart ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal };
        var runner = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(zip))!, "updater-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "updater.ps1"),runner);
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-ExecutionPolicy"); start.ArgumentList.Add("Bypass"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(runner);
        if (restart) { start.ArgumentList.Add("-Restart"); start.ArgumentList.Add("-NoRestartPrompt"); }
        foreach (var value in new[] { "-InstallPath", AppContext.BaseDirectory, "-AppPid", Environment.ProcessId.ToString(), "-ZipPath", Path.GetFullPath(zip), "-ExpectedSha256", sha256, "-ReleasesUrl", releasesUrl }) start.ArgumentList.Add(value);
        try { Process.Start(start); }
        catch (System.ComponentModel.Win32Exception) { throw new IOException("更新器未獲准啟動；管理員授權被取消或認證失敗。更新已中止。"); }
    }
    public static async Task DownloadAndLaunch(string repository, bool elevated = false, bool restart = true, IProgress<string>? progress = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new ArgumentException("請先設定有效 GitHub 專案");
        using var release = JsonDocument.Parse(await Client.GetStringAsync($"https://api.github.com/repos/{repository}/releases/latest"));
        var tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'),out var latest)) throw new IOException("發布版本號無效");
        if (latest <= new Version(CurrentVersion.Major,CurrentVersion.Minor,CurrentVersion.Build)) throw new IOException("目前已是最新版本 " + CurrentVersion.ToString(3));
        string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        var assets = release.RootElement.GetProperty("assets").EnumerateArray().Where(a => { var n = a.GetProperty("name").GetString() ?? ""; return n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && n.Contains("windows", StringComparison.OrdinalIgnoreCase) && n.Contains(arch, StringComparison.OrdinalIgnoreCase); }).ToArray();
        if (assets.Length != 1) throw new IOException("發布頁未提供唯一匹配本機架構的 Windows ZIP，請從 Releases 手動選擇");
        var asset = assets[0]; var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest is null || !System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) throw new IOException("發布資產沒有可信 SHA256，已停止自動更新");
        var url = asset.GetProperty("browser_download_url").GetString()!;
        if (!url.StartsWith($"https://github.com/{repository}/releases/download/", StringComparison.Ordinal)) throw new IOException("更新網址不屬於設定的發布專案");
        var folder = Path.Combine(Path.GetTempPath(), "Omni-updates"); Directory.CreateDirectory(folder); var zip = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        try
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) { response.EnsureSuccessStatusCode(); await using var input = await response.Content.ReadAsStreamAsync(); await using var output = File.Create(zip); var buffer=new byte[65536];long received=0;int n;
                while((n=await input.ReadAsync(buffer))>0){await output.WriteAsync(buffer.AsMemory(0,n));received+=n;progress?.Report($"正在下載更新：{received/1_000_000.0:F1} MB");} }
            await using (var input = File.OpenRead(zip)) { var actual = Convert.ToHexString(await SHA256.HashDataAsync(input)); if (!actual.Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new IOException("更新 ZIP 的 SHA256 不符"); }
            progress?.Report("更新已驗證，正在準備安裝…");
            Launch(zip, digest[7..], $"https://github.com/{repository}/releases", elevated, restart);
            if (restart && ExitForUpdate is not null) await ExitForUpdate();
        }
        catch { if (File.Exists(zip)) File.Delete(zip); throw; }
    }
}
