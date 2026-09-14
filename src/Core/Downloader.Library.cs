using System.Security.Cryptography;
namespace Omni.Core;
public sealed partial class Downloader
{
    public string LogDirectory=>Path.Combine(store.DataDirectory,"logs");
    void WriteFailureLog(DownloadJob j){try{System.IO.Directory.CreateDirectory(LogDirectory);File.WriteAllText(Path.Combine(LogDirectory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".log"),ProcessRunner.Redact((j.Error??"")+"\n"+(j.Stderr??"")));}catch(IOException){}catch(UnauthorizedAccessException){}}
    readonly SemaphoreSlim publishGate=new(1);
    readonly HashSet<string> networkPaused=[];
    int? lastRate;
    bool wasBusy;
    public Func<Preferences,bool>? NetworkPermitted { get; set; }
    public Func<string,CancellationToken,Task<string>>? ResolveDuplicate { get; set; }
    public event Action<DownloadJob>? Completed;
    public void ClearNetworkCache(){if(Jobs.Any(j=>j.State is JobState.Analyzing or JobState.Downloading))throw new IOException("請先暫停下載 / Pause downloads first");var path=Path.GetFullPath(Path.Combine(workDir,"network-cache"));if(System.IO.Directory.Exists(path)){var info=new DirectoryInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Cache cannot be a link");System.IO.Directory.Delete(path,true);}}
    async Task ApplyNetworkPolicy()
    {
        var allowed=NetworkPermitted?.Invoke(Settings)??true; var rate=Settings.EffectiveLimit(DateTime.Now); var changed=lastRate.HasValue&&lastRate!=rate;lastRate=rate;
        if(!allowed||changed) foreach(var j in Jobs.Where(j=>j.State is JobState.Analyzing or JobState.Downloading or JobState.RetryWait)) { await Pause(j.Id);if(allowed)Resume(j.Id);else networkPaused.Add(j.Id); }
        if(allowed)foreach(var id in networkPaused.ToArray()){Resume(id);networkPaused.Remove(id);}
    }
    string JobWork(DownloadJob j)
    {
        var root=Settings.TempDirectory.Length>0?Path.Combine(Settings.TempDirectory,"Omni-work"):workDir;
        var path=Path.GetFullPath(j.WorkPath??Path.Combine(root,j.Id));
        if(Path.GetFileName(path)!=j.Id||!Guid.TryParse(j.Id,out _))throw new IOException("無效暫存路徑 / Invalid work path");
        return path;
    }
    void CleanWork(DownloadJob j)
    {
        var dir=JobWork(j); if(!System.IO.Directory.Exists(dir))return;
        var info=new DirectoryInfo(dir);if((info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("暫存目錄不能是連結 / Work directory cannot be a link");
        if(info.EnumerateFileSystemInfos("*",SearchOption.AllDirectories).Any(f=>(f.Attributes&FileAttributes.ReparsePoint)!=0))throw new IOException("暫存目錄包含連結 / Work directory contains a link");
        System.IO.Directory.Delete(dir,true);
    }
    async Task<bool> PublishFinal(DownloadJob j,string file,string work,Preferences p,CancellationToken ct)
    {
        await publishGate.WaitAsync(ct);
        try {
            var stem=DownloadOptions.FileStem(j,p);var destination=Path.Combine(j.Directory,stem+"."+j.Extension);var action=p.DuplicateAction;
            if(File.Exists(destination)&&action=="ask") action=ResolveDuplicate is null?throw new IOException("檔案已存在，請開啟 App 選擇處理方式 / File exists; open the app"):await ResolveDuplicate(destination,ct);
            if(File.Exists(destination)&&action=="skip"){j.State=JobState.Cancelled;j.Error="已跳過重複檔案 / Duplicate skipped";store.Save(j);return false;}
            if(action is "rename" or "ask")destination=Validation.UniquePath(j.Directory,stem,"."+j.Extension);
            if(action is not ("rename" or "ask" or "overwrite" or "skip"))throw new IOException("無效重複檔案選項");
            // Copy to a unique sibling first. Only a verified copy can replace an existing file.
            var staging=Path.Combine(j.Directory,".omni-"+j.Id+".part");
            try {
                await using(var input=File.OpenRead(file))await using(var output=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){await input.CopyToAsync(output,ct);await output.FlushAsync(ct);}
                await using(var a=File.OpenRead(file))await using(var b=File.OpenRead(staging)){if(!((await SHA256.HashDataAsync(a,ct)).SequenceEqual(await SHA256.HashDataAsync(b,ct))))throw new IOException("檔案複製驗證失敗 / Copy verification failed");}
                ct.ThrowIfCancellationRequested();File.Move(staging,destination,action=="overwrite");
            } finally {if(File.Exists(staging))File.Delete(staging);}
            j.FilePath=destination;j.State=JobState.Completed;j.Progress=100;j.TotalBytes=new FileInfo(destination).Length;j.Bytes=j.TotalBytes.Value;j.CompletedAt=DateTimeOffset.UtcNow;store.Save(j);
            try {
                foreach(var sidecar in System.IO.Directory.EnumerateFiles(work,"media.*").Where(f=>Path.GetExtension(f) is ".srt" or ".vtt" or ".jpg")) {
                    var suffix=Path.GetFileName(sidecar)[5..];var target=Path.Combine(j.Directory,Path.GetFileNameWithoutExtension(destination)+suffix);File.Copy(sidecar,target,true);
                }
                File.Delete(file);
            } catch(IOException e) {j.Error="影片已儲存，附加檔案未全部儲存 / Media saved; some sidecars could not be saved: "+e.Message;store.Save(j);}
            return true;

        } finally {publishGate.Release();}
    }
    public async Task RenameDownloaded(string id,string name)
    {
        if(string.IsNullOrWhiteSpace(name)||Validation.TitleError(name) is not null)throw new ArgumentException(Validation.TitleError(name)??"檔名不可留空 / Filename required");
        await publishGate.WaitAsync();try {var j=store.Load(true).Single(x=>x.Id==id&&x.State==JobState.Completed);var old=j.FilePath??throw new IOException("找不到檔案");
            if(!File.Exists(old))throw new FileNotFoundException("檔案已移動或刪除 / File missing");if(Path.GetFileNameWithoutExtension(old)==name)return;
            var target=Validation.UniquePath(Path.GetDirectoryName(old)!,name,Path.GetExtension(old));File.Move(old,target);
            try {var records=store.Load().Where(x=>x.FilePath==old).ToArray();foreach(var record in records)record.FilePath=target;store.SaveMany(records);ReloadEditedJobs(Jobs.Where(x=>x.FilePath==old).Select(x=>x.Id).ToArray());}catch{File.Move(target,old);throw;}
        }finally{publishGate.Release();}
    }
}
