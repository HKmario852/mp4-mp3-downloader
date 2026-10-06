# MusicBrainz 與 Scan

「查找歌曲標籤」使用 MusicBrainz 公開 API，不需要 MusicBrainz 帳號或密碼。可輸入歌曲名、歌手，或 MusicBrainz recording 網址。選擇歌曲及專輯版本後先預覽，按「儲存標籤」才寫入檔案。

「Scan 音訊辨識」適合原標題或歌手不準確的檔案。公開版本已配置 App 的 AcoustID application key，毋須自行註冊。若要使用自己的 application/client key，可到 [AcoustID 應用程式註冊](https://acoustid.org/new-application) 取得，再填入 App「設定 → 格式 → 進階設定」的 AcoustID 欄位。這不是 MusicBrainz 密碼，也不是提交指紋用的個人 user key。

Windows Scan 使用同一視窗內的獨立核對頁，結果不會立即改動檔案；按「套用所選標籤」才寫入勾選欄位，預設保留目前封面，可選擇保留復原記錄。

Scan 在本機解碼最多 120 秒音訊，再傳送指紋及歌曲總長度到 AcoustID，不上傳音訊檔案。MusicBrainz 文字查詢會傳送歌曲名稱、歌手或 MBID。AcoustID 未收錄的歌曲可能沒有結果；多個候選版本必須由使用者選擇，不能保證每首歌曲都能辨識。

Windows 預設在下載完成後於背景查找新 MP3 的中繼資料，可在設定改為下載前查找或關閉。Android 自動標籤配對預設關閉，但找不到來源封面時，其封面後備流程仍可能查詢 MusicBrainz。自動配對會先查文字資料，需要時使用音訊指紋。手動編輯過的標籤及封面受到保護；標籤編輯器中的手動查找則供使用者明確預覽及儲存。

自動專輯封面接受長短邊比例不超過 1.15、至少 100px 的圖片，例如 233×217、475×500。圖片保持比例，不把長方形影片縮圖拉伸為專輯封面。Windows 貼上及拖放可處理圖片檔案、圖片像素、PNG 剪貼簿資料，亦會嘗試解析瀏覽器提供的圖片網址或 HTML；來源網址必須仍可讀取。Android 需來源 App 提供可讀的圖片 URI。

技術文件：[MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API)、[AcoustID API](https://acoustid.org/webservice)、[Chromaprint](https://github.com/acoustid/chromaprint)。
