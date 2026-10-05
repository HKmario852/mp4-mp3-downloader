using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Omni.Windows.UiKit;
namespace Omni.Windows;

public sealed class AudioPreview : StackPanel
{
    public AudioPreviewSession Session {get;}
    readonly Slider seek=new(){Name="PreviewPosition",Minimum=0,Maximum=1,IsEnabled=false,IsMoveToPointEnabled=true,SmallChange=1,LargeChange=10,Margin=new Thickness(6,4,0,4)};
    readonly Slider volume=new(){Name="PreviewVolume",Minimum=0,Maximum=1,Value=.7,IsMoveToPointEnabled=true,SmallChange=.01,LargeChange=.1,Margin=new Thickness(6,4,6,4)};
    readonly TextBlock time=Text("0:00 / 0:00",12),level=Text("70%",12);
    readonly Button play,mute;
    bool updating,subscribed;string? playbackLabel,muteLabel;

    public AudioPreview(AudioPreviewSession session)
    {
        Session=session;
        play=IconButton("play",T("播放試聽","Play preview"),Session.TogglePlayback);play.Name="PreviewPlayback";play.IsEnabled=false;Children.Add(play);
        var progress=new Grid();progress.ColumnDefinitions.Add(new(){Width=GridLength.Auto});progress.ColumnDefinitions.Add(new());
        var progressIcon=Icon("timeline",18);progressIcon.VerticalAlignment=VerticalAlignment.Center;progressIcon.ToolTip=T("播放進度","Playback position");progress.Children.Add(progressIcon);Grid.SetColumn(seek,1);progress.Children.Add(seek);Children.Add(progress);
        time.HorizontalAlignment=HorizontalAlignment.Right;time.Margin=new Thickness(0,0,2,2);Children.Add(time);
        var sound=new Grid();sound.ColumnDefinitions.Add(new(){Width=GridLength.Auto});sound.ColumnDefinitions.Add(new());sound.ColumnDefinitions.Add(new(){Width=new GridLength(38)});
        mute=Button("",Session.ToggleMute);mute.Name="PreviewMute";mute.Content=Icon("volume",19);mute.Padding=new Thickness(3);mute.Margin=new Thickness(0);mute.Background=Brushes.Transparent;mute.BorderThickness=new Thickness(0);mute.Width=28;mute.Height=30;
        sound.Children.Add(mute);Grid.SetColumn(volume,1);sound.Children.Add(volume);Grid.SetColumn(level,2);level.HorizontalAlignment=HorizontalAlignment.Right;sound.Children.Add(level);Children.Add(sound);
        System.Windows.Automation.AutomationProperties.SetName(seek,T("播放進度（秒）","Playback position in seconds"));
        System.Windows.Automation.AutomationProperties.SetName(volume,T("音量","Volume"));
        System.Windows.Automation.AutomationProperties.SetHelpText(seek,T("點擊或拖動以跳到指定時間；方向鍵調整一秒。","Click or drag to seek; arrow keys adjust one second."));
        System.Windows.Automation.AutomationProperties.SetHelpText(volume,T("點擊或拖動以調整音量；方向鍵調整百分之一。","Click or drag to adjust volume; arrow keys adjust one percent."));
        seek.ValueChanged+=(_,_)=>{if(!updating&&seek.IsEnabled)Session.Seek(seek.Value);};
        volume.ValueChanged+=(_,_)=>{if(!updating)Session.SetVolume(volume.Value);};
        Loaded+=(_,_)=>{if(!subscribed){Session.Changed+=Sync;Session.Failure+=ShowFailure;subscribed=true;}Sync();};
        Unloaded+=(_,_)=>{Session.Changed-=Sync;Session.Failure-=ShowFailure;subscribed=false;};
        Sync();
    }
    public event Action<string>? Failure;
    void ShowFailure(string message)=>Failure?.Invoke(message);
    public void SetFile(string? path)=>Session.SetFile(path);
    public void Stop()=>Session.Stop();
    void Sync()
    {
        updating=true;
        try
        {
            seek.Maximum=Math.Max(1,Session.Duration);seek.IsEnabled=Session.Ready;
            if(!seek.IsMouseCaptureWithin)seek.Value=Math.Clamp(Session.Position,0,seek.Maximum);
            volume.Value=Session.Volume;
            var label=Session.Playing?T("暫停試聽","Pause preview"):T("播放試聽","Play preview");
            play.IsEnabled=Session.FilePath is not null&&File.Exists(Session.FilePath);
            if(playbackLabel!=label){playbackLabel=label;play.Content=IconLabel(Session.Playing?"pause":"play",label);System.Windows.Automation.AutomationProperties.SetName(play,label);}
            var percentage=$"{Math.Round(Session.Volume*100)}%";level.Text=percentage;volume.ToolTip=T("音量：","Volume: ")+percentage;
            var nextMuteLabel=Session.Volume==0?T("取消靜音","Unmute"):T("靜音","Mute");if(muteLabel!=nextMuteLabel){muteLabel=nextMuteLabel;mute.Content=Icon(Session.Volume==0?"mute":"volume",19);mute.ToolTip=muteLabel;System.Windows.Automation.AutomationProperties.SetName(mute,muteLabel);}
            time.Text=$"{TimeSpan.FromSeconds(seek.Value):m\\:ss} / {TimeSpan.FromSeconds(Session.Duration):m\\:ss}";seek.ToolTip=time.Text;
        }
        finally{updating=false;}
    }
}
