# Multi-format audio verification — 2026-10-09

This records a local development build on `codex/multi-format-audio`; it is not a public release or a real-tablet test.

| Check | Result |
| --- | --- |
| Windows Core tests | 97 passed, including Unicode tags, artwork, track/disc totals, MusicBrainz IDs, unchanged fields, exact undo and ID3 version choice |
| Android JVM tests | 23 passed |
| Android emulator instrumentation | 7 passed: four-container tag/artwork preservation, review apply/exact undo, local Chromaprint fingerprints, ID3v2.3/v2.4, SAF verified write/fault rollback/stale rejection, and two same-Activity review UI checks |
| Real offline yt-dlp pipeline | Opus, M4A, MP3 320, MP3 V0 and FLAC valid; missing native Opus rejected |
| Native stream copy | Encoded packet SHA-256 values unchanged for Opus/WebM remux and M4A |
| Android tag output | All four containers independently decoded by host FFmpeg; encoded packet hashes identical before/after editing |
| WPF at 1440×940 and 1050-wide | Four formats read/play, raw ID3 disabled outside MP3, full-page review/sidebar/draft preserved, no pre-apply file writes, optional ID3v2.4 control present |
| Packaging | Windows self-contained publish and Android debug/instrumentation APK builds passed |
| Documentation | Relative links in pipeline/development/third-party documents exist; Git whitespace check passed |

Only original synthetic sine tones and a solid-color cover were used. No personal song files or browser credentials were read for this refactor's tests. Test lookup responses were fixtures; no live AcoustID match is claimed.

Local evidence (ignored build artifacts):

- `artifacts/audio-pipeline-qa/results.json` and per-profile logs.
- `artifacts/audio-windows-final/multi-format-checks.json` and editor/review/settings screenshots.
- `artifacts/audio-android-qa/instrumentation.txt`, `packet-checks.json` and review screenshot.
- `artifacts/android-test-results/` JVM reports.

See [reproduction commands and implementation](AUDIO-PIPELINE.md). The Windows Opus preview uses a disposable decoded WAV. Android MediaExtractor may report decoded FLAC as `audio/raw`; the FLAC signature is checked and fingerprinting feeds PCM directly. Android validation is a header/sample-timeline check, not a complete decode; the host decode supplemented these fixture tests. Long-running foreground-service reliability, every SAF/cloud provider, and live YouTube/VPN/account combinations remain outside this verification.
