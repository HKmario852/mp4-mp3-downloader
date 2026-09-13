# 全能影音下載器 — 實作紀錄

## 已確認範圍
Windows 可攜 WPF App、Android Compose 通用 APK、MV3 Chrome/Brave 擴充功能。Windows 以使用者提供的深色三欄畫面為視覺依據，Android 延續色彩與操作流程。

## 平台校正
- Cookie 僅經 Native Messaging + current-user Named Pipe 傳輸，不進 URI、命令列、歷史資料庫或日誌。協定保留非敏感喚醒功能；native host 可直接啟動鄰近 App，資料夾移動後必須先手動啟動 App 一次才有機會校正登錄路徑。
- window.blur 不是接收 ACK；成功勾號由主程式持久化接收回覆觸發。複合網址 ACK 表示等待確認，沒有任何下載開始。
- Windows 不保證允許背景程式搶前景；嘗試 Activate / SetForegroundWindow，若系統拒絕則閃動工作列，持續保持 PendingChoice，絕不自動下載。
- SAF 跨卷沒有通用原子搬移保證。使用目標暫存文件、串流複製、長度/雜湊驗證、重新命名、最後刪來源。未知容量需手動同意。
- MP3 格式最多雙聲道；不主動指定 -ac，來源多聲道無法原樣保存在 MP3，介面需說明轉碼限制。
- MusicBrainz 是元資料社群資料庫，封面取自 Cover Art Archive，沒有官方或必然正確保證；精確比對才採用，否則只用影片縮圖。
- ID3 APIC 沒有標準 Secondary Cover 類型，第二封面使用 Other (0) + description=Video thumbnail；第一封面 Front Cover (3)。圖片原始位元組保留於 APIC；外部 cover.jpg 如來源非 JPEG，需另外轉 JPEG。
- Android content:// 不是實體路徑，不能交給 MediaScanner；由文件提供者管理。實體 MP3 才 scanFile，.covers 永遠排除。
- 可攜更新預設使用目前使用者權限，只在實際需要時提權；更新包 SHA256 必須來自可信發布資訊，不能將同包自帶雜湊當驗證。
- Title 空白仍可保留空 ID3 frame，但無法產生空檔名，因此保留原檔名；非空 Title 才重新命名。檔名碰撞加序號，從不附加 Artist/Album。

## 官方參考
- https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
- https://developer.chrome.com/docs/extensions/reference/api/cookies
- https://developer.android.com/training/data-storage/shared/documents-files
- https://developer.android.com/develop/background-work/services/fgs/timeout
- https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting
- https://coverartarchive.org/
- https://github.com/yt-dlp/yt-dlp
- https://github.com/yausername/youtubedl-android

## 驗收原則
編譯、單元測試、桌面實測、Android 裝置實測及登入影片驗證分別記錄，不以模擬資料代替功能完成。
