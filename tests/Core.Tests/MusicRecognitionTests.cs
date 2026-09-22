using System.Net;
using System.Text.Json;
using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class MusicRecognitionTests
{
 const string Id="cb39b5d8-ebb8-4bad-9f17-9d952108ecb7",Release="11111111-1111-1111-1111-111111111111";
 const string Recording="""{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7","title":"Song","artist-credit":[{"name":"Artist"}],"releases":[{"id":"11111111-1111-1111-1111-111111111111","title":"Album"}]}""";
 sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> f):HttpMessageHandler
 {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)=>Task.FromResult(f(r));}
 static HttpResponseMessage Body(string s)=>new(HttpStatusCode.OK){Content=new StringContent(s)};
 [Fact]public async Task MissingKeyDoesNotReadAudioOrSendNetwork(){
  using var http=new HttpClient(new Handler(_=>throw new Exception("Network must not run")));
  var result=await new MusicMetadata(http).Scan("missing.mp3","missing.exe","",null,CancellationToken.None);
  Assert.Equal(MusicLookupState.SetupRequired,result.State);
 }
 [Fact]public void WeakFingerprintsAreNotAcceptedAndStrongIdsAreDeduplicated(){
  using var doc=JsonDocument.Parse("""{"status":"ok","results":[{"score":0.70,"recordings":[{"id":"22222222-2222-2222-2222-222222222222"}]},{"score":0.98,"recordings":[{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"},{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"},{"id":"invalid"}]}]}""");
  Assert.Equal(new[]{Id},MusicMetadata.FingerprintMatches(doc.RootElement));
 }
 [Fact]public void ApiErrorIsNotReportedAsNoMatch(){using var doc=JsonDocument.Parse("""{"status":"error","error":{"code":4,"message":"invalid API key"}}""");Assert.Throws<IOException>(()=>MusicMetadata.FingerprintMatches(doc.RootElement));}
 [Fact]public void WrongDurationRejectsExactTitleArtist(){using var doc=JsonDocument.Parse("""{"recordings":[{"title":"Song","length":500000,"artist-credit":[{"name":"Artist"}]}]}""");Assert.Empty(MusicMetadata.Candidates(doc.RootElement,new("Song","Artist",100)));}
 [Fact]public async Task MultipleAlbumsRequireManualSelection(){
  const string json="""{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7","title":"Song","artist-credit":[{"name":"Artist"}],"releases":[{"id":"11111111-1111-1111-1111-111111111111","title":"Album A"},{"id":"22222222-2222-2222-2222-222222222222","title":"Album B"}]}""";
  using var http=new HttpClient(new Handler(_=>Body(json)));
  var result=await new MusicMetadata(http).Recording(Id,null,CancellationToken.None);
  Assert.Equal(MusicLookupState.Ambiguous,result.State);Assert.Equal(2,result.Choices!.Length);Assert.Null(result.Tags);
 }
 [Fact]public async Task SelectedReleaseSuppliesTrackDiscYearAndAlbumArtistWithoutCover(){
  using var http=new HttpClient(new Handler(r=>{
   var uri=r.RequestUri!.AbsolutePath;if(uri.StartsWith("/ws/2/recording"))return Body(Recording);
   if(uri.StartsWith("/ws/2/release"))return Body("""{"title":"Album","date":"2020-05-02","artist-credit":[{"name":"Album Artist"}],"media":[{"position":2,"tracks":[{"number":"03","recording":{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"}}]}]}""");
   return new(HttpStatusCode.NotFound);
  }));
  var result=await new MusicMetadata(http).Recording(Id,Release,CancellationToken.None);
  Assert.Equal(MusicLookupState.MatchedNoCover,result.State);Assert.Equal("03",result.Tags!["TRCK"]);Assert.Equal("2",result.Tags["TPOS"]);Assert.Equal("2020",result.Tags["TYER"]);Assert.Equal("Album Artist",result.Tags["TPE2"]);
 }
 [Fact]public async Task UnrelatedReleaseCannotBeApplied(){
  using var http=new HttpClient(new Handler(_=>Body(Recording)));
  await Assert.ThrowsAsync<InvalidDataException>(()=>new MusicMetadata(http).Recording(Id,"22222222-2222-2222-2222-222222222222",CancellationToken.None));
 }
}
