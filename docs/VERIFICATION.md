# 驗證紀錄 — 2026-09-13

本交付包含可編譯原始碼、Windows x64 可攜式測試包、Android 通用 Debug APK、MV3 擴充功能。原始碼及 Releases 已在 HKmario852/mp4-mp3-downloader 公開發布；Windows 未簽署，Android 為測試 APK，擴充功能尚未發布瀏覽器商店。

## 0.1.4（2026-09-14）

- 正式安裝更新驗收：從使用者正在執行的 0.1.3 設定頁填入官方倉庫並下載更新，SHA256 與公開資產吻合；使用者結束舊版並重開後，實際行程與介面確認 0.1.4、「移除」按鈕及原有暫停任務。更新前後皆為 11 筆任務、7 筆可見歷史，設定與群組資料雜湊一致；任務資料因啟動時重設暫停／登入提示而不宣稱逐位元相同。此驗收涵蓋 0.1.3 的手動退出／重開遷移，不代表已驗證 0.1.4 下一次更新的全自動重開。
- C# 35 項測試通過，包括暫停／失敗／待選擇移除、完成檔案保護、ID3 實體 frame 第一封面及第二封面資料驗證。
- 擴充功能 Service Worker 7 項測試及 Chromium 隔離 DOM 6 項測試通過；新增 SPA sender 舊網址、index/t 變動、私人清單 URL、Cookie 查詢期間導航拒絕。
- Android 0.1.4 通用 Debug APK 編譯及 9 項 JVM 測試通過。Windows self-contained x64 編譯通過。
- 更新器測試包含 SHA256、路徑、架構、衝突拒絕及隔離資料夾實際覆蓋／保留 data 和 native-host.json／刪除 ZIP。測試未對使用者正在運行的 App 執行覆蓋。
- 使用者手動重新載入後，真實 Brave 按鈕 → Native Messaging → App 接收成功，資料庫確認 HadCredentials=true 及 PendingChoice，沒有擷取或輸出 Cookie 值。使用者選擇單片後，YouTube 仍回 Video unavailable；穩定版及隔離官方 nightly 均未解決測試影片。此結果不等於私人影片下載成功。
- 公開 v0.1.4 最新 Release 可匿名讀取，五項上傳資產 SHA256 與本機一致；Windows ZIP 通過全 PE、路徑及雜湊檢查。

## 0.1.3 歷史與 MusicBrainz 驗證（2026-09-13）

- C# 核心 33 項測試通過，包括缺歌手查詢、Unicode 標題、重複 recording、多 release 封面後備、歧義拒絕、服務不可用、任務狀態及單檔刪除保護。API 配對與封面成功分支使用模擬 HTTP 回應。
- `artifacts/qa-013-library/update-checks.json`：11 組新 WPF 檢查通過；深色確認、取消不動作、篩選快照不影響後來新增紀錄、清空保留檔案、單一檔案刪除、完成移出及失敗分頁。原生資源回收筒 API 僅以自行產生的測試檔案驗證。
- `artifacts/qa-013-ui/ui-checks.json` 8 組、`artifacts/qa-013-library-regression/library-checks.json` 10 組既有 WPF 檢查通過；含真實 MP3 標籤保存與重新命名。
- `artifacts/qa-013-download/download-smoke.json`：真實 yt-dlp / FFmpeg 完成公開 Big Buck Bunny MP4、MP3；MP3 開啟 MusicBrainz，查詢時間及「查無可靠配對」狀態已保存。這是電影範例，無配對不代表音樂庫錯誤。
- 真實 MusicBrainz 歌曲查詢曾回 HTTP 503 及「web server is currently busy」；服務可用性會變動，不將模擬封面結果宣稱為真實歌曲配對成功。
- Android 0.1.3 APK 與 9 項 JVM 測試通過。新增 Android 單檔刪除及 API 配對的 Activity／SAF 真機流程仍未驗收。
- 使用者正在運行的舊 App、下載檔案與個人資料未被覆蓋；新版本須退出系統匣後自行更新。

API 行為依據：[MusicBrainz 限速及 User-Agent](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)、[Cover Art Archive API](https://musicbrainz.org/doc/Cover_Art_Archive/API)。每次 MP3 查詢以 MusicBrainz 設定已開啟為前提，不會擅自開啟雲端查詢。

## 0.1.2 檔案管理驗證（2026-09-13）

- C# 核心 26 項測試通過：格式 ID 與容量一致、原生格式偏好順序、4K 上限、儲存目錄向後相容、接收時目錄快照、標籤後重載記錄、格式篩選與清空不刪實體檔案。
- `artifacts/qa-library/library-checks.json`：10 組 WPF 功能檢查通過。由 FFmpeg 產生 2 秒測試 MP3，經真實 TagWindow 修改 Title／Artist、檔案重新命名、固定音軌批次保存、驗證未修改 Title 不重新命名、引擎路徑更新、主／最近下載選取、搜尋與格式篩選、分開目錄、來源品質選單。SHOpenFolderAndSelectItems 對測試檔案回傳成功；不宣稱已目視驗證使用者原本下載檔案的 Explorer 選取。
- `artifacts/qa-012-ui/ui-checks.json`：原有 8 組 WPF UI 行為檢查仍通過，包含深色選取、完整縮圖、收合圖示、網址、最小化及關閉。
- `artifacts/qa-012-download/download-smoke.json`：實際 yt-dlp / FFmpeg 下載公開 Big Buck Bunny MP4、轉碼 MP3，兩者均完成。未使用私人 Cookie。
- Android APK 0.1.2 建置與 7 項 JVM 測試通過；本次新增格式篩選、目錄選擇的 Activity／SAF 實機互動未驗收。
- 新版 Windows 成品已建立；仍在運行的使用者舊 App 未被強制關閉或覆蓋。

格式選擇依據：[yt-dlp format selection](https://github.com/yt-dlp/yt-dlp#format-selection)。檔案總管選取使用 [SHOpenFolderAndSelectItems](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems)。估算不保證包含容器或 ID3 圖片開銷；來源未提供 filesize 時仍依 bitrate × duration 估算。

## 0.1.1 介面修正驗證

- C# 核心 22 項測試通過，新增 MP3 位元率估算、MP4 高度選擇與音訊加總、已混流檔案不重複計算、未知容量處理。
- 6 項隔離 Chromium 頁面測試通過：裁切容器外可點擊選單、CSS 隔離、ACK 及 1.5 秒復原、備用掛載與 SPA 去重、原生失敗提示、未登入拒絕、複製節點重新掛載、擴充功能 context 失效時引導重新整理。Chrome API 以測試替身提供，未使用私人 Cookie；這不是 Mario 的真實 Brave 登入流程實測。
- 原有 Service Worker 的 4 項測試通過。
- 真實 WPF 渲染與行為檢查通過：完整網址、品質改動更新估算、完整縮圖、深藍選取、收合圖示、捲軸、最小化保留可見視窗狀態、關閉隐藏至系統匣。結果位於 `artifacts/qa-ui-fixes/ui-checks.json`，畫面中的四色縮圖及任務為明確標示的測試資料。
- Windows 0.1.1 重新發布；已同步本工作區中解壓的擴充功能資料夾。Brave 需要重新載入擴充功能及重新整理影片頁才能執行新程式。使用者目前運行中的舊 App 未被強制終止或覆蓋。

容量計算參考來源欄位：[yt-dlp format metadata](https://github.com/yt-dlp/yt-dlp#filtering-formats)。優先使用 filesize / filesize_approx，其次 bitrate × duration；輸出容器、封面及來源變動會令實際大小不同。

## 已完成的驗證

| 項目 | 結果與邊界 |
| --- | --- |
| 最終封裝 | Windows 成品 ZIP 通過 SHA256、路徑與全 PE 架構檢查；原始碼包排除本機資料及建置產物；APK 確認 arm64-v8a / armeabi-v7a / x86 / x86_64 |
| Windows 編譯 | .NET SDK 9.0.301 編譯 net8.0；WPF App / Native Host self-contained win-x64 發布成功 |
| Android 編譯 | JDK 21、SDK 35、Gradle 8.11.1；assembleDebug 成功，APK 內含四種 ABI |
| C# 核心測試 | 18 項通過：URI/輸入驗證、Unicode、Cookie 換行拒絕、UTF-8 framing、大小限制、EMA、AND 搜尋、ID3 空欄位/多實例、批次 Track、保留檔案、播放清單明確選擇及去重 |
| Android JVM 測試 | 6 項通過，報告在 artifacts/android-test-results |
| 擴充功能單元測試 | 4 項通過：Cookie 同意及隔離、過時 SPA 請求拒絕、原生接收失敗不回報成功；使用模擬 chrome API，沒有宣稱完成真實 DOM 驗證 |
| 更新器測試 | 6 項通過，使用 Windows PowerShell：有效封裝、錯誤架構、ZIP traversal、拍平名稱碰撞、使用者資料保護、錯誤 SHA256 |
| Windows 實際下載 | 公開 Big Buck Bunny 範例分別完成 MP4 下載及 MP3 轉碼；下載器執行真實 yt-dlp / FFmpeg |
| 成品音訊 | FFprobe 確認 MP3 320000 bit/s、44100 Hz、2 channels；來源 MP4 為 H.264 640×360 / AAC |
| Native Messaging / IPC | 實際 Omni.NativeHost.exe stdio → current-user Named Pipe → SQLite 接收 ACK；複合網址保持 PendingChoice；重送不重複；合成測試 Cookie 不進歷史 |
| Android 原生執行 | API 32 模擬器安裝成品 APK，1 項 instrumentation 通過：實際下載 MP4、轉碼 MP3、檢查結果檔案與標籤。測試直接驅動引擎，不涵蓋 Activity / 前景服務生命週期 |
| Windows 介面 | 用真實 WPF 元件 RenderTargetBitmap 輸出並目視檢查三欄介面、深色選單、最近下載；不是手繪或 HTML 替代。此方式不驗證 OS 焦點、系統匣或 Toast 實際點擊 |

最新下載與橋接證據在 `artifacts/qa-final-download/`；Android 原生結果在 `artifacts/android-native-test.txt`。較早的 `qa` / `qa-download` 包含排錯歷史，並非本次最終驗收結果。

## 仍需整合驗收

### 圖示更新驗證

2026-09-13：加入原創紫藍播放／下載圖示。Windows 重新發布成功，從實際 App.exe 擷取圖示並目視確認，WPF 主頁渲染確認新品牌圖；Android 重新編譯成功及 6 項 JVM 測試通過，aapt 確認 APK launcher 指向 adaptive icon。擴充功能 4 項測試通過，manifest 所列 PNG 尺寸逐一核對。未把 APK launcher 資源驗證宣稱為所有手機桌面實際顯示驗證。原圖、生成方式及提示詞見 assets/branding/README.md。

### 待驗場景

- 真實 Chrome / Brave 載入擴充功能、YouTube 現行 DOM、登入 Cookie、受限影片、首次原生授權對話框、瀏覽器冷啟動及前景焦點仲裁。
- Windows 真實 Toast 深度路由、使用者搬移綠色版後自癒、檔案被其他程式鎖定時重試/跳過、更新中断與 rollback 壓力測試。更新器已有自動化驗證，未對使用者正在運行的正式版本執行替換。
- Android Activity 視覺與點擊流程、Android 15+ dataSync 時限、Wi-Fi/行動數據切換、通知 Action、SD card / 雲端 SAF 權限與容量、OEM 衍生行程終止。API 32 引擎測試不可替代這些驗收。
- MusicBrainz / Cover Art Archive 實際匹配及多封面播放器相容性、完整播放清單的失敗彙總與長時間壓力測試。
- ID3 擴充標頭與 unsynchronisation 變體目前會安全拒絕；任意原始 frame 透過進階 Base64 JSON 編輯，並非完整 MP3Tag 產品的全部專用欄位控制器。
- GitHub 原始碼倉庫已建立；Releases、自動更新私人倉庫認證、正式 APK 簽署、Windows 簽章、第三方二進位對應來源封裝尚未配置。端側 ONNX 音樂模型尚未提供。

## 重跑方式

```powershell
dotnet test tests/Core.Tests/Core.Tests.csproj -c Release
node --test tests/extension.test.mjs
python tests/test_updater.py
./scripts/Build-Windows.ps1
./scripts/Build-Android.ps1 -JavaHome 'C:\path\to\jdk21' -AndroidHome 'C:\path\to\sdk'
./scripts/Test-Android.ps1 -DeviceId '<adb device id>' -AndroidHome 'C:\path\to\sdk'
./scripts/Package.ps1
```

Windows 下載煙霧測試會下載公開範例到指定輸出目錄；勿用真實私人 Cookie 作為測試資料。

```powershell
dotnet run --project tests/Windows.Smoke -c Release -- artifacts/qa-check artifacts/windows-win-x64
```
