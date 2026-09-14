using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Omni.Windows;
public static class Localization
{
    static readonly Dictionary<string,string> Labels=new()
    {
        ["分析連結"]="Analyze link",["下載任務"]="Download tasks",["下載失敗"]="Failed downloads",["最近下載"]="Recent downloads",["查看全部 ›"]="View all ›",
        ["⏸ 暫停"]="⏸ Pause",["▶ 繼續"]="▶ Resume",["移除"]="Remove",["編輯所選標籤"]="Edit selected tags",["開啟檔案位置"]="Show in folder",["診斷"]="Diagnostics",
        ["你的媒體，井然有序"]="Your media, organized",["將喜歡的影音收藏到這裡"]="Keep your favorite media here",["貼上連結，選擇格式，即可開始下載"]="Paste a link and choose a format to download",
        ["下載設定"]="Download settings",["準備好下一段精彩"]="Ready for your next download",["分析連結後，預覽影片資訊與可選格式。"]="Analyze a link to preview details and formats.",
        ["下載格式"]="Download format",["畫質 / 音質選擇"]="Video / audio quality",["瀏覽"]="Browse",["↓　開始下載"]="↓ Start download",
        ["MP3 最高雙聲道；320 kbps 不會提升來源本身音質。"]="MP3 supports up to stereo. 320 kbps does not improve the source audio quality.",
        ["標題"]="Title",["格式"]="Format",["畫質 / 音質"]="Quality",["狀態"]="Status",["大小"]="Size",["取消"]="Cancel",["儲存"]="Save"
    };
    static string Label(string s,bool english)=>english?Labels.GetValueOrDefault(s,s):Labels.FirstOrDefault(p=>p.Value==s).Key??s;
    public static void Apply(DependencyObject root,bool english)
    {
        // Only static shell controls; never translate user titles or bound data rows.
        if(root is DataGrid grid){foreach(var col in grid.Columns)if(col.Header is string h)col.Header=Label(h,english);return;}
        if(root is TextBlock t && (t.Name.Length==0||t.Name=="EmptyTitle"||t.Name=="PageTitle") && t.DataContext is not Omni.Core.DownloadJob)t.Text=Label(t.Text,english);
        if(root is Button b&&b.Content is string s)b.Content=Label(s,english);
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Apply(VisualTreeHelper.GetChild(root,i),english);
    }
}
