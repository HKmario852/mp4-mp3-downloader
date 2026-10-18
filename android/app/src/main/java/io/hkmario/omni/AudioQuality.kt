package io.hkmario.omni

import android.content.Context
import android.media.AudioFormat
import android.media.MediaCodec
import android.media.MediaExtractor
import android.media.MediaFormat
import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.log10
import kotlin.math.max
import kotlin.math.pow
import kotlin.math.sin

/**
 * Estimates how much of the frequency range an audio file really contains (same method and thresholds as the
 * Windows AudioQuality). Lossy encoders cut the top of the spectrum with a steep low-pass, and converting to a
 * bigger bitrate or to FLAC cannot bring it back, so a FLAC or "320 kbps" MP3 whose spectrum stops early was made
 * from a weaker source. This is an estimate: some recordings simply have no high frequencies.
 */
object AudioQuality {
    enum class Grade { Unknown, Low, Medium, High, FullBand }
    data class Result(val grade: Grade, val cutoffHz: Double?, val codec: String, val sampleRate: Int, val bitRate: Long?, val suspicious: Boolean, val verdict: String)

    private const val SIZE = 4096
    private const val MAX_US = 120_000_000L

    /** Average power per FFT bin (Hann window, 4096 points, no overlap), in dB. Index i is i × rate / 4096 Hz. */
    fun spectrum(samples: FloatArray): DoubleArray {
        val power = DoubleArray(SIZE / 2); val re = DoubleArray(SIZE); val im = DoubleArray(SIZE); var windows = 0
        var start = 0
        while (start + SIZE <= samples.size) {
            for (i in 0 until SIZE) { re[i] = samples[start + i] * (0.5 - 0.5 * cos(2 * PI * i / (SIZE - 1))); im[i] = 0.0 }
            fft(re, im)
            for (i in 0 until SIZE / 2) power[i] += re[i] * re[i] + im[i] * im[i]
            windows++; start += SIZE
        }
        return DoubleArray(SIZE / 2) { if (windows == 0) -200.0 else 10 * log10(power[it] / windows + 1e-20) }
    }

    /** In-place iterative radix-2 FFT; the length must be a power of two. */
    fun fft(re: DoubleArray, im: DoubleArray) {
        val n = re.size; var j = 0
        for (i in 1 until n) {
            var bit = n shr 1
            while (j and bit != 0) { j = j xor bit; bit = bit shr 1 }
            j = j xor bit
            if (i < j) { var t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t }
        }
        var len = 2
        while (len <= n) {
            val angle = -2 * PI / len; val wr = cos(angle); val wi = sin(angle)
            var i = 0
            while (i < n) {
                var cr = 1.0; var ci = 0.0
                for (k in 0 until len / 2) {
                    val a = i + k; val b = a + len / 2
                    val tr = re[b] * cr - im[b] * ci; val ti = re[b] * ci + im[b] * cr
                    re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti
                    val ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr
                }
                i += len
            }
            len = len shl 1
        }
    }

    /** The frequency where the spectrum falls off a cliff; null for full band, NaN when silent or too quiet. */
    fun cutoff(db: DoubleArray, sampleRate: Int): Double? {
        val binHz = sampleRate.toDouble() / SIZE; val nyquist = sampleRate / 2.0
        fun level(from: Double, to: Double): Double {
            val a = (from / binHz).toInt().coerceIn(1, db.size - 1); val b = (to / binHz).toInt().coerceIn(a + 1, db.size)
            var sum = 0.0; for (i in a until b) sum += 10.0.pow(db[i] / 10)
            return 10 * log10(sum / (b - a) + 1e-20)
        }
        val reference = level(1000.0, 4000.0)
        if (reference < -120) return Double.NaN
        var f = 12000.0
        while (f <= nyquist - 1200) {
            val below = level(f - 1000, f)
            if (below < reference - 70) break // already in the noise floor: no cliff to find
            var highest = Double.NEGATIVE_INFINITY; var g = f
            while (g + 500 <= nyquist - 200) { highest = max(highest, level(g, g + 500)); g += 500 }
            if (below - highest >= 25) return f
            f += 100
        }
        return null
    }

    fun gradeOf(cutoff: Double?) = when {
        cutoff == null -> Grade.FullBand
        cutoff.isNaN() -> Grade.Unknown
        cutoff < 17500 -> Grade.Low
        cutoff < 19200 -> Grade.Medium
        cutoff < 20800 -> Grade.High
        else -> Grade.FullBand
    }

    private fun lossless(codec: String) = codec in listOf("flac", "alac", "wav", "raw") || codec.startsWith("pcm")

    fun judge(db: DoubleArray, sampleRate: Int, codec: String, bitRate: Long?): Result {
        // No cliff below Nyquist, but a low sample rate is itself the limit (22.05 kHz holds nothing above 11 kHz)
        val cutoff = cutoff(db, sampleRate) ?: (sampleRate / 2.0).takeIf { it < 20800 }; val grade = gradeOf(cutoff)
        val lowRate = sampleRate < 32000 && grade != Grade.Unknown
        val fakeHigh = codec == "mp3" && (bitRate ?: 0) >= 256_000 && grade in listOf(Grade.Low, Grade.Medium)
        val fakeLossless = lossless(codec) && grade in listOf(Grade.Low, Grade.Medium, Grade.High)
        val band = when (grade) {
            Grade.Low -> "約 128 kbps 或以下嘅有損音源 / about 128 kbps or lower lossy source"
            Grade.Medium -> "約 160–192 kbps 嘅有損音源 / about 160–192 kbps lossy source"
            Grade.High -> "約 224–320 kbps 或 Opus 等級 / about 224–320 kbps or Opus quality"
            Grade.FullBand -> "頻寬完整 / full bandwidth"
            Grade.Unknown -> "音量太低，無法判斷 / too quiet to judge"
        }
        val (zh, en) = band.split(" / ")
        val rate = java.text.DecimalFormat("0.##", java.text.DecimalFormatSymbols(java.util.Locale.ROOT)).format(sampleRate / 1000.0); val top = java.text.DecimalFormat("0.#", java.text.DecimalFormatSymbols(java.util.Locale.ROOT)).format(sampleRate / 2000.0)
        val verdict = when {
            lowRate -> "取樣率只有 $rate kHz，最高頻率約 $top kHz，屬低音質 / Sample rate is only $rate kHz, so nothing above about $top kHz: low quality"
            fakeLossless -> "疑似假無損：由有損音源轉出（$zh） / Likely fake lossless: made from a lossy source ($en)"
            fakeHigh -> "疑似升頻：檔案標示高位元率，但頻譜似$zh / Likely upscaled: high bitrate label, but the spectrum looks like $en"
            else -> band
        }
        return Result(grade, cutoff?.takeUnless { it.isNaN() }, codec, sampleRate, bitRate, fakeLossless || fakeHigh || lowRate, verdict)
    }

    /** Decodes up to two minutes on the device and judges the spectrum. [path] is a file path or content:// URI. */
    suspend fun analyze(context: Context, path: String): Result = withContext(Dispatchers.IO) {
        val extractor = MediaExtractor(); var codec: MediaCodec? = null
        try {
            if (path.startsWith("content://")) extractor.setDataSource(context, Uri.parse(path), null) else extractor.setDataSource(path)
            val index = (0 until extractor.trackCount).firstOrNull { extractor.getTrackFormat(it).getString(MediaFormat.KEY_MIME)?.startsWith("audio/") == true } ?: error("找不到音訊")
            extractor.selectTrack(index); val format = extractor.getTrackFormat(index)
            val mime = format.getString(MediaFormat.KEY_MIME)!!
            val name = when (mime) { "audio/mpeg" -> "mp3"; "audio/flac", "audio/x-flac" -> "flac"; "audio/raw" -> path.substringAfterLast('.', "").lowercase().takeIf { it in listOf("flac", "wav") } ?: "raw"; "audio/opus" -> "opus"; "audio/mp4a-latm" -> "aac"; "audio/vorbis" -> "vorbis"; else -> mime.removePrefix("audio/") }
            var rate = format.getInteger(MediaFormat.KEY_SAMPLE_RATE); var channels = format.getInteger(MediaFormat.KEY_CHANNEL_COUNT)
            val durationUs = if (format.containsKey(MediaFormat.KEY_DURATION)) format.getLong(MediaFormat.KEY_DURATION) else 0L
            val bytes = if (path.startsWith("content://")) context.contentResolver.openFileDescriptor(Uri.parse(path), "r")?.use { it.statSize } ?: -1 else File(path).length()
            val bitRate = if (format.containsKey(MediaFormat.KEY_BIT_RATE)) format.getInteger(MediaFormat.KEY_BIT_RATE).toLong() else if (bytes > 0 && durationUs > 0) bytes * 8_000_000 / durationUs else null
            val out = FloatCollector()
            if (mime == "audio/raw") { // some FLAC extractors already deliver PCM
                val encoding = if (format.containsKey(MediaFormat.KEY_PCM_ENCODING)) format.getInteger(MediaFormat.KEY_PCM_ENCODING) else AudioFormat.ENCODING_PCM_16BIT
                val buffer = ByteBuffer.allocate(4 * 1024 * 1024)
                while (extractor.sampleTrackIndex >= 0 && extractor.sampleTime < MAX_US) {
                    currentCoroutineContext().ensureActive(); buffer.clear()
                    val size = extractor.readSampleData(buffer, 0); if (size < 0) break
                    buffer.position(0); buffer.limit(size); out.add(buffer, encoding, channels)
                    if (!extractor.advance()) break
                }
            } else {
                val decoder = MediaCodec.createDecoderByType(mime); codec = decoder; decoder.configure(format, null, null, 0); decoder.start()
                var inputEnd = false; var outputEnd = false; var encoding = AudioFormat.ENCODING_PCM_16BIT; val info = MediaCodec.BufferInfo()
                val deadline = android.os.SystemClock.elapsedRealtime() + 90_000
                while (!outputEnd) {
                    currentCoroutineContext().ensureActive(); check(android.os.SystemClock.elapsedRealtime() < deadline) { "音質分析逾時" }
                    if (!inputEnd) { val i = decoder.dequeueInputBuffer(10_000); if (i >= 0) { val buffer = decoder.getInputBuffer(i)!!; val size = extractor.readSampleData(buffer, 0)
                        if (size < 0 || extractor.sampleTime >= MAX_US) { decoder.queueInputBuffer(i, 0, 0, 0, MediaCodec.BUFFER_FLAG_END_OF_STREAM); inputEnd = true } else { decoder.queueInputBuffer(i, 0, size, extractor.sampleTime, 0); extractor.advance() } } }
                    val o = decoder.dequeueOutputBuffer(info, 10_000)
                    if (o == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) { val f = decoder.outputFormat
                        encoding = if (f.containsKey(MediaFormat.KEY_PCM_ENCODING)) f.getInteger(MediaFormat.KEY_PCM_ENCODING) else AudioFormat.ENCODING_PCM_16BIT
                        rate = f.getInteger(MediaFormat.KEY_SAMPLE_RATE); channels = f.getInteger(MediaFormat.KEY_CHANNEL_COUNT) }
                    if (o >= 0) { try { if (info.size > 0) { val data = decoder.getOutputBuffer(o)!!; data.position(info.offset); data.limit(info.offset + info.size); out.add(data, encoding, channels) }
                        outputEnd = info.flags and MediaCodec.BUFFER_FLAG_END_OF_STREAM != 0 } finally { decoder.releaseOutputBuffer(o, false) } }
                }
            }
            judge(spectrum(out.toArray()), rate, name, bitRate)
        } finally { try { codec?.stop() } catch (_: Exception) {}; codec?.release(); extractor.release() }
    }

    /** Collects decoded PCM as mono floats. */
    private class FloatCollector {
        private var data = FloatArray(1 shl 20); private var size = 0
        fun add(buffer: ByteBuffer, encoding: Int, channels: Int) {
            val b = buffer.slice().order(ByteOrder.LITTLE_ENDIAN); val ch = max(1, channels)
            val frameBytes = ch * when (encoding) { AudioFormat.ENCODING_PCM_FLOAT, AudioFormat.ENCODING_PCM_32BIT -> 4; AudioFormat.ENCODING_PCM_24BIT_PACKED -> 3; AudioFormat.ENCODING_PCM_8BIT -> 1; else -> 2 }
            while (b.remaining() >= frameBytes) {
                var sum = 0f
                repeat(ch) { sum += when (encoding) {
                    AudioFormat.ENCODING_PCM_FLOAT -> b.float
                    AudioFormat.ENCODING_PCM_32BIT -> b.int / 2147483648f
                    AudioFormat.ENCODING_PCM_24BIT_PACKED -> { val lo = b.get().toInt() and 255; val mid = b.get().toInt() and 255; val hi = b.get().toInt(); ((hi shl 16) or (mid shl 8) or lo) / 8388608f }
                    AudioFormat.ENCODING_PCM_8BIT -> ((b.get().toInt() and 255) - 128) / 128f
                    else -> b.short / 32768f } }
                if (size == data.size) data = data.copyOf(size * 2)
                data[size++] = sum / ch
            }
        }
        fun toArray() = data.copyOf(size)
    }
}
