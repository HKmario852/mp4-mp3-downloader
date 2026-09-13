using Omni.Core;
using Xunit;
namespace Omni.Tests;

public sealed class LibraryFeatures
{
    [Fact] public void EstimatesFollowChosenIdsAndNativePreferenceOrder()
    {
        var info = new MediaInfo("video", "", "", null, 60, [
            new("m4a", null, false, true, 1_000_000, 128, "140"),
            new("mp4", 1080, true, false, 60_000_000, 8000, "old", "avc1"),
            new("mp4", 1080, true, false, 30_000_000, 4000, "preferred", "av01"),
            new("mp4", 2160, true, false, 80_000_000, 9000, "4k", "av01"),
            new("mp4", 4320, true, false, 200_000_000, 18000, "8k", "av01")]);
        Assert.Equal("preferred+140", VideoSelection.Select(info,1080)!.Selector);
        Assert.Equal(31_000_000, SizeEstimator.Estimate(info,DownloadMode.Mp4,1080));
        Assert.Equal("4k+140", VideoSelection.Select(info,0)!.Selector);
        Assert.Equal("4k+140", VideoSelection.Select(info,4320)!.Selector);
        Assert.Contains("2160", VideoSelection.Fallback(0));
        Assert.DoesNotContain("4320", VideoSelection.Fallback(4320));
    }
    [Fact] public void LegacyDirectoryAndSeparateDirectoriesRoundTrip()
    {
        var p = new Preferences { DownloadDirectory = Path.GetTempPath() };
        Assert.Equal(p.DownloadDirectory,p.DirectoryFor(DownloadMode.Mp3));
        p.SetDirectory(DownloadMode.Mp3,Path.Combine(Path.GetTempPath(),"music"));
        p.SetDirectory(DownloadMode.Mp4,Path.Combine(Path.GetTempPath(),"videos"));
        p = Json.Decode<Preferences>(Json.Encode(p)); p.Validate();
        Assert.NotEqual(p.DirectoryFor(DownloadMode.Mp3),p.DirectoryFor(DownloadMode.Mp4));
        p.VideoHeight=4320; Assert.Throws<ArgumentException>(p.Validate);
    }
    [Fact] public async Task FormatFilterClearKeepsOtherHistoryAndPhysicalFiles()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-test-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        try {
            var store=new Store(dir);
            var jobs=new[]{DownloadMode.Mp3,DownloadMode.Mp4}.Select(mode=>new DownloadJob{Title="Music",Artist="Mario",Mode=mode,State=JobState.Completed,FilePath=Path.Combine(dir,"file."+mode)}).ToArray();
            foreach(var j in jobs){await File.WriteAllTextAsync(j.FilePath!,"fixture");store.Save(j);}
            var ids=store.Load(true).Where(j=>HistorySearch.Matches(j,"music mario",DownloadMode.Mp3)).Select(j=>j.Id).ToArray();
            Assert.Single(ids);store.ClearHistory(ids);Assert.Equal(DownloadMode.Mp4,Assert.Single(store.Load(true)).Mode);Assert.All(jobs,j=>Assert.True(File.Exists(j.FilePath)));
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public async Task IntakeSnapshotsDirectoriesAndEditedRecordsReload()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try {
            var store=new Store(dir); var done=new DownloadJob{State=JobState.Completed,Mode=DownloadMode.Mp3,Title="before"};store.Save(done);
            await using var engine=new Downloader(store,dir,Path.Combine(dir,"work"));
            var p=engine.Settings;p.Mp3Directory=Path.Combine(dir,"music");p.Mp4Directory=Path.Combine(dir,"videos");engine.SaveSettings(p);
            foreach(var mode in new[]{"mp3","mp4"}) await engine.Accept(new(Guid.NewGuid().ToString(),"https://www.youtube.com/watch?v=a&list=b",mode));
            Assert.All(engine.Jobs.Where(j=>j.State==JobState.PendingChoice),j=>Assert.Equal(p.DirectoryFor(j.Mode),j.Directory));
            done.Title="after";store.Save(done);engine.ReloadEditedJobs([done.Id]);Assert.Equal("after",engine.Jobs.Single(j=>j.Id==done.Id).Title);
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
