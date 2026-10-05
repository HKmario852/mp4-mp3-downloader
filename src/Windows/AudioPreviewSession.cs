using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using static Omni.Windows.UiKit;
namespace Omni.Windows;

// Owned by the main window, so navigation can detach the controls without closing audio.
public sealed class AudioPreviewSession : IDisposable
{
    readonly MediaPlayer player=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
    bool disposed;double volume=.7,lastVolume=.7;
    public string? FilePath {get;private set;}
    public bool Playing {get;private set;}
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
        Playing=false;timer.Stop();player.Close();Changed?.Invoke();
    }
    public void StopIfFile(string? path){if(IsFile(path))Stop();}
    public void TogglePlayback()
    {
        if(disposed||FilePath is null||!File.Exists(FilePath))return;
        try
        {
            if(player.Source is null){player.Open(new Uri(FilePath));player.Volume=volume;}
            if(Playing){player.Pause();timer.Stop();}else{player.Play();timer.Start();}
            Playing=!Playing;Changed?.Invoke();
        }
        catch(Exception e){Stop();Failure?.Invoke(T("無法試聽：","Cannot preview: ")+e.Message);}
    }
    public void Seek(double seconds){if(disposed||!Ready)return;player.Position=TimeSpan.FromSeconds(Math.Clamp(seconds,0,Duration));Changed?.Invoke();}
    public void SetVolume(double value){if(disposed)return;volume=Math.Clamp(value,0,1);player.Volume=volume;Changed?.Invoke();}
    public void ToggleMute(){if(volume>0){lastVolume=volume;SetVolume(0);}else SetVolume(lastVolume);}
    public void Dispose(){if(disposed)return;disposed=true;Stop();Changed=null;Failure=null;}
}
