# 全能影音下載器技術方案

## 1. 擴充功能及 Protocol

`extension/manifest.json` 宣告 cookies、nativeMessaging、storage，host permission 僅 YouTube。Content Script 的 `SELECTORS` 有序降級：metadata owner → actions menu → metadata menu → player controls；watch 頁 debounce MutationObserver，Shorts 僅導航時基本注入；監聽 `yt-navigate-finish`、`spfdone` 並以固定 DOM ID 防重複。

按鈕檢查 masthead avatar，提供 MP4 / MP3 選單。DOM 只提供 UX 登入提示，SW 另檢查目前 tab 對應 CookieStore、登入 Cookie、同源及同一頁 URL。使用者在擴充設定同意轉發後，Cookie 經 Native Messaging stdio（4-byte little-endian length + UTF-8 JSON），再由 `Omni.NativeHost.exe` 透過 current-user Named Pipe 送入 App。單訊息上限 256 KiB；不支援把 partitioned cookies 混入 Netscape cookie jar。

`window.blur` 監聽保留為喚醒提示，但只有 App ACK 才會顯示綠勾 1.5 秒。2 秒沒有失焦不能證明失敗；真正接收有 20 秒 UI 逾時，提示先查看 App，避免重複加入。Native Messaging 不依賴 Chromium 外部協定確認框；fallback 明確列為無登入憑證的 `ytdl://download?url=${encodeURIComponent(url)}&mode=...`，提示只有瀏覽器提供「一律允許」時才可勾選。

`RegistryIntegration` 每次主實例冷啟動校正 `HKCU\Software\Classes\ytdl\shell\open\command` 為 `"App.exe完整路徑" --minimized "%1"`。不要求 UAC。App 資料夾移動後必須手動啟動一次才可自癒；失效的舊協定本身不能找到新路徑。

Mutex 保證單實例；Named Pipe 按 Windows SID + session 命名、限制目前使用者。接收 requestId 去重、先寫 SQLite 再 ACK。一般網址保持系統匣，`v` + `list` 存為 PendingChoice，主視窗嘗試聚焦、等待明確選擇，不倒數、不自動最小化。Windows 拒絕搶前景時改為工作列閃動，任務仍掛起。純 `/playlist?list=` 直接進入清單發現流程。

## 2. 框架及 UI

Windows：WPF / .NET 8，利用成熟 HWND、系統匣、COM Toast、Named Pipe 整合。Android：Kotlin / Jetpack Compose，原生 Service、通知 Action、SAF。兩端共享資料契約、命名及不變條件測試，平台 I/O 分別實作，避免讓 UI 框架插件隱藏生命週期差異。

Windows 是深色三欄、紫藍漸層、左側純圖示收合、任務表格、最近下載、右側設定。Android 手機底部導航、卡片佇列；寬畫面改用 NavigationRail。通知無論前景或冷啟動均以 page=history 路由已下載；空間及下載範圍對話框是全域待處理狀態。

Windows 發布根目錄：App.exe、Omni.NativeHost.exe、yt-dlp.exe、ffmpeg.exe、ffprobe.exe、deno.exe、updater.ps1、LICENSE、README.md、tool-versions.json。使用者資料獨立在 data/；無寫入權限時改用 LocalAppData。

## 3. 網路、通知及 Session

`DownloadService.kt` 使用 dataSync foreground service、NetworkCallback、START_NOT_STICKY、受限時暫停、onTimeout 停止服務。Android 15 的背景時間上限無法由 App 取消。網路 callback 是事後通知，不能保證切換瞬間絕對零行動封包；偵測後立即停止未授權工作。

SessionGrants 僅存在記憶體：cellularAll、taskIds、suppressUnknownSpace，程序重啟失效。清單授權繼承到該清單子任務。PendingIntent 必須 explicit + immutable；BroadcastReceiver 只呼叫引擎暫停/取消，不開 Activity。

## 4. 雙封面及 I/O

MusicBrainz 預設關閉。HTTP 工作池最多 5，MusicBrainz 查詢起點至少相隔 1 秒，429/503 延後；精確歌曲名及歌手、唯一 recording 才查 Cover Art Archive approved front。查不到時保留影片縮圖。這不是身份或作品權利的權威證明。

兩端以原始圖片 bytes 寫 ID3 APIC：type=3 Front Cover；第二圖 type=0 Other、description=Video thumbnail。MP3 封面只存於音訊標籤，下載及手動編輯均不額外輸出 JPG；影片縮圖如有啟用，仍可作為獨立檔案保存。內嵌 APIC 不重壓。

實體 MP3 才進 MediaScannerConnection.scanFile；content:// 由提供者管理。批次編輯封面直接更新各 MP3 的 APIC。

## 5. MP3Tag 編輯器

Delta 用「缺少 key」表示未修改，「key = 空字串」表示保留空 frame。TRCK 固定填值，不遞增。Title 即時檢查 `\ / : * ? " < > |`、控制字元、Windows 保留裝置名、結尾空格/句號，錯誤顯示紅字並停用儲存；絕不自動底線替換使用者 Title。

未動 TIT2 不走 Rename；非空 Title 更名遇衝突加序號；空 Title 保留原檔名。保存後 isUserEdited=true。背景序列化寫入、同目錄暫存、備份及例外復原；不顯示進度條。Advanced Raw Frames 使用 Base64 JSON，例如 `TXXX#1` 表示同 frame ID 的第二個實例，避免覆蓋其他未改欄位。TIT2 禁止從 raw 通道繞過檔名驗證。

目前 ID3 讀寫支援標準 v2.3/v2.4；遇到 extended header / unsynchronisation 等複雜變體會拒絕編輯並保護原檔，不把有限支援宣稱為所有 MP3Tag 格式完整相容。

## 6. SAF 安全搬移

Android 先在私有 staging 下載及轉碼。可取得容量時要求至少「檔案大小 + 10 MiB」；0/未知走無逾時詢問，Session 勾選只在同意後保存。SAF 不能保證跨卷原子 move，改用：建立唯一 .part document → stream copy → 回讀長度及 SHA256 → rename commit → 最後刪來源。失敗保留来源並移除未提交目標；不支援 rename 的提供者回報失敗。

儲存時把實際 parent URI 存到 task.directory，以支援封面及碰撞命名，不假設 provider ID 具有路徑語義。

## 7. 群組、EMA 及取消

播放清單一次非同步發現完整 entries，去重 ID，建立 sanitized 子目錄、獨立子工作。只有 discoveryComplete 且所有子任務終止才彙總通知，包含 discovery / download 失敗數。Windows 有 bounded virtualizing ListBox，Android 展開後 bounded LazyColumn。

EMA 每 500 ms 取樣，α=.25；實際 byte delta 為零或非下載狀態立即 speed=0、ETA=0。Android 來源未提供總量時依進度估計，總量未知顯示不確定資訊。重試 2/4/8 秒，最多三次；HTTP 存取拒絕不盲目重試。

取消只作用於 queued / analyzing / downloading / retryWait，排除完成及暫停；先終止並等待工作退出，再清理 task 專用 staging。暫停保留續傳檔。Android 使用上游 wrapper 的程序終止 API；不同 OEM 對衍生 FFmpeg 行程的行為仍需裝置壓力驗證。

## 8. 全欄位搜尋及歷史清空

Title、URL、fileName、Artist、Album 各欄位大小寫不敏感；標準空格分詞，所有詞均須至少命中一欄位。按清空時捕捉精確 ID 快照及 X 數量，確認只隱藏該快照，不因搜尋文字或新下載變動而擴大範圍。SQLite 保留 request 去重紀錄；磁碟媒體不刪除。

## 9. 模型及轉碼

`Models.cs` / `Models.kt` 包含 task、group、history、preferences、request/ACK。下載偏好在入列時快照品質；Cookies 與 Session 權限不寫歷史。SQLite 保存狀態；程序重啟將未完成工作改為 Paused。

MP4：`bv*[height<=?H][ext=mp4]+ba[ext=m4a]/b[height<=?H][ext=mp4]/bv*[height<=?H]+ba/b[height<=?H]`，merge/remux mp4。問號容許 metadata 未提供解像度的直接影片；無法在未提供尺寸時保證來源低於上限，實際輸出以檢查為準。

MP3：`-f bestaudio/best -x --audio-format mp3 --audio-quality 320k --postprocessor-args "ExtractAudio+ffmpeg_o:-ar 44100" --embed-metadata`。沒有 -ac；MP3 格式自身上限雙聲道，不能聲稱多聲道原封保留。44.1 kHz 避免低取樣率 MPEG-2 MP3 將 320 kbps 限制成 160 kbps；不會改善來源音質。

## 10. Windows 更新

冷啟動非阻塞檢查設定的 GitHub Releases；設定可下載匹配架構 ZIP，從 HTTPS API asset digest 驗證 SHA256，再啟動 updater.ps1。預設 asInvoker，僅使用者選擇需要時 runas，Win32Exception（含取消/密碼拒絕）停止更新。沒有真實 GitHub repository 時不虛構發布端點。

完整 updater.ps1：絕對路徑／reparse point 檢查、SHA256、ZIP path traversal／symbolic link／拍平碰撞防護、全 EXE/DLL PE header、CLR AnyCPU flags、本機 x64/ARM64/x86 比對、不符開 Releases、PID 路徑比對及等待、鎖定預檢、備份、逐檔覆蓋、rollback、ZIP 清理、ASCII 進度、末尾 Y/N 重啟。每程序 ExecutionPolicy 參數不修改電腦整體執行原則。

## 後續值得加入但未假裝已完成

- 發布 CI 的 release signing、SBOM 及固定版本第三方 source bundle。
- 真實 Chrome/Brave DOM 回歸、登入受限影片的使用者帳號測試。
- Android 15+ 前景服務 timeout、不同 SD card / 雲端 SAF provider、斷電復原測試。
- 帶授權模型的端側音樂分類／分段；目前沒有假模型或虛構 MusicBrainz 權威仲裁結果。
