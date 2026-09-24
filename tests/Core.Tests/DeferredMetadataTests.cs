using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using Omni.Core;
using Xunit;

namespace Omni.Tests;

public sealed class DeferredMetadataTests
{
    const string RecordingId="cb39b5d8-ebb8-4bad-9f17-9d952108ecb7";
    const string ReleaseId="885b1ba8-65f0-476b-939c-704db7a696de";
    sealed class ReplyHandler(byte[]? cover=null) : HttpMessageHandler
    {
        public TaskCompletionSource? SearchStarted, ContinueSearch;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var uri=request.RequestUri!.AbsoluteUri;
            if(uri.Contains("/recording/?")){
                SearchStarted?.TrySetResult();
                if(ContinueSearch is not null)await ContinueSearch.Task.WaitAsync(ct);
                return Json($$"""{"recordings":[{"id":"{{RecordingId}}","title":"Song","score":100,"length":20000,"artist-credit":[{"name":"Artist"}],"releases":[{"id":"{{ReleaseId}}","title":"Album","status":"Official"}]}]}""");
            }
            if(uri.StartsWith("https://coverartarchive.org/release/",StringComparison.Ordinal) && cover is not null)return Json("""{"images":[{"front":true,"image":"https://example.com/cover.png"}]}""");
            if(uri=="https://example.com/cover.png" && cover is not null)return new(HttpStatusCode.OK){Content=new ByteArrayContent(cover)};
            if(uri.Contains("/recording/"))return Json($$"""{"id":"{{RecordingId}}","title":"Song","artist-credit":[{"name":"Artist"}],"releases":[{"id":"{{ReleaseId}}","title":"Album"}]}""");
            if(uri.Contains("/release/"))return Json("""{"title":"Album","artist-credit":[{"name":"Artist"}],"date":"2026-01-01"}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        static HttpResponseMessage Json(string value)=>new(HttpStatusCode.OK){Content=new StringContent(value)};
    }
    static (Store Store,DownloadJob Job,string File) Fixture(string dir)
    {
        Directory.CreateDirectory(dir);var file=Path.Combine(dir,"Song.mp3");File.WriteAllBytes(file,[1,2,3,4]);
        var store=new Store(Path.Combine(dir,"db"));var job=new DownloadJob{State=JobState.Completed,PendingMetadata=true,Mode=DownloadMode.Mp3,OutputFormat="mp3",Url="https://example.com/audio",FilePath=file,Title="Song",Artist="Artist",Duration=20,Options=new Preferences{Mp3MetadataMode="after",EmbedThumbnail=false}};store.Save(job);return(store,job,file);
    }
    static async Task WaitFinished(Store store,string id){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));while(store.Load().Single(j=>j.Id==id).PendingMetadata){timeout.Token.ThrowIfCancellationRequested();await Task.Delay(50,timeout.Token);}}
    [Fact] public async Task CompletedFileIsAvailableWhileMetadataRunsAndOnlyThenGetsTags()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-deferred-"+Guid.NewGuid());
        try{var (store,job,file)=Fixture(dir);job.Options!.EmbedThumbnail=true;store.Save(job);
            var coverPath=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Windows/Assets/brand.png"));
            var handler=new ReplyHandler(File.ReadAllBytes(coverPath)){SearchStarted=new(),ContinueSearch=new()};using var http=new HttpClient(handler);await using var engine=new Downloader(store,dir,Path.Combine(dir,"work"),new MusicMetadata(http));
            await handler.SearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(JobState.Completed,store.Load().Single().State);Assert.True(File.Exists(file));Assert.Equal([1,2,3,4],File.ReadAllBytes(file));
            handler.ContinueSearch.TrySetResult();await WaitFinished(store,job.Id);var saved=store.Load().Single();Assert.False(saved.PendingMetadata);Assert.Equal(JobState.Completed,saved.State);Assert.Equal("Album",Id3Document.Read(file).Text("TALB"));Assert.Equal("Artist",Id3Document.Read(file).Text("TPE1"));Assert.Single(Id3Document.Read(file).GetCovers());Assert.Empty(Directory.EnumerateFiles(dir,"*.jpg"));Assert.False(saved.IsUserEdited);
        }finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public async Task LaterFileEditPreventsBackgroundOverwrite()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-deferred-"+Guid.NewGuid());
        try{var (store,job,file)=Fixture(dir);var handler=new ReplyHandler{SearchStarted=new(),ContinueSearch=new()};using var http=new HttpClient(handler);await using var engine=new Downloader(store,dir,Path.Combine(dir,"work"),new MusicMetadata(http));
            await handler.SearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));File.AppendAllText(file,"manual change");var changed=File.ReadAllBytes(file);
            handler.ContinueSearch.TrySetResult();await WaitFinished(store,job.Id);Assert.Equal(changed,File.ReadAllBytes(file));Assert.Contains("未覆蓋",store.Load().Single().MetadataStatus);
        }finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public void ExistingPreferencePreservesOffAndMovesAutomaticLookupAfterCompletion()
    {
        var dir=Path.Combine(Path.GetTempPath(),"omni-settings-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try{var store=new Store(dir);using(var db=new SqliteConnection($"Data Source={Path.Combine(dir,"history.db")}")){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="INSERT INTO settings VALUES('preferences',$json)";cmd.Parameters.AddWithValue("$json","{\"musicBrainz\":true}");cmd.ExecuteNonQuery();}
            Assert.Equal("after",store.Preferences().Mp3MetadataMode);var p=store.Preferences();p.Mp3MetadataMode="off";store.SavePreferences(p);Assert.Equal("off",store.Preferences().Mp3MetadataMode);
        }finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
