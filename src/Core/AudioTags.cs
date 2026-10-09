namespace Omni.Core;

// Canonical field IDs keep the existing review/delta API stable across containers.
// Fields contains ONLY values to write; absent keys and null Covers are untouched.
public sealed record AudioMetadata(Dictionary<string,string> Fields, List<Cover>? Covers = null);
public interface IAudioTagService
{
    Task<AudioMetadata> ReadTagsAsync(string filePath);
    Task WriteTagsAsync(string filePath, AudioMetadata metadata);
    Task EmbedCoverArtAsync(string filePath, byte[] imageBytes, string mimeType);
}
public sealed class TagLibAudioTagService : IAudioTagService
{
    static readonly SemaphoreSlim serial = new(1);
    public Task<AudioMetadata> ReadTagsAsync(string filePath) => Task.Run(() => {
        var doc=AudioTagDocument.Read(filePath);
        return new AudioMetadata(TagReview.Values(doc),doc.GetCovers());
    });
    public async Task WriteTagsAsync(string filePath, AudioMetadata metadata)
    {
        await serial.WaitAsync();
        var temp=filePath+".tags-"+Guid.NewGuid();
        try {
            var hash=await TagReview.Hash(filePath);var doc=AudioTagDocument.Read(filePath);
            foreach(var pair in metadata.Fields)doc.SetText(TagReview.Frame(pair.Key,doc.Version),pair.Value);
            if(metadata.Covers is not null)doc.SetCovers(metadata.Covers);
            await doc.Write(filePath,temp);
            if(await TagReview.Hash(filePath)!=hash)throw new IOException("檔案在編輯期間已有修改 / File changed while editing");
            File.Replace(temp,filePath,null);
        } finally {if(File.Exists(temp))File.Delete(temp);serial.Release();}
    }
    public Task EmbedCoverArtAsync(string filePath,byte[] imageBytes,string mimeType)
    {
        if(imageBytes.Length is 0 or > 32*1024*1024 || mimeType is not ("image/png" or "image/jpeg"))throw new ArgumentException("Use PNG/JPEG artwork under 32 MB");
        return WriteTagsAsync(filePath,new([], [new(imageBytes,mimeType,"Album front",3)]));
    }
}

public sealed class AudioTagDocument
{
    public static Task SetNewMp3Version(string path,int version,CancellationToken ct=default)=>Task.Run(()=>{
        ct.ThrowIfCancellationRequested();
        if(version is not (3 or 4))throw new ArgumentOutOfRangeException(nameof(version));
        using var file=TagLib.File.Create(path);
        var tag=(TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2,true);
        tag.Version=(byte)version;file.Save();
    },ct);
    readonly Id3Document? id3;
    readonly Dictionary<string,string> values = [], changes = [];
    List<Cover> covers = [];bool artworkChanged;
    public byte Version => id3?.Version ?? 4;
    public List<Id3Frame> Frames => id3?.Frames ?? [];
    AudioTagDocument(Id3Document? id3) {this.id3=id3;}
    public static AudioTagDocument Read(string path)
    {
        if(Path.GetExtension(path).Equals(".mp3",StringComparison.OrdinalIgnoreCase))return new(Id3Document.Read(path));
        if(!AudioPipeline.Supported(Path.GetExtension(path)))throw new InvalidDataException("不支援此音訊標籤格式 / Unsupported tag format");
        try {
            using var file=TagLib.File.Create(path);
            var d=new AudioTagDocument(null);var t=file.Tag;
            foreach(var (id,_) in TagReview.Fields)d.values[id]=Get(t,id);
            d.values["COMM"]=t.Comment??"";
            d.covers=t.Pictures.Select(p=>new Cover(p.Data.Data,p.MimeType,p.Description,(byte)p.Type)).ToList();return d;
        } catch(Exception e) when(e is TagLib.CorruptFileException or TagLib.UnsupportedFormatException) {throw new InvalidDataException("音訊標頭或標籤無法讀取 / Invalid audio header or tags",e);}
    }
    public string Text(string id) {
        if(id3 is null)return values.GetValueOrDefault(Canonical(id),"");
        var value=id3.Text(id);
        if(id=="TXXX:MusicBrainz Recording Id"&&value.Length==0){
            value=id3.Text("TXXX:MusicBrainz Track Id");
            if(value.Length==0){var owner=System.Text.Encoding.ASCII.GetBytes("http://musicbrainz.org\0");var frame=id3.Frames.FirstOrDefault(f=>f.Id=="UFID"&&f.Data.AsSpan().StartsWith(owner));if(frame is not null)value=System.Text.Encoding.UTF8.GetString(frame.Data.AsSpan(owner.Length));}
        }
        return value;
    }
    static string Canonical(string id)=>id switch {"TDRC"=>"TYER","TORY"=>"TDOR","TXXX:MusicBrainz Track Id"=>"TXXX:MusicBrainz Recording Id",_=>id};
    public string Comment()=>Text("COMM");
    public List<Cover> GetCovers()=>id3?.GetCovers()??covers;
    public void SetText(string id,string value){if(id3 is not null){id3.SetText(id,value);if(id=="TXXX:MusicBrainz Recording Id"){id3.SetText("TXXX:MusicBrainz Track Id",value);var owner=System.Text.Encoding.ASCII.GetBytes("http://musicbrainz.org\0");var index=id3.Frames.FindIndex(f=>f.Id=="UFID"&&f.Data.AsSpan().StartsWith(owner));var frame=new Id3Frame("UFID",[0,0],[..owner,..System.Text.Encoding.UTF8.GetBytes(value)]);if(index<0)id3.Frames.Add(frame);else id3.Frames[index]=frame;}}else {id=Canonical(id);values[id]=value;changes[id]=value;}}
    public void SetRaw(string id,byte[] bytes){if(id3 is null)throw new ArgumentException("Raw ID3 frames apply only to MP3");id3.SetRaw(id,bytes);}
    public void SetCovers(IEnumerable<Cover> items){if(id3 is not null)id3.SetCovers(items);else {covers=items.ToList();artworkChanged=true;}}
    public async Task Write(string source,string destination,CancellationToken ct=default)
    {
        if(id3 is not null){await id3.Write(source,destination,ct);return;}
        // TagLib uses the extension to pick a container; staging names must retain it.
        var stage=destination+Path.GetExtension(source);
        try {
            File.Copy(source,stage,false);ct.ThrowIfCancellationRequested();
            using(var file=TagLib.File.Create(stage)){
                foreach(var pair in changes)Set(file.Tag,pair.Key,pair.Value);
                if(artworkChanged)file.Tag.Pictures=covers.Select(c=>(TagLib.IPicture)new TagLib.Picture(new TagLib.ByteVector(c.Bytes)){MimeType=c.Mime,Description=c.Description,Type=(TagLib.PictureType)c.Type}).ToArray();
                file.Save();
            }
            var verified=Read(stage);
            foreach(var pair in changes)if(verified.Text(pair.Key)!=pair.Value)throw new IOException("標籤驗證失敗 / Tag verification failed: "+pair.Key);
            if(artworkChanged&&!verified.GetCovers().Select(c=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(c.Bytes))).SequenceEqual(covers.Select(c=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(c.Bytes)))))throw new IOException("封面驗證失敗 / Artwork verification failed");
            ct.ThrowIfCancellationRequested();File.Move(stage,destination);
        } finally {if(File.Exists(stage))File.Delete(stage);}
    }
    static string Join(string[]? values)=>string.Join("; ",values??[]);
    static string Num(uint a,uint b)=>a==0?"":b==0?a.ToString():$"{a}/{b}";
    static (uint,uint) Numbers(string s){if(s.Length==0)return(0,0);var parts=s.Split('/');if(parts.Length>2||!uint.TryParse(parts[0],out var a)||parts.Length==2&&!uint.TryParse(parts[1],out _))throw new ArgumentException("曲目／光碟格式必須為 n 或 n/total");return(a,parts.Length==2?uint.Parse(parts[1]):0);}
    static TagLib.Ogg.XiphComment? Xiph(TagLib.Tag t)=>t as TagLib.Ogg.XiphComment ?? (t is TagLib.Ogg.GroupedComment g?g.Comments.FirstOrDefault():t is TagLib.CombinedTag c?c.Tags.Select(Xiph).FirstOrDefault(x=>x is not null):null);
    static TagLib.Mpeg4.AppleTag? Apple(TagLib.Tag t)=>t as TagLib.Mpeg4.AppleTag ?? (t is TagLib.CombinedTag c?c.Tags.Select(Apple).FirstOrDefault(x=>x is not null):null);
    static string Custom(TagLib.Tag t,string name)=>Xiph(t)?.GetFirstField(name)??Apple(t)?.GetDashBox("com.apple.iTunes",name)??"";
    static void Custom(TagLib.Tag t,string name,string value){if(Xiph(t) is {} x)x.SetField(name,value.Length==0?[]:[value]);else if(Apple(t) is {} a)a.SetDashBox("com.apple.iTunes",name,value);else throw new InvalidDataException("Missing container tag");}
    static string Get(TagLib.Tag t,string id)=>id switch {
        "TIT2"=>t.Title??"","TPE1"=>Join(t.Performers),"TALB"=>t.Album??"","TPE2"=>Join(t.AlbumArtists),
        "TRCK"=>Num(t.Track,t.TrackCount),"TPOS"=>Num(t.Disc,t.DiscCount),"TYER"=>Xiph(t)?.GetFirstField("DATE")??Apple(t)?.GetText(new TagLib.ByteVector(new byte[]{169,100,97,121})).FirstOrDefault()??(t.Year==0?"":t.Year.ToString()),
        "TDOR"=>Custom(t,"ORIGINALDATE"),"TCON"=>Join(t.Genres),"TCOM"=>Join(t.Composers),"TPUB"=>Custom(t,"LABEL"),"TSRC"=>Custom(t,"ISRC"),
        "TXXX:MusicBrainz Recording Id"=>t.MusicBrainzTrackId??"","TXXX:MusicBrainz Album Id"=>t.MusicBrainzReleaseId??"",
        "TSOT"=>t.TitleSort??"","TSOP"=>Join(t.PerformersSort),_=>Custom(t,id)
    };
    static void Set(TagLib.Tag t,string id,string v){string[] list=v.Length==0?[]:[v];switch(id){
        case "TIT2":t.Title=v;break;case "TPE1":t.Performers=list;break;case "TALB":t.Album=v;break;case "TPE2":t.AlbumArtists=list;break;
        case "TRCK":var track=Numbers(v);t.Track=track.Item1;t.TrackCount=track.Item2;break;case "TPOS":var disc=Numbers(v);t.Disc=disc.Item1;t.DiscCount=disc.Item2;break;
        case "TYER":if(Xiph(t) is {} x)x.SetField("DATE",list);else Apple(t)!.SetText(new TagLib.ByteVector(new byte[]{169,100,97,121}),v);break;
        case "TDOR":Custom(t,"ORIGINALDATE",v);break;case "TCON":t.Genres=list;break;case "TCOM":t.Composers=list;break;case "TPUB":Custom(t,"LABEL",v);break;case "TSRC":Custom(t,"ISRC",v);break;
        case "TXXX:MusicBrainz Recording Id":t.MusicBrainzTrackId=v;break;case "TXXX:MusicBrainz Album Id":t.MusicBrainzReleaseId=v;break;
        case "TSOT":t.TitleSort=v;break;case "TSOP":t.PerformersSort=list;break;case "COMM":t.Comment=v;break;default:Custom(t,id,v);break;
    }}
}
