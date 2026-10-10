# OMNI 0.2.19 — 多格式音訊與安全標籤寫入

- Windows 及 Android 支援 MP3、Opus、M4A、FLAC 的下載、標籤、封面、資料夾匯入、AcoustID 核對及復原。
- Opus 選取原生 WebM/Opus 音訊並直接封裝；M4A 選取原生 AAC。兩者保留編碼音訊，沒有相符串流時清楚提示，不會偷偷重新編碼。
- MP3 保留 320 kbps 預設，新增 VBR V0。新下載 MP3 預設 ID3v2.3，設定內可選 ID3v2.4；編輯已有 MP3 保留原本版本。
- FLAC 使用無損編碼，但不能提升原本有損來源的音質。WAV 下載保留，WAV 標籤編輯不在本次支援範圍。
- 標籤寫入只套用選取欄位，封面需明確選擇；Android SAF 寫回加入原檔檢查、驗證及失敗復原備份。
- Android 可選下載後背景辨識，或辨識完成後才標示下載完成；手動修改受到保護。
- Windows Opus 預覽使用臨時解碼檔，原檔不變，切換頁面繼續播放。
- 封裝包含第三方授權文件；原始碼 ZIP 僅包含 Git 追蹤的檔案。

驗證：97 個 Windows 核心測試、23 個 Android JVM 測試、7 個 Android 模擬器測試通過；離線實際 yt-dlp 下載／轉碼、原生音訊封裝及修改標籤前後的編碼音訊雜湊通過。Windows 1440×940 及較窄視窗的畫面已檢查。

Android APK 仍為測試用 debug 版本；未驗證真實平板、所有 SAF 雲端提供者、長時間背景服務或真實 YouTube／VPN／帳戶組合。完整範圍見[驗證記錄](https://github.com/HKmario852/mp4-mp3-downloader/blob/main/docs/VERIFICATION-AUDIO-PIPELINE.md)，指令及實作見[音訊管線](https://github.com/HKmario852/mp4-mp3-downloader/blob/main/docs/AUDIO-PIPELINE.md)。

Windows 請解壓完整 ZIP 並執行 `OMNI.exe`，保留現有 `data/` 及 `native-host.json`。由仍使用 `App.exe` 的舊版首次遷移時，需退出 App 後手動解壓；不要只複製執行檔。
