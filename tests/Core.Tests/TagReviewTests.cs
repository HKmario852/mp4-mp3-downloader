using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class TagReviewTests
{
    [Fact] public async Task SelectedTagsPreserveOtherFramesAudioAndUndoExactly(){
        var dir=Path.Combine(Path.GetTempPath(),"omni-review-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{var path=Path.Combine(dir,"original.mp3");var raw=Path.Combine(dir,"source.mp3");File.WriteAllBytes(raw,[1,2,3,4,5]);var d=Id3Document.Read(raw);d.SetText("TIT2","原名");d.SetText("TPE1","原演出者");d.SetRaw("PRIV",[7,8,9]);d.SetCovers([new([10,20,30],"image/png","Keep",3)]);await d.Write(raw,path);var before=File.ReadAllBytes(path);var store=new Store(Path.Combine(dir,"db"));var job=new DownloadJob{FilePath=path,Title="原名",Artist="原演出者"};store.Save(job);
            var undo=await TagReviewUndo.Apply(store,job,new(){["TIT2"]="A LETTER <nZk Ver.>",["TXXX:MusicBrainz Recording Id"]="Unicode 測試"},null,true,await TagReview.Hash(path));
            var after=Id3Document.Read(path);Assert.Equal("A LETTER <nZk Ver.>",after.Text("TIT2"));Assert.Equal("Unicode 測試",after.Text("TXXX:MusicBrainz Recording Id"));Assert.Equal("原演出者",after.Text("TPE1"));Assert.Equal(d.Frames.Single(f=>f.Id=="PRIV").Data,after.Frames.Single(f=>f.Id=="PRIV").Data);Assert.Equal(d.GetCovers()[0].Bytes,after.GetCovers()[0].Bytes);Assert.Equal(new byte[]{1,2,3,4,5},File.ReadAllBytes(path).Skip((int)after.AudioOffset));Assert.Equal(path,job.FilePath);
            await undo!.Restore(store,job);Assert.Equal(before,File.ReadAllBytes(path));Assert.Equal("原名",job.Title);Assert.False(job.IsUserEdited);
        }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public async Task StaleReviewCannotOverwriteExternalChanges(){var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);try{var file=Path.Combine(dir,"song.mp3");File.WriteAllBytes(file,[1,2,3]);var hash=await TagReview.Hash(file);File.AppendAllText(file,"changed");await Assert.ThrowsAsync<IOException>(()=>TagReviewUndo.Apply(new Store(Path.Combine(dir,"db")),new(){FilePath=file},new(){["TIT2"]="new"},null,false,hash));}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}}
    [Fact] public async Task UndoRefusesToClobberLaterEdits(){var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);try{var file=Path.Combine(dir,"song.mp3");File.WriteAllBytes(file,[1,2,3]);var store=new Store(Path.Combine(dir,"db"));var job=new DownloadJob{FilePath=file};var undo=await TagReviewUndo.Apply(store,job,new(){["TIT2"]="new"},null,true,await TagReview.Hash(file));File.AppendAllText(file,"later");await Assert.ThrowsAsync<IOException>(()=>undo!.Restore(store,job));Assert.True(File.Exists(undo!.Backup));}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}}
}
