package io.hkmario.omni

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.io.File
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.security.MessageDigest
import java.util.concurrent.TimeUnit

/**
 * Keeps yt-dlp current between app releases. Newer yt-dlp releases (checked against GitHub's SHA-256) replace the
 * copy that [BundledYtDlp] manages; [BundledYtDlp.ensureCurrent] keeps a newer non-bundled copy, so app updates
 * never downgrade it.
 */
internal object YtDlpUpdater {
    const val repository = "yt-dlp/yt-dlp"
    private const val assetName = "yt-dlp"
    private val checkInterval = TimeUnit.DAYS.toMillis(1)
    private val client = OkHttpClient.Builder().callTimeout(5, TimeUnit.MINUTES).build()
    private val mutex = Mutex()

    data class Result(val updated: Boolean, val version: String?)

    private fun installed(context: Context) = File(context.noBackupFilesDir, "youtubedl-android/yt-dlp/yt-dlp")
    private fun stamp(context: Context) = File(context.noBackupFilesDir, "yt-dlp.checked")

    /** The version of the yt-dlp the engine runs, or null before the engine has set it up. */
    fun currentVersion(context: Context): String? {
        val file = installed(context)
        val hash = BundledYtDlp.sha256(file) ?: return null
        return if (hash == BundledYtDlp.bundledSha256) BundledYtDlp.version else BundledYtDlp.installedVersion(file)
    }

    /** Checks GitHub at most once a day (used at startup). */
    suspend fun updateIfDue(context: Context): Result? {
        val checked = stamp(context)
        if (checked.exists() && System.currentTimeMillis() - checked.lastModified() < checkInterval) return null
        return update(context)
    }

    /** Downloads the latest yt-dlp release when it is newer than the copy in use. Call after the engine is ready. */
    suspend fun update(context: Context): Result = withContext(Dispatchers.IO) {
        mutex.withLock {
            val current = currentVersion(context)
            val release = client.newCall(Request.Builder().url("https://api.github.com/repos/$repository/releases/latest").header("User-Agent", "OmniDownloader").build()).execute().use {
                check(it.isSuccessful) { "yt-dlp releases: HTTP ${it.code}" }
                JSONObject(it.body!!.string())
            }
            stamp(context).writeText(release.optString("tag_name"))
            if (!BundledYtDlp.isOlder(current, release.getString("tag_name"))) return@withLock Result(false, current)
            val assets = release.getJSONArray("assets")
            val asset = (0 until assets.length()).map { assets.getJSONObject(it) }.firstOrNull { it.getString("name") == assetName }
                ?: error("yt-dlp release has no $assetName asset")
            val (url, sha256) = trustedAsset(asset.getString("browser_download_url"), asset.optString("digest"))
            val target = installed(context)
            val temp = File(target.parentFile, "yt-dlp.update.tmp")
            try {
                val digest = MessageDigest.getInstance("SHA-256")
                client.newCall(Request.Builder().url(url).build()).execute().use { response ->
                    check(response.isSuccessful) { "yt-dlp download: HTTP ${response.code}" }
                    response.body!!.byteStream().use { input ->
                        temp.outputStream().use { output ->
                            val buffer = ByteArray(65536)
                            while (true) {
                                val n = input.read(buffer)
                                if (n < 0) break
                                output.write(buffer, 0, n)
                                digest.update(buffer, 0, n)
                            }
                            output.fd.sync()
                        }
                    }
                }
                check(digest.digest().joinToString("") { "%02x".format(it) } == sha256) { "yt-dlp SHA-256 mismatch; update stopped" }
                val downloaded = BundledYtDlp.installedVersion(temp)
                check(downloaded != null && BundledYtDlp.isOlder(current, downloaded)) { "Downloaded yt-dlp did not report a newer version" }
                // A running download keeps reading the old file; the next one uses the new copy.
                Files.move(temp.toPath(), target.toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
                Result(true, downloaded)
            } finally {
                temp.delete()
            }
        }
    }

    /** Only yt-dlp's own GitHub release downloads with a GitHub-computed SHA-256 are accepted. Returns url to hex digest. */
    fun trustedAsset(url: String, digest: String?): Pair<String, String> {
        require(url.startsWith("https://github.com/$repository/releases/download/")) { "yt-dlp download URL is not from the yt-dlp releases" }
        require(digest != null && Regex("sha256:[a-fA-F0-9]{64}").matches(digest)) { "yt-dlp release asset has no SHA-256; update stopped" }
        return url to digest.substring(7).lowercase()
    }
}
