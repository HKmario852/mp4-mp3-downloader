using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class Invariants
{
    [Fact] public void RawFrameEditPreservesOtherInstances() { var path = Path.GetTempFileName(); try { var d = Id3Document.Read(path); d.SetRaw("TXXX", [0, 1]); d.SetRaw("TXXX#1", [0, 2]); d.SetRaw("TXXX", [0, 3]); Assert.Equal(new byte[] { 0, 2 }, d.Frames[1].Data); } finally { File.Delete(path); } }
    [Fact] public async Task FramingUsesUtf8ByteCountAndRejectsOversize() { using var stream = new MemoryStream(); await Ipc.Write(stream, new IntakeRequest(Guid.NewGuid().ToString(), "https://www.youtube.com/watch?v=日本語", "mp3"), CancellationToken.None); stream.Position = 0; var r = await Ipc.Read<IntakeRequest>(stream, CancellationToken.None); Assert.Contains("日本語", r.Url); using var bad = new MemoryStream(BitConverter.GetBytes(Ipc.MaxBytes + 1)); await Assert.ThrowsAsync<InvalidDataException>(() => Ipc.Read<IntakeRequest>(bad, CancellationToken.None)); }
    [Theory]
    [InlineData("a/b")]
    [InlineData("CON")]
    [InlineData("Song.")]
    [InlineData("a\n")]
    [InlineData("a:b")]
    public void RejectIllegalTitles(string title) => Assert.NotNull(Validation.TitleError(title));
    [Theory]
    [InlineData("夜に駆ける")]
    [InlineData("مرحبا")]
    [InlineData("봄날")]
    [InlineData("")]
    public void PreserveUnicodeAndEmptyTag(string title) => Assert.Null(Validation.TitleError(title));
    [Fact]
    public void ProtocolParsesOnceAndRejectsSecrets()
    {
        var url = "https://www.youtube.com/watch?v=abc&list=def"; var r = Validation.ParseProtocol("ytdl://download?url=" + Uri.EscapeDataString(url) + "&mode=mp3"); Assert.Equal(url, r.Url); Assert.True(Validation.Compound(r.Url));
        Assert.Throws<ArgumentException>(() => Validation.ParseProtocol("ytdl://download?url=" + Uri.EscapeDataString(url) + "&mode=mp3&cookies=secret"));
        Assert.Throws<ArgumentException>(() => Validation.ParseProtocol("ytdl://download?url=" + Uri.EscapeDataString(url) + "&mode=mp3&mode=mp4"));
    }
    [Fact] public void CookieNewlinesRejected() => Assert.Throws<ArgumentException>(() => Validation.Request(new(Guid.NewGuid().ToString(), "https://www.youtube.com/watch?v=a", "mp3", [new("SID", "x\ny", ".youtube.com", "/", true, true, false, null)])));
    [Fact] public void SearchAndAcrossFields() { var j = new DownloadJob { Title = "夜に駆ける", Artist = "YOASOBI", Album = "THE BOOK", Url = "https://example.org/a" }; Assert.True(HistorySearch.Matches(j, "夜 yoasobi book")); Assert.False(HistorySearch.Matches(j, "夜 other")); }
    [Fact] public void EmaResetsWhenIdle() { var e = new SpeedEma(); Assert.Equal(2000, e.Sample(1000, 5000, .5, true).speed); Assert.Equal(2500, e.Sample(3000, 5000, .5, true).speed); Assert.Equal((0d, 0L), e.Sample(3000, 5000, .5, true)); Assert.Equal((0d, 0L), e.Sample(4000, 5000, .5, false)); }
    [Fact]
    public async Task EmptyFrameAndUnicodeRoundTripPreservesAudio()
    {
        var dir = Path.Combine(Path.GetTempPath(), "omni-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try { var source = Path.Combine(dir, "a.mp3"); byte[] audio = [0xff, 0xfb, 0, 1, 2, 3]; await File.WriteAllBytesAsync(source, audio); var d = Id3Document.Read(source); d.SetText("TIT2", "夜に駆ける"); d.SetText("TALB", ""); d.SetRaw("PRIV", [1, 2, 3]); var target = Path.Combine(dir, "b.mp3"); await d.Write(source, target); var read = Id3Document.Read(target); Assert.Equal("夜に駆ける", read.Text("TIT2")); Assert.Contains(read.Frames, f => f.Id == "TALB" && f.Data.Length > 0); Assert.Equal(new byte[] { 1, 2, 3 }, read.Frames.First(f => f.Id == "PRIV").Data); Assert.Equal(audio, (await File.ReadAllBytesAsync(target))[(int)read.AudioOffset..]); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task BatchTrackDoesNotRenameAndClearHistoryKeepsFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "omni-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try { var store = new Store(dir); var jobs = new[] { "一", "二" }.Select(name => new DownloadJob { Title = name, FilePath = Path.Combine(dir, name + ".mp3"), State = JobState.Completed }).ToArray(); foreach (var j in jobs) { await File.WriteAllBytesAsync(j.FilePath!, [0xff, 0xfb, 1]); store.Save(j); } await new TagEditor(store).Apply(jobs, new(new() { { "TRCK", "1" }, { "TALB", "" } })); foreach (var j in jobs) { Assert.True(File.Exists(j.FilePath)); Assert.Equal("1", Id3Document.Read(j.FilePath!).Text("TRCK")); } var snapshot = new[] { jobs[0].Id }; store.ClearHistory(snapshot); Assert.Single(store.Load(true)); Assert.All(jobs, j => Assert.True(File.Exists(j.FilePath))); }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(dir, true); }
    }
    [Fact]
    public async Task CompoundRequestNeverStartsBeforeChoiceAndDeduplicates()
    {
        var dir = Path.Combine(Path.GetTempPath(), "omni-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try { var store = new Store(dir); await using (var engine = new Downloader(store, dir, Path.Combine(dir, "work"))) { var r = new IntakeRequest(Guid.NewGuid().ToString(), "https://www.youtube.com/watch?v=abc&list=def", "mp3"); await engine.Accept(r); await engine.Accept(r); await Task.Delay(700); Assert.Single(engine.Jobs); Assert.Equal(JobState.PendingChoice, engine.Jobs[0].State); await engine.Cancel(engine.Jobs.Select(j => j.Id)); Assert.Equal(JobState.Cancelled, engine.Jobs[0].State); } }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(dir, true); }
    }
}
