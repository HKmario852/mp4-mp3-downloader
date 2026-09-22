using System.Windows;
using System.Windows.Controls;
using static Omni.Windows.UiKit;
namespace Omni.Windows;
public sealed class MusicSearchWindow:Window {
 readonly TaskCompletionSource<(string Title,string Artist)?> answer=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public MusicSearchWindow(Window owner,string title,string artist){Owner=owner;Title="MusicBrainz 查找";Width=560;SizeToContent=SizeToContent.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;Resources=owner.Resources;var body=new StackPanel{Margin=new Thickness(22)};body.Children.Add(Text("查找歌曲標籤",22));body.Children.Add(Text("歌曲名稱，或貼上 MusicBrainz recording 連結 / MBID",13));var name=new TextBox{Text=title,Margin=new Thickness(0,8,0,12)};body.Children.Add(name);body.Children.Add(Text("演出者（選填）",13));var by=new TextBox{Text=artist,Margin=new Thickness(0,8,0,12)};body.Children.Add(by);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(Button("取消",Close));buttons.Children.Add(Button("查找",()=>{if(!string.IsNullOrWhiteSpace(name.Text)){answer.TrySetResult((name.Text.Trim(),by.Text.Trim()));Close();}},true));body.Children.Add(buttons);Content=body;Closed+=(_,_)=>answer.TrySetResult(null);}
 public Task<(string Title,string Artist)?> Ask(){ShowDialog();return answer.Task;}
}
