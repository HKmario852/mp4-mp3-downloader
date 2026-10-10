# 0.2.12 驗證

更新器測試 9/9 通過：發布 ZIP 校验、架構錯誤、路徑越界、重複檔名、禁止包含使用者資料、錯誤 SHA256、正常安裝保留 data 與 native-host.json、唯讀未變 NativeHost 保留內容及修改時間、同長度但不同內容的 NativeHost 正確更新。

Windows Release App 已建置成功。本版 NativeHost 與 0.2.10／0.2.11 相同，未修改防毒設定。

Android APK 建置及 22/22 單元測試通過。在使用者原安裝資料夾執行修正版更新器成功；App 版本已為 0.2.12，SQLite 下載紀錄數量及偏好設定 SHA256 均保持一致，NativeHost SHA256 未改變，安裝的 updater.ps1 與修正版相同。
