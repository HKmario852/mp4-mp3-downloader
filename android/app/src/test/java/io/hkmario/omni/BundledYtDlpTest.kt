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

    @Test fun knownOldArchiveIsReplacedEvenWhenAndroidCannotInspectZipimport() {
        assertTrue(BundledYtDlp.shouldReplaceInstalled("89a0d9058ea9018e380b7771898ff46e393a1986dcd13fef331693c87ce1fca4", null))
        assertFalse(BundledYtDlp.shouldReplaceInstalled(BundledYtDlp.bundledSha256, null))
        assertFalse(BundledYtDlp.shouldReplaceInstalled("unknown-newer-archive", null))
        assertFalse(BundledYtDlp.shouldReplaceInstalled("unknown-newer-archive", "2026.09.01"))
    }

    @Test fun bundledResourceMatchesExpectedDigest() {
        assertEquals(BundledYtDlp.bundledSha256, BundledYtDlp.sha256(File("src/main/res/raw/ytdlp")))
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
