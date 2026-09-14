using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class DownloadOptionsTests
{
    [Theory][InlineData("mp3",true)][InlineData("m4a",true)][InlineData("flac",false)][InlineData("wav",false)]
    public void AudioFormatControlsEncoderAndLossyBitrate(string format,bool bitrate)
    {
        var args=DownloadOptions.Format(new(){Mode=DownloadMode.Mp3,OutputFormat=format,AudioKbps=192},new());
        Assert.Equal(format,args[args.IndexOf("--audio-format")+1]);
        Assert.Equal(bitrate,args.Contains("--audio-quality"));
        if(bitrate)Assert.Equal("192k",args[args.IndexOf("--audio-quality")+1]);
    }
    [Fact] public void NetworkScheduleCrossesMidnightAndOffIsExplicit()
    {
        var p=new Preferences{ProxyMode="off",ScheduleLimit=true,LimitStart="22:00",LimitEnd="06:00",ScheduledKiB=100,LimitKiB=0};
        var night=DownloadOptions.Network(p,new(2026,9,15,23,0,0));Assert.Equal("",night[night.IndexOf("--proxy")+1]);Assert.Contains("100K",night);
        Assert.DoesNotContain("--limit-rate",DownloadOptions.Network(p,new(2026,9,15,12,0,0)));
    }
    [Fact] public void FormatValidationRejectsUnsupportedContainerCodec()
    {
        Assert.Throws<ArgumentException>(()=>new Preferences{VideoFormat="webm",VideoCodec="h264"}.Validate());
        new Preferences{Concurrency=10,VideoFormat="mkv",VideoCodec="h265"}.Validate();
        Assert.Throws<ArgumentException>(()=>new Preferences{Concurrency=11}.Validate());
    }
    [Fact] public void DefaultSongNamePreservesNativeScriptAndNeverAddsArtist()
    {
        var j=new DownloadJob{Mode=DownloadMode.Mp3,Title="夜に駆ける",Artist="YOASOBI",Album="THE BOOK"};
        Assert.Equal("夜に駆ける",DownloadOptions.FileStem(j,new()));
        Assert.Equal("YOASOBI - 夜に駆ける",DownloadOptions.FileStem(j,new(){AudioNaming="{artist} - {title}"}));
    }
    [Fact] public async Task RenameCollisionPreservesBothFilesAndAllMatchingRecords()
    {
        var root=Path.Combine(Path.GetTempPath(),"omni-rename-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {
            var store=new Store(root);var source=Path.Combine(root,"original.mp3");File.WriteAllText(source,"audio");File.WriteAllText(Path.Combine(root,"new.mp3"),"existing");
            var a=new DownloadJob{State=JobState.Completed,FilePath=source,Title="Original",Mode=DownloadMode.Mp3};var b=new DownloadJob{State=JobState.Completed,FilePath=source};store.Save(a);store.Save(b);
            await using var engine=new Downloader(store,root,Path.Combine(root,"work"));
            await engine.RenameDownloaded(a.Id,"new");
            Assert.All(store.Load(),r=>Assert.Equal(Path.Combine(root,"new (1).mp3"),r.FilePath));
            Assert.Equal("existing",File.ReadAllText(Path.Combine(root,"new.mp3")));Assert.Equal("audio",File.ReadAllText(Path.Combine(root,"new (1).mp3")));
            Assert.Equal("Original",store.Load().Single(r=>r.Id==a.Id).Title);
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
    }
}
