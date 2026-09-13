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
class Storage(private val context: Context, private val grants: SessionGrants) {
    suspend fun publish(source: File, tree: String, name: String, group: String?, askUnknown: suspend () -> Pair<Boolean,Boolean>): Published = withContext(Dispatchers.IO) {
        if (tree.isBlank()) {
            val dir = File(context.getExternalFilesDir(android.os.Environment.DIRECTORY_MUSIC), group ?: "").apply { mkdirs() }
            val free = StatFs(dir.absolutePath).availableBytes
            requireSpace(free,source.length(),askUnknown)
            val target=uniqueFile(dir,name)
            if (!source.renameTo(target)) { copyFileVerified(source,target); check(source.delete()) { "檔案已複製，但無法移除來源暫存" } }
            scan(target); return@withContext Published(target.absolutePath,dir.absolutePath)
        }
        val root=DocumentFile.fromTreeUri(context,Uri.parse(tree)) ?: throw IOException("儲存目錄權限已失效，請重新選擇")
        val parent=if(group==null)root else root.findFile(group) ?: root.createDirectory(group) ?: throw IOException("無法建立播放清單目錄")
        val capacity=providerFreeBytes(parent.uri)
        requireSpace(capacity,source.length(),askUnknown)
        val finalName=uniqueDocumentName(parent,name)
        val temp=parent.createFile("application/octet-stream",".omni-${java.util.UUID.randomUUID()}.part") ?: throw IOException("無法建立目標暫存檔")
        var committed=false
        try {
            val expected=MessageDigest.getInstance("SHA-256")
            source.inputStream().use { input -> (context.contentResolver.openOutputStream(temp.uri,"wt") ?: throw IOException("無法開啟目標")).use { output -> val buf=ByteArray(65536);while(true){val n=input.read(buf);if(n<0)break;expected.update(buf,0,n);output.write(buf,0,n)};output.flush() } }
            val actual=MessageDigest.getInstance("SHA-256");var size=0L
            (context.contentResolver.openInputStream(temp.uri) ?: throw IOException("無法驗證目標檔案")).use { input -> val buf=ByteArray(65536);while(true){val n=input.read(buf);if(n<0)break;size+=n;actual.update(buf,0,n)} }
            if(size!=source.length() || !expected.digest().contentEquals(actual.digest()))throw IOException("複製驗證失敗；已保留來源")
            if(!temp.renameTo(finalName))throw IOException("儲存提供者不支援安全提交檔名；已保留來源")
            committed=true
            if(!source.delete()) throw IOException("已完成目標檔案，但無法清除暫存來源：${temp.uri}")
            Published(temp.uri.toString(),parent.uri.toString()) // content:// is provider-managed; never pass to scanFile.
        } finally { if(!committed)temp.delete() }
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
    private fun copyFileVerified(source: File,target: File) { val temp=File(target.parentFile,".${target.name}.part");try{source.inputStream().use{i->temp.outputStream().use{o->i.copyTo(o);o.fd.sync()}};if(source.length()!=temp.length() || !digest(source).contentEquals(digest(temp)))throw IOException("搬移驗證失敗");if(!temp.renameTo(target))throw IOException("無法提交輸出檔案")}finally{if(temp.exists())temp.delete()} }
    private fun digest(file: File): ByteArray { val md=MessageDigest.getInstance("SHA-256");file.inputStream().use{i->val b=ByteArray(65536);while(true){val n=i.read(b);if(n<0)break;md.update(b,0,n)}};return md.digest() }
    fun scan(file: File) { if(file.extension.equals("mp3",true) && file.canonicalPath.split(File.separator).none{it==".covers"}) MediaScannerConnection.scanFile(context,arrayOf(file.absolutePath),arrayOf("audio/mpeg"),null) }
    suspend fun coverFor(path: String, jpeg: ByteArray, parent: String = "") = withContext(Dispatchers.IO) {
        if(path.startsWith("content://")) {
            if(!parent.startsWith("content://"))throw IOException("缺少儲存提供者父目錄，請重新選擇儲存路徑")
            // Provider document IDs are opaque. Keep the actual parent URI at publication.
            val parentDoc=TreeDocument(context,Uri.parse(parent))
            val covers=parentDoc.find(".covers") ?: parentDoc.create("vnd.android.document/directory",".covers")
            val child=TreeDocument(context,covers)
            val noMedia=child.find(".nomedia") ?: child.create("application/octet-stream",".nomedia")
            context.contentResolver.openOutputStream(noMedia,"wt")?.close()
            val cover=child.find("cover.jpg") ?: child.create("image/jpeg","cover.jpg")
            (context.contentResolver.openOutputStream(cover,"wt") ?: throw IOException("封面目錄寫入失敗")).use{it.write(jpeg)}
        } else {
            val dir=File(File(path).parentFile,".covers").apply{if(!exists()&&!mkdirs())throw IOException("無法建立封面目錄")}
            File(dir,".nomedia").writeBytes(byteArrayOf());File(dir,"cover.jpg").writeBytes(jpeg)
        }
    }
}
class TreeDocument(private val context: Context,private val uri: Uri) {
    fun find(name: String): Uri? { val children=DocumentsContract.buildChildDocumentsUriUsingTree(uri,DocumentsContract.getDocumentId(uri));context.contentResolver.query(children,arrayOf(DocumentsContract.Document.COLUMN_DOCUMENT_ID,DocumentsContract.Document.COLUMN_DISPLAY_NAME),null,null,null)?.use{c->while(c.moveToNext())if(c.getString(1)==name)return DocumentsContract.buildDocumentUriUsingTree(uri,c.getString(0))};return null }
    fun create(mime: String,name: String): Uri = DocumentsContract.createDocument(context.contentResolver,uri,mime,name) ?: throw IOException("無法建立 $name")
}
