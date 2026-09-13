using System.Diagnostics;
using Omni.Core;
var destination = Path.GetFullPath(args[1]); Directory.CreateDirectory(destination);
var folder = Path.Combine(destination, "native-bridge-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
var host = Path.Combine(folder, "Omni.NativeHost.exe"); File.Copy(Path.GetFullPath(args[0]), host);
var origin = "chrome-extension://" + new string('a', 32) + "/";
File.WriteAllText(Path.Combine(folder, "native-host.json"), Json.Encode(new { allowed_origins = new[] { origin } }));
var store = new Store(Path.Combine(folder, "data")); await using var engine = new Downloader(store, folder, Path.Combine(folder, "work"));
using var stop = new CancellationTokenSource(); var server = Ipc.Listen(engine.Accept, stop.Token);
var request = new IntakeRequest(Guid.NewGuid().ToString(), "https://www.youtube.com/watch?v=synthetic&list=synthetic", "mp3", [new("SID", "SYNTHETIC_TEST_COOKIE", ".youtube.com", "/", true, true, false, null)]);
for (int i = 0; i < 2; i++)
{
    using var process = Process.Start(new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, ArgumentList = { origin } })!;
    using var timeout = new CancellationTokenSource(15000); await Ipc.Write(process.StandardInput.BaseStream, request, timeout.Token); process.StandardInput.Close(); var ack = await Ipc.Read<IntakeAck>(process.StandardOutput.BaseStream, timeout.Token); await process.WaitForExitAsync(timeout.Token);
    if (!ack.Ok || ack.RequestId != request.RequestId || ack.State != "PendingChoice") throw new Exception("Invalid acknowledgement: " + Json.Encode(ack));
}
if (engine.Jobs.Length != 1 || engine.Jobs[0].State != JobState.PendingChoice) throw new Exception("Deduplication/confirmation invariant failed");
if (Json.Encode(store.Load()).Contains("SYNTHETIC_TEST_COOKIE")) throw new Exception("Cookie leaked into history");
stop.Cancel(); try { await server; } catch (OperationCanceledException) { }
File.WriteAllText(Path.Combine(destination, "native-bridge.json"), Json.Encode(new { passed = true, checks = new[] { "actual native host stdio framing", "current-user Named Pipe", "PendingChoice ACK", "request-id deduplication", "no cookie in history" } }));
Console.WriteLine("Native host -> Named Pipe -> durable PendingChoice ACK passed; replay deduplicated and credentials absent from history.");
File.Delete(host);
