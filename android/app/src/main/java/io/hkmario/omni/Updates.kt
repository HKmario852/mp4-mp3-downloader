package io.hkmario.omni
import android.content.Context
import android.content.Intent
import android.content.BroadcastReceiver
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.io.File
import java.security.MessageDigest
object Updates {
 private val client=OkHttpClient.Builder().callTimeout(15,java.util.concurrent.TimeUnit.MINUTES).build()
 private fun repo(p:Prefs):String {require(Regex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$").matches(p.releaseRepository));return "https://github.com/${p.releaseRepository}"}
 private fun newer(tag:String,current:String):Boolean{val a=tag.removePrefix("v").split('.').map{it.toIntOrNull()?:0};val b=current.split('.').map{it.toIntOrNull()?:0};for(i in 0..2){val diff=a.getOrElse(i){0}-b.getOrElse(i){0};if(diff!=0)return diff>0};return false}
 suspend fun latest(p:Prefs):JSONObject=withContext(Dispatchers.IO){repo(p);client.newCall(Request.Builder().url("https://api.github.com/repos/${p.releaseRepository}/releases/latest").header("User-Agent","OmniDownloader/0.2.0").build()).execute().use{check(it.isSuccessful){"Update service: HTTP ${it.code}"};JSONObject(it.body!!.string())}}
 suspend fun check(context:Context,p:Prefs):Pair<Boolean,String>{val release=latest(p);val current=context.packageManager.getPackageInfo(context.packageName,0).versionName?:"0";val tag=release.getString("tag_name");val available=newer(tag,current);return available to if(available)"${if(p.language=="en")"New version"else"有新版本"}: $tag"else if(p.language=="en")"You are up to date: $current"else"目前已是最新版本：$current"}
 // Debug-signed test builds and release builds have different signing keys; Android refuses to update across them.
 @Suppress("DEPRECATION") private val signingFlags=if(Build.VERSION.SDK_INT>=28)PackageManager.GET_SIGNING_CERTIFICATES else PackageManager.GET_SIGNATURES
 @Suppress("DEPRECATION") private fun signers(info:PackageInfo?):Set<String>{val certs=if(Build.VERSION.SDK_INT>=28)info?.signingInfo?.apkContentsSigners else info?.signatures;return certs.orEmpty().map{MessageDigest.getInstance("SHA-256").digest(it.toByteArray()).joinToString(""){b->"%02x".format(b)}}.toSet()}
 suspend fun install(engine:Engine,progress:(String)->Unit)=withContext(Dispatchers.IO){val p=engine.prefs.value;val release=latest(p);val current=engine.context.packageManager.getPackageInfo(engine.context.packageName,0).versionName?:"0";require(newer(release.getString("tag_name"),current)){"Already up to date"};val assets=release.getJSONArray("assets");val apks=(0 until assets.length()).map{assets.getJSONObject(it)}.filter{it.getString("name").endsWith(".apk")&&it.getString("name").contains("universal")};require(apks.size==1){"No unique universal APK"};val a=apks.single();val expected=a.optString("digest");require(Regex("sha256:[a-fA-F0-9]{64}").matches(expected)){"Missing trusted SHA256"};val url=a.getString("browser_download_url");require(url.startsWith(repo(p)+"/releases/download/"));val dir=File(engine.context.cacheDir,"updates").apply{mkdirs()};val file=File(dir,"update.apk");try{val digest=MessageDigest.getInstance("SHA-256");client.newCall(Request.Builder().url(url).build()).execute().use{response->check(response.isSuccessful);response.body!!.byteStream().use{input->file.outputStream().use{out->val b=ByteArray(65536);var total=0L;while(true){val n=input.read(b);if(n<0)break;out.write(b,0,n);digest.update(b,0,n);total+=n;progress("${total/1000000} MB")}}}};check(digest.digest().joinToString(""){"%02x".format(it)}.equals(expected.substring(7),true)){"SHA256 mismatch"};val archive=engine.context.packageManager.getPackageArchiveInfo(file.absolutePath,signingFlags);check(archive?.packageName==engine.context.packageName){"APK package mismatch"};check(signers(archive)==signers(engine.context.packageManager.getPackageInfo(engine.context.packageName,signingFlags))){text(p,"呢個版本用正式金鑰簽署，同你裝咗嘅測試版唔同，Android 唔可以直接蓋過。請先匯出需要嘅設定，解除安裝測試版一次，再由發布頁安裝；共用下載資料夾入面嘅檔案會保留，App 內紀錄不會。","This version is signed with the release key, unlike the test build you have installed, so Android cannot update over it. Uninstall the test build once, then install this version from the releases page. Files in your shared download folder are kept; in-app history is not.")};engine.tasks.value.forEach{engine.pause(it.id)};withContext(Dispatchers.Main){val uri=FileProvider.getUriForFile(engine.context,"${engine.context.packageName}.files",file);engine.context.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri,"application/vnd.android.package-archive").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK))}}catch(e:Exception){file.delete();throw e}}
}
class BootReceiver:BroadcastReceiver(){override fun onReceive(context:Context,intent:Intent){if(intent.action!=Intent.ACTION_BOOT_COMPLETED)return;val e=(context.applicationContext as OmniApp).engine;if(e.prefs.value.startAtLogin){Notices.create(context);Notices.show(context,if(e.prefs.value.language=="en")"Open the app to resume downloads"else"開啟 App 繼續未完成下載")}}}
