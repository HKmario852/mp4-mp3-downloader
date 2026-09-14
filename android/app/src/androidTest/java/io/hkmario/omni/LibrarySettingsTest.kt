package io.hkmario.omni
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.*
import org.junit.runner.RunWith
import java.io.File
@RunWith(AndroidJUnit4::class)
class LibrarySettingsTest {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 private fun screenshot(name:String){compose.waitForIdle();val image=compose.onRoot().captureToImage().asAndroidBitmap();File(compose.activity.getExternalFilesDir(null),name).outputStream().use{image.compress(android.graphics.Bitmap.CompressFormat.PNG,100,it)}}
 @Test fun embeddedSettingsSaveAndLibrarySelection(){
  val engine=(compose.activity.application as OmniApp).engine
  compose.runOnIdle{engine.save(engine.prefs.value.copy(language="zh-Hant",theme="dark",autoUpdate=false))}
  screenshot("v2-android-start.png")
  compose.onNodeWithTag("nav-settings").performClick()
  compose.onNodeWithText("一般").assertExists();compose.onNodeWithText("下載",substring=false).assertExists()
  screenshot("v2-android-settings-dark.png")
  compose.onNodeWithText("淺色").performClick();compose.onNodeWithText("儲存變更").performClick()
  compose.onNodeWithText("設定已儲存").assertExists();compose.onNodeWithText("OK").performClick()
  compose.runOnIdle{assertEquals("light",engine.prefs.value.theme)}
  screenshot("v2-android-settings-light.png")
  compose.onNodeWithTag("nav-history").performClick()
  compose.onNodeWithText("管理影片、音訊及標籤").assertExists()
  screenshot("v2-android-library.png")
  compose.onNodeWithTag("nav-settings").performClick()
  compose.onNodeWithText("深色").performClick()
  compose.onNodeWithTag("nav-history").performClick()
  compose.onNodeWithText("未儲存變更").assertExists();compose.onNodeWithText("離開").performClick()
  compose.runOnIdle{assertEquals("light",engine.prefs.value.theme)}
 }
}
