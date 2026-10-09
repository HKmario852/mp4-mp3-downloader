<p align="center"><img src="../../docs/assets/logo.png" alt="Omni Downloader" width="88" /></p>
<h1 align="center">Omni Downloader</h1>
<p align="center">動画と音声をダウンロード。ファイルを整理し、音声タグと楽曲の照合結果を確認。</p>

<p align="center">
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases/latest"><img src="https://img.shields.io/github/v/release/HKmario852/mp4-mp3-downloader" alt="Latest release" /></a>
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases"><img src="https://img.shields.io/github/downloads/HKmario852/mp4-mp3-downloader/total" alt="Release downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-5865F2" alt="Windows and Android" />
  <a href="../../LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue" alt="GPL-3.0-or-later" /></a>
</p>

<p align="center"><a href="../../README.md">English</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.zh-CN.md">简体中文</a> · <strong>日本語</strong> · <a href="README.ko.md">한국어</a> · <a href="README.es.md">Español</a></p>

<p align="center">
  <a href="../../docs/screenshots/windows-downloads.png"><img src="../../docs/screenshots/windows-downloads.png" alt="ダウンロード一覧" width="49%" /></a>
  <a href="../../docs/screenshots/windows-tag-editor.png"><img src="../../docs/screenshots/windows-tag-editor.png" alt="音声タグ編集" width="49%" /></a>
</p>

*Windows の画面は合成したサンプルメディアとオリジナルの幾何学アートを使用しています。クリックで拡大できます。*

<details>
<summary>その他のスクリーンショット</summary>

![ダウンロード済みファイル](../../docs/screenshots/windows-library.png)
![設定](../../docs/screenshots/windows-settings.png)

</details>

## 機能

- 📥 yt-dlp で動画やプレイリストをダウンロード。キュー、停止、再開、タスク管理に対応。
- 🎞️ MP4、MKV、WebM、MP3、Opus、M4A、FLAC、WAV を選択可能。利用できる形式は配信元によります。
- 🏷️ MP3、Opus、M4A、FLAC のタグを一括編集し、ほぼ正方形のカバー画像を埋め込み。別の JPG ファイルは作成しません。
- 🔎 AcoustID／MusicBrainz の候補を確認してから、選んだ項目だけを取り込み。復元用の記録も保存できます。
- 🎧 Windows では追加した音楽フォルダーを記憶し、ページを切り替えても試聴を継続します。
- 🌐 ブラウザー拡張機能で Chrome／Brave の YouTube リンクを Windows アプリに送信。

## ダウンロード／インストール

**[最新リリース](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)** から取得できます。

| 対象 | 必要環境 | インストール |
| --- | --- | --- |
| Windows | Windows 10（2004 以降）または 11、x64 | Windows ZIP を書き込み可能なフォルダーに展開し、`OMNI.exe` を起動。同梱ファイルはまとめて保管してください。 |
| Android | Android 8.0 以降（API 26） | universal debug APK をインストール。アンインストール後もファイルを残すには共有フォルダーを選択します。 |
| ブラウザー拡張機能 | Windows の Chrome／Brave | 展開した拡張機能を読み込み、アプリ設定で ID を接続。[設定手順](../../docs/DEVELOPMENT.md#browser-connection)。 |

> **注意:**
> Windows の実行ファイルは未署名です。Android はテスト用の debug ビルドで、実機とバックグラウンドサービスの検証は完了していません。

> [!NOTE]
> 旧版の実行ファイルは `App.exe` です。新しい名前への初回更新では、アプリを終了し、`data/` と `native-host.json` を残して Windows パッケージ全体を既存フォルダーに展開してください。その後 `OMNI.exe` を起動します。

## 使い方

1. **New download** に動画またはプレイリストの URL を貼り付け、**Analyze link** を選びます。
2. 形式、品質、保存先を選んでダウンロードを開始します。
3. 完了したファイルは **Downloaded** で確認できます。**Tag editor** で MP3、Opus、M4A、FLAC のファイルやフォルダーを追加し、タグやカバー画像を編集します。
4. 楽曲を識別するには 対応する音声ファイルを 1 つ選び、**Scan 音訊辨識** を実行します。候補と変更を確認し、明示的に適用した項目と画像だけが書き込まれます。

## ソースからビルド

Windows に **.NET 8 SDK 以降**をインストールし、PowerShell で実行します。

```powershell
git clone https://github.com/HKmario852/mp4-mp3-downloader.git
cd mp4-mp3-downloader
./scripts/Build-Windows.ps1
```

出力：`artifacts/windows-win-x64/OMNI.exe`。スクリプトが同梱メディアツールをダウンロードして検証します。配布アプリには .NET ランタイムが含まれます。

Android には **JDK 21**、**Android SDK／build-tools 35**、**NDK 27.0.12077973**、**CMake 3.22.1** も必要です。`JAVA_HOME` と `ANDROID_HOME` を設定して実行します。

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

出力：`artifacts/OmniDownloader-universal-debug.apk`。instrumentation テストのビルドと JVM 単体テストも実行します。構成、検証、パッケージ作成については [開発ガイド](../../docs/DEVELOPMENT.md) を参照してください。

## 設定

- **Settings** で保存先、形式、品質、字幕、画像の設定を変更できます。動画サムネイルの別ファイル保存は **初期設定では無効**です。
- ネイティブの Opus／M4A は再エンコードせず音声を保持します。MP3 は CBR または VBR V0 を選択でき、新規 MP3 のタグは ID3v2.3 が既定で、ID3v2.4 も選べます。FLAC にしても非可逆圧縮された音源の音質は向上しません。[音声パイプライン](../../docs/AUDIO-PIPELINE.md)。
- Windows の 音声メタデータ検索は、初期設定では **ダウンロード完了後**にバックグラウンドで行います。開始前または無効にも変更できます。Android の自動タグ照合は初期設定で無効ですが、カバー画像の補完時には MusicBrainz に問い合わせることがあります。
- アプリの言語設定は **繁体字中国語と英語**です。翻訳は未完了で、一部の画面とブラウザー拡張機能は中国語のままです。README の翻訳はアプリの対応言語を増やすものではありません。
- 制限付きコンテンツには、閲覧権限のあるアカウントから書き出した Netscape 形式の cookies ファイルが必要な場合があります。共有しないでください。[ブラウザー接続](../../docs/DEVELOPMENT.md#browser-connection) · [楽曲識別](../../docs/MUSIC-RECOGNITION.md)。

## 技術構成

- **Windows：** C#、.NET 8、WPF、SQLite。
- **Android：** Kotlin、Jetpack Compose、SQLite、Storage Access Framework。
- **メディアと連携：** yt-dlp、FFmpeg、Deno、Chromaprint、MusicBrainz、AcoustID、JavaScript／Manifest V3。

## プライバシー／免責事項

設定と履歴はローカルに保存されます。ダウンロード時は配信元、更新確認時は GitHub に接続します。楽曲検索ではタイトル、アーティスト、ID、または音声フィンガープリントと長さをメタデータサービスに送信し、**音声ファイル自体は送信しません**。ブラウザーの Cookie 転送には同意が必要です。[プライバシーの詳細](../../docs/PRIVACY.md) を参照してください。

ダウンロードが許可されたコンテンツにのみ使用してください。DRM の復号機能はなく、対応サイトとは無関係の独立したプロジェクトです。サイト対応は yt-dlp に依存し、サイトの変更によって動作しなくなる場合があります。配信元の利用継続や楽曲の照合成功は保証しません。

## ライセンスと謝辞

ライセンスは **[GPL-3.0-or-later](../../LICENSE)** です。yt-dlp、FFmpeg、youtubedl-android、Deno、Chromaprint、MusicBrainz、AcoustID、Cover Art Archive に感謝します。上流プロジェクトと再配布の義務は [サードパーティー情報](../../THIRD-PARTY.md) を参照してください。
