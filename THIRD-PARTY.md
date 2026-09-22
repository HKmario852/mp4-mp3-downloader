# 第三方來源

| 元件 | 上游 | 授權 / 說明 |
| --- | --- | --- |
| yt-dlp | https://github.com/yt-dlp/yt-dlp | Unlicense；打包元件可能另有授權 |
| FFmpeg | https://ffmpeg.org/ ; https://github.com/BtbN/FFmpeg-Builds | 此專案工具腳本選用 GPL build |
| Deno | https://github.com/denoland/deno | MIT |
| youtubedl-android | https://github.com/yausername/youtubedl-android | GPL-3.0 |
| .NET / WPF | https://github.com/dotnet/wpf | MIT |
| Microsoft.Data.Sqlite | https://github.com/dotnet/efcore | MIT |
| Windows Community Toolkit | https://github.com/CommunityToolkit/WindowsCommunityToolkit | MIT |
| AndroidX / Compose | https://android.googlesource.com/platform/frameworks/support | Apache-2.0 |
| Coil | https://github.com/coil-kt/coil | Apache-2.0 |
| OkHttp | https://github.com/square/okhttp | Apache-2.0 |

0.1.4 Windows 工具版本：yt-dlp 2026.08.19、FFmpeg N-126497-g5b614efc7e（20260911）、Deno 2.9.6。工具由上游 GitHub SHA256 驗證；未修改上游二進位。包內包含 FFmpeg-LICENSE.txt、Deno-LICENSE.txt 及 DotNet-NOTICES.txt。

對應來源與建置入口：

- yt-dlp：[2026.08.19 原始碼](https://github.com/yt-dlp/yt-dlp/tree/2026.08.19)，Windows packaging 見該版本 bundle 與 devscripts。
- FFmpeg：[5b614efc7e 原始碼](https://github.com/FFmpeg/FFmpeg/tree/5b614efc7e)，[BtbN 對應建置](https://github.com/BtbN/FFmpeg-Builds/tree/autobuild-2026-09-11-13-20)。依該建置樹的 README 執行 makeimage.sh win64 gpl 及 build.sh win64 gpl；所有啟用函式庫的來源及建置步驟位於 scripts.d/。
- Deno：[v2.9.6 原始碼及授權](https://github.com/denoland/deno/tree/v2.9.6)。
- Android 的 youtubedl-android 與相依版本見 android/app/build.gradle.kts；上游 Gradle / FFmpeg 建置腳本位於前述專案。

本 App 原始碼與建置腳本可在同一 Releases 的原始碼包下載。

## 0.2.4 音訊指紋

- Android 本機 Chromaprint 1.5.1：[上游版本](https://github.com/acoustid/chromaprint/tree/v1.5.1)，MIT；內含 KissFFT（BSD-3-Clause）及 FFmpeg avresample（LGPL-2.1-or-later）。完整來源及版權聲明在 `android/app/src/main/cpp/vendor/chromaprint-1.5.1`，JNI 與 CMake 建置入口在相鄰目錄。Android NDK 27、CMake 3.22.1，建置四種 ABI。APK assets/licenses 保留授權聲明。
- Windows 使用現有 GPL FFmpeg 的 Chromaprint muxer，無額外常駐服務。
- [MusicBrainz Web Service](https://musicbrainz.org/doc/MusicBrainz_API) 用於公開中繼資料查詢；[AcoustID](https://acoustid.org/webservice) 用於指紋查詢，需使用應用程式 client key。此版本不附帶第三方應用程式或短期測試 key。
