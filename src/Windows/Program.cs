using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Omni.Core;
namespace Omni.Windows;
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var singleton = new Mutex(true, "Local\\" + Ipc.Name, out bool primary);
        var protocol = args.FirstOrDefault(a => a.StartsWith("ytdl://", StringComparison.OrdinalIgnoreCase));
        if (!primary)
        {
            try { Ipc.Send(protocol is not null ? Validation.ParseProtocol(protocol) : new IntakeRequest(Guid.NewGuid().ToString(), "", ToastNotificationManagerCompat.WasCurrentProcessToastActivated() ? "history" : "open")).GetAwaiter().GetResult(); } catch (Exception e) { System.Windows.MessageBox.Show(e.Message, "全能影音下載器"); }
            return;
        }
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var data = Path.Combine(AppContext.BaseDirectory, "data");
        try { Directory.CreateDirectory(data); } catch (UnauthorizedAccessException) { data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDownloader"); }
        RegistryIntegration.HealProtocol();
        var store = new Store(data); var engine = new Downloader(store, AppContext.BaseDirectory, Path.Combine(data, "work"));
        RegistryIntegration.HealNativeHost(engine.Settings.ExtensionId);
        var window = new MainWindow(engine, store); app.MainWindow = window;
        ToastNotificationManagerCompat.OnActivated += _ => app.Dispatcher.BeginInvoke(() => window.OpenHistory());
        using var trayIconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/App;component/Assets/omni.ico"))!.Stream;
        using var trayIcon = new System.Drawing.Icon(trayIconStream, 32, 32);
        var tray = new System.Windows.Forms.NotifyIcon { Text = "全能影音下載器", Icon = trayIcon, Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip(); menu.Items.Add("開啟", null, (_, _) => window.Reveal()); menu.Items.Add("已下載", null, (_, _) => window.OpenHistory());
        menu.Items.Add("結束", null, async (_, _) => { window.AllowClose = true; await engine.DisposeAsync(); tray.Dispose(); app.Shutdown(); }); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => window.Reveal();
        engine.Notify += text => app.Dispatcher.BeginInvoke(() => Notifications.Show(text));
        engine.ChoiceRequested += job => app.Dispatcher.BeginInvoke(() => window.AskChoice(job));
        using var ipcCt = new CancellationTokenSource(); var listener = Ipc.Listen(request => { if (request.Mode is ("open" or "history") && request.Url == "" && request.Cookies is null) { app.Dispatcher.BeginInvoke(() => { if (request.Mode == "history") window.OpenHistory(); else window.Reveal(); }); return Task.FromResult(new IntakeAck(true, request.RequestId, "Opened")); } return engine.Accept(request); }, ipcCt.Token);
        bool shuttingDown = false;
        UpdateManager.ExitForUpdate = async () => {
            if (shuttingDown) return; shuttingDown = true; ipcCt.Cancel();
            window.AllowClose = true;
            await engine.DisposeAsync(); tray.Dispose(); app.Shutdown();
        };
        app.Startup += async (_, _) =>
        {
            if (!args.Contains("--minimized")) window.Show();
            if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated()) window.OpenHistory();
            if (protocol is not null) try { await engine.Accept(Validation.ParseProtocol(protocol)); } catch (Exception e) { System.Windows.MessageBox.Show(e.Message, "無法接收下載"); }
            foreach (var pending in engine.Jobs.Where(j => j.State == JobState.PendingChoice)) window.AskChoice(pending);
            await UpdateManager.Check(engine.Settings.ReleaseRepository);
        };
        app.DispatcherUnhandledException += (_, e) => { System.Windows.MessageBox.Show(e.Exception.Message, "操作未完成"); e.Handled = true; };
        app.Run(); ipcCt.Cancel(); tray.Dispose(); singleton.ReleaseMutex();
    }
}
