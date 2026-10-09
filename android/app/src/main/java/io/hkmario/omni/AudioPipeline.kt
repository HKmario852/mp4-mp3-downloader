package io.hkmario.omni

import android.media.MediaExtractor
import android.media.MediaFormat
import java.io.File
import kotlinx.coroutines.*

object AudioPipeline {
 fun selector(format:String)=when(format){
  "opus"->"bestaudio[ext=webm][acodec=opus]"
  "m4a"->"bestaudio[ext=m4a][acodec^=mp4a.40]/bestaudio[acodec=aac]"
  "mp3"->"bestaudio[acodec=mp3]/bestaudio/best"
  "flac","wav"->"bestaudio/best"
  else->error("Unsupported audio format")
 }
 fun arguments(format:String,p:Prefs,kbps:Int):List<Pair<String,String?>>{
  val a=mutableListOf<Pair<String,String?>>("-f" to selector(format),"-x" to null,"--audio-format" to format)
  if(format in listOf("opus","m4a"))a.add("--postprocessor-args" to "ExtractAudio+ffmpeg_o:-c:a copy")
  if(format=="mp3"){a.add("--audio-quality" to if(p.mp3Encoding=="v0")"0"else "${kbps}k");a.add("--postprocessor-args" to "Metadata+ffmpeg_o:-id3v2_version ${p.id3Version}")}
  return a
 }
 fun quality(format:String,p:Prefs,kbps:Int)=when(format){"opus","m4a"->"原生音訊 / Native audio";"flac","wav"->"無損轉檔 / Lossless conversion";else->if(p.mp3Encoding=="v0")"VBR V0"else "$kbps kbps"}
 suspend fun validate(file:File,expected:Double?)=withContext(Dispatchers.IO){
  val extractor=MediaExtractor()
  try{
   extractor.setDataSource(file.absolutePath)
   val index=(0 until extractor.trackCount).firstOrNull{extractor.getTrackFormat(it).getString(MediaFormat.KEY_MIME)?.startsWith("audio/")==true}?:error("找不到有效音訊軌")
   val format=extractor.getTrackFormat(index);val mime=format.getString(MediaFormat.KEY_MIME)?.replace("audio/x-flac","audio/flac")
   val wanted=when(file.extension){"opus"->"audio/opus";"m4a"->"audio/mp4a-latm";"mp3"->"audio/mpeg";"flac"->"audio/flac";else->mime}
   // Some Android FLAC extractors decode to PCM themselves.
   val decodedFlac=file.extension=="flac"&&mime=="audio/raw"&&file.inputStream().use{String(ByteArray(4).apply{java.io.DataInputStream(it).readFully(this)},Charsets.US_ASCII)}=="fLaC"
   check(mime==wanted||decodedFlac){"來源編碼不相容（${file.extension}: $mime）；未進行有損降級，請改選其他格式"}
   val duration=format.getLong(MediaFormat.KEY_DURATION)/1_000_000.0
   check(duration>0&&(expected==null||expected<=0||kotlin.math.abs(duration-expected)<=maxOf(2.0,expected*.01))){"音訊長度不符，可能下載未完成"}
   extractor.selectTrack(index);var last=Long.MIN_VALUE;var count=0
   while(extractor.sampleTrackIndex>=0){ensureActive();val time=extractor.sampleTime;check(time>=last){"音訊時間軸無效"};last=time;count++;if(!extractor.advance())break}
   check(count>0){"音訊沒有可讀取資料：${file.extension}"}
  }finally{extractor.release()}
 }
}
