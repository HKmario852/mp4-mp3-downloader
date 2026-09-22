package io.hkmario.omni
import android.net.Uri
import kotlinx.coroutines.*
import java.io.File
import java.util.UUID
import java.security.MessageDigest

object AcoustIdClient { fun resolve(custom:String)=custom.trim().ifBlank { "wdBJF1kUQS" } }
object TagReview {
 val fields=listOf("TIT2" to "標題","TPE1" to "演出者","TALB" to "專輯","TPE2" to "專輯演出者","TRCK" to "曲目","TPOS" to "光碟","TYER" to "年份","TDOR" to "原始發行日期","TCON" to "類型","TCOM" to "作曲者","TPUB" to "發行商標","TSRC" to "ISRC","TXXX:MusicBrainz Recording Id" to "MusicBrainz 錄音 ID","TXXX:MusicBrainz Album Id" to "MusicBrainz 發行 ID","TSOT" to "標題排序方式","TSOP" to "演出者排序方式")
 fun input(engine:Engine,path:String)=if(path.startsWith("content://"))engine.context.contentResolver.openInputStream(Uri.parse(path))?:error("檔案權限已失效")else File(path).inputStream()
 fun hash(engine:Engine,path:String):String {val digest=MessageDigest.getInstance("SHA-256");input(engine,path).use{val b=ByteArray(65536);while(true){val n=it.read(b);if(n<0)break;digest.update(b,0,n)}};return digest.digest().joinToString(""){"%02x".format(it)}}
 suspend fun read(engine:Engine,song:TaskItem)=withContext(Dispatchers.IO){val source=File(engine.context.cacheDir,"review-${UUID.randomUUID()}.mp3");try{input(engine,song.path!!).use{i->source.outputStream().use{i.copyTo(it)}};val d=Id3.read(source);SongTags(fields.associate{(id,_)->id to d.text(if(id=="TYER"&&d.version==4)"TDRC"else if(id=="TDOR"&&d.version==3)"TORY"else id)},d.artwork())}finally{source.delete()}}
 suspend fun apply(engine:Engine,song:TaskItem,values:Map<String,String>,cover:ByteArray?,keep:Boolean,originalHash:String):ReviewUndo?=withContext(Dispatchers.IO+NonCancellable){
  require(hash(engine,song.path!!)==originalHash){"檔案已被修改，請返回編輯再掃描"}
  val backup=File(engine.context.filesDir,"tag-undo/${UUID.randomUUID()}.mp3");if(keep){backup.parentFile!!.mkdirs();input(engine,song.path).use{i->backup.outputStream().use{i.copyTo(it)}}}
  try{TagEditor(engine).apply(setOf(song.id),values,emptyMap(),cover,renameFile=false,expectedHash=originalHash);if(!keep)null else ReviewUndo(backup,song,hash(engine,song.path)).also{File(backup.path+".json").writeText(org.json.JSONObject().put("path",song.path).put("afterHash",it.after).toString())}}catch(e:Exception){backup.delete();throw e}
 }
}
data class ReviewUndo(val backup:File,val before:TaskItem,val after:String){
 suspend fun restore(engine:Engine)=withContext(Dispatchers.IO+NonCancellable){val path=before.path!!;require(TagReview.hash(engine,path)==after){"檔案其後已有修改，不能直接復原"};if(path.startsWith("content://")){val recovery=File(engine.context.cacheDir,"undo-recovery-${UUID.randomUUID()}.mp3");TagReview.input(engine,path).use{i->recovery.outputStream().use{i.copyTo(it)}};try{engine.context.contentResolver.openOutputStream(Uri.parse(path),"wt")!!.use{o->backup.inputStream().use{it.copyTo(o)}}}catch(e:Exception){runCatching{engine.context.contentResolver.openOutputStream(Uri.parse(path),"wt")!!.use{o->recovery.inputStream().use{it.copyTo(o)}}};throw e}finally{recovery.delete()}}else{val stage=File(path+".undo-${UUID.randomUUID()}");backup.copyTo(stage);java.nio.file.Files.move(stage.toPath(),File(path).toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING)};engine.update(before.id){before};backup.delete();File(backup.path+".json").delete()}
}
