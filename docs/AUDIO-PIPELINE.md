# Multi-format audio pipeline

OMNI supports downloads and tag editing for MP3, Opus, M4A and FLAC on Windows and Android. WAV downloads remain available; WAV is not included in the tag editor.

## User settings

- **Formats → Audio format:** MP3 (default), Opus, M4A, FLAC or WAV.
- **MP3 encoding:** CBR (128/192/256/320 kbps; default 320) or VBR V0. This controls transcoding; an already-native MP3 stream is retained without re-encoding.
- **New MP3 tag version:** ID3v2.3 (default) or optional ID3v2.4. Editing an existing MP3 keeps its current version; unsupported/nonstandard ID3 layouts fail rather than being rewritten unsafely.
- **Metadata timing:** finish downloading before background identification, or wait for identification before completion. Windows also has an off choice. Android's MusicBrainz toggle turns automatic identification off; its timing option applies when enabled.
- Manual edits are protected against delayed background identification. Ambiguous matches require manual review.

Choosing FLAC does not recover information lost in YouTube's AAC/Opus source. Choose native Opus/M4A to avoid an additional lossy conversion; choose MP3 for playback compatibility. Format IDs 251 and 140 are common examples, not guarantees: codec filters select the best available matching audio stream.

## yt-dlp command matrix

All rows use `-x` / `--extract-audio`. These are argument-list templates, not shell-interpolated strings.

| Output | `-f` selector | `--audio-format` | Quality / postprocessor arguments |
| --- | --- | --- | --- |
| Opus | `bestaudio[ext=webm][acodec=opus]` | `opus` | `--postprocessor-args "ExtractAudio+ffmpeg_o:-c:a copy"` |
| M4A | `bestaudio[ext=m4a][acodec^=mp4a.40]/bestaudio[acodec=aac]` | `m4a` | `--postprocessor-args "ExtractAudio+ffmpeg_o:-c:a copy"` |
| MP3 CBR | `bestaudio[acodec=mp3]/bestaudio/best` | `mp3` | `--audio-quality 320k` (or 128k/192k/256k) |
| MP3 V0 | `bestaudio[acodec=mp3]/bestaudio/best` | `mp3` | `--audio-quality 0` |
| FLAC | `bestaudio/best` | `flac` | yt-dlp's ExtractAudio uses the `flac` encoder |

MP3 additionally passes `--postprocessor-args "Metadata+ffmpeg_o:-id3v2_version 3"` (or 4). A final tag-version pass handles newly downloaded MP3s even when metadata embedding was disabled or native MP3 extraction skipped FFmpeg. yt-dlp uses `libmp3lame` for MP3 conversion. Native AAC already in M4A may skip remux entirely; this also preserves the audio.

No Opus/M4A selector falls back to another codec. Missing native audio stops with a message suggesting MP3/FLAC. A failed copy, corrupt header or bad timeline also stops; OMNI does not silently re-encode.

Builders: [C#](../src/Core/AudioPipeline.cs), [Kotlin](../android/app/src/main/java/io/hkmario/omni/AudioPipeline.kt). Existing [Windows](../src/Core/DownloadOptions.cs) and [Android](../android/app/src/main/java/io/hkmario/omni/Options.kt) command builders add network, output, metadata and thumbnail preferences. Audio artwork is embedded separately; it does not produce JPG sidecars.

```csharp
var preferences = new Preferences { Mp3Encoding = "v0", Id3Version = 4 };
var arguments = AudioPipeline.Arguments("mp3", preferences, 320);
// Add each argument via ProcessStartInfo.ArgumentList, never interpolate a shell command.
```

```kotlin
val arguments = AudioPipeline.arguments("opus", Prefs(), 320)
Options.apply(YoutubeDLRequest(url), arguments)
```

## Tag abstraction and write boundary

Canonical IDs keep the existing comparison table/delta API stable. `Fields` is a patch: an absent key is untouched, an empty value clears that selected field. Null artwork preserves all pictures; an empty artwork list removes them. Do not pass a full read snapshot when only one field is selected.

| Container | Native metadata | Recording ID |
| --- | --- | --- |
| MP3 | Existing ID3v2.3/v2.4 frames | MusicBrainz-owned UFID plus compatible TXXX aliases |
| Opus / FLAC | Vorbis comments; native picture blocks / METADATA_BLOCK_PICTURE | `MUSICBRAINZ_TRACKID` |
| M4A | MP4 `ilst`, `covr` pictures | `----:com.apple.iTunes:MusicBrainz Track Id` |

MusicBrainz Track Id here means the **recording MBID**, not the release-track MBID. The release MBID uses MusicBrainz Album Id. Full original dates use Vorbis `ORIGINALDATE` / M4A `----:com.apple.iTunes:ORIGINALDATE`; ID3v2.3's TORY stores only the original year. Track/disc values support `n` and `n/total`.

Windows uses TagLibSharp 2.3.0 for non-MP3 containers and new MP3 version conversion. MP3 editing retains OMNI's frame-preserving ID3 writer so unchecked frames and audio bytes stay intact. Other formats preserve unselected fields semantically; their containers may be reorganized. The concrete service stages changes, re-reads selected values and checks the original hash before replacement:

```csharp
namespace Omni.Core;

// Canonical field IDs keep the existing review/delta API stable across containers.
// Fields contains ONLY values to write; absent keys and null Covers are untouched.
public sealed record AudioMetadata(Dictionary<string,string> Fields, List<Cover>? Covers = null);
public interface IAudioTagService
{
    Task<AudioMetadata> ReadTagsAsync(string filePath);
    Task WriteTagsAsync(string filePath, AudioMetadata metadata);
    Task EmbedCoverArtAsync(string filePath, byte[] imageBytes, string mimeType);
}
public sealed class TagLibAudioTagService : IAudioTagService
{
    static readonly SemaphoreSlim serial = new(1);
    public Task<AudioMetadata> ReadTagsAsync(string filePath) => Task.Run(() => {
        var doc=AudioTagDocument.Read(filePath);
        return new AudioMetadata(TagReview.Values(doc),doc.GetCovers());
    });
    public async Task WriteTagsAsync(string filePath, AudioMetadata metadata)
    {
        await serial.WaitAsync();
        var temp=filePath+".tags-"+Guid.NewGuid();
        try {
            var hash=await TagReview.Hash(filePath);var doc=AudioTagDocument.Read(filePath);
            foreach(var pair in metadata.Fields)doc.SetText(TagReview.Frame(pair.Key,doc.Version),pair.Value);
            if(metadata.Covers is not null)doc.SetCovers(metadata.Covers);
            await doc.Write(filePath,temp);
            if(await TagReview.Hash(filePath)!=hash)throw new IOException("檔案在編輯期間已有修改 / File changed while editing");
            File.Replace(temp,filePath,null);
        } finally {if(File.Exists(temp))File.Delete(temp);serial.Release();}
    }
    public Task EmbedCoverArtAsync(string filePath,byte[] imageBytes,string mimeType)
    {
        if(imageBytes.Length is 0 or > 32*1024*1024 || mimeType is not ("image/png" or "image/jpeg"))throw new ArgumentException("Use PNG/JPEG artwork under 32 MB");
        return WriteTagsAsync(filePath,new([], [new(imageBytes,mimeType,"Album front",3)]));
    }
}
```

Full implementation: [AudioTags.cs](../src/Core/AudioTags.cs). The existing [TagEditor](../src/Core/Id3.cs) and [review/undo](../src/Core/TagReview.cs) use the same document abstraction. Scan results remain separate until the user applies selected fields; artwork remains opt-in. Folder imports, renaming, in-memory refresh and exact undo use the actual file extension.

Android uses the pinned jaudiotagger Android fork for Opus/FLAC and field serialization for M4A. [Mp4TagPatch](../android/app/src/main/java/io/hkmario/omni/Mp4TagPatch.kt) patches chosen atoms, preserves unrelated atoms, and replaces/appends `moov` without moving media or changing chunk offsets. This avoids the fork's flattening writer failing on FFmpeg M4As. MP3 editing keeps its existing raw-frame writer. [AudioTags.kt](../android/app/src/main/java/io/hkmario/omni/AudioTags.kt) is the unified document facade.

## Android SAF example

Do not pass a `content://` URI to a random-access tagging library. Copy it to a cache file with the correct extension, edit a separate staged file, then write it back through the granted URI. Keep a durable private backup until the provider write has been verified:

```kotlin
suspend fun updateSafTags(context: Context, uri: Uri, extension: String,
                         selected: Map<String, String>, cover: ByteArray?) =
    withContext(Dispatchers.IO) {
        val id = UUID.randomUUID()
        val source = File(context.cacheDir, "source-$id.$extension")
        val staged = File(context.cacheDir, "edited-$id.$extension")
        val backup = File(context.filesDir, "tag-recovery/$id.$extension")
        try {
            require(AudioTags.supported(extension))
            (context.contentResolver.openInputStream(uri)
                ?: error("Read permission unavailable")).use { input ->
                source.outputStream().use { input.copyTo(it) }
            }
            val tags = AudioTags.read(source)
            selected.forEach { (key, value) -> tags.setText(key, value) }
            if (cover != null) tags.covers(listOf(Art(cover, "image/png", "Front", 3)))
            tags.write(source, staged)
            SafTagStorage.durableCopy(source, backup)
            SafTagStorage.writeBack(context, uri, staged, backup)
            backup.delete()
            File(backup.path + ".uri").delete()
        } finally {
            source.delete()
            staged.delete()
            // A failed provider write intentionally retains the durable backup.
        }
    }
```

Use the actual MIME type when embedding JPEG instead of the example's PNG. Imports retain SAF permissions where the provider allows them. [TagEditor.kt](../android/app/src/main/java/io/hkmario/omni/TagEditor.kt) serializes writes and updates track state; [SafTagStorage.kt](../android/app/src/main/java/io/hkmario/omni/SafTagStorage.kt) checks hashes, flushes the backup to disk, verifies write-back, attempts rollback and retains failed recovery copies. Undo uses the same SAF helper.

SAF providers cannot guarantee atomic replacement. Revoked access, a disconnected device, or a cloud provider error may prevent rollback too; the error names the retained private backup. It is not automatically deleted on failure. Private backups disappear if app data is cleared/uninstalled.

## Edge defenses

| Case | Handling |
| --- | --- |
| Native codec missing / YouTube changes format IDs | Codec-based selection; fail clearly and suggest another format; no silent lossy fallback |
| Native-copy failure / damaged source | Keep the task failed and staging output unpublished; retry after fixing the source/tool issue |
| Negative initial AAC/Opus timestamps | Allowed for encoder delay / pre-skip; reject backwards timestamps, not a negative first timestamp |
| Truncated header, missing audio, unexpected codec/duration | Reject before publishing |
| File changed after scan | Original hash guard refuses stale import/undo |
| Unselected fields or artwork | Leave them untouched; keep existing MP3 version |
| Interrupted SAF write | Verify, attempt rollback, retain durable recovery file and report failure |
| MP4 oversized header or extends-to-EOF atom | Refuse unsupported editing layout, preserve original; remux a copy before retrying |
| Delayed background identification | Protect manual changes; ambiguous versions stay in manual review |

Windows requires FFprobe beside FFmpeg. It checks actual codec/header/duration, all packet DTS values, and a complete FFmpeg decode. Android validates native MIME/header/duration and sample timestamps with MediaExtractor (including `audio/x-flac` and devices that decode native FLAC into `audio/raw`). The latter path verifies the FLAC signature and feeds decoded PCM directly to Chromaprint; PCM 8/16/24/32-bit and float buffers are converted to fingerprint input without modifying the source. Android validation is not a full decode/CRC check; it depends on the platform extractor. The offline QA additionally decodes Android-written files with FFmpeg on the host.

Windows Opus preview decodes a disposable WAV because WPF's native player does not reliably support Ogg/Opus. Downloaded files remain unchanged; leaving sections keeps the shared playback session alive, and stopping/closing removes the temporary preview.

## Reproducible verification

Fixtures are original one-second tag-test and twenty-second fingerprint-test sine tones and a solid-color PNG. No personal music or copyrighted covers are used.

```powershell
python scripts/Generate-AudioFixtures.py --ffmpeg artifacts/windows-win-x64/ffmpeg.exe
$env:OMNI_TEST_FFPROBE = (Resolve-Path artifacts/windows-win-x64/ffprobe.exe).Path
dotnet test tests/Core.Tests
python scripts/Test-AudioPipeline.py --tools artifacts/windows-win-x64
dotnet run --project tests/Windows.Smoke -- artifacts/audio-windows-ui artifacts/windows-win-x64 --multi-format-regression
```

Android: build with [Build-Android.ps1](../scripts/Build-Android.ps1), install the app and instrumentation APK on a test device/emulator, then run `io.hkmario.omni.AudioPipelineTest` and `io.hkmario.omni.AcoustIdReviewUiTest`. SAF fault injection is an in-process test provider (API 29+), not an exported production provider. Pull `files/qa-audio.*` from the debug test app for independent FFprobe packet-hash and FFmpeg decode verification.

These checks verify synthetic pipelines and UI behavior; they do not establish every YouTube region/VPN/account combination or real-tablet compatibility. See [third-party notices](../THIRD-PARTY.md) for pinned dependencies and licenses.
