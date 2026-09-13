using Omni.Core;
using Xunit;
namespace Omni.Tests;
public class Removal
{
    [Fact] public async Task ThumbnailIsTheFirstFrontCoverAndAlbumIsSecondary()
    {
        var path=Path.GetTempFileName();var output=path+".mp3";
        try {
            var thumb=new Cover([1,2,3],"image/jpeg","Video thumbnail",3);
            var album=new Cover([4,5,6],"image/png","Album",3);
            var covers=CoverOrder.ThumbnailFirst([album],thumb);
            var tag=Id3Document.Read(path);tag.SetCovers(covers);await tag.Write(path,output);
            var apic=Id3Document.Read(output).Frames.Where(f=>f.Id=="APIC").ToArray();
            Assert.Equal(2,apic.Length);Assert.Equal(3,apic[0].Data[12]);Assert.Equal(0,apic[1].Data[11]);
            Assert.Equal(thumb.Bytes,apic[0].Data[^3..]);Assert.Equal(album.Bytes,apic[1].Data[^3..]);
        } finally {File.Delete(path);File.Delete(output);}
    }
    [Fact] public async Task RemovePausedFailedAndPendingKeepsCompletedFile()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-removal-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try {
            var store=new Store(dir);var work=Path.Combine(dir,"work");
            var jobs=new[]{JobState.Paused,JobState.Failed,JobState.PendingChoice,JobState.Completed}.Select(state=>new DownloadJob{State=state}).ToArray();
            foreach(var j in jobs){store.Save(j);var folder=Path.Combine(work,j.Id);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"media.part"),"test");}
            await using(var engine=new Downloader(store,dir,work)) {
                await engine.Cancel(jobs.Select(j=>j.Id));
                Assert.All(engine.Jobs.Where(j=>j.Id!=jobs[^1].Id),j=>Assert.Equal(JobState.Cancelled,j.State));
                Assert.All(jobs.Take(3),j=>Assert.False(Directory.Exists(Path.Combine(work,j.Id))));
                Assert.True(File.Exists(Path.Combine(work,jobs[^1].Id,"media.part")));
            }
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
