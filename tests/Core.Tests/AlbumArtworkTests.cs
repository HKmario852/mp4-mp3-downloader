using Omni.Core;
using Xunit;
using System.Buffers.Binary;
namespace Omni.Tests;
public class AlbumArtworkTests {
 public static byte[] Png(int w,int h){var b=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(b,0);BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16,4),w);BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(20,4),h);return b;}
 [Theory][InlineData(233,217,true)][InlineData(475,500,true)][InlineData(500,500,true)][InlineData(1280,720,false)][InlineData(500,400,false)][InlineData(0,0,false)][InlineData(32,32,false)]public void RatioPolicy(int w,int h,bool expected)=>Assert.Equal(expected,AlbumArtwork.Accept(Png(w,h)));
 [Fact]public void TruncatedInputRejected(){Assert.False(AlbumArtwork.Accept([255,216,255]));Assert.False(AlbumArtwork.Accept([]));}
 [Fact]public void CandidateRankingRejectsWideVideo(){using var d=System.Text.Json.JsonDocument.Parse("""{"thumbnails":[{"url":"https://example.org/wide","width":1920,"height":1080},{"url":"https://example.org/album","width":475,"height":500}]}""");Assert.Equal(new[]{"https://example.org/album"},AlbumArtwork.Candidates(d.RootElement));}
}
