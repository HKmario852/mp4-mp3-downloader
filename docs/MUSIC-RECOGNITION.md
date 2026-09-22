0.2.5：公開版本已配置 App 的 AcoustID application key，毋須自行註冊。下文自行註冊僅適用於自訂 key。Scan 現在使用獨立核對頁，按「套用所選標籤」才會正式寫入；預設保留目前封面。

# MusicBrainz 與 Scan

「查找歌曲標籤」使用 MusicBrainz 公開 API，不需要 MusicBrainz 帳號或密碼。可輸入歌曲名、歌手，或 MusicBrainz recording 網址。選擇歌曲及專輯版本後先預覽，按「儲存標籤」才寫入檔案。

「Scan 音訊辨識」適合原標題或歌手不準確的檔案。先在 [AcoustID 應用程式註冊](https://acoustid.org/new-application) 取得自己的 application/client API key，再填入 App「設定 → 格式 → 進階設定」的 AcoustID 欄位並儲存。這不是 MusicBrainz 密碼，也不是提交指紋用的個人 user key。沒有 key 時仍可使用文字查找。

Scan 在本機解碼最多 120 秒音訊，再傳送指紋及歌曲總長度到 AcoustID，不上傳音訊檔案。MusicBrainz 文字查詢會傳送歌曲名稱、歌手或 MBID。AcoustID 未收錄的歌曲可能沒有結果；多個候選版本必須由使用者選擇，不能保證每首歌曲都能辨識。

啟用自動 MusicBrainz 配對後，新 MP3 先查找文字中繼資料；有 application key 才會在需要時使用 Scan。手動編輯過的標籤及封面受到保護。標籤編輯器中的查找供使用者明確預覽及儲存。

封面接受長短邊比例不超過 1.15、至少 100px 的圖片，例如 233×217、475×500。圖片保持比例，不把長方形影片縮圖拉伸為專輯封面。Windows 貼上支援圖片檔案、圖片像素和 PNG 剪貼簿資料；Android 需來源 App 提供可讀的圖片 URI。單純複製網頁圖片網址不等同複製圖片。

技術文件：[MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API)、[AcoustID API](https://acoustid.org/webservice)、[Chromaprint](https://github.com/acoustid/chromaprint)。
