package io.hkmario.omni
import androidx.activity.compose.setContent
import androidx.compose.material3.*
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.graphics.Color
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.*
import java.io.File
import kotlinx.coroutines.runBlocking

class AcoustIdReviewUiTest {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 @Test fun scanRouteRetainsEditorDraftAndReturnsInSameActivity(){
  val engine=(compose.activity.application as OmniApp).engine;val prefs=engine.prefs.value;val file=File(compose.activity.filesDir,"review-route-test.mp3");file.writeBytes(byteArrayOf(1,2,3));val song=TaskItem(id="review-route-test",url="",mode="mp3",path=file.path,title="辨識測試歌曲",state=State.Completed,history=false)
  try{compose.runOnIdle{engine.save(prefs.copy(language="zh-Hant",theme="dark",autoUpdate=false));engine.tagImports[song.id]=song};compose.onNodeWithTag("nav-tags").performClick();compose.onNodeWithTag("tag-search").performTextInput("辨識測試歌曲");compose.onNodeWithTag("tag-row-${song.id}").performScrollTo().performClick();compose.waitUntil(10000){compose.onAllNodes(hasSetTextAction() and hasText("標題 / Title")).fetchSemanticsNodes().isNotEmpty()};compose.onNode(hasSetTextAction() and hasText("標題 / Title")).performScrollTo().performTextReplacement("尚未儲存 <草稿>");compose.onNodeWithText("Scan 音訊辨識").performClick();compose.onNodeWithTag("acoustid-review").assertExists();compose.onNodeWithTag("nav-settings").assertDoesNotExist();compose.onNodeWithText("返回標籤編輯").performClick();compose.onNodeWithTag("tag-search").assertTextContains("辨識測試歌曲");compose.onNode(hasSetTextAction() and hasText("尚未儲存 <草稿>")).assertExists();assertArrayEquals(byteArrayOf(1,2,3),file.readBytes())}
  finally{compose.runOnIdle{engine.tagImports.remove(song.id);engine.save(prefs)};file.delete()}
 }
 @Test fun resultPreviewWritesOnlySelectedFieldsAndSupportsExactUndo(){
  val engine=(compose.activity.application as OmniApp).engine;val file=File(compose.activity.filesDir,"review-apply-test.mp3");file.writeBytes(byteArrayOf(1,2,3));val source=file.readBytes();val song=TaskItem(id="review-apply-test",url="",mode="mp3",path=file.path,title="原名",state=State.Completed,history=false);var applied:Map<String,String>?=null;var undo:ReviewUndo?=null
  val candidate=MusicCandidate("cb39b5d8-ebb8-4bad-9f17-9d952108ecb7","11111111-1111-1111-1111-111111111111","A LETTER <nZk Ver.>","澤野弘之","Original Soundtrack","2014",.98,"fixture")
  try{compose.runOnIdle{engine.tagImports[song.id]=song;compose.activity.setContent{MaterialTheme(colorScheme=darkColorScheme(primary=Purple,onPrimary=Color.White,secondary=Blue,background=Color(0xFF111923),surface=Color(0xFF1A2532))){AcoustIdReviewScreen(engine,song,{}, {v,_,u,_->applied=v;undo=u},scanLookup={MusicResult("辨識完成",choices=listOf(candidate,candidate.copy(releaseId="22222222-2222-2222-2222-222222222222",album="另一版本")))},resolveLookup={MusicResult("辨識完成",title=it.title,tags=mapOf("TIT2" to it.title,"TPE1" to it.artist))})}}}
   compose.waitUntil(10000){compose.onAllNodes(hasText("✓ 辨識完成")).fetchSemanticsNodes().isNotEmpty()};assertArrayEquals(source,file.readBytes());compose.onNodeWithText("取消全選").performClick()
   // First table checkbox belongs to Title; filter excludes artwork radios and footer switches.
   compose.onAllNodes(isToggleable() and hasAnyAncestor(hasTestTag("acoustid-review")))[0].performClick()
   val image=compose.onRoot().captureToImage().asAndroidBitmap();File(compose.activity.getExternalFilesDir(null),"review-android.png").outputStream().use{image.compress(android.graphics.Bitmap.CompressFormat.PNG,100,it)}
   compose.onNodeWithText("套用所選標籤").performClick();compose.waitUntil(10000){applied!=null};assertEquals(setOf("TIT2"),applied!!.keys);assertEquals(candidate.title,Id3.read(file).text("TIT2"));assertEquals("",Id3.read(file).text("TPE1"));runBlocking{undo!!.restore(engine)};assertArrayEquals(source,file.readBytes())
  }finally{compose.runOnIdle{engine.tagImports.remove(song.id)};file.delete()}
 }
}
