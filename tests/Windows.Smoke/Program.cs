using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using Omni.Core;
using Omni.Windows;
namespace Omni.Smoke;
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var store = new Store(Path.Combine(output, "smoke-data"));
        if (args.Contains("--ui-regression")) store.Save(new DownloadJob { Id="ui-fixture", RequestId="ui-fixture", State=JobState.Completed, Title="介面測試 · 完整縮圖與深藍選取", Url="https://www.youtube.com/watch?v=ui_fixture&list=PL_preview&index=123&feature=shared", Mode=DownloadMode.Mp3, AudioKbps=320, Duration=240, CompletedAt=DateTimeOffset.UtcNow });
        if (args.Contains("--library-regression") || args.Contains("--update-regression"))
        {
            foreach (var (id, mode) in new[]{("music-a",DownloadMode.Mp3),("music-b",DownloadMode.Mp3),("video",DownloadMode.Mp4)})
            {
                var path=Path.Combine(output,id+"."+mode.ToString().ToLowerInvariant());
                if(mode==DownloadMode.Mp3) ProcessRunner.Run(Path.Combine(Path.GetFullPath(args[1]),"ffmpeg.exe"),["-y","-f","lavfi","-i","sine=frequency=440:duration=2","-codec:a","libmp3lame","-b:a","320k",path],null,CancellationToken.None).GetAwaiter().GetResult();
                else File.WriteAllText(path,"selection fixture");
                store.Save(new DownloadJob{Id=id,RequestId=id,Title=id,Mode=mode,State=JobState.Completed,FilePath=path,Url="https://example.org/"+id,Artist="Mario",Duration=2,AudioKbps=320,CompletedAt=DateTimeOffset.UtcNow});
            }
            var p=store.Preferences();p.Mp3Directory=Path.Combine(output,"music");p.Mp4Directory=Path.Combine(output,"video");store.SavePreferences(p);
        }
        if (args.Contains("--update-regression")) { store.Save(new DownloadJob{Id="paused",Title="暫停測試",State=JobState.Paused}); store.Save(new DownloadJob{Id="failed",Title="失敗測試",State=JobState.Failed}); }
        var engine = new Downloader(store, Path.GetFullPath(args[1]), Path.Combine(output, "smoke-work"));
        var window = new MainWindow(engine, store) { AllowClose = true, ShowInTaskbar = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        app.DispatcherUnhandledException += (_, e) => { File.WriteAllText(Path.Combine(output, "smoke-error.txt"), e.Exception.ToString()); e.Handled = true; app.Shutdown(1); };
        bool exercised = false;
        window.ContentRendered += async (_, _) =>
        {
            if (exercised) return; exercised = true;
            try
            {
                await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(output, "windows-empty.png"));
                if (args.Contains("--music-smoke")) {
                    var result=await new MusicMetadata().Lookup("Radiohead - Creep (Official Music Video)","",238,CancellationToken.None);
                    File.WriteAllText(Path.Combine(output,"musicbrainz-live.json"),Json.Encode(new{result.State,result.Title,result.Artist,result.Album,coverBytes=result.Cover?.Bytes.Length}));
                    if(result.Cover is not null) { var source=Path.Combine(output,"cover-test.mp3");await ProcessRunner.Run(Path.Combine(Path.GetFullPath(args[1]),"ffmpeg.exe"),["-y","-f","lavfi","-i","sine=frequency=440:duration=2",source],null,CancellationToken.None);var doc=Id3Document.Read(source);doc.SetText("TIT2",result.Title!);doc.SetCovers([result.Cover,new(File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"src/Windows/Assets/brand.png")),"image/png","Test secondary",0)]);await doc.Write(source,Path.Combine(output,"dual-cover-test.mp3"));var read=Id3Document.Read(Path.Combine(output,"dual-cover-test.mp3"));if(read.Frames.Count(f=>f.Id=="APIC")!=2)throw new Exception("Dual cover embedding failed"); }
                    await engine.DisposeAsync();window.Close();app.Shutdown(result.State==MusicLookupState.Matched?0:3);return;
                }
                if (args.Contains("--update-regression")) { await CheckUpdate(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return; }
                if (args.Contains("--library-regression")) { await CheckLibrary(window,engine,store,app,output); await engine.DisposeAsync(); window.AllowClose=true; window.Close(); app.Shutdown(0); return; }
                if (args.Contains("--ui-regression")) { await CheckUi(window, app, output); await engine.DisposeAsync(); window.AllowClose=true; window.Close(); app.Shutdown(0); return; }
                if (args.Contains("--render-only")) { Capture(window, Path.Combine(output, "windows-desktop.png")); await engine.DisposeAsync(); window.Close(); app.Shutdown(0); return; }
                var p = engine.Settings; p.DownloadDirectory = Path.Combine(output, "media"); p.MusicBrainz=args.Contains("--music-download-smoke"); engine.SaveSettings(p);
                await engine.Accept(new(Guid.NewGuid().ToString(), "https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4", "mp3"));
                await engine.Accept(new(Guid.NewGuid().ToString(), "https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4", "mp4"));
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                while (engine.Jobs.Any(j => j.State is not (JobState.Completed or JobState.Failed or JobState.Cancelled))) await Task.Delay(500, timeout.Token);
                var results = engine.Jobs.Select(j => new { j.State, j.FilePath, j.Error, j.Stderr, j.MetadataStatus, j.MetadataCheckedAt }).ToArray(); File.WriteAllText(Path.Combine(output, "download-smoke.json"), Json.Encode(results));
                await Task.Delay(700); Capture(window, Path.Combine(output, "windows-download.png"));
                await engine.DisposeAsync(); window.Close(); app.Shutdown(engine.Jobs.All(j => j.State == JobState.Completed) ? 0 : 2);
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "smoke-error.txt"), e.ToString()); await engine.DisposeAsync(); app.Shutdown(1); }
        };
        Environment.ExitCode = app.Run(window);
    }
    static void Capture(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content; visual.UpdateLayout(); var width=visual.ActualWidth+visual.Margin.Left+visual.Margin.Right; var height=visual.ActualHeight+visual.Margin.Top+visual.Margin.Bottom; var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); var background = new DrawingVisual(); using (var dc = background.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height)); bitmap.Render(background); bitmap.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var f = File.Create(path); png.Save(f);
    }
    static async Task CheckUi(MainWindow window, System.Windows.Application app, string output)
    {
        void Check(bool valid,string reason) { if(!valid) throw new Exception(reason); }
        T Find<T>(string name) => (T)window.FindName(name);
        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.OpenHistory(); var grid=Find<DataGrid>("JobGrid"); grid.SelectedIndex=0;
        var url=Find<TextBox>("UrlBox"); var full="https://www.youtube.com/watch?v=ui_fixture&list=PL_preview&index=123&feature=shared"; url.Text=full;
        window.ApplyPreview(new("介面測試 · 完整縮圖與深藍選取","","",null,240,[new("mp4",1080,true,false,60_000_000,2000),new("mp4",720,true,false,30_000_000,1000),new("m4a",null,false,true,4_000_000,128)]));
        var thumb=Find<System.Windows.Controls.Image>("Thumbnail"); var drawing=new DrawingVisual(); using(var dc=drawing.RenderOpen()){dc.DrawRectangle(Brushes.DarkSlateBlue,null,new Rect(0,0,640,360));dc.DrawRectangle(Brushes.MediumPurple,null,new Rect(0,0,80,80));dc.DrawRectangle(Brushes.DeepSkyBlue,null,new Rect(560,0,80,80));dc.DrawRectangle(Brushes.Turquoise,null,new Rect(0,280,80,80));dc.DrawRectangle(Brushes.Orange,null,new Rect(560,280,80,80));} var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);thumb.Source=bitmap;
        await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(url.Text==full && url.GetRectFromCharacterIndex(full.Length-1).Bottom<=url.ActualHeight,"URL was vertically clipped");
        Check(Find<TextBlock>("EstimateLabel").Text.Contains("64.0 MB"),"MP4 estimate not displayed");
        Check(thumb.Stretch==Stretch.Uniform,"Thumbnail is cropped");
        var row=(DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);row.ApplyTemplate();var surface=(Border)row.Template.FindName("RowSurface",row);Check(((SolidColorBrush)surface.Background).Color==Color.FromRgb(39,53,84),"Selected row is not dark blue");
        Capture(window,Path.Combine(output,"windows-selected.png"));
        Click("Mp3Button");Find<ComboBox>("QualityBox").SelectedValue=128;Check(Find<TextBlock>("EstimateLabel").Text.Contains("3.8 MB"),"Changing MP3 quality did not update estimate");
        Click("CollapseNav");window.UpdateLayout();foreach(var name in new[]{"NewNav","QueueNav","HistoryNav","TagsNav","SettingsNav"}) {var button=Find<Button>(name);Check(button.Padding.Left==0 && ((StackPanel)button.Content).Children.Count==1,"Collapsed nav contains label or excessive padding");Check(button.ActualWidth>=28,"Collapsed icon lacks width");}
        Check(Find<StackPanel>("NavFooter").Visibility==Visibility.Collapsed,"Collapsed footer text remains visible");Capture(window,Path.Combine(output,"windows-collapsed.png"));
        window.Height=700;window.UpdateLayout();var detail=Find<ScrollViewer>("DetailScroll");detail.ScrollToBottom();await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check(detail.VerticalOffset>0,"Right panel scrollbar cannot scroll");
        window.WindowState=WindowState.Minimized;await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check(window.IsVisible,"Minimize hid the window from taskbar");
        window.WindowState=WindowState.Normal;window.AllowClose=false;window.Close();Check(!window.IsVisible&&!window.ShowInTaskbar,"Close did not hide to tray");
        File.WriteAllText(Path.Combine(output,"ui-checks.json"),"{\"passed\":true,\"checks\":[\"full URL\",\"quality estimates\",\"uncropped thumbnail\",\"dark selection\",\"collapsed icons\",\"scrollbar\",\"minimize stays visible\",\"close hides to tray\"]}");
    }
    static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is FrameworkElement e)yield return e;foreach(var nested in Descendants(child))yield return nested;}
    }
    static async Task CheckLibrary(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output)
    {
        void Check(bool valid,string message){if(!valid)throw new Exception(message);}
        T Find<T>(string name)=>(T)window.FindName(name);
        void Click(string name)=>Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var recent=Find<DataGrid>("RecentGrid");recent.SelectedItem=recent.Items.Cast<DownloadJob>().First(j=>j.Id=="music-a");
        await Task.Delay(1100);string? opened=null;window.RevealFile=path=>opened=path;Click("FolderButton");
        Check(opened==store.Load().Single(j=>j.Id=="music-a").FilePath,"Recent selection lost during refresh / folder action ignored");
        ShellFiles.Select(opened!); // Exercise the native Shell API against our own generated fixture.
        Click("EditTagsButton");await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var dialog=window.OwnedWindows.OfType<TagWindow>().Single();
        T Field<T>(string name) where T:FrameworkElement=>Descendants(dialog).OfType<T>().Single(x=>x.Name==name);
        Field<TextBox>("TIT2").Text="bad/name";Check(!Field<Button>("SaveTags").IsEnabled,"Invalid Title did not disable save");
        Field<TextBox>("TIT2").Text="測試歌曲";Field<TextBox>("TPE1").Text="Mario edited";
        var closed=new TaskCompletionSource();dialog.Closed+=(_,_)=>closed.SetResult();Field<Button>("SaveTags").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var edited=store.Load().Single(j=>j.Id=="music-a");Check(Path.GetFileName(edited.FilePath)=="測試歌曲.mp3"&&File.Exists(edited.FilePath),"Title edit did not rename file");
        Check(Id3Document.Read(edited.FilePath!).Text("TPE1")=="Mario edited"&&edited.IsUserEdited,"Tags were not saved");
        Check(engine.Jobs.Single(j=>j.Id==edited.Id).FilePath==edited.FilePath,"Engine kept stale path after tag edit");
        Click("TagsNav");Check(Find<TextBlock>("PageTitle").Text.Contains("標籤編輯"),"Tag section not opened");
        var grid=Find<DataGrid>("JobGrid");Check(grid.Items.Count==2&&grid.Items.Cast<DownloadJob>().All(j=>j.Mode==DownloadMode.Mp3),"Tag section includes videos");
        grid.SelectAll();Click("EditTagsButton");await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        dialog=window.OwnedWindows.OfType<TagWindow>().Single();Field<TextBox>("TRCK").Text="1";
        closed=new TaskCompletionSource();dialog.Closed+=(_,_)=>closed.SetResult();Field<Button>("SaveTags").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await closed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Check(store.Load().Where(j=>j.Mode==DownloadMode.Mp3).All(j=>Id3Document.Read(j.FilePath!).Text("TRCK")=="1"),"Batch tracks not fixed at 1");
        Check(store.Load().Single(j=>j.Id==edited.Id).FilePath==edited.FilePath,"Track edit renamed file");
        Click("HistoryNav");var filter=Find<ComboBox>("FormatFilter");filter.SelectedIndex=2;Check(grid.Items.Count==1&&((DownloadJob)grid.Items[0]).Mode==DownloadMode.Mp4,"MP4 filter failed");
        filter.SelectedIndex=1;Find<TextBox>("SearchBox").Text="測試 mario";Check(grid.Items.Count==1,"AND search and MP3 filter failed");
        Find<TextBox>("SearchBox").Text="";filter.SelectedIndex=0;Check(grid.Items.Count==3,"All formats filter failed");
        Click("Mp3Button");Check(Find<TextBox>("DirectoryBox").Text==engine.Settings.Mp3Directory,"MP3 directory missing");
        Click("Mp4Button");Check(Find<TextBox>("DirectoryBox").Text==engine.Settings.Mp4Directory,"MP4 directory missing");
        window.ApplyPreview(new("測試影片","","",null,120,[new("mp4",720,true,true,20_000_000,1000,"720"),new("mp4",1080,true,true,40_000_000,2000,"1080")]));
        var options=Find<ComboBox>("QualityBox").Items.Cast<QualityOption>().ToArray();Check(options.All(q=>q.Value is 0 or 720 or 1080),"Unavailable / 8K qualities still shown");
        Capture(window,Path.Combine(output,"windows-library.png"));
        File.WriteAllText(Path.Combine(output,"library-checks.json"),Json.Encode(new{passed=true,checks=new[]{"recent selection survives timer","folder button and native Shell call","tag section","invalid Title guard","real MP3 ID3 and rename","engine path refresh","fixed batch tracks without rename","MP3/MP4 filters with AND search","separate directories","4K cap and unavailable quality removal"}}));
    }
    static async Task CheckUpdate(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output)
    {
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);}
        T Find<T>(string name)=>(T)window.FindName(name);
        void Click(string name)=>Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var grid=Find<DataGrid>("JobGrid");
        Check(grid.Items.Count==1&&((DownloadJob)grid.Items[0]).State==JobState.Paused,"Completed/failed tasks remained in active queue");
        Check(Find<DataGrid>("RecentGrid").Items.Count==3,"Completed tasks missing from recent downloads");
        Click("FailedButton");Check(grid.Items.Count==1&&((DownloadJob)grid.Items[0]).State==JobState.Failed,"Failed tasks are not accessible");Click("FailedButton");
        Click("HistoryNav");Check(grid.Items.Count==3,"History missing completed tasks");
        void ConfirmAction(string button,bool accept,string expected,Action? during=null)
        {
            Exception? failure=null;
            app.Dispatcher.BeginInvoke(new Action(()=>{
                var dialog=window.OwnedWindows.OfType<ConfirmWindow>().Single();
                try {
                    Check(dialog.WindowStyle==WindowStyle.None&&((SolidColorBrush)dialog.Background).Color!=Colors.White,"Confirmation is white/native");
                    Check(Descendants(dialog).OfType<TextBlock>().Single(t=>t.Name=="ConfirmationMessage").Text.Contains(expected),"Confirmation count/file is wrong");
                    Capture(dialog,Path.Combine(output,button+"-dialog.png"));during?.Invoke();
                    Descendants(dialog).OfType<Button>().Single(b=>b.Name==(accept?"AcceptConfirmation":"CancelConfirmation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }catch(Exception ex){failure=ex;dialog.Close();}
            }),DispatcherPriority.ApplicationIdle);
            Click(button);if(failure is not null)throw failure;
        }
        Find<ComboBox>("FormatFilter").SelectedIndex=1;
        ConfirmAction("ClearButton",false,"2 筆");Check(store.Load(true).Count(j=>j.State==JobState.Completed)==3,"Cancel cleared history");
        ConfirmAction("ClearButton",true,"2 筆",()=>store.Save(new DownloadJob{Id="new-after-prompt",State=JobState.Completed,Mode=DownloadMode.Mp3,Title="new arrival"}));
        Check(store.Load(true).Any(j=>j.Id=="new-after-prompt"),"Clear removed a record added after confirmation opened");
        Check(File.Exists(Path.Combine(output,"music-a.mp3"))&&File.Exists(Path.Combine(output,"music-b.mp3")),"History clear deleted media");
        Find<ComboBox>("FormatFilter").SelectedIndex=2;grid.SelectedIndex=0;await Task.Delay(650);
        window.RecycleFile=path=>File.Move(path,path+".test-trash");ConfirmAction("DeleteFileButton",false,"video.mp4");Check(File.Exists(Path.Combine(output,"video.mp4")),"Cancel deleted a file");
        ConfirmAction("DeleteFileButton",true,"video.mp4");Check(!File.Exists(Path.Combine(output,"video.mp4"))&&File.Exists(Path.Combine(output,"video.mp4.test-trash")),"Selected file deletion failed");
        Check(File.Exists(Path.Combine(output,"music-a.mp3")),"Unselected file was deleted");Check(!store.Load(true).Any(j=>j.Id=="video"),"Deleted file remained in history");
        Check(!Find<Button>("ClearButton").IsEnabled,"Empty history clear button remains enabled");
        var probe=Path.Combine(output,"recycle-probe.txt");File.WriteAllText(probe,"Disposable recycle API test");ShellFiles.Recycle(probe);Check(!File.Exists(probe),"Native recycle API failed");
        File.WriteAllText(Path.Combine(output,"update-checks.json"),Json.Encode(new{passed=true,checks=new[]{"active queue excludes completed and failed","recent and history retain completed","separate failure view","dark confirmation","clear cancellation","clear snapshot count","clear keeps files","delete cancellation","delete only selected file","empty clear disabled","native recycle API"}}));
    }

}
