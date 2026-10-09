# Development guide

This guide preserves the useful build, architecture, browser, and update notes from the old README. Release-specific changes belong in [GitHub Releases](https://github.com/HKmario852/mp4-mp3-downloader/releases), not the project introduction.

## Project layout

| Directory | Responsibility |
| --- | --- |
| [src/Core](../src/Core/) | Download tasks, SQLite, media tools, metadata, ID3, validation, native IPC |
| [src/Windows](../src/Windows/) | WPF views, tray, notifications, browser registration, updates |
| [src/NativeHost](../src/NativeHost/) | Chromium Native Messaging bridge |
| [android](../android/) | Compose UI, foreground service, SAF storage, Android media handling |
| [extension](../extension/) | Manifest V3, YouTube page integration, cookie consent |
| [scripts](../scripts/) | Builds, tool preparation, packaging, Windows updater |
| [tests](../tests/) | Core, integration, extension, and screenshot checks |

Windows and Android implement their own platform I/O. They share behavior and data conventions, not a cross-platform UI framework.

## Build and checks

Run from the repository root in PowerShell on Windows. Use .NET 8 SDK or newer. Published Windows packages target x64 and include the .NET runtime.

```powershell
dotnet test tests/Core.Tests/Core.Tests.csproj
./scripts/Build-Windows.ps1
```

The Windows build places `OMNI.exe`, `Omni.NativeHost.exe`, yt-dlp, FFmpeg, ffprobe, Deno, and the updater in `artifacts/windows-win-x64/`. [Prepare-Tools.ps1](../scripts/Prepare-Tools.ps1) downloads upstream release assets and checks their SHA256 digests. Keep these files together. Without the media tools, the UI can open, but downloads cannot run.

Android needs a full JDK 21, Android SDK / build-tools 35, NDK 27.0.12077973, and CMake 3.22.1. The Gradle wrapper is pinned to 8.11.1.

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

The script copies source into an ASCII-only local build cache to avoid Gradle test-launch issues with non-ASCII paths. It builds the debug app and instrumentation APK, runs JVM unit tests, and copies the app to `artifacts/OmniDownloader-universal-debug.apk`. Bundled ABIs are arm64-v8a, armeabi-v7a, x86, and x86_64. A debug build does not establish real-device or background-service reliability; distribution signing requires your own key, kept outside Git.

Extension unit checks require Node.js; rendered extension checks use the Playwright dependency in [package.json](../package.json):

```powershell
npm ci
npm run test:extension
npm run test:browser
```

See [verification records](VERIFICATION.md) and the version-specific `VERIFICATION-*.md` documents for the actual scope of previous checks. Those records describe a particular build, not a guarantee for every device or website.

## Audio pipeline

See [multi-format audio, tag services, SAF recovery and verification](AUDIO-PIPELINE.md). MP3 defaults to ID3v2.3; ID3v2.4 is optional for new downloads.

## Browser connection

1. Extract and manually launch the Windows app once.
2. Open `chrome://extensions` or `brave://extensions`, enable developer mode, and load the unpacked `extension` folder.
3. Copy the extension ID into the app's browser settings, then save and connect.
4. Read the extension's consent prompt before enabling forwarding of local YouTube login cookies.
5. Open a supported YouTube page and use the extension's download action. Format and quality follow app preferences.

The bridge uses Native Messaging and a current-user Named Pipe. A success acknowledgement follows persistence of the received task. Video-plus-playlist URLs still require a choice in the app. The `ytdl://` fallback carries a URL and format, **no cookies**. Login is only useful for content that the account can access; it does not bypass rights or DRM restrictions.

The Windows app registers the protocol and native host under HKCU, without administrator permission. If you move the portable folder, manually start the app again to repair absolute paths. Reload the unpacked extension and refresh YouTube after updating its files.

Do not put cookies in URLs, Git, logs, screenshots, or release archives. Forwarded browser credentials stay in memory and need to be sent again after restarting. A separately imported cookies file remains a sensitive file on disk.

## Data and media behavior

- Windows stores preferences and history in `data/` beside the app, falling back to `%LOCALAPPDATA%/OmniDownloader` when needed. Supported audio files and folder sources added to the tag editor are remembered. Audio preview belongs to the app session and continues between pages.
- On Android, the initial app-external music folder is deleted on uninstall. Select a shared SAF folder for durable downloads. Foreground-service limits and storage-provider behavior depend on Android and the device.
- Preferences are captured when a download is queued. Incomplete tasks are restored as paused after a process restart. Pause retains resumable work; cancel removes task-specific staging.
- Quality choices depend on source formats. The video selector limits known heights to the chosen setting; sources without height metadata prevent an absolute resolution guarantee. Transcoding cannot improve the source's quality.
- Audio artwork is embedded in the format’s native picture structure, without publishing JPG sidecars. Automatic album artwork accepts near-square images rather than stretching rectangular video thumbnails. Standalone video thumbnails are opt-in.
- Windows MP3 metadata mode defaults to background lookup after download, with before/off alternatives. Android automatic tag matching defaults to off; its missing-artwork fallback may still query MusicBrainz. Disabling tag matching is not a global offline switch.

## Tag editing and recognition

The editor works with MP3 (ID3v2.3/v2.4), Opus/FLAC (Vorbis comments), and M4A (MP4 atoms); do not assume every unusual frame or file variant is supported. Deltas distinguish an absent key (unchanged) from an explicitly empty value. Track numbers are fixed batch values, not automatic increments. Changing a nonempty title can rename the file; Windows filename validation and collision handling apply. Unchanged titles do not trigger renaming.

Scan is a top-level view in the existing window, not a second window. Fingerprinting decodes up to 120 seconds locally. AcoustID receives the fingerprint and total duration; MusicBrainz receives metadata queries or IDs. Public MusicBrainz lookup needs no account. The distributed Windows app has an AcoustID application key; an optional custom application key can be configured. Neither a MusicBrainz password nor an AcoustID personal submission key is required.

Results remain separate from persisted tags. The review page writes only selected fields after explicit application, and changes artwork only when requested. Optional undo records retain a file snapshot; intervening file changes can prevent restoration. Automatic download-time metadata enrichment is a separate setting, not a manual Scan confirmation.

See [music recognition](MUSIC-RECOGNITION.md) and [privacy](PRIVACY.md) for the lookup and consent boundaries.

## Packaging and updates

After building both platforms:

```powershell
./scripts/Package.ps1
```

The script packages Windows, the browser extension, and source, then writes `artifacts/SHA256SUMS.txt`. The Android debug APK is included in the checksum list if present. Release packages must exclude runtime `data/`, cookies, private keys, local SDK configuration, and `native-host.json`.

The Windows app checks the configured GitHub repository for releases and asks before installing. The updater verifies the asset digest, ZIP paths and executable architecture, waits for the app, backs up replaced files, and rolls back failures. It preserves user data and browser registration. Paused tasks need to be resumed; in-memory login credentials do not survive restart.

Windows packages now use `OMNI.exe`. Older releases used `App.exe`, and their bundled updater requires that old filename. For the first migration, exit the app and manually extract the complete new Windows package into the same folder, preserving `data/` and `native-host.json`. Start `OMNI.exe`, remove the old `App.exe`, and update any manually created shortcuts. The new updater also supports migrating a legacy installation when invoked directly with the required PID and verified package digest; subsequent updates restart `OMNI.exe`. The new native host prefers `OMNI.exe` and supports the old name only as a fallback.

For updater development, a real matching package, PID, and SHA256 are required:

```powershell
./scripts/updater.ps1 -InstallPath 'C:\Apps\Omni' -AppPid 1234 -ZipPath 'C:\Downloads\Omni.zip' -ExpectedSha256 '<64 hex>' -ReleasesUrl 'https://github.com/HKmario852/mp4-mp3-downloader/releases'
```

This is a developer example, not a command to run with placeholder values. Unsigned Windows builds can trigger antivirus warnings; that warning alone does not prove either safety or malware.

## README maintenance

The main README and [five translations](i18n/) share the same sections and factual scope. Documentation languages do not imply UI language support. Regenerate screenshots and check links as described in [README maintenance](README-MAINTENANCE.md).

Older [architecture](ARCHITECTURE.md) and [implementation](IMPLEMENTATION.md) records explain design decisions, but some describe earlier defaults or UI versions. Current source and this guide take precedence for current behavior. Redistribution requirements are in [LICENSE](../LICENSE) and [third-party notices](../THIRD-PARTY.md).
