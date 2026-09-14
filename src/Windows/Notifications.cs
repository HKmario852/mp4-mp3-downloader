using Microsoft.Toolkit.Uwp.Notifications;
namespace Omni.Windows;
public static class Notifications
{
    public static Func<Omni.Core.Preferences>? Preferences { get; set; }
    public static void Show(string text)
    {
        var p=Preferences?.Invoke();if(p is not null&&(!p.SystemNotifications||p.IsQuiet(DateTime.Now)))return;
        try { new ToastContentBuilder().AddArgument("page", "history").AddText("全能影音下載器").AddText(text.Length > 240 ? text[..240] : text).Show(); }
        catch (Exception) { /* Notification delivery must never fail a completed download. */ }
    }
}
