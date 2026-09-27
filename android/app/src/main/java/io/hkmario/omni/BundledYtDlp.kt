package io.hkmario.omni

import android.content.Context
import java.io.File
import java.io.FileOutputStream
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.util.zip.ZipFile

/** Keep the Android extractor current even when an older install already extracted its bundled copy. */
internal object BundledYtDlp {
    const val version = "2026.08.19"

    fun installedVersion(file: File): String? = runCatching {
        ZipFile(file).use { zip ->
            zip.getEntry("yt_dlp/version.py")?.let { entry ->
                zip.getInputStream(entry).bufferedReader().use { reader ->
                    Regex("__version__\\s*=\\s*['\"]([0-9]+(?:\\.[0-9]+)+)['\"]")
                        .find(reader.readText())?.groupValues?.get(1)
                }
            }
        }
    }.getOrNull()

    fun isOlder(current: String?, bundled: String = version): Boolean {
        if (current == null) return true
        val currentParts = current.split('.').map { it.toIntOrNull() ?: return true }
        val bundledParts = bundled.split('.').map { it.toIntOrNull() ?: return true }
        for (index in 0 until maxOf(currentParts.size, bundledParts.size)) {
            val difference = currentParts.getOrElse(index) { 0 }.compareTo(bundledParts.getOrElse(index) { 0 })
            if (difference != 0) return difference < 0
        }
        return false
    }

    fun ensureCurrent(context: Context) {
        val directory = File(context.noBackupFilesDir, "youtubedl-android/yt-dlp")
        val installed = File(directory, "yt-dlp")
        if (!isOlder(installedVersion(installed))) return
        check(directory.isDirectory || directory.mkdirs()) { "Cannot prepare yt-dlp directory" }
        val replacement = File(directory, "yt-dlp.bundled.tmp")
        try {
            context.resources.openRawResource(R.raw.ytdlp).use { input ->
                FileOutputStream(replacement).use { output ->
                    input.copyTo(output)
                    output.fd.sync()
                }
            }
            check(installedVersion(replacement) == version) { "Bundled yt-dlp version mismatch" }
            try {
                Files.move(replacement.toPath(), installed.toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
            } catch (_: java.nio.file.AtomicMoveNotSupportedException) {
                Files.move(replacement.toPath(), installed.toPath(), StandardCopyOption.REPLACE_EXISTING)
            }
        } finally {
            replacement.delete()
        }
    }
}
