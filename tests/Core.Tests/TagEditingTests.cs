using Omni.Core;
using Xunit;
namespace Omni.Tests;
public class TagEditingTests
{
    [Fact] public async Task CommentsCoversAndOptionalRenamePreserveAudio()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-tags-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{
            var source=Path.Combine(dir,"original.mp3");byte[] audio=[255,251,144,0,1,2,3,4];File.WriteAllBytes(source,audio);
            var store=new Store(Path.Combine(dir,"data"));var j=new DownloadJob{FilePath=source,Title="Original",Mode=DownloadMode.Mp3};store.Save(j);
            var tag=new TagEditor(store);byte[] art=[137,80,78,71,1,2,3];
            await tag.Apply([j],new(new(){["TIT2"]="城市夜色",["COMM"]="廣東話註解",["TPE2"]="專輯演出者",["TPOS"]="2",["TCOM"]="作曲者"},Covers:[new(art,"image/png","Cover",3)],RenameFile:false));
            Assert.True(j.CoverUserEdited);Assert.True(store.Load().Single().CoverUserEdited);Assert.Equal(source,j.FilePath);var d=Id3Document.Read(source);Assert.Equal("廣東話註解",d.Text("COMM"));Assert.Equal("專輯演出者",d.Text("TPE2"));Assert.Equal(art,d.GetCovers().Single().Bytes);Assert.Equal(audio,File.ReadAllBytes(source).Skip((int)d.AudioOffset).ToArray());
            await tag.Apply([j],new(new(){["COMM"]="",["TRCK"]="1"},Covers:[],RenameFile:false));
            d=Id3Document.Read(source);Assert.Empty(d.GetCovers());Assert.Equal("",d.Text("COMM"));Assert.Contains(d.Frames,f=>f.Id=="COMM");Assert.Equal(source,j.FilePath);
            await tag.Apply([j],new(new(){["TIT2"]="歌曲新名"},RenameFile:true));Assert.Equal("歌曲新名.mp3",Path.GetFileName(j.FilePath));Assert.False(File.Exists(source));
        }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public async Task ImportEditsDoNotCreateDownloadHistory()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-import-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{var path=Path.Combine(dir,"local.mp3");File.WriteAllBytes(path,[1,2,3]);var store=new Store(Path.Combine(dir,"data"));var j=new DownloadJob{FilePath=path};await new TagEditor(store).Apply([j],new(new(){["TRCK"]="1"},RenameFile:false),persist:false);Assert.Empty(store.Load());Assert.Equal("1",Id3Document.Read(path).Text("TRCK"));}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public async Task AutomaticCoverPreservesMetadataAndDoesNotLockUserArtwork(){var dir=Path.Combine(Path.GetTempPath(),"omni-art-"+Guid.NewGuid());Directory.CreateDirectory(dir);try{var path=Path.Combine(dir,"song.mp3");File.WriteAllBytes(path,[1,2,3]);var store=new Store(Path.Combine(dir,"data"));var j=new DownloadJob{FilePath=path,Title="Keep title",Artist="Keep artist"};await new TagEditor(store).Apply([j],new([],Covers:[new(AlbumArtworkTests.Png(475,500),"image/png","Album front",3)],RenameFile:false),automaticCover:true);Assert.False(j.IsUserEdited);Assert.False(j.CoverUserEdited);Assert.Equal("Keep title",j.Title);Assert.Equal("Keep artist",j.Artist);Assert.Equal(path,j.FilePath);Assert.Single(Id3Document.Read(path).GetCovers());}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}}
    [Theory][InlineData(79,100)][InlineData(100,151)] public void ScaleLimits(int text,int ui)=>Assert.Throws<ArgumentException>(()=>new Preferences{TextScale=text,UiScale=ui}.Validate());
}
