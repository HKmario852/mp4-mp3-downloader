# README maintenance

## Screenshots

The generator captures the client area of a real Windows WPF window sized 1440 × 1040. Its data comes from a fresh isolated store: synthesized silent audio, a plain-color video, and original geometric artwork. It does not use a personal music folder, a browser profile, copyrighted covers, or a live download. Automatic updates, clipboard monitoring, and metadata lookups are disabled for the sample store.

On Windows with .NET 8 SDK and the bundled FFmpeg available:

```powershell
./scripts/Generate-ReadmeScreenshots.ps1
```

Or provide an existing FFmpeg executable:

```powershell
./scripts/Generate-ReadmeScreenshots.ps1 -FfmpegPath 'C:\Tools\ffmpeg.exe'
```

The [generator](../tests/Readme.Screenshots/Program.cs) renders downloads, the tag editor, the library, and settings into [screenshots/](screenshots/). It uses a new `artifacts/readme-demo-*` directory for each run and refuses an existing work directory. Demo data stays outside Git. Display paths are replaced with `C:\Omni-Demo`; no account names or real media are shown.

The app can still display untranslated Chinese controls in English mode; do not edit screenshots to suggest otherwise.

## Links and preview

With Python 3 installed:

```powershell
python scripts/Check-ReadmeLinks.py
```

This checks local Markdown and HTML links, images, heading anchors, language switchers, and the shared eight-section structure of all six READMEs. It also checks local links in the development and maintenance guides. External URLs are not fetched by this script.

With GitHub CLI authenticated, render using GitHub's Markdown API:

```powershell
gh api -X POST markdown -f mode=markdown -F text=@README.md
python scripts/Render-ReadmePreview.py
python -m http.server 8765 --bind 127.0.0.1
```

Open `http://127.0.0.1:8765/artifacts/readme-preview/` in a desktop browser. The helper runs the exact API command above, saves its HTML, and adds a local GitHub-like wrapper. Its CSS is only for preview; the README itself uses GitHub-flavoured Markdown and supported HTML. Inspect the badge row, two-column screenshot pair, expanded screenshots, tables, and code blocks. Preview files are ignored in `artifacts/`.

## Factual sources

Recheck these when changing claims, and update every translation together:

| Claim | Primary source |
| --- | --- |
| Windows target and packaging | [Windows.csproj](../src/Windows/Windows.csproj), [Build-Windows.ps1](../scripts/Build-Windows.ps1) |
| Android SDK, native build and debug outputs | [app/build.gradle.kts](../android/app/build.gradle.kts), [Build-Android.ps1](../scripts/Build-Android.ps1) |
| Formats and thumbnail behavior | [DownloadOptions.cs](../src/Core/DownloadOptions.cs), [Downloader.cs](../src/Core/Downloader.cs) |
| MP3 lookup defaults | [Models.cs](../src/Core/Models.cs), [Downloader.Metadata.cs](../src/Core/Downloader.Metadata.cs), Android preferences and engine |
| Recognition and selected-field writes | [MusicRecognition.cs](../src/Core/MusicRecognition.cs), [TagReview.cs](../src/Core/TagReview.cs), [AcoustIdReviewView.cs](../src/Windows/AcoustIdReviewView.cs) |
| ID3 and artwork | [Id3.cs](../src/Core/Id3.cs), [AlbumArtwork.cs](../src/Core/AlbumArtwork.cs) |
| Folder persistence and playback | [TagLibrary.cs](../src/Core/TagLibrary.cs), [AudioPreviewSession.cs](../src/Windows/AudioPreviewSession.cs) |
| Partial UI translation | [Localization.cs](../src/Windows/Localization.cs), [AcoustIdReviewView.cs](../src/Windows/AcoustIdReviewView.cs), [extension/](../extension/) |
| Browser permissions | [manifest.json](../extension/manifest.json), [RegistryIntegration.cs](../src/Windows/RegistryIntegration.cs) |
| License and real logo | [LICENSE](../LICENSE), [brand.png](../src/Windows/Assets/brand.png) |

The README logo is a byte-for-byte copy of the project's `brand.png`. Dynamic release/download badges refer to this repository rather than hard-coded counts. The translated READMEs are documentation, not a promise of five additional UI languages.
