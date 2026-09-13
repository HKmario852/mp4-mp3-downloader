using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Omni.Core;
namespace Omni.Windows;
public partial class MainWindow : Window
{
    readonly Downloader engine; readonly Store store; readonly DispatcherTimer timer; readonly HashSet<string> choices = []; bool history, tagsPage, recentSelection, failures, collapsed; DownloadMode mode = DownloadMode.Mp4; string groupSignature = "";
    string? thumbnailUrl;
    bool refreshing;
    string? previewJobId;
    MediaInfo? previewInfo;
    readonly Dictionary<string, MediaInfo> mediaCache = new();
    int analysisVersion;
    public bool AllowClose { get; set; }
    public MainWindow(Downloader engine, Store store)
    {
        this.engine = engine; this.store = store; InitializeComponent(); SetMode(DownloadMode.Mp4);
        engine.WriteExternalCover = (bytes, path) => Dispatcher.InvokeAsync(() => CoverIO.Write(bytes, path, this)).Task.Unwrap();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; timer.Tick += (_, _) => Refresh(); timer.Start();
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; ShowInTaskbar = false; Hide(); } };
        SizeChanged += (_, _) => { DetailColumn.Width = new GridLength(ActualWidth < 1100 ? 320 : 380); };
        UpdateNavigation();
        Refresh(); StatusLabel.Text = engine.ToolsReady ? "已準備就緒" : "請先執行 scripts/Prepare-Tools.ps1 準備 yt-dlp 與 ffmpeg。";
    }
    public void Reveal() { ShowInTaskbar = true; Show(); WindowState = WindowState.Normal; Activate(); if (!NativeFocus.Foreground(new System.Windows.Interop.WindowInteropHelper(this).Handle)) NativeFocus.Flash(new System.Windows.Interop.WindowInteropHelper(this).Handle); }
    public void OpenHistory() { history = true; tagsPage = false; recentSelection = false; Refresh(); Reveal(); }
    public void AskChoice(DownloadJob j)
    {
        if (!choices.Add(j.Id)) return; Reveal();
        var dialog = new Window { Title = "選擇下載範圍", Width = 490, Height = 240, Owner = this, Background = Background, Foreground = Foreground, WindowStartupLocation = WindowStartupLocation.CenterOwner, Resources = Resources };
        var body = new StackPanel { Margin = new Thickness(22) }; body.Children.Add(new TextBlock { Text = "呢個連結同時包含影片同播放清單", FontSize = 19, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
        foreach (var (label, all) in new[] { ("僅下載此影片", false), ("下載整個播放清單", true) })
        { var b = new Button { Content = label }; b.Click += async (_, _) => { await engine.Choose(j.Id, all); dialog.Close(); }; body.Children.Add(b); }
        dialog.Content = body; dialog.Closed += (_, _) => choices.Remove(j.Id); dialog.Show();
        // Closing the choice leaves PendingChoice; no timeout or default action.
    }
    void Refresh()
    {
        if (!IsInitialized) return;
        var selected = JobGrid.SelectedItems.Cast<DownloadJob>().Select(x => x.Id).ToHashSet();
        var items = history ? store.Load(true).Where(j => j.State == JobState.Completed && !j.IsGroupRoot && MatchesLibrary(j)).OrderByDescending(j => j.CompletedAt).ToArray() : engine.Jobs.Where(j => !j.IsGroupRoot && j.GroupId is null && (failures ? j.State == JobState.Failed : LibraryActions.InQueue(j))).OrderByDescending(j => j.CreatedAt).ToArray();
        refreshing = true;
        try {
            JobGrid.ItemsSource = items; foreach (var j in items.Where(j => selected.Contains(j.Id))) JobGrid.SelectedItems.Add(j);
            var recentIds = RecentGrid.SelectedItems.Cast<DownloadJob>().Select(j => j.Id).ToHashSet();
            var recent = store.Load(true).Where(j => j.State == JobState.Completed && !j.IsGroupRoot).OrderByDescending(j => j.CompletedAt).Take(3).ToArray();
            RecentGrid.ItemsSource = recent; foreach (var j in recent.Where(j => recentIds.Contains(j.Id))) RecentGrid.SelectedItems.Add(j);
        }
        finally { refreshing = false; }
        EmptyPanel.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed; CountLabel.Text = $"({items.Length})"; PageTitle.Text = tagsPage ? "標籤編輯 · MP3" : history ? "已下載" : failures ? "下載失敗" : "下載任務";
        SearchBox.Visibility = ClearButton.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.IsEnabled = history && items.Length > 0;
        DeleteFileButton.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
        DeleteFileButton.IsEnabled = history && SelectedJobs().Length == 1 && SelectedJobs()[0].State == JobState.Completed;
        FailedButton.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
        FailedButton.Content = failures ? "返回下載任務" : $"失敗任務（{engine.Jobs.Count(j => j.State == JobState.Failed && !j.IsGroupRoot)}）";
        if (previewJobId is string selectedId) { var selectedJob = engine.Jobs.FirstOrDefault(j => j.Id == selectedId); MetadataLabel.Text = selectedJob?.MetadataStatus ?? ""; }
        FormatFilter.Visibility = history && !tagsPage ? Visibility.Visible : Visibility.Collapsed;
        EditTagsButton.Visibility = history || RecentGrid.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = tagsPage ? "尚未有符合條件的 MP3" : "將喜歡的影音收藏到這裡";
        var groups = engine.Groups.Where(g => engine.Jobs.Any(j => j.GroupId == g.Id && !j.IsGroupRoot && (failures ? j.State == JobState.Failed : LibraryActions.InQueue(j)))).ToArray(); var sig = failures + ":" + string.Join(';', groups.Select(g => g.Id + ":" + g.Title + ":" + string.Join(',', engine.Jobs.Where(j => j.GroupId == g.Id && !j.IsGroupRoot && (failures ? j.State == JobState.Failed : LibraryActions.InQueue(j))).Select(j => j.Id))));
        if (groupSignature != sig)
        {
            groupSignature = sig; GroupCards.Children.Clear();
            foreach (var g in groups)
            {
                var children = engine.Jobs.Where(j => j.GroupId == g.Id && !j.IsGroupRoot && (failures ? j.State == JobState.Failed : LibraryActions.InQueue(j))).ToArray(); var exp = new Expander { Header = $"清單 · {g.Title} ({children.Length})", Foreground = Foreground, Margin = new Thickness(3, 6, 3, 6) };
                var panel = new StackPanel(); var cancel = new Button { Content = "移除清單未完成任務" }; cancel.Click += async (_, _) => await engine.Cancel(engine.Jobs.Where(j => j.GroupId == g.Id).Select(j => j.Id)); panel.Children.Add(cancel);
                var list = new ListBox { MaxHeight = 150, ItemsSource = children, Background = Background, Foreground = Foreground }; var template = new DataTemplate(); var stack = new FrameworkElementFactory(typeof(StackPanel)); var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title")); stack.AppendChild(name); var state = new FrameworkElementFactory(typeof(TextBlock)); state.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StatusText")); state.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(106, 149, 255))); stack.AppendChild(state); template.VisualTree = stack; list.ItemTemplate = template; VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(list, true); panel.Children.Add(list); exp.Content = panel; GroupCards.Children.Add(exp);
            }
        }
        if (!history && groups.Length > 0) EmptyPanel.Visibility = Visibility.Collapsed;
        GroupsPanel.Visibility = !history && groups.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var exp in GroupCards.Children.OfType<Expander>()) if (exp.IsExpanded && exp.Content is StackPanel groupPanel) foreach (var list in groupPanel.Children.OfType<ListBox>()) list.Items.Refresh();
        RecentPanel.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
        try { var drive = new DriveInfo(Path.GetPathRoot(DirectoryBox.Text)!); SpaceLabel.Text = $"可用空間 {drive.AvailableFreeSpace / 1_000_000_000.0:F1} GB"; } catch (Exception ex) when (ex is IOException or ArgumentException) { SpaceLabel.Text = "未能取得可用空間"; }
    }
    async void AnalyzeClick(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim(); var version = ++analysisVersion; previewJobId = null; MetadataLabel.Text = "";
        try { StatusLabel.Text = "正在分析連結…"; var info = await engine.Analyze(url); mediaCache[url] = info; if (version != analysisVersion || UrlBox.Text.Trim() != url) return; ApplyPreview(info); StatusLabel.Text = "選擇格式及品質後開始下載"; }
        catch (Exception ex) { if (version == analysisVersion) StatusLabel.Text = ex.Message; }
    }
    public void ApplyPreview(MediaInfo info) { previewInfo = info; MediaTitle.Text = info.Title; MediaSubtitle.Text = info.Duration is double d ? $"片長 {TimeSpan.FromSeconds(d):hh\\:mm\\:ss}" : "已取得影片資訊"; ShowImage(info.Thumbnail); UpdateQualities(); }
    void UrlChanged(object sender, TextChangedEventArgs e) { analysisVersion++; previewInfo = null; previewJobId = null; if (MetadataLabel is not null) MetadataLabel.Text = ""; if (QualityBox is not null && EstimateLabel is not null) UpdateQualities(); }
    void UpdateQualities()
    {
        var selected = QualityBox.SelectedValue is int q ? q : mode == DownloadMode.Mp3 ? engine.Settings.AudioKbps : engine.Settings.VideoHeight;
        var maxHeight = previewInfo?.Formats?.Where(f => f.Video && f.Height <= 2160).Select(f => f.Height ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        var qualities = mode == DownloadMode.Mp3 ? new[] {128,192,256,320} : new[] {720,1080,1440,2160,0}.Where(n => n == 0 || maxHeight == 0 || n <= maxHeight).ToArray();
        QualityBox.ItemsSource = qualities.Select(n => new QualityOption(n, mode == DownloadMode.Mp3 ? $"{n} kbps" : n == 0 ? "最佳（最高 4K）" : n == 2160 ? "2160p · 4K" : $"{n}p", SizeEstimator.Label(SizeEstimator.Estimate(previewInfo, mode, n)))).ToArray();
        QualityBox.SelectedValue = qualities.Contains(selected) ? selected : qualities.Last(); UpdateEstimate();
    }
    void QualityChanged(object sender, SelectionChangedEventArgs e) { if (EstimateLabel is not null) UpdateEstimate(); }
    void UpdateEstimate()
    {
        var q = QualityBox.SelectedValue is int n ? n : 0; var bytes = SizeEstimator.Estimate(previewInfo, mode, q);
        if (bytes is null) { EstimateLabel.Text = previewInfo is null ? "分析連結後顯示預估大小" : "來源未提供足夠資料，暫時無法估算"; return; }
        var plan = previewInfo is null ? null : VideoSelection.Select(previewInfo, q);
        var detail = mode == DownloadMode.Mp3 ? "按片長及輸出位元率計算，另加封面與標籤" : plan is null ? "實際大小或有差異" : $"{(plan.Video.Height is int h ? $"實際 {h}p · " : "")}{plan.Video.Codec} · 影音串流合計，封裝後略有差異";
        EstimateLabel.Text = $"預估檔案大小：{SizeEstimator.Label(bytes)}\n{detail}";
    }
    void ShowImage(string? url) { if (url == thumbnailUrl) return; thumbnailUrl = url; try { Thumbnail.Source = url is null ? null : new BitmapImage(new Uri(url)); } catch (Exception) { Thumbnail.Source = null; } }
    void SelectPreview(DownloadJob j) { UrlBox.Text = j.Url; previewJobId = j.Id; MetadataLabel.Text = j.MetadataStatus; ApplyPreview(mediaCache.TryGetValue(j.Url, out var info) ? info : new(j.Title, j.Artist, j.Album, j.Thumbnail, j.Duration)); }
    void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!refreshing) { recentSelection = false; if (JobGrid.SelectedItem is DownloadJob j) SelectPreview(j); } }
    void RecentSelected(object sender, SelectionChangedEventArgs e) { if (!refreshing && RecentGrid.SelectedItem is DownloadJob j) { recentSelection = true; SelectPreview(j); } }
    async void DownloadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = engine.Settings; p.SetDirectory(mode, DirectoryBox.Text);
            if (mode == DownloadMode.Mp3) p.AudioKbps = (int)QualityBox.SelectedValue; else p.VideoHeight = (int)QualityBox.SelectedValue; engine.SaveSettings(p);
            await engine.Accept(new(Guid.NewGuid().ToString("N"), UrlBox.Text.Trim(), mode == DownloadMode.Mp3 ? "mp3" : "mp4")); history = false; failures = false; tagsPage = false; recentSelection = false; Refresh(); StatusLabel.Text = "已加入任務";
        }
        catch (Exception ex) { StatusLabel.Text = ex.Message; }
    }
    void SetMode(DownloadMode value) { mode = value; DirectoryBox.Text = engine.Settings.DirectoryFor(value); DirectoryLabel.Text = value == DownloadMode.Mp3 ? "MP3 儲存位置" : "MP4 儲存位置"; QualityBox.SelectedValue = null; UpdateQualities(); Mp4Button.Background = value == DownloadMode.Mp4 ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromRgb(34, 47, 64)); Mp3Button.Background = value == DownloadMode.Mp3 ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromRgb(34, 47, 64)); }
    void Mp4Click(object s, RoutedEventArgs e) => SetMode(DownloadMode.Mp4); void Mp3Click(object s, RoutedEventArgs e) => SetMode(DownloadMode.Mp3);
    void BrowseClick(object s, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFolderDialog(); if (dialog.ShowDialog(this) == true) { var p = engine.Settings; p.SetDirectory(mode, dialog.FolderName); engine.SaveSettings(p); DirectoryBox.Text = p.DirectoryFor(mode); } }
    void NewClick(object s, RoutedEventArgs e) { history = false; failures = false; tagsPage = false; recentSelection = false; Refresh(); UrlBox.Focus(); }
    void QueueClick(object s, RoutedEventArgs e) { history = false; failures = false; tagsPage = false; recentSelection = false; Refresh(); }
    void HistoryClick(object s, RoutedEventArgs e) => OpenHistory();
    void SearchChanged(object s, TextChangedEventArgs e) { if (IsLoaded) Refresh(); }
    async void ClearClick(object s, RoutedEventArgs e)
    {
        var snapshot = store.Load(true).Where(j => j.State == JobState.Completed && !j.IsGroupRoot && MatchesLibrary(j)).Select(j => j.Id).ToArray();
        if (snapshot.Length == 0) return;
        if (await new ConfirmWindow(this, "清空歷史", $"即將清空當前篩選出的 {snapshot.Length} 筆歷史紀錄？\n實體檔案會保留。", "清空紀錄").Ask()) { store.ClearHistory(snapshot); Refresh(); }
    }
    public Action<string> RecycleFile { get; set; } = ShellFiles.Recycle;
    async void DeleteFileClick(object s, RoutedEventArgs e)
    {
        var selected = SelectedJobs(); if (!history || selected.Length != 1 || selected[0].FilePath is not string path) { StatusLabel.Text = "請在已下載選取一個檔案。"; return; }
        if (OwnedWindows.OfType<TagWindow>().Any()) { StatusLabel.Text = "請先完成或關閉標籤編輯，再刪除檔案。"; return; }
        var id = selected[0].Id;
        if (!await new ConfirmWindow(this, "刪除所選檔案", $"將「{Path.GetFileName(path)}」移到資源回收筒，並移除對應下載紀錄？\n其他檔案不受影響。", "刪除檔案").Ask()) return;
        try { LibraryActions.DeleteFile(store, id, path, RecycleFile); StatusLabel.Text = "所選檔案已移到資源回收筒"; Refresh(); }
        catch (Exception ex) { StatusLabel.Text = "未能刪除檔案：" + ex.Message; }
    }
    void FailedClick(object s, RoutedEventArgs e) { failures = !failures; recentSelection = false; Refresh(); }
    async void PauseClick(object s, RoutedEventArgs e) { foreach (var j in JobGrid.SelectedItems.Cast<DownloadJob>().ToArray()) await engine.Pause(j.Id); }
    void ResumeClick(object s, RoutedEventArgs e) { foreach (var j in JobGrid.SelectedItems.Cast<DownloadJob>().ToArray()) { if (j.State == JobState.PendingChoice) AskChoice(j); else engine.Resume(j.Id); } }
    async void CancelClick(object s, RoutedEventArgs e) => await engine.Cancel(JobGrid.SelectedItems.Cast<DownloadJob>().Select(j => j.Id).ToArray());
    public Action<string> RevealFile { get; set; } = ShellFiles.Select;
    DownloadJob[] SelectedJobs() => (recentSelection ? RecentGrid : JobGrid).SelectedItems.Cast<DownloadJob>().ToArray();
    bool MatchesLibrary(DownloadJob j) => HistorySearch.Matches(j, SearchBox.Text, tagsPage ? DownloadMode.Mp3 : FormatFilter.SelectedIndex == 1 ? DownloadMode.Mp3 : FormatFilter.SelectedIndex == 2 ? DownloadMode.Mp4 : null);
    void FilterChanged(object s, SelectionChangedEventArgs e) { if (IsLoaded) { recentSelection = false; Refresh(); } }
    void FolderClick(object s, RoutedEventArgs e)
    {
        try { var j = SelectedJobs().FirstOrDefault(); if (j?.FilePath is not string path) { StatusLabel.Text = "請先選取一個已下載檔案。"; return; } RevealFile(path); StatusLabel.Text = "已在檔案總管選取檔案"; }
        catch (Exception ex) { StatusLabel.Text = "無法開啟檔案位置：" + ex.Message; }
    }
    void ErrorClick(object s, RoutedEventArgs e)
    {
        if (JobGrid.SelectedItem is not DownloadJob j) return; var dialog = Dialogs.Basic(this, "下載診斷", 650, 450); var panel = new DockPanel { Margin = new Thickness(18) }; var export = new Button { Content = "匯出 .log" }; DockPanel.SetDock(export, Dock.Bottom); panel.Children.Add(export);
        var text = (j.Error ?? "此任務未記錄錯誤。") + "\n\n" + (j.Stderr ?? ""); panel.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        export.Click += (_, _) => { var save = new Microsoft.Win32.SaveFileDialog { Filter = "診斷日誌|*.log", FileName = "omni-diagnostic.log" }; if (save.ShowDialog(dialog) == true) { File.WriteAllText(save.FileName, ProcessRunner.Redact(text)); Notifications.Show("診斷日誌已匯出"); } }; dialog.Content = panel; dialog.Show();
    }
    void TagsClick(object s, RoutedEventArgs e)
    {
        history = true; tagsPage = true; recentSelection = false; SearchBox.Text = ""; Refresh();
        StatusLabel.Text = "選取一首或多首 MP3，再按「編輯所選標籤」；亦可雙擊曲目。";
    }
    void EditTagsClick(object s, RoutedEventArgs e)
    {
        try {
            var selected = SelectedJobs().Where(j => j.State == JobState.Completed && j.Mode == DownloadMode.Mp3).ToArray();
            if (selected.Length == 0) { StatusLabel.Text = "請先選取一首或多首 MP3。"; return; }
            if (selected.Any(j => !File.Exists(j.FilePath))) { StatusLabel.Text = "部分所選檔案已移動或刪除，請重新選取。"; return; }
            var dialog = new TagWindow(this, store, selected);
            dialog.Closed += (_, _) => { engine.ReloadEditedJobs(selected.Select(j => j.Id)); Refresh(); };
            dialog.Show();
        } catch (Exception ex) { StatusLabel.Text = "無法開啟標籤編輯：" + ex.Message; }
    }
    void FileDoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement((ItemsControl)s, e.OriginalSource as DependencyObject) is not DataGridRow) return;
        if (SelectedJobs().FirstOrDefault()?.Mode == DownloadMode.Mp3) EditTagsClick(s, e);
        else FolderClick(s, e);
    }
    void SettingsClick(object s, RoutedEventArgs e) { var dialog = new SettingsWindow(this, engine); dialog.Closed += (_, _) => SetMode(mode); dialog.Show(); }
    void CollapseClick(object s, RoutedEventArgs e) { collapsed = !collapsed; UpdateNavigation(); }
    void UpdateNavigation()
    {
        NavColumn.Width = new GridLength(collapsed ? 76 : 196); NavFooter.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        var buttons = new[] { NewNav, QueueNav, HistoryNav, TagsNav, SettingsNav, CollapseNav };
        var labels = new[] { "新增下載", "下載中", "已下載", "標籤編輯", "設定", "收合側欄" };
        var paths = new[] { "M12,3 L12,21 M3,12 L21,12", "M12,2 L12,17 M5,10 L12,17 L19,10 M3,18 L3,22 L21,22 L21,18", "M3,12 L9,18 L21,5", "M9,18 L9,5 L21,2 L21,15 M9,8 L21,5 M9,18 C9,22 2,22 2,19 C2,16 9,15 9,18 M21,15 C21,19 14,19 14,16 C14,13 21,12 21,15", "M3,5 L21,5 M3,12 L21,12 M3,19 L21,19 M8,2 L8,8 M16,9 L16,15 M10,16 L10,22", "M3,5 L21,5 M3,12 L21,12 M3,19 L21,19" };
        for (int i = 0; i < buttons.Length; i++) { var panel = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(paths[i]), Stroke = Foreground, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 22, Height = 22, Stretch = Stretch.Uniform }); if (!collapsed && i < 5) panel.Children.Add(new TextBlock { Text = labels[i], Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); buttons[i].Content = panel; buttons[i].Padding = new Thickness(collapsed ? 0 : 8, 10, collapsed ? 0 : 8, 10); System.Windows.Automation.AutomationProperties.SetName(buttons[i], labels[i]); }
    }
}
public static class NativeFocus
{
    [System.Runtime.InteropServices.DllImport("user32.dll")][return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] static extern bool SetForegroundWindow(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool FlashWindow(IntPtr h, bool invert);
    public static bool Foreground(IntPtr h) => SetForegroundWindow(h); public static void Flash(IntPtr h) => FlashWindow(h, true);
}
public static class Dialogs
{
    public static Window Basic(Window owner, string title, int width, int height) => new() { Owner = owner, Title = title, Width = width, Height = height, Background = owner.Background, Foreground = owner.Foreground, Resources = owner.Resources, WindowStartupLocation = WindowStartupLocation.CenterOwner };
}
public sealed class QualityLabelConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value is QualityOption q ? $"{q.Label}  ·  {q.Estimate}" : value is int n ? n == 0 ? "最佳" : n >= 720 ? $"{n}p" : n.ToString() : value;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
public sealed record QualityOption(int Value, string Label, string Estimate);
