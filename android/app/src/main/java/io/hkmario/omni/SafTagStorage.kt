package io.hkmario.omni

import android.content.Context
import android.net.Uri
import java.io.File
import java.io.IOException
import java.security.MessageDigest

/** Providers cannot promise atomic replacement. Keep a durable local recovery copy. */
object SafTagStorage {
 fun hashBytes(bytes:ByteArray)=MessageDigest.getInstance("SHA-256").digest(bytes).joinToString(""){"%02x".format(it)}
 fun hash(file:File)=file.inputStream().use{hash(it)}
 private fun hash(input:java.io.InputStream):String{val digest=MessageDigest.getInstance("SHA-256");val buffer=ByteArray(65536);while(true){val n=input.read(buffer);if(n<0)break;digest.update(buffer,0,n)};return digest.digest().joinToString(""){"%02x".format(it)}}
 private fun hash(context:Context,uri:Uri)=context.contentResolver.openInputStream(uri)?.use{hash(it)}?:throw IOException("無法重新讀取檔案以驗證")
 fun writeBack(context:Context,uri:Uri,staged:File,backup:File){
  val before=hash(backup);if(hash(context,uri)!=before)throw IOException("原檔在編輯期間已有修改，未寫入")
  File(backup.path+".uri").writeText(uri.toString())
  fun copy(file:File){val stream=context.contentResolver.openOutputStream(uri,"wt")?:throw IOException("提供者未允許寫入");stream.use{o->file.inputStream().use{it.copyTo(o)};o.flush()}}
  try{copy(staged);check(hash(context,uri)==hash(staged)){"寫回驗證失敗"}}
  catch(e:Exception){
   val restored=runCatching{copy(backup);check(hash(context,uri)==before)}.isSuccess
   throw IOException((if(restored)"標籤寫入失敗，原檔已復原。"else "標籤寫入及復原失敗。")+" 復原備份保留於 ${backup.absolutePath}",e)
  }
 }
 fun durableCopy(source:File,backup:File){backup.parentFile!!.mkdirs();source.inputStream().use{i->backup.outputStream().use{o->i.copyTo(o);o.fd.sync()}}}
}
