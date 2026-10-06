using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Omni.Core;
using Omni.Windows;

// Render the real WPF views, using only a fresh private store and generated media.
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if(args.Length!=3){Console.Error.WriteLine("Usage: Readme.Screenshots <fresh-work-dir> <screenshots-dir> <ffmpeg.exe>");return 2;}
        var work=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);var ffmpeg=Path.GetFullPath(args[2]);
        if(Directory.Exists(work)){Console.Error.WriteLine("Use a new work directory; existing stores are never read or removed.");return 2;}
        Directory.CreateDirectory(work);Directory.CreateDirectory(output);
        var store=new Store(Path.Combine(work,"data"));
        store.SavePreferences(new Preferences{Language="en",AutoUpdate=false,ResumeOnStart=false,MonitorClipboard=false,Mp3MetadataMode="off",MusicBrainz=false,DownloadDirectory=@"C:\Omni-Demo",Mp3Directory=@"C:\Omni-Demo\Audio",Mp4Directory=@"C:\Omni-Demo\Video"});
        var titles=new[]{"Northern Lights","Quiet Orbit","Color Study"};var colors=new[]{"#7D55E7","#249AAA","#EF9168"};
        for(var i=0;i<3;i++)
        {
            var audio=i<2;var path=Path.Combine(work,titles[i]+(audio?".mp3":".mp4"));var art=Path.Combine(work,$"art-{i}.png");
            DrawArtwork(art,colors[i],i);
            var mediaArgs=audio?new[]{"-y","-f","lavfi","-i","anullsrc=r=44100:cl=stereo","-t","20","-c:a","libmp3lame","-b:a","320k",path}:new[]{"-y","-f","lavfi","-i","color=c=0x18222D:s=640x360:r=1:d=2","-c:v","libx264","-pix_fmt","yuv420p",path};
            ProcessRunner.Run(ffmpeg,mediaArgs,null,CancellationToken.None).GetAwaiter().GetResult();
            var job=new DownloadJob{Id=$"sample-{i}",RequestId=$"sample-{i}",State=JobState.Completed,Title=titles[i],Artist=audio?"Demo Ensemble":"",Album=audio?"Sample Sessions":"",Mode=audio?DownloadMode.Mp3:DownloadMode.Mp4,FilePath=path,Url=$"https://example.invalid/demo/{i}",Thumbnail=new Uri(art).AbsoluteUri,AudioKbps=320,Height=360,Duration=audio?20:2,TotalBytes=new FileInfo(path).Length,CompletedAt=new DateTimeOffset(2026,1,1,12,i,0,TimeSpan.Zero)};
            if(audio)new TagEditor(store).Apply([job],new(new(){["TIT2"]=titles[i],["TPE1"]="Demo Ensemble",["TALB"]="Sample Sessions",["TPE2"]="Demo Ensemble",["TRCK"]=(i+1).ToString(),["TPOS"]="1",["TYER"]="2026",["TCON"]="Demo",["COMM"]="Synthetic audio and original artwork; demonstration only."},Covers:[new Cover(File.ReadAllBytes(art),"image/png","Generated sample",3)],RenameFile:false),persist:false).GetAwaiter().GetResult();
            store.Save(job);
        }
        store.Save(new DownloadJob{Id="sample-paused",RequestId="sample-paused",State=JobState.Paused,Title="Gradient Motion · sample task",Url="https://example.invalid/demo/gradient-motion",Mode=DownloadMode.Mp4,Height=1080,Progress=58,TotalBytes=24_000_000,Directory=@"C:\Omni-Demo\Video"});
        var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        // No main Program/registry, IPC, update checks, browser session or live downloads.
        var engine=new Downloader(store,Path.GetDirectoryName(ffmpeg)!,Path.Combine(work,"staging"));
        var window=new MainWindow(engine,store){AllowClose=true,ShowInTaskbar=false,Left=-10000,Top=-10000,Width=1440,Height=1040,WindowStartupLocation=WindowStartupLocation.Manual};
        bool started=false;
        window.ContentRendered+=async(_,_)=>
        {
            if(started)return;started=true;
            try
            {
                async Task Idle()=>await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                void DemoStorage()=>((TextBlock)window.FindName("SpaceLabel")).Text="Demo · 120 GB free";
                await Idle();DemoStorage();Capture(window,Path.Combine(output,"windows-downloads.png"));
                ((Button)window.FindName("TagsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();
                var editor=(TagEditorView)((ContentControl)window.FindName("PageHost")).Content;
                for(var i=0;i<500&&editor.Busy;i++)await Task.Delay(20);
                if(editor.Busy)throw new Exception("Demo editor did not finish loading.");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                await (Task)typeof(TagEditorView).GetMethod("SelectSongs",flags)!.Invoke(editor,new object[]{new[]{"sample-0"}})!;
                // Path labels are sample-only; actual files remain inside the isolated work directory.
                ((TextBlock)typeof(TagEditorView).GetField("fileLocation",flags)!.GetValue(editor)!).Text=@"C:\Omni-Demo\Audio\Northern Lights.mp3";
                var preview=Descendants(editor).OfType<AudioPreview>().Single();preview.Session.SetVolume(.5);
                await Task.Delay(500);await Idle();DemoStorage();Capture(window,Path.Combine(output,"windows-tag-editor.png"));
                ((Button)window.FindName("HistoryNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();
                var library=(LibraryView)((ContentControl)window.FindName("PageHost")).Content;
                var table=Descendants(library).OfType<DataGrid>().Single();table.SelectedIndex=0;await Idle();
                foreach(var box in Descendants(library).OfType<TextBox>().Where(b=>b.IsReadOnly&&b.Text.StartsWith(work,StringComparison.OrdinalIgnoreCase)))box.Text=@"C:\Omni-Demo\"+Path.GetFileName(box.Text);
                DemoStorage();Capture(window,Path.Combine(output,"windows-library.png"));
                ((Button)window.FindName("SettingsNav")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Idle();Capture(window,Path.Combine(output,"windows-settings.png"));
                Console.WriteLine("Rendered four real WPF views with generated media, original artwork and sample paths.");
                await engine.DisposeAsync();window.Close();app.Shutdown(0);
            }
            catch(Exception e){Console.Error.WriteLine(e);await engine.DisposeAsync();window.Close();app.Shutdown(1);}
        };
        return app.Run(window);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
    static void DrawArtwork(string path,string color,int variant)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18,25,39)),null,new Rect(0,0,500,500));
            var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(color),Color.FromRgb(34,77,133),45);
            dc.DrawEllipse(brush,null,new Point(250,250),178,178);
            var pen=new Pen(new SolidColorBrush(Color.FromArgb(180,223,235,255)),6);
            dc.DrawEllipse(null,pen,new Point(250,250),110+variant*10,110+variant*10);
            dc.DrawLine(pen,new Point(105,350-variant*20),new Point(395,150+variant*20));
            dc.DrawEllipse(Brushes.White,null,new Point(250,250),12,12);
        }
        Save(visual,500,500,path);
    }
    static void Capture(Window window,string path)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();
        Save(content,(int)content.ActualWidth,(int)content.ActualHeight,path);
    }
    static void Save(Visual visual,int width,int height,string path){var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(17,25,35)),null,new Rect(0,0,width,height));bitmap.Render(background);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
}
