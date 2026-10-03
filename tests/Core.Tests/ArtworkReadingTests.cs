using System.Text;
using Omni.Core;
using Xunit;
namespace Omni.Tests;

public class ArtworkReadingTests
{
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(16)]
    public void ReadsPaddedJpegWithoutChangingOriginalFrame(int padding)
    {
        byte[] image=[255,216,255,254,0,4,1,2,255,217];
        byte[] frame=[0,..Encoding.ASCII.GetBytes("image/jpeg"),0,3,0,..new byte[padding],..image];
        var doc=new Id3Document();doc.Frames.Add(new("APIC",[0,0],frame));
        Assert.Equal(image,doc.GetCovers().Single().Bytes);
        Assert.Equal(frame,doc.Frames.Single().Data);
    }
    [Fact] public void PreservesUnicodeDescriptionAndPng()
    {
        var image=AlbumArtworkTests.Png(500,500);var doc=new Id3Document();
        doc.Frames.Add(new("APIC",[0,0],[1,..Encoding.ASCII.GetBytes("image/png"),0,3,255,254,..Encoding.Unicode.GetBytes("專輯封面 日本語"),0,0,0,..image]));
        var cover=doc.GetCovers().Single();Assert.Equal("專輯封面 日本語",cover.Description);Assert.Equal(image,cover.Bytes);
    }
    [Fact] public void DoesNotStripUnknownBytesOrSearchArbitraryPayload()
    {
        var doc=new Id3Document();byte[] payload=[0,1,255,216,255,1,2];
        doc.SetRaw("APIC",[0,..Encoding.ASCII.GetBytes("image/jpeg"),0,3,0,..payload]);
        Assert.Equal(payload,doc.GetCovers().Single().Bytes);
        doc.SetRaw("APIC",[0,..Encoding.ASCII.GetBytes("image/jpeg"),0,3,65,66]);Assert.Empty(doc.GetCovers());
    }
    [Fact] public async Task EditingTextPreservesPaddedArtworkFrameAndAudio()
    {
        var folder=Path.Combine(Path.GetTempPath(),"omni-padded-art-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            var source=Path.Combine(folder,"raw.mp3");var path=Path.Combine(folder,"song.mp3");byte[] audio=[255,251,1,2,3,4];File.WriteAllBytes(source,audio);
            var doc=Id3Document.Read(source);byte[] frame=[0,..Encoding.ASCII.GetBytes("image/jpeg"),0,3,0,0,255,216,255,254,0,4,1,2,255,217];
            doc.SetRaw("APIC",frame);doc.SetText("TPE1","保留演出者");await doc.Write(source,path);
            var store=new Store(Path.Combine(folder,"db"));await new TagEditor(store).Apply([new(){FilePath=path}],new(new(){["TALB"]="新專輯"},RenameFile:false),persist:false);
            var read=Id3Document.Read(path);Assert.Equal(frame,read.Frames.Single(f=>f.Id=="APIC").Data);Assert.Equal("保留演出者",read.Text("TPE1"));Assert.Equal("新專輯",read.Text("TALB"));Assert.Equal(audio,File.ReadAllBytes(path).Skip((int)read.AudioOffset));
        }
        finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
    }
}
