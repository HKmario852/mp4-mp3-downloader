# 0.2.5 驗證

## 導航與寫入邊界

- Windows：MainWindow.ActiveView = acoustid-review，根 Content 換成 AcoustIdReviewView；原 AppRoot 與 TagEditorView 實例保留，返回時重新附加。沒有建立 Window、Modal 或覆蓋層。
- Android：頂層 pageFlow = acoustid-review，同一 TagEditorScreen 組合保留 remember 草稿及捲動狀態，內容切換為 AcoustIdReviewScreen，Scaffold 側欄及底部導航隱藏。已下載頁的標籤入口亦使用主導航。
- 明確按套用後，TagReview 將選取欄位交給既有 TagEditor 原子暫存／備份流程；封面需獨立選取。TXXX 描述欄位按名稱更新，不移除其他 TXXX。
- 套用及復原檢查檔案 SHA256，防止直接覆蓋已變更的檔案。備份保留於 tag-undo；臨時復原按鈕隨編輯器生命週期存在。

## 已驗證

- Core 67 項測試：選擇性寫入、特殊字元標題、Unicode TXXX、未選 frame／封面不變、音訊 bytes 不變、精確復原及外部變更衝突。
- Windows 自動化實際視圖：同一視窗導航、取消保留草稿、多候選、未確認不寫入、單一欄位套用、未選封面保留、返回草稿合併及逐位元復原。核對 1580×980 及 1050×980 視窗截圖。
- Android 四 ABI 編譯及 JVM 測試；x86_64 模擬器執行兩項 Compose 流程測試，檢查實際根路由、側欄隱藏、草稿／搜尋保留、只写勾選欄位及精確復原。檢視辨識頁截圖並修正文字對比。
- 正式 AcoustID 查詢：ENDROLL -HaThA- 得分 0.9939346；錄音 cb39b5d8-ebb8-4bad-9f17-9d952108ecb7、兩個 LOSTandFOUND 發行版本。只讀原音訊，未改檔。

## 範圍限制

介面候選切換、写入及復原測試使用可重現的服務回應與測試音訊；正式查詢另用使用者歌曲讀取驗證。AcoustID 收錄、網絡及封面供應會影響其他歌曲結果。Android ARM 裝置已編譯，未逐款實機驗證。永久復原記錄沒有額外的歷史瀏覽頁，備份可能佔用與 MP3 相同的空間。
