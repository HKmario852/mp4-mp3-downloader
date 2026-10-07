<p align="center"><img src="docs/assets/logo.png" alt="Omni Downloader" width="88" /></p>
<h1 align="center">Omni Downloader</h1>
<p align="center">Download videos and audio. Organize files, edit MP3 tags, and review music matches.</p>

<p align="center">
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases/latest"><img src="https://img.shields.io/github/v/release/HKmario852/mp4-mp3-downloader" alt="Latest release" /></a>
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases"><img src="https://img.shields.io/github/downloads/HKmario852/mp4-mp3-downloader/total" alt="Release downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-5865F2" alt="Windows and Android" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue" alt="GPL-3.0-or-later" /></a>
</p>

<p align="center"><strong>English</strong> · <a href="docs/i18n/README.zh-TW.md">繁體中文</a> · <a href="docs/i18n/README.zh-CN.md">简体中文</a> · <a href="docs/i18n/README.ja.md">日本語</a> · <a href="docs/i18n/README.ko.md">한국어</a> · <a href="docs/i18n/README.es.md">Español</a></p>

<p align="center">
  <a href="docs/screenshots/windows-downloads.png"><img src="docs/screenshots/windows-downloads.png" alt="Download queue" width="49%" /></a>
  <a href="docs/screenshots/windows-tag-editor.png"><img src="docs/screenshots/windows-tag-editor.png" alt="MP3 tag editor" width="49%" /></a>
</p>

*Windows screenshots use generated sample media and original geometric artwork. Click to enlarge.*

<details>
<summary>More screenshots</summary>

![Downloaded files](docs/screenshots/windows-library.png)
![Settings](docs/screenshots/windows-settings.png)

</details>

## Features

- 📥 Download videos and playlists with yt-dlp; queue, pause, resume, and manage tasks.
- 🎞️ Choose MP4, MKV, WebM, MP3, M4A, FLAC, or WAV, depending on the source.
- 🏷️ Edit MP3 tags in batches and embed near-square cover art without extra JPG files.
- 🔎 Review AcoustID / MusicBrainz matches before importing selected fields, with optional undo records.
- 🎧 On Windows, remember added music folders and keep preview audio playing while switching pages.
- 🌐 Send YouTube links from Chrome or Brave to the Windows app through the browser extension.

## Download / Install

Get the packages from **[the latest release](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)**.

| Platform | Requirements | Install |
| --- | --- | --- |
| Windows | Windows 10 (2004+) or 11, x64 | Extract the Windows ZIP into a writable folder. Run `OMNI.exe`; keep the bundled files together. |
| Android | Android 8.0+ (API 26) | Install the universal debug APK. Choose a shared folder to keep downloads after uninstalling. |
| Browser extension | Chrome / Brave on Windows | Load the unpacked extension and connect its ID in app settings. [Setup guide](docs/DEVELOPMENT.md#browser-connection). |

> **Note:**
> The Windows executable is unsigned. Android packages are debug builds for testing; device and background-service verification is incomplete.

> [!NOTE]
> Older releases use `App.exe`. For the first update to the new name, exit the app and extract the complete Windows package into your existing folder, keeping `data/` and `native-host.json`. Then start `OMNI.exe`.

## Quick start

1. Paste a video or playlist URL into **New download**, then select **Analyze link**.
2. Choose a format, quality, and destination, then start the download.
3. Find completed files in **Downloaded**. Open **Tag editor** to add MP3 files or folders and edit their tags or artwork.
4. To identify a song, select one MP3 and choose **Scan 音訊辨識**. Review the candidates and changes; only explicitly applied fields and artwork are written.

## Build from source

On Windows, install **.NET 8 SDK or newer**, then run in PowerShell:

```powershell
git clone https://github.com/HKmario852/mp4-mp3-downloader.git
cd mp4-mp3-downloader
./scripts/Build-Windows.ps1
```

Output: `artifacts/windows-win-x64/OMNI.exe`. The script downloads and verifies the bundled media tools; the published app includes its .NET runtime.

For Android, also install **JDK 21**, **Android SDK / build-tools 35**, **NDK 27.0.12077973**, and **CMake 3.22.1**. Set `JAVA_HOME` and `ANDROID_HOME`, then run:

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

Output: `artifacts/OmniDownloader-universal-debug.apk`. The script also builds instrumentation tests and runs JVM unit tests. See [the development guide](docs/DEVELOPMENT.md) for architecture, checks, and packaging.

## Configuration

- Set output folders, formats, quality, subtitles, and artwork options in **Settings**. Standalone video thumbnails are **off by default**.
- Windows defaults to MP3 metadata lookup **after downloading**, in the background; choose before, after, or off. Android automatic tag matching defaults to off, but its artwork fallback can still query MusicBrainz.
- The app offers **Traditional Chinese and English** settings. Translation is incomplete: some screens and the browser extension remain in Chinese. These README translations do not add app languages.
- Restricted content may require a Netscape-format cookies file from an account with access. Never share that file. [Browser connection](docs/DEVELOPMENT.md#browser-connection) · [Music recognition](docs/MUSIC-RECOGNITION.md).

## Tech stack

- **Windows:** C#, .NET 8, WPF, SQLite.
- **Android:** Kotlin, Jetpack Compose, SQLite, Storage Access Framework.
- **Media and integration:** yt-dlp, FFmpeg, Deno, Chromaprint, MusicBrainz, AcoustID, JavaScript / Manifest V3.

## Privacy / Disclaimer

Preferences and history are stored locally. Downloads contact the source website; update checks contact GitHub. Music lookups send titles, artists, IDs, or audio fingerprints and duration to metadata services, **not the audio file**. Browser cookie forwarding requires consent. See [privacy details](docs/PRIVACY.md).

Use only with content you are allowed to download. This project does not implement DRM decryption and is not affiliated with the supported websites. Site support depends on yt-dlp and may break when websites change. Availability and music matches are not guaranteed.

## License and acknowledgements

Licensed under **[GPL-3.0-or-later](LICENSE)**. Thanks to yt-dlp, FFmpeg, youtubedl-android, Deno, Chromaprint, MusicBrainz, AcoustID, and Cover Art Archive. See [third-party notices](THIRD-PARTY.md) for upstream projects and redistribution obligations.
