package io.hkmario.omni
import org.junit.Assert.*
import org.junit.Test
import kotlin.math.PI
import kotlin.math.sin
class AudioQualityTest {
    // Flat spectrum made of many tones up to [top] Hz with random phases: a brick-wall source with nothing above.
    private fun tones(top: Double, rate: Int = 44100, seconds: Double = 8.0): FloatArray {
        val random = java.util.Random(7); val freqs = generateSequence(50.0) { it + 50 }.takeWhile { it <= top }.map { it to random.nextDouble() * 2 * PI }.toList()
        return FloatArray((rate * seconds).toInt()) { i -> val t = i.toDouble() / rate; (freqs.sumOf { (f, p) -> sin(2 * PI * f * t + p) } * 0.002).toFloat() }
    }

    @Test fun fftFindsASineInItsBin() {
        val re = DoubleArray(4096) { sin(2 * PI * 100 * it / 4096) }; val im = DoubleArray(4096)
        AudioQuality.fft(re, im)
        assertEquals(100, (0 until 2048).maxBy { re[it] * re[it] + im[it] * im[it] })
    }

    @Test fun findsTheCutoffOfABandLimitedSource() {
        listOf(16000.0 to AudioQuality.Grade.Low, 18500.0 to AudioQuality.Grade.Medium, 20000.0 to AudioQuality.Grade.High).forEach { (top, grade) ->
            val cutoff = AudioQuality.cutoff(AudioQuality.spectrum(tones(top)), 44100)
            assertNotNull(cutoff); assertTrue("$top → $cutoff", cutoff!! in top - 300..top + 1100)
            assertEquals(grade, AudioQuality.gradeOf(cutoff))
        }
    }

    @Test fun fullBandAndSilence() {
        assertEquals(AudioQuality.Grade.FullBand, AudioQuality.gradeOf(AudioQuality.cutoff(AudioQuality.spectrum(tones(21900.0)), 44100)))
        assertEquals(AudioQuality.Grade.Unknown, AudioQuality.gradeOf(AudioQuality.cutoff(AudioQuality.spectrum(FloatArray(44100 * 5)), 44100)))
    }

    @Test fun flagsFakeLosslessAndUpscaledFiles() {
        val low = AudioQuality.spectrum(tones(16000.0))
        assertTrue(AudioQuality.judge(low, 44100, "flac", 900_000).suspicious)
        assertTrue(AudioQuality.judge(low, 44100, "flac", 900_000).verdict.contains("假無損"))
        assertTrue(AudioQuality.judge(low, 44100, "mp3", 320_000).verdict.contains("升頻"))
        assertFalse(AudioQuality.judge(low, 44100, "mp3", 128_000).suspicious) // an honest 128 kbps file
        assertFalse(AudioQuality.judge(AudioQuality.spectrum(tones(21900.0)), 44100, "flac", 900_000).suspicious)
    }

    @Test fun aLowSampleRateIsLowQuality() {
        val r = AudioQuality.judge(AudioQuality.spectrum(tones(10000.0, 22050)), 22050, "flac", 700_000)
        assertEquals(AudioQuality.Grade.Low, r.grade); assertTrue(r.suspicious)
        assertTrue(r.verdict.contains("取樣率只有 22.05 kHz")); assertEquals(11025.0, r.cutoffHz!!, 0.0)
    }

    @Test fun parsesTheSourceTrack() {
        val s = AudioPipeline.parseSource("251|opus|106.064|48000\n")
        assertEquals(AudioPipeline.SourceAudio("opus", 106.064, 48000, "251"), s)
        assertEquals("Opus · 106 kbps · 48 kHz", AudioPipeline.sourceText(s!!.codec, s.kbps, s.sampleRate))
        assertEquals("AAC · 129 kbps · 44.1 kHz", AudioPipeline.sourceText("mp4a.40.2", 129.4, 44100))
        assertNull(AudioPipeline.parseSource("18|NA|NA|NA")); assertNull(AudioPipeline.parseSource("garbage"))
    }

    @Test fun notesWhenTheOutputBitrateExceedsTheSource() {
        assertTrue(AudioPipeline.upscaleNote("mp3", "cbr", 320, 106.0)!!.contains("音質以來源為準"))
        assertNull(AudioPipeline.upscaleNote("mp3", "cbr", 128, 129.0))
        assertNull(AudioPipeline.upscaleNote("mp3", "v0", 320, 106.0))
        assertNull(AudioPipeline.upscaleNote("opus", "cbr", 320, 106.0))
        assertNull(AudioPipeline.upscaleNote("mp3", "cbr", 320, null))
    }
}
