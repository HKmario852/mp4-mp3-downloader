# 0.2.11 驗證

Windows 封面流程 smoke 測試使用實際 MP3 與已有 ID3 封面的圖片，驗證本機 PNG 拖放至現有封面的事件路由、HTML 圖片／data URI、圖片 URL、Unicode URL stream、虛擬 FileContents、PNG stream、file URI、沒有圖片副檔名的暫存檔、WebP 轉換、失敗保留草稿、編輯區其他位置的圖片拖放、預覽不寫檔、儲存後 APIC 封面更新、Unicode 標籤保留及無 JPG sidecar。已檢視正常視窗大小的儲存後畫面。

瀏覽器來源格式以測試資料及受控 HTTP 回應驗證，未在使用者的 Chrome／Brave 上執行真實跨程式拖放。受保護的圖片網址仍可能需要先另存圖片。

Core 測試 74/74 通過；Android 測試 22/22 通過，debug APK 建置成功。Windows Release App 建置成功。NativeHost 原始碼及其 IPC 協定未變更；本機單檔重新打包遇到存取拒絕，因此發布包沿用 0.2.10 的 NativeHost 二進位，來源 ZIP 已對照 GitHub 公開 asset SHA256 驗證，二進位 SHA256 為 `e5e630de2a8394e3c7672913e0555e8c14ce99e85c805f340482c2b903faeb0a`。
