using System.Windows;
using System.Windows.Controls;
using Omni.Core;
using static Omni.Windows.UiKit;
namespace Omni.Windows;
public sealed class MusicMatchWindow:Window
{
    readonly TaskCompletionSource<MusicCandidate?> answer=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public MusicMatchWindow(Window owner,IEnumerable<MusicCandidate> choices){Owner=owner;Title="選擇歌曲及專輯版本";Width=690;Height=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=owner.Background;Foreground=owner.Foreground;Resources=owner.Resources;
        var panel=new DockPanel{Margin=new Thickness(20)};var title=Text("選擇歌曲及專輯版本（未選擇前不套用）",20);DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);var list=new ListBox{ItemsSource=choices.ToArray(),Margin=new Thickness(0,16,0,16)};list.SetResourceReference(Control.BackgroundProperty,"Surface");list.Foreground=Foreground;
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Button("保留原有資料",Close));actions.Children.Add(Button("使用此版本",()=>{if(list.SelectedItem is MusicCandidate choice){answer.TrySetResult(choice);Close();}},true));DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);panel.Children.Add(list);Content=panel;Closed+=(_,_)=>answer.TrySetResult(null);
    }
    public async Task<MusicCandidate?> Ask(CancellationToken ct=default){ct.ThrowIfCancellationRequested();using var registration=ct.Register(()=>Dispatcher.BeginInvoke(()=>Close()));Show();return await answer.Task;}
}
