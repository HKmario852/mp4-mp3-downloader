# OMNI 0.2.20 — 修復 Windows 更新包及管理歌曲來源

- 修正 0.2.19 Windows 更新包的重複文件，解決 `Flattening collision: ARCHITECTURE.md`。更新包改回舊更新器相容的單層結構，保留全部第三方授權文件。
- 發布前直接用已安裝 0.2.18 的更新器，測試完整 Windows ZIP 的驗證及安裝，確認設定、下載紀錄及瀏覽器本機設定保留。
- 標籤編輯新增「管理已加入來源」，顯示已保存資料夾／檔案的完整路徑。移除來源不會刪除歌曲或下載紀錄，列表即時更新，重開後亦保留移除結果。
- MP3、Opus、M4A、FLAC 及可選 ID3v2.4 功能保留。Android APK 版本編號同步為 0.2.20，仍為 debug 測試版。

如上一版更新失敗，關閉錯誤提示後重新「檢查更新」，下載並安裝 0.2.20。若仍在使用 `App.exe`，首次轉用 `OMNI.exe` 需要完全退出 App 後手動解壓完整 Windows ZIP，保留 `data/` 及 `native-host.json`。

完整文件、截圖及翻譯可在 GitHub 查看；Windows ZIP 內 README 的連結指向此版本的線上文件，避免舊更新器攤平資料夾後造成重複檔名。
