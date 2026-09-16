using System.Buffers.Binary;
using System.Text;
namespace Omni.Core;
public sealed record Id3Frame(string Id, byte[] Flags, byte[] Data);
public sealed record Cover(byte[] Bytes, string Mime, string Description, byte Type);
public static class CoverOrder
{
    public static List<Cover> ThumbnailFirst(IEnumerable<Cover> albumCovers, Cover? thumbnail) => thumbnail is null
        ? albumCovers.ToList()
        : [thumbnail with { Type = 3 }, .. albumCovers.Select(c => c with { Type = 0 })];
}
public sealed class Id3Document
{
    public byte Version { get; private set; } = 3;
    public long AudioOffset { get; private set; }
    public List<Id3Frame> Frames { get; } = [];
    static int Synch(ReadOnlySpan<byte> b) => (b[0] << 21) | (b[1] << 14) | (b[2] << 7) | b[3];
    static void SetSynch(Span<byte> b, int n) { b[0] = (byte)((n >> 21) & 127); b[1] = (byte)((n >> 14) & 127); b[2] = (byte)((n >> 7) & 127); b[3] = (byte)(n & 127); }
    public static Id3Document Read(string path)
    {
        var d = new Id3Document(); using var f = File.OpenRead(path); var header = new byte[10];
        if (f.Read(header) != 10 || Encoding.ASCII.GetString(header, 0, 3) != "ID3") return d;
        if (header[3] is not (3 or 4)) throw new InvalidDataException("僅支援 ID3v2.3 / v2.4");
        if (header[5] != 0) throw new InvalidDataException("此 ID3 使用擴充標頭或 unsynchronisation；為保護原標籤，請先轉為標準 v2.3/v2.4");
        d.Version = header[3]; int size = Synch(header.AsSpan(6)); if (size > 64 * 1024 * 1024 || size > f.Length - 10) throw new InvalidDataException("ID3 長度無效");
        var bytes = new byte[size]; f.ReadExactly(bytes); d.AudioOffset = 10 + size;
        for (int i = 0; i + 10 <= size;)
        {
            if (bytes[i] == 0) break; string id = Encoding.ASCII.GetString(bytes, i, 4);
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Z0-9]{4}$")) throw new InvalidDataException("ID3 frame 無效");
            int length = d.Version == 4 ? Synch(bytes.AsSpan(i + 4, 4)) : BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(i + 4, 4));
            if (length < 0 || length > size - i - 10) throw new InvalidDataException("ID3 frame 長度無效");
            d.Frames.Add(new(id, bytes.AsSpan(i + 8, 2).ToArray(), bytes.AsSpan(i + 10, length).ToArray())); i += 10 + length;
        }
        return d;
    }
    public string Text(string id)
    {
        if (id == "COMM") return Comment();
        var b = Frames.FirstOrDefault(f => f.Id == id)?.Data; if (b is null || b.Length < 2) return "";
        return (b[0] switch { 0 => Encoding.Latin1.GetString(b, 1, b.Length - 1), 1 => DecodeUtf16(b.AsSpan(1)), 2 => Encoding.BigEndianUnicode.GetString(b, 1, b.Length - 1), 3 => Encoding.UTF8.GetString(b, 1, b.Length - 1), _ => "" }).TrimEnd('\0');
    }
    static string DecodeUtf16(ReadOnlySpan<byte> b) => b.Length >= 2 && b[0] == 0xfe && b[1] == 0xff ? Encoding.BigEndianUnicode.GetString(b[2..]) : Encoding.Unicode.GetString(b.Length >= 2 && b[0] == 0xff && b[1] == 0xfe ? b[2..] : b);
    public void SetText(string id, string value)
    {
        if(id=="COMM"){SetRaw("COMM",[1, (byte)'e',(byte)'n',(byte)'g',0xff,0xfe,0,0,0xff,0xfe,..Encoding.Unicode.GetBytes(value),0,0]);return;}
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^T[A-Z0-9]{3}$") || id == "TXXX") throw new ArgumentException("此欄位需要進階 Raw Frame 編輯");
        // Keep a real empty frame, even when the string is empty.
        SetRaw(id, [1, 0xff, 0xfe, .. Encoding.Unicode.GetBytes(value), 0, 0]);
    }
    public string Comment()
    {
        var b=Frames.FirstOrDefault(f=>f.Id=="COMM")?.Data;
        if(b is null || b.Length<5)return "";
        int start=4,step=b[0] is 1 or 2?2:1;
        for(;start+step<=b.Length;start+=step)if(b[start]==0&&(step==1||b[start+1]==0)){start+=step;break;}
        return (b[0] switch{0=>Encoding.Latin1.GetString(b.AsSpan(start)),1=>DecodeUtf16(b.AsSpan(start)),2=>Encoding.BigEndianUnicode.GetString(b.AsSpan(start)),3=>Encoding.UTF8.GetString(b.AsSpan(start)),_=>""}).TrimEnd('\0');
    }
    public List<Cover> GetCovers()
    {
        var result=new List<Cover>();
        foreach(var f in Frames.Where(f=>f.Id=="APIC")){
            var b=f.Data;if(b.Length<5)continue;int end=Array.IndexOf(b,(byte)0,1);if(end<0||end+2>=b.Length)continue;
            var mime=Encoding.ASCII.GetString(b,1,end-1);byte type=b[end+1];int start=end+2,step=b[0] is 1 or 2?2:1;
            for(;start+step<=b.Length;start+=step)if(b[start]==0&&(step==1||b[start+1]==0)){start+=step;break;}
            if(start<b.Length)result.Add(new(b[start..],mime,"",type));
        }
        return result;
    }
    public void SetRaw(string id, byte[] payload)
    {
        var parts = id.Split('#', 2); var name = parts[0]; var index = parts.Length == 2 && int.TryParse(parts[1], out var parsed) ? parsed : 0;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Z0-9]{4}$") || index < 0 || payload.Length > 32 * 1024 * 1024) throw new ArgumentException("Frame 無效");
        var matches = Frames.Select((f, i) => (f, i)).Where(x => x.f.Id == name).ToArray();
        if (index < matches.Length) Frames[matches[index].i] = new(name, [0, 0], payload);
        else if (index == matches.Length) Frames.Add(new(name, [0, 0], payload));
        else throw new ArgumentException("Frame 索引不存在");
    }
    public void SetCovers(IEnumerable<Cover> covers)
    {
        Frames.RemoveAll(f => f.Id == "APIC");
        foreach (var c in covers) Frames.Add(new("APIC", [0, 0], [0, .. Encoding.ASCII.GetBytes(c.Mime), 0, c.Type, .. Encoding.Latin1.GetBytes(c.Description), 0, .. c.Bytes]));
    }
    public async Task Write(string source, string destination, CancellationToken ct = default)
    {
        var size = Frames.Sum(f => 10 + f.Data.Length); if (size > 64 * 1024 * 1024) throw new InvalidDataException("封面與標籤超過 64MB");
        await using var input = File.OpenRead(source); input.Position = AudioOffset;
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        byte[] header = [(byte)'I', (byte)'D', (byte)'3', Version, 0, 0, 0, 0, 0, 0]; SetSynch(header.AsSpan(6), size); await output.WriteAsync(header, ct);
        foreach (var frame in Frames)
        {
            byte[] h = new byte[10]; Encoding.ASCII.GetBytes(frame.Id).CopyTo(h, 0);
            if (Version == 4) SetSynch(h.AsSpan(4, 4), frame.Data.Length); else BinaryPrimitives.WriteInt32BigEndian(h.AsSpan(4, 4), frame.Data.Length);
            frame.Flags.CopyTo(h, 8); await output.WriteAsync(h, ct); await output.WriteAsync(frame.Data, ct);
        }
        await input.CopyToAsync(output, ct); await output.FlushAsync(ct); output.Flush(true);
    }
}
public sealed record TagDelta(Dictionary<string, string> Text, Dictionary<string, string>? RawBase64 = null, List<Cover>? Covers = null, bool RenameFile = true);
public sealed class TagEditor(Store store)
{
    static readonly SemaphoreSlim serial = new(1);
    public async Task Apply(IReadOnlyList<DownloadJob> jobs, TagDelta delta, bool persist = true, bool automaticCover = false)
    {
        if (delta.Text.TryGetValue("TIT2", out var title) && Validation.TitleError(title) is { } error) throw new ArgumentException(error);
        if (delta.RawBase64?.Keys.Any(k => k.Split('#')[0] == "TIT2") == true) throw new ArgumentException("請在 Title 欄位修改歌曲名");
        if (delta.Text.Count == 0 && delta.RawBase64?.Count is not > 0 && delta.Covers is null) return;
        await serial.WaitAsync();
        try
        {
            foreach (var j in jobs)
            {
                var source = j.FilePath ?? throw new IOException("找不到檔案"); var doc = Id3Document.Read(source);
                foreach (var (id, text) in delta.Text) doc.SetText(id, text); // TRCK fixed value, no increment.
                foreach (var (id, encoded) in delta.RawBase64 ?? []) doc.SetRaw(id, Convert.FromBase64String(encoded));
                if (delta.Covers is not null) doc.SetCovers(delta.Covers);
                var temp = source + ".edit-" + Guid.NewGuid().ToString("N"); var backup = source + ".backup-" + Guid.NewGuid().ToString("N"); string destination = source;
                if (delta.RenameFile && delta.Text.TryGetValue("TIT2", out title) && title.Length > 0 && !string.Equals(Path.GetFileNameWithoutExtension(source), title, StringComparison.Ordinal)) destination = Validation.UniquePath(Path.GetDirectoryName(source)!, title, ".mp3");
                try
                {
                    await doc.Write(source, temp); File.Replace(temp, source, backup);
                    try { if (destination != source) File.Move(source, destination); }
                    catch { File.Replace(backup, source, null); throw; }
                    var old = Json.Encode(j);
                    try { j.FilePath = destination; if (delta.Text.ContainsKey("TIT2")) j.Title = doc.Text("TIT2"); if (delta.Text.ContainsKey("TPE1")) j.Artist = doc.Text("TPE1"); if (delta.Text.ContainsKey("TALB")) j.Album = doc.Text("TALB"); if(!automaticCover)j.IsUserEdited = true; if(!automaticCover&&(delta.Covers is not null||delta.RawBase64?.Keys.Any(k=>k.Split('#')[0]=="APIC")==true))j.CoverUserEdited=true; if(persist) store.Save(j); }
                    catch { if (destination != source) File.Move(destination, source); File.Replace(backup, source, null); var previous = Json.Decode<DownloadJob>(old); j.FilePath = previous.FilePath; j.Title = previous.Title; j.Artist = previous.Artist; j.Album = previous.Album; j.IsUserEdited = previous.IsUserEdited;j.CoverUserEdited=previous.CoverUserEdited; throw; }
                    File.Delete(backup);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        finally { serial.Release(); }
    }
}
