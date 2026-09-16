package io.hkmario.omni
import android.graphics.Bitmap
import org.junit.Test
import org.junit.Assert.*
class ArtworkPolicyTest {
 @Test fun actualDecodedDimensionsDecideArtwork(){for((w,h,expected) in listOf(Triple(233,217,true),Triple(475,500,true),Triple(1280,720,false))){val bitmap=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);val output=java.io.ByteArrayOutputStream();bitmap.compress(Bitmap.CompressFormat.PNG,100,output);bitmap.recycle();assertEquals(expected,AlbumArtwork.accept(output.toByteArray()))};assertFalse(AlbumArtwork.accept(byteArrayOf(1,2,3)))}
}
