# MP4/MP3 downloader 0.2.8

Android 套件內置的 yt-dlp 更新至官方穩定版 2026.08.19。從舊版本升級時，App 會替換已抽出的舊版引擎；如果使用者另外安裝了更新的引擎，則不會降級。Android 下載失敗或分析連結遇到 YouTube 年齡限制時，會顯示具體登入及 Cookie 步驟。

Windows「分析連結」現在會使用設定中的 YouTube Cookie 檔案，與正式下載一致。Windows 及 Android 對需要年齡確認的影片均提供更清楚的錯誤提示。

年齡限制仍由 YouTube 帳號控制。更新 yt-dlp 不等於完成登入；使用者須先用自己的帳號完成年齡確認，再依設定匯入有效的 Netscape cookies.txt，或在 Windows 使用已登入的瀏覽器擴充功能傳送。Cookie 等同登入憑證，請勿分享。
