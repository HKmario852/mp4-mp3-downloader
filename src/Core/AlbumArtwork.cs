using System.Buffers.Binary;
using System.Text.Json;
namespace Omni.Core;
public static class AlbumArtwork
{
    public static bool NearSquare(int w,int h)=>w>=100&&h>=100&&(double)Math.Max(w,h)/Math.Min(w,h)<=1.15;
    public static (int Width,int Height) Dimensions(byte[] b){
        try{
            int Be(int i)=>BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(i,4));
            if(b.Length>=24&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return(Be(16),Be(20));
            if(b.Length>4&&b[0]==255&&b[1]==216){int i=2;while(i+4<=b.Length){if(b[i++]!=255)continue;while(i<b.Length&&b[i]==255)i++;if(i>=b.Length)break;int marker=b[i++];if(marker is 0xd9 or 0xda)break;if(marker is 0x01 or >=0xd0 and <=0xd7)continue;if(i+2>b.Length)break;int length=(b[i]<<8)|b[i+1];if(length<2||i+length>b.Length)break;if(marker is >=0xc0 and <=0xcf && marker is not (0xc4 or 0xc8 or 0xcc) && length>=7)return((b[i+5]<<8)|b[i+6],(b[i+3]<<8)|b[i+4]);i+=length;}}
            if(b.Length>=30&&System.Text.Encoding.ASCII.GetString(b,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(b,8,4)=="WEBP"){
                var kind=System.Text.Encoding.ASCII.GetString(b,12,4);
                if(kind=="VP8X")return(1+b[24]+(b[25]<<8)+(b[26]<<16),1+b[27]+(b[28]<<8)+(b[29]<<16));
                if(kind=="VP8 "&&b[23]==157&&b[24]==1&&b[25]==42)return((b[26]|b[27]<<8)&16383,(b[28]|b[29]<<8)&16383);
                if(kind=="VP8L"&&b[20]==47)return(1+((b[21]|b[22]<<8)&16383),1+((b[22]>>6|b[23]<<2|b[24]<<10)&16383));
            }
        }catch(ArgumentOutOfRangeException){}return(0,0);
    }
    public static bool Accept(byte[] b){var(w,h)=Dimensions(b);return NearSquare(w,h);}
    public static string[] Candidates(JsonElement root){
        if(!root.TryGetProperty("thumbnails",out var thumbs)||thumbs.ValueKind!=JsonValueKind.Array)return [];
        return thumbs.EnumerateArray().Where(t=>t.ValueKind==JsonValueKind.Object).Where(t=>t.TryGetProperty("url",out var u)&&u.ValueKind==JsonValueKind.String)
            .Where(t=>!t.TryGetProperty("width",out var w)||!w.TryGetInt32(out var wi)||!t.TryGetProperty("height",out var h)||!h.TryGetInt32(out var hi)||NearSquare(wi,hi))
            .OrderByDescending(t=>t.TryGetProperty("width",out var w)&&w.TryGetInt32(out var n)?n:0).Select(t=>t.GetProperty("url").GetString()!).Where(u=>u.StartsWith("https://",StringComparison.OrdinalIgnoreCase)).Distinct().Take(8).ToArray();
    }
    public static async Task<Cover?> Source(MediaInfo info,CancellationToken ct){using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(25));foreach(var url in (info.ArtworkCandidates??[]).Concat(info.Thumbnail is null?[]:new[]{info.Thumbnail}).Distinct())try{var c=await MusicMetadata.Fetch(url,"Source album artwork",3,deadline.Token);if(c is not null&&Accept(c.Bytes))return c;}catch(Exception e)when(e is HttpRequestException or IOException or OperationCanceledException && !ct.IsCancellationRequested){}return null;}
}
