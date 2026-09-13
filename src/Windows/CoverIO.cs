using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
namespace Omni.Windows;
public static class CoverIO
{
    static readonly SemaphoreSlim Serial = new(1);
    public static async Task Write(byte[] original, string path, Window owner)
    {
        await Serial.WaitAsync();
        try
        {
            byte[] jpeg;
            if (original.Length > 2 && original[0] == 255 && original[1] == 216) jpeg = original;
            else using (var source = new MemoryStream(original))
                { var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); var encoder = new JpegBitmapEncoder { QualityLevel = 95 }; encoder.Frames.Add(decoder.Frames[0]); using var output = new MemoryStream(); encoder.Save(output); jpeg = output.ToArray(); }
            while (true)
            {
                try { await Task.Run(() => { var temp = path + ".omni-" + Guid.NewGuid().ToString("N"); try { File.WriteAllBytes(temp, jpeg); File.Move(temp, path, true); var attrs = File.GetAttributes(path); if (attrs.HasFlag(FileAttributes.Hidden)) File.SetAttributes(path, attrs & ~FileAttributes.Hidden); } finally { if (File.Exists(temp)) File.Delete(temp); } }); return; }
                catch (IOException e) when ((e.HResult & 0xffff) is 32 or 33)
                {
                    if (!await Retry(owner)) return;
                }
            }
        }
        finally { Serial.Release(); }
    }
    static Task<bool> Retry(Window owner)
    {
        var tcs = new TaskCompletionSource<bool>(); var d = Dialogs.Basic(owner, "外部封面暫時無法寫入", 510, 260); var p = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) }; p.Children.Add(new System.Windows.Controls.TextBlock { Text = "外部封面圖檔已被其他程式開啟，請關閉佔用該檔案的程式以完成封面變更", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 15) });
        foreach (var (label, value) in new[] { ("重試", true), ("跳過", false) }) { var b = new System.Windows.Controls.Button { Content = label }; b.Click += (_, _) => { tcs.TrySetResult(value); d.Close(); }; p.Children.Add(b); }
        d.Closed += (_, _) => tcs.TrySetResult(false); d.Content = p; d.Show(); return tcs.Task;
    }
}
