package io.hkmario.omni
import android.graphics.BitmapFactory
import org.json.JSONObject
object AlbumArtwork {
 fun nearSquare(w:Int,h:Int)=w>=100&&h>=100&&maxOf(w,h).toDouble()/minOf(w,h)<=1.15
 fun accept(bytes:ByteArray):Boolean{val opts=BitmapFactory.Options().apply{inJustDecodeBounds=true};BitmapFactory.decodeByteArray(bytes,0,bytes.size,opts);return nearSquare(opts.outWidth,opts.outHeight)}
 suspend fun source(info:JSONObject):Art? = kotlinx.coroutines.withTimeoutOrNull(25000){
  val a=info.optJSONArray("thumbnails");val rows=(0 until (a?.length()?:0)).mapNotNull{a?.optJSONObject(it)}.filter{!it.has("width")||!it.has("height")||nearSquare(it.optInt("width"),it.optInt("height"))}.sortedByDescending{it.optInt("width")}
  val urls=(rows.map{it.optString("url")}.take(8)+info.optString("thumbnail")).filter{it.startsWith("https://")}.distinct()
  for(url in urls)try{val art=Metadata.fetch(url,"Source album artwork",3);if(art!=null&&accept(art.bytes))return@withTimeoutOrNull art}catch(e:kotlinx.coroutines.CancellationException){throw e}catch(_:Exception){}
  null
 }
}
