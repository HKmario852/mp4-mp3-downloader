# 0.2.7 驗證

- Core：`dotnet test tests/Core.Tests/Core.Tests.csproj -c Release --no-restore`，71 通過、0 失敗。包括 MP3 不要求獨立縮圖、MP4 保留縮圖功能、背景辨識後只嵌入封面。
- Windows：`dotnet run --project tests/Windows.Smoke -c Release -- artifacts/qa-v27-ui-2 artifacts/windows-win-x64 --v27-regression` 通過；英文網址與任務搜尋提示、提示與游標垂直對齊（中心差約 0.001 px）、最新 MP3 排在最上方。已檢視正常視窗尺寸截圖。
- Windows：`--v24-regression` 通過；多首 MP3 貼上封面並儲存後，檔案各有內嵌封面，目的地沒有 JPG。
- Windows：實際下載測試完成一首 MP3 和一段 MP4；輸出資料夾只包含音訊與影片檔，沒有 JPG。測試來源不提供可用封面，因此此項只驗證目的地不會產生旁邊 JPG；內嵌封面由上述標籤與背景辨識測試驗證。
- Android：`Build-Android.ps1` 成功執行 `assembleDebug assembleDebugAndroidTest testDebugUnitTest`，包括 MP3 不要求獨立 JPG 的測試。未在實體 Android 裝置進行下載操作。
