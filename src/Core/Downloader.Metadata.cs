namespace Omni.Core;

public sealed partial class Downloader
{
    // Runs after PublishFinal has made the MP3 available in 已下載. It never delays
    // the download queue or turns a completed download into a failed download.
    async Task EnrichCompleted(DownloadJob job, CancellationToken ct)
    {
        try
        {
            if(job.FilePath is not { } path || !File.Exists(path))throw new IOException("檔案已移動或刪除");
            var starting=store.Load().FirstOrDefault(j=>j.Id==job.Id);
            if(starting is null||!starting.PendingMetadata||starting.FilePath!=path)return;
            starting.MetadataStatus="正在背景辨識標籤與封面…";store.Save(starting);ReloadEditedJobs([starting.Id]);
            var originalHash=await TagReview.Hash(path);
            var p=job.Options??Settings;
            var lookup=await music.Recognize(job.Title,job.Artist,job.Duration,path,FfmpegPath,p.AcoustIdClientKey,ct);
            ct.ThrowIfCancellationRequested();
            // A background lookup cannot ask the user to choose among releases.
            // Keep the source tags in that case; the manual review page remains available.
            var text=job.IsUserEdited||!p.KeepMetadata||lookup.State==MusicLookupState.Ambiguous
                ? new Dictionary<string,string>()
                : (lookup.Tags??new Dictionary<string,string>()).ToDictionary(x=>x.Key,x=>x.Value);
            var source=deferredInfo.TryGetValue(job.Id,out var info)?info:new MediaInfo(job.Title,job.Artist,job.Album,job.Thumbnail,job.Duration);
            Cover? cover=null;
            if(p.EmbedThumbnail&&!job.CoverUserEdited){
                cover=lookup.Cover;
                if(cover is null)cover=await FindSourceArtwork(job.Url,source,null,ct);
            }
            ct.ThrowIfCancellationRequested();
            var current=store.Load().FirstOrDefault(j=>j.Id==job.Id);
            if(current is null || current.State!=JobState.Completed || !current.PendingMetadata || current.FilePath!=path)
                throw new IOException("下載紀錄或檔案已變動");
            if(current.IsUserEdited)text.Clear();
            if(current.CoverUserEdited)cover=null;
            var doc=Id3Document.Read(path);
            var supported=TagReview.Fields.Select(x=>x.Id).ToHashSet();
            text=text.Where(x=>x.Value.Length>0 && supported.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value);
            if(doc.Version==4&&text.Remove("TYER",out var year))text["TDRC"]=year;
            if(doc.Version==3&&text.Remove("TDOR",out var originalDate))text["TORY"]=originalDate.Length>=4?originalDate[..4]:originalDate;
            if(text.Count>0||cover is not null)await new TagEditor(store).Apply([current],new(text,Covers:cover is null?null:[cover],RenameFile:false),automaticCover:true,expectedHash:originalHash);
            current.TotalBytes=new FileInfo(path).Length;current.Bytes=current.TotalBytes.Value;
            current.MetadataStatus=lookup.State==MusicLookupState.Ambiguous?"找到多個版本；請到標籤編輯手動選擇":lookup.Message;
            current.MetadataStatus+=cover is null?" · 未找到合適封面":" · 已加入近正方形封面";
            current.MetadataCheckedAt=DateTimeOffset.UtcNow;current.PendingMetadata=false;store.Save(current);ReloadEditedJobs([current.Id]);
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested) { /* Resume on next launch. */ }
        catch(Exception e)
        {
            var current=store.Load().FirstOrDefault(j=>j.Id==job.Id);
            if(current is not null&&current.PendingMetadata&&current.FilePath==job.FilePath){
                current.MetadataStatus=e is IOException&&e.Message.Contains("已被修改")?"檔案其後已修改；背景辨識未覆蓋檔案":"背景辨識未完成："+e.Message;
                current.MetadataCheckedAt=DateTimeOffset.UtcNow;current.PendingMetadata=false;store.Save(current);ReloadEditedJobs([current.Id]);
            }
        }
        finally { deferredInfo.TryRemove(job.Id,out _); }
    }
}
