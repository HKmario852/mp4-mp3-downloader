using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Omni.Core;
using static Omni.Windows.UiKit;
namespace Omni.Windows;

// Owned by the main window, so navigation can detach the controls without closing audio.
public sealed class AudioPreviewSession : IDisposable
{
    readonly MediaPlayer player=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
    bool disposed;double volume=.7,lastVolume=.7;
    CancellationTokenSource? preparation;
    string? previewFile;
    public string? FfmpegPath {get;set;}
    public string? FilePath {get;private set;}
    public bool Playing {get;private set;}
    public bool Preparing=>preparation is not null;
    public bool Ready=>player.Source is not null&&player.NaturalDuration.HasTimeSpan;
    public double Duration=>Ready?player.NaturalDuration.TimeSpan.TotalSeconds:0;
    public double Position=>Ready?player.Position.TotalSeconds:0;
    public double Volume=>volume;
    public event Action? Changed;
    public event Action<string>? Failure;
    public AudioPreviewSession()
    {
        player.MediaOpened+=(_,_)=>{if(!disposed)Changed?.Invoke();};
        player.MediaEnded+=(_,_)=>{Playing=false;timer.Stop();player.Pause();player.Position=TimeSpan.Zero;Changed?.Invoke();};
        player.MediaFailed+=(_,e)=>{Stop();Failure?.Invoke(T("無法試聽：","Cannot preview: ")+e.ErrorException.Message);};
        timer.Tick+=(_,_)=>Changed?.Invoke();
    }
    public bool IsFile(string? path)=>FilePath is not null&&path is not null&&string.Equals(Path.GetFullPath(FilePath),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase);
    public void SetFile(string? path)
    {
        if(disposed||IsFile(path))return;
        Stop();FilePath=path;Changed?.Invoke();
    }
    public void Stop()
    {
        preparation?.Cancel();preparation=null;
        Playing=false;timer.Stop();player.Close();Changed?.Invoke();
        if(previewFile is not null){try{File.Delete(previewFile);}catch(IOException){}previewFile=null;}
    }
    public void StopIfFile(string? path){if(IsFile(path))Stop();}
    public async void TogglePlayback()
    {
        if(disposed||FilePath is null||!File.Exists(FilePath))return;
        if(preparation is not null){Stop();return;}
        try
        {
            if(player.Source is null){
                var path=FilePath;
                // WPF's native player does not reliably decode Ogg/Opus. Decode only
                // a disposable preview; the original downloaded stream stays intact.
                if(Path.GetExtension(path).Equals(".opus",StringComparison.OrdinalIgnoreCase)){
                    if(FfmpegPath is null)throw new IOException("FFmpeg unavailable for Opus preview");
                    var run=new CancellationTokenSource();preparation=run;Changed?.Invoke();
                    var temp=Path.Combine(Path.GetTempPath(),"omni-preview-"+Guid.NewGuid()+".wav");
                    try{
                        await ProcessRunner.Run(FfmpegPath,["-v","error","-nostdin","-i",path,"-map","0:a:0","-c:a","pcm_s16le",temp],null,run.Token);
                        if(disposed||run.IsCancellationRequested)return;
                        previewFile=temp;path=temp;
                    }finally{
                        if(ReferenceEquals(preparation,run))preparation=null;
                        if(previewFile!=temp&&File.Exists(temp))File.Delete(temp);
                        run.Dispose();
                    }
                }
                player.Open(new Uri(path));player.Volume=volume;
            }
            if(Playing){player.Pause();timer.Stop();}else{player.Play();timer.Start();}
            Playing=!Playing;Changed?.Invoke();
        }
        catch(OperationCanceledException){}
        catch(Exception e){Stop();Failure?.Invoke(T("無法試聽：","Cannot preview: ")+e.Message);}
    }
    public void Seek(double seconds){if(disposed||!Ready)return;player.Position=TimeSpan.FromSeconds(Math.Clamp(seconds,0,Duration));Changed?.Invoke();}
    public void SetVolume(double value){if(disposed)return;volume=Math.Clamp(value,0,1);player.Volume=volume;Changed?.Invoke();}
    public void ToggleMute(){if(volume>0){lastVolume=volume;SetVolume(0);}else SetVolume(lastVolume);}
    public void Dispose(){if(disposed)return;disposed=true;Stop();Changed=null;Failure=null;}
}
