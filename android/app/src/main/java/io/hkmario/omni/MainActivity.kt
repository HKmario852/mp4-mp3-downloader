package io.hkmario.omni

import android.Manifest
import android.content.Intent
import android.content.ClipData
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.FileProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

val Ink=Color(0xFF111923);val Panel=Color(0xFF1A2532);val Muted=Color(0xFF91A5C2);val Purple=Color(0xFF7354FF);val Blue=Color(0xFF168FFF)
val Accent=Brush.horizontalGradient(listOf(Color(0xFF6130FF),Blue))
class MainActivity:ComponentActivity() {
    private val page=MutableStateFlow("new");private val sharedUrl=MutableStateFlow("")
    private lateinit var engine:Engine
    private var folderMode="mp4"
    private val notifications=registerForActivityResult(ActivityResultContracts.RequestPermission()){}
    private val tree=registerForActivityResult(ActivityResultContracts.OpenDocumentTree()){uri->if(uri!=null){contentResolver.takePersistableUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION);engine.save(if(folderMode=="mp3")engine.prefs.value.copy(mp3Tree=uri.toString())else engine.prefs.value.copy(mp4Tree=uri.toString()))}}
    override fun onCreate(savedInstanceState:Bundle?){super.onCreate(savedInstanceState);engine=(application as OmniApp).engine;route(intent);if(Build.VERSION.SDK_INT>=33)notifications.launch(Manifest.permission.POST_NOTIFICATIONS);setContent{MaterialTheme(colorScheme=darkColorScheme(primary=Purple,secondary=Blue,background=Ink,surface=Panel,onBackground=Color(0xFFEDF2FF),onSurface=Color(0xFFEDF2FF))){AppUi(engine,page,sharedUrl,{mode->folderMode=mode;tree.launch(null)},::openFile,::shareLog)}}}
    override fun onNewIntent(intent:Intent){super.onNewIntent(intent);setIntent(intent);route(intent)}
    private fun route(intent:Intent){if(intent.getStringExtra("page")=="history")page.value="history";if(intent.action==Intent.ACTION_SEND){sharedUrl.value=Regex("https://\\S+").find(intent.getStringExtra(Intent.EXTRA_TEXT)?:"")?.value?:"";page.value="new"}}
    private fun openFile(task:TaskItem){val path=task.path?:return;val uri=if(path.startsWith("content://"))Uri.parse(path)else FileProvider.getUriForFile(this,"$packageName.files",File(path));startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri,if(task.mode=="mp3")"audio/mpeg"else"video/mp4").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION))}
    private fun shareLog(task:TaskItem){val dir=File(cacheDir,"logs").apply{mkdirs()};val file=File(dir,"diagnostic.log");file.writeText(Engine.redact((task.error?:"")+"\n"+(task.stderr?:"")));val uri=FileProvider.getUriForFile(this,"$packageName.files",file);startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_STREAM,uri).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION),"分享診斷日誌"))}
}
@Composable fun AppUi(engine:Engine,pageFlow:MutableStateFlow<String>,urlFlow:MutableStateFlow<String>,chooseFolder:(String)->Unit,open:(TaskItem)->Unit,share:(TaskItem)->Unit) {
    val page by pageFlow.collectAsStateWithLifecycle();val tasks by engine.tasks.collectAsStateWithLifecycle();val groups by engine.groups.collectAsStateWithLifecycle();val prefs by engine.prefs.collectAsStateWithLifecycle();val questions by engine.spaceQuestions.collectAsStateWithLifecycle();val initError by engine.initError.collectAsStateWithLifecycle();val scope=rememberCoroutineScope()
    var formatFilter by remember{mutableStateOf("all")};var search by remember{mutableStateOf("")};var selected by remember{mutableStateOf(setOf<String>())};var clearIds by remember{mutableStateOf<Set<String>?>(null)};var editIds by remember{mutableStateOf<Set<String>?>(null)};var error by remember{mutableStateOf<String?>(null)};var diagnosis by remember{mutableStateOf<TaskItem?>(null)}
    val history=tasks.filter{it.history&&!it.groupRoot&&it.state==State.Completed&&(formatFilter=="all"||it.mode==formatFilter)&&Rules.matches(it,search)}
    fun start(){try{DownloadService.start(engine.context)}catch(e:Exception){error="無法啟動背景服務：${e.message}"}}
    BoxWithConstraints(Modifier.fillMaxSize().background(Ink)) {
        val wide=maxWidth>=840.dp
        Scaffold(containerColor=Ink,bottomBar={if(!wide)NavigationBar(containerColor=Panel){listOf("new" to "新增","queue" to "下載中","history" to "已下載","settings" to "設定").forEach{(key,label)->NavigationBarItem(selected=page==key,onClick={pageFlow.value=key},icon={Text(when(key){"new"->"＋";"queue"->"↓";"history"->"✓";else->"⚙"},fontSize=22.sp)},label={Text(label)})}}}) { padding ->
            Row(Modifier.fillMaxSize().padding(padding)) {
                if(wide)NavigationRail(containerColor=Panel){Spacer(Modifier.height(20.dp));listOf("new" to "＋","queue" to "↓","history" to "✓","settings" to "⚙").forEach{(key,symbol)->NavigationRailItem(selected=page==key,onClick={pageFlow.value=key},icon={Text(symbol,fontSize=24.sp)})}}
                Column(Modifier.weight(1f).fillMaxHeight().padding(if(wide)24.dp else 18.dp)) {
                    Row(verticalAlignment=Alignment.CenterVertically){Image(painter=androidx.compose.ui.res.painterResource(R.drawable.omni_mark),contentDescription=null,modifier=Modifier.size(38.dp));Spacer(Modifier.width(12.dp));Text("全能影音下載器",fontSize=23.sp,fontWeight=FontWeight.Bold)}
                    Text("你的媒體，井然有序",color=Muted,fontSize=12.sp,modifier=Modifier.padding(top=5.dp,bottom=22.dp))
                    if(initError!=null)Text(initError!!,color=MaterialTheme.colorScheme.error)
                    when(page) {
                        "new" -> NewDownload(engine,urlFlow,{id->pageFlow.value="queue";start()},{error=it},chooseFolder)
                        "settings" -> SettingsUi(engine,prefs,chooseFolder)
                        else -> {
                            Row(verticalAlignment=Alignment.CenterVertically){Text(if(page=="history")"已下載 (${history.size})"else"下載任務",fontSize=23.sp,fontWeight=FontWeight.SemiBold);Spacer(Modifier.weight(1f));if(page=="history")TextButton(onClick={clearIds=history.map{it.id}.toSet()}){Text("清空歷史")}}
                            if(page=="history")OutlinedTextField(value=search,onValueChange={search=it},placeholder={Text("搜尋歌曲、歌手、專輯、網址…")},singleLine=true,modifier=Modifier.fillMaxWidth().padding(vertical=12.dp))
                            if(page=="history")Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){listOf("all","mp3","mp4").forEach{f->FilterChip(selected=formatFilter==f,onClick={formatFilter=f;selected=emptySet()},label={Text(if(f=="all")"全部格式"else f.uppercase())})}}
                            if(selected.isNotEmpty())Row{TextButton(onClick={val mp3=tasks.filter{it.id in selected&&it.mode=="mp3"&&it.state==State.Completed}.map{it.id}.toSet();if(mp3.isNotEmpty())editIds=mp3 else error="請選擇已下載 MP3"}){Text("編輯標籤 (${selected.size})")};TextButton(onClick={scope.launch{engine.cancel(selected)}}){Text("取消所選")};TextButton(onClick={selected=emptySet()}){Text("取消選取")}}
                            val shown=if(page=="history")history else tasks.filter{!it.groupRoot&&it.groupId==null}
                            LazyColumn(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(12.dp)) {
                                if(shown.isEmpty()&&(page=="history"||groups.isEmpty()))item{EmptyCard()}
                                if(page!="history")items(groups,key={it.id}){g->var expanded by remember(g.id){mutableStateOf(false)};val children=tasks.filter{it.groupId==g.id&&!it.groupRoot};Card(colors=CardDefaults.cardColors(containerColor=Panel)){Column(Modifier.padding(16.dp)){Row(verticalAlignment=Alignment.CenterVertically){Text("清單 · ${g.title} (${children.size})",modifier=Modifier.weight(1f));TextButton(onClick={expanded=!expanded}){Text(if(expanded)"收合"else"展開")}};TextButton(onClick={scope.launch{engine.cancel(tasks.filter{it.groupId==g.id}.map{it.id}.toSet())}}){Text("取消清單未完成項目")};if(expanded)LazyColumn(Modifier.heightIn(max=320.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){items(children,key={it.id}){t->TaskCard(t,t.id in selected,{selected=if(t.id in selected)selected-t.id else selected+t.id},{scope.launch{engine.pause(t.id)}},{engine.resume(t.id);start()},{scope.launch{engine.cancel(setOf(t.id))}},{runCatching{open(t)}.onFailure{error=it.message}},{diagnosis=t})}}}}}
                                items(shown,key={it.id}){t->TaskCard(t,t.id in selected,{selected=if(t.id in selected)selected-t.id else selected+t.id},{scope.launch{engine.pause(t.id)}},{engine.resume(t.id);start()},{scope.launch{engine.cancel(setOf(t.id))}},{runCatching{open(t)}.onFailure{error=it.message}},{diagnosis=t})}
                            }
                        }
                    }
                }
            }
        }
    }
    tasks.firstOrNull{it.state==State.PendingChoice}?.let{task->AlertDialog(onDismissRequest={},title={Text("選擇下載範圍")},text={Text("此連結同時包含影片同播放清單。選擇前不會開始下載。")},confirmButton={TextButton(onClick={engine.choose(task.id,true);start()}){Text("整個播放清單")}},dismissButton={TextButton(onClick={engine.choose(task.id,false);start()}){Text("僅此影片")}})}
    val blocked=engine.needsPermission()
    if(blocked!=null&&tasks.none{it.state==State.PendingChoice})AlertDialog(onDismissRequest={scope.launch{engine.pause(blocked.id)}},title={Text("目前並非可用 Wi-Fi")},text={Text("已啟用僅限 Wi-Fi。要允許此任務使用行動數據或受限網路嗎？")},confirmButton={TextButton(onClick={engine.grants.cellularAll=true;start()}){Text("本次啟動全部允許")}},dismissButton={Column{TextButton(onClick={engine.grants.tasks.add(blocked.id);start()}){Text("僅限本次任務")};TextButton(onClick={scope.launch{engine.pause(blocked.id)}}){Text("暫停")}}})
    questions.firstOrNull()?.let{q->var suppress by remember(q.taskId){mutableStateOf(false)};AlertDialog(onDismissRequest={},title={Text("剩餘空間未知")},text={Column{Text("無法確定目標路徑剩餘空間，是否仍要繼續？");Row(verticalAlignment=Alignment.CenterVertically){Checkbox(suppress,{suppress=it});Text("本次啟動期間不再提示")}}},confirmButton={TextButton(onClick={q.answer.complete(true to suppress)}){Text("繼續")}},dismissButton={TextButton(onClick={q.answer.complete(false to false)}){Text("取消搬移")}})}
    clearIds?.let{ids->AlertDialog(onDismissRequest={clearIds=null},title={Text("清空歷史")},text={Text("即將清空當前篩選出的 ${ids.size} 筆歷史紀錄？\n實體檔案會保留。")},confirmButton={TextButton(onClick={engine.clearHistory(ids);clearIds=null}){Text("確認清空")}},dismissButton={TextButton(onClick={clearIds=null}){Text("取消")}})}
    editIds?.let{ids->TagDialog(engine,ids,{editIds=null})}
    diagnosis?.let{t->AlertDialog(onDismissRequest={diagnosis=null},title={Text("下載診斷")},text={Column(Modifier.heightIn(max=400.dp).verticalScroll(rememberScrollState())){Text(t.error?:"未記錄錯誤");Spacer(Modifier.height(12.dp));Text(t.stderr?:"",fontSize=12.sp,color=Muted)}},confirmButton={TextButton(onClick={share(t)}){Text("分享 .log")}},dismissButton={TextButton(onClick={diagnosis=null}){Text("關閉")}})}
    error?.let{text->AlertDialog(onDismissRequest={error=null},title={Text("操作未完成")},text={Text(text)},confirmButton={TextButton(onClick={error=null}){Text("知道了")}})}
}
@Composable fun GradientButton(text:String,onClick:()->Unit,modifier:Modifier=Modifier){Box(modifier.fillMaxWidth().background(Accent,RoundedCornerShape(10.dp)).clickable(onClick=onClick).padding(18.dp),contentAlignment=Alignment.Center){Text(text,color=Color.White,fontSize=17.sp,fontWeight=FontWeight.SemiBold)}}
@Composable fun EmptyCard(){Column(Modifier.fillMaxWidth().padding(vertical=50.dp),horizontalAlignment=Alignment.CenterHorizontally){Text("↓",fontSize=55.sp,color=Purple);Text("下一段精彩，由這裡開始",fontSize=20.sp,modifier=Modifier.padding(12.dp));Text("貼上連結，收藏你喜歡的影音",color=Muted)}}
@Composable fun NewDownload(engine:Engine,urlFlow:MutableStateFlow<String>,enqueued:(String)->Unit,error:(String)->Unit,chooseFolder:(String)->Unit){val url by urlFlow.collectAsStateWithLifecycle();val prefs by engine.prefs.collectAsStateWithLifecycle();var mode by remember{mutableStateOf("mp4")};var title by remember{mutableStateOf("準備好下一段精彩")};var thumb by remember{mutableStateOf<String?>(null)};var busy by remember{mutableStateOf(false)};val scope=rememberCoroutineScope()
    Column(Modifier.verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(14.dp)){
        OutlinedTextField(value=url,onValueChange={urlFlow.value=it},placeholder={Text("貼上影片連結")},singleLine=true,modifier=Modifier.fillMaxWidth(),shape=RoundedCornerShape(10.dp))
        GradientButton(if(busy)"正在分析…"else"分析連結",{if(!busy){busy=true;scope.launch{try{val data=engine.analyze(url.trim());title=data.optString("title");thumb=data.optString("thumbnail")}catch(e:Exception){error(e.message?:"分析失敗")}finally{busy=false}}}})
        Card(colors=CardDefaults.cardColors(containerColor=Panel)){Column(Modifier.padding(18.dp),verticalArrangement=Arrangement.spacedBy(16.dp)){Box(Modifier.fillMaxWidth().height(175.dp).background(Color(0xFF243449),RoundedCornerShape(8.dp)),contentAlignment=Alignment.Center){if(thumb!=null)AsyncImage(thumb,null,Modifier.fillMaxSize(),contentScale=androidx.compose.ui.layout.ContentScale.Crop)else Text("▶",fontSize=48.sp,color=Purple)};Text(title,fontSize=21.sp,fontWeight=FontWeight.SemiBold);Text("下載格式",color=Muted);Row(horizontalArrangement=Arrangement.spacedBy(10.dp)){FilterChip(selected=mode=="mp4",onClick={mode="mp4"},label={Text("▣ 影片 MP4")});FilterChip(selected=mode=="mp3",onClick={mode="mp3"},label={Text("♫ 純音訊 MP3")})};Text("畫質 / 音質選擇",color=Muted)
            Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){(if(mode=="mp3")listOf(128,192,256,320)else listOf(720,1080,1440,2160,0)).forEach{q->FilterChip(selected=q==if(mode=="mp3")prefs.kbps else prefs.height,onClick={engine.save(if(mode=="mp3")prefs.copy(kbps=q)else prefs.copy(height=q))},label={Text(if(q==0)"最佳（最高 4K）"else if(mode=="mp3")"$q"else"${q}p")})}}
            Text("${mode.uppercase()} 儲存位置",color=Muted);Text(if(prefs.treeFor(mode).isBlank())"App 音樂資料夾（解除安裝會移除）"else"已選擇共用儲存目錄",fontSize=13.sp);OutlinedButton(onClick={chooseFolder(mode)}){Text("選擇資料夾")};GradientButton("↓　開始下載",{try{enqueued(engine.enqueue(url.trim(),mode))}catch(e:Exception){error(e.message?:"無法新增")}});Text("MP3 最高雙聲道；320 kbps 不會提升來源本身音質。",fontSize=12.sp,color=Muted)
        }}
    }
}
@Composable fun TaskCard(t:TaskItem,selected:Boolean,toggle:()->Unit,pause:()->Unit,resume:()->Unit,cancel:()->Unit,open:()->Unit,diagnose:()->Unit){Card(colors=CardDefaults.cardColors(containerColor=if(selected)Color(0xFF283452)else Panel)){Column(Modifier.padding(14.dp)){Row(verticalAlignment=Alignment.CenterVertically){Checkbox(selected,{toggle()});Column(Modifier.weight(1f)){Text(t.title,fontWeight=FontWeight.SemiBold,maxLines=2);Text("${t.mode.uppercase()} · "+if(t.mode=="mp3")"${t.kbps} kbps"else if(t.height==0)"最佳"else"${t.height}p",color=Muted,fontSize=12.sp)};if(t.state==State.Completed)Text("✓",color=Color(0xFF2DDD83),fontSize=25.sp)};Spacer(Modifier.height(8.dp));Text(status(t),color=if(t.state==State.Completed)Color(0xFF2DDD83)else Blue,fontSize=13.sp);if(t.state==State.Downloading){LinearProgressIndicator(progress={t.progress/100},modifier=Modifier.fillMaxWidth().padding(vertical=10.dp),color=Purple);Text((if(t.speed<=0)"0 MB/s"else"%.2f MB/s".format(t.speed/1_000_000))+" · ETA ${t.eta}",fontSize=12.sp,color=Muted)};Row{when(t.state){State.Completed->TextButton(onClick=open){Text("開啟")};State.Paused,State.Failed->TextButton(onClick=resume){Text("繼續")};State.Downloading,State.Queued,State.Analyzing,State.RetryWait->TextButton(onClick=pause){Text("暫停")};else->Unit};if(t.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.RetryWait))TextButton(onClick=cancel){Text("取消")};if(t.state==State.Failed)TextButton(onClick=diagnose){Text("診斷")}}}}}
fun status(t:TaskItem)=when(t.state){State.PendingChoice->"等待選擇";State.Queued->"排隊中";State.Analyzing->"分析中";State.Downloading->"下載中 ${t.progress.toInt()}%";State.Processing->"處理中";State.Paused->"已暫停";State.Completed->"已完成";State.Cancelled->"已取消";State.Failed->"下載失敗";State.RetryWait->"連線異常，將在 ${((t.retryAt-System.currentTimeMillis())/1000).coerceAtLeast(0)} 秒後重試 (${t.retry}/3)"}
@Composable fun SettingsUi(engine:Engine,p:Prefs,chooseFolder:(String)->Unit){Column(Modifier.verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(14.dp)){Text("偏好設定",fontSize=24.sp,fontWeight=FontWeight.SemiBold);listOf(Triple("僅限 Wi-Fi",p.wifiOnly,0),Triple("自動清洗音樂標題後綴",p.cleanTitle,1),Triple("MusicBrainz 查詢",p.musicBrainz,2)).forEach{(label,value,key)->Row(verticalAlignment=Alignment.CenterVertically){Text(label,Modifier.weight(1f));Switch(value,{engine.save(when(key){0->p.copy(wifiOnly=it);1->p.copy(cleanTitle=it);else->p.copy(musicBrainz=it)})})}};Text("MusicBrainz 開啟後會傳送歌曲名及歌手，精確匹配後才採用雲端封面。",color=Muted,fontSize=12.sp);Text("同時下載數量");Row(horizontalArrangement=Arrangement.spacedBy(10.dp)){(1..5).forEach{n->FilterChip(selected=p.concurrency==n,onClick={engine.save(p.copy(concurrency=n))},label={Text("$n")})}};listOf("mp4","mp3").forEach{mode->OutlinedButton(onClick={chooseFolder(mode)}){Text("選擇 ${mode.uppercase()} 儲存目錄")}};Text("本次啟動授權在 App 程序重開後失效。Android 登入影片暫不與桌面 Cookie 同步。",color=Muted,fontSize=12.sp);Text("全能影音下載器 0.1.2",color=Muted,modifier=Modifier.padding(top=24.dp))}}
@Composable fun TagDialog(engine:Engine,ids:Set<String>,close:()->Unit){val scope=rememberCoroutineScope();var delta by remember{mutableStateOf(mapOf<String,String>())};var raw by remember{mutableStateOf("{}")};var error by remember{mutableStateOf<String?>(null)};var saving by remember{mutableStateOf(false)};var cover by remember{mutableStateOf<ByteArray?>(null)};val initial=remember(ids){ids.first().let(engine::get)}
    val pick=androidx.activity.compose.rememberLauncherForActivityResult(ActivityResultContracts.GetContent()){uri->if(uri!=null)scope.launch{try{cover=withContext(Dispatchers.IO){engine.context.contentResolver.openInputStream(uri)?.use{it.readBytes()}}}catch(e:Exception){error=e.message}}}
    val titleError=delta["TIT2"]?.let(Rules::titleError)
    AlertDialog(onDismissRequest={if(!saving)close()},title={Text("編輯 ${ids.size} 首 MP3")},text={Column(Modifier.heightIn(max=480.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(10.dp)){Text("只儲存已修改欄位；音軌編號會統一填入固定值。",fontSize=12.sp);listOf("TIT2" to "Title · 歌曲名","TPE1" to "Artist · 歌手","TALB" to "Album · 專輯","TRCK" to "Track · 音軌","TCON" to "Genre · 類型","TYER" to "Year · 年份").forEach{(id,label)->OutlinedTextField(value=delta[id]?:if(ids.size==1)when(id){"TIT2"->initial.title;"TPE1"->initial.artist;"TALB"->initial.album;else->""}else"",onValueChange={delta=delta+(id to it)},label={Text(label)},isError=id=="TIT2"&&titleError!=null,supportingText={if(id=="TIT2"&&titleError!=null)Text(titleError,color=MaterialTheme.colorScheme.error)},singleLine=true)};OutlinedTextField(raw,{raw=it},label={Text("進階 Raw Frames：Base64 JSON")});OutlinedButton(onClick={pick.launch("image/*")}){Text(if(cover==null)"選擇封面"else"✓ 封面已選取")};TextButton(onClick={val clip=engine.context.getSystemService(android.content.ClipboardManager::class.java).primaryClip;val uri=if(clip!=null&&clip.itemCount>0)clip.getItemAt(0).uri else null;if(uri!=null)scope.launch{try{cover=withContext(Dispatchers.IO){engine.context.contentResolver.openInputStream(uri)?.use{it.readBytes()}}}catch(e:Exception){error=e.message}}else error="剪貼簿未包含圖片檔案"}){Text("貼上剪貼簿封面")};error?.let{Text(it,color=MaterialTheme.colorScheme.error)} }},confirmButton={TextButton(enabled=titleError==null&&!saving,onClick={saving=true;scope.launch{try{val json=org.json.JSONObject(raw);val bytes=json.keys().asSequence().associateWith{android.util.Base64.decode(json.getString(it),android.util.Base64.DEFAULT)};TagEditor(engine).apply(ids,delta,bytes,cover);close()}catch(e:Exception){error=e.message}finally{saving=false}}}){Text("儲存")}},dismissButton={TextButton(enabled=!saving,onClick=close){Text("取消")}})
}
