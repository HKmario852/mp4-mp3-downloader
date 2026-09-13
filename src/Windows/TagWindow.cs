using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Omni.Core;
using Validation = Omni.Core.Validation;
namespace Omni.Windows;
public sealed class TagWindow : Window
{
    public TagWindow(Window owner, Store store, DownloadJob[] jobs)
    {
        Owner = owner; Title = $"MP3 標籤編輯 · {jobs.Length} 首"; Width = 630; Height = 760; Background = owner.Background; Foreground = owner.Foreground; Resources = owner.Resources; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = new ScrollViewer { Content = panel }; var delta = new Dictionary<string, string>(); var doc = Id3Document.Read(jobs[0].FilePath!); byte[]? cover = null;
        panel.Children.Add(new TextBlock { Text = $"編輯 {jobs.Length} 首音訊", FontSize = 24 }); panel.Children.Add(new TextBlock { Text = "只儲存有改動的欄位。清空會保留空標籤；音軌編號不會遞增。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 14) });
        var error = new TextBlock { Name = "TitleError", Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap }; var save = new Button { Name = "SaveTags", Content = "儲存", Background = (Brush)FindResource("Accent") };
        bool saving = false;
        Closing += (_, e) => { if (saving) e.Cancel = true; };
        foreach (var (id, label) in new[] { ("TIT2", "Title · 歌曲名"), ("TPE1", "Artist · 歌手"), ("TALB", "Album · 專輯"), ("TRCK", "Track · 固定音軌編號"), ("TCON", "Genre · 類型"), ("TYER", "Year · 年份") })
        {
            panel.Children.Add(new TextBlock { Text = label }); var original = jobs.Length == 1 ? doc.Text(id) : ""; var field = new TextBox { Name = id, Text = original }; panel.Children.Add(field);
            field.TextChanged += (_, _) => { if (jobs.Length == 1 && field.Text == original) delta.Remove(id); else delta[id] = field.Text; var invalid = delta.TryGetValue("TIT2", out var title) ? Validation.TitleError(title) : null; error.Text = invalid ?? ""; save.IsEnabled = !saving && invalid is null; }; if (id == "TIT2") panel.Children.Add(error);
        }
        var advanced = new Expander { Header = "進階：任意 ID3 Frame（Base64 JSON）", Margin = new Thickness(0, 14, 0, 10) }; var raw = new TextBox { Text = "{}", AcceptsReturn = true, Height = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; advanced.Content = raw; panel.Children.Add(advanced);
        var paste = new Button { Content = "從剪貼簿貼上封面" }; paste.Click += (_, _) =>
        {
            try { if (System.Windows.Clipboard.ContainsFileDropList()) { var file = System.Windows.Clipboard.GetFileDropList().Cast<string>().FirstOrDefault(File.Exists); if (file is not null) cover = File.ReadAllBytes(file); } else if (System.Windows.Clipboard.ContainsImage()) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(System.Windows.Clipboard.GetImage())); using var output = new MemoryStream(); encoder.Save(output); cover = output.ToArray(); } if (cover is not null) paste.Content = "✓ 封面已選取（儲存後套用）"; } catch (Exception e) { error.Text = e.Message; }
        }; panel.Children.Add(paste); panel.Children.Add(save);
        save.Click += async (_, _) =>
        {
            try
            {
                var rawFields = Json.Decode<Dictionary<string, string>>(raw.Text); if (rawFields.Keys.Any(k => k.Split('#')[0] == "TIT2")) throw new ArgumentException("請在 Title 欄位修改歌曲名，才能驗證檔名");
                var covers = cover is null ? null : new List<Cover> { new(cover, cover.Length > 2 && cover[0] == 255 ? "image/jpeg" : "image/png", "User cover", 3) };
                saving = true; save.IsEnabled = false; panel.IsEnabled = false; var changes = new Dictionary<string, string>(delta); await Task.Run(() => new TagEditor(store).Apply(jobs, new(changes, rawFields, covers)));
                if (cover is not null) foreach (var dir in jobs.Select(j => Path.GetDirectoryName(j.FilePath!)!).Distinct(StringComparer.OrdinalIgnoreCase)) await CoverIO.Write(cover, Path.Combine(dir, "cover.jpg"), this);
                saving = false; Close();
            }
            catch (Exception e) { error.Text = e.Message; }
            finally { saving = false; panel.IsEnabled = true; save.IsEnabled = !delta.TryGetValue("TIT2", out var title) || Validation.TitleError(title) is null; }
        };
    }
}
