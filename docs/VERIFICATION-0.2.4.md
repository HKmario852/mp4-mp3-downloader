# 0.2.4 驗證範圍

- Core：64 項測試通過，包含弱指紋結果、歌曲長度差異、專輯歧義、release 所屬關係、缺少 client key 及標籤欄位。
- Windows：Release 編譯通過；自動化驗證檔案／PNG／BitmapSource 封面輸入、儲存前不寫檔、批次儲存及手動封面鎖定。已檢視統計卡片及封面預覽截圖。
- Android：四 ABI 通用 APK 編譯及 JVM 測試通過。模擬器三項原生測試通過：本機指紋與 Windows FFmpeg 對同一測試 WAV 的結果一致、缺少 key 提前結束、封面尺寸政策。
- 線上 MusicBrainz：recording cb39b5d8-ebb8-4bad-9f17-9d952108ecb7 成功取得 ENDROLL -HaThA- / SennaRin / LOSTandFOUND、2026、曲目 7、光碟 1，並取得 4000×4000 專輯封面。

限制：未有正式 AcoustID application key，尚未驗證真實 Scan 線上配對。剪貼簿測試使用實際處理函式及不同資料表示，不能等同所有來源 App 的原生貼上或實體滑鼠拖放驗證。Android 原生測試在 x86_64 模擬器執行，其他 ABI 已編譯但未逐一在實機執行。
