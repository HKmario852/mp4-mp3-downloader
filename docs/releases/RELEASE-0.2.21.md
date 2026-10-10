# OMNI 0.2.21 — yt-dlp 自動更新及正式簽署 Android 版

- **yt-dlp 自動更新**：網站改版令舊版 yt-dlp 無法下載時，唔使再等 OMNI 出新版。Windows 及 Android 每日檢查一次 yt-dlp 新版本（設定 › 關於 ›「每日自動更新 yt-dlp」，預設開啟），亦可按「立即更新 yt-dlp」。每次下載都要符合 GitHub 提供的 SHA256 先會使用；Windows 版存放喺 data 資料夾，之後安裝 OMNI 更新都唔會換返舊版。
- **Android 改為正式簽署版本**：唔再係 debug 測試版，執行更快。由於簽署金鑰不同，**已安裝舊版（debug）嘅用戶需要先解除安裝一次**再安裝 `OmniDownloader-universal.apk`；App 內更新會偵測並說明。共用下載資料夾入面嘅檔案會保留，App 內下載紀錄不會。
- Windows 更新包不再附帶約 40 份舊版本說明；各版本紀錄改喺 GitHub 的 CHANGELOG 查看。

Windows 用戶照常喺 App 內「檢查更新」並安裝。完整說明見 [CHANGELOG](https://github.com/HKmario852/mp4-mp3-downloader/blob/main/CHANGELOG.md)。
