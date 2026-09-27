# 0.2.9 驗證

- Android `assembleDebug assembleDebugAndroidTest testDebugUnitTest` 成功；單元測試覆蓋舊引擎檔案雜湊、官方資產雜湊，以及不覆蓋未知較新版本。
- 在隔離的 Android 實機測試 App 中，先放入與 0.2.7 相同的 `2025.11.12` 引擎檔，再啟動 App；下載引擎初始化成功，安裝後引擎 SHA-256 為官方 2026.08.19 資產的 `1fa6733c37ea6fb51c99ad8fe785e7b7e5f3246c9b980230329d4fb72ed8d4d6`。測試 App 隨後已移除，沒有改動現有安裝。
- 尚未在回報問題的平板上實測；需要使用者安裝 0.2.9 後確認。
