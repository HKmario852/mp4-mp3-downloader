using System.Text.Json;
using System.Globalization;
namespace Omni.Core;
public sealed record MusicCandidate(string RecordingId,string? ReleaseId,string Title,string Artist,string Album,string Edition="",double? Confidence=null,string AcoustId="")
{
    public override string ToString()=>$"{Title} · {Artist} · {Album} · {Edition}";
}
public sealed partial class MusicMetadata
{
    public static bool TryRecordingId(string value,out Guid id){
        if(Guid.TryParse(value.Trim(),out id))return true;
        if(Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)&&uri.Host is "musicbrainz.org" or "www.musicbrainz.org"){var parts=uri.AbsolutePath.Trim('/').Split('/');if(parts.Length==2&&parts[0]=="recording")return Guid.TryParse(parts[1],out id);}
        id=default;return false;
    }
    static MusicCandidate[] ToChoices(IEnumerable<JsonElement> rows)=>rows.SelectMany(r=>{
        var releases=Array(r,"releases");var id=Text(r,"id");
        return releases.Length==0?new[]{new MusicCandidate(id,null,Text(r,"title"),string.Join(" & ",Artists(r)),"")}:
            releases.Select(a=>new MusicCandidate(id,Text(a,"id"),Text(r,"title"),string.Join(" & ",Artists(r)),Text(a,"title"),string.Join(" · ",new[]{Text(a,"date"),Text(a,"country"),Text(a,"id")[..Math.Min(8,Text(a,"id").Length)]}.Where(x=>x.Length>0))));
    }).Where(c=>Guid.TryParse(c.RecordingId,out _)).DistinctBy(c=>(c.RecordingId,c.ReleaseId)).Take(25).ToArray();
    async Task<JsonElement> ReadEntity(string path,CancellationToken ct){
        await Rate.WaitAsync(ct);try{var delay=next-DateTimeOffset.UtcNow;if(delay>TimeSpan.Zero)await Task.Delay(delay,ct);next=DateTimeOffset.UtcNow.AddSeconds(1);
            using var response=await client.GetAsync("https://musicbrainz.org/ws/2/"+path,ct);
            if((int)response.StatusCode is 429 or 503)next=DateTimeOffset.UtcNow.AddSeconds(Math.Max(5,response.Headers.RetryAfter?.Delta?.TotalSeconds??5));
            response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return doc.RootElement.Clone();
        }finally{Rate.Release();}
    }
    public async Task<MusicCandidate[]> SearchChoices(string title,string artist,double? duration,CancellationToken ct){
        var q=Prepare(title,artist,duration);using var doc=JsonDocument.Parse(await Search(q,ct));var rows=Array(doc.RootElement,"recordings");
        if(rows.Length==0&&q.Artist.Length>0){using var fallback=JsonDocument.Parse(await Search(q with{Artist=""},ct));rows=Array(fallback.RootElement,"recordings").Select(x=>x.Clone()).ToArray();}
        return ToChoices(rows);
    }
    public async Task<MusicLookup> Recording(string recordingId,string? releaseId,CancellationToken ct){
        if(!Guid.TryParse(recordingId,out var id))throw new ArgumentException("MusicBrainz recording ID 無效");
        var r=await ReadEntity($"recording/{id}?inc=artists+releases+release-groups+isrcs+genres+artist-rels+work-rels&fmt=json",ct);
        var choices=ToChoices([r]);
        if(releaseId is null&&choices.Length>1)return new(MusicLookupState.Ambiguous,Choices:choices);
        var selected=releaseId is null?choices.FirstOrDefault():choices.FirstOrDefault(x=>x.ReleaseId==releaseId);
        if(releaseId is not null&&selected is null)throw new InvalidDataException("專輯不屬於此歌曲");
        var tags=new Dictionary<string,string>{{"TIT2",Text(r,"title")},{"TPE1",string.Join(" & ",Artists(r))}};
        tags["TXXX:MusicBrainz Recording Id"]=id.ToString();
        if(r.TryGetProperty("isrcs",out var codes)&&codes.ValueKind==JsonValueKind.Array)tags["TSRC"]=string.Join("; ",codes.EnumerateArray().Select(x=>x.GetString()));
        tags["TCON"]=string.Join("; ",Array(r,"genres").Select(g=>Text(g,"name")));
        tags["TSOP"]=string.Join("; ",Array(r,"artist-credit").Where(a=>a.TryGetProperty("artist",out _)).Select(a=>Text(a.GetProperty("artist"),"sort-name")));
        var composers=new List<string>();foreach(var relation in Array(r,"relations")){
            if(Text(relation,"type")=="composer"&&relation.TryGetProperty("artist",out var ar))composers.Add(Text(ar,"name"));
            if(relation.TryGetProperty("work",out var work)&&Guid.TryParse(Text(work,"id"),out var workId)){var detail=await ReadEntity($"work/{workId}?inc=artist-rels&fmt=json",ct);composers.AddRange(Array(detail,"relations").Where(a=>Text(a,"type")=="composer"&&a.TryGetProperty("artist",out _)).Select(a=>Text(a.GetProperty("artist"),"name")));}
        }tags["TCOM"]=string.Join("; ",composers.Distinct());
        Cover? cover=null;var album=selected?.Album;
        if(selected?.ReleaseId is string rel&&Guid.TryParse(rel,out var rid)){
            var release=await ReadEntity($"release/{rid}?inc=recordings+artist-credits+labels+release-groups&fmt=json",ct);
            tags["TXXX:MusicBrainz Album Id"]=rid.ToString();
            tags["TPUB"]=string.Join("; ",Array(release,"label-info").Where(l=>l.TryGetProperty("label",out _)&&l.GetProperty("label").ValueKind==JsonValueKind.Object).Select(l=>Text(l.GetProperty("label"),"name")));
            if(release.TryGetProperty("release-group",out var group))tags["TDOR"]=Text(group,"first-release-date");
            album=Text(release,"title");tags["TALB"]=album;tags["TPE2"]=string.Join(" & ",Artists(release));
            var date=Text(release,"date");if(date.Length>=4)tags["TYER"]=date[..4];
            foreach(var medium in Array(release,"media"))foreach(var track in Array(medium,"tracks"))if(track.TryGetProperty("recording",out var tr)&&Text(tr,"id")==id.ToString()){
                tags["TRCK"]=Text(track,"number");if(Number(medium,"position") is double disc)tags["TPOS"]=disc.ToString(CultureInfo.InvariantCulture);
            }
            try{using var response=await client.GetAsync($"https://coverartarchive.org/release/{rid}",ct);if(response.IsSuccessStatusCode){using var images=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));foreach(var image in Array(images.RootElement,"images")){if(!image.TryGetProperty("front",out var front)||front.ValueKind!=JsonValueKind.True)continue;var candidate=await FetchWith(client,Text(image,"image"),"Album front",3,ct);if(candidate is not null&&AlbumArtwork.Accept(candidate.Bytes)){cover=candidate;break;}}}}catch(Exception e)when(e is HttpRequestException or IOException or JsonException){}
        }
        return new(cover is null?MusicLookupState.MatchedNoCover:MusicLookupState.Matched,tags["TIT2"],tags["TPE1"],album,cover,Tags:tags.Where(p=>!string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p=>p.Key,p=>p.Value));
    }
    public static string[] FingerprintMatches(JsonElement root){
        if(Text(root,"status")!="ok")throw new IOException("AcoustID 查詢失敗，請檢查 application API key");
        var results=Array(root,"results");var best=results.Select(r=>Number(r,"score")??0).DefaultIfEmpty().Max();if(best<.95)return [];
        return results.Where(r=>(Number(r,"score")??0)>=Math.Max(.8,best-.10)).SelectMany(r=>Array(r,"recordings")).Select(r=>Text(r,"id")).Where(x=>Guid.TryParse(x,out _)).Distinct().Take(10).ToArray();
    }
    static readonly SemaphoreSlim FingerprintRate=new(1);
    static DateTimeOffset nextFingerprint;
    public async Task<MusicLookup> Scan(string file,string ffmpeg,string key,double? duration,CancellationToken ct,bool review=false,IProgress<int>? progress=null){
        progress?.Report(0);
        if(string.IsNullOrWhiteSpace(key))return new(MusicLookupState.SetupRequired,Detail:"Scan 尚未設定：請在「設定 → 格式」填入 AcoustID application API key。");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(90));ct=deadline.Token;
        if(duration is not >0){var info=await ProcessRunner.Run(Path.Combine(Path.GetDirectoryName(ffmpeg)!,"ffprobe.exe"),["-v","error","-show_entries","format=duration","-of","default=noprint_wrappers=1:nokey=1",file],null,ct);if(double.TryParse(info.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds))duration=seconds;}
        if(duration is not >0)throw new IOException("無法讀取音訊時長");
        progress?.Report(1);
        var fingerprint=(await ProcessRunner.Run(ffmpeg,["-v","error","-i",file,"-t","120","-map","0:a:0","-ac","2","-f","chromaprint","-fp_format","base64","pipe:1"],null,ct)).Trim();
        if(fingerprint.Length<10)throw new IOException("無法產生音訊指紋");
        progress?.Report(2);
        using var form=new FormUrlEncodedContent(new Dictionary<string,string>{{"client",key.Trim()},{"duration",((int)Math.Round(duration.Value)).ToString(CultureInfo.InvariantCulture)},{"fingerprint",fingerprint},{"meta","recordingids"},{"format","json"}});
        JsonElement root;await FingerprintRate.WaitAsync(ct);try{var wait=nextFingerprint-DateTimeOffset.UtcNow;if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);nextFingerprint=DateTimeOffset.UtcNow.AddSeconds(1);using var response=await client.PostAsync("https://api.acoustid.org/v2/lookup",form,ct);response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));root=json.RootElement.Clone();}finally{FingerprintRate.Release();}
        progress?.Report(3);
        if(review){
            if(Text(root,"status")!="ok")throw new IOException("AcoustID 查詢失敗，請檢查設定");
            var hits=Array(root,"results").SelectMany(x=>Array(x,"recordings").Select(r=>new{Id=Text(r,"id"),Score=Number(x,"score")??0,AcoustId=Text(x,"id")})).Where(x=>Guid.TryParse(x.Id,out _)).GroupBy(x=>x.Id).Select(g=>g.MaxBy(x=>x.Score)!).OrderByDescending(x=>x.Score).Take(10);
            var found=new List<MusicCandidate>();foreach(var hit in hits){var recording=await ReadEntity($"recording/{hit.Id}?inc=artists+releases&fmt=json",ct);found.AddRange(ToChoices([recording]).Select(c=>c with{Confidence=hit.Score,AcoustId=hit.AcoustId}));}
            return new(found.Count==0?MusicLookupState.NoMatch:MusicLookupState.Ambiguous,Choices:found.ToArray());
        }
        var ids=FingerprintMatches(root);if(ids.Length==0)return new(MusicLookupState.NoMatch);
        if(ids.Length==1)return await Recording(ids[0],null,ct);
        var choices=new List<MusicCandidate>();foreach(var rid in ids){var r=await ReadEntity($"recording/{rid}?inc=artists+releases&fmt=json",ct);choices.AddRange(ToChoices([r]));}
        return new(MusicLookupState.Ambiguous,Choices:choices.ToArray());
    }
    public async Task<MusicLookup> Recognize(string title,string artist,double? duration,string file,string ffmpeg,string key,CancellationToken ct){
        key=AcoustIdClient.Resolve(key);var text=await Lookup(title,artist,duration,ct);if(text.State is MusicLookupState.Matched or MusicLookupState.MatchedNoCover)return text;
        if(string.IsNullOrWhiteSpace(key))return text with{Detail=text.Message+" · Scan 未設定 application API key"};
        try{var scan=await Scan(file,ffmpeg,key,duration,ct);return scan.State==MusicLookupState.NoMatch?text:scan;}catch(Exception e)when(!ct.IsCancellationRequested&&e is IOException or HttpRequestException or JsonException or OperationCanceledException or DownloadException){return text with{Detail=text.Message+" · Scan 暫時無法使用"};}
    }
}
