using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Omni.Core;
using static Omni.Windows.UiKit;
namespace Omni.Windows;

// A real root view; MainWindow detaches the editor while retaining its instance.
public sealed class AcoustIdReviewView:UserControl
{
    readonly DownloadJob song;readonly Downloader engine;readonly Store store;readonly Action<Dictionary<string,string>?,Cover?,TagReviewUndo?,int> leave;
    readonly MusicMetadata service;readonly DockPanel root=new(){Margin=new Thickness(18)};
    readonly TextBlock status=Text("正在分析音訊指紋…",18),count=Text("已選擇 0 個變更"),detail=Text("",12);
    readonly StackPanel candidateList=new(),rows=new(),art=new();readonly Grid body=new();readonly ProgressBar progress=new(){Height=4,IsIndeterminate=true};
    readonly CheckBox keepUndo=new(){Content="套用後保留復原記錄",IsChecked=true};readonly Button apply;
    readonly Dictionary<MusicCandidate,Button> candidateButtons=[]; readonly Image headerArt=new(){Width=62,Height=62,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,14,0)}; readonly Dictionary<string,CheckBox> checks=[];readonly Dictionary<string,MusicLookup> cache=[];
    CancellationTokenSource? scan,resolve;int generation;bool writing,closed,mounted;Dictionary<string,string> original=[];MusicLookup? proposed;MusicCandidate? candidate;Cover? originalCover;bool useCover;string originalHash="";
    public bool Writing=>writing;
    public string TrackId=>song.Id;
    public AcoustIdReviewView(Window owner,DownloadJob song,Downloader engine,Store store,Action<Dictionary<string,string>?,Cover?,TagReviewUndo?,int> leave,MusicMetadata? metadata=null){
        service=metadata??new();this.song=song;this.engine=engine;this.store=store;this.leave=leave;Resources=owner.Resources;Background=owner.Background;FontSize=14;
        var header=new StackPanel();var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};brand.Children.Add(new Image{Source=new BitmapImage(new Uri("pack://application:,,,/App;component/Assets/brand.png")),Width=30,Height=30});brand.Children.Add(Text("  全能影音下載器",17));header.Children.Add(brand);var nav=new DockPanel();nav.Children.Add(IconButton("back","返回標籤編輯",Back));var title=Text("AcoustID 音訊辨識",27);title.HorizontalAlignment=HorizontalAlignment.Center;nav.Children.Add(title);header.Children.Add(nav);
        header.Children.Add(Text("辨識結果",25));header.Children.Add(Text("比較目前標籤與線上資料，選擇要匯入的項目",14));
        var info=new StackPanel{Margin=new Thickness(8)};info.Children.Add(Text(Path.GetFileName(song.FilePath)??song.Title,18));info.Children.Add(Text($"{song.Duration?.ToString("0")??"—"} 秒 · MP3 · {song.AudioKbps} kbps   |   AcoustID + MusicBrainz",13));info.Children.Add(status);info.Children.Add(progress);var summary=new DockPanel();summary.Children.Add(headerArt);summary.Children.Add(info);header.Children.Add(Card(summary,8));DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new StackPanel();footer.Children.Add(Text("套用前請確認資料；現有標籤只會在你選取的欄位被取代。",12));var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(count);actions.Children.Add(keepUndo);actions.Children.Add(Button("取消",Back));actions.Children.Add(Button("返回編輯",Back));apply=Button("套用所選標籤",async()=>await Apply(),true);apply.IsEnabled=false;actions.Children.Add(apply);footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        body.ColumnDefinitions.Add(new(){Width=new GridLength(270)});body.ColumnDefinitions.Add(new());body.ColumnDefinitions.Add(new(){Width=new GridLength(310)});
        var left=new DockPanel();var tools=new StackPanel();tools.Children.Add(Button("再次掃描",async()=>await Start()));tools.Children.Add(detail);DockPanel.SetDock(tools,Dock.Bottom);left.Children.Add(tools);var label=Text("可能符合的錄音",19);DockPanel.SetDock(label,Dock.Top);left.Children.Add(label);left.Children.Add(Scroll(candidateList));body.Children.Add(Card(left,10));
        var center=new DockPanel();var top=new StackPanel();top.Children.Add(Text("標籤比較",19));var commands=new WrapPanel();foreach(var (text,mode) in new[]{("全選有變更",0),("只填空白欄位",1),("取消全選",2)})commands.Children.Add(Button(text,()=>SelectFields(mode)));top.Children.Add(commands);var legend=new StackPanel{Orientation=Orientation.Horizontal};foreach(var (name,hex) in new[]{("● 相同   ","#91A5C2"),("● 將更新   ","#AA82FF"),("● 新增", "#53E7A7")}){var item=Text(name,12);item.Foreground=Brush(hex);legend.Children.Add(item);}top.Children.Add(legend);var headings=Row();Add(headings,Text("選取",12),0);Add(headings,Text("標籤",12),1);Add(headings,Text("目前值",12),2);Add(headings,Text("辨識結果",12),3);top.Children.Add(headings);DockPanel.SetDock(top,Dock.Top);center.Children.Add(top);center.Children.Add(Scroll(rows));var table=Card(center,10);Grid.SetColumn(table,1);body.Children.Add(table);
        var artCard=Card(Scroll(art),10);Grid.SetColumn(artCard,2);body.Children.Add(artCard);root.Children.Add(body);Content=root;
        SizeChanged+=(_,_)=>{body.ColumnDefinitions[0].Width=new GridLength(ActualWidth<1100?200:270);body.ColumnDefinitions[2].Width=new GridLength(ActualWidth<1100?220:310);};
        Loaded+=async(_,_)=>{if(mounted)return;mounted=true;await Start();};Unloaded+=(_,_)=>{scan?.Cancel();resolve?.Cancel();};
    }
    ScrollViewer Scroll(UIElement child){var s=new ScrollViewer{Content=child,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};s.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=FindResource("SlimScrollBar");return s;}
    static Grid Row(){var g=new Grid{Margin=new Thickness(0,2,0,2),MinHeight=28};foreach(var width in new[]{.55,1.1,1.7,1.7})g.ColumnDefinitions.Add(new(){Width=new GridLength(width,GridUnitType.Star)});return g;}
    static void Add(Grid g,UIElement v,int column){Grid.SetColumn(v,column);g.Children.Add(v);}
    public void Back(){if(writing||closed)return;closed=true;generation++;scan?.Cancel();resolve?.Cancel();leave(null,null,null,0);}
    async Task Start(){
        if(writing||closed)return;scan?.Cancel();resolve?.Cancel();scan=new();var token=scan.Token;var run=++generation;checks.Clear();cache.Clear();proposed=null;candidateList.Children.Clear();candidateButtons.Clear();rows.Children.Clear();UpdateCount();progress.Visibility=Visibility.Visible;status.Text="正在分析音訊指紋…";
        var steps=new[]{"讀取音訊","建立指紋","搜尋 AcoustID","載入 MusicBrainz 資料"};var stage=0;
        try{
            if(song.FilePath is null||!File.Exists(song.FilePath)||!Path.GetExtension(song.FilePath).Equals(".mp3",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("請選擇可讀取的 MP3 音訊檔案");
            var doc=await Task.Run(()=>Id3Document.Read(song.FilePath),token);original=TagReview.Values(doc);originalCover=doc.GetCovers().FirstOrDefault();originalHash=await TagReview.Hash(song.FilePath);ShowArtwork();
            var result=await service.Scan(song.FilePath,engine.FfmpegPath,AcoustIdClient.Resolve(engine.Settings.AcoustIdClientKey),song.Duration,token,true,new Progress<int>(n=>{if(run!=generation||closed)return;stage=n;status.Text=$"{n+1}/4 · {steps[n]}…";}));
            if(run!=generation||closed)return;var choices=result.Choices??[];progress.Visibility=Visibility.Collapsed;
            if(choices.Length==0){status.Text="未找到 AcoustID 配對 · 可以再次掃描或返回手動編輯";return;}
            foreach(var c in choices){var content=new StackPanel();content.Children.Add(Text(c.Title,16));content.Children.Add(Text(c.Artist,13));content.Children.Add(Text(c.Album,13));content.Children.Add(Text(c.Edition,12));content.Children.Add(Text($"指紋相似度 {c.Confidence:P0}",13));var b=Button("",async()=>await Choose(c));b.Content=content;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Margin=new Thickness(2,5,2,5);System.Windows.Automation.AutomationProperties.SetName(b,$"{c.Title} {c.Album} {c.Confidence:P0}");candidateButtons[c]=b;candidateList.Children.Add(b);}
            await Choose(choices.OrderByDescending(c=>c.Confidence).First());
        }catch(OperationCanceledException){if(run==generation&&!closed)status.Text="掃描已取消或逾時 · 可再次掃描";}
        catch(Exception e){if(run==generation&&!closed)status.Text=(stage==0?"音訊無法讀取：":stage==1?"指紋建立失敗：":stage==3?"MusicBrainz 資料無法載入：":"網絡／AcoustID 查詢失敗：")+(e is HttpRequestException?"請檢查連線後重試":e.Message);}
        finally{if(run==generation&&!closed)progress.Visibility=Visibility.Collapsed;}
    }
    async Task Choose(MusicCandidate c){
        if(writing||closed)return;resolve?.Cancel();resolve=new();var token=resolve.Token;candidate=c;foreach(var (choice,button) in candidateButtons){button.SetResourceReference(Control.BackgroundProperty,choice==c?"Selected":"Surface");button.BorderBrush=choice==c?Brush("#24B8FF"):Brush("#31485D");button.BorderThickness=new Thickness(choice==c?2:1);}proposed=null;checks.Clear();rows.Children.Clear();useCover=false;UpdateCount();status.Text="正在載入 MusicBrainz 資料…";progress.Visibility=Visibility.Visible;
        try{var key=c.RecordingId+":"+c.ReleaseId;if(!cache.TryGetValue(key,out var result)){result=await service.Recording(c.RecordingId,c.ReleaseId,token);token.ThrowIfCancellationRequested();cache[key]=result;}if(token.IsCancellationRequested||closed)return;proposed=result;status.Text="✓ 辨識完成 · 請核對所選欄位";detail.Text=$"指紋資訊\nAcoustID {c.AcoustId}\nRecording {c.RecordingId}\n{DateTime.Now:g}\n分數非標籤準確率";RenderFields();ShowArtwork();}
        catch(OperationCanceledException){}catch(Exception){if(!token.IsCancellationRequested&&!closed)status.Text="MusicBrainz 資料無法載入 · 請選擇另一版本或重試";}
        finally{if(!token.IsCancellationRequested&&!closed)progress.Visibility=Visibility.Collapsed;}
    }
    void RenderFields(){rows.Children.Clear();checks.Clear();foreach(var (id,label) in TagReview.Fields){var old=original.GetValueOrDefault(id)??"";var value=proposed?.Tags?.GetValueOrDefault(id)??"";var changed=value.Length>0&&old!=value;var state=!changed?"相同":old.Length==0?"新增":"更新";var color=!changed?"#91A5C2":old.Length==0?"#53E7A7":"#AA82FF";var grid=Row();var check=new CheckBox{IsChecked=changed&&(old.Length==0||candidate?.Confidence>=.95),IsEnabled=changed,Content=state,FontSize=10,Margin=new Thickness(0),Foreground=Brush(color)};checks[id]=check;check.Click+=(_,_)=>UpdateCount();System.Windows.Automation.AutomationProperties.SetName(check,label+" · "+state);Add(grid,check,0);Add(grid,Text(label,12),1);Add(grid,Text(old.Length==0?"（空白）":old,12),2);var next=Text(value.Length==0?"（來源未提供，保留原值）":value,12);next.Foreground=Brush(color);Add(grid,next,3);var line=new Border{Child=grid,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,1,0,1)};line.SetResourceReference(Border.BorderBrushProperty,"Line");rows.Children.Add(line);}UpdateCount();}
    void SelectFields(int mode){foreach(var (id,c) in checks)c.IsChecked=c.IsEnabled&&mode!=2&&(mode==0||string.IsNullOrEmpty(original.GetValueOrDefault(id)));UpdateCount();}
    void ShowArtwork(){
        art.Children.Clear();art.Children.Add(Text("專輯封面",20));var pair=new Grid();pair.ColumnDefinitions.Add(new());pair.ColumnDefinitions.Add(new());int index=0;
        foreach(var (label,cover) in new[]{("目前封面",originalCover),("建議封面",proposed?.Cover)}){var column=new StackPanel{Margin=new Thickness(4,10,4,10)};column.Children.Add(Text(label,14));var image=new Image{Height=140,Stretch=Stretch.Uniform,Margin=new Thickness(0,8,0,8)};column.Children.Add(image);if(cover is null)column.Children.Add(Text("沒有可用封面",12));else try{using var stream=new MemoryStream(cover.Bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();image.Source=bitmap;if(index==0)headerArt.Source=bitmap;column.Children.Add(Text($"{bitmap.PixelWidth} × {bitmap.PixelHeight}\n{cover.Mime}",11));}catch{column.Children.Add(Text("圖片無法預覽",12));}Grid.SetColumn(column,index++);pair.Children.Add(column);}art.Children.Add(pair);
        var keep=new RadioButton{Content="保留目前封面",IsChecked=!useCover,GroupName="cover",Margin=new Thickness(0,10,0,8)};var replace=new RadioButton{Content="使用建議封面",IsChecked=useCover,IsEnabled=proposed?.Cover is not null,GroupName="cover"};keep.SetResourceReference(Control.ForegroundProperty,"Text");replace.SetResourceReference(Control.ForegroundProperty,"Text");keep.Checked+=(_,_)=>{useCover=false;UpdateCount();};replace.Checked+=(_,_)=>{useCover=true;UpdateCount();};art.Children.Add(keep);art.Children.Add(replace);
    }
    void UpdateCount(){var n=checks.Count(p=>p.Value.IsChecked==true)+(useCover?1:0);count.Text=$"已選擇 {n} 個變更  ";apply.IsEnabled=!writing&&proposed is not null&&n>0;}
    async Task Apply(){if(writing||proposed is null)return;var values=checks.Where(p=>p.Value.IsChecked==true).ToDictionary(p=>p.Key,p=>proposed.Tags![p.Key]);var cover=useCover?proposed.Cover:null;if(values.Count==0&&cover is null)return;writing=true;root.IsEnabled=false;try{var keep=keepUndo.IsChecked==true;var undo=await Task.Run(()=>TagReviewUndo.Apply(store,song,values,cover,keep,originalHash));engine.ReloadEditedJobs([song.Id]);closed=true;scan?.Cancel();resolve?.Cancel();leave(values,cover,undo,values.Count+(cover is null?0:1));}catch(Exception e){status.Text="標籤寫入失敗："+e.Message;}finally{writing=false;root.IsEnabled=true;UpdateCount();}}
}
