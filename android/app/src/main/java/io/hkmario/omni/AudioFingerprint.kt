package io.hkmario.omni

import android.content.Context
import android.media.*
import android.net.Uri
import kotlinx.coroutines.*
import java.nio.ByteOrder

/** Local decoder feeds Chromaprint; only the resulting fingerprint is sent to AcoustID. */
object AudioFingerprint {
    init { System.loadLibrary("omni_fingerprint") }
    private external fun create(rate:Int,channels:Int):Long
    private external fun feed(handle:Long,samples:ShortArray):Boolean
    private external fun finish(handle:Long):String?
    private external fun free(handle:Long)
    data class Result(val fingerprint:String,val duration:Int)
    suspend fun calculate(context:Context,path:String):Result=withContext(Dispatchers.IO){
        val extractor=MediaExtractor();var codec:MediaCodec?=null;var handle=0L
        try {
            if(path.startsWith("content://"))extractor.setDataSource(context,Uri.parse(path),null)else extractor.setDataSource(path)
            val index=(0 until extractor.trackCount).firstOrNull{extractor.getTrackFormat(it).getString(MediaFormat.KEY_MIME)?.startsWith("audio/")==true}?:error("找不到音訊")
            extractor.selectTrack(index);val format=extractor.getTrackFormat(index);val seconds=(format.getLong(MediaFormat.KEY_DURATION)/1_000_000).toInt();require(seconds>0){"無法讀取音訊時長"}
            val decoder=MediaCodec.createDecoderByType(format.getString(MediaFormat.KEY_MIME)!!);codec=decoder;decoder.configure(format,null,null,0);decoder.start()
            var inputEnd=false;var outputEnd=false;val info=MediaCodec.BufferInfo();val deadline=android.os.SystemClock.elapsedRealtime()+90000
            while(!outputEnd){currentCoroutineContext().ensureActive();check(android.os.SystemClock.elapsedRealtime()<deadline){"音訊辨識逾時"}
                if(!inputEnd){val i=decoder.dequeueInputBuffer(10000);if(i>=0){val buffer=decoder.getInputBuffer(i)!!;val size=extractor.readSampleData(buffer,0);if(size<0||extractor.sampleTime>=120_000_000){decoder.queueInputBuffer(i,0,0,0,MediaCodec.BUFFER_FLAG_END_OF_STREAM);inputEnd=true}else{decoder.queueInputBuffer(i,0,size,extractor.sampleTime,0);extractor.advance()}}}
                val out=decoder.dequeueOutputBuffer(info,10000)
                if(out==MediaCodec.INFO_OUTPUT_FORMAT_CHANGED){val f=decoder.outputFormat;require(!f.containsKey(MediaFormat.KEY_PCM_ENCODING)||f.getInteger(MediaFormat.KEY_PCM_ENCODING)==android.media.AudioFormat.ENCODING_PCM_16BIT){"不支援此 PCM 格式"};if(handle==0L){handle=create(f.getInteger(MediaFormat.KEY_SAMPLE_RATE),f.getInteger(MediaFormat.KEY_CHANNEL_COUNT));check(handle!=0L){"無法啟動 Chromaprint"}}}
                if(out>=0){try{if(info.size>0){check(handle!=0L);val data=decoder.getOutputBuffer(out)!!;data.position(info.offset);data.limit(info.offset+info.size);val shorts=data.slice().order(ByteOrder.LITTLE_ENDIAN).asShortBuffer();val values=ShortArray(shorts.remaining());shorts.get(values);check(feed(handle,values)){"音訊指紋處理失敗"}};outputEnd=info.flags and MediaCodec.BUFFER_FLAG_END_OF_STREAM!=0}finally{decoder.releaseOutputBuffer(out,false)}}
            }
            val value=finish(handle);check(!value.isNullOrBlank()){ "未能產生音訊指紋" };Result(value,seconds)
        }finally{if(handle!=0L)free(handle);try{codec?.stop()}catch(_:Exception){};codec?.release();extractor.release()}
    }
}
