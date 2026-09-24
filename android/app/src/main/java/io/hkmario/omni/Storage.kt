package io.hkmario.omni
import android.content.Context
import android.media.MediaScannerConnection
import android.net.Uri
import android.os.StatFs
import android.provider.DocumentsContract
import androidx.documentfile.provider.DocumentFile
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.IOException
import java.security.MessageDigest

data class Published(val path: String,val parent: String)
class DuplicateSkipped:Exception("Duplicate skipped")
class Storage(private val context: Context, private val grants: SessionGrants) {
    private val publishMutex=kotlinx.coroutines.sync.Mutex()
    suspend fun publish(source: File, tree: String, name: String, group: String?, duplicate:String="rename",askDuplicate:suspend(String)->String={"rename"}, askUnknown: suspend () -> Pair<Boolean,Boolean>): Published = withContext(Dispatchers.IO) {
        publishMutex.lock();try {
        if (tree.isBlank()) {
            val dir = File(context.getExternalFilesDir(android.os.Environment.DIRECTORY_MUSIC), group ?: "").apply { mkdirs() }
            val free = StatFs(dir.absolutePath).availableBytes
            requireSpace(free,source.length(),askUnknown)
            val existing=File(dir,name);val action=if(existing.exists()&&duplicate=="ask")askDuplicate(name)else duplicate
            if(existing.exists()&&action=="skip")throw DuplicateSkipped()
            val target=if(action=="overwrite")existing else uniqueFile(dir,name)
            copyFileVerified(source,target);source.delete()
            scan(target); return@withContext Published(target.absolutePath,dir.absolutePath)
        }
        val root=DocumentFile.fromTreeUri(context,Uri.parse(tree)) ?: throw IOException("儲存目錄權限已失效，請重新選擇")
        val parent=if(group==null)root else root.findFile(group) ?: root.createDirectory(group) ?: throw IOException("無法建立播放清單目錄")
        val capacity=providerFreeBytes(parent.uri)
        requireSpace(capacity,source.length(),askUnknown)
        val existing=parent.findFile(name);val action=if(existing!=null&&duplicate=="ask")askDuplicate(name)else duplicate
        if(existing!=null&&action=="skip")throw DuplicateSkipped()
        val finalName=if(action=="overwrite")name else uniqueDocumentName(parent,name)
        var backup:DocumentFile?=null
        val temp=parent.createFile("application/octet-stream",".omni-${java.util.UUID.randomUUID()}.part") ?: throw IOException("無法建立目標暫存檔")
        var committed=false
        try {
            val expected=MessageDigest.getInstance("SHA-256")
            source.inputStream().use { input -> (context.contentResolver.openOutputStream(temp.uri,"wt") ?: throw IOException("無法開啟目標")).use { output -> val buf=ByteArray(65536);while(true){val n=input.read(buf);if(n<0)break;expected.update(buf,0,n);output.write(buf,0,n)};output.flush() } }
            val actual=MessageDigest.getInstance("SHA-256");var size=0L
            (context.contentResolver.openInputStream(temp.uri) ?: throw IOException("無法驗證目標檔案")).use { input -> val buf=ByteArray(65536);while(true){val n=input.read(buf);if(n<0)break;size+=n;actual.update(buf,0,n)} }
            if(size!=source.length() || !expected.digest().contentEquals(actual.digest()))throw IOException("複製驗證失敗；已保留來源")
            if(existing!=null&&action=="overwrite"){check(existing.renameTo(".omni-backup-${java.util.UUID.randomUUID()}")){"Cannot preserve existing file before overwrite"};backup=existing}
            if(!temp.renameTo(finalName))throw IOException("儲存提供者不支援安全提交檔名；已保留來源")
            committed=true;backup?.delete();backup=null
            source.delete()
            Published(temp.uri.toString(),parent.uri.toString()) // content:// is provider-managed; never pass to scanFile.
        } finally { if(!committed){temp.delete();backup?.renameTo(name)} }
        }finally{publishMutex.unlock()}
    }
    suspend fun sidecars(work:File,published:Published,audioOnly:Boolean=false):Int=withContext(Dispatchers.IO) {
        val name=if(published.path.startsWith("content://"))DocumentFile.fromSingleUri(context,Uri.parse(published.path))?.name?:"media" else File(published.path).name
        val stem=name.substringBeforeLast('.');var failures=0
        work.listFiles()?.filter{it.name.startsWith("media.")&&it.extension in listOf("srt","vtt","jpg")&&(!audioOnly||it.extension!="jpg")}?.forEach{source->
            try {val targetName=stem+source.name.removePrefix("media")
                if(published.parent.startsWith("content://")){val parent=TreeDocument(context,Uri.parse(published.parent));val uri=parent.find(targetName)?:parent.create(if(source.extension=="jpg")"image/jpeg"else"text/plain",targetName);context.contentResolver.openOutputStream(uri,"wt")!!.use{o->source.inputStream().use{it.copyTo(o)}}}
                else copyFileVerified(source,File(published.parent,targetName))
            }catch(_:Exception){failures++}
        };failures
    }
    private suspend fun requireSpace(free: Long?, bytes: Long, ask: suspend () -> Pair<Boolean,Boolean>) {
        if(free!=null && free>0) { if(bytes>free-10*1024*1024)throw IOException("空間不足：需要檔案大小加 10 MB");return }
        if(grants.suppressUnknownSpace)return
        val (proceed,suppress)=ask();if(!proceed)throw IOException("使用者未同意未知剩餘空間，檔案已保留於暫存")
        if(suppress)grants.suppressUnknownSpace=true
    }
    private fun providerFreeBytes(uri: Uri): Long? = try {
        context.contentResolver.openFileDescriptor(uri,"r")?.use { descriptor -> android.system.Os.fstatvfs(descriptor.fileDescriptor).let { it.f_bavail * it.f_frsize } }
    } catch(_: Exception) { null }
    private fun uniqueFile(dir: File,name: String): File { var f=File(dir,name);var i=1;val stem=name.substringBeforeLast('.');val ext=name.substringAfterLast('.');while(f.exists()){f=File(dir,"$stem (${i++}).$ext")};return f }
    private fun uniqueDocumentName(dir: DocumentFile,name: String): String { var n=name;var i=1;while(dir.findFile(n)!=null)n="${name.substringBeforeLast('.')} (${i++}).${name.substringAfterLast('.')}";return n }
    private fun copyFileVerified(source: File,target: File) { val temp=File(target.parentFile,".omni-${java.util.UUID.randomUUID()}.part");try{source.inputStream().use{i->temp.outputStream().use{o->i.copyTo(o);o.fd.sync()}};if(source.length()!=temp.length() || !digest(source).contentEquals(digest(temp)))throw IOException("搬移驗證失敗");java.nio.file.Files.move(temp.toPath(),target.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING)}finally{if(temp.exists())temp.delete()} }
    private fun digest(file: File): ByteArray { val md=MessageDigest.getInstance("SHA-256");file.inputStream().use{i->val b=ByteArray(65536);while(true){val n=i.read(b);if(n<0)break;md.update(b,0,n)}};return md.digest() }
    fun scan(file: File) { if(file.extension.equals("mp3",true) && file.canonicalPath.split(File.separator).none{it==".covers"}) MediaScannerConnection.scanFile(context,arrayOf(file.absolutePath),arrayOf("audio/mpeg"),null) }
}
class TreeDocument(private val context: Context,private val uri: Uri) {
    fun find(name: String): Uri? { val children=DocumentsContract.buildChildDocumentsUriUsingTree(uri,DocumentsContract.getDocumentId(uri));context.contentResolver.query(children,arrayOf(DocumentsContract.Document.COLUMN_DOCUMENT_ID,DocumentsContract.Document.COLUMN_DISPLAY_NAME),null,null,null)?.use{c->while(c.moveToNext())if(c.getString(1)==name)return DocumentsContract.buildDocumentUriUsingTree(uri,c.getString(0))};return null }
    fun create(mime: String,name: String): Uri = DocumentsContract.createDocument(context.contentResolver,uri,mime,name) ?: throw IOException("無法建立 $name")
}
