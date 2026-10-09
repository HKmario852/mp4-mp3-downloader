using System.Globalization;
using System.Text.Json;
namespace Omni.Core;

public static class AudioPipeline
{
    public static bool Supported(string extension) => extension.TrimStart('.').ToLowerInvariant() is "mp3" or "opus" or "m4a" or "flac";
    public static string Selector(string format) => format switch {
        "opus" => "bestaudio[ext=webm][acodec=opus]",
        "m4a" => "bestaudio[ext=m4a][acodec^=mp4a.40]/bestaudio[acodec=aac]",
        "mp3" => "bestaudio[acodec=mp3]/bestaudio/best",
        "flac" or "wav" => "bestaudio/best",
        _ => throw new ArgumentException("Unsupported audio format: " + format)
    };
    public static List<string> Arguments(string format, Preferences p, int kbps)
    {
        var a = new List<string> { "-f", Selector(format), "-x", "--audio-format", format };
        if (format is "opus" or "m4a") a.AddRange(["--postprocessor-args", "ExtractAudio+ffmpeg_o:-c:a copy"]);
        if (format == "mp3") {
            a.AddRange(["--audio-quality", p.Mp3Encoding == "v0" ? "0" : $"{kbps}k"]);
            // yt-dlp uses libmp3lame only when the source is not already MP3.
        }
        if(format=="mp3")a.AddRange(["--postprocessor-args", $"Metadata+ffmpeg_o:-id3v2_version {p.Id3Version}"]);
        return a;
    }
    public static string Quality(string format, Preferences? p, int kbps) => format switch {
        "opus" or "m4a" => "原生音訊 / Native audio",
        "flac" or "wav" => "無損轉檔 / Lossless conversion",
        _ => p?.Mp3Encoding == "v0" ? "VBR V0" : $"{kbps} kbps"
    };
    // Check the actual codec/header/duration, then decode audio before publishing.
    // Opus pre-skip and AAC encoder delay can produce legitimate negative first PTS.
    public static async Task Validate(string file, string ffmpeg, double? expected, CancellationToken ct)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        var json = await ProcessRunner.Run(probe, ["-v","error","-select_streams","a:0","-show_entries","stream=codec_name,sample_rate,channels:format=duration","-of","json",file], null, ct);
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() != 1) throw new InvalidDataException("找不到有效音訊軌 / Invalid audio stream");
        var codec = streams[0].GetProperty("codec_name").GetString();
        var wanted = Path.GetExtension(file) switch { ".opus"=>"opus", ".m4a"=>"aac", ".mp3"=>"mp3", ".flac"=>"flac", _=>codec };
        if (codec != wanted) throw new InvalidDataException("來源編碼不相容；未進行有損降級，請改選其他格式 / Incompatible native codec; choose another format");
        if (!double.TryParse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration <= 0 || expected is > 0 && Math.Abs(duration - expected.Value) > Math.Max(2, expected.Value * .01))
            throw new InvalidDataException("音訊長度不符，可能下載未完成 / Audio duration mismatch");
        double previous=double.NegativeInfinity;bool backwards=false;int packets=0;
        await ProcessRunner.Run(probe,["-v","error","-select_streams","a:0","-show_entries","packet=dts_time","-of","csv=p=0",file],line=>{
            if(double.TryParse(line.Split(',')[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var value)){backwards|=value<previous;previous=value;packets++;}
        },ct);
        if(backwards||packets==0)throw new InvalidDataException("音訊時間軸無效 / Invalid audio timeline");
        await ProcessRunner.Run(ffmpeg, ["-v","error","-xerror","-nostdin","-i",file,"-map","0:a:0","-f","null","-"], null, ct);
    }
}
