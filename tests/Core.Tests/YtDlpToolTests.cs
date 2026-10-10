using System.Net;
using System.Security.Cryptography;
using System.Text;
using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class YtDlpToolTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("omni-ytdlp-").FullName;
    string Bundled => Path.Combine(root, "app");
    string Tools => Path.Combine(root, "data", "tools");
    public void Dispose() => Directory.Delete(root, true);

    // The fake "exe" holds its version as text; the probe reads it back instead of running a process.
    static Task<string?> Probe(string exe, CancellationToken _) => Task.FromResult<string?>(File.ReadAllText(exe).Trim());
    YtDlpTool Tool(string bundledVersion, Fake http)
    {
        Directory.CreateDirectory(Bundled); File.WriteAllText(Path.Combine(Bundled, "yt-dlp.exe"), bundledVersion);
        return new YtDlpTool(Bundled, Tools, new HttpClient(http), Probe);
    }
    static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    static string Release(string tag, string? digest, string url = "https://github.com/yt-dlp/yt-dlp/releases/download/x/yt-dlp.exe") =>
        $$"""{"tag_name":"{{tag}}","assets":[{"name":"yt-dlp.exe","browser_download_url":"{{url}}"{{(digest is null ? "" : $",\"digest\":\"{digest}\"")}}}]}""";

    [Theory]
    [InlineData("2026.09.30", "2026.08.19", true)]
    [InlineData("2026.08.19.1", "2026.08.19", true)]
    [InlineData("2026.08.19", "2026.08.19", false)]
    [InlineData("2026.08.01", "2026.08.19", false)]
    [InlineData("2026.08.19", null, true)]
    [InlineData("nightly", "2026.08.19", false)]
    public void ComparesDateVersions(string candidate, string? current, bool newer) => Assert.Equal(newer, YtDlpTool.IsNewer(candidate, current));

    [Fact]
    public async Task InstallsANewerVerifiedRelease()
    {
        var tool = Tool("2026.08.19", new Fake(Release("2026.09.30", "sha256:" + Sha("2026.09.30")), "2026.09.30"));
        var result = await tool.Update();
        Assert.True(result.Updated); Assert.Equal("2026.09.30", result.Version);
        Assert.Equal(Path.Combine(Tools, "yt-dlp.exe"), tool.ExecutablePath);
        // A restart picks the downloaded copy again, and an app update with an even newer bundled copy wins
        var again = new YtDlpTool(Bundled, Tools, new HttpClient(new Fake("", "")), Probe); await again.Refresh();
        Assert.Equal(tool.ExecutablePath, again.ExecutablePath);
        File.WriteAllText(Path.Combine(Bundled, "yt-dlp.exe"), "2026.10.05"); await again.Refresh();
        Assert.Equal(Path.Combine(Bundled, "yt-dlp.exe"), again.ExecutablePath); Assert.Equal("2026.10.05", again.Version);
    }

    [Fact]
    public async Task SkipsTheDownloadWhenUpToDate()
    {
        var http = new Fake(Release("2026.08.19", "sha256:" + Sha("x")), "x");
        var result = await Tool("2026.08.19", http).Update();
        Assert.False(result.Updated); Assert.Equal(1, http.Requests);
        Assert.False(File.Exists(Path.Combine(Tools, "yt-dlp.exe")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task RejectsMissingOrWrongChecksums(string? digest)
    {
        var tool = Tool("2026.08.19", new Fake(Release("2026.09.30", digest), "2026.09.30"));
        await Assert.ThrowsAsync<IOException>(() => tool.Update());
        Assert.Equal(Path.Combine(Bundled, "yt-dlp.exe"), tool.ExecutablePath);
        Assert.Empty(Directory.EnumerateFiles(Tools, "*.exe*"));
    }

    [Fact]
    public async Task RejectsDownloadsFromOtherHosts()
    {
        var tool = Tool("2026.08.19", new Fake(Release("2026.09.30", "sha256:" + Sha("2026.09.30"), "https://example.com/yt-dlp.exe"), "2026.09.30"));
        await Assert.ThrowsAsync<IOException>(() => tool.Update());
    }

    [Fact]
    public async Task ChecksAtMostOnceADay()
    {
        var http = new Fake(Release("2026.08.19", "sha256:" + Sha("x")), "x");
        var tool = Tool("2026.08.19", http);
        Assert.NotNull(await tool.UpdateIfDue());
        Assert.Null(await tool.UpdateIfDue());
        Assert.Equal(1, http.Requests);
    }

    sealed class Fake(string releaseJson, string binary) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            var body = request.RequestUri!.Host == "api.github.com" ? releaseJson : binary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
