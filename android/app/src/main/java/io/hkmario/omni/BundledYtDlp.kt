package io.hkmario.omni

import android.content.Context
import java.io.File
import java.io.FileOutputStream
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.security.MessageDigest
import java.util.zip.ZipFile

/** Keep the Android extractor current even when an older install already extracted its bundled copy. */
internal object BundledYtDlp {
    const val version = "2026.08.19"
    const val bundledSha256 = "1fa6733c37ea6fb51c99ad8fe785e7b7e5f3246c9b980230329d4fb72ed8d4d6"
    private const val previousSha256 = "89a0d9058ea9018e380b7771898ff46e393a1986dcd13fef331693c87ce1fca4"

    fun sha256(file: File): String? = runCatching {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().buffered().use { input ->
            val buffer = ByteArray(65536)
            while (true) {
                val count = input.read(buffer)
                if (count < 0) break
                digest.update(buffer, 0, count)
            }
        }
        digest.digest().joinToString("") { "%02x".format(it) }
    }.getOrNull()

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

    fun shouldReplaceInstalled(hash: String?, detectedVersion: String?): Boolean = when (hash) {
        null, previousSha256 -> true
        bundledSha256 -> false
        else -> detectedVersion?.let { isOlder(it) } ?: false
    }

    fun ensureCurrent(context: Context) {
        val directory = File(context.noBackupFilesDir, "youtubedl-android/yt-dlp")
        val installed = File(directory, "yt-dlp")
        val installedHash = sha256(installed)
        val detectedVersion = if (installedHash == null || installedHash == previousSha256 || installedHash == bundledSha256) null else installedVersion(installed)
        // An unknown archive that Android cannot inspect may be a newer user-installed version.
        if (!shouldReplaceInstalled(installedHash, detectedVersion)) return
        check(directory.isDirectory || directory.mkdirs()) { "Cannot prepare yt-dlp directory" }
        val replacement = File(directory, "yt-dlp.bundled.tmp")
        try {
            context.resources.openRawResource(R.raw.ytdlp).use { input ->
                FileOutputStream(replacement).use { output ->
                    input.copyTo(output)
                    output.fd.sync()
                }
            }
            check(sha256(replacement) == bundledSha256) { "Bundled yt-dlp checksum mismatch" }
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
