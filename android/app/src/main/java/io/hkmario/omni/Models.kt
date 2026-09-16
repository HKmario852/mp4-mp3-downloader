package io.hkmario.omni

import kotlinx.serialization.Serializable
import java.util.UUID

@Serializable enum class State { PendingChoice, Queued, Analyzing, Downloading, Processing, RetryWait, Paused, Completed, Failed, Cancelled }
@Serializable data class TaskItem(
    val id: String = UUID.randomUUID().toString(), val url: String, val title: String = url,
    val outputFormat: String = "", val workPath: String? = null, val duration: Double? = null,
    val mode: String = "mp4", val height: Int = 1080, val kbps: Int = 320,
    val state: State = State.Queued, val groupId: String? = null, val groupRoot: Boolean = false,
    val artist: String = "", val album: String = "", val thumbnail: String? = null,
    val path: String? = null, val targetTree: String? = null, val directory: String = "", val bytes: Long = 0, val total: Long? = null,
    val progress: Float = 0f, val speed: Double = 0.0, val eta: Long = 0,
    val retry: Int = 0, val retryAt: Long = 0, val error: String? = null, val stderr: String? = null,
    val metadataStatus: String = "", val isUserEdited: Boolean = false, val coverUserEdited:Boolean=false, val history: Boolean = true,
    val createdAt: Long = System.currentTimeMillis(), val completedAt: Long? = null
)
@Serializable data class PlaylistGroup(val id: String = UUID.randomUUID().toString(), val title: String, val discoveryComplete: Boolean = false, val discoveryFailures: Int = 0, val notified: Boolean = false)
@Serializable data class Prefs(
 val concurrency:Int=5,val height:Int=1080,val kbps:Int=320,val cleanTitle:Boolean=true,val musicBrainz:Boolean=false,val wifiOnly:Boolean=true,
 val tree:String="",val mp3Tree:String?=null,val mp4Tree:String?=null,
 val textScale:Int=100,val uiScale:Int=100,val theme:String="dark",val language:String="zh-Hant",val startAtLogin:Boolean=false,val autoUpdate:Boolean=true,val resumeOnStart:Boolean=false,
 val monitorClipboard:Boolean=false,val tempDirectory:String="internal",val duplicateAction:String="rename",val autoRetry:Boolean=true,
 val retryCount:Int=3,val retrySeconds:Int=2,val cleanFailed:Boolean=false,val completionAction:String="none",val defaultType:String="video",
 val videoFormat:String="mp4",val audioFormat:String="mp3",val videoCodec:String="auto",val downloadSubtitles:Boolean=false,
 val subtitleLanguages:String="en,zh-Hant",val subtitleFormat:String="srt",val embedSubtitles:Boolean=false,val keepThumbnail:Boolean=true,
 val embedThumbnail:Boolean=true,val keepMetadata:Boolean=true,val videoNaming:String="{title}",val audioNaming:String="{title}",
 val proxyMode:String="system",val proxyUrl:String="",val timeoutSeconds:Int=30,val connectionRetries:Int=3,val fragments:Int=1,
 val limitKiB:Int=0,val scheduleLimit:Boolean=false,val limitStart:String="18:00",val limitEnd:String="23:00",val scheduledKiB:Int=1024,
 val allowedNetwork:String="any",val cookieFile:String="",val notifyComplete:Boolean=true,val notifyFailure:Boolean=true,val notifyAll:Boolean=true,
 val systemNotifications:Boolean=true,val sound:Boolean=false,val soundName:String="default",val taskbarProgress:Boolean=true,
 val quietHours:Boolean=false,val quietStart:String="22:00",val quietEnd:String="08:00",
 val releaseRepository:String="HKmario852/mp4-mp3-downloader"
) {
 fun treeFor(mode:String)=(if(mode=="mp3")mp3Tree else mp4Tree)?:tree
 fun validate()=Options.validate(this)
 fun effectiveLimit()=if(scheduleLimit&&Options.inPeriod(limitStart,limitEnd))scheduledKiB else limitKiB
 fun isQuiet()=quietHours&&Options.inPeriod(quietStart,quietEnd)
}
val TaskItem.extension:String get()=outputFormat.ifBlank{mode}

class SessionGrants {
    @Volatile var cellularAll = false
    val tasks = java.util.concurrent.ConcurrentHashMap.newKeySet<String>()
    @Volatile var suppressUnknownSpace = false
    fun mayUseCellular(id: String) = cellularAll || id in tasks
}
object Rules {
    fun inQueue(t: TaskItem) = t.state in listOf(State.PendingChoice,State.Queued,State.Analyzing,State.Downloading,State.Processing,State.RetryWait,State.Paused)
    private val forbidden = Regex("[\\\\/:*?\"<>|\\x00-\\x1f]")
    private val reserved = Regex("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\.|$)", RegexOption.IGNORE_CASE)
    fun titleError(s: String): String? = when {
        forbidden.containsMatchIn(s) -> "不可包含字元：\\ / : * ? \" < > | 或控制字元"
        s.length > 180 -> "歌曲名過長（最多 180 字元）"
        reserved.containsMatchIn(s) || s == "." || s == ".." || s.endsWith(' ') || s.endsWith('.') -> "不可使用系統保留檔名或結尾空格、句號"
        else -> null
    }
    fun safeName(s: String): String { val n = forbidden.replace(s, " ").trim().trimEnd('.').take(160).ifBlank { "未命名" }; return if (reserved.containsMatchIn(n)) "_$n" else n }
    fun cleanTitle(s: String) = s.replace(Regex("\\s*[\\[(【](official\\s*(music\\s*)?video|official audio|lyrics?|歌詞|官方MV|MV|HD|4K)[\\])】]\\s*$", RegexOption.IGNORE_CASE), "").trim()
    fun validUrl(s: String): Boolean = try { val u = java.net.URI(s); s.length <= 8192 && u.scheme == "https" && !u.host.isNullOrBlank() && u.rawUserInfo == null && u.port == -1 } catch (_: Exception) { false }
    fun matches(t: TaskItem, q: String): Boolean { val fields = listOf(t.title,t.url,t.path?.substringAfterLast('/') ?: "",t.artist,t.album); return q.split(' ').filter { it.isNotEmpty() }.all { k -> fields.any { it.contains(k,true) } } }
}
class Ema {
    private var previous = 0L; private var smooth = 0.0
    fun sample(bytes: Long, total: Long?, elapsed: Double, active: Boolean): Pair<Double, Long> {
        val raw = if (elapsed > 0) (bytes - previous).coerceAtLeast(0) / elapsed else 0.0; previous = bytes
        smooth = if (!active || raw == 0.0) 0.0 else if (smooth == 0.0) raw else .25 * raw + .75 * smooth
        return smooth to if (smooth > 0 && total != null) kotlin.math.ceil((total - bytes).coerceAtLeast(0) / smooth).toLong() else 0L
    }
}
