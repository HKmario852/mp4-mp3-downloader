package io.hkmario.omni
import android.net.Uri
import android.provider.DocumentsContract
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.io.File
import java.io.IOException

class TagEditor(private val engine:Engine) {
    companion object {private val lock=Mutex()}
    suspend fun apply(ids:Set<String>,delta:Map<String,String>,raw:Map<String,ByteArray>,cover:ByteArray?,removeCover:Boolean=false,renameFile:Boolean=true,automaticCover:Boolean=false)=lock.withLock { withContext(Dispatchers.IO) {
        delta["TIT2"]?.let{require(Rules.titleError(it)==null){Rules.titleError(it)!!}};require(raw.keys.none{it.substringBefore('#')=="TIT2"}){"請用 Title 欄位修改歌曲名"}
        if(renameFile&&!delta["TIT2"].isNullOrEmpty())ids.forEach{val t=engine.get(it);require(t.path?.startsWith("content://")!=true||t.directory.startsWith("content://")){"請先加入歌曲所在資料夾，取得重新命名授權"}}
        val directories=linkedMapOf<String,String>()
        ids.forEach { id ->
            val task=engine.get(id);val path=task.path ?: throw IOException("找不到音訊檔案");val content=path.startsWith("content://")
            val staged=File(engine.context.cacheDir,"tag-${java.util.UUID.randomUUID()}.mp3")
            val source=if(content){(engine.context.contentResolver.openInputStream(Uri.parse(path))?:throw IOException("檔案權限已失效")).use{i->staged.outputStream().use{i.copyTo(it)}};staged}else File(path)
            val temp=File(source.parentFile,".${source.name}.edit-${java.util.UUID.randomUUID()}");val backup=File(source.parentFile,".${source.name}.backup-${java.util.UUID.randomUUID()}")
            val tag=Id3.read(source);delta.forEach{(k,v)->tag.setText(if(k=="TYER"&&tag.version==4)"TDRC"else k,v)};raw.forEach{(k,v)->tag.setRaw(k,v)}
            if(removeCover)tag.covers(emptyList());if(cover!=null)tag.covers(listOf(Art(cover,when { cover.size>=2 && cover[0]==0xff.toByte() && cover[1]==0xd8.toByte()->"image/jpeg"; cover.size>=12 && String(cover,8,4)=="WEBP"->"image/webp"; else->"image/png" },if(automaticCover)"Album front"else"User cover",3)))
            var destination=path
            try {
                tag.write(source,temp)
                if(content) {
                    source.copyTo(backup)
                    try{(engine.context.contentResolver.openOutputStream(Uri.parse(path),"wt")?:throw IOException("無法寫入標籤")).use{o->temp.inputStream().use{it.copyTo(o)}}}
                    catch(e:Exception){runCatching{engine.context.contentResolver.openOutputStream(Uri.parse(path),"wt")?.use{o->backup.inputStream().use{it.copyTo(o)}}};throw IOException("標籤寫入失敗，復原備份保留於 ${backup.absolutePath}",e)}
                    if(renameFile&&delta.containsKey("TIT2")&&!delta["TIT2"].isNullOrEmpty()){
                        require(task.directory.startsWith("content://")){"缺少父目錄授權，無法安全重新命名"}
                        val parent=TreeDocument(engine.context,Uri.parse(task.directory));val title=delta.getValue("TIT2");var newName="$title.mp3";var i=1
                        while(parent.find(newName)?.let{it.toString()!=path}==true)newName="$title (${i++}).mp3"
                        destination=DocumentsContract.renameDocument(engine.context.contentResolver,Uri.parse(path),newName)?.toString()?:throw IOException("標籤已儲存，但提供者未允許重新命名；備份已保留")
                    }
                } else {
                    if(renameFile&&delta.containsKey("TIT2")&&!delta["TIT2"].isNullOrEmpty()&&delta["TIT2"]!=source.nameWithoutExtension){var target=File(source.parentFile,delta["TIT2"]+".mp3");var i=1;while(target.exists())target=File(source.parentFile,"${delta["TIT2"]} (${i++}).mp3");destination=target.absolutePath}
                    source.copyTo(backup)
                    java.nio.file.Files.move(temp.toPath(),source.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING)
                    try{if(destination!=path)java.nio.file.Files.move(source.toPath(),File(destination).toPath())}catch(e:Exception){backup.copyTo(source,true);throw e}
                }
                engine.update(id){it.copy(path=destination,title=if(delta.containsKey("TIT2"))delta.getValue("TIT2")else it.title,artist=if(delta.containsKey("TPE1"))delta.getValue("TPE1")else it.artist,album=if(delta.containsKey("TALB"))delta.getValue("TALB")else it.album,isUserEdited=it.isUserEdited||!automaticCover,coverUserEdited=it.coverUserEdited||(!automaticCover&&(cover!=null||removeCover||raw.keys.any{k->k.substringBefore('#')=="APIC"})))}
                if(cover!=null)directories[if(content)task.directory else File(destination).parent!!]=destination
                backup.delete();engine.storage.scan(File(destination))
            } finally{temp.delete();if(content)staged.delete()}
        }
        if(cover!=null){val jpeg=Engine.toJpeg(cover);var failed=0;directories.forEach{(parent,path)->try{engine.storage.coverFor(path,jpeg,parent)}catch(_:Exception){failed++}};if(failed>0)Notices.show(engine.context,"封面更新完成（含 $failed 個目錄寫入失敗）")}
    } }
}
