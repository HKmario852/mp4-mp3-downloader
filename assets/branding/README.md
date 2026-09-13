# 全能影音下載器圖示

使用內建 image_gen 產生原創透明 PNG，再由 `scripts/Export-Icons.ps1` 作尺寸及 ICO 格式轉換；沒有呼叫付費 CLI fallback。原圖 `omni-master.png` 保留生成結果與 alpha。

配色：紫色 `#6130FF` → 電光藍 `#168FFF`，深海軍藍 `#111923`。主體為播放三角形與向下下載箭嘴，不含文字。

套用位置：Windows EXE / 視窗 / 系統匣 / 主頁標誌 / 協定 DefaultIcon；Android launcher adaptive icon / 主頁標誌；Chrome / Brave manifest / 工具列 / 設定頁。

Windows ICO 含 16、20、24、32、40、48、64、128、256 px。Android 前景保持在中央安全區，背景獨立使用 App 深色；依據 [Android adaptive icon 文件](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive)。

## 原始生成提示詞

Use case: logo-brand. Create ONE final production app icon mark for Omni Downloader, a dark-themed video and music downloader. 1024x1024 square canvas, genuinely transparent background with alpha. A bold compact rounded triangular PLAY ribbon fused elegantly with a clear DOWNWARD DOWNLOAD ARROW through its center, one coherent iconic silhouette, not separate floating symbols. Premium restrained soft bevels, mostly flat vector-like thick geometry, smooth violet #6130FF to electric blue #168FFF gradient, small lavender highlights, dark navy inner negative space only where useful. Perfectly front-on, centered, mark occupies roughly 78% of canvas with clear padding on all sides. Very legible at 32px, few broad shapes, no tiny details. Matches UI background #111923 and panels #18222D. No text, letters, numbers, watermark, enclosing square tile, external drop shadows, mockup, UI, grid, variants, photography or background decoration. Deliver the isolated icon itself with clean anti-aliased transparent edges.

實際原圖為 1254 × 1254；平台圖示以此原圖重新取樣。重新匯出：`./scripts/Export-Icons.ps1`，之後執行 Windows / Android 建置與 Package 腳本。
