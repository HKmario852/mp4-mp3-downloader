package io.hkmario.omni

import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream

class BundledYtDlpTest {
    @Test fun upgradesOlderEngineWithoutDowngradingNewerOne() {
        assertTrue(BundledYtDlp.isOlder("2025.11.12"))
        assertFalse(BundledYtDlp.isOlder("2026.08.19"))
        assertFalse(BundledYtDlp.isOlder("2026.09.01"))
        assertTrue(BundledYtDlp.isOlder(null))
    }

    @Test fun readsInstalledZipimportVersion() {
        val archive = File.createTempFile("yt-dlp-test", ".zip")
        try {
            ZipOutputStream(archive.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("yt_dlp/version.py"))
                zip.write("__version__ = '2025.11.12'\n".toByteArray())
                zip.closeEntry()
            }
            assertEquals("2025.11.12", BundledYtDlp.installedVersion(archive))
        } finally {
            archive.delete()
        }
    }
}
