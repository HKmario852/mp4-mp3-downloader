package io.hkmario.omni
import org.junit.Assert.*
import org.junit.Test
import java.time.LocalTime
import kotlinx.serialization.json.Json
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
class OptionsTest {
 @Test fun formatAndBitrateStayConsistent(){for(f in listOf("mp3","m4a","flac","wav")){val args=Options.format(TaskItem(url="https://example.org",mode="mp3",outputFormat=f,kbps=192),Prefs());assertTrue(args.contains("--audio-format" to f));assertEquals(f in listOf("mp3","m4a"),args.any{it.first=="--audio-quality"});if(f in listOf("mp3","m4a"))assertTrue(args.contains("--audio-quality" to "192k"))}}
 @Test fun overnightSchedule(){assertTrue(Options.inPeriod("22:00","06:00",LocalTime.of(23,0)));assertTrue(Options.inPeriod("22:00","06:00",LocalTime.of(1,0)));assertFalse(Options.inPeriod("22:00","06:00",LocalTime.of(12,0)))}
 @Test fun legacyPreferencesStillDecode(){val p=Json.decodeFromString<Prefs>("{\"tree\":\"content://old\",\"musicBrainz\":true}");assertEquals("content://old",p.treeFor("mp3"));assertTrue(p.musicBrainz);assertEquals("{title}",p.audioNaming);assertEquals("mp4",p.videoFormat);assertEquals(p,Json.decodeFromString<Prefs>(Json.encodeToString(p)))}
 @Test fun tenDownloadsAllowedAndUnsupportedCodecRejected(){Prefs(concurrency=10).validate();assertTrue(runCatching{Prefs(concurrency=11).validate()}.isFailure);assertTrue(runCatching{Prefs(videoFormat="webm",videoCodec="h264").validate()}.isFailure)}
 @Test fun namingKeepsNativeSongTitle(){val t=TaskItem(url="https://example.org",title="夜に駆ける",artist="YOASOBI",mode="mp3");assertEquals("夜に駆ける",Options.fileStem(t,Prefs()));assertTrue(runCatching{Options.validateNaming("{title}/{artist}")}.isFailure)}
}
