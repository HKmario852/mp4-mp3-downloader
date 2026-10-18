using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
namespace Omni.Core;

/// <summary>
/// Estimates how much of the frequency range an audio file really contains. Lossy encoders remove the top of the
/// spectrum with a steep low-pass (about 16–17 kHz for 128 kbps, 20 kHz for Opus or 320 kbps MP3), and converting
/// to a bigger bitrate or to FLAC cannot bring it back. So a FLAC or "320 kbps" MP3 whose spectrum stops early was
/// made from a weaker source. This is an estimate: some recordings simply have no high frequencies.
/// </summary>
public static class AudioQuality
{
    public enum Grade { Unknown, Low, Medium, High, FullBand }
    public sealed record Result(Grade Grade, double? CutoffHz, string Codec, int SampleRate, long? BitRate, int? BitDepth, bool Suspicious, string Verdict);

    const int Size = 4096;
    const double MaxSeconds = 120;
    static readonly HashSet<string> LosslessCodecs = ["flac", "alac", "wavpack", "ape", "tta"];
    static bool IsLossless(string codec) => LosslessCodecs.Contains(codec) || codec.StartsWith("pcm_", StringComparison.Ordinal);

    /// <summary>Decodes up to two minutes with ffmpeg and judges the spectrum.</summary>
    public static async Task<Result> Analyze(string file, string ffmpeg, CancellationToken ct)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        var json = await ProcessRunner.Run(probe, ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=codec_name,sample_rate,bit_rate,bits_per_raw_sample,bits_per_sample:format=bit_rate", "-of", "json", file], null, ct);
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidDataException("找不到音訊軌 / No audio stream");
        var s = streams[0];
        string Prop(JsonElement e, string name) => !e.TryGetProperty(name, out var v) ? "" : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? "";
        var codec = Prop(s, "codec_name");
        var rate = int.TryParse(Prop(s, "sample_rate"), out var r) ? r : 0;
        long? bitRate = long.TryParse(Prop(s, "bit_rate"), out var b) ? b : long.TryParse(Prop(doc.RootElement.GetProperty("format"), "bit_rate"), out var fb) ? fb : null;
        int? depth = int.TryParse(Prop(s, "bits_per_raw_sample"), out var d) && d > 0 ? d : int.TryParse(Prop(s, "bits_per_sample"), out var d2) && d2 > 0 ? d2 : null;
        if (rate <= 0) throw new InvalidDataException("無法讀取取樣率 / Unknown sample rate");
        var samples = await DecodeMono(ffmpeg, file, ct);
        return Judge(Spectrum(samples), rate, codec, bitRate, depth);
    }

    static async Task<float[]> DecodeMono(string ffmpeg, string file, CancellationToken ct)
    {
        var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in new[] { "-v", "error", "-nostdin", "-i", file, "-map", "0:a:0", "-t", MaxSeconds.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-f", "f32le", "-" }) start.ArgumentList.Add(a);
        using var p = new Process { StartInfo = start };
        if (!p.Start()) throw new IOException("無法啟動 ffmpeg");
        using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { } });
        var errors = p.StandardError.ReadToEndAsync(CancellationToken.None);
        using var buffer = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(buffer, CancellationToken.None);
        await p.WaitForExitAsync(CancellationToken.None); ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0) throw new IOException("音訊解碼失敗 / Could not decode audio: " + (await errors).Trim());
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var samples = new float[bytes.Length / 4];
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(bytes[..(samples.Length * 4)]).CopyTo(samples);
        return samples;
    }

    /// <summary>Average power per FFT bin (Hann window, 4096 points, no overlap), in dB. Index i is i × rate / 4096 Hz.</summary>
    public static double[] Spectrum(ReadOnlySpan<float> samples)
    {
        var power = new double[Size / 2];
        var re = new double[Size]; var im = new double[Size];
        int windows = 0;
        for (int start = 0; start + Size <= samples.Length; start += Size)
        {
            for (int i = 0; i < Size; i++) { re[i] = samples[start + i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1))); im[i] = 0; }
            Fft(re, im);
            for (int i = 0; i < Size / 2; i++) power[i] += re[i] * re[i] + im[i] * im[i];
            windows++;
        }
        var db = new double[Size / 2];
        for (int i = 0; i < db.Length; i++) db[i] = windows == 0 ? -200 : 10 * Math.Log10(power[i] / windows + 1e-20);
        return db;
    }

    /// <summary>In-place iterative radix-2 FFT; the length must be a power of two.</summary>
    public static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len, wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                    (cr, ci) = (cr * wr - ci * wi, cr * wi + ci * wr);
                }
            }
        }
    }

    /// <summary>
    /// The frequency where the spectrum falls off a cliff: the 1 kHz below is at least 25 dB louder than every 500 Hz
    /// band above it. Null when there is no such cliff (full band); NaN when the audio is silent or too quiet.
    /// </summary>
    public static double? Cutoff(double[] db, int sampleRate)
    {
        double binHz = (double)sampleRate / Size, nyquist = sampleRate / 2.0;
        double Level(double from, double to)
        {
            int a = Math.Clamp((int)(from / binHz), 1, db.Length - 1), b = Math.Clamp((int)(to / binHz), a + 1, db.Length);
            double sum = 0; for (int i = a; i < b; i++) sum += Math.Pow(10, db[i] / 10);
            return 10 * Math.Log10(sum / (b - a) + 1e-20);
        }
        double reference = Level(1000, 4000);
        if (reference < -120) return double.NaN;
        for (double f = 12000; f <= nyquist - 1200; f += 100)
        {
            double below = Level(f - 1000, f);
            if (below < reference - 70) break; // already in the noise floor: no cliff to find
            double highest = double.NegativeInfinity;
            for (double g = f; g + 500 <= nyquist - 200; g += 500) highest = Math.Max(highest, Level(g, g + 500));
            if (below - highest >= 25) return f;
        }
        return null;
    }

    public static Grade GradeOf(double? cutoff) => cutoff switch
    {
        null => Grade.FullBand,
        double c when double.IsNaN(c) => Grade.Unknown,
        < 17500 => Grade.Low,
        < 19200 => Grade.Medium,
        < 20800 => Grade.High,
        _ => Grade.FullBand
    };

    public static Result Judge(double[] db, int sampleRate, string codec, long? bitRate, int? bitDepth)
    {
        var cutoff = Cutoff(db, sampleRate);
        // No cliff below Nyquist, but a low sample rate is itself the limit (22.05 kHz holds nothing above 11 kHz)
        if (cutoff is null && sampleRate / 2.0 < 20800) cutoff = sampleRate / 2.0;
        bool lowRate = sampleRate < 32000;
        var grade = GradeOf(cutoff);
        bool lossless = IsLossless(codec);
        bool fakeHigh = codec == "mp3" && bitRate >= 256_000 && grade is Grade.Low or Grade.Medium;
        bool fakeLossless = lossless && grade is Grade.Low or Grade.Medium or Grade.High;
        string band = grade switch
        {
            Grade.Low => "約 128 kbps 或以下嘅有損音源 / about 128 kbps or lower lossy source",
            Grade.Medium => "約 160–192 kbps 嘅有損音源 / about 160–192 kbps lossy source",
            Grade.High => "約 224–320 kbps 或 Opus 等級 / about 224–320 kbps or Opus quality",
            Grade.FullBand => "頻寬完整 / full bandwidth",
            _ => "音量太低，無法判斷 / too quiet to judge"
        };
        string rate = (sampleRate / 1000.0).ToString("0.##", CultureInfo.InvariantCulture), top = (sampleRate / 2000.0).ToString("0.#", CultureInfo.InvariantCulture);
        string verdict = lowRate && grade is not Grade.Unknown ? $"取樣率只有 {rate} kHz，最高頻率約 {top} kHz，屬低音質 / Sample rate is only {rate} kHz, so nothing above about {top} kHz: low quality"
            : fakeLossless ? "疑似假無損：由有損音源轉出（" + band.Split(" / ")[0] + "） / Likely fake lossless: made from a lossy source (" + band.Split(" / ")[1] + ")"
            : fakeHigh ? "疑似升頻：檔案標示高位元率，但頻譜似" + band.Split(" / ")[0] + " / Likely upscaled: high bitrate label, but the spectrum looks like " + band.Split(" / ")[1]
            : band;
        return new(grade, cutoff is double c && !double.IsNaN(c) ? c : null, codec, sampleRate, bitRate, bitDepth, fakeLossless || fakeHigh || (lowRate && grade is not Grade.Unknown), verdict);
    }
}
