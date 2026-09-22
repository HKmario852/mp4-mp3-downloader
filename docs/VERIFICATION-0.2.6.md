# 0.2.6 驗證

- Core 70 項測試通過，包含：完成下載時檔案已可用、背景 MusicBrainz 標籤與封面匯入、外部檔案改動後拒絕覆蓋、舊設定遷移。
- Windows 設定頁實際渲染並測試模式切換與儲存；測試截圖位於 `artifacts/qa-v26-settings/settings-metadata.png`（本機產物，不在發佈 ZIP）。
- Windows 完整流程測試實際產生 MP3 與 MP4，兩項任務都進入「已完成」；MP3 的背景辨識當時仍在進行，沒有阻住檔案可用（`artifacts/qa-v26-full/download-smoke.json`）。
- Windows 既有下載庫與標籤編輯視圖回歸測試通過；Android 四 ABI APK 與 JVM 測試建置成功，本版 Android 功能未變。
- 公開影片在當前 VPN 網絡下首次分析耗時 21.4 秒，同一連結進入下載任務時分析耗時 0.000187 秒（`artifacts/qa-v26-cache/cache-checks.json`）。另一條公開 YouTube 來源單次分析耗時 23.1 秒；未更改 VPN 路由，故沒有 VPN 開／關的受控對照。
- 發布時核對 Windows／Android 套件雜湊，以及從公開發布頁重新下載較小的來源壓縮檔與 SHA256SUMS。大型套件沒有完整公開重新下載，不以此聲稱通過。
