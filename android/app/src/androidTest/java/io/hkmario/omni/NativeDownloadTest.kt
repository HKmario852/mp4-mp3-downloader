package io.hkmario.omni
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File
import org.json.JSONObject
import com.yausername.youtubedl_android.YoutubeDL
import com.yausername.youtubedl_android.YoutubeDLRequest

@RunWith(AndroidJUnit4::class)
class NativeDownloadTest {
    @Test fun downloadAndTranscodeInBackground()=runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val engine=(context.applicationContext as OmniApp).engine
        withTimeout(120000){engine.ready.await()}
        engine.save(engine.prefs.value.copy(wifiOnly=false,musicBrainz=false,tree="",kbps=320))
        engine.serviceRunning=true
        val url="https://raw.githubusercontent.com/mediaelement/mediaelement-files/master/big_buck_bunny.mp4"
        val ids=listOf(engine.enqueue(url,"mp4","mp4"),engine.enqueue(url,"mp3","mp3"))+listOf("m4a","flac","wav","mkv").map{engine.enqueue(url,if(it=="mkv")"mp4"else"mp3",it)}
        withTimeout(240000){while(ids.any{engine.get(it).state !in listOf(State.Completed,State.Failed)}){engine.pump();delay(500)}}
        ids.forEach { id ->val t=engine.get(id);assertEquals(t.error+" "+t.stderr,State.Completed,t.state);val file=File(t.path!!);assertTrue(file.length()>1000);if(t.extension=="mp3"){val tag=Id3.read(file);assertEquals(t.title,tag.text("TIT2"));assertTrue(tag.frames.any{it.id=="TALB"})} }
        engine.serviceRunning=false
    }
}
