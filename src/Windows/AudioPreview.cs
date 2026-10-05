using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static Omni.Windows.UiKit;
namespace Omni.Windows;

public sealed class AudioPreview : StackPanel
{
    readonly MediaPlayer player=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
    readonly Slider seek=new(){Name="PreviewPosition",Minimum=0,Maximum=1,IsEnabled=false,IsMoveToPointEnabled=true,SmallChange=1,LargeChange=10,Margin=new Thickness(6,4,0,4)};
    readonly Slider volume=new(){Name="PreviewVolume",Minimum=0,Maximum=1,Value=.7,IsMoveToPointEnabled=true,SmallChange=.01,LargeChange=.1,Margin=new Thickness(6,4,6,4)};
    readonly TextBlock time=Text("0:00 / 0:00",12),level=Text("70%",12);
    readonly Button play,mute;
    string? file;bool playing,updatingSeek;double lastVolume=.7;

    public AudioPreview()
    {
        play=IconButton("play",T("播放試聽","Play preview"),TogglePlayback);play.Name="PreviewPlayback";play.IsEnabled=false;Children.Add(play);
        var progress=new Grid();progress.ColumnDefinitions.Add(new(){Width=GridLength.Auto});progress.ColumnDefinitions.Add(new());
        var progressIcon=Icon("timeline",18);progressIcon.VerticalAlignment=VerticalAlignment.Center;progressIcon.ToolTip=T("播放進度","Playback position");progress.Children.Add(progressIcon);Grid.SetColumn(seek,1);progress.Children.Add(seek);Children.Add(progress);
        time.HorizontalAlignment=HorizontalAlignment.Right;time.Margin=new Thickness(0,0,2,2);Children.Add(time);
        var sound=new Grid();sound.ColumnDefinitions.Add(new(){Width=GridLength.Auto});sound.ColumnDefinitions.Add(new());sound.ColumnDefinitions.Add(new(){Width=new GridLength(38)});
        mute=Button("",ToggleMute);mute.Name="PreviewMute";mute.Content=Icon("volume",19);mute.Padding=new Thickness(3);mute.Margin=new Thickness(0);mute.Background=Brushes.Transparent;mute.BorderThickness=new Thickness(0);mute.Width=28;mute.Height=30;
        sound.Children.Add(mute);Grid.SetColumn(volume,1);sound.Children.Add(volume);Grid.SetColumn(level,2);level.HorizontalAlignment=HorizontalAlignment.Right;sound.Children.Add(level);Children.Add(sound);
        System.Windows.Automation.AutomationProperties.SetName(seek,T("播放進度（秒）","Playback position in seconds"));
        System.Windows.Automation.AutomationProperties.SetName(volume,T("音量","Volume"));
        System.Windows.Automation.AutomationProperties.SetHelpText(seek,T("點擊或拖動以跳到指定時間；方向鍵調整一秒。","Click or drag to seek; arrow keys adjust one second."));
        System.Windows.Automation.AutomationProperties.SetHelpText(volume,T("點擊或拖動以調整音量；方向鍵調整百分之一。","Click or drag to adjust volume; arrow keys adjust one percent."));
        seek.ValueChanged+=(_,_)=>{if(updatingSeek||!seek.IsEnabled||!player.NaturalDuration.HasTimeSpan)return;player.Position=TimeSpan.FromSeconds(seek.Value);UpdateTime();};
        volume.ValueChanged+=(_,_)=>UpdateVolume();
        player.MediaOpened+=(_,_)=>{if(!IsVisible||file is null){Stop();return;}updatingSeek=true;try{seek.Maximum=Math.Max(1,player.NaturalDuration.HasTimeSpan?player.NaturalDuration.TimeSpan.TotalSeconds:1);seek.Value=0;seek.IsEnabled=player.NaturalDuration.HasTimeSpan;}finally{updatingSeek=false;}UpdateTime();};
        player.MediaEnded+=(_,_)=>{playing=false;timer.Stop();player.Pause();player.Position=TimeSpan.Zero;UpdatePosition();UpdatePlayback();};
        player.MediaFailed+=(_,e)=>{Stop();Failure?.Invoke(T("無法試聽：","Cannot preview: ")+e.ErrorException.Message);};
        timer.Tick+=(_,_)=>UpdatePosition();
        Unloaded+=(_,_)=>Stop();IsVisibleChanged+=(_,e)=>{if(e.NewValue is false)Stop();};
        UpdateVolume();
    }
    public event Action<string>? Failure;
    public void SetFile(string? path){Stop();file=path;play.IsEnabled=path is not null&&File.Exists(path);}
    public void Stop()
    {
        playing=false;timer.Stop();player.Close();updatingSeek=true;
        try{seek.IsEnabled=false;seek.Value=0;seek.Maximum=1;}finally{updatingSeek=false;}
        time.Text="0:00 / 0:00";UpdatePlayback();
    }
    void TogglePlayback()
    {
        try
        {
            if(file is null||!IsVisible)return;
            if(player.Source is null){player.Open(new Uri(file));player.Volume=volume.Value;}
            if(playing){player.Pause();timer.Stop();}else{player.Play();timer.Start();}
            playing=!playing;UpdatePlayback();
        }
        catch(Exception e){Stop();Failure?.Invoke(T("無法試聽：","Cannot preview: ")+e.Message);}
    }
    void UpdatePlayback(){var label=playing?T("暫停試聽","Pause preview"):T("播放試聽","Play preview");play.Content=IconLabel(playing?"pause":"play",label);System.Windows.Automation.AutomationProperties.SetName(play,label);}
    void ToggleMute(){if(volume.Value>0){lastVolume=volume.Value;volume.Value=0;}else volume.Value=lastVolume;}
    void UpdateVolume(){player.Volume=volume.Value;var percentage=$"{Math.Round(volume.Value*100)}%";level.Text=percentage;volume.ToolTip=T("音量：","Volume: ")+percentage;mute.Content=Icon(volume.Value==0?"mute":"volume",19);var label=volume.Value==0?T("取消靜音","Unmute"):T("靜音","Mute");mute.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(mute,label);}
    void UpdatePosition(){if(!seek.IsMouseCaptureWithin){updatingSeek=true;try{seek.Value=Math.Clamp(player.Position.TotalSeconds,seek.Minimum,seek.Maximum);}finally{updatingSeek=false;}}UpdateTime();}
    void UpdateTime(){var duration=player.NaturalDuration.HasTimeSpan?player.NaturalDuration.TimeSpan:TimeSpan.Zero;time.Text=$"{TimeSpan.FromSeconds(seek.Value):m\\:ss} / {duration:m\\:ss}";seek.ToolTip=time.Text;}
}
