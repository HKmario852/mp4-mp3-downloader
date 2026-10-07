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
        if(args.Length==0){var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));Directory.SetCurrentDirectory(root);args=[Path.Combine(root,"artifacts","qa-0.2.17-interactive"),Path.Combine(root,"artifacts","OmniDownloader-windows-x64"),"--player-interactive"];}
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var store = new Store(Path.Combine(output, "smoke-data"));
        if (args.Contains("--ui-regression")) store.Save(new DownloadJob { Id="ui-fixture", RequestId="ui-fixture", State=JobState.Completed, Title="介面測試 · 完整縮圖與深藍選取", Url="https://www.youtube.com/watch?v=ui_fixture&list=PL_preview&index=123&feature=shared", Mode=DownloadMode.Mp3, AudioKbps=320, Duration=240, CompletedAt=DateTimeOffset.UtcNow });
        if (args.Contains("--tag-first-frame-regression") || args.Contains("--player-regression") || args.Contains("--player-interactive") || args.Contains("--library-persistence-regression") || args.Contains("--metadata-read-regression") || args.Contains("--folder-import-regression") || args.Contains("--artwork-drop-regression") || args.Contains("--review-regression") || args.Contains("--live-scan") || args.Contains("--v27-regression") || args.Contains("--v24-regression") || args.Contains("--v21-regression") || args.Contains("--library-regression") || args.Contains("--update-regression") || args.Contains("--v2-regression"))
        {
            foreach (var (id, mode) in new[]{("music-a",DownloadMode.Mp3),("music-b",DownloadMode.Mp3),("video",DownloadMode.Mp4)})
            {
                var path=Path.Combine(output,id+"."+mode.ToString().ToLowerInvariant());
                if(mode==DownloadMode.Mp3) ProcessRunner.Run(Path.Combine(Path.GetFullPath(args[1]),"ffmpeg.exe"),["-y","-f","lavfi","-i",args.Contains("--player-interactive")?"sine=frequency=440:duration=180":"sine=frequency=440:duration=20","-af",args.Any(a=>a.StartsWith("--player-"))?"volume=0":"anull","-codec:a","libmp3lame","-b:a","320k",path],null,CancellationToken.None).GetAwaiter().GetResult();
                else File.WriteAllText(path,"selection fixture");
                store.Save(new DownloadJob{Id=id,RequestId=id,Title=id,Mode=mode,State=JobState.Completed,FilePath=path,Url="https://example.org/"+id,Thumbnail=new Uri(Path.GetFullPath(Path.Combine(args[1],"..","..","src","Windows","Assets","brand.png"))).AbsoluteUri,TotalBytes=new FileInfo(path).Length,Artist="Mario",Duration=2,AudioKbps=320,CompletedAt=DateTimeOffset.UtcNow.AddMinutes(args.Contains("--v27-regression")&&id=="music-a"?-10:0)});
            }
            var p=store.Preferences();p.Mp3Directory=Path.Combine(output,"music");p.Mp4Directory=Path.Combine(output,"video");if(args.Contains("--v27-regression"))p.Language="en";store.SavePreferences(p);
        }
        if (args.Contains("--failed-count-regression")) store.Save(new DownloadJob{Id="cc000000000000000000000000000001",Title="Demo task",State=JobState.Paused});
        if (args.Contains("--update-regression")) { store.Save(new DownloadJob{Id="paused",Title="暫停測試",State=JobState.Paused}); store.Save(new DownloadJob{Id="failed",Title="失敗測試",State=JobState.Failed}); }
        var engine = new Downloader(store, Path.GetFullPath(args[1]), Path.Combine(output, "smoke-work"));
        var window = new MainWindow(engine, store) { AllowClose = true, ShowInTaskbar = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        if(args.Contains("--player-interactive")){window.ShowInTaskbar=true;window.Title="全能影音下載器 — 播放器測試";window.Left=50;window.Top=30;window.Width=1440;window.Height=900;window.Closed+=(_,_)=>app.Shutdown();}
        app.DispatcherUnhandledException += (_, e) => { File.WriteAllText(Path.Combine(output, "smoke-error.txt"), e.Exception.ToString()); e.Handled = true; app.Shutdown(1); };
        bool exercised = false;
        window.ContentRendered += async (_, _) =>
        {
            if (exercised) return; exercised = true;
            try
            {
                await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(output, "windows-empty.png"));
                if(args.Contains("--failed-count-regression")){await CheckFailedCount(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--tag-first-frame-regression")){await CheckTagFirstFrame(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--player-regression")||args.Contains("--player-interactive")){await CheckPlayer(window,engine,store,app,output,args.Contains("--player-interactive"));await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--recording-smoke")){
                    var result=await new MusicMetadata().Recording("cb39b5d8-ebb8-4bad-9f17-9d952108ecb7","885b1ba8-65f0-476b-939c-704db7a696de",CancellationToken.None);
                    File.WriteAllText(Path.Combine(output,"recording-live.json"),Json.Encode(new{result.State,result.Title,result.Artist,result.Album,result.Tags,coverBytes=result.Cover?.Bytes.Length,dimensions=result.Cover is null?null:(object)AlbumArtwork.Dimensions(result.Cover.Bytes)}));
                    if(result.Cover is not null)File.WriteAllBytes(Path.Combine(output,"reference-album-cover"+(result.Cover.Mime=="image/png"?".png":".jpg")),result.Cover.Bytes);
                    await engine.DisposeAsync();window.Close();app.Shutdown(result.Title=="ENDROLL -HaThA-"?0:3);return;
                }
                if (args.Contains("--music-smoke")) {
                    var result=await new MusicMetadata().Lookup("Radiohead - Creep (Official Music Video)","",238,CancellationToken.None);
                    File.WriteAllText(Path.Combine(output,"musicbrainz-live.json"),Json.Encode(new{result.State,result.Title,result.Artist,result.Album,coverBytes=result.Cover?.Bytes.Length}));
                    if(result.Cover is not null) { var source=Path.Combine(output,"cover-test.mp3");await ProcessRunner.Run(Path.Combine(Path.GetFullPath(args[1]),"ffmpeg.exe"),["-y","-f","lavfi","-i","sine=frequency=440:duration=20",source],null,CancellationToken.None);var doc=Id3Document.Read(source);doc.SetText("TIT2",result.Title!);doc.SetCovers([result.Cover,new(File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"src/Windows/Assets/brand.png")),"image/png","Test secondary",0)]);await doc.Write(source,Path.Combine(output,"dual-cover-test.mp3"));var read=Id3Document.Read(Path.Combine(output,"dual-cover-test.mp3"));if(read.Frames.Count(f=>f.Id=="APIC")!=2)throw new Exception("Dual cover embedding failed"); }
                    await engine.DisposeAsync();window.Close();app.Shutdown(result.State==MusicLookupState.Matched?0:3);return;
                }
                if(args.Contains("--live-scan")){var result=await new MusicMetadata().Scan(args[3],Path.Combine(Path.GetFullPath(args[1]),"ffmpeg.exe"),AcoustIdClient.Resolve(""),null,CancellationToken.None,true);File.WriteAllText(Path.Combine(output,"live-scan.json"),Json.Encode(new{result.State,result.Choices}));await engine.DisposeAsync();window.Close();app.Shutdown(result.Choices?.Length>0?0:3);return;}
                if(args.Contains("--review-regression")){await CheckReview(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--v27-regression")){await CheckV27(window,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--v26-regression")){await CheckV26(window,engine,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--cache-regression")){await CheckCache(engine,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--metadata-read-regression")){await CheckMetadataRead(window,engine,store,output,args.Length>3?args[3]:null,args.Length>4?args[4]:null);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--library-persistence-regression")||args.Contains("--library-reopen")){await CheckLibraryPersistence(window,engine,store,output,args);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--folder-import-regression")){await CheckFolderImport(window,engine,store,output,args.Length>3?args[3]:null);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--artwork-drop-regression")){await CheckArtworkDrop(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if(args.Contains("--v24-regression")){await CheckV21(window,engine,store,app,output);await CheckV24(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return;}
                if (args.Contains("--v21-regression")) { await CheckV21(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return; }
                if (args.Contains("--v2-regression")) { await CheckV2(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return; }
                if (args.Contains("--update-regression")) { await CheckUpdate(window,engine,store,app,output);await engine.DisposeAsync();window.Close();app.Shutdown(0);return; }
                if (args.Contains("--library-regression")) { await CheckLibrary(window,engine,store,app,output); await engine.DisposeAsync(); window.AllowClose=true; window.Close(); app.Shutdown(0); return; }
                if (args.Contains("--clear-input-regression")) { await CheckClearInputs(window,app,output); await engine.DisposeAsync(); window.AllowClose=true; window.Close(); app.Shutdown(0); return; }
                if (args.Contains("--ui-regression")) { await CheckUi(window, app, output); await engine.DisposeAsync(); window.AllowClose=true; window.Close(); app.Shutdown(0); return; }
                if (args.Contains("--render-only")) { Capture(window, Path.Combine(output, "windows-desktop.png")); await engine.DisposeAsync(); window.Close(); app.Shutdown(0); return; }
                var p = engine.Settings; p.DownloadDirectory = Path.Combine(output, "media"); p.MusicBrainz=args.Contains("--music-download-smoke"); engine.SaveSettings(p);
                await engine.Accept(new(Guid.NewGuid().ToString(), "https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4", "mp3"));
                await engine.Accept(new(Guid.NewGuid().ToString(), "https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4", "mp4"));
                if(args.Contains("--formats"))foreach(var format in new[]{"m4a","flac","wav","mkv"})await engine.Accept(new(Guid.NewGuid().ToString(),"https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4",format=="mkv"?"mp4":"mp3",null,format));
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
    static async Task CheckFailedCount(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        var button=(Button)window.FindName("FailedButton");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        async Task Refresh(){typeof(MainWindow).GetMethod("Refresh",flags)!.Invoke(window,null);await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();}
        void Label(string expected){var rendered=string.Join("",Descendants(button).OfType<TextBlock>().Select(t=>t.Text));Check(rendered==expected,$"Failed button rendered '{rendered}', expected '{expected}' (Content: {button.Content})");}
        await Refresh();Label("失敗任務（0）");
        var job=engine.Jobs.Single(j=>j.Id=="cc000000000000000000000000000001");job.State=JobState.Failed;store.Save(job);
        await Refresh();Label("失敗任務（1）");
        Capture(window,Path.Combine(output,"queue-failed-count.png"));
        ((TextBox)window.FindName("SearchBox")).Text="unrelated query";await Refresh();Label("失敗任務（1）");
        ((TextBox)window.FindName("SearchBox")).Clear();button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Refresh();
        Label("返回下載任務");Check(((DataGrid)window.FindName("JobGrid")).Items.Count==1,"Failed page must show the failed job");
        Capture(window,Path.Combine(output,"failed-count.png"));
        UiKit.Language="en";await Refresh();Label("Back to download tasks");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Refresh();Label("Failed tasks (1)");
        job.State=JobState.Paused;store.Save(job);await Refresh();Label("Failed tasks (0)");
        job.State=JobState.Failed;store.Save(job);await Refresh();Label("Failed tasks (1)");
        await engine.Cancel([job.Id]);await Refresh();Label("Failed tasks (0)");
        UiKit.Language="zh-TW";await Refresh();Label("失敗任務（0）");
        File.WriteAllText(Path.Combine(output,"failed-count-checks.json"),Json.Encode(new{passed=true,checks=new[]{"rendered count 0 to 1","search-independent count","failed page return label","English and Chinese","state transition and removal"}}));
    }
    static async Task CheckTagFirstFrame(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        byte[] Cover(Color color){var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{color.B,color.G,color.R,255},4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();png.Save(stream);return stream.ToArray();}
        var legacy=Path.Combine(output,"legacy-cover.png");File.WriteAllBytes(legacy,Cover(Colors.Red));
        foreach(var song in store.Load().Where(j=>j.Extension=="mp3")){
            await new TagEditor(store).Apply([song],new(new(){["TIT2"]="Current demo song",["TPE1"]="Demo artist"},Covers:[new(Cover(Colors.Blue),"image/png","Front",3)],RenameFile:false));
            song.Title="STALE CACHED TITLE";song.Artist="STALE CACHED ARTIST";song.Thumbnail=new Uri(legacy).AbsoluteUri;store.Save(song);
        }
        var files=store.Load().Where(j=>j.Extension=="mp3").Select(j=>j.FilePath!).ToArray();var hashes=await Task.WhenAll(files.Select(TagReview.Hash));var history=store.Load().Select(Json.Encode).ToArray();
        for(int visit=0;visit<2;visit++){
            ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var view=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;
            var list=(StackPanel)typeof(TagEditorView).GetField("songList",flags)!.GetValue(view)!;
            void Inspect(){Check(!Descendants(list).OfType<TextBlock>().Any(t=>t.Text.Contains("STALE CACHED")),"A stale metadata row appeared before disk hydration");Check(!Descendants(list).OfType<System.Windows.Controls.Image>().Any(i=>i.Source is BitmapImage b&&b.UriSource?.LocalPath==legacy),"A history thumbnail appeared before the embedded artwork");}
            string? transientError=null;EventHandler observer=(_,_)=>{try{Inspect();}catch(Exception e){transientError=e.Message;}};view.LayoutUpdated+=observer;
            try{window.UpdateLayout();Inspect();Capture(window,Path.Combine(output,$"tag-loading-{visit}.png"));for(int i=0;view.Busy&&i<500;i++)await Task.Delay(20);Check(!view.Busy,"Tag library did not finish loading");await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();Inspect();Check(transientError is null,transientError??"");}
            finally{view.LayoutUpdated-=observer;}
            Check(Descendants(list).OfType<TextBlock>().Count(t=>t.Text=="Current demo song")==2,"Every row must display disk metadata on its first visible frame");
            Check(Descendants(list).OfType<System.Windows.Controls.Image>().Count(i=>i.Source is not null)==2,"Every row must display embedded artwork after loading");
            if(visit==0){var fields=(Dictionary<string,TextBox>)typeof(TagEditorView).GetField("fields",flags)!.GetValue(view)!;fields["TIT2"].Text="Demo unsaved draft";await (Task)typeof(TagEditorView).GetMethod("LoadThumbnails",flags)!.Invoke(view,null)!;Check(fields["TIT2"].Text=="Demo unsaved draft"&&view.Dirty,"Artwork hydration must preserve an unsaved draft");await (Task)typeof(TagEditorView).GetMethod("LoadSelection",flags)!.Invoke(view,null)!;}
            Capture(window,Path.Combine(output,$"tag-first-frame-{visit}.png"));
            ((Button)window.FindName("QueueNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        }
        Check(hashes.SequenceEqual(await Task.WhenAll(files.Select(TagReview.Hash))),"Opening the editor must not write MP3 files");Check(history.SequenceEqual(store.Load().Select(Json.Encode)),"Opening the editor must not rewrite history");
        File.WriteAllText(Path.Combine(output,"tag-first-frame-checks.json"),Json.Encode(new{passed=true,checks=new[]{"no stale first frame","embedded artwork only","navigation reentry","draft preservation","no file or history writes"}}));
    }
    static async Task CheckPlayer(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output,bool interactive)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        async Task Idle()=>await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        async Task Wait(Func<bool> condition){for(var i=0;i<200&&!condition();i++)await Task.Delay(50);Check(condition(),"Timed out waiting for the native audio player");}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        void Nav(string name)=>((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        async Task<TagEditorView> Open(){Nav("TagsNav");await Idle();var view=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;await Wait(()=>!view.Busy);await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(view,new object[]{new[]{"music-a"}})!;return view;}
        AudioPreview Preview(TagEditorView view)=>Descendants(view).OfType<AudioPreview>().Single();
        MediaPlayer Player(AudioPreview view)=>(MediaPlayer)typeof(AudioPreviewSession).GetField("player",flags)!.GetValue(view.Session)!;
        Slider Seek(AudioPreview view)=>Descendants(view).OfType<Slider>().Single(s=>s.Name=="PreviewPosition");
        Slider Volume(AudioPreview view)=>Descendants(view).OfType<Slider>().Single(s=>s.Name=="PreviewVolume");
        void Toggle(AudioPreview view)=>Descendants(view).OfType<Button>().Single(b=>b.Name=="PreviewPlayback").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bool Playing(AudioPreview view)=>view.Session.Playing;
        bool Timing(AudioPreview view)=>((DispatcherTimer)typeof(AudioPreviewSession).GetField("timer",flags)!.GetValue(view.Session)!).IsEnabled;
        async Task Start(AudioPreview view){Toggle(view);await Wait(()=>Seek(view).IsEnabled&&Player(view).Position.TotalSeconds>.1);Check(Math.Abs(Player(view).Volume-Volume(view).Value)<.001,"Opening audio must respect the selected volume");}
        var editor=await Open();var preview=Preview(editor);
        var hashes=await Task.WhenAll(store.Load().Where(j=>j.Extension=="mp3").Select(j=>TagReview.Hash(j.FilePath!)));
        if(interactive)
        {
            while(window.IsLoaded){var current=((ContentControl)window.FindName("PageHost")).Content as TagEditorView;var p=current is null?preview:Preview(current);File.AppendAllText(Path.Combine(output,"player-live-state-"+Environment.ProcessId+".jsonl"),Json.Encode(new{playing=Playing(p),timer=Timing(p),source=Player(p).Source?.ToString(),position=Player(p).Position.TotalSeconds,seek=Seek(p).Value,duration=Seek(p).Maximum,volume=Player(p).Volume,visible=p.IsVisible})+Environment.NewLine);await Task.Delay(1000);}return;
        }
        await Start(preview);Check(Math.Abs(Seek(preview).Maximum-20)<.2,"Seek range must use decoded duration instead of a 0..1 placeholder");
        Toggle(preview);Seek(preview).Value=12;await Task.Delay(250);Check(Math.Abs(Player(preview).Position.TotalSeconds-12)<.5,"Seeking while paused must jump to the chosen time");Check(!Playing(preview)&&!Timing(preview),"Seeking must preserve pause state");
        Volume(preview).Value=1;Volume(preview).Value=.5;Check(Math.Abs(Player(preview).Volume-.5)<.001,"Volume must change directly from 100 to 50 percent");
        var mute=Descendants(preview).OfType<Button>().Single(b=>b.Name=="PreviewMute");mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Player(preview).Volume==0,"Mute must silence the preview");mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Player(preview).Volume==.5,"Unmute must restore the prior level");
        Seek(preview).Value=Seek(preview).Maximum-.2;Toggle(preview);await Wait(()=>!Playing(preview));Check(Seek(preview).Value<.1&&!Timing(preview),"End of track must reset the play button, timer and position");
        async Task Continued(AudioPreview view,double before,string page){await Task.Delay(600);Check(Playing(view)&&Timing(view)&&Player(view).Source is not null&&Player(view).Position.TotalSeconds>before+.2,page+" must keep the same audio playing");Check(Math.Abs(Player(view).Volume-.5)<.001,page+" must preserve volume");}
        await Start(preview);var session=preview.Session;var before=Player(preview).Position.TotalSeconds;Nav("QueueNav");await Idle();await Continued(preview,before,"Downloads");
        before=Player(preview).Position.TotalSeconds;editor=await Open();preview=Preview(editor);Check(ReferenceEquals(session,preview.Session),"Recreated editor must use the window's existing session");await Continued(preview,before,"Returning to editor");Check(Math.Abs(Seek(preview).Value-Player(preview).Position.TotalSeconds)<.5&&Volume(preview).Value==.5,"Restored controls must show live position and volume");
        before=Player(preview).Position.TotalSeconds;Nav("HistoryNav");await Idle();await Continued(preview,before,"Downloaded library");editor=await Open();preview=Preview(editor);
        before=Player(preview).Position.TotalSeconds;Nav("NewNav");await Idle();await Continued(preview,before,"New download");editor=await Open();preview=Preview(editor);
        before=Player(preview).Position.TotalSeconds;Nav("SettingsNav");await Idle();await Continued(preview,before,"Settings");var settings=(SettingsView)((ContentControl)window.FindName("PageHost")).Content;settings.Back!();await Idle();Check(ReferenceEquals(editor,((ContentControl)window.FindName("PageHost")).Content)&&Playing(preview),"Returning from Settings must keep playback active");
        before=Player(preview).Position.TotalSeconds;window.MusicServiceFactory=()=>new MusicMetadata(new System.Net.Http.HttpClient(new ReviewHandler(File.ReadAllBytes("src/Windows/Assets/brand.png"))));window.OpenAcoustIdReview(editor,store.Load().Single(j=>j.Id=="music-a"));await Idle();await Continued(preview,before,"Scan review");((AcoustIdReviewView)window.Content).Back();await Idle();Check(Playing(preview),"Returning from Scan must keep playing");
        Toggle(preview);var paused=Player(preview).Position.TotalSeconds;Nav("QueueNav");await Idle();editor=await Open();preview=Preview(editor);await Task.Delay(350);Check(!Playing(preview)&&!Timing(preview)&&Math.Abs(Player(preview).Position.TotalSeconds-paused)<.2,"Navigation must also preserve a paused session");
        await Start(preview);await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{"music-b"}})!;Check(Player(preview).Source is null&&!Playing(preview),"Selecting another song must stop the old preview");
        await Start(preview);Nav("QueueNav");await Idle();Nav("TagsNav");await Idle();editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;await Wait(()=>!editor.Busy);preview=Preview(editor);Check(preview.Session.IsFile(store.Load().Single(j=>j.Id=="music-b").FilePath)&&Playing(preview),"Returning must automatically select the playing song, even when it is not first");
        preview.Stop();
        var volumeTrack=(System.Windows.Controls.Primitives.Track)Volume(preview).Template.FindName("PART_Track",Volume(preview));Volume(preview).Value=.5;await Idle();Check(Math.Abs(volumeTrack.Value-.5)<.001&&volumeTrack.Minimum==0&&volumeTrack.Maximum==1,"The rendered track must follow volume range and value");
        window.UpdateLayout();Capture(window,Path.Combine(output,"player-preview.png"));window.Width=1050;await Idle();Capture(window,Path.Combine(output,"player-narrow.png"));
        Check(hashes.SequenceEqual(await Task.WhenAll(store.Load().Where(j=>j.Extension=="mp3").Select(j=>TagReview.Hash(j.FilePath!)))),"Preview controls must not modify MP3 files");
        await Start(preview);window.AllowClose=true;window.Close();await Idle();Check(Player(preview).Source is null&&!Playing(preview)&&!Timing(preview),"Closing the main window must dispose playback");
        File.WriteAllText(Path.Combine(output,"player-checks.json"),Json.Encode(new{passed=true,checks=new[]{"native MP3 playback","decoded seek range","paused seek","volume 100 to 50","mute and restore","end of track","download navigation continues","recreated editor shares session and live controls","downloaded library continues","new download continues","settings continues and returns","scan continues and returns","paused navigation preserves position","song selection stops old preview","return selects active song","rendered track binding","no MP3 changes","window close disposes playback"}}));
    }
    static async Task CheckLibraryPersistence(MainWindow window,Downloader engine,Store store,string output,string[] args)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        List<DownloadJob> Songs(TagEditorView view)=>(List<DownloadJob>)typeof(TagEditorView).GetField("songs",flags)!.GetValue(view)!;
        async Task<TagEditorView> Open(){((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);var view=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;while(view.Busy)await Task.Delay(20);return view;}
        var folder=Path.Combine(output,"saved-library");
        if(args.Contains("--library-reopen"))
        {
            var reopened=await Open();Check(store.TagLibrarySources().Count==1,"A new process must restore the saved folder");Check(Songs(reopened).Count(j=>j.FilePath!.StartsWith(folder+Path.DirectorySeparatorChar))==3,"A new process must load the folder's songs without another import");
            Check(Songs(reopened).Select(j=>j.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()==Songs(reopened).Count,"Restart must not duplicate rows");
            window.UpdateLayout();Capture(window,Path.Combine(output,"library-reopened.png"));File.WriteAllText(Path.Combine(output,"library-reopened.json"),Json.Encode(new{passed=true,count=Songs(reopened).Count}));return;
        }
        Directory.CreateDirectory(folder);
        var fixture=store.Load().First(j=>j.Extension=="mp3").FilePath!;
        foreach(var name in new[]{"歌一.mp3","歌二.mp3"})File.Copy(fixture,Path.Combine(folder,name),true);
        var editor=await Open();var history=store.Load().Select(Json.Encode).ToArray();
        await editor.Import([folder,folder+Path.DirectorySeparatorChar]);while(editor.Busy)await Task.Delay(20);
        Check(store.TagLibrarySources().Count==1,"Repeated folder input must persist one source");Check(Songs(editor).Count(j=>j.FilePath!.StartsWith(folder+Path.DirectorySeparatorChar))==2,"Initial import must contain both songs");
        ((Button)window.FindName("QueueNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        File.Move(Path.Combine(folder,"歌一.mp3"),Path.Combine(folder,"已改名.mp3"));File.Copy(fixture,Path.Combine(folder,"新歌.mp3"),true);
        editor=await Open();Check(Songs(editor).Count(j=>j.FilePath!.StartsWith(folder+Path.DirectorySeparatorChar))==3,"Returning must discover new/renamed files without a manual import");
        await editor.Import([folder]);while(editor.Busy)await Task.Delay(20);Check(Songs(editor).Count==5,"Restoring and importing again must merge with existing history rows");
        Check(history.SequenceEqual(store.Load().Select(Json.Encode)),"Saved tag-library sources must not change download history");
        var paths=Directory.GetFiles(folder,"*.mp3");var hashes=await Task.WhenAll(paths.Select(TagReview.Hash));
        store.Checkpoint();
        var executable=Environment.ProcessPath!;
        await ProcessRunner.Run(executable,[output,args[1],"--library-reopen"],null,CancellationToken.None);
        Check(File.Exists(Path.Combine(output,"library-reopened.json")),"The new-process restore test must finish");
        Check(hashes.SequenceEqual(await Task.WhenAll(paths.Select(TagReview.Hash))),"Persistence and restore must not change MP3 bytes");
        File.WriteAllText(Path.Combine(output,"library-persistence-checks.json"),Json.Encode(new{passed=true,checks=new[]{"source persistence","page navigation restore","new and renamed songs","deduplication","actual new-process restart","no download history changes","no MP3 writes"}}));
    }
    static async Task CheckMetadataRead(MainWindow window,Downloader engine,Store store,string output,string? taggedFolder,string? otherFolder)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var files=store.Load(true).Where(j=>j.Extension=="mp3").ToArray();
        var png=File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"src/Windows/Assets/brand.png"));
        foreach(var j in files){await new TagEditor(store).Apply([j],new(new(){["TIT2"]="歌曲 "+j.Id,["TPE1"]="正確演出者",["TALB"]="實際專輯"},Covers:[new(png,"image/png","Front",3)],RenameFile:false));j.Title="過期標題";j.Artist="未知的演出者";store.Save(j);}
        var a=files.Single(j=>j.Id=="music-a");var b=files.Single(j=>j.Id=="music-b");
        async Task SetRawCover(DownloadJob j,byte[] bytes){var doc=Id3Document.Read(j.FilePath!);doc.SetRaw("APIC",bytes);var temp=j.FilePath+".fixture";await doc.Write(j.FilePath!,temp);File.Move(temp,j.FilePath!,true);}
        await SetRawCover(a,[0,..System.Text.Encoding.ASCII.GetBytes("image/png"),0,3,0,0,..png]);
        await SetRawCover(b,[0,..System.Text.Encoding.ASCII.GetBytes("image/jpeg"),0,3,0,1,2,3]);
        ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;
        async Task Ready(){while(editor.Busy)await Task.Delay(20);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
        async Task Select(string id){await Ready();await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{id}})!;await Ready();}
        Dictionary<string,TextBox> Fields()=>(Dictionary<string,TextBox>)typeof(TagEditorView).GetField("fields",flags)!.GetValue(editor)!;
        List<DownloadJob> Songs()=>(List<DownloadJob>)typeof(TagEditorView).GetField("songs",flags)!.GetValue(editor)!;
        var image=(System.Windows.Controls.Image)typeof(TagEditorView).GetField("artwork",flags)!.GetValue(editor)!;
        var location=(TextBlock)typeof(TagEditorView).GetField("fileLocation",flags)!.GetValue(editor)!;
        var error=(TextBlock)typeof(TagEditorView).GetField("error",flags)!.GetValue(editor)!;
        await Select(a.Id);Check(Fields()["TPE1"].Text=="正確演出者"&&Fields()["TALB"].Text=="實際專輯","Metadata must come from ID3, not cached history");
        Check(image.Source is not null&&!editor.Dirty,"Padded embedded artwork must render without marking tags dirty");Check(location.Text==a.FilePath,"Selected file path must be visible");
        await Select(b.Id);Check(Fields()["TALB"].Text=="實際專輯"&&image.Source is null&&error.Text.Contains("封面"),"Invalid artwork must not prevent metadata display or retain previous cover");
        Fields()["TIT2"].Text="未儲存嘅修改";await (Task)typeof(TagEditorView).GetMethod("LoadThumbnails",flags)!.Invoke(editor,null)!;
        Check(Fields()["TIT2"].Text=="未儲存嘅修改"&&editor.Dirty,"Refreshing row metadata must preserve editor drafts");
        await (Task)typeof(TagEditorView).GetMethod("LoadSelection",flags)!.Invoke(editor,null)!;
        string? actualArtist=null;int actualCoverWidth=0;
        if(taggedFolder is not null&&otherFolder is not null)
        {
            var paths=new[]{Path.Combine(taggedFolder,"バケモノと呼ばれて.mp3"),Path.Combine(otherFolder,"バケモノと呼ばれて.mp3")}.Select(Path.GetFullPath).ToArray();
            var hashes=await Task.WhenAll(paths.Select(TagReview.Hash));var history=store.Load().Select(Json.Encode).ToArray();
            await editor.Import(paths);await Ready();var tagged=Songs().Single(j=>j.FilePath==paths[0]);var other=Songs().Single(j=>j.FilePath==paths[1]);
            Check(tagged.Id!=other.Id,"Same-title files in different folders must remain independently selectable");
            await Select(other.Id);Check(Fields()["TPE1"].Text=="未知的演出者"&&image.Source is not null,"Downloads copy must display its actual tags and recover padded JPEG");
            window.UpdateLayout();Capture(window,Path.Combine(output,"downloads-copy.png"));
            await Select(tagged.Id);Check(Fields()["TPE1"].Text=="藤川千愛"&&Fields()["TALB"].Text=="HiKiKoMoRi"&&Fields()["TRCK"].Text=="10/11","Desktop copy metadata must appear correctly");
            Check(location.Text==paths[0]&&image.Source is BitmapSource,"Desktop file path and embedded cover must be visible");actualArtist=Fields()["TPE1"].Text;actualCoverWidth=(image.Source as BitmapSource)?.PixelWidth??0;
            Check(hashes.SequenceEqual(await Task.WhenAll(paths.Select(TagReview.Hash))),"User MP3 files must remain byte-identical");Check(history.SequenceEqual(store.Load().Select(Json.Encode)),"Reading/importing metadata must not rewrite history");
        }
        window.UpdateLayout();Capture(window,Path.Combine(output,"metadata-read.png"));
        File.WriteAllText(Path.Combine(output,"metadata-read-checks.json"),Json.Encode(new{passed=true,actualArtist,actualCoverWidth,checks=new[]{"disk tags override stale rows","padded artwork preview","invalid artwork isolation","visible source path","unsaved draft preservation","distinct folder copies","real desktop tags","real downloads tags","no user file or history writes"}}));
    }
    static async Task CheckFolderImport(MainWindow window,Downloader engine,Store store,string output,string? userFolder)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        List<DownloadJob> Songs(TagEditorView view)=>(List<DownloadJob>)typeof(TagEditorView).GetField("songs",flags)!.GetValue(view)!;
        HashSet<string> Selected(TagEditorView view)=>(HashSet<string>)typeof(TagEditorView).GetField("selected",flags)!.GetValue(view)!;
        async Task Ready(TagEditorView view){while(view.Busy)await Task.Delay(20);await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);}
        var folder=Path.Combine(output,"import-folder");Directory.CreateDirectory(folder);
        var jobs=store.Load(true).Where(j=>j.Extension=="mp3").ToArray();
        foreach(var j in jobs){var path=Path.Combine(folder,Path.GetFileName(j.FilePath)!);File.Copy(j.FilePath!,path,true);j.FilePath=path;await new TagEditor(store).Apply([j],new(new(){{"TIT2","同名歌曲 日本語"},{"TPE1","Aimer"}},RenameFile:false));j.Title="過期歌曲資料";j.Artist="未知的演出者";store.Save(j);}
        var original=jobs.First(j=>j.Id=="music-a");
        var alias=Json.Decode<DownloadJob>(Json.Encode(original));alias.Id="duplicate-history";alias.FilePath=Path.Combine(folder,".","music-a.mp3").Replace('\\','/');store.Save(alias);
        var initial=Json.Decode<DownloadJob>(Json.Encode(alias));initial.Id="initial-alias";initial.FilePath=initial.FilePath!.ToUpperInvariant();
        typeof(MainWindow).GetMethod("ShowTagEditor",flags)!.Invoke(window,new object[]{new[]{initial}});
        var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;await Ready(editor);
        Check(Songs(editor).Count==2,"History and initial aliases must produce one row per file");
        Check(Selected(editor).Count==1&&Songs(editor).Any(j=>Selected(editor).Contains(j.Id)),"Initial alias selection must map to the retained row");
        var hashes=await Task.WhenAll(jobs.Select(j=>TagReview.Hash(j.FilePath!)));var history=store.Load().Select(Json.Encode).ToArray();
        await editor.Import([folder,initial.FilePath!,original.FilePath!]);await Ready(editor);
        Check(Songs(editor).Count==2,"Overlapping folder/file inputs must retain distinct same-title files without duplicates");
        Check(Songs(editor).All(j=>j.Title=="同名歌曲 日本語"&&j.Artist=="Aimer"),"Existing cached row metadata must refresh from disk");
        Check(Songs(editor).Any(j=>j.Id==original.Id&&j.Url==original.Url),"Import must preserve the history job identity and provenance");
        Check(Selected(editor).Count==2,"All canonical imported rows must be selected");
        await editor.Import([folder.ToUpperInvariant()]);await Ready(editor);
        await Task.WhenAll(editor.Import([folder]),editor.Import([folder]));await Ready(editor);
        Check(Songs(editor).Count==2&&Selected(editor).Count==2,"Repeated and concurrent imports must remain idempotent");
        Check(history.SequenceEqual(store.Load().Select(Json.Encode)),"Import must not rewrite download history");
        Check(hashes.SequenceEqual(await Task.WhenAll(jobs.Select(j=>TagReview.Hash(j.FilePath!)))),"Import must not change MP3 bytes");
        var moved=Path.Combine(folder,"renamed.mp3");File.Move(jobs.First(j=>j.Id=="music-b").FilePath!,moved);
        await editor.Import([folder]);await Ready(editor);
        Check(Songs(editor).Count==2&&Songs(editor).All(j=>File.Exists(j.FilePath)),"Reimport after a file move must remove the stale editor row");
        ((Dictionary<string,TextBox>)typeof(TagEditorView).GetField("fields",flags)!.GetValue(editor)!)["TALB"].Text="匯入後儲存";
        await (Task)typeof(TagEditorView).GetMethod("Save",flags)!.Invoke(editor,null)!;await Ready(editor);
        Check(Songs(editor).All(j=>Id3Document.Read(j.FilePath!).Text("TALB")=="匯入後儲存"),"Each unique imported file must remain editable");
        int? actualFiles=null;
        if(userFolder is not null)
        {
            var files=Directory.EnumerateFiles(userFolder,"*.mp3",SearchOption.AllDirectories).ToArray();var before=await Task.WhenAll(files.Select(TagReview.Hash));
            var isolated=new Store(Path.Combine(output,"user-folder-read-only-data"));editor=new TagEditorView(window,engine,isolated);((ContentControl)window.FindName("PageHost")).Content=editor;await Ready(editor);
            await editor.Import([userFolder]);await Ready(editor);await editor.Import([userFolder]);await Ready(editor);
            Check(Songs(editor).Count==files.Length,"Actual folder must produce exactly one row per MP3 after repeated import");
            Check(Songs(editor).Select(j=>Path.GetFullPath(j.FilePath!)).Distinct(StringComparer.OrdinalIgnoreCase).Count()==files.Length,"Actual folder paths must be unique");
            Check(before.SequenceEqual(await Task.WhenAll(files.Select(TagReview.Hash))),"Actual user MP3 files must remain byte-identical");actualFiles=files.Length;
            await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{Songs(editor).First().Id}})!;
        }
        window.UpdateLayout();Capture(window,Path.Combine(output,"folder-import.png"));
        File.WriteAllText(Path.Combine(output,"folder-import-checks.json"),Json.Encode(new{passed=true,actualFiles,checks=new[]{"history and initial aliases","folder/file overlap","same title distinct files","tag refresh","stable history identity","case-insensitive selection","repeat and concurrent imports","stale moved rows","no pre-save writes","save unique files","actual folder read-only hashes"}}));
    }
    static async Task CheckV27(MainWindow window,System.Windows.Application app,string output)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var search=(TextBox)window.FindName("SearchBox");var searchHint=(TextBlock)window.FindName("SearchHint");
        var urlHint=(TextBlock)window.FindName("UrlHint");
        Check(searchHint.Text.StartsWith("Search tasks",StringComparison.Ordinal),"Downloading search hint did not switch to English");
        Check(urlHint.Text.StartsWith("Paste a YouTube",StringComparison.Ordinal),"URL hint did not switch to English");
        window.UpdateLayout();
        var hintY=searchHint.TranslatePoint(new Point(0,searchHint.ActualHeight/2),window).Y;
        search.Text="x";window.UpdateLayout();
        var caret=search.GetRectFromCharacterIndex(0);var caretY=search.TranslatePoint(new Point(0,caret.Y+caret.Height/2),window).Y;
        Check(Math.Abs(hintY-caretY)<4,$"Search hint and caret are on different lines ({hintY:F1}, {caretY:F1})");
        search.Text="";Capture(window,Path.Combine(output,"windows-english-search.png"));
        ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;
        for(var i=0;i<100&&editor.Busy;i++)await Task.Delay(30);
        var songs=(StackPanel)typeof(TagEditorView).GetField("songList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!;
        var first=Descendants((Border)songs.Children[0]).OfType<TextBlock>().First().Text;
        Check(first=="music-b",$"Tag editor order should be newest first, found {first}");
        Capture(window,Path.Combine(output,"windows-tags-newest-first.png"));
        File.WriteAllText(Path.Combine(output,"v27-checks.json"),Json.Encode(new{passed=true,first,searchHint=searchHint.Text,urlHint=urlHint.Text,baselineDifference=Math.Abs(hintY-caretY)}));
    }
    static async Task CheckV26(MainWindow window,Downloader engine,string output)
    {
        ((Button)window.FindName("SettingsNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);var host=(ContentControl)window.FindName("PageHost");var settings=(SettingsView)host.Content;
        settings.StartupWriter=_=>{};
        var nav=(StackPanel)typeof(SettingsView).GetField("nav",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(settings)!;
        nav.Children.OfType<Button>().ElementAt(2).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var form=(StackPanel)typeof(SettingsView).GetField("form",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(settings)!;
        var logicalCombos=form.Children.OfType<Border>().Select(c=>c.Child).OfType<Grid>().SelectMany(g=>g.Children.OfType<ComboBox>()).ToArray();
        File.WriteAllText(Path.Combine(output,"settings-debug.txt"),"rows="+form.Children.Count+"; combos="+string.Join(";",logicalCombos.Select(c=>string.Join(",",c.Items.OfType<ComboBoxItem>().Select(i=>i.Tag)))));
        var mode=logicalCombos.Single(c=>c.Items.OfType<ComboBoxItem>().Any(i=>Equals(i.Tag,"after")));
        if(!Equals(((ComboBoxItem)mode.SelectedItem).Tag,"after"))throw new Exception("New installs should default to background metadata");
        mode.BringIntoView();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Capture(window,Path.Combine(output,"settings-metadata.png"));
        mode.SelectedItem=mode.Items.OfType<ComboBoxItem>().Single(i=>Equals(i.Tag,"before"));
        if(!settings.Save()||engine.Settings.Mp3MetadataMode!="before")throw new Exception("Automatic metadata mode did not save");
        File.WriteAllText(Path.Combine(output,"v26-checks.json"),Json.Encode(new{passed=true,settings=engine.Settings.Mp3MetadataMode}));
    }
    static async Task CheckCache(Downloader engine,string output)
    {
        const string url="https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4";
        var settings=engine.Settings;settings.Mp3MetadataMode="off";settings.DownloadDirectory=Path.Combine(output,"downloads");engine.SaveSettings(settings);
        var watch=System.Diagnostics.Stopwatch.StartNew();await engine.Analyze(url);var first=watch.Elapsed.TotalSeconds;
        await engine.Accept(new(Guid.NewGuid().ToString("N"),url,"mp3"));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        while(engine.Jobs[0].AnalysisSeconds is null){timeout.Token.ThrowIfCancellationRequested();await Task.Delay(40,timeout.Token);}
        var job=engine.Jobs[0];if(job.AnalysisSeconds>=1)throw new Exception($"Cached link was re-analyzed: {job.AnalysisSeconds:F2}s");
        if(job.State is not (JobState.Completed or JobState.Failed))await engine.Pause(job.Id);
        File.WriteAllText(Path.Combine(output,"cache-checks.json"),Json.Encode(new{passed=true,firstAnalysisSeconds=first,downloadAnalysisSeconds=job.AnalysisSeconds}));
    }
    sealed class ReviewHandler(byte[] image):System.Net.Http.HttpMessageHandler {
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken ct){await Task.Delay(60,ct);var path=request.RequestUri!.AbsolutePath;string json;
        if(path.Contains("lookup"))json="""{"status":"ok","results":[{"id":"scan-id","score":0.98,"recordings":[{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"}]}]}""";
        else if(request.RequestUri.Host=="coverartarchive.org")json="""{"images":[{"front":true,"image":"https://images.example/cover.jpg"}]}""";
        else if(path.Contains("cover.jpg")){var r=new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(image)};return r;}
        else if(path.Contains("/recording/"))json="""{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7","title":"A LETTER <nZk Ver.>","artist-credit":[{"name":"澤野弘之"}],"releases":[{"id":"11111111-1111-1111-1111-111111111111","title":"MOBILE SUIT GUNDAM UNICORN Original Soundtrack","date":"2014"},{"id":"22222222-2222-2222-2222-222222222222","title":"另一專輯版本","date":"2020"}]}""";
        else json="""{"title":"MOBILE SUIT GUNDAM UNICORN Original Soundtrack","date":"2014-05-21","artist-credit":[{"name":"澤野弘之"}],"media":[{"position":1,"tracks":[{"number":"7","recording":{"id":"cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"}}]}]}""";
        return new(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent(json)};
        }
    }
    static async Task CheckReview(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output){
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        async Task Idle()=>await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        window.Width=1580;window.Height=980;
        ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;while(editor.Busy)await Task.Delay(20);
        // Match the real Scan entry point: review the currently selected file.
        await (Task)typeof(TagEditorView).GetMethod("SelectSongs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(editor,new object[]{new[]{"music-a"}})!;
        window.PreviewSession.SetVolume(0);window.PreviewSession.TogglePlayback();for(var i=0;i<200&&window.PreviewSession.Position<.1;i++)await Task.Delay(50);Check(window.PreviewSession.Playing&&window.PreviewSession.Ready,"Review fixture must start real playback");
        var title=Descendants(editor).OfType<TextBox>().Single(b=>b.Name=="TIT2");title.Text="未儲存草稿";var comment=Descendants(editor).OfType<TextBox>().Single(b=>b.Name=="COMM");comment.Text="保留這個草稿";
        var root=window.Content;var job=store.Load().Single(j=>j.Id=="music-a");var before=await TagReview.Hash(job.FilePath!);var imagePath=Path.Combine(Environment.CurrentDirectory,"artifacts/qa-0.2.4-reference/reference-album-cover.jpg");var image=File.ReadAllBytes(File.Exists(imagePath)?imagePath:Path.Combine(Environment.CurrentDirectory,"src/Windows/Assets/brand.png"));
        window.MusicServiceFactory=()=>new MusicMetadata(new System.Net.Http.HttpClient(new ReviewHandler(image)));window.OpenAcoustIdReview(editor,job);await Idle();Check(window.Content is AcoustIdReviewView&&window.ActiveView=="acoustid-review","Root navigation missing");Check(app.Windows.Count==1,"Scan opened a second window");Check(!((FrameworkElement)window.FindName("MainNav")).IsVisible,"Sidebar visible on review");((AcoustIdReviewView)window.Content).Back();await Idle();Check(ReferenceEquals(root,window.Content)&&title.Text=="未儲存草稿"&&comment.Text=="保留這個草稿","Cancel discarded draft");Check(before==await TagReview.Hash(job.FilePath!),"Cancel changed file");Check(window.PreviewSession.Playing,"Scan cancellation must preserve playback");
        window.OpenAcoustIdReview(editor,job);await Idle();var review=(AcoustIdReviewView)window.Content;var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var apply=(Button)typeof(AcoustIdReviewView).GetField("apply",flags)!.GetValue(review)!;for(int i=0;i<600&&!apply.IsEnabled;i++)await Task.Delay(50);Check(apply.IsEnabled,"Review results not ready");Capture(window,Path.Combine(output,"review-wide.png"));window.Width=1050;await Idle();Capture(window,Path.Combine(output,"review-narrow.png"));window.Width=1580;await Idle();
        var checks=(Dictionary<string,CheckBox>)typeof(AcoustIdReviewView).GetField("checks",flags)!.GetValue(review)!;foreach(var c in checks.Values)c.IsChecked=false;checks["TIT2"].IsChecked=true;typeof(AcoustIdReviewView).GetMethod("UpdateCount",flags)!.Invoke(review,null);Check(before==await TagReview.Hash(job.FilePath!),"Preview wrote file");await (Task)typeof(AcoustIdReviewView).GetMethod("Apply",flags)!.Invoke(review,null)!;while(editor.Busy)await Task.Delay(20);await Idle();Check(ReferenceEquals(root,window.Content),"Apply did not return to editor");Check(title.Text=="A LETTER <nZk Ver.>"&&comment.Text=="保留這個草稿","Apply dropped unchecked draft");Check(!window.PreviewSession.Playing&&!window.PreviewSession.Ready,"Explicit metadata apply must release the playing file before writing");Check(Id3Document.Read(job.FilePath!).Text("TPE1")=="","Unchecked artist changed");Check(Id3Document.Read(job.FilePath!).GetCovers().Count==0,"Unselected artwork changed");Capture(window,Path.Combine(output,"review-applied.png"));
        var undo=(TagReviewUndo)typeof(TagEditorView).GetField("lastUndo",flags)!.GetValue(editor)!;await undo.Restore(store,job);Check(before==await TagReview.Hash(job.FilePath!),"Undo did not restore exact file");File.WriteAllText(Path.Combine(output,"review-checks.json"),Json.Encode(new{passed=true,checks=new[]{"same-window root route","sidebar hidden","cancel retains draft","multiple candidates","no pre-apply writes","checked fields only","artwork opt-in","draft merge on return","exact undo","scan cancel preserves playback","explicit apply releases audio before write"}}));
    }
    static async Task CheckV21(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output){
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        void Click(string name)=>((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        async Task Idle()=>await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        window.Width=1580;window.Height=980;window.OpenHistory();await Idle();Capture(window,Path.Combine(output,"library-icons.png"));
        Click("TagsNav");await Idle();var host=(ContentControl)window.FindName("PageHost");Check(host.Content is TagEditorView,"Tag page missing");var editor=(TagEditorView)host.Content;for(int i=0;i<100&&editor.Busy;i++)await Task.Delay(30);
        await (Task)typeof(TagEditorView).GetMethod("SelectSongs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(editor,new object[]{new[]{"music-a"}})!;
        var title=Descendants(editor).OfType<TextBox>().Single(b=>b.Name=="TIT2");var save=Descendants(editor).OfType<Button>().Single(b=>b.Name=="SaveTags");
        title.Text=new string('W',300);await Idle();Check(title.ActualWidth<=320,"Long title must not expand textbox");Check(Descendants(editor).OfType<ScrollViewer>().Where(v=>v.Content is StackPanel).All(v=>v.ScrollableWidth<1),"Editor must not scroll horizontally");title.Text="invalid/title";Check(save.IsEnabled,"Metadata title must allow filename characters when rename is off");Capture(window,Path.Combine(output,"tags-validation.png"));
        title.Text="城市夜色";Check(save.IsEnabled,"Valid change should save");var original=store.Load().Single(j=>j.Id=="music-a").FilePath;
        save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for(int i=0;i<100&&store.Load().Single(j=>j.Id=="music-a").Title!="城市夜色";i++)await Task.Delay(50);
        for(int i=0;i<100&&editor.Busy;i++)await Task.Delay(30);await Idle();Check(store.Load().Single(j=>j.Id=="music-a").Title=="城市夜色","Tag did not save");Check(store.Load().Single(j=>j.Id=="music-a").FilePath==original,"Opt-out rename changed filename");Capture(window,Path.Combine(output,"tag-editor.png"));
        Click("SettingsNav");await Idle();Check(((FrameworkElement)window.FindName("MainNav")).Visibility==Visibility.Collapsed,"Main sidebar should be hidden in settings");Check(Grid.GetColumn(host)==0,"Settings should fill all columns");
        Capture(window,Path.Combine(output,"settings-full-width.png"));
        var settingsProbe=(SettingsView)host.Content;var themeChoice=Descendants(settingsProbe).OfType<ComboBox>().First(c=>c.Items.OfType<ComboBoxItem>().Any(x=>Equals(x.Tag,"dark")));themeChoice.SelectedIndex=1;Check(settingsProbe.HasChanges(),"Changed theme must be dirty");themeChoice.SelectedIndex=0;Check(!settingsProbe.HasChanges(),"Reverted theme must not be dirty");
        var p=engine.Settings;p.TextScale=125;p.UiScale=110;UiKit.Apply(window,p);await Idle();Capture(window,Path.Combine(output,"settings-scaled.png"));
        p.Theme="light";UiKit.Apply(window,p);await Idle();Capture(window,Path.Combine(output,"settings-scaled-light.png"));
        var settings=(SettingsView)host.Content;settings.Back!();await Idle();Check(ReferenceEquals(host.Content,editor),"Settings back must restore same tag editor");
        var all=Descendants(editor).OfType<Button>().Single(b=>b.Content is string x&&x=="全選");all.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));for(int i=0;i<100&&editor.Busy;i++)await Task.Delay(30);await Idle();
        Check(Descendants(editor).OfType<CheckBox>().Count(c=>c.IsVisible&&c.Content is null)==2,"Multi-select must show song checkboxes");
        var clear=Descendants(editor).OfType<Button>().Single(b=>b.Content is string x&&x=="取消全選");clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));for(int i=0;i<100&&editor.Busy;i++)await Task.Delay(30);await Idle();Check(!Descendants(editor).OfType<CheckBox>().Any(c=>c.IsVisible&&c.Content is null),"Cleared selection must hide song checkboxes");
        var imports=Enumerable.Range(0,24).Select(i=>Path.Combine(output,$"scroll-{i}.mp3")).ToArray();foreach(var path in imports)File.Copy(original!,path,true);await editor.Import(imports);await Idle();
        var songPanel=(StackPanel)typeof(TagEditorView).GetField("songList",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!;var songScroll=(ScrollViewer)songPanel.Parent;Check(songScroll.ScrollableHeight>0,"Song fixture must overflow");
        var bar=Descendants(songScroll).OfType<System.Windows.Controls.Primitives.ScrollBar>().First(b=>b.Orientation==Orientation.Vertical);var thumb=Descendants(bar).OfType<System.Windows.Controls.Primitives.Thumb>().First();Check(thumb.ActualHeight>0&&thumb.IsHitTestVisible,"Scrollbar thumb must be reachable");thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0));thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,40));thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,40,false));await Idle();Check(songScroll.VerticalOffset>0,"Dragging thumb must scroll songs");
        window.OpenHistory();await Idle();var equal=Descendants(host).OfType<System.Windows.Controls.Primitives.UniformGrid>().First(g=>g.Columns==4);Check(equal.Children.OfType<FrameworkElement>().Select(c=>Math.Round(c.ActualWidth,1)).Distinct().Count()==1,"Summary cards must be equal width");
        var libraryTable=Descendants(host).OfType<DataGrid>().Single();libraryTable.SelectedItem=libraryTable.Items[0];await Idle();Check(libraryTable.Columns[0].Visibility==Visibility.Collapsed,"Single selection hides checkbox");libraryTable.SelectedItems.Add(libraryTable.Items[1]);await Idle();Check(libraryTable.Columns[0].Visibility==Visibility.Visible,"Multiple selection shows checkboxes");Check(libraryTable.Items.Cast<LibraryView.LibraryRow>().Count(r=>r.Checked)==2,"Row selection must select files for actions");Capture(window,Path.Combine(output,"library-equal-width.png"));
        File.WriteAllText(Path.Combine(output,"checks.json"),Json.Encode(new{passed=true,checks=new[]{"embedded tag editor","invalid title guard","actual tag save","filename preserved","full-width settings","125 percent text and 110 percent UI","settings returns to original editor","select all and clear","conditional checkboxes","equal-width summary","song scrollbar drag","library row selection","fixed long-title width","settings revert is clean"}}));
    }
    static async Task CheckArtworkDrop(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output){
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        byte[] Png(int w,int h,byte color){var pixels=Enumerable.Repeat(color,w*h*4).ToArray();var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var buffer=new MemoryStream();encoder.Save(buffer);return buffer.ToArray();}
        var original=Png(250,250,240);var replacement=Png(475,500,80);var song=store.Load().Single(j=>j.Id=="music-a");
        var doc=Id3Document.Read(song.FilePath!);doc.SetText("COMM","保留註解 日本語");doc.SetCovers([new(original,"image/png","Original",3)]);var tagged=song.FilePath+".tagged";await doc.Write(song.FilePath!,tagged);File.Move(tagged,song.FilePath!,true);var originalHash=await TagReview.Hash(song.FilePath!);
        ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;while(editor.Busy)await Task.Delay(20);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{"music-a"}})!;
        var read=typeof(TagEditorView).GetMethod("ReadCoverData",flags)!;var selected=(HashSet<string>)typeof(TagEditorView).GetField("selected",flags)!.GetValue(editor)!;
        async Task Read(System.Windows.IDataObject data)=>await (Task)read.Invoke(editor,new object[]{data})!;
        byte[] Preview()=>((List<Cover>)typeof(TagEditorView).GetField("covers",flags)!.GetValue(editor)!).Single().Bytes;
        var cover=(System.Windows.Controls.Image)typeof(TagEditorView).GetField("artwork",flags)!.GetValue(editor)!;
        var target=Descendants(editor).OfType<Grid>().Single(g=>g.Name=="AlbumCoverDropTarget");Check(target.AllowDrop&&target.IsVisible,"Artwork drop target must be reachable");var path=Path.Combine(output,"拖放封面.png");File.WriteAllBytes(path,replacement);
        var ctor=typeof(DragEventArgs).GetConstructors(flags).Single();
        var drag=(DragEventArgs)ctor.Invoke(new object[]{new System.Windows.DataObject(DataFormats.FileDrop,new[]{path}),DragDropKeyStates.None,DragDropEffects.Copy,cover,new Point(110,110)});drag.RoutedEvent=DragDrop.PreviewDropEvent;cover.RaiseEvent(drag);while(editor.Busy)await Task.Delay(20);
        Check(drag.Handled&&selected.SetEquals(new[]{"music-a"})&&Preview().SequenceEqual(replacement),"Dropping onto existing artwork must replace preview and preserve song selection");
        var dataUrl="data:image/png;base64,"+Convert.ToBase64String(replacement);var browser=new System.Windows.DataObject();browser.SetData(DataFormats.Html,$"<a href='https://example.org/page'><img src='{dataUrl}'></a>");browser.SetData(DataFormats.UnicodeText,"https://example.org/page");
        Check(ArtworkInput.CanRead(browser),"Browser HTML must be accepted by drag-over");await Read(browser);Check(Preview().SequenceEqual(replacement),"Browser HTML image must be used instead of its page link");
        var stream=new MemoryStream(replacement);stream.Position=stream.Length;await Read(new System.Windows.DataObject("PNG",stream));Check(stream.Position==stream.Length&&Preview().SequenceEqual(replacement),"PNG stream must be read from beginning without changing source position");
        await Read(new System.Windows.DataObject("FileContents",new MemoryStream[]{new(replacement)}));Check(Preview().SequenceEqual(replacement),"Virtual image file must load");
        await Read(new System.Windows.DataObject(DataFormats.UnicodeText,new Uri(path).AbsoluteUri));Check(Preview().SequenceEqual(replacement),"file URI must load");
        var unnamed=Path.Combine(output,"browser-image.tmp");File.WriteAllBytes(unnamed,replacement);await Read(new System.Windows.DataObject(DataFormats.FileDrop,new[]{unnamed}));Check(Preview().SequenceEqual(replacement),"Image file without PNG extension must load from its contents");
        var remote=new System.Windows.DataObject();remote.SetData(DataFormats.Html,"<img src='https://example.org/cover?x=1&amp;y=2'>");remote.SetData(DataFormats.UnicodeText,"https://example.org/page");var captured=ArtworkInput.Capture(remote);Check(captured.Url?.AbsoluteUri=="https://example.org/cover?x=1&y=2","HTML image URL must be decoded");
        using(var http=new System.Net.Http.HttpClient(new ArtworkHandler(replacement)))Check((await captured.Read(engine.FfmpegPath,CancellationToken.None,http)).SequenceEqual(replacement),"Remote image bytes must load");
        using(var urlStream=new MemoryStream(System.Text.Encoding.Unicode.GetBytes("https://example.org/cover\0"))){var url=ArtworkInput.Capture(new System.Windows.DataObject("UniformResourceLocatorW",urlStream));Check(url.Url?.AbsoluteUri=="https://example.org/cover","Browser Unicode URL stream must load");}
        var webp=Path.Combine(output,"album.webp");await ProcessRunner.Run(engine.FfmpegPath,["-y","-i",path,"-frames:v","1","-c:v","libwebp",webp],null,CancellationToken.None);await Read(new System.Windows.DataObject(DataFormats.FileDrop,new[]{webp}));Check(AlbumArtwork.Dimensions(Preview())==(475,500),"WebP conversion must preserve dimensions");
        var failedBefore=Preview();await Read(new System.Windows.DataObject(DataFormats.Text,"not an image"));Check(Preview().SequenceEqual(failedBefore),"Unsupported drop must preserve artwork draft");Check(await TagReview.Hash(song.FilePath!)==originalHash,"Artwork preview must not write MP3");
        // Drop a PNG slightly outside the image: it must not be imported as an empty MP3 selection.
        var outer=(DragEventArgs)ctor.Invoke(new object[]{new System.Windows.DataObject(DataFormats.FileDrop,new[]{path}),DragDropKeyStates.None,DragDropEffects.Copy,editor,new Point(10,10)});outer.RoutedEvent=DragDrop.DropEvent;editor.RaiseEvent(outer);while(editor.Busy)await Task.Delay(20);Check(selected.SetEquals(new[]{"music-a"})&&Preview().SequenceEqual(replacement),"Image drop outside artwork must keep selected song and change artwork");
        await (Task)typeof(TagEditorView).GetMethod("Save",flags)!.Invoke(editor,null)!;doc=Id3Document.Read(song.FilePath!);Check(doc.GetCovers().Single().Bytes.SequenceEqual(replacement),"Save must persist replacement artwork");Check(doc.Text("COMM")=="保留註解 日本語","Cover replacement must preserve other tags");Check(!Directory.EnumerateFiles(output,"*.jpg").Any(),"Artwork import must not create JPG sidecars");
        await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Capture(window,Path.Combine(output,"artwork-drop-saved.png"));File.WriteAllText(Path.Combine(output,"artwork-drop-checks.json"),Json.Encode(new{passed=true,checks=new[]{"existing-cover drop routing","local PNG","browser HTML image","browser Unicode URL stream","remote image request","virtual image content","PNG stream reset","file URI","WebP dimensions","failure preserves draft","outside-image routing","preview does not write","saved APIC replacement","Unicode tags preserved","no JPG sidecar"}}));
    }
    sealed class ArtworkHandler(byte[] image):System.Net.Http.HttpMessageHandler{
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(image)});
    }
    static async Task CheckV24(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output){
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;
        for(int i=0;i<100&&editor.Busy;i++)await Task.Delay(30);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{"music-a","music-b"}})!;
        var pixel=BitmapSource.Create(475,500,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)128,475*500*4).ToArray(),475*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(pixel));using var encoded=new MemoryStream();encoder.Save(encoded);var png=encoded.ToArray();var artPath=Path.Combine(output,"album-cover.png");File.WriteAllBytes(artPath,png);
        var read=typeof(TagEditorView).GetMethod("ReadCoverData",flags)!;
        await (Task)read.Invoke(editor,new object[]{new System.Windows.DataObject(DataFormats.FileDrop,new[]{artPath})})!;Check(editor.Dirty,"File artwork must create unsaved preview");Check(!Id3Document.Read(store.Load().Single(j=>j.Id=="music-a").FilePath!).GetCovers().Any(),"Preview must not write before save");
        await (Task)read.Invoke(editor,new object[]{new System.Windows.DataObject("PNG",new MemoryStream(png))})!;Check(editor.Dirty,"PNG clipboard format must preview");
        await (Task)read.Invoke(editor,new object[]{new System.Windows.DataObject(DataFormats.Bitmap,pixel)})!;Check(editor.Dirty,"Bitmap clipboard format must preview");
        Check(Descendants(editor).OfType<Grid>().Any(g=>g.AllowDrop&&g.Width==220),"Artwork drop target missing");
        await (Task)typeof(TagEditorView).GetMethod("Save",flags)!.Invoke(editor,null)!;
        foreach(var id in new[]{"music-a","music-b"}){var song=store.Load().Single(j=>j.Id==id);Check(song.CoverUserEdited,"Manual artwork protection not saved");Check(Id3Document.Read(song.FilePath!).GetCovers().Count==1,"Batch artwork not embedded");}
        Check(!Directory.EnumerateFiles(output,"*.jpg").Any(),"Saving MP3 artwork created a standalone JPG");
        Capture(window,Path.Combine(output,"tag-cover-paste.png"));
        var service=new MusicMetadata(new System.Net.Http.HttpClient(new FingerprintHandler()));var audio=store.Load().Single(j=>j.Id=="music-a").FilePath!;
        var fpAudio=Path.Combine(output,"fingerprint-fixture.mp3");await ProcessRunner.Run(engine.FfmpegPath,["-y","-f","lavfi","-i","sine=frequency=440:duration=200","-codec:a","libmp3lame",fpAudio],null,CancellationToken.None);
        var result=await service.Scan(fpAudio,engine.FfmpegPath,"test-application-key",null,CancellationToken.None);Check(result.State==MusicLookupState.NoMatch,"Fingerprint request pipeline failed");
        File.WriteAllText(Path.Combine(output,"v24-checks.json"),Json.Encode(new{passed=true,checks=new[]{"file-drop preview","PNG clipboard representation","bitmap clipboard representation","multi-file cover save","manual cover protection","local FFmpeg Chromaprint and mocked AcoustID post"}}));
    }
    sealed class FingerprintHandler:System.Net.Http.HttpMessageHandler{
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken ct){var body=await request.Content!.ReadAsStringAsync(ct);if(request.Method!=System.Net.Http.HttpMethod.Post||!body.Contains("fingerprint=")||!body.Contains("duration=20")||body.Contains(".mp3"))throw new Exception("Invalid fingerprint POST");return new(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent("{\"status\":\"ok\",\"results\":[]}")};}
    }
    static async Task CheckV2(MainWindow window,Downloader engine,Store store,System.Windows.Application app,string output) {
        void Check(bool ok,string error){if(!ok)throw new Exception(error);}
        window.Width=1580;window.Height=980;
        window.OpenHistory();await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var host=(ContentControl)window.FindName("PageHost");Check(host.Content is LibraryView,"History does not use embedded library");
        var library=(LibraryView)host.Content;var grid=Descendants(library).OfType<DataGrid>().Single();
        Check(grid.Columns.Count==8,"Library needs checkbox, thumbnail/title, format, quality, size, finished, status and menu");
        Check(grid.Items.Count==3,"Completed files absent");grid.SelectedIndex=0;
        await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var checks=Descendants(library).OfType<CheckBox>().Where(c=>c.Content is string x&&x.Contains("全選")).ToArray();Check(checks.Length==1,"Select-all missing");checks[0].IsChecked=true;checks[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(Descendants(library).OfType<Button>().Single(b=>b.Content?.ToString()=="移除所選").IsEnabled,"Multi-delete remains disabled");
        Capture(window,Path.Combine(output,"v2-library-dark.png"));
        ((Button)window.FindName("SettingsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(host.Content is SettingsView&&!window.OwnedWindows.OfType<SettingsWindow>().Any(),"Settings must be embedded");
        var settings=(SettingsView)host.Content;Capture(window,Path.Combine(output,"v2-settings-dark.png"));
        foreach(var name in new[]{"下載","格式","網絡","通知","關於","一般"}) {
            var button=Descendants(settings).OfType<Button>().First(b=>b.Content?.ToString()?.EndsWith("　"+name)==true);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(Descendants(settings).OfType<ScrollViewer>().Any(),"Settings scrolling missing: "+name);
        }
        var pref=engine.Settings;pref.Theme="light";UiKit.Apply(window,pref);Capture(window,Path.Combine(output,"v2-settings-light.png"));
        pref.Theme="dark";UiKit.Apply(window,pref);
        settings.StartupWriter=_=>{};
        var language=Descendants(settings).OfType<ComboBox>().First(c=>c.Items.OfType<ComboBoxItem>().Any(i=>i.Tag?.ToString()=="en"));language.SelectedItem=language.Items.OfType<ComboBoxItem>().Single(i=>i.Tag?.ToString()=="en");
        Check(settings.Save(),"Cannot save language setting");await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        Check(engine.Settings.Language=="en"&&Descendants(settings).OfType<TextBlock>().Any(t=>t.Text=="Settings"),"English labels not refreshed on save");
        Capture(window,Path.Combine(output,"v2-settings-english.png"));

        File.WriteAllText(Path.Combine(output,"v2-checks.json"),Json.Encode(new{passed=true,checks=new[]{"completed library columns","multi-selection","embedded six-category settings","dark/light rendering"}}));
    }
    static void Capture(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content; visual.UpdateLayout(); var bounds=visual.LayoutTransform.TransformBounds(new Rect(0,0,visual.ActualWidth,visual.ActualHeight));var width=bounds.Width+visual.Margin.Left+visual.Margin.Right; var height=bounds.Height+visual.Margin.Top+visual.Margin.Bottom; var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); var background = new DrawingVisual(); using (var dc = background.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height)); bitmap.Render(background); bitmap.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var f = File.Create(path); png.Save(f);
    }
    static async Task CheckClearInputs(MainWindow window,System.Windows.Application app,string output)
    {
        var url=(TextBox)window.FindName("UrlBox");var urlClear=(Button)window.FindName("ClearUrlButton");
        var search=(TextBox)window.FindName("SearchBox");var searchClear=(Button)window.FindName("ClearSearchButton");
        if(urlClear.Visibility!=Visibility.Collapsed||searchClear.Visibility!=Visibility.Collapsed)throw new Exception("Clear buttons should hide when empty");
        url.Text="https://example.org/video";search.Text="artist";
        await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(urlClear.Visibility!=Visibility.Visible||searchClear.Visibility!=Visibility.Visible)throw new Exception("Clear buttons should appear with text");
        Capture(window,Path.Combine(output,"clear-inputs-visible.png"));
        urlClear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));searchClear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(url.Text!=""||search.Text!=""||urlClear.Visibility!=Visibility.Collapsed||searchClear.Visibility!=Visibility.Collapsed)throw new Exception("Clear buttons failed to clear and hide");
        File.WriteAllText(Path.Combine(output,"clear-inputs.json"),"{\"passed\":true}");
    }
    static async Task CheckUi(MainWindow window, System.Windows.Application app, string output)
    {
        void Check(bool valid,string reason) { if(!valid) throw new Exception(reason); }
        T Find<T>(string name) => (T)window.FindName(name);
        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.OpenHistory(); var grid=Find<DataGrid>("JobGrid"); grid.SelectedIndex=0;
        var url=Find<TextBox>("UrlBox"); var full="https://www.youtube.com/watch?v=ui_fixture&list=PL_preview&index=123&feature=shared"; url.Text=full;
        Check(Find<Button>("ClearUrlButton").Visibility==Visibility.Visible,"URL clear button is hidden with text");Click("ClearUrlButton");Check(url.Text=="","URL clear button did not clear text");url.Text=full;
        var search=Find<TextBox>("SearchBox");search.Text="fixture";Check(Find<Button>("ClearSearchButton").Visibility==Visibility.Visible,"Search clear button is hidden with text");Click("ClearSearchButton");Check(search.Text=="","Search clear button did not clear text");
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
