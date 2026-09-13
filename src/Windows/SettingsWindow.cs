using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Omni.Core;
namespace Omni.Windows;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(Window owner, Downloader engine)
    {
        Owner = owner; Title = "偏好設定"; Width = 570; Height = 720; Background = owner.Background; Foreground = owner.Foreground; Resources = owner.Resources; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = new ScrollViewer { Content = panel }; var p = Json.Decode<Preferences>(Json.Encode(engine.Settings));
        panel.Children.Add(new TextBlock { Text = "偏好設定", FontSize = 25 });
        var clean = new CheckBox { Content = "自動清洗音樂標題後綴", IsChecked = p.CleanTitle }; var mb = new CheckBox { Content = "MusicBrainz 查詢（會傳送歌曲名及歌手）", IsChecked = p.MusicBrainz }; panel.Children.Add(clean); panel.Children.Add(mb);
        panel.Children.Add(new TextBlock { Text = "同時下載數量（1–5）" }); var count = new ComboBox { ItemsSource = new[] { 1, 2, 3, 4, 5 }, SelectedItem = p.Concurrency }; panel.Children.Add(count);
        TextBox DirectoryField(string label, DownloadMode mode)
        {
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 12, 0, 0) });
            var row = new DockPanel(); var browse = new Button { Content = "瀏覽", Padding = new Thickness(8) }; DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
            var field = new TextBox { Text = p.DirectoryFor(mode), IsReadOnly = true, TextWrapping = TextWrapping.Wrap }; row.Children.Add(field); panel.Children.Add(row);
            browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog(); if (picker.ShowDialog(this) == true) field.Text = picker.FolderName; }; return field;
        }
        var videoDirectory = DirectoryField("MP4 儲存位置", DownloadMode.Mp4);
        var audioDirectory = DirectoryField("MP3 儲存位置", DownloadMode.Mp3);
        panel.Children.Add(new TextBlock { Text = "瀏覽器擴充功能 ID" }); var extension = new TextBox { Text = p.ExtensionId }; panel.Children.Add(extension); var link = new Button { Content = "儲存並連接瀏覽器" }; panel.Children.Add(link);
        panel.Children.Add(new TextBlock { Text = "GitHub 專案（owner/repository）", Margin = new Thickness(0, 18, 0, 0) }); var repo = new TextBox { Text = p.ReleaseRepository }; panel.Children.Add(repo);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) }; panel.Children.Add(status);
        void Save() { p.CleanTitle = clean.IsChecked == true; p.MusicBrainz = mb.IsChecked == true; p.Concurrency = (int)count.SelectedItem; p.ExtensionId = extension.Text.Trim(); p.ReleaseRepository = repo.Text.Trim(); p.Mp4Directory = videoDirectory.Text; p.Mp3Directory = audioDirectory.Text; engine.SaveSettings(p); }
        link.Click += (_, _) => { try { if (!System.Text.RegularExpressions.Regex.IsMatch(extension.Text.Trim(), "^[a-p]{32}$")) throw new ArgumentException("請輸入擴充功能設定顯示的 32 字元 ID"); Save(); RegistryIntegration.HealNativeHost(p.ExtensionId); status.Text = "已連接。請在擴充功能設定同意本機 Cookie 傳送。"; } catch (Exception e) { status.Text = e.Message; } };
        var save = new Button { Content = "儲存設定", Background = (System.Windows.Media.Brush)FindResource("Accent") }; save.Click += (_, _) => { try { Save(); status.Text = "已儲存"; } catch (Exception e) { status.Text = e.Message; } }; panel.Children.Add(save);
        var releases = new Button { Content = "開啟 GitHub Releases" }; releases.Click += (_, _) => { if (System.Text.RegularExpressions.Regex.IsMatch(repo.Text, @"^[\w.-]+/[\w.-]+$")) Process.Start(new ProcessStartInfo($"https://github.com/{repo.Text}/releases") { UseShellExecute = true }); else status.Text = "請先填寫 GitHub 專案"; }; panel.Children.Add(releases);
        panel.Children.Add(new TextBlock { Text = "更新 ZIP 的 SHA256（由可信發布頁取得）", Margin = new Thickness(0, 14, 0, 0) }); var sha = new TextBox(); panel.Children.Add(sha); var elevated = new CheckBox { Content = "以管理員權限更新（僅限目錄需要時）" }; panel.Children.Add(elevated);
        var update = new Button { Content = "選擇已下載更新 ZIP" }; update.Click += (_, _) => { try { Save(); if (!System.Text.RegularExpressions.Regex.IsMatch(repo.Text, @"^[\w.-]+/[\w.-]+$")) throw new ArgumentException("請先設定 GitHub 專案"); var open = new Microsoft.Win32.OpenFileDialog { Filter = "更新 ZIP|*.zip" }; if (open.ShowDialog(this) == true) { UpdateManager.Launch(open.FileName, sha.Text.Trim(), $"https://github.com/{repo.Text}/releases", elevated.IsChecked == true); status.Text = "更新器已啟動；請從系統匣結束 App，更新器會等待所有下載程序退出。"; } } catch (Exception e) { status.Text = e.Message; } }; panel.Children.Add(update);
        var download = new Button { Content = "下載並安裝最新版本" }; download.Click += async (_, _) => { try { Save(); download.IsEnabled = false; status.Text = "正在下載並驗證更新包…"; await UpdateManager.DownloadAndLaunch(p.ReleaseRepository, elevated.IsChecked == true); status.Text = "已驗證更新包。請從系統匣結束 App，更新器會繼續處理。"; } catch (Exception e) { status.Text = e.Message; } finally { download.IsEnabled = true; } }; panel.Children.Add(download);
    }
}
