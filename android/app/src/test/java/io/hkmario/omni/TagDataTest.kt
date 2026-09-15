package io.hkmario.omni
import org.junit.Test
import org.junit.Assert.*
import java.io.File
class TagDataTest {
 @Test fun commentCoverAndAudioRoundTrip(){val dir=java.nio.file.Files.createTempDirectory("omni-tags-").toFile();try{val input=File(dir,"audio.mp3");val audio=byteArrayOf(1,2,3,4);input.writeBytes(audio);val d=Id3.read(input);d.setText("COMM","廣東話註解");d.setText("TPE2","專輯演出者");val art=byteArrayOf(137.toByte(),80,78,71);d.covers(listOf(Art(art,"image/png","Front",3)));val output=File(dir,"tagged.mp3");d.write(input,output);val read=Id3.read(output);assertEquals("廣東話註解",read.text("COMM"));assertEquals("專輯演出者",read.text("TPE2"));assertArrayEquals(art,read.artwork());assertArrayEquals(audio,output.readBytes().drop(read.audioOffset.toInt()).toByteArray());read.setText("COMM","");read.covers(emptyList());val blank=File(dir,"blank.mp3");read.write(output,blank);val end=Id3.read(blank);assertEquals("",end.text("COMM"));assertTrue(end.frames.any{it.id=="COMM"});assertNull(end.artwork())}finally{dir.deleteRecursively()}}
}
