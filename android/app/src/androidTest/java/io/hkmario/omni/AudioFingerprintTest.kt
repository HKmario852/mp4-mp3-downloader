package io.hkmario.omni
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import org.junit.Test
import org.junit.Assert.*
import java.io.File
class AudioFingerprintTest {
 @Test fun nativeFingerprintMatchesDesktopChromaprint()=runBlocking {
  val instrumentation=InstrumentationRegistry.getInstrumentation();val context=instrumentation.targetContext
  val file=File(context.cacheDir,"fingerprint-test.wav")
  try{instrumentation.context.assets.open("fingerprint-fixture.wav").use{i->file.outputStream().use{i.copyTo(it)}}
   val result=AudioFingerprint.calculate(context,file.absolutePath)
   val expected=instrumentation.context.assets.open("fingerprint-expected.txt").bufferedReader().use{it.readText().trim()}
   assertEquals(20,result.duration);assertEquals(expected,result.fingerprint)
  }finally{file.delete()}
 }
 @Test fun absentKeyDoesNotReadAudio()=runBlocking {val context=InstrumentationRegistry.getInstrumentation().targetContext;val result=MusicRecognition.scan(context,"missing.mp3","");assertTrue(result.status.contains("application API key"));assertNull(result.title)}
}
