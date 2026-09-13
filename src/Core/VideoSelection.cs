namespace Omni.Core;

public sealed record VideoPlan(MediaFormat Video, MediaFormat? Audio)
{
    public string? Selector => Video.Id is null ? null : Audio is null ? Video.Id : Audio.Id is null ? null : Video.Id + "+" + Audio.Id;
}

public static class VideoSelection
{
    public static int Cap(int quality) => quality <= 0 ? 2160 : Math.Min(quality, 2160);
    public static string Fallback(int quality)
    {
        var cap = $"[height<=?{Cap(quality)}]";
        return $"bv{cap}[ext=mp4]+ba[ext=m4a]/b{cap}[ext=mp4]/bv{cap}+ba/b{cap}";
    }
    public static VideoPlan? Select(MediaInfo info, int quality)
    {
        var formats = info.Formats ?? [];
        // Extractor formats arrive in yt-dlp's worst-to-best preference order.
        // The chosen IDs are also passed to the actual download, so estimates use identical streams.
        var ordered = formats.All(f => f.Id is null)
            ? formats.OrderBy(f => f.Height ?? 0).ThenBy(f => f.Bitrate ?? 0).ToArray() : formats;
        bool Fits(MediaFormat f) => f.Video && (f.Height is null || f.Height <= Cap(quality));
        var video = ordered.LastOrDefault(f => Fits(f) && !f.Audio && f.Extension == "mp4");
        var audio = ordered.LastOrDefault(f => !f.Video && f.Audio && f.Extension == "m4a");
        if (video is not null && audio is not null) return new(video, audio);
        var muxed = ordered.LastOrDefault(f => Fits(f) && f.Audio && f.Extension == "mp4");
        if (muxed is not null) return new(muxed, null);
        video = ordered.LastOrDefault(f => Fits(f) && !f.Audio);
        audio = ordered.LastOrDefault(f => !f.Video && f.Audio);
        if (video is not null && audio is not null) return new(video, audio);
        muxed = ordered.LastOrDefault(f => Fits(f) && f.Audio);
        return muxed is null ? null : new(muxed, null);
    }
}
