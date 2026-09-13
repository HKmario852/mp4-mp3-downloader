using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Principal;
using System.Diagnostics;
namespace Omni.Core;
public static class Ipc
{
    public const int MaxBytes = 262144;
    public static string Name => OperatingSystem.IsWindows() ? "Omni.Downloader." + WindowsIdentity.GetCurrent().User!.Value + "." + Process.GetCurrentProcess().SessionId : throw new PlatformNotSupportedException();
    public static async Task<T> Read<T>(Stream stream, CancellationToken ct)
    {
        byte[] prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, ct); int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is <= 0 or > MaxBytes) throw new InvalidDataException("訊息大小無效");
        byte[] data = new byte[length]; await stream.ReadExactlyAsync(data, ct); return Json.Decode<T>(System.Text.Encoding.UTF8.GetString(data));
    }
    public static async Task Write<T>(Stream stream, T message, CancellationToken ct)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(Json.Encode(message)); if (data.Length > MaxBytes) throw new InvalidDataException("訊息過大");
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, data.Length); await stream.WriteAsync(prefix, ct); await stream.WriteAsync(data, ct); await stream.FlushAsync(ct);
    }
    public static async Task<IntakeAck> Send(IntakeRequest request, int timeoutMs = 15000)
    {
        using var ct = new CancellationTokenSource(timeoutMs);
        using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(ct.Token); await Write(pipe, request, ct.Token); return await Read<IntakeAck>(pipe, ct.Token);
    }
    public static async Task Listen(Func<IntakeRequest, Task<IntakeAck>> accept, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(ct);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(10000);
            try { var request = await Read<IntakeRequest>(pipe, limit.Token); var ack = await accept(request); await Write(pipe, ack, limit.Token); }
            catch (Exception e) when (e is IOException or ArgumentException or System.Text.Json.JsonException or OperationCanceledException) { /* Untrusted malformed client; never log payload. */ }
        }
    }
}
