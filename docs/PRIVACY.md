# Privacy / 私隱

The app stores preferences and download history locally. It does not upload your media library, browser cookies, or diagnostic logs to GitHub. Download requests go to the media website you select.

本程式只在本機儲存設定及下載紀錄，不會將媒體庫、瀏覽器 Cookie 或診斷日誌上傳 GitHub。下載請求會傳送至你選擇的影音網站。

- Browser authentication forwarding requires consent in the extension. Credentials are sent to the local Windows downloader using Native Messaging and are not put in custom URLs. They expire when the application process ends.
- 自訂 Cookie 檔案包含登入權限，請勿分享。只供使用者明確選擇的 YouTube 下載使用。
- MusicBrainz is optional. When enabled, the song title, artist and duration are used to find metadata; artwork is downloaded from Cover Art Archive and the source thumbnail server.
- Clipboard monitoring is off by default. When enabled, the foreground app checks for HTTPS links and offers/fills them; it never starts a download automatically. Android restricts background clipboard access.
- Update checks contact the configured GitHub repository. Installation and restart require confirmation. Standard network services receive your IP address when contacted.
- Logs stay local unless you choose to export/share them. Network URLs and credential-looking strings are redacted from diagnostics; review exported files before sharing.
- Windows can move deleted files to Recycle Bin. Android uses the system trash flow where supported; other storage providers require explicit confirmation before permanent deletion.

This is an open-source independent downloader, not a YouTube or MusicBrainz product.

音訊 Scan：本機產生 Chromaprint 指紋，向 AcoustID 傳送指紋、歌曲長度及 application client key；不傳送原始音訊。公開 MusicBrainz 查詢傳送歌曲名、歌手或 MBID，無須登入帳號。
