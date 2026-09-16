using System.Net;
using Omni.Core;
using Xunit;
namespace Omni.Tests;

public sealed class MetadataAndLibrary
{
    sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => Task.FromResult(respond(request)); }
    static HttpResponseMessage Body(string json) => new(HttpStatusCode.OK){Content=new StringContent(json)};
    const string Recording = """
      {"recordings":[{"title":"夜に駆ける","score":100,"length":240000,"artist-credit":[{"name":"YOASOBI"}],"releases":[{"id":"11111111-1111-1111-1111-111111111111","title":"Missing art","status":"Official"}]},
      {"title":"夜に駆ける","score":"100","length":240000,"artist-credit":[{"name":"YOASOBI"}],"releases":[{"id":"22222222-2222-2222-2222-222222222222","title":"THE BOOK","status":"Official"}]}]}
      """;
    [Fact] public async Task MissingArtistStillQueriesAndOtherReleaseCoverIsUsed()
    {
        var requests=new List<string>();
        using var client=new HttpClient(new Handler(r=>{
            var uri=r.RequestUri!.ToString();requests.Add(uri);
            if(uri.Contains("musicbrainz.org"))return Body(Recording);
            if(uri.Contains("11111111"))return new(HttpStatusCode.NotFound);
            if(uri.Contains("22222222"))return Body("""{"images":[{"front":true,"approved":true,"image":"https://example.org/front.png"}]}""");
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(AlbumArtworkTests.Png(500,500))};
        }));
        var result=await new MusicMetadata(client).Lookup("夜に駆ける","",240,CancellationToken.None);
        Assert.Equal(MusicLookupState.Matched,result.State);Assert.Equal("THE BOOK",result.Album);Assert.Equal("YOASOBI",result.Artist);Assert.NotNull(result.Cover);
        Assert.Equal(4,requests.Count);Assert.DoesNotContain("artist%3A",requests[0],StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void QueryStripsVideoSuffixWithoutRomanizing()
    {var q=MusicMetadata.Prepare("YOASOBI - 夜に駆ける (Official Music Video)","",240);Assert.Equal("夜に駆ける",q.Title);Assert.Equal("YOASOBI",q.Artist);}
    [Fact] public async Task SameTitleByDifferentArtistsIsNotAutoApplied()
    {
        using var client=new HttpClient(new Handler(_=>Body(Recording.Replace("\"name\":\"YOASOBI\"}],\"releases\":[{\"id\":\"222", "\"name\":\"Another Artist\"}],\"releases\":[{\"id\":\"222"))));
        Assert.Equal(MusicLookupState.Ambiguous,(await new MusicMetadata(client).Lookup("夜に駆ける","",240,CancellationToken.None)).State);
    }
    [Fact] public async Task MissingArtistAndDurationDoesNotGuessFromTitleAlone()
    {
        using var client=new HttpClient(new Handler(_=>Body(Recording)));
        Assert.Equal(MusicLookupState.NoMatch,(await new MusicMetadata(client).Lookup("夜に駆ける","",null,CancellationToken.None)).State);
    }
    [Fact] public async Task ServiceFailureDoesNotFailDownloadPipeline()
    {
        using var client=new HttpClient(new Handler(_=>throw new HttpRequestException("offline")));
        Assert.Equal(MusicLookupState.Unavailable,(await new MusicMetadata(client).Lookup("Creep","Radiohead",238,CancellationToken.None)).State);
    }
    [Fact] public void QueueSeparatesCompletedFailedAndCancelled()
    {
        foreach(var state in new[]{JobState.Completed,JobState.Failed,JobState.Cancelled})Assert.False(LibraryActions.InQueue(new(){State=state}));
        foreach(var state in new[]{JobState.Downloading,JobState.Queued,JobState.Processing,JobState.Paused,JobState.PendingChoice})Assert.True(LibraryActions.InQueue(new(){State=state}));
    }
    [Fact] public void DeleteOnlySelectedFileAndKeepHistoryWhenRecycleFails()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-delete-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try {
            var store=new Store(dir);var jobs=new[]{"a","b"}.Select(n=>new DownloadJob{Title=n,State=JobState.Completed,FilePath=Path.Combine(dir,n+".mp3")}).ToArray();
            foreach(var j in jobs){File.WriteAllText(j.FilePath!,"fixture");store.Save(j);}
            Assert.Throws<IOException>(()=>LibraryActions.DeleteFile(store,jobs[0].Id,jobs[0].FilePath!,_=>throw new IOException("locked")));
            Assert.Equal(2,store.Load(true).Count);
            LibraryActions.DeleteFile(store,jobs[0].Id,jobs[0].FilePath!,path=>File.Move(path,path+".test-trash"));
            Assert.Equal(jobs[1].Id,Assert.Single(store.Load(true)).Id);Assert.True(File.Exists(jobs[1].FilePath));Assert.True(File.Exists(jobs[0].FilePath+".test-trash"));
            Assert.Throws<IOException>(()=>LibraryActions.DeleteFile(store,jobs[1].Id,Path.Combine(dir,"wrong.mp3"),_=>throw new Exception("must not invoke")));
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
