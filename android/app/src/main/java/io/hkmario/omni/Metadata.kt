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

object Metadata {
    private val client=OkHttpClient.Builder().connectTimeout(15,TimeUnit.SECONDS).readTimeout(20,TimeUnit.SECONDS).build()
    private val pool=Semaphore(5);private val rate=Mutex();private var next=0L
    private fun request(url: String)=Request.Builder().url(url.replaceFirst("http://","https://")).header("User-Agent","OmniDownloader/0.1.0 (local open-source music tagger)").build()
    suspend fun fetch(url: String,description: String,type: Int): Art? = withContext(Dispatchers.IO) {
        client.newCall(request(url)).execute().use { r ->if(!r.isSuccessful)return@withContext null;val body=r.body ?: return@withContext null;if(body.contentLength()>32*1024*1024) return@withContext null
            val out=java.io.ByteArrayOutputStream();body.byteStream().use{i->val b=ByteArray(65536);while(true){val n=i.read(b);if(n<0)break;if(out.size()+n>32*1024*1024)throw java.io.IOException("封面過大");out.write(b,0,n)}};val bytes=out.toByteArray();val mime=when{bytes.size>2&&bytes[0]==0xff.toByte()&&bytes[1]==0xd8.toByte()->"image/jpeg";bytes.size>8&&bytes[0]==0x89.toByte()&&bytes[1]==80.toByte()->"image/png";bytes.size>12&&String(bytes,8,4)=="WEBP"->"image/webp";else->return@withContext null};Art(bytes,mime,description,type) }
    }
    suspend fun find(title: String,artist: String): Art? = pool.withPermit {
        if(artist.isBlank())return@withPermit null
        try {
            val root=rate.withLock { delay((next-System.currentTimeMillis()).coerceAtLeast(0));next=System.currentTimeMillis()+1000
                withContext(Dispatchers.IO) { val q=URLEncoder.encode("recording:\"${title.replace("\"","")}\" AND artist:\"${artist.replace("\"","")}\"","UTF-8");client.newCall(request("https://musicbrainz.org/ws/2/recording/?fmt=json&limit=5&query=$q")).execute().use{r->if(r.code==429||r.code==503)next=System.currentTimeMillis()+5000;if(!r.isSuccessful)return@withContext null;JSONObject(r.body!!.string())} } } ?: return@withPermit null
            val rs=root.getJSONArray("recordings");val matches=(0 until rs.length()).map{rs.getJSONObject(it)}.filter { r -> r.optString("title").equals(title,true)&&r.optJSONArray("artist-credit")?.let{a->(0 until a.length()).any{a.getJSONObject(it).optString("name").equals(artist,true)}}==true }
            if(matches.size!=1)return@withPermit null;val release=matches.first().optJSONArray("releases")?.optJSONObject(0)?.optString("id") ?: return@withPermit null
            val coverJson=withContext(Dispatchers.IO){client.newCall(request("https://coverartarchive.org/release/$release")).execute().use{r->if(r.isSuccessful)JSONObject(r.body!!.string())else null}} ?: return@withPermit null
            val imgs=coverJson.getJSONArray("images");val front=(0 until imgs.length()).map{imgs.getJSONObject(it)}.firstOrNull{it.optBoolean("front")&&it.optBoolean("approved")} ?: return@withPermit null
            fetch(front.getString("image"),"Album front",3)
        } catch(e: kotlinx.coroutines.CancellationException) { throw e } catch(_: Exception) { null }
    }
}
