package io.hkmario.omni

import java.io.File
import java.io.IOException
import org.jaudiotagger.audio.AudioFileIO
import org.jaudiotagger.audio.mp3.MP3File
import org.jaudiotagger.tag.FieldKey
import org.jaudiotagger.tag.images.AndroidArtwork
import org.jaudiotagger.tag.id3.ID3v23Tag
import org.jaudiotagger.tag.id3.ID3v24Tag
import org.jaudiotagger.tag.Tag
import org.jaudiotagger.tag.flac.FlacTag
import org.jaudiotagger.tag.vorbiscomment.VorbisCommentTag
import org.jaudiotagger.tag.mp4.Mp4Tag
import org.jaudiotagger.tag.mp4.field.Mp4TagReverseDnsField

/** Patches use the existing review field IDs; missing fields/artwork remain untouched. */
class AudioTags private constructor(private val id3:Id3?,private val values:MutableMap<String,String>,private var arts:List<Art>) {
 private val changes=mutableMapOf<String,String>();private var changedArt=false
 val version:Int get()=id3?.version?:4
 val frames:List<Frame> get()=id3?.frames?:emptyList()
 fun text(id:String):String {
  val value=id3?.text(id)?:values[canonical(id)].orEmpty()
  if(id3!=null&&id=="TXXX:MusicBrainz Recording Id"&&value.isEmpty()) {
   val alias=id3.text("TXXX:MusicBrainz Track Id");if(alias.isNotEmpty())return alias
   val owner="http://musicbrainz.org\u0000".toByteArray(Charsets.US_ASCII)
   return id3.frames.firstOrNull{it.id=="UFID"&&it.data.take(owner.size).toByteArray().contentEquals(owner)}?.data?.drop(owner.size)?.toByteArray()?.toString(Charsets.UTF_8).orEmpty()
  }
  return value
 }
 fun artwork():ByteArray?=id3?.artwork()?:arts.firstOrNull()?.bytes
 fun setText(id:String,value:String){if(id3!=null){id3.setText(id,value);if(id=="TXXX:MusicBrainz Recording Id"){
   id3.setText("TXXX:MusicBrainz Track Id",value);val owner="http://musicbrainz.org\u0000".toByteArray(Charsets.US_ASCII)
   val index=id3.frames.indexOfFirst{it.id=="UFID"&&it.data.take(owner.size).toByteArray().contentEquals(owner)}
   val frame=Frame("UFID",byteArrayOf(0,0),owner+value.toByteArray(Charsets.UTF_8));if(index<0)id3.frames+=frame else id3.frames[index]=frame
  }}else{val key=canonical(id);require(key in keys){"Unsupported tag: $id"};values[key]=value;changes[key]=value}}
 fun setRaw(id:String,bytes:ByteArray){require(id3!=null){"Raw ID3 frames apply only to MP3"};id3.setRaw(id,bytes)}
 fun covers(value:List<Art>){if(id3!=null)id3.covers(value)else{arts=value;changedArt=true}}
 fun write(source:File,target:File){
  if(id3!=null){id3.write(source,target);return}
  val stage=File(target.path+"."+source.extension)
  try{
   source.copyTo(stage,false);val audio=AudioFileIO.read(stage);val tag=audio.tagOrCreateAndSetDefault
   changes.forEach{(id,value)->if(id=="TDOR")setOriginalDate(tag,value)else if(id=="TRCK"||id=="TPOS")setNumber(tag,id,value)else{val key=keys.getValue(id);if(value.isEmpty())tag.deleteField(key)else tag.setField(key,value)}}
   if(changedArt){tag.deleteArtworkField();arts.forEach{a->tag.setField(AndroidArtwork().apply{binaryData=a.bytes;mimeType=a.mime;description=a.description;pictureType=a.type;val options=android.graphics.BitmapFactory.Options().apply{inJustDecodeBounds=true};android.graphics.BitmapFactory.decodeByteArray(a.bytes,0,a.bytes.size,options);width=options.outWidth;height=options.outHeight})}}
   if(source.extension.equals("m4a",true))Mp4TagPatch.write(stage,tag,changes.keys,changedArt)else audio.commit()
   val read=read(stage);if(changedArt)check(read.arts.map{SafTagStorage.hashBytes(it.bytes)}==arts.map{SafTagStorage.hashBytes(it.bytes)}){"封面驗證失敗"};changes.forEach{(id,v)->check(read.text(id)==v){"Tag verification failed: $id"}}
   java.nio.file.Files.move(stage.toPath(),target.toPath())
  }catch(e:Exception){throw IOException("音訊標籤寫入／驗證失敗：${e.message}",e)}finally{stage.delete()}
 }
 companion object {
  fun supported(extension:String)=extension.trimStart('.').lowercase() in listOf("mp3","opus","m4a","flac")
  private fun canonical(id:String)=when(id){"TDRC"->"TYER";"TORY"->"TDOR";else->id}
  val keys=mapOf("TIT2" to FieldKey.TITLE,"TPE1" to FieldKey.ARTIST,"TALB" to FieldKey.ALBUM,"TPE2" to FieldKey.ALBUM_ARTIST,"TRCK" to FieldKey.TRACK,"TPOS" to FieldKey.DISC_NO,"TYER" to FieldKey.YEAR,"TDOR" to FieldKey.ORIGINAL_YEAR,"TCON" to FieldKey.GENRE,"TCOM" to FieldKey.COMPOSER,"TPUB" to FieldKey.RECORD_LABEL,"TSRC" to FieldKey.ISRC,"TXXX:MusicBrainz Recording Id" to FieldKey.MUSICBRAINZ_TRACK_ID,"TXXX:MusicBrainz Album Id" to FieldKey.MUSICBRAINZ_RELEASEID,"TSOT" to FieldKey.TITLE_SORT,"TSOP" to FieldKey.ARTIST_SORT,"COMM" to FieldKey.COMMENT)
  private fun setNumber(tag:Tag,id:String,value:String){
   require(value.isEmpty()||Regex("[0-9]+(/[0-9]+)?").matches(value)){"曲目／光碟格式必須為 n 或 n/total"}
   val key=keys.getValue(id);val total=if(id=="TRCK")FieldKey.TRACK_TOTAL else FieldKey.DISC_TOTAL
   val parts=value.split('/');if(value.isEmpty())tag.deleteField(key)else tag.setField(key,parts[0])
   if(parts.size==2)tag.setField(total,parts[1])else tag.deleteField(total)
  }
  private fun vorbis(t:Tag):VorbisCommentTag?=when(t){is VorbisCommentTag->t;is FlacTag->t.vorbisCommentTag;else->null}
  private fun originalDate(t:Tag):String=vorbis(t)?.getFirst("ORIGINALDATE")?:if(t is Mp4Tag)t.getFirst("----:com.apple.iTunes:ORIGINALDATE")else ""
  private fun setOriginalDate(t:Tag,value:String){val v=vorbis(t);if(v!=null){if(value.isEmpty())v.deleteField("ORIGINALDATE")else v.setField("ORIGINALDATE",value)}else if(t is Mp4Tag){val name="----:com.apple.iTunes:ORIGINALDATE";if(value.isEmpty())t.deleteField(name)else t.setField(Mp4TagReverseDnsField(name,"com.apple.iTunes","ORIGINALDATE",value))}else error("Unsupported date field")}
  fun read(file:File):AudioTags {
   if(file.extension.equals("mp3",true))return AudioTags(Id3.read(file),mutableMapOf(),emptyList())
   require(supported(file.extension)){"Unsupported audio format"}
   try{val audio=AudioFileIO.read(file);val t=audio.tag
    val values=keys.mapValues{(_,key)->if(t==null)""else if(key in listOf(FieldKey.ARTIST,FieldKey.ALBUM_ARTIST,FieldKey.GENRE,FieldKey.COMPOSER,FieldKey.ARTIST_SORT))t.getAll(key).joinToString("; ")else t.getFirst(key)}.toMutableMap()
    if(t!=null)for((id,total) in listOf("TRCK" to FieldKey.TRACK_TOTAL,"TPOS" to FieldKey.DISC_TOTAL)){
     val n=values[id].orEmpty();val count=t.getFirst(total);if(n.isNotEmpty()&&count.isNotEmpty())values[id]="$n/$count"
    }
    if(t!=null)values["TDOR"]=(if(file.extension.equals("m4a",true))Mp4TagPatch.readCustom(file,"----:com.apple.iTunes:ORIGINALDATE")else originalDate(t)).ifEmpty{values["TDOR"].orEmpty()}
    val arts=t?.artworkList?.map{Art(it.binaryData,it.mimeType,it.description,it.pictureType)}?:emptyList()
    return AudioTags(null,values,arts)
   }catch(e:Exception){throw IOException("音訊標頭／標籤無法讀取：${e.message}",e)}
  }
  fun setNewMp3Version(file:File,version:Int){require(version in 3..4);val audio=AudioFileIO.read(file) as MP3File;val previous=audio.getID3v2Tag()
   audio.setID3v2Tag(if(version==3){when(previous){null->ID3v23Tag();is ID3v23Tag->ID3v23Tag(previous);else->ID3v23Tag(previous)}}else{when(previous){null->ID3v24Tag();is ID3v24Tag->ID3v24Tag(previous);else->ID3v24Tag(previous)}});audio.commit()
  }
 }
}
