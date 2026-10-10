package io.hkmario.omni

import org.junit.Assert.*
import org.junit.Test

class YtDlpUpdaterTest {
    private val url = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.09.30/yt-dlp"
    private val digest = "sha256:" + "AB".repeat(32)

    @Test fun acceptsOnlyYtDlpReleaseDownloadsWithADigest() {
        assertEquals(url to "ab".repeat(32), YtDlpUpdater.trustedAsset(url, digest))
        assertThrows(IllegalArgumentException::class.java) { YtDlpUpdater.trustedAsset(url, null) }
        assertThrows(IllegalArgumentException::class.java) { YtDlpUpdater.trustedAsset(url, "") }
        assertThrows(IllegalArgumentException::class.java) { YtDlpUpdater.trustedAsset(url, "sha1:abc") }
        assertThrows(IllegalArgumentException::class.java) { YtDlpUpdater.trustedAsset("https://example.com/yt-dlp", digest) }
        assertThrows(IllegalArgumentException::class.java) { YtDlpUpdater.trustedAsset("https://github.com/other/yt-dlp/releases/download/x/yt-dlp", digest) }
    }

    @Test fun updatesOnlyToNewerReleases() {
        assertTrue(BundledYtDlp.isOlder("2026.08.19", "2026.09.30"))
        assertTrue(BundledYtDlp.isOlder("2026.08.19", "2026.08.19.1"))
        assertFalse(BundledYtDlp.isOlder("2026.08.19", "2026.08.19"))
        assertFalse(BundledYtDlp.isOlder("2026.09.30", "2026.08.19"))
    }
}
