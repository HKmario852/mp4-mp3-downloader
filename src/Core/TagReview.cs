using System.Security.Cryptography;
namespace Omni.Core;

public static class TagReview
{
    public static readonly (string Id,string Label)[] Fields = [
        ("TIT2","標題"),("TPE1","演出者"),("TALB","專輯"),("TPE2","專輯演出者"),
        ("TRCK","曲目"),("TPOS","光碟"),("TYER","年份"),("TDOR","原始發行日期"),
        ("TCON","類型"),("TCOM","作曲者"),("TPUB","發行商標"),("TSRC","ISRC"),
        ("TXXX:MusicBrainz Recording Id","MusicBrainz 錄音 ID"),("TXXX:MusicBrainz Album Id","MusicBrainz 發行 ID"),
        ("TSOT","標題排序方式"),("TSOP","演出者排序方式")];
    public static string Frame(string id,byte version)=>id=="TYER"&&version==4?"TDRC":id=="TDOR"&&version==3?"TORY":id;
    public static Dictionary<string,string> Values(Id3Document doc)=>Fields.ToDictionary(f=>f.Id,f=>doc.Text(Frame(f.Id,doc.Version)));
    public static async Task<string> Hash(string path){await using var stream=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(stream));}
}

public sealed class TagReviewUndo
{
    public string Backup {get;}
    readonly string path,afterHash; readonly DownloadJob before;
    TagReviewUndo(string backup,string path,string hash,DownloadJob before){Backup=backup;this.path=path;afterHash=hash;this.before=before;}
    public static async Task<TagReviewUndo?> Apply(Store store,DownloadJob job,Dictionary<string,string> selected,Cover? cover,bool keep,string originalHash){
        var path=job.FilePath??throw new IOException("找不到檔案");
        if(await TagReview.Hash(path)!=originalHash)throw new IOException("檔案已被其他操作修改，請返回編輯再掃描。");
        var before=Json.Decode<DownloadJob>(Json.Encode(job));var directory=Path.Combine(store.DataDirectory,"tag-undo");Directory.CreateDirectory(directory);
        var backup=Path.Combine(directory,Guid.NewGuid()+".mp3");if(keep)File.Copy(path,backup);
        try{var doc=Id3Document.Read(path);var mapped=selected.ToDictionary(p=>TagReview.Frame(p.Key,doc.Version),p=>p.Key=="TDOR"&&doc.Version==3&&p.Value.Length>=4?p.Value[..4]:p.Value);
            await new TagEditor(store).Apply([job],new(mapped,Covers:cover is null?null:[cover],RenameFile:false),store.Load().Any(j=>j.Id==job.Id),expectedHash:originalHash);
            if(!keep)return null;var undo=new TagReviewUndo(backup,path,await TagReview.Hash(path),before);
            File.WriteAllText(backup+".json",Json.Encode(new{Path=path,Before=before,AfterHash=undo.afterHash}));return undo;
        }catch{if(File.Exists(backup))File.Delete(backup);throw;}
    }
    public async Task Restore(Store store,DownloadJob job){
        if(await TagReview.Hash(path)!=afterHash)throw new IOException("檔案其後已有修改，為避免覆蓋，不能直接復原。");
        var stage=path+".undo-"+Guid.NewGuid();File.Copy(Backup,stage);try{File.Move(stage,path,true);}finally{if(File.Exists(stage))File.Delete(stage);}
        job.Title=before.Title;job.Artist=before.Artist;job.Album=before.Album;job.IsUserEdited=before.IsUserEdited;job.CoverUserEdited=before.CoverUserEdited;
        if(store.Load().Any(j=>j.Id==job.Id))store.Save(job);File.Delete(Backup);File.Delete(Backup+".json");
    }
}
