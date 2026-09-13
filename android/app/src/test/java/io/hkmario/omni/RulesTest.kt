package io.hkmario.omni
import org.junit.Assert.*
import org.junit.Test
import java.io.File
class RulesTest {
    @Test fun separateDirectoriesPreserveLegacyFallback(){val old=Prefs(tree="content://legacy");assertEquals("content://legacy",old.treeFor("mp3"));val split=old.copy(mp3Tree="content://music",mp4Tree="content://video");assertEquals("content://music",split.treeFor("mp3"));assertEquals("content://video",split.treeFor("mp4"));assertEquals("",split.copy(mp3Tree="").treeFor("mp3"))}

    @Test fun rawFrameEditPreservesOtherInstances(){val file=File.createTempFile("omni-id3",".mp3");try{val tag=Id3.read(file);tag.setRaw("TXXX",byteArrayOf(0,1));tag.setRaw("TXXX#1",byteArrayOf(0,2));tag.setRaw("TXXX",byteArrayOf(0,3));assertArrayEquals(byteArrayOf(0,2),tag.frames[1].data)}finally{file.delete()}}
    @Test fun titleValidation(){listOf("a/b","CON","a:","Song.","x\n").forEach{assertNotNull(Rules.titleError(it))};listOf("夜に駆ける","مرحبا","봄날","").forEach{assertNull(Rules.titleError(it))}}
    @Test fun andSearch(){val task=TaskItem(url="https://example.org",title="夜に駆ける",artist="YOASOBI",album="THE BOOK");assertTrue(Rules.matches(task,"夜 yoasobi book"));assertFalse(Rules.matches(task,"夜 other"))}
    @Test fun emaIdle(){val e=Ema();assertEquals(2000.0,e.sample(1000,5000,.5,true).first,0.0);assertEquals(2500.0,e.sample(3000,5000,.5,true).first,0.0);assertEquals(0.0,e.sample(3000,5000,.5,true).first,0.0)}
    @Test fun sessionDoesNotPersist(){val a=SessionGrants();a.cellularAll=true;a.suppressUnknownSpace=true;val b=SessionGrants();assertFalse(b.mayUseCellular("a"));assertFalse(b.suppressUnknownSpace)}
    @Test fun id3EmptyFrameAndAudioRoundtrip(){val dir=kotlin.io.path.createTempDirectory("omni-id3").toFile();try{val source=File(dir,"a.mp3");val audio=byteArrayOf(-1,-5,0,1,2,3);source.writeBytes(audio);val tag=Id3.read(source);tag.setText("TIT2","夜に駆ける");tag.setText("TALB","");tag.setRaw("PRIV",byteArrayOf(1,2,3));val dest=File(dir,"b.mp3");tag.write(source,dest);val read=Id3.read(dest);assertEquals("夜に駆ける",read.text("TIT2"));assertTrue(read.frames.any{it.id=="TALB"&&it.data.isNotEmpty()});assertArrayEquals(audio,dest.readBytes().copyOfRange(read.audioOffset.toInt(),dest.length().toInt()))}finally{dir.deleteRecursively()}}
}
