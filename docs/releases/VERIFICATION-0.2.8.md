# 0.2.8 驗證

- 官方 yt-dlp 2026.08.19 Android zipimport 檔與 GitHub 發行資產 SHA256 相符：`1fa6733c37ea6fb51c99ad8fe785e7b7e5f3246c9b980230329d4fb72ed8d4d6`。
- Android `assembleDebug assembleDebugAndroidTest testDebugUnitTest` 通過；測試包括舊引擎版本升級及較新版本不降級。APK 內 `res/raw/ytdlp` 的 SHA256 與官方資產相符。
- Core `dotnet test` 通過，包括年齡限制錯誤提示。
- 未用使用者帳號 Cookie 測試受限影片；成功播放及下載仍取決於帳號、影片授權、地區及網站回應。
