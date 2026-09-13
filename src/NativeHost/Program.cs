using System.Diagnostics;
using System.Text.Json;
using Omni.Core;

try
{
    var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "native-host.json")));
    var allowed = config.RootElement.GetProperty("allowed_origins").EnumerateArray().Select(x => x.GetString()).ToArray();
    if (args.Length == 0 || !allowed.Contains(args[0])) return;
    using var timeout = new CancellationTokenSource(18000);
    var request = await Ipc.Read<IntakeRequest>(Console.OpenStandardInput(), timeout.Token); Validation.Request(request);
    IntakeAck ack;
    try { ack = await Ipc.Send(request, 400); }
    catch (Exception e) when (e is TimeoutException or OperationCanceledException or IOException)
    {
        Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "App.exe")) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--minimized" } });
        ack = await Ipc.Send(request, 15000);
    }
    await Ipc.Write(Console.OpenStandardOutput(), ack, timeout.Token);
}
catch (Exception)
{
    try { await Ipc.Write(Console.OpenStandardOutput(), new IntakeAck(false, "", "Error", "無法連接本機 App，請手動啟動並檢查瀏覽器連接設定"), CancellationToken.None); } catch (IOException) { }
}
