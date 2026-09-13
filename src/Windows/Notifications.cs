using Microsoft.Toolkit.Uwp.Notifications;
namespace Omni.Windows;
public static class Notifications
{
    public static void Show(string text)
    {
        try { new ToastContentBuilder().AddArgument("page", "history").AddText("全能影音下載器").AddText(text.Length > 240 ? text[..240] : text).Show(); }
        catch (Exception) { /* Notification delivery must never fail a completed download. */ }
    }
}
