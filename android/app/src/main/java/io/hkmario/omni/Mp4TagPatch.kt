package io.hkmario.omni

import java.io.File
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import org.jaudiotagger.tag.Tag
import org.jaudiotagger.tag.mp4.Mp4TagField
import org.jaudiotagger.tag.mp4.field.Mp4TagReverseDnsField

/** Patch only chosen ilst atoms. Append moov without moving media/chunk offsets.
 * The Android jaudiotagger fork's JCodec flatten writer fails on some FFmpeg M4As.
 * Its tag field serializers are still used; unknown atoms are kept as raw bytes.
 */
internal object Mp4TagPatch {
 private data class Atom(val name:String,val bytes:ByteArray,val header:Int)
 private fun atoms(bytes:ByteArray):List<Atom>{val result=mutableListOf<Atom>();var at=0
  while(at<bytes.size){require(bytes.size-at>=8){"Truncated MP4 atom"};val buffer=ByteBuffer.wrap(bytes,at,bytes.size-at).order(ByteOrder.BIG_ENDIAN);var size=buffer.int.toLong() and 0xffffffffL;val name=String(bytes,at+4,4,Charsets.ISO_8859_1);var header=8;if(size==1L){require(bytes.size-at>=16);size=buffer.apply{position(at+8)}.long;header=16};if(size==0L)size=(bytes.size-at).toLong();require(size>=header&&size<=bytes.size-at){"Invalid MP4 atom size"};result+=Atom(name,bytes.copyOfRange(at,at+size.toInt()),header);at+=size.toInt()};return result
 }
 private fun box(name:String,payload:ByteArray):ByteArray=ByteBuffer.allocate(payload.size+8).order(ByteOrder.BIG_ENDIAN).putInt(payload.size+8).put(name.toByteArray(Charsets.ISO_8859_1)).put(payload).array()
 private fun key(atom:Atom):String{if(atom.name!="----")return atom.name;val children=atoms(atom.bytes.copyOfRange(atom.header,atom.bytes.size))
  fun text(name:String)=children.firstOrNull{it.name==name}?.let{String(it.bytes,it.header+4,it.bytes.size-it.header-4,Charsets.UTF_8)}.orEmpty()
  return "----:${text("mean")}:${text("name")}"}
 private fun serialize(fields:List<org.jaudiotagger.tag.TagField>):List<ByteArray> {
  if(fields.isEmpty())return emptyList()
  val first=fields.first() as Mp4TagField
  val payload=java.io.ByteArrayOutputStream()
  if(first is Mp4TagReverseDnsField){
   payload.write(box("mean",ByteArray(4)+first.issuer.toByteArray(Charsets.UTF_8)))
   payload.write(box("name",ByteArray(4)+first.descriptor.toByteArray(Charsets.UTF_8)))
  }
  for(field in fields){val f=field as Mp4TagField
   val header=ByteBuffer.allocate(8).order(ByteOrder.BIG_ENDIAN).putInt(f.fieldType.fileClassId).putInt(0).array()
   payload.write(box("data",header+f.rawContent))
  }
  return listOf(box(if(first is Mp4TagReverseDnsField)"----"else first.id,payload.toByteArray()))
 }
 fun readCustom(file:File,id:String):String {
  RandomAccessFile(file,"r").use{raf->var offset=0L
   while(offset<raf.length()){
    require(raf.length()-offset>=8);raf.seek(offset);var size=raf.readInt().toLong() and 0xffffffffL;val type=ByteArray(4);raf.readFully(type);var header=8
    if(size==1L){size=raf.readLong();header=16};if(size==0L)size=raf.length()-offset;require(size>=header&&size<=raf.length()-offset)
    if(String(type,Charsets.ISO_8859_1)=="moov"){
     require(size<=64*1024*1024);val data=ByteArray(size.toInt()-header);raf.readFully(data)
     fun child(bytes:ByteArray,name:String):ByteArray?=atoms(bytes).firstOrNull{it.name==name}?.let{it.bytes.copyOfRange(it.header, it.bytes.size)}
     val udta=child(data,"udta")?:return "";val meta=child(udta,"meta")?:return "";val ilst=child(meta.copyOfRange(4,meta.size),"ilst")?:return ""
     val item=atoms(ilst).firstOrNull{key(it)==id}?:return ""
     val value=atoms(item.bytes.copyOfRange(item.header,item.bytes.size)).firstOrNull{it.name=="data"}?:return ""
     return String(value.bytes,value.header+8,value.bytes.size-value.header-8,Charsets.UTF_8)
    };offset+=size
   }
  };return ""
 }
 fun write(file:File,tag:Tag,changed:Set<String>,artwork:Boolean){
  val patches=linkedMapOf<String,List<ByteArray>>()
  changed.forEach{id->val key=if(id=="TDOR")"----:com.apple.iTunes:ORIGINALDATE"else tag.createField(AudioTags.keys.getValue(id),if(id in listOf("TRCK","TPOS"))"1"else "probe").id
   patches[key]=serialize(tag.getFields(key))
  }
  if(artwork)patches["covr"]=serialize(tag.getFields("covr"))
  fun patch(bytes:ByteArray,path:List<String>):ByteArray{
   val list=atoms(bytes).toMutableList();val target=path.first();val index=list.indexOfFirst{it.name==target}
   val existing=list.getOrNull(index);val prefix=if(target=="meta")existing?.bytes?.copyOfRange(existing.header,existing.header+4)?:ByteArray(4)else byteArrayOf()
   val content=existing?.bytes?.copyOfRange(existing.header+prefix.size,existing.bytes.size)?:byteArrayOf()
   val updated=if(path.size==1){val old=atoms(content);val result=java.io.ByteArrayOutputStream();val seen=mutableSetOf<String>();for(atom in old){val id=key(atom);if(id !in patches)result.write(atom.bytes)else if(seen.add(id))patches.getValue(id).forEach{result.write(it)}};patches.filterKeys{it !in seen}.values.flatten().forEach{result.write(it)};result.toByteArray()}else{
    var child=patch(content,path.drop(1))
    if(target=="meta"&&existing==null){val handler=ByteArray(25);"mdir".toByteArray().copyInto(handler,8);child=box("hdlr",handler)+child};child
   }
   val new=Atom(target,box(target,prefix+updated),8);if(index<0)list+=new else list[index]=new
   return list.fold(byteArrayOf()){a,item->a+item.bytes}
  }
  RandomAccessFile(file,"rw").use{raf->var offset=0L;var moov:Pair<Long,ByteArray>?=null
   while(offset<raf.length()){require(raf.length()-offset>=8);raf.seek(offset);var size=raf.readInt().toLong() and 0xffffffffL;val type=ByteArray(4);raf.readFully(type);var header=8;if(size==1L){size=raf.readLong();header=16};require(size!=0L){"MP4 extends-to-EOF atoms require remux before tag editing"};require(size>=header&&size<=raf.length()-offset);if(String(type,Charsets.ISO_8859_1)=="moov"){require(moov==null&&size<=64*1024*1024){"Unsupported/oversized MP4 header"};val bytes=ByteArray(size.toInt());raf.seek(offset);raf.readFully(bytes);moov=offset to bytes};offset+=size
   }
   val (at,old)=moov?:error("Missing MP4 moov");val new=patch(old,listOf("moov","udta","meta","ilst"))
   if(at+old.size==raf.length()){raf.setLength(at);raf.seek(at)}else{raf.seek(at+4);raf.write("free".toByteArray());raf.seek(raf.length())}
   raf.write(new);raf.fd.sync()
  }
 }
}
