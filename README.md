最新版本：[0.2.2](https://github.com/HKmario852/mp4-mp3-downloader/releases/tag/v0.2.2) · [更新內容](docs/RELEASE-0.2.2.md)

最新介面與標籤編輯更新： [0.2.1 更新紀錄](docs/RELEASE-0.2.1.md)。

# mp4/mp3 downloader

目前版本 **0.2.0**：已下載多選管理、縮圖／詳細資料、App 內六分類設定、主題及新增格式。[更新內容與平台限制](docs/RELEASE-0.2.0.md)。既有 0.1.4 可在設定內更新，無需手動解壓。

全能影音下載器 / Omni Downloader。GitHub 倉庫名稱：`mp4-mp3-downloader`。

Windows 可攜式 App、Android 原生 App，以及 Chrome / Brave Manifest V3 擴充功能。繁體中文深色介面，Windows 跟隨提供的三欄 UI 參考。

## 建置

### Windows
需要 .NET 8 SDK 或較新 SDK、Windows 10 2004+。

```powershell
dotnet test tests/Core.Tests/Core.Tests.csproj
./scripts/Build-Windows.ps1
```

輸出：`artifacts/windows-win-x64/App.exe`。App、Native Host、yt-dlp、ffmpeg、ffprobe、Deno 全部平鋪於同一目錄。建置腳本從上游 GitHub Releases 下載工具並驗證發布資產 SHA256。移動整個資料夾後，先手動啟動 App 一次，讓 HKCU 協定及 Native Messaging 絕對路徑自動更新。

工具不存在時 App 仍可啟動、編輯歷史及設定；下載會顯示缺少工具，絕不顯示假進度。

### Android
建議使用完整 JDK 21、Android SDK 35 / build-tools 35，Gradle Wrapper 固定 8.11.1。建置腳本會將來源同步至本機快取內的英文路徑，避免 Gradle 測試程序在中文工作目錄的類別載入問題；成品會複製回本專案。

```powershell
./scripts/Build-Android.ps1 -JavaHome 'C:\path\to\jdk' -AndroidHome 'C:\path\to\Android\Sdk'
```

輸出 `artifacts/OmniDownloader-universal-debug.apk`，內嵌四種 ABI 的 yt-dlp / FFmpeg 相依套件。Debug APK 可安裝測試；正式發佈需自己的簽署金鑰，不把私鑰提交 Git。

## 下載與 App 內更新

[下載最新 Windows 版本、Android 測試 APK 及瀏覽器擴充功能](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)。Windows x64 為免安裝版，首次解壓到可寫入的固定資料夾後啟動 App.exe。

0.1.4 起：設定 →「下載並安裝最新版本」。App 會驗證 GitHub 發布資產的 SHA256，暫停未完成任務並退出，更新器保留 data/ 與 native-host.json，安裝後重新啟動。更新後需手動繼續暫停任務；私人內容的登入資料只保留於記憶體，重啟後需從擴充功能重新傳送。

從 0.1.3 更新：GitHub 專案填 HKmario852/mp4-mp3-downloader，按「下載並安裝最新版本」，下載驗證後從系統匣選「結束」，按更新器的 Y 重開。這次仍需結束舊版，但不需要手動解壓；之後使用新版自動流程。

## 0.1.4 修正

- 擴充功能以目前分頁的影片及清單識別核對請求，避免 YouTube SPA 舊網址或 index/t 參數變動誤報頁面已切換；Cookie 傳送前再次確認。
- 支援 /playlist 頁面掛載。私人清單需登入有存取權的 YouTube 帳戶，並在擴充功能設定同意傳送至本機；手動貼上網址與 URI 後備不帶 Cookie。
- 「移除」可移除暫停、失敗、待選擇及進行中的任務，清理其工作暫存，排除完成項目。
- MP3 預設以來源影片縮圖作第一封面；MusicBrainz 圖作第二封面，縮圖不可取得時才以專輯图後備。既有 MP3 不會被自動重寫。
- 設定預填正式 GitHub 倉庫；更新版本比較使用實際 App 版本。

## 0.1.3 歷史、刪除與音樂資料

- 清空歷史採用深色確認視窗，顯示當前篩選數量；只清除紀錄，保留檔案。
- 完成任務自動移出下載任務，出現在最近下載及已下載；失敗項目從「失敗任務」查看及重試。
- 已下載選取一個檔案後可按「刪除檔案」。Windows 移到資源回收筒；Android 確認後永久刪除。只有刪除成功才清除對應紀錄。
- MusicBrainz 開啟後，每次新 MP3 都查詢，包括來源缺少歌手欄位的情況。保守解析「歌手 - 歌名」，或用歌名、片長及搜尋分數核對；顯示配對／無可靠配對／服務不可用結果。多個候選歌手不自動覆寫。
- 最多嘗試五個發行版本的專輯封面；查無或服務繁忙時保留來源標籤與可取得的影片縮圖。不能保證每段影片都有音樂資料；已下載檔案不會自動重新處理。

## 0.1.2 檔案管理修正

- 「標籤編輯」列出已下載 MP3；選取曲目後按「編輯所選標籤」，或雙擊曲目。支援多選批次修改。
- 「最近下載」及主清單的「開啟檔案位置」都可用；檔案被移動或刪除會顯示原因。
- 已下載加入「全部格式 / MP3 / MP4」篩選，可結合關鍵字搜尋；清空歷史只影響當前篩選命中記錄。
- MP4、MP3 儲存位置分開記住，在右側切換格式後選擇資料夾，亦可在設定一次指定兩者。舊設定沿用原本共用目錄；排隊中的任務保留新增時的位置。
- Windows 與 Android 影片最高 2160p（4K），「最佳」亦套用 4K 上限；舊 4320p 偏好自動降至 2160p。
- Windows MP4 容量估算與下載共用格式選擇，將選中的影音格式 ID 傳給 yt-dlp，避免不同來源造成估算偏差。分析後隱藏來源未提供的較高解像度，並顯示實際來源高度。MP3 估算按片長與輸出位元率計算；封面、標籤及封裝不包含在串流估算內。
- Android 同步格式篩選及分開儲存目錄；APK 為 0.1.2 Debug 測試版。

更新：系統匣右鍵選「結束」，將新版 Windows ZIP 解壓覆蓋原本程式資料夾，保留 `data/` 與 `native-host.json`，再開啟 App.exe。0.1.2 不需要重新設定擴充功能。

## 瀏覽器連接

### 0.1.1 介面修正版

Windows 收合側欄只保留向量圖示；最小化保留工作列，關閉則收入系統匣。網址支援換行，選取列保持深藍色，右側縮圖完整顯示。先分析連結後，品質選單會顯示預估 MP4 / MP3 大小；來源資料不足時明確顯示無法估算。

擴充功能選單改為 Shadow DOM + top-layer popover，避免被 YouTube 的容器或全域樣式裁切。更新擴充功能檔案後，需在 `brave://extensions` 按該擴充功能的重新載入，再重新整理 YouTube 頁面。更新不需更換擴充功能 ID 或重新勾選已保存的 Cookie 同意。

更新 Windows 時，先從系統匣右鍵選「結束」，再將新 ZIP 解壓覆蓋原有程式檔。保留原有 `data/` 與 `native-host.json`，完成後開啟 App.exe。

1. 手動啟動 Windows App。
2. Chrome `chrome://extensions` 或 Brave `brave://extensions` 開啟開發人員模式，載入 `extension` 資料夾。
3. 開啟擴充功能設定，複製 ID 到 App 的「設定 → 瀏覽器擴充功能 ID」，按「儲存並連接瀏覽器」。
4. 在擴充功能設定閱讀並同意本機登入 Cookie 轉發。
5. 登入 YouTube，在標準影片頁點「快速下載」。MP4 / MP3 品質繼承 App 設定。

Cookie 只走 Native Messaging / current-user Named Pipe。綠色勾號表示主程式已持久化接收任務；播放清單複合網址仍需手動確認。URI fallback 不傳 Cookie。請勿把登入 Cookie 放入網址、Git、日誌或更新 ZIP。

## 功能與行為

- 併發 1–5、排隊、續傳暫停、排他取消、500 ms EMA 網速、失敗退避、清單分組。
- MP4 品質最高 4K / 最佳（同樣限制 4K），MP3 128/192/256/320 kbps。
- 已下載的多關鍵字 AND 搜尋，篩選結果快照清空，保留實體檔案。
- MP3 批次 ID3 編輯：未修改欄位保留、Track 固定填值、空字串保留 Frame、Title 即時驗證及重新命名。
- MusicBrainz 預設關閉；開啟後精確比對歌曲/歌手，Cover Art Archive 封面 + 影片縮圖 APIC。
- Windows 系統匣、Toast；Android 通知背景暫停/取消、Wi-Fi 工作階段授權、SAF 未知容量確認。
- Android 初始儲存於 App 外部音樂資料夾，解除安裝會刪除；建議使用「選擇資料夾」儲存到共用 SAF 目錄。
- 原始碼與發布資產位於公開 GitHub 倉庫 `HKmario852/mp4-mp3-downloader`；App 更新不需 GitHub 登入。

## 專案

```text
src/Core/       任務、SQLite、下載器、ID3、驗證、Native IPC
src/Windows/    WPF、系統匣、Toast、HKCU、自癒、更新入口
src/NativeHost/ Chromium stdio bridge
android/        Compose、背景 Service、SAF、Android ID3
extension/      MV3、DOM fallback、登入標記、Cookie 同意
scripts/        建置、工具下載、完整 updater.ps1
tests/          關鍵不變條件測試
docs/           架構校正與驗收紀錄
```

## 發布及更新

發佈包只包含平鋪二進位與文件，不包含 `data/`、Cookie 或 native-host.json。上傳發布頁前產生 ZIP 的 SHA256。更新器會先驗證雜湊、ZIP 路徑及所有 PE Machine 類型，再等待指定 App PID 結束；覆蓋前備份，失敗回復。CLR AnyCPU DLL 依 CLR flags 判定，避免誤認成 x86。

```powershell
./scripts/updater.ps1 -InstallPath 'C:\Apps\Omni' -AppPid 1234 -ZipPath 'C:\Downloads\Omni.zip' -ExpectedSha256 '<64 hex>' -ReleasesUrl 'https://github.com/OWNER/REPO/releases'
```

API / 平台限制及修正依據見 [docs/IMPLEMENTATION.md](docs/IMPLEMENTATION.md)，實測狀態見 [docs/VERIFICATION.md](docs/VERIFICATION.md)。

## 第三方與授權

本專案以 GPL-3.0-or-later 發布，使用 youtubedl-android / FFmpeg 等 GPL 相依套件。重新散布時須保留授權並履行對應來源義務；詳細上游來源見 THIRD-PARTY.md。商店或網站仍決定影片存取權；本工具不實作 DRM 解密。
