<p align="center"><img src="../../docs/assets/logo.png" alt="Omni Downloader" width="88" /></p>
<h1 align="center">Omni Downloader</h1>
<p align="center">영상과 오디오를 다운로드하고, 파일과 MP3 태그를 관리하며 음악 인식 결과를 확인하세요.</p>

<p align="center">
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases/latest"><img src="https://img.shields.io/github/v/release/HKmario852/mp4-mp3-downloader" alt="Latest release" /></a>
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases"><img src="https://img.shields.io/github/downloads/HKmario852/mp4-mp3-downloader/total" alt="Release downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-5865F2" alt="Windows and Android" />
  <a href="../../LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue" alt="GPL-3.0-or-later" /></a>
</p>

<p align="center"><a href="../../README.md">English</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a> · <strong>한국어</strong> · <a href="README.es.md">Español</a></p>

<p align="center">
  <a href="../../docs/screenshots/windows-downloads.png"><img src="../../docs/screenshots/windows-downloads.png" alt="다운로드 목록" width="49%" /></a>
  <a href="../../docs/screenshots/windows-tag-editor.png"><img src="../../docs/screenshots/windows-tag-editor.png" alt="MP3 태그 편집" width="49%" /></a>
</p>

*Windows 스크린샷에는 합성 샘플 미디어와 직접 만든 기하학적 커버만 사용했습니다. 클릭하면 확대됩니다.*

<details>
<summary>스크린샷 더 보기</summary>

![다운로드한 파일](../../docs/screenshots/windows-library.png)
![설정](../../docs/screenshots/windows-settings.png)

</details>

## 기능

- 📥 yt-dlp로 영상과 재생목록을 다운로드하고 대기열, 일시 정지, 재개, 작업을 관리합니다.
- 🎞️ MP4, MKV, WebM, MP3, M4A, FLAC, WAV를 선택할 수 있습니다. 사용 가능한 형식은 원본에 따라 다릅니다.
- 🏷️ MP3 태그를 일괄 편집하고 정사각형에 가까운 커버를 삽입합니다. 별도의 JPG 파일은 만들지 않습니다.
- 🔎 AcoustID／MusicBrainz 후보를 확인한 뒤 선택한 필드만 가져옵니다. 복원 기록도 선택적으로 보관할 수 있습니다.
- 🎧 Windows에서는 추가한 음악 폴더를 기억하며 페이지를 바꿔도 미리 듣기가 계속됩니다.
- 🌐 브라우저 확장으로 Chrome이나 Brave의 YouTube 링크를 Windows 앱에 보냅니다.

## 다운로드／설치

**[최신 릴리스](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)** 에서 패키지를 받으세요.

| 플랫폼 | 요구 사항 | 설치 방법 |
| --- | --- | --- |
| Windows | Windows 10(2004 이상) 또는 11, x64 | Windows ZIP을 쓰기 가능한 폴더에 풀고 `App.exe`를 실행합니다. 포함된 파일은 같은 폴더에 두세요. |
| Android | Android 8.0 이상(API 26) | universal debug APK를 설치합니다. 앱 삭제 후에도 다운로드를 보관하려면 공유 폴더를 선택하세요. |
| 브라우저 확장 | Windows의 Chrome／Brave | 압축을 푼 확장을 로드하고 앱 설정에서 ID를 연결합니다. [설정 안내](../../docs/DEVELOPMENT.md#browser-connection). |

> **참고:**
> Windows 실행 파일은 서명되지 않았습니다. Android 패키지는 테스트용 debug 빌드이며, 실제 기기와 백그라운드 서비스 검증은 아직 완료되지 않았습니다.

## 빠른 시작

1. **New download**에 영상 또는 재생목록 URL을 붙여 넣고 **Analyze link**를 선택합니다.
2. 형식, 품질, 저장 위치를 선택한 뒤 다운로드를 시작합니다.
3. 완료된 파일은 **Downloaded**에서 확인합니다. **Tag editor**에서 MP3 또는 폴더를 추가해 태그와 커버를 편집합니다.
4. 음악을 인식하려면 MP3 하나를 선택하고 **Scan 音訊辨識**를 누릅니다. 후보와 변경 내용을 검토한 뒤 명시적으로 적용한 필드와 커버만 기록됩니다.

## 소스에서 빌드

Windows에 **.NET 8 SDK 이상**을 설치하고 PowerShell에서 실행합니다.

```powershell
git clone https://github.com/HKmario852/mp4-mp3-downloader.git
cd mp4-mp3-downloader
./scripts/Build-Windows.ps1
```

출력: `artifacts/windows-win-x64/App.exe`. 스크립트가 포함될 미디어 도구를 다운로드하고 검증합니다. 배포 앱에는 .NET 런타임이 포함됩니다.

Android에는 **JDK 21**, **Android SDK／build-tools 35**, **NDK 27.0.12077973**, **CMake 3.22.1**도 필요합니다. `JAVA_HOME`과 `ANDROID_HOME`을 설정한 뒤 실행합니다.

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

출력: `artifacts/OmniDownloader-universal-debug.apk`. instrumentation 테스트를 빌드하고 JVM 단위 테스트도 실행합니다. 구조, 검증, 패키징은 [개발 안내](../../docs/DEVELOPMENT.md)를 참고하세요.

## 설정

- **Settings**에서 저장 위치, 형식, 품질, 자막, 커버 옵션을 변경합니다. 영상 썸네일을 별도 파일로 저장하는 기능은 **기본적으로 꺼져 있습니다**.
- Windows는 기본적으로 **다운로드 후** 백그라운드에서 MP3 메타데이터를 조회합니다. 다운로드 전 또는 끄기로 바꿀 수 있습니다. Android의 자동 태그 매칭은 기본적으로 꺼져 있지만, 누락된 커버를 찾는 과정에서 MusicBrainz를 조회할 수 있습니다.
- 앱의 언어 설정은 **번체 중국어와 영어**입니다. 번역이 완전하지 않아 일부 화면과 브라우저 확장은 중국어로 표시됩니다. README 번역이 앱 언어를 추가하는 것은 아닙니다.
- 접근이 제한된 콘텐츠에는 권한이 있는 계정에서 내보낸 Netscape 형식 cookies 파일이 필요할 수 있습니다. 이 파일을 공유하지 마세요. [브라우저 연결](../../docs/DEVELOPMENT.md#browser-connection) · [음악 인식](../../docs/MUSIC-RECOGNITION.md).

## 기술 구성

- **Windows:** C#, .NET 8, WPF, SQLite.
- **Android:** Kotlin, Jetpack Compose, SQLite, Storage Access Framework.
- **미디어와 연동:** yt-dlp, FFmpeg, Deno, Chromaprint, MusicBrainz, AcoustID, JavaScript／Manifest V3.

## 개인정보／면책 사항

설정과 기록은 로컬에 저장됩니다. 다운로드는 원본 사이트에, 업데이트 확인은 GitHub에 접속합니다. 음악 조회 시 제목, 아티스트, ID 또는 오디오 지문과 길이를 메타데이터 서비스에 전송하며, **오디오 파일 자체는 전송하지 않습니다**. 브라우저 Cookie 전달에는 동의가 필요합니다. [개인정보 안내](../../docs/PRIVACY.md)를 참고하세요.

다운로드가 허용된 콘텐츠에만 사용하세요. DRM 복호화 기능은 없으며 지원 사이트와 제휴하지 않은 독립 프로젝트입니다. 사이트 지원은 yt-dlp에 의존하므로 사이트가 변경되면 작동하지 않을 수 있습니다. 원본의 지속적인 이용 가능 여부나 음악 매칭 성공을 보장하지 않습니다.

## 라이선스 및 감사

**[GPL-3.0-or-later](../../LICENSE)** 라이선스를 사용합니다. yt-dlp, FFmpeg, youtubedl-android, Deno, Chromaprint, MusicBrainz, AcoustID, Cover Art Archive에 감사드립니다. 상위 프로젝트와 재배포 의무는 [타사 고지](../../THIRD-PARTY.md)를 참고하세요.
