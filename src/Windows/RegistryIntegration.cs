using Microsoft.Win32;
using System.IO;
using Omni.Core;
namespace Omni.Windows;
public static class RegistryIntegration
{
    public static void HealProtocol()
    {
        using var root = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ytdl"); root.SetValue("", "URL:全能影音下載器"); root.SetValue("URL Protocol", "");
        using var icon = root.CreateSubKey("DefaultIcon"); var expectedIcon = $"\"{Environment.ProcessPath}\",0";
        if (!string.Equals(icon.GetValue("") as string, expectedIcon, StringComparison.Ordinal)) icon.SetValue("", expectedIcon);
        using var command = root.CreateSubKey(@"shell\open\command"); var expected = $"\"{Environment.ProcessPath}\" --minimized \"%1\"";
        if (!string.Equals(command.GetValue("") as string, expected, StringComparison.Ordinal)) command.SetValue("", expected);
    }
    public static void HealNativeHost(string id)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-p]{32}$")) return;
        var path = Path.Combine(AppContext.BaseDirectory, "native-host.json");
        File.WriteAllText(path, Json.Encode(new { name = "com.omni.downloader", description = "全能影音下載器本機橋接", path = Path.Combine(AppContext.BaseDirectory, "Omni.NativeHost.exe"), type = "stdio", allowed_origins = new[] { "chrome-extension://" + id + "/" } }));
        foreach (var vendor in new[] { @"Google\Chrome", @"BraveSoftware\Brave-Browser", @"Chromium" })
        { using var key = Registry.CurrentUser.CreateSubKey(@"Software\" + vendor + @"\NativeMessagingHosts\com.omni.downloader"); key.SetValue("", path); }
    }
}
