package io.hkmario.omni

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Test
import org.junit.Assert.*
import kotlinx.coroutines.runBlocking
import java.io.File

class AudioPipelineTest {
 @Test fun allContainersPreserveUnselectedTagsAndSupportCoverAndUndo()=runBlocking {
  val instrumentation=InstrumentationRegistry.getInstrumentation();val context=instrumentation.targetContext
  val root=File(context.cacheDir,"audio-format-qa").apply{mkdirs()}
  try{for(ext in listOf("mp3","opus","m4a","flac")){
   val original=File(root,"source.$ext");instrumentation.context.assets.open("tone.$ext").use{i->original.outputStream().use{i.copyTo(it)}}
   val before=AudioTags.read(original);val current=File(root,"tagged.$ext");val d=AudioTags.read(original)
   val extra=mapOf("TPE2" to "Demo album artist","TYER" to "2026","TCON" to "Demo genre","TCOM" to "Demo composer","TPUB" to "Demo label","TSRC" to "HKAAA2600001","TXXX:MusicBrainz Album Id" to "885b1ba8-65f0-476b-939c-704db7a696de","TSOT" to "Demo sort","TSOP" to "Demo artist sort")
   extra.forEach{(key,value)->d.setText(key,value)}
   d.setText("TIT2","測試 · 日本語 🎵");d.setText("TRCK","2/9");d.setText("TPOS","1/2");d.setText("TXXX:MusicBrainz Recording Id","cb39b5d8-ebb8-4bad-9f17-9d952108ecb7");d.setText("TDOR",if(ext=="mp3")"2020"else"2020-02-03")
   val cover=instrumentation.context.assets.open("cover.png").use{it.readBytes()};d.covers(listOf(Art(cover,"image/png","Demo cover",3)));d.write(original,current)
   val after=AudioTags.read(current);extra.forEach{(key,value)->assertEquals("$ext: $key",value,after.text(key))};assertEquals("測試 · 日本語 🎵",after.text("TIT2"));assertEquals("2/9",after.text("TRCK"));assertEquals("1/2",after.text("TPOS"));assertEquals(before.text("TPE1"),after.text("TPE1"));assertEquals(before.text("TALB"),after.text("TALB"));assertArrayEquals(cover,after.artwork());assertEquals("cb39b5d8-ebb8-4bad-9f17-9d952108ecb7",after.text("TXXX:MusicBrainz Recording Id"))
   // Pull these synthetic files for FFprobe packet-hash verification on the host.
   current.copyTo(File(context.filesDir,"qa-audio.$ext"),true)
   AudioPipeline.validate(current,1.0)
   val saved=original.readBytes();original.copyTo(current,true);assertArrayEquals(saved,current.readBytes())
  }}finally{root.deleteRecursively()}
 }
 @Test fun reviewApplyAndExactUndoWorkForEveryContainer()=runBlocking {
  val i=InstrumentationRegistry.getInstrumentation();val context=i.targetContext
  val engine=(context.applicationContext as OmniApp).engine
  for(ext in listOf("mp3","opus","m4a","flac")){
   val file=File(context.cacheDir,"qa-review.$ext");i.context.assets.open("tone.$ext").use{input->file.outputStream().use{input.copyTo(it)}}
   val original=file.readBytes();val song=TaskItem(id="qa-review-$ext",url="",mode="mp3",outputFormat=ext,path=file.path,title="Demo tone",state=State.Completed,history=false)
   engine.tagImports[song.id]=song
   try{
    val undo=TagReview.apply(engine,song,mapOf("TIT2" to "Selected change"),null,true,TagReview.hash(engine,file.path))!!
    assertEquals("Selected change",AudioTags.read(file).text("TIT2"));assertEquals("Demo artist",AudioTags.read(file).text("TPE1"));assertEquals("Selected change",engine.get(song.id).title)
    undo.restore(engine);assertArrayEquals(original,file.readBytes());assertEquals("Demo tone",engine.get(song.id).title)
   }finally{engine.tagImports.remove(song.id);file.delete()}
  }
 }
 @Test fun localFingerprintWorksForAllContainers()=runBlocking {
  val i=InstrumentationRegistry.getInstrumentation();val context=i.targetContext
  for(ext in listOf("mp3","opus","m4a","flac")){
   val file=File(context.cacheDir,"qa-fingerprint.$ext")
   try{i.context.assets.open("fingerprint/tone.$ext").use{input->file.outputStream().use{input.copyTo(it)}}
    val result=AudioFingerprint.calculate(context,file.path);assertTrue("$ext fingerprint",result.fingerprint.length>=10);assertTrue(result.duration in 19..21)
   }finally{file.delete()}
  }
 }
 @Test fun newMp3VersionIsConfigurable(){val instrumentation=InstrumentationRegistry.getInstrumentation();for(version in listOf(3,4)){val file=File(instrumentation.targetContext.cacheDir,"version-$version.mp3");try{instrumentation.context.assets.open("tone.mp3").use{i->file.outputStream().use{i.copyTo(it)}};AudioTags.setNewMp3Version(file,version);assertEquals(version,Id3.read(file).version)}finally{file.delete()}}}
 @androidx.test.filters.SdkSuppress(minSdkVersion=29)
 @Test fun safWriteBackVerifiesAndRecoversFailures(){
  val instrumentation=InstrumentationRegistry.getInstrumentation();val target=instrumentation.targetContext
  val provider=AudioTestProvider();provider.attachInfo(target,android.content.pm.ProviderInfo().apply{authority="io.hkmario.omni.test.audio"})
  val resolver=android.content.ContentResolver.wrap(provider)
  val context=object:android.content.ContextWrapper(target){override fun getContentResolver()=resolver}
  val bytes=instrumentation.context.assets.open("tone.opus").use{it.readBytes()};val name="qa-saf.opus";val uri=android.net.Uri.parse("content://io.hkmario.omni.test.audio/$name")
  fun reset(){provider.call("fixture",name,android.os.Bundle().apply{putByteArray("bytes",bytes)})}
  val source=File(context.cacheDir,"saf-source.opus");val staged=File(context.cacheDir,"saf-result.opus");val backup=File(context.filesDir,"tag-recovery/qa-saf.opus")
  try{
   reset();source.writeBytes(bytes);val tag=AudioTags.read(source);tag.setText("TIT2","SAF 測試");tag.write(source,staged);SafTagStorage.durableCopy(source,backup)
   SafTagStorage.writeBack(context,uri,staged,backup);assertArrayEquals(staged.readBytes(),context.contentResolver.openInputStream(uri)!!.use{it.readBytes()})
   reset();val failed=runCatching{SafTagStorage.writeBack(context,uri.buildUpon().appendQueryParameter("fail","once").build(),staged,backup)}
   assertTrue(failed.isFailure);assertTrue(backup.isFile);assertArrayEquals(bytes,context.contentResolver.openInputStream(uri)!!.use{it.readBytes()})
   reset();backup.writeText("stale snapshot");assertTrue(runCatching{SafTagStorage.writeBack(context,uri,staged,backup)}.isFailure);assertArrayEquals(bytes,context.contentResolver.openInputStream(uri)!!.use{it.readBytes()})
  }finally{source.delete();staged.delete();backup.delete();File(backup.path+".uri").delete()}
 }
}
