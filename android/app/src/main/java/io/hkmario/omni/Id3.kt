package io.hkmario.omni
import java.io.File
import java.io.IOException
import java.nio.ByteBuffer
import java.nio.ByteOrder

data class Frame(val id: String,val flags: ByteArray,val data: ByteArray)
data class Art(val bytes: ByteArray,val mime: String,val description: String,val type: Int)
class Id3 private constructor(val version: Int,val audioOffset: Long,val frames: MutableList<Frame>) {
    companion object {
        private fun syn(b: ByteArray,o: Int) = ((b[o].toInt() and 127) shl 21) or ((b[o+1].toInt() and 127) shl 14) or ((b[o+2].toInt() and 127) shl 7) or (b[o+3].toInt() and 127)
        fun read(file: File): Id3 = file.inputStream().use { input ->
            val h=ByteArray(10);if(input.read(h)!=10 || String(h,0,3,Charsets.US_ASCII)!="ID3")return@use Id3(3,0,mutableListOf())
            val v=h[3].toInt();if(v !in 3..4 || h[5].toInt()!=0)throw IOException("此 ID3 格式需先轉為標準 v2.3/v2.4，以保護原有標籤")
            val length=syn(h,6);if(length>64*1024*1024 || length>file.length()-10)throw IOException("ID3 長度無效")
            val b=ByteArray(length);java.io.DataInputStream(input).readFully(b);val frames=mutableListOf<Frame>();var pos=0
            while(pos+10<=b.size && b[pos].toInt()!=0) { val id=String(b,pos,4,Charsets.US_ASCII);if(!id.matches(Regex("[A-Z0-9]{4}")))throw IOException("無效 Frame");val n=if(v==4)syn(b,pos+4)else ByteBuffer.wrap(b,pos+4,4).int;if(n<0 || n>b.size-pos-10)throw IOException("Frame 越界");frames+=Frame(id,b.copyOfRange(pos+8,pos+10),b.copyOfRange(pos+10,pos+10+n));pos+=10+n }
            Id3(v,(length+10).toLong(),frames)
        }
        private fun syncBytes(n: Int)=byteArrayOf(((n shr 21) and 127).toByte(),((n shr 14) and 127).toByte(),((n shr 7) and 127).toByte(),(n and 127).toByte())
    }
    fun text(id: String): String { if(id=="COMM")return comment(); val b=frames.firstOrNull{it.id==id}?.data ?: return "";if(b.size<2)return "";val charset=when(b[0].toInt()){0->Charsets.ISO_8859_1;1->Charsets.UTF_16;2->Charsets.UTF_16BE;3->Charsets.UTF_8;else->return ""};return String(b,1,b.size-1,charset).trimEnd('\u0000') }
    fun comment():String {val b=frames.firstOrNull{it.id=="COMM"}?.data?:return "";if(b.size<5)return "";var start=4;val step=if(b[0].toInt() in 1..2)2 else 1;while(start+step<=b.size){if(b[start].toInt()==0&&(step==1||b[start+1].toInt()==0)){start+=step;break};start+=step};return String(b,start,b.size-start,when(b[0].toInt()){0->Charsets.ISO_8859_1;1->Charsets.UTF_16;2->Charsets.UTF_16BE;else->Charsets.UTF_8}).trimEnd('\u0000')}
    fun artwork():ByteArray? {val b=frames.firstOrNull{it.id=="APIC"}?.data?:return null;if(b.size<5)return null;var end=1;while(end<b.size&&b[end].toInt()!=0)end++;var start=end+2;val step=if(b[0].toInt() in 1..2)2 else 1;while(start+step<=b.size){if(b[start].toInt()==0&&(step==1||b[start+1].toInt()==0)){start+=step;break};start+=step};return if(start<b.size)b.copyOfRange(start,b.size)else null}
    fun setRaw(id: String,data: ByteArray) { val parts=id.split('#',limit=2);val name=parts[0];val index=parts.getOrNull(1)?.toIntOrNull()?:0;require(name.matches(Regex("[A-Z0-9]{4}"))&&index>=0);val matches=frames.indices.filter{frames[it].id==name};val frame=Frame(name,byteArrayOf(0,0),data);if(index<matches.size)frames[matches[index]]=frame else if(index==matches.size)frames+=frame else error("Frame 索引不存在") }
    fun setText(id: String,value: String) { if(id=="COMM"){setRaw(id,byteArrayOf(1,101,110,103,0xff.toByte(),0xfe.toByte(),0,0,0xff.toByte(),0xfe.toByte())+value.toByteArray(Charsets.UTF_16LE)+byteArrayOf(0,0));return}; require(id.matches(Regex("T[A-Z0-9]{3}")) && id!="TXXX");setRaw(id,byteArrayOf(1,0xff.toByte(),0xfe.toByte())+value.toByteArray(Charsets.UTF_16LE)+byteArrayOf(0,0)) }
    fun covers(arts: List<Art>) { frames.removeAll{it.id=="APIC"};arts.forEach{a->frames+=Frame("APIC",byteArrayOf(0,0),byteArrayOf(0)+a.mime.toByteArray(Charsets.US_ASCII)+byteArrayOf(0,a.type.toByte())+a.description.toByteArray(Charsets.ISO_8859_1)+byteArrayOf(0)+a.bytes)} }
    fun write(source: File,target: File) {
        val size=frames.sumOf{10+it.data.size};require(size<=64*1024*1024)
        source.inputStream().use { input ->var remaining=audioOffset;while(remaining>0){val n=input.skip(remaining);if(n<=0)throw IOException("無法定位音訊資料");remaining-=n}
            target.outputStream().use { out ->out.write(byteArrayOf(73,68,51,version.toByte(),0,0)+syncBytes(size));frames.forEach{f->out.write(f.id.toByteArray(Charsets.US_ASCII));out.write(if(version==4)syncBytes(f.data.size)else ByteBuffer.allocate(4).order(ByteOrder.BIG_ENDIAN).putInt(f.data.size).array());out.write(f.flags);out.write(f.data)};input.copyTo(out);out.fd.sync() }
        }
    }
}
