package io.hkmario.omni
import android.content.ContentProvider
import android.content.ContentValues
import android.net.Uri
import android.os.Bundle
import android.os.ParcelFileDescriptor
import android.database.Cursor
import java.io.File
import java.io.IOException
import java.util.concurrent.ConcurrentHashMap

/** Test-only provider, restricted to original synthetic fixture data. */
class AudioTestProvider:ContentProvider(){
 private val writes=ConcurrentHashMap<String,Int>()
 override fun onCreate()=true
 private fun file(uri:Uri):File{val name=uri.lastPathSegment!!;require(name.matches(Regex("qa-[a-z0-9-]+\\.(mp3|opus|flac|m4a)")));return File(context!!.filesDir,name)}
 override fun call(method:String,arg:String?,extras:Bundle?):Bundle?{if(method=="fixture"){val uri=Uri.parse("content://io.hkmario.omni.test.audio/$arg");file(uri).writeBytes(extras!!.getByteArray("bytes")!!);writes.remove(arg);return Bundle()};return null}
 override fun openFile(uri:Uri,mode:String):ParcelFileDescriptor{if(mode.contains('w')&&uri.getQueryParameter("fail")=="once"&&writes.merge(uri.lastPathSegment!!,1,Int::plus)==1)throw IOException("Injected write failure");return ParcelFileDescriptor.open(file(uri),ParcelFileDescriptor.parseMode(mode))}
 override fun getType(uri:Uri)="audio/*"
 override fun query(uri:Uri,projection:Array<out String>?,selection:String?,selectionArgs:Array<out String>?,sortOrder:String?):Cursor?=null
 override fun insert(uri:Uri,values:ContentValues?):Uri?=null
 override fun update(uri:Uri,values:ContentValues?,selection:String?,selectionArgs:Array<out String>?)=0
 override fun delete(uri:Uri,selection:String?,selectionArgs:Array<out String>?)=0
}
