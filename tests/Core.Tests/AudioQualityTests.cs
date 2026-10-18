using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class AudioQualityTests
{
    // Flat spectrum made of many tones up to [top] Hz with random phases: a brick-wall source with nothing above.
    static float[] Tones(double top, int rate = 44100, double seconds = 8)
    {
        var random = new Random(7); var freqs = new List<(double f, double phase)>();
        for (double f = 50; f <= top; f += 50) freqs.Add((f, random.NextDouble() * 2 * Math.PI));
        var samples = new float[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++) { double t = (double)i / rate, v = 0; foreach (var (f, p) in freqs) v += Math.Sin(2 * Math.PI * f * t + p); samples[i] = (float)(v * 0.002); }
        return samples;
    }

    [Fact]
    public void FftFindsASineInItsBin()
    {
        var re = new double[4096]; var im = new double[4096];
        for (int i = 0; i < re.Length; i++) re[i] = Math.Sin(2 * Math.PI * 100 * i / 4096);
        AudioQuality.Fft(re, im);
        var peak = Enumerable.Range(0, 2048).MaxBy(i => re[i] * re[i] + im[i] * im[i]);
        Assert.Equal(100, peak);
    }

    [Theory]
    [InlineData(16000, AudioQuality.Grade.Low)]
    [InlineData(18500, AudioQuality.Grade.Medium)]
    [InlineData(20000, AudioQuality.Grade.High)]
    public void FindsTheCutoffOfABandLimitedSource(double top, AudioQuality.Grade grade)
    {
        var cutoff = AudioQuality.Cutoff(AudioQuality.Spectrum(Tones(top)), 44100);
        Assert.NotNull(cutoff);
        Assert.InRange(cutoff!.Value, top - 300, top + 1100);
        Assert.Equal(grade, AudioQuality.GradeOf(cutoff));
    }

    [Fact]
    public void FullBandAndSilence()
    {
        Assert.Equal(AudioQuality.Grade.FullBand, AudioQuality.GradeOf(AudioQuality.Cutoff(AudioQuality.Spectrum(Tones(21900)), 44100)));
        Assert.Equal(AudioQuality.Grade.Unknown, AudioQuality.GradeOf(AudioQuality.Cutoff(AudioQuality.Spectrum(new float[44100 * 5]), 44100)));
    }

    [Fact]
    public void FlagsFakeLosslessAndUpscaledFiles()
    {
        var low = AudioQuality.Spectrum(Tones(16000));
        Assert.True(AudioQuality.Judge(low, 44100, "flac", 900_000, 16).Suspicious);
        Assert.Contains("假無損", AudioQuality.Judge(low, 44100, "flac", 900_000, 16).Verdict);
        Assert.Contains("升頻", AudioQuality.Judge(low, 44100, "mp3", 320_000, null).Verdict);
        Assert.False(AudioQuality.Judge(low, 44100, "mp3", 128_000, null).Suspicious); // an honest 128 kbps file
        var full = AudioQuality.Spectrum(Tones(21900));
        Assert.False(AudioQuality.Judge(full, 44100, "flac", 900_000, 16).Suspicious);
    }

    [Fact]
    public void ALowSampleRateIsLowQuality()
    {
        var r = AudioQuality.Judge(AudioQuality.Spectrum(Tones(10000, 22050)), 22050, "flac", 700_000, 16);
        Assert.Equal(AudioQuality.Grade.Low, r.Grade); Assert.True(r.Suspicious);
        Assert.Contains("取樣率只有 22.05 kHz", r.Verdict); Assert.Equal(11025, r.CutoffHz);
    }

    // End to end with the bundled ffmpeg (skipped when the tools have not been prepared, as in CI).
    static string? Ffmpeg()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "windows-win-x64", "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    [Fact]
    public async Task JudgesRealEncodes()
    {
        if (Ffmpeg() is not { } ffmpeg) return;
        var dir = Directory.CreateTempSubdirectory("omni-quality-").FullName;
        try
        {
            async Task<string> Make(string name, params string[] args) { var path = Path.Combine(dir, name); await ProcessRunner.Run(ffmpeg, ["-v", "error", "-y", .. args, path], null, default); return path; }
            var noise = await Make("noise.wav", "-f", "lavfi", "-i", "anoisesrc=color=white:sample_rate=44100:amplitude=0.3:duration=8", "-c:a", "pcm_s16le");
            // A low-quality source cut at 16 kHz, like YouTube's 128 kbps AAC or older 128 kbps MP3 encoders
            // (ffmpeg's own LAME keeps up to ~20 kHz even at 128 kbps, so the cut is set explicitly)
            var mp3Low = await Make("low.mp3", "-i", noise, "-c:a", "libmp3lame", "-b:a", "128k", "-cutoff", "16000");
            var fakeFlac = await Make("fake.flac", "-i", mp3Low, "-c:a", "flac");
            var upscaled = await Make("up.mp3", "-i", mp3Low, "-c:a", "libmp3lame", "-b:a", "320k");
            var fakeFromGood = await Make("fake-good.flac", "-i", await Make("good.mp3", "-i", noise, "-c:a", "libmp3lame", "-b:a", "128k"), "-c:a", "flac");
            var real320 = await Make("real.mp3", "-i", noise, "-c:a", "libmp3lame", "-b:a", "320k");
            var realFlac = await Make("real.flac", "-i", noise, "-c:a", "flac");
            var silence = await Make("silence.flac", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono", "-t", "5", "-c:a", "flac");

            var full = await AudioQuality.Analyze(realFlac, ffmpeg, default);
            Assert.Equal(AudioQuality.Grade.FullBand, full.Grade); Assert.False(full.Suspicious);
            var honest = await AudioQuality.Analyze(mp3Low, ffmpeg, default);
            Assert.Equal(AudioQuality.Grade.Low, honest.Grade); Assert.False(honest.Suspicious);
            var fake = await AudioQuality.Analyze(fakeFlac, ffmpeg, default);
            Assert.True(fake.Suspicious); Assert.Contains("假無損", fake.Verdict);
            var up = await AudioQuality.Analyze(upscaled, ffmpeg, default);
            Assert.True(up.Suspicious); Assert.Contains("升頻", up.Verdict);
            Assert.True((await AudioQuality.Analyze(fakeFromGood, ffmpeg, default)).Suspicious); // even a ~20 kHz lossy source is not lossless
            var good = await AudioQuality.Analyze(real320, ffmpeg, default);
            Assert.False(good.Suspicious); Assert.True(good.Grade is AudioQuality.Grade.High or AudioQuality.Grade.FullBand);
            Assert.Equal(AudioQuality.Grade.Unknown, (await AudioQuality.Analyze(silence, ffmpeg, default)).Grade);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ParsesTheSourceTrack()
    {
        var s = AudioPipeline.ParseSource("251|opus|106.064|48000\n");
        Assert.Equal(new AudioPipeline.SourceAudio("opus", 106.064, 48000, "251"), s);
        Assert.Equal("Opus · 106 kbps · 48 kHz", AudioPipeline.SourceText(s!.Codec, s.Kbps, s.SampleRate));
        Assert.Equal("AAC · 129 kbps · 44.1 kHz", AudioPipeline.SourceText("mp4a.40.2", 129.4, 44100));
        Assert.Equal("MP3", AudioPipeline.SourceText("mp3", null, null));
        Assert.Null(AudioPipeline.ParseSource("18|NA|NA|NA"));
        Assert.Null(AudioPipeline.ParseSource("garbage"));
    }

    [Fact]
    public void NotesWhenTheOutputBitrateExceedsTheSource()
    {
        var cbr = new Preferences { Mp3Encoding = "cbr" };
        Assert.Contains("音質以來源為準", AudioPipeline.UpscaleNote("mp3", cbr, 320, 106));
        Assert.Null(AudioPipeline.UpscaleNote("mp3", cbr, 128, 129));
        Assert.Null(AudioPipeline.UpscaleNote("mp3", new Preferences { Mp3Encoding = "v0" }, 320, 106));
        Assert.Null(AudioPipeline.UpscaleNote("opus", cbr, 320, 106));
        Assert.Null(AudioPipeline.UpscaleNote("mp3", cbr, 320, null));
    }
}
