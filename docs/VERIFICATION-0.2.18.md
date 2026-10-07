# 0.2.18 驗證 — 2026-10-08

## 問題重現

使用隔離 Store、合成 MP3 及自行產生的純色封面，沒有讀取使用者的歌曲或 Cookie。

- 原版失敗任務測試確實失敗：Button.Content 已是「失敗任務（1）」，實際渲染的 TextBlock 仍是「失敗任務（0）」。翻譯程式改寫 ContentPresenter 的文字，令文字綁定失效。
- 原版首次顯示測試確實失敗：歌曲列表在讀取 ID3 完成前顯示了測試用的過期標題。

## 修正驗證

- 真實 WPF 元件檢查失敗數量 0 → 1 → 0、移除任務、搜尋不影響總失敗數量、失敗頁面的返回文字、中文及英文。檢查實際渲染文字，而非只檢查 Button.Content。
- 監察標籤頁面的 LayoutUpdated，包括首次進入及重新進入：沒有出現過期標題或歷史縮圖；首次顯示歌曲列時已使用磁碟標籤和內嵌封面。背景重新讀取不覆蓋未儲存草稿。
- 開啟標籤編輯前後，合成 MP3 的 SHA256 及隔離下載歷史保持不變。
- 資料夾保存回歸通過，包括真正第二個程序重開、新增／改名檔案及去重。
- 播放器 18 項回歸通過，包括原生 MP3 播放、直接跳播及音量調整、跨頁面持續播放、返回編輯時共用播放狀態。
- 在正常 1440 × 940 視窗渲染並目視檢查下載失敗及標籤編輯畫面。這是 WPF RenderTargetBitmap 驗證，不宣稱已在使用者的正式歌曲資料上操作驗收。

重跑新增檢查：

```powershell
dotnet run --project tests/Windows.Smoke -c Release -- artifacts/qa-failed-count artifacts/windows-win-x64 --failed-count-regression
dotnet run --project tests/Windows.Smoke -c Release -- artifacts/qa-tag-first-frame artifacts/windows-win-x64 --tag-first-frame-regression
```

本次修改 Windows 介面及主程式名稱。Windows x64 自含式程式已建置；下載工具沿用現有版本，NativeHost 重新建置以支援 `OMNI.exe`。

## OMNI 主程式名稱

- Windows 組件及 Product/Title 改為 OMNI；主視窗、系統匣及 AcoustID 頁面的圖示使用新組件資源路徑。
- 87 項 Core 測試通過，包括新版及舊版共存時優先選取 `OMNI.exe`、舊安裝後備及缺少主程式時停止啟動。
- AcoustID 核對頁面 WPF 回歸通過，新組件名稱下的主視窗及核對頁面資源正常載入。
- 新版更新器支援 `App.exe` → `OMNI.exe` 遷移，保留歷史及瀏覽器本機設定；新更新包拒絕包含舊主程式名稱。舊版內置更新器本身不會因此獲得新檔名支援，第一次需手動更新或直接使用新更新器。
- 六份 README 及開發文件的 205 項相對路徑檢查通過。
- 更新器 11 個案例完成驗證，涵蓋新名稱安裝、舊名稱遷移、資料保存、唯讀且未改動的橋接保留、摘要／架構／路徑及舊主程式拒絕。唯讀橋接案例首次超過既有 30 秒限制，单独重跑於 13.7 秒通過；沒有放寬逾時限制。
