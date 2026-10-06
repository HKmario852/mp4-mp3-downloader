<p align="center"><img src="../../docs/assets/logo.png" alt="Omni Downloader" width="88" /></p>
<h1 align="center">Omni Downloader</h1>
<p align="center">Descarga vídeo y audio. Organiza archivos, edita etiquetas MP3 y revisa las coincidencias musicales.</p>

<p align="center">
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases/latest"><img src="https://img.shields.io/github/v/release/HKmario852/mp4-mp3-downloader" alt="Latest release" /></a>
  <a href="https://github.com/HKmario852/mp4-mp3-downloader/releases"><img src="https://img.shields.io/github/downloads/HKmario852/mp4-mp3-downloader/total" alt="Release downloads" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Android-5865F2" alt="Windows and Android" />
  <a href="../../LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue" alt="GPL-3.0-or-later" /></a>
</p>

<p align="center"><a href="../../README.md">English</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <strong>Español</strong></p>

<p align="center">
  <a href="../../docs/screenshots/windows-downloads.png"><img src="../../docs/screenshots/windows-downloads.png" alt="Cola de descargas" width="49%" /></a>
  <a href="../../docs/screenshots/windows-tag-editor.png"><img src="../../docs/screenshots/windows-tag-editor.png" alt="Editor de etiquetas MP3" width="49%" /></a>
</p>

*Las capturas de Windows usan medios de muestra generados e ilustraciones geométricas originales. Haz clic para ampliarlas.*

<details>
<summary>Más capturas</summary>

![Archivos descargados](../../docs/screenshots/windows-library.png)
![Ajustes](../../docs/screenshots/windows-settings.png)

</details>

## Funciones

- 📥 Descarga vídeos y listas con yt-dlp; gestiona la cola, pausa y reanuda las tareas.
- 🎞️ Elige MP4, MKV, WebM, MP3, M4A, FLAC o WAV según lo que ofrezca la fuente.
- 🏷️ Edita etiquetas MP3 por lotes e incrusta carátulas casi cuadradas, sin crear archivos JPG adicionales.
- 🔎 Revisa las coincidencias de AcoustID／MusicBrainz antes de importar los campos elegidos, con registros de restauración opcionales.
- 🎧 En Windows, conserva las carpetas de música añadidas y mantiene la reproducción de la vista previa al cambiar de página.
- 🌐 Envía enlaces de YouTube desde Chrome o Brave a la aplicación de Windows mediante la extensión.

## Descarga / Instalación

Los paquetes están en **[la última versión](https://github.com/HKmario852/mp4-mp3-downloader/releases/latest)**.

| Plataforma | Requisitos | Instalación |
| --- | --- | --- |
| Windows | Windows 10 (2004 o posterior) u 11, x64 | Extrae el ZIP de Windows en una carpeta con permisos de escritura. Ejecuta `App.exe` y conserva juntos los archivos incluidos. |
| Android | Android 8.0 o posterior (API 26) | Instala el APK universal de depuración. Elige una carpeta compartida para conservar las descargas al desinstalar. |
| Extensión del navegador | Chrome / Brave en Windows | Carga la extensión descomprimida y conecta su ID en los ajustes de la aplicación. [Guía de conexión](../../docs/DEVELOPMENT.md#browser-connection). |

> **Nota:**
> El ejecutable de Windows no está firmado. Los paquetes de Android son compilaciones de depuración para pruebas; la verificación en dispositivos y de los servicios en segundo plano está incompleta.

## Primeros pasos

1. Pega la URL de un vídeo o una lista en **New download** y selecciona **Analyze link**.
2. Elige formato, calidad y destino; después inicia la descarga.
3. Consulta los archivos terminados en **Downloaded**. En **Tag editor**, añade MP3 o carpetas para editar sus etiquetas y carátulas.
4. Para identificar una canción, selecciona un MP3 y pulsa **Scan 音訊辨識**. Revisa las versiones y los cambios; solo se escriben los campos y la carátula que apliques expresamente.

## Compilar desde el código fuente

En Windows, instala **.NET 8 SDK o posterior** y ejecuta en PowerShell:

```powershell
git clone https://github.com/HKmario852/mp4-mp3-downloader.git
cd mp4-mp3-downloader
./scripts/Build-Windows.ps1
```

Resultado: `artifacts/windows-win-x64/App.exe`. El script descarga y verifica las herramientas multimedia incluidas. La aplicación publicada incorpora el entorno de ejecución de .NET.

Para Android también necesitas **JDK 21**, **Android SDK / build-tools 35**, **NDK 27.0.12077973** y **CMake 3.22.1**. Configura `JAVA_HOME` y `ANDROID_HOME`, y ejecuta:

```powershell
./scripts/Build-Android.ps1 -JavaHome $env:JAVA_HOME -AndroidHome $env:ANDROID_HOME
```

Resultado: `artifacts/OmniDownloader-universal-debug.apk`. El script también compila las pruebas de instrumentación y ejecuta las pruebas unitarias de JVM. Consulta la [guía de desarrollo](../../docs/DEVELOPMENT.md) para ver la arquitectura, las comprobaciones y el empaquetado.

## Configuración

- Ajusta las carpetas, formatos, calidad, subtítulos y carátulas en **Settings**. Las miniaturas de vídeo como archivos independientes están **desactivadas por defecto**.
- Windows busca metadatos MP3 en segundo plano **después de descargar**, de forma predeterminada; puedes hacerlo antes o desactivarlo. En Android, la búsqueda automática de etiquetas está desactivada por defecto, pero la búsqueda de una carátula ausente aún puede consultar MusicBrainz.
- La aplicación ofrece ajustes de idioma en **chino tradicional e inglés**. La traducción está incompleta: algunas pantallas y la extensión siguen en chino. Estas traducciones del README no añaden idiomas a la interfaz.
- El contenido restringido puede requerir un archivo cookies en formato Netscape de una cuenta con acceso. No lo compartas. [Conexión del navegador](../../docs/DEVELOPMENT.md#browser-connection) · [Reconocimiento musical](../../docs/MUSIC-RECOGNITION.md).

## Tecnologías

- **Windows:** C#, .NET 8, WPF, SQLite.
- **Android:** Kotlin, Jetpack Compose, SQLite, Storage Access Framework.
- **Medios e integración:** yt-dlp, FFmpeg, Deno, Chromaprint, MusicBrainz, AcoustID, JavaScript / Manifest V3.

## Privacidad / Aviso

Los ajustes y el historial se guardan localmente. Las descargas contactan con el sitio de origen y las comprobaciones de actualización, con GitHub. Las búsquedas musicales envían títulos, artistas, identificadores o huellas de audio y su duración a servicios de metadatos, **no el archivo de audio**. El reenvío de cookies del navegador requiere consentimiento. Consulta los [detalles de privacidad](../../docs/PRIVACY.md).

Úsalo solo con contenido que tengas permiso para descargar. El proyecto no descifra DRM ni está afiliado a los sitios compatibles. La compatibilidad depende de yt-dlp y puede dejar de funcionar cuando cambian los sitios. No se garantiza la disponibilidad de las fuentes ni el éxito de las coincidencias musicales.

## Licencia y agradecimientos

Publicado bajo **[GPL-3.0-or-later](../../LICENSE)**. Gracias a yt-dlp, FFmpeg, youtubedl-android, Deno, Chromaprint, MusicBrainz, AcoustID y Cover Art Archive. Los proyectos de origen y las obligaciones de redistribución figuran en los [avisos de terceros](../../THIRD-PARTY.md).
