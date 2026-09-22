# mp4/mp3 downloader 0.2.4

- 已下載統計卡片保持等寬，彩色圖示置左，名稱及數值置右並垂直置中。
- Windows 標籤編輯支援從剪貼簿貼上圖片／圖片檔案，以及拖放圖片到封面區；Android 支援圖片 URI 貼上及拖放。封面先預覽，按儲存標籤後才寫入所選歌曲。
- MusicBrainz 查找支援歌曲名、演出者，以及 recording 網址／MBID，取得專輯、專輯演出者、年份、曲目及光碟編號。
- 多個歌曲或專輯版本會要求選擇；自動下載只採用可靠配對，保留手動編輯保護。
- 新增 Scan 音訊指紋辨識及 AcoustID application/client key 設定。Windows 使用 FFmpeg；Android 內建 Chromaprint，本機產生指紋。
- 維持近正方形封面政策，不裁切、不拉伸；查無可靠封面時保留原封面。

AcoustID 未附帶 application key。請參閱 [設定說明](MUSIC-RECOGNITION.md)。本版已驗證本機指紋，但未驗證使用正式 key 的 AcoustID 線上配對。公開 MusicBrainz 查詢不需要登入。

Windows 未簽章免安裝包；Android 通用 Debug APK。擴充功能沒有修改。
