# 0.2.0 驗證紀錄

2026-09-15；Windows x64、Android API 36 模擬器。

- C# 核心：43 項測試通過。包含新格式／有損位元率、跨午夜限速、1–10 併發驗證、原生語言命名及重名檔案重新命名後同步紀錄。
- 擴充功能：7 項背景模組測試及 6 項隔離瀏覽器測試通過。沿用 0.1.4 擴充功能，未變更私人登入傳送協定。本次未重新聲稱驗證使用者私人清單。
- Android JVM：14 項測試通過，包括舊設定反序列化、新格式參數及命名。
- Android 儀器測試：2 項通過。操作六分類設定入口、深淺色儲存、未儲存離頁提示及已下載入口；實際下載／轉換 MP4、MP3、M4A、FLAC、WAV、MKV。
- Windows 實際下載：同一公開測試片源成功輸出上述六種格式。WebM／指定影片編碼採來源選取；本次未以無相容串流的片源冒充 WebM 成功。
- Windows 畫面驗證：已下載八欄、多選啟用移除、App 內設定、六分類切換、深淺色渲染、英文設定儲存後即時更新。測試縮圖採本機測試圖，不是使用者下載紀錄。
- 更新器：7 項測試通過，含 SHA256／PE／ZIP 路徑檢查、隔離安裝目錄實際更新、保留測試資料及清理 ZIP。

Windows 產物版本為 0.2.0.0；Android versionName 0.2.0 / versionCode 6。使用者現有 Windows 安裝資料夾未由建置流程覆蓋。此紀錄區分測試環境更新及使用者親自確認的正式更新，並不代表使用者已安裝 0.2.0。

Android 的系統垃圾桶及 SAF 目錄操作仍受儲存提供者能力限制；APK 安裝仍需系統確認。背景開機限制依 [Android 官方文件](https://developer.android.com/about/versions/15/behavior-changes-15)，垃圾桶授權依 [MediaStore API](https://developer.android.com/reference/android/provider/MediaStore)。
