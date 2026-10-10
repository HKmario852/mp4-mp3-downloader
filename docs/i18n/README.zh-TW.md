<p align="center"><img src="../../docs/assets/logo.png" alt="Omni Downloader" width="88" /></p>
<h1 align="center">Omni Downloader</h1>
<p align="center">下載影音、管理檔案、編輯音訊標籤，並核對音樂辨識結果。</p>

<p align="center">
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases/latest"><img src="https://img.shields.io/github/v/release/HKmario852/mp4-mp3-downloader" alt="Latest release" /></a>
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases"><img src="https://img.shields.io/github/downloads/HKmario852/mp4-mp3-downloader/total" alt="Release downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-5865F2" alt="Windows and Android" />
  <a href="../../LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue" alt="GPL-3.0-or-later" /></a>
</p>

<p align="center"><a href="../../README.md">English</a> · <strong>繁體中文</strong> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.es.md">Español</a></p>

<p align="center">
  <a href="../../docs/screenshots/windows-downloads.png"><img src="../../docs/screenshots/windows-downloads.png" alt="下載任務" width="49%" /></a>
  <a href="../../docs/screenshots/windows-tag-editor.png"><img src="../../docs/screenshots/windows-tag-editor.png" alt="音訊標籤編輯" width="49%" /></a>
</p>

*Windows 截圖使用合成示範媒體與原創幾何封面。點擊可放大。*

<details>
<summary>更多截圖</summary>

![已下載檔案](../../docs/screenshots/windows-library.png)
![設定](../../docs/screenshots/windows-settings.png)

</details>

## 功能

- 📥 使用 yt-dlp 下載影片與播放清單，支援排隊、暫停、續傳及任務管理。
- 🎞️ 可選 MP4、MKV、WebM、MP3、Opus、M4A、FLAC 或 WAV，實際選項取決於來源。
- 🏷️ 批次編輯 MP3、Opus、M4A 及 FLAC 標籤，內嵌近正方形封面，不另存 JPG 檔案。
- 🔎 匯入前核對 AcoustID／MusicBrainz 配對，只套用選取欄位，並可保留復原記錄。
- 🎧 Windows 會記住加入的音樂資料夾，切換頁面時繼續播放預覽音訊。
- 🌐 透過瀏覽器擴充功能，將 Chrome 或 Brave 的 YouTube 連結傳到 Windows App。

## 下載／安裝

請到 **[最新版本頁面](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)** 下載。

| 平台 | 系統需求 | 安裝方式 |
| --- | --- | --- |
| Windows | Windows 10（2004+）或 11，x64 | 將 Windows ZIP 解壓到可寫入的資料夾，執行 `OMNI.exe`；保留所有隨附檔案。 |
| Android | Android 8.0+（API 26） | 安裝通用 APK。選擇共用資料夾，避免解除安裝時一併刪除下載檔案。 |
| 瀏覽器擴充功能 | Windows 上的 Chrome／Brave | 載入解壓後的擴充功能，並在 App 設定連接其 ID。[設定步驟](../../docs/DEVELOPMENT.md#browser-connection)。 |

> **注意:**
> Windows 執行檔未簽署。由 0.2.21 起 Android APK 為已簽署的正式版本；如曾安裝較早的測試（debug）版本，請先解除安裝一次再安裝，因為 Android 不能跨簽署金鑰更新。實機及背景服務驗證尚未完整。
>
> 兩個平台都會在版本之間自動更新 yt-dlp（設定 › 關於，預設每日檢查），並以 GitHub 提供的 SHA-256 核對下載。

> [!NOTE]
> 舊版使用 `App.exe`。首次改用新名稱時，請完全退出 App，將整個 Windows 更新包解壓到原資料夾，保留 `data/` 和 `native-host.json`，然後啟動 `OMNI.exe`。

## 快速開始

1. 在 **新增下載** 貼上影片或播放清單網址，按 **分析連結**。
2. 選擇格式、品質與儲存位置，開始下載。
3. 在 **已下載** 查看完成的檔案。前往 **標籤編輯** 加入 MP3、Opus、M4A、FLAC 或資料夾，修改標籤與封面。
4. 辨識歌曲時，選取一個支援的音訊檔並按 **Scan 音訊辨識**。核對候選版本與變更後，只有明確套用的欄位與封面才會寫入。

## 從原始碼建置

在 Windows 安裝 **.NET 8 SDK 或更新版本**，然後於 PowerShell 執行：

```powershell
git clone https://github.com/HKmario852/mp4-mp3-downloader.git
cd mp4-mp3-downloader
./scripts/Build-Windows.ps1
```

輸出：`artifacts/windows-win-x64/OMNI.exe`。腳本會下載並驗證隨附的媒體工具；發布版已包含 .NET 執行環境。

建置 Android 還需要 **JDK 21**、**Android SDK／build-tools 35**、**NDK 27.0.12077973** 及 **CMake 3.22.1**。設定 `JAVA_HOME` 與 `ANDROID_HOME` 後執行：

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

輸出：`artifacts/OmniDownloader-universal.apk`（以你的正式金鑰簽署；先執行一次 `./scripts/Setup-AndroidSigning.ps1` 建立）及 `artifacts/OmniDownloader-universal-debug.apk`。腳本亦會建置 instrumentation 測試及執行 JVM 單元測試。架構、檢查與打包方式見 [開發指南](../../docs/DEVELOPMENT.md)。

## 設定

- 在 **設定** 調整儲存位置、格式、品質、字幕與封面選項。獨立影片縮圖 **預設關閉**。
- 原生 Opus／M4A 保留來源的編碼音訊。MP3 可選 CBR 或 VBR V0；新 MP3 預設 ID3v2.3，可選 ID3v2.4。FLAC 不會提升有損來源的音質。[音訊管線](../../docs/AUDIO-PIPELINE.md)。
- Windows 預設在 **下載完成後** 於背景查找 MP3 標籤，可改為下載前或關閉。Android 自動標籤配對預設關閉，但封面後備流程仍可能查詢 MusicBrainz。
- App 提供 **繁體中文與英文** 設定，但翻譯尚未完整：部分畫面及瀏覽器擴充功能仍為中文。README 翻譯不代表新增介面語言。
- 受限制內容可能需要具備存取權帳號的 Netscape 格式 cookies 檔案，請勿分享。[瀏覽器連接](../../docs/DEVELOPMENT.md#browser-connection) · [音樂辨識](../../docs/MUSIC-RECOGNITION.md)。

## 技術架構

- **Windows：** C#、.NET 8、WPF、SQLite。
- **Android：** Kotlin、Jetpack Compose、SQLite、Storage Access Framework。
- **媒體與整合：** yt-dlp、FFmpeg、Deno、Chromaprint、MusicBrainz、AcoustID、JavaScript／Manifest V3。

## 隱私／免責聲明

設定及歷史記錄儲存在本機。下載會連接來源網站，更新檢查會連接 GitHub。音樂查詢會將標題、演出者、ID 或音訊指紋與長度傳送至中繼資料服務，**不會上傳音訊檔案**。瀏覽器 Cookie 轉發須先取得同意。詳見 [隱私說明](../../docs/PRIVACY.md)。

請只下載你獲准下載的內容。本專案不實作 DRM 解密，亦不隸屬於支援的網站。網站支援依賴 yt-dlp，網站變更可能令下載失效；無法保證來源持續可用或歌曲配對成功。

## 授權與致謝

本專案採用 **[GPL-3.0-or-later](../../LICENSE)**。感謝 yt-dlp、FFmpeg、youtubedl-android、Deno、Chromaprint、MusicBrainz、AcoustID 及 Cover Art Archive。上游來源與再散布義務見 [第三方聲明](../../THIRD-PARTY.md)。
