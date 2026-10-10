using Omni.Core;
using Xunit;
namespace Omni.Tests;

public sealed class TagLibraryTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"omni-library-"+Guid.NewGuid());
    public TagLibraryTests()=>Directory.CreateDirectory(root);
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
    [Fact] public void SourcesSurviveNewStoreWithoutBecomingDownloadHistory()
    {
        var folder=Path.Combine(root,"歌曲");Directory.CreateDirectory(folder);
        var file=Path.Combine(folder,"日本語.mp3");File.WriteAllBytes(file,[1,2,3]);
        var store=new Store(Path.Combine(root,"data"));
        store.RememberTagLibrarySources([TagLibrary.Source(folder),TagLibrary.Source(folder+Path.DirectorySeparatorChar),TagLibrary.Source(file),TagLibrary.Source(file.ToUpperInvariant())]);
        var reopened=new Store(store.DataDirectory);
        Assert.Equal(2,reopened.TagLibrarySources().Count);
        Assert.Empty(reopened.Load());Assert.Equal(new Preferences().Language,reopened.Preferences().Language);
        var read=TagLibrary.Read(reopened.TagLibrarySources());
        Assert.Single(read.Songs);Assert.Equal("日本語",read.Songs[0].Title);Assert.Empty(read.Unavailable);
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(file));
    }
    [Fact] public void SavedFolderReflectsNewAndMovedFilesAndRetainsUnavailableSources()
    {
        var folder=Path.Combine(root,"songs");Directory.CreateDirectory(folder);
        var store=new Store(Path.Combine(root,"data"));store.RememberTagLibrarySources([TagLibrary.Source(folder)]);
        Assert.Empty(TagLibrary.Read(store.TagLibrarySources()).Songs);
        var first=Path.Combine(folder,"first.mp3");File.WriteAllBytes(first,[1]);
        Assert.Single(TagLibrary.Read(store.TagLibrarySources()).Songs);
        File.Move(first,Path.Combine(folder,"renamed.mp3"));
        var nested=Path.Combine(folder,"nested");Directory.CreateDirectory(nested);File.WriteAllBytes(Path.Combine(nested,"second.MP3"),[2]);
        var read=TagLibrary.Read(store.TagLibrarySources());Assert.Equal(2,read.Songs.Length);Assert.DoesNotContain(read.Songs,j=>j.FilePath==first);
        Directory.Move(folder,folder+"-away");Assert.Single(TagLibrary.Read(store.TagLibrarySources()).Unavailable);Assert.Single(new Store(store.DataDirectory).TagLibrarySources());
        Directory.Move(folder+"-away",folder);Assert.Equal(2,TagLibrary.Read(store.TagLibrarySources()).Songs.Length);
    }
    [Fact] public void InvalidFileDoesNotHideOtherSongsAndStandaloneRenameIsRemembered()
    {
        var good=Path.Combine(root,"good.mp3");File.WriteAllBytes(good,[1,2,3]);
        var bad=Path.Combine(root,"bad.mp3");File.WriteAllBytes(bad,[(byte)'I',(byte)'D',(byte)'3',2,0,0,0,0,0,0]);
        var store=new Store(Path.Combine(root,"data"));store.RememberTagLibrarySources([TagLibrary.Source(good),TagLibrary.Source(bad)]);
        var read=TagLibrary.Read(store.TagLibrarySources());Assert.Single(read.Songs);Assert.Equal(bad,Assert.Single(read.Unavailable));
        var next=Path.Combine(root,"新名.mp3");File.Move(good,next);store.MoveTagLibraryFile(good,next);
        Assert.Contains(new Store(store.DataDirectory).TagLibrarySources(),s=>s.Path==next);Assert.Single(TagLibrary.Read(store.TagLibrarySources()).Songs);
    }
    [Fact] public void ForgettingFolderPersistsWithoutDeletingFilesOrHistoryOrOtherSources()
    {
        var desktop=Path.Combine(root,"Desktop","song");var downloads=Path.Combine(root,"Downloads","song");
        Directory.CreateDirectory(desktop);Directory.CreateDirectory(downloads);
        var keep=Path.Combine(desktop,"same.mp3");var remove=Path.Combine(downloads,"same.mp3");
        File.WriteAllBytes(keep,[1,2,3]);File.WriteAllBytes(remove,[1,2,3]);
        var store=new Store(Path.Combine(root,"data"));store.RememberTagLibrarySources([TagLibrary.Source(desktop),TagLibrary.Source(downloads)]);
        var job=new DownloadJob{FilePath=remove,Title="History remains",State=JobState.Completed};store.Save(job);
        var history=Json.Encode(store.Load().Single());
        store.ForgetTagLibrarySource(new(downloads.ToUpperInvariant()+Path.DirectorySeparatorChar,true));
        store.ForgetTagLibrarySource(new(downloads,true));
        var reopened=new Store(store.DataDirectory);
        Assert.Equal(desktop,Assert.Single(reopened.TagLibrarySources()).Path);
        Assert.Equal(history,Json.Encode(reopened.Load().Single()));
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(keep));Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(remove));
        Assert.Equal(keep,Assert.Single(TagLibrary.Read(reopened.TagLibrarySources()).Songs).FilePath);
    }
}
