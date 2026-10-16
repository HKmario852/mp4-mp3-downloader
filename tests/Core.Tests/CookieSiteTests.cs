using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class CookieSiteTests : IDisposable
{
    readonly string file = Path.GetTempFileName();
    public void Dispose() => File.Delete(file);
    string Jar(params string[] domains)
    {
        File.WriteAllLines(file, new[] { "# Netscape HTTP Cookie File" }.Concat(domains.Select(d => $"{d}\tTRUE\t/\tTRUE\t0\tauth_token\tx")));
        return file;
    }

    [Theory]
    [InlineData("https://x.com/user/status/1", true)]
    [InlineData("https://twitter.com/user/status/1", true)]
    [InlineData("https://mobile.twitter.com/user/status/1", true)]
    [InlineData("https://www.youtube.com/watch?v=a", false)]
    public void UsesXCookiesOnlyForX(string url, bool expected) => Assert.Equal(expected, Validation.CookieFileCovers(Jar(".x.com"), url));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=a", true)]
    [InlineData("https://youtu.be/a", true)]
    [InlineData("https://music.youtube.com/watch?v=a", true)]
    [InlineData("https://x.com/user/status/1", false)]
    public void UsesYouTubeCookiesOnlyForYouTube(string url, bool expected) => Assert.Equal(expected, Validation.CookieFileCovers(Jar(".youtube.com"), url));

    [Fact]
    public void ReadsHttpOnlyLinesAndIgnoresComments()
    {
        File.WriteAllLines(file, ["# Netscape HTTP Cookie File", "# .x.com\tTRUE\t/\tTRUE\t0\tc\tv", "#HttpOnly_.twitter.com\tTRUE\t/\tTRUE\t0\tauth_token\tv"]);
        Assert.True(Validation.CookieFileCovers(file, "https://x.com/a/status/1"));
        File.WriteAllLines(file, ["# Netscape HTTP Cookie File", "# .x.com\tTRUE\t/\tTRUE\t0\tc\tv"]);
        Assert.False(Validation.CookieFileCovers(file, "https://x.com/a/status/1"));
    }

    [Fact]
    public void ExplainsSignInForSensitivePosts()
    {
        Assert.Contains("cookies.txt", ProcessRunner.Diagnose("ERROR: [twitter] 123: NSFW tweet requires authentication. Use --cookies-from-browser or --cookies for the authentication."));
        Assert.Contains("cookies.txt", ProcessRunner.Diagnose("ERROR: [twitter] 9: You are not authorized to view this protected tweet. Use --cookies-from-browser or --cookies for the authentication."));
        Assert.Contains("年齡確認", ProcessRunner.Diagnose("ERROR: Sign in to confirm your age. Use --cookies-from-browser or --cookies")); // YouTube keeps its own message
    }
}
