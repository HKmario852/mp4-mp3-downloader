Windows x64 免安裝版、Android 通用測試 APK、Chrome / Brave 擴充功能。

- 修正 YouTube SPA 舊網址及清單位置變動造成的「頁面已切換」誤報，支援播放清單頁按鈕。
- 「移除」支援暫停、失敗、等待選擇及進行中的任務；保留已完成檔案。
- MP3 預設 YouTube 影片縮圖為第一封面，MusicBrainz 專輯圖為第二封面。
- App 設定內下載更新、SHA256 驗證、暫停任務、退出、安裝及重開；保留設定與歷史。

**下載**：Windows 選 `OmniDownloader-windows-x64.zip`；Android 選 `OmniDownloader-universal-debug.apk`；瀏覽器選 `OmniDownloader-browser-extension.zip`。

**首次使用**：解壓 Windows ZIP 到可寫入的固定資料夾，開啟 App.exe。安裝擴充功能後，在 App 設定填入自己的擴充功能 ID，按「儲存並連接瀏覽器」。私人影片／清單必須在 YouTube 登入有權限的帳號，並在擴充功能設定同意向本機轉送登入 Cookie。Cookie 不會上傳 GitHub。

**由 0.1.3 更新**：設定的 GitHub 專案填 `HKmario852/mp4-mp3-downloader`，按「下載並安裝最新版本」。下載驗證後，從系統匣選「結束」，更新器完成後輸入 Y 重開；不用自行解壓。0.1.4 起退出及重開由更新流程處理。

**擴充功能更新**：覆蓋原擴充功能資料夾，在 Brave / Chrome 擴充功能頁重新載入，再重新整理 YouTube。這是手動載入版本，尚未上架瀏覽器商店。

**測試狀態**：35 項 C#、7 項擴充功能背景測試、6 項隔離瀏覽器測試、9 項 Android JVM 測試通過；更新器測試包括隔離目錄實際替換及個人資料保留。私人清單實際下載仍需使用者登入環境驗收，不能保證 YouTube 提供每個受限內容。Windows 未簽章；Android 是 Debug 測試版，未發布商店。

來源及授權：見原始碼的 LICENSE、THIRD-PARTY.md、建置腳本及上游來源。SHA256SUMS.txt 與 GitHub 每項資產的 digest 可用於驗證下載。
