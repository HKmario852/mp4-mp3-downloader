package io.hkmario.omni
import android.content.Context
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import okhttp3.*
import org.json.JSONObject
import java.net.URLEncoder
import java.util.UUID
import java.util.concurrent.TimeUnit

data class MusicCandidate(val recordingId:String,val releaseId:String?,val title:String,val artist:String,val album:String,val edition:String=""){override fun toString()="$title · $artist · $album · $edition"}
data class MusicQuestion(val choices:List<MusicCandidate>,val answer:CompletableDeferred<MusicCandidate?> = CompletableDeferred())
object MusicRecognition {
 fun recordingId(value:String):String?{val v=value.trim();runCatching{UUID.fromString(v)}.getOrNull()?.let{return it.toString()};val uri=runCatching{java.net.URI(v)}.getOrNull()?:return null;if(uri.host !in listOf("musicbrainz.org","www.musicbrainz.org"))return null;val parts=uri.path.trim('/').split('/');return if(parts.size==2&&parts[0]=="recording")runCatching{UUID.fromString(parts[1]).toString()}.getOrNull()else null}
 private fun array(r:JSONObject,k:String):List<JSONObject>{val a=r.optJSONArray(k)?:return emptyList();return(0 until a.length()).mapNotNull{a.optJSONObject(it)}}
 private fun artist(r:JSONObject)=array(r,"artist-credit").joinToString(" & "){it.optString("name").ifBlank{it.optJSONObject("artist")?.optString("name").orEmpty()}}
 internal fun choices(rows:List<JSONObject>)=rows.flatMap{r->val releases=array(r,"releases");if(releases.isEmpty())listOf(MusicCandidate(r.optString("id"),null,r.optString("title"),artist(r),""))else releases.map{a->MusicCandidate(r.optString("id"),a.optString("id"),r.optString("title"),artist(r),a.optString("title"),listOf(a.optString("date"),a.optString("country"),a.optString("id").take(8)).filter{it.isNotBlank()}.joinToString(" · "))}}.distinctBy{it.recordingId to it.releaseId}.take(25)
 suspend fun search(title:String,singer:String):List<MusicCandidate>{
  val(name,by)=Metadata.prepare(title,singer);fun escape(s:String)=s.replace("\\","\\\\").replace("\"","\\\"")
  suspend fun query(a:String):List<MusicCandidate>{val q="recording:\"${escape(name)}\""+if(a.isBlank())""else" AND artist:\"${escape(a)}\"";return choices(array(Metadata.json("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=25&query=${URLEncoder.encode(q,"UTF-8")}"),"recordings"))}
  return query(by).ifEmpty{if(by.isBlank())emptyList()else query("")}
 }
 suspend fun recording(id:String,releaseId:String?=null):MusicResult {
  UUID.fromString(id);val r=Metadata.json("https://musicbrainz.org/ws/2/recording/$id?inc=artists+releases+release-groups&fmt=json");val options=choices(listOf(r))
  if(releaseId==null&&options.size>1)return MusicResult("請選擇專輯版本",choices=options)
  val chosen=if(releaseId==null)options.firstOrNull()else options.firstOrNull{it.releaseId==releaseId}?:error("專輯不屬於此歌曲")
  val tags=mutableMapOf("TIT2" to r.optString("title"),"TPE1" to artist(r));var cover:Art?=null
  chosen?.releaseId?.let{rid->UUID.fromString(rid);val release=Metadata.json("https://musicbrainz.org/ws/2/release/$rid?inc=recordings+artist-credits&fmt=json");tags["TALB"]=release.optString("title");tags["TPE2"]=artist(release);release.optString("date").takeIf{it.length>=4}?.let{tags["TYER"]=it.take(4)}
   for(m in array(release,"media"))for(t in array(m,"tracks"))if(t.optJSONObject("recording")?.optString("id")==id){tags["TRCK"]=t.optString("number");tags["TPOS"]=m.optInt("position",1).toString()}
   try{val images=Metadata.json("https://coverartarchive.org/release/$rid",false);for(image in array(images,"images")){if(!image.optBoolean("front"))continue;val art=Metadata.fetch(image.getString("image"),"Album front",3);if(art!=null&&AlbumArtwork.accept(art.bytes)){cover=art;break}}}catch(e:CancellationException){throw e}catch(_:Exception){}
  }
  return MusicResult(if(cover==null)"MusicBrainz：已配對，未取得封面"else"MusicBrainz：已配對",tags["TIT2"],tags["TPE1"],tags["TALB"],cover,tags=tags)
 }
 private val rate=Mutex();private var next=0L
 private val http=OkHttpClient.Builder().callTimeout(30,TimeUnit.SECONDS).build()
 suspend fun scan(context:Context,path:String,key:String):MusicResult {
  if(key.isBlank())return MusicResult("Scan 尚未設定：請在「設定 → 格式」填入 AcoustID application API key。")
  val fp=withTimeout(90000){AudioFingerprint.calculate(context,path)}
  val root=rate.withLock{delay((next-System.currentTimeMillis()).coerceAtLeast(0));next=System.currentTimeMillis()+1000
   withContext(Dispatchers.IO){val body=FormBody.Builder().add("client",key.trim()).add("duration",fp.duration.toString()).add("fingerprint",fp.fingerprint).add("meta","recordingids").add("format","json").build();http.newCall(Request.Builder().url("https://api.acoustid.org/v2/lookup").post(body).header("User-Agent","MP4MP3Downloader/0.2.4").build()).execute().use{r->check(r.isSuccessful){"AcoustID 服務無法使用"};JSONObject(r.body!!.string())}}
  }
  check(root.optString("status")=="ok"){"AcoustID 查詢失敗，請檢查 application key"}
  val results=array(root,"results");val best=results.maxOfOrNull{it.optDouble("score",0.0)}?:0.0;if(best<.95)return MusicResult("AcoustID：未找到可靠配對")
  val ids=results.filter{it.optDouble("score",0.0)>=maxOf(.8,best-.10)}.flatMap{array(it,"recordings")}.map{it.getString("id")}.distinct().take(10)
  if(ids.isEmpty())return MusicResult("AcoustID：未找到可靠配對")
  if(ids.size==1)return recording(ids.single())
  return MusicResult("請選擇歌曲版本",choices=ids.flatMap{choices(listOf(Metadata.json("https://musicbrainz.org/ws/2/recording/$it?inc=artists+releases&fmt=json")))})
 }
 suspend fun recognize(context:Context,path:String,key:String,title:String,artist:String,duration:Double?):MusicResult {
  val text=Metadata.lookup(title,artist,duration);if(text.title!=null)return text
  if(key.isBlank())return text.copy(status=text.status+" · Scan 未設定 application API key")
  return try{val scan=scan(context,path,key);if(scan.title==null&&scan.choices.isEmpty())text else scan}catch(_:TimeoutCancellationException){text.copy(status=text.status+" · Scan 逾時，已保留來源資料")}catch(e:CancellationException){throw e}catch(_:Exception){text.copy(status=text.status+" · Scan 暫時無法使用")}
 }
}
