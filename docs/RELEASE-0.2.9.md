# mp4/mp3 downloader 0.2.9

修正 Android 0.2.8 可能顯示「下載引擎初始化失敗：Bundled yt-dlp version mismatch」而無法下載的問題。升級流程現在使用官方 yt-dlp 資產的 SHA-256 驗證檔案，不再依賴 Android 在初始化時解析 zipimport 內的版本字串。已知的 0.2.7 舊引擎會更新；已是 2026.08.19 或可能由使用者自行更新的較新引擎不會被降級。

此更新可直接覆蓋安裝 0.2.8，無需清除 App 資料。Windows 延續 0.2.8 的下載及年齡限制提示修正，版本號同步更新以配合 App 內更新檢查。
