namespace Omni.Core;

public static class SizeEstimator
{
    public static long? Estimate(MediaInfo? info, DownloadMode mode, int quality)
    {
        if (info is null) return null;
        if (mode == DownloadMode.Mp3)
            return info.Duration is > 0 ? (long)Math.Ceiling(info.Duration.Value * quality * 1000 / 8) : null;
        var plan = VideoSelection.Select(info, quality);
        if (plan is null) return null;
        var bytes = Bytes(plan.Video, info.Duration);
        if (plan.Audio is null || bytes is null) return bytes;
        var audioBytes = Bytes(plan.Audio, info.Duration);
        return audioBytes is null ? null : bytes + audioBytes;
    }
    static long? Bytes(MediaFormat f, double? duration) => f.FileSize is > 0 ? f.FileSize : f.Bitrate is > 0 && duration is > 0 ? (long)Math.Ceiling(f.Bitrate.Value * 1000 / 8 * duration.Value) : null;
    public static string Label(long? bytes) => bytes is null ? "大小待分析" : bytes >= 1_000_000_000 ? $"約 {bytes / 1_000_000_000.0:F2} GB" : $"約 {bytes / 1_000_000.0:F1} MB";
}
