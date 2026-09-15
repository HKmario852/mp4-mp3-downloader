package io.hkmario.omni
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.graphics.asAndroidBitmap
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.*
import java.io.File
class TagEditorUiTest {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 @Test fun importedSongEditsWithoutRenameAndWarnsBeforeLeaving(){
  val engine=(compose.activity.application as OmniApp).engine
  val file=File(compose.activity.filesDir,"tag-ui-test.mp3");file.writeBytes(byteArrayOf(1,2,3,4))
  val doc=Id3.read(file);doc.setText("TIT2","測試歌曲");val tmp=File(file.parentFile,"tag-ui-prepared.mp3");doc.write(file,tmp);tmp.copyTo(file,true);tmp.delete()
  val task=TaskItem(id="tag-ui-test",url="",mode="mp3",path=file.absolutePath,title="測試歌曲",state=State.Completed,history=false)
  compose.runOnIdle{engine.save(engine.prefs.value.copy(language="zh-Hant",theme="dark",autoUpdate=false));engine.tagImports[task.id]=task}
  compose.onNodeWithTag("nav-tags").performClick()
  compose.onNodeWithText("搜尋歌曲、演出者、專輯或檔名…").assertExists()
  compose.onNodeWithTag("tag-search").performTextInput("測試歌曲")
  compose.onNodeWithTag("tag-row-tag-ui-test").performScrollTo().performClick()
  compose.waitUntil(10000){compose.onAllNodes(hasSetTextAction() and hasText("測試歌曲")).fetchSemanticsNodes().isNotEmpty()}
  val title=compose.onNode(hasSetTextAction() and hasText("標題 / Title"));title.performScrollTo().performTextReplacement("invalid/title")
  compose.onNodeWithText("儲存標籤").assertIsNotEnabled()
  title.performTextReplacement("新歌曲名")
  compose.onNodeWithText("儲存標籤").performClick()
  try{compose.waitUntil(10000){engine.get(task.id).title=="新歌曲名"}}catch(e:Throwable){File(compose.activity.getExternalFilesDir(null),"tag-failure.txt").writeText(compose.onRoot(useUnmergedTree=true).printToString()+"\nENGINE="+engine.get(task.id));throw e}
  assertEquals(file.absolutePath,engine.get(task.id).path);assertEquals("新歌曲名",Id3.read(file).text("TIT2"))
  compose.onNodeWithTag("tag-search").performTextClearance();compose.waitForIdle();val image=compose.onRoot().captureToImage().asAndroidBitmap();File(compose.activity.getExternalFilesDir(null),"v21-android-tags.png").outputStream().use{image.compress(android.graphics.Bitmap.CompressFormat.PNG,100,it)}
  title.performTextReplacement("未儲存")
  compose.onNodeWithTag("nav-settings").performClick();compose.onNodeWithText("未儲存變更").assertExists();compose.onNodeWithText("返回").performClick()
  compose.onNodeWithText("復原變更").performClick();compose.waitForIdle()
  compose.runOnIdle{engine.tagImports.remove(task.id)};file.delete()
 }
}
