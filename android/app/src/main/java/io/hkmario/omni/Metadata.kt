package io.hkmario.omni
import okhttp3.OkHttpClient
import okhttp3.Request
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.sync.withPermit
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.URLEncoder
import java.util.concurrent.TimeUnit

data class MusicResult(val status:String,val title:String?=null,val artist:String?=null,val album:String?=null,val cover:Art?=null,val choices:List<MusicCandidate> = emptyList(),val tags:Map<String,String> = emptyMap())
object Metadata {
    private val client=OkHttpClient.Builder().connectTimeout(15,TimeUnit.SECONDS).readTimeout(20,TimeUnit.SECONDS).build()
    private val pool=Semaphore(5);private val rate=Mutex();private var next=0L
    private fun request(url: String)=Request.Builder().url(url.replaceFirst("http://","https://")).header("User-Agent","MP4MP3Downloader/0.2.5 (https://github.com/HKmario852)").build()
    suspend fun fetch(url: String,description: String,type: Int): Art? = withContext(Dispatchers.IO) {
        client.newCall(request(url)).awaitResponse().use { r ->if(!r.isSuccessful)return@withContext null;val body=r.body ?: return@withContext null;if(body.contentLength()>32*1024*1024) return@withContext null
            val out=java.io.ByteArrayOutputStream();body.byteStream().use{i->val b=ByteArray(65536);while(true){val n=i.read(b);if(n<0)break;if(out.size()+n>32*1024*1024)throw java.io.IOException("封面過大");out.write(b,0,n)}};val bytes=out.toByteArray();val mime=when{bytes.size>2&&bytes[0]==0xff.toByte()&&bytes[1]==0xd8.toByte()->"image/jpeg";bytes.size>8&&bytes[0]==0x89.toByte()&&bytes[1]==80.toByte()->"image/png";bytes.size>12&&String(bytes,8,4)=="WEBP"->"image/webp";else->return@withContext null};Art(bytes,mime,description,type) }
    }
    fun prepare(title:String,artist:String):Pair<String,String> {
        var name=title.replace(Regex("""\s*[\(\[【](?:(?:official|music|lyrics?|audio|video|mv|hd|4k|visuali[sz]er|字幕|歌詞|官方)\s*)+[\)\]】]\s*$""",RegexOption.IGNORE_CASE),"").trim();var singer=artist.trim()
        val parts=name.split(Regex("""\s+[-–—]\s+"""));if(parts.size==2&&parts.all{it.isNotBlank()}&&(singer.isBlank()||normalized(parts[0])==normalized(singer))){singer=parts[0];name=parts[1]}
        return name.ifBlank{title} to singer
    }
    private fun normalized(text:String)=java.text.Normalizer.normalize(text,java.text.Normalizer.Form.NFKC).filter{it.isLetterOrDigit()}.uppercase(java.util.Locale.ROOT)
    private fun array(root:JSONObject,key:String):List<JSONObject>{val a=root.optJSONArray(key)?:return emptyList();return(0 until a.length()).mapNotNull{a.optJSONObject(it)}}
    private fun artists(recording:JSONObject)=array(recording,"artist-credit").map{it.optString("name").ifBlank{it.optJSONObject("artist")?.optString("name")?:""}}.filter{it.isNotBlank()}
    suspend fun lookup(title:String,artist:String,duration:Double?):MusicResult {
        val first=lookupOnce(title,artist,duration)
        return if(first.title==null&&first.status.contains("查無")&&artist.isNotBlank())lookupOnce(title,"",duration)else first
    }
    internal suspend fun json(url:String,musicBrainz:Boolean=true):JSONObject=withContext(Dispatchers.IO){
        suspend fun read():JSONObject=client.newCall(request(url)).awaitResponse().use{r->if(!r.isSuccessful)throw java.io.IOException("服務暫時無法使用 (${r.code})");JSONObject(r.body!!.string())}
        if(!musicBrainz)read()else rate.withLock{delay((next-System.currentTimeMillis()).coerceAtLeast(0));next=System.currentTimeMillis()+1000;read()}
    }
    private suspend fun lookupOnce(title:String,artist:String,duration:Double?):MusicResult=pool.withPermit {
        try {
            val(name,singer)=prepare(title,artist)
            fun escaped(s:String)=s.replace("\\","\\\\").replace("\"","\\\"")
            val query="recording:\"${escaped(name)}\""+if(singer.isBlank())""else" AND artist:\"${escaped(singer)}\""
            var root:JSONObject?=null
            for(attempt in 0..1){
                root=rate.withLock{delay((next-System.currentTimeMillis()).coerceAtLeast(0));next=System.currentTimeMillis()+1000
                    withContext(Dispatchers.IO){client.newCall(request("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=25&query=${URLEncoder.encode(query,"UTF-8")}")).execute().use{r->
                        if(r.code==429||r.code==503){next=System.currentTimeMillis()+((r.header("Retry-After")?.toLongOrNull()?:5L).coerceAtLeast(5L)*1000);null}
                        else{if(!r.isSuccessful)throw java.io.IOException("MusicBrainz unavailable");JSONObject(r.body!!.string())}
                    }}
                }
                if(root!=null)break
                if(next-System.currentTimeMillis()>30000)break
            }
            if(root==null)return@withPermit MusicResult("MusicBrainz：服務暫時無法使用，已保留來源資料")
            val matches=array(root,"recordings").filter{r->normalized(r.optString("title"))==normalized(name)&&artists(r).isNotEmpty()&&(duration==null||!r.has("length")||kotlin.math.abs(r.optDouble("length")/1000-duration)<=maxOf(8.0,duration*.04))&&
                if(singer.isNotBlank())artists(r).any{normalized(it)==normalized(singer)}||normalized(artists(r).joinToString(" & "))==normalized(singer)
                else r.optInt("score")>=95&&duration!=null&&duration>0&&r.has("length")&&kotlin.math.abs(r.optDouble("length")/1000-duration)<=maxOf(8.0,duration*.04)
            }.sortedBy{r->if(duration!=null&&r.has("length"))kotlin.math.abs(r.optDouble("length")/1000-duration)else Double.MAX_VALUE}
            if(matches.isEmpty())return@withPermit MusicResult("MusicBrainz：查無可靠配對，已保留來源資料")
            if(matches.map{normalized(artists(it).joinToString(" & "))}.distinct().size!=1)return@withPermit MusicResult("MusicBrainz：有多個可能結果，請選擇版本",choices=MusicRecognition.choices(matches))
            val versions=MusicRecognition.choices(matches);if(versions.isNotEmpty()){if(versions.size>1)return@withPermit MusicResult("請選擇歌曲及專輯版本",choices=versions);return@withPermit MusicRecognition.recording(versions.first().recordingId,versions.first().releaseId)}
            val matchedTitle=matches.first().getString("title");val matchedArtist=artists(matches.first()).joinToString(" & ")
            val releases=matches.flatMap{array(it,"releases")}.filter{it.optString("status") in listOf("","Official")}.sortedByDescending{it.optJSONObject("release-group")?.optString("primary-type")=="Album"}.distinctBy{it.optString("id")}.take(5)
            for(release in releases){val id=runCatching{java.util.UUID.fromString(release.optString("id"))}.getOrNull()?:continue
                try {
                    val coverJson=withContext(Dispatchers.IO){client.newCall(request("https://coverartarchive.org/release/$id")).execute().use{r->if(r.isSuccessful)JSONObject(r.body!!.string())else null}}?:continue
                    for(front in array(coverJson,"images").filter{it.optBoolean("front")&&it.optBoolean("approved")}){
                        val art=fetch(front.getString("image"),"Album front",3)?:continue
                        if(!AlbumArtwork.accept(art.bytes))continue
                        return@withPermit MusicResult("MusicBrainz：已配對並嵌入專輯封面",matchedTitle,matchedArtist,release.optString("title"),art)
                    }
                }catch(e:kotlinx.coroutines.CancellationException){throw e}catch(_:Exception){}
            }
            MusicResult("MusicBrainz：已配對標籤，未取得專輯封面",matchedTitle,matchedArtist,releases.firstOrNull()?.optString("title"))
        }catch(e:kotlinx.coroutines.CancellationException){throw e}catch(_:Exception){MusicResult("MusicBrainz：服務暫時無法使用，已保留來源資料")}
    }
}
