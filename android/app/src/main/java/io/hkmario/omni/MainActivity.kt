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
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.FileProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import coil.compose.AsyncImage
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

val Ink:Color @Composable get()=MaterialTheme.colorScheme.background
val Panel:Color @Composable get()=MaterialTheme.colorScheme.surface
val Muted:Color @Composable get()=MaterialTheme.colorScheme.onSurfaceVariant
val Purple=Color(0xFF7354FF);val Blue=Color(0xFF168FFF)
object UiLanguage {var value by mutableStateOf("zh-Hant")}
fun uiText(zh:String,en:String)=if(UiLanguage.value=="en")en else zh
val Accent=Brush.horizontalGradient(listOf(Color(0xFF6130FF),Blue))
class MainActivity:ComponentActivity() {
    private val page=MutableStateFlow("new");private val sharedUrl=MutableStateFlow("")
    private lateinit var engine:Engine
    private var folderMode="mp4"
    private val notifications=registerForActivityResult(ActivityResultContracts.RequestPermission()){}
    private val tree=registerForActivityResult(ActivityResultContracts.OpenDocumentTree()){uri->if(uri!=null){contentResolver.takePersistableUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION);engine.save(if(folderMode=="mp3")engine.prefs.value.copy(mp3Tree=uri.toString())else engine.prefs.value.copy(mp4Tree=uri.toString()))}}
    override fun onCreate(savedInstanceState:Bundle?){
        super.onCreate(savedInstanceState);engine=(application as OmniApp).engine;route(intent)
        if(Build.VERSION.SDK_INT>=33)notifications.launch(Manifest.permission.POST_NOTIFICATIONS)
        if(savedInstanceState==null&&engine.prefs.value.resumeOnStart){engine.tasks.value.filter{it.state==State.Paused}.forEach{engine.resume(it.id)};if(engine.tasks.value.any{it.state==State.Queued})DownloadService.start(this)}
        if(savedInstanceState==null&&engine.prefs.value.autoUpdate)lifecycleScope.launch{runCatching{Updates.check(this@MainActivity,engine.prefs.value)}.onSuccess{if(it.first)Notices.show(this@MainActivity,it.second)} }
        lifecycleScope.launch{engine.completionEvents.collect{task->if(lifecycle.currentState.isAtLeast(androidx.lifecycle.Lifecycle.State.RESUMED))runCatching{when(engine.prefs.value.completionAction){"file"->openFile(task);"folder"->folder(engine,task)}}}}
        setContent{val p by engine.prefs.collectAsStateWithLifecycle();val dark=p.theme=="dark"||(p.theme=="system"&&isSystemInDarkTheme());val colors=if(dark)darkColorScheme(primary=Purple,onPrimary=Color.White,primaryContainer=Color(0xFF273554),onPrimaryContainer=Color(0xFFEDF2FF),secondaryContainer=Color(0xFF273554),onSecondaryContainer=Color.White,secondary=Blue,background=Color(0xFF111923),surface=Color(0xFF1A2532),onBackground=Color(0xFFEDF2FF),onSurface=Color(0xFFEDF2FF),onSurfaceVariant=Color(0xFF91A5C2))else lightColorScheme(primary=Color(0xFF6242DB),secondary=Blue,background=Color(0xFFF3F5FB),surface=Color(0xFFFFFFFF));MaterialTheme(colorScheme=colors){AppUi(engine,page,sharedUrl,{mode->folderMode=mode;tree.launch(null)},::openFile,::shareLog)}}
    }
    override fun onResume(){super.onResume();if(::engine.isInitialized&&engine.prefs.value.monitorClipboard)window.decorView.post{val clip=getSystemService(android.content.ClipboardManager::class.java).primaryClip;val value=if(clip!=null&&clip.itemCount>0)clip.getItemAt(0).coerceToText(this).toString().trim()else "";if(Rules.validUrl(value)&&sharedUrl.value!=value)sharedUrl.value=value}}
    override fun onNewIntent(intent:Intent){super.onNewIntent(intent);setIntent(intent);route(intent)}
    private fun route(intent:Intent){if(intent.getStringExtra("page")=="history")page.value="history";if(intent.action==Intent.ACTION_SEND){sharedUrl.value=Regex("https://\\S+").find(intent.getStringExtra(Intent.EXTRA_TEXT)?:"")?.value?:"";page.value="new"}}
    private fun openFile(task:TaskItem){val path=task.path?:return;val uri=if(path.startsWith("content://"))Uri.parse(path)else FileProvider.getUriForFile(this,"$packageName.files",File(path));startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri,android.webkit.MimeTypeMap.getSingleton().getMimeTypeFromExtension(task.extension)?:if(task.mode=="mp3")"audio/*"else"video/*").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION))}
    private fun shareLog(task:TaskItem){val dir=File(cacheDir,"logs").apply{mkdirs()};val file=File(dir,"diagnostic.log");file.writeText(Engine.redact((task.error?:"")+"\n"+(task.stderr?:"")));val uri=FileProvider.getUriForFile(this,"$packageName.files",file);startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_STREAM,uri).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION),"分享診斷日誌"))}
}
@Composable fun AppUi(engine:Engine,pageFlow:MutableStateFlow<String>,urlFlow:MutableStateFlow<String>,chooseFolder:(String)->Unit,open:(TaskItem)->Unit,share:(TaskItem)->Unit) {
    val page by pageFlow.collectAsStateWithLifecycle();val tasks by engine.tasks.collectAsStateWithLifecycle();val groups by engine.groups.collectAsStateWithLifecycle();val prefs by engine.prefs.collectAsStateWithLifecycle();val questions by engine.spaceQuestions.collectAsStateWithLifecycle();val initError by engine.initError.collectAsStateWithLifecycle();val scope=rememberCoroutineScope()
    var settingsDirty by remember{mutableStateOf(false)};var nextPage by remember{mutableStateOf<String?>(null)}
    fun navigate(target:String){if(page=="settings"&&settingsDirty)nextPage=target else pageFlow.value=target}
    var failures by remember{mutableStateOf(false)};var deleteId by remember{mutableStateOf<String?>(null)};var formatFilter by remember{mutableStateOf("all")};var search by remember{mutableStateOf("")};var selected by remember{mutableStateOf(setOf<String>())};var clearIds by remember{mutableStateOf<Set<String>?>(null)};var editIds by remember{mutableStateOf<Set<String>?>(null)};var error by remember{mutableStateOf<String?>(null)};var diagnosis by remember{mutableStateOf<TaskItem?>(null)}
    val history=tasks.filter{it.history&&!it.groupRoot&&it.state==State.Completed&&(formatFilter=="all"||it.mode==formatFilter)&&Rules.matches(it,search)}
    fun start(){try{DownloadService.start(engine.context)}catch(e:Exception){error="無法啟動背景服務：${e.message}"}}
    BoxWithConstraints(Modifier.fillMaxSize().background(Ink)) {
        val wide=maxWidth>=840.dp
        Scaffold(containerColor=Ink,bottomBar={if(!wide)NavigationBar(containerColor=Panel){listOf("new" to "新增","queue" to "下載中","history" to "已下載","settings" to "設定").forEach{(key,label)->NavigationBarItem(modifier=Modifier.testTag("nav-$key"),selected=page==key,onClick={navigate(key)},icon={Text(when(key){"new"->"＋";"queue"->"↓";"history"->"✓";else->"⚙"},fontSize=22.sp)},label={Text(text(prefs,label,when(key){"new"->"New";"queue"->"Downloads";"history"->"Downloaded";else->"Settings"}))})}}}) { padding ->
            Row(Modifier.fillMaxSize().padding(padding)) {
                if(wide)NavigationRail(containerColor=Panel){Spacer(Modifier.height(20.dp));listOf("new" to "＋","queue" to "↓","history" to "✓","settings" to "⚙").forEach{(key,symbol)->NavigationRailItem(modifier=Modifier.testTag("nav-$key"),selected=page==key,onClick={navigate(key)},icon={Text(symbol,fontSize=24.sp)})}}
                Column(Modifier.weight(1f).fillMaxHeight().padding(if(wide)24.dp else 18.dp)) {
                    Row(verticalAlignment=Alignment.CenterVertically){Image(painter=androidx.compose.ui.res.painterResource(R.drawable.omni_mark),contentDescription=null,modifier=Modifier.size(38.dp));Spacer(Modifier.width(12.dp));Text("全能影音下載器",fontSize=23.sp,fontWeight=FontWeight.Bold)}
                    Text(uiText("你的媒體，井然有序","Your media, organized"),color=Muted,fontSize=12.sp,modifier=Modifier.padding(top=5.dp,bottom=22.dp))
                    if(initError!=null)Text(initError!!,color=MaterialTheme.colorScheme.error)
                    when(page) {
                        "new" -> NewDownload(engine,urlFlow,{id->pageFlow.value="queue";start()},{error=it},chooseFolder)
                        "settings" -> SettingsScreen(engine){settingsDirty=it}
                        "history" -> LibraryScreen(engine,open)
                        else -> {
                            Row(verticalAlignment=Alignment.CenterVertically){Text(if(page=="history")text(prefs,"已下載 (${history.size})","Downloaded (${history.size})")else if(failures)text(prefs,"下載失敗","Failed downloads")else text(prefs,"下載任務","Download tasks"),fontSize=23.sp,fontWeight=FontWeight.SemiBold);Spacer(Modifier.weight(1f));if(page=="history")TextButton(enabled=history.isNotEmpty(),onClick={clearIds=history.map{it.id}.toSet()}){Text("清空歷史")}}
                            if(page=="history")OutlinedTextField(value=search,onValueChange={search=it},placeholder={Text("搜尋歌曲、歌手、專輯、網址…")},singleLine=true,modifier=Modifier.fillMaxWidth().padding(vertical=12.dp))
                            if(page=="history")Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){listOf("all","mp3","mp4").forEach{f->FilterChip(selected=formatFilter==f,onClick={formatFilter=f;selected=emptySet()},label={Text(if(f=="all")"全部格式"else f.uppercase())})}}
                            if(page!="history")TextButton(onClick={failures=!failures;selected=emptySet()}){Text(if(failures)"返回下載任務"else"失敗任務（${tasks.count{it.state==State.Failed&&!it.groupRoot}}）")}
                            if(selected.isNotEmpty())Row(Modifier.horizontalScroll(rememberScrollState())){if(page=="history"&&selected.size==1)TextButton(onClick={deleteId=selected.single()}){Text("刪除檔案")};TextButton(onClick={val mp3=tasks.filter{it.id in selected&&it.extension=="mp3"&&it.state==State.Completed}.map{it.id}.toSet();if(mp3.isNotEmpty())editIds=mp3 else error="請選擇已下載 MP3"}){Text("編輯標籤 (${selected.size})")};TextButton(onClick={scope.launch{engine.cancel(selected)}}){Text(uiText("移除","Remove"))};TextButton(onClick={selected=emptySet()}){Text(uiText("取消選取","Clear selection"))}}
                            val shown=if(page=="history")history else tasks.filter{!it.groupRoot&&it.groupId==null&&(if(failures)it.state==State.Failed else Rules.inQueue(it))}
                            LazyColumn(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(12.dp)) {
                                if(shown.isEmpty()&&(page=="history"||tasks.none{it.groupId!=null&&!it.groupRoot&&(if(failures)it.state==State.Failed else Rules.inQueue(it))}))item{EmptyCard()}
                                if(page!="history")items(groups.filter{g->tasks.any{it.groupId==g.id&&!it.groupRoot&&(if(failures)it.state==State.Failed else Rules.inQueue(it))}},key={it.id}){g->var expanded by remember(g.id){mutableStateOf(false)};val children=tasks.filter{it.groupId==g.id&&!it.groupRoot&&(if(failures)it.state==State.Failed else Rules.inQueue(it))};Card(colors=CardDefaults.cardColors(containerColor=Panel)){Column(Modifier.padding(16.dp)){Row(verticalAlignment=Alignment.CenterVertically){Text("清單 · ${g.title} (${children.size})",modifier=Modifier.weight(1f));TextButton(onClick={expanded=!expanded}){Text(if(expanded)"收合"else"展開")}};TextButton(onClick={scope.launch{engine.cancel(tasks.filter{it.groupId==g.id}.map{it.id}.toSet())}}){Text("移除清單未完成項目")};if(expanded)LazyColumn(Modifier.heightIn(max=320.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){items(children,key={it.id}){t->TaskCard(t,t.id in selected,{selected=if(t.id in selected)selected-t.id else selected+t.id},{scope.launch{engine.pause(t.id)}},{engine.resume(t.id);start()},{scope.launch{engine.cancel(setOf(t.id))}},{runCatching{open(t)}.onFailure{error=it.message}},{diagnosis=t})}}}}}
                                items(shown,key={it.id}){t->TaskCard(t,t.id in selected,{selected=if(t.id in selected)selected-t.id else selected+t.id},{scope.launch{engine.pause(t.id)}},{engine.resume(t.id);start()},{scope.launch{engine.cancel(setOf(t.id))}},{runCatching{open(t)}.onFailure{error=it.message}},{diagnosis=t})}
                            }
                        }
                    }
                }
            }
        }
    }
    nextPage?.let{target->AlertDialog(onDismissRequest={nextPage=null},title={Text(text(prefs,"未儲存變更","Unsaved changes"))},text={Text(text(prefs,"離開並捨棄設定變更？","Leave and discard settings changes?"))},confirmButton={TextButton(onClick={settingsDirty=false;nextPage=null;pageFlow.value=target}){Text(text(prefs,"離開","Leave"))}},dismissButton={TextButton(onClick={nextPage=null}){Text(text(prefs,"返回","Back"))}})}
    val duplicates by engine.duplicateQuestions.collectAsStateWithLifecycle()
    duplicates.firstOrNull()?.let{q->AlertDialog(onDismissRequest={},title={Text(text(prefs,"檔案已存在","File already exists"))},text={Text(q.name)},confirmButton={TextButton(onClick={q.answer.complete("rename")}){Text(text(prefs,"自動重新命名","Auto rename"))}},dismissButton={Row{TextButton(onClick={q.answer.complete("overwrite")}){Text(text(prefs,"覆蓋","Overwrite"))};TextButton(onClick={q.answer.complete("skip")}){Text(text(prefs,"跳過","Skip"))}}})}
    tasks.firstOrNull{it.state==State.PendingChoice}?.let{task->AlertDialog(onDismissRequest={},title={Text(uiText("選擇下載範圍","Choose download scope"))},text={Text(uiText("此連結同時包含影片同播放清單。選擇前不會開始下載。","This link includes a video and playlist. Downloading waits for your choice."))},confirmButton={TextButton(onClick={engine.choose(task.id,true);start()}){Text(uiText("整個播放清單","Entire playlist"))}},dismissButton={TextButton(onClick={engine.choose(task.id,false);start()}){Text(uiText("僅此影片","This video only"))}})}
    val blocked=engine.needsPermission()
    if(blocked!=null&&tasks.none{it.state==State.PendingChoice})AlertDialog(onDismissRequest={scope.launch{engine.pause(blocked.id)}},title={Text(uiText("目前並非可用 Wi-Fi","Wi-Fi unavailable"))},text={Text(uiText("已啟用僅限 Wi-Fi。要允許此任務使用行動數據或受限網路嗎？","Wi-Fi only is enabled. Allow this download on the current network?"))},confirmButton={TextButton(onClick={engine.grants.cellularAll=true;start()}){Text(uiText("本次啟動全部允許","Allow for this app session"))}},dismissButton={Column{TextButton(onClick={engine.grants.tasks.add(blocked.id);start()}){Text(uiText("僅限本次任務","Allow this task only"))};TextButton(onClick={scope.launch{engine.pause(blocked.id)}}){Text(uiText("暫停","Pause"))}}})
    questions.firstOrNull()?.let{q->var suppress by remember(q.taskId){mutableStateOf(false)};AlertDialog(onDismissRequest={},title={Text(uiText("剩餘空間未知","Available space unknown"))},text={Column{Text(uiText("無法確定目標路徑剩餘空間，是否仍要繼續？","Cannot determine free space. Continue anyway?"));Row(verticalAlignment=Alignment.CenterVertically){Checkbox(suppress,{suppress=it});Text(uiText("本次啟動期間不再提示","Do not ask again this session"))}}},confirmButton={TextButton(onClick={q.answer.complete(true to suppress)}){Text(uiText("繼續","Resume"))}},dismissButton={TextButton(onClick={q.answer.complete(false to false)}){Text(uiText("取消搬移","Cancel transfer"))}})}
    deleteId?.let{id->AlertDialog(onDismissRequest={deleteId=null},title={Text("刪除所選檔案")},text={Text("將永久刪除「${engine.get(id).title}」及對應紀錄，無法復原。其他檔案不受影響。")},confirmButton={TextButton(onClick={deleteId=null;scope.launch{try{engine.deleteDownloaded(id);selected=selected-id}catch(e:Exception){error=e.message}}}){Text("刪除檔案")}},dismissButton={TextButton(onClick={deleteId=null}){Text(uiText("取消","Cancel"))}})}
    clearIds?.let{ids->AlertDialog(onDismissRequest={clearIds=null},title={Text("清空歷史")},text={Text("即將清空當前篩選出的 ${ids.size} 筆歷史紀錄？\n實體檔案會保留。")},confirmButton={TextButton(onClick={engine.clearHistory(ids);clearIds=null}){Text("確認清空")}},dismissButton={TextButton(onClick={clearIds=null}){Text(uiText("取消","Cancel"))}})}
    editIds?.let{ids->TagDialog(engine,ids,{editIds=null})}
    diagnosis?.let{t->AlertDialog(onDismissRequest={diagnosis=null},title={Text(uiText("下載診斷","Download diagnostics"))},text={Column(Modifier.heightIn(max=400.dp).verticalScroll(rememberScrollState())){Text(t.error?:"未記錄錯誤");Spacer(Modifier.height(12.dp));Text(t.stderr?:"",fontSize=12.sp,color=Muted)}},confirmButton={TextButton(onClick={share(t)}){Text(uiText("分享 .log","Share .log"))}},dismissButton={TextButton(onClick={diagnosis=null}){Text(uiText("關閉","Close"))}})}
    error?.let{text->AlertDialog(onDismissRequest={error=null},title={Text(uiText("操作未完成","Operation incomplete"))},text={Text(text)},confirmButton={TextButton(onClick={error=null}){Text(uiText("知道了","OK"))}})}
}
@Composable fun GradientButton(text:String,onClick:()->Unit,modifier:Modifier=Modifier){Box(modifier.fillMaxWidth().background(Accent,RoundedCornerShape(10.dp)).clickable(onClick=onClick).padding(18.dp),contentAlignment=Alignment.Center){Text(text,color=Color.White,fontSize=17.sp,fontWeight=FontWeight.SemiBold)}}
@Composable fun EmptyCard(){Column(Modifier.fillMaxWidth().padding(vertical=50.dp),horizontalAlignment=Alignment.CenterHorizontally){Text("↓",fontSize=55.sp,color=Purple);Text(uiText("下一段精彩，由這裡開始","Start your next download here"),fontSize=20.sp,modifier=Modifier.padding(12.dp));Text(uiText("貼上連結，收藏你喜歡的影音","Paste a link to save your favorite media"),color=Muted)}}
@Composable fun NewDownload(engine:Engine,urlFlow:MutableStateFlow<String>,enqueued:(String)->Unit,error:(String)->Unit,chooseFolder:(String)->Unit){val url by urlFlow.collectAsStateWithLifecycle();val prefs by engine.prefs.collectAsStateWithLifecycle();var mode by remember{mutableStateOf(if(prefs.defaultType=="audio")"mp3"else"mp4")};var askType by remember{mutableStateOf(false)};var title by remember{mutableStateOf("準備好下一段精彩")};var thumb by remember{mutableStateOf<String?>(null)};var busy by remember{mutableStateOf(false)};val scope=rememberCoroutineScope()
    Column(Modifier.verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(14.dp)){
        OutlinedTextField(value=url,onValueChange={urlFlow.value=it},placeholder={Text(uiText("貼上影片連結","Paste video link"))},singleLine=true,modifier=Modifier.fillMaxWidth(),shape=RoundedCornerShape(10.dp))
        GradientButton(if(busy)uiText("正在分析…","Analyzing…")else uiText("分析連結","Analyze link"),{if(!busy){busy=true;scope.launch{try{val data=engine.analyze(url.trim());title=data.optString("title");thumb=data.optString("thumbnail")}catch(e:Exception){error(e.message?:"分析失敗")}finally{busy=false}}}})
        Card(colors=CardDefaults.cardColors(containerColor=Panel)){Column(Modifier.padding(18.dp),verticalArrangement=Arrangement.spacedBy(16.dp)){Box(Modifier.fillMaxWidth().height(175.dp).background(Color(0xFF243449),RoundedCornerShape(8.dp)),contentAlignment=Alignment.Center){if(thumb!=null)AsyncImage(thumb,null,Modifier.fillMaxSize(),contentScale=androidx.compose.ui.layout.ContentScale.Fit)else Text("▶",fontSize=48.sp,color=Purple)};Text(title,fontSize=21.sp,fontWeight=FontWeight.SemiBold);Text(uiText("下載格式","Download format"),color=Muted);Row(horizontalArrangement=Arrangement.spacedBy(10.dp)){FilterChip(selected=mode=="mp4",onClick={mode="mp4"},label={Text("▣ "+prefs.videoFormat.uppercase())});FilterChip(selected=mode=="mp3",onClick={mode="mp3"},label={Text("♫ "+prefs.audioFormat.uppercase())})};Text(uiText("畫質 / 音質選擇","Video / audio quality"),color=Muted)
            Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){(if(mode=="mp3")listOf(128,192,256,320)else listOf(720,1080,1440,2160,0)).forEach{q->FilterChip(selected=q==if(mode=="mp3")prefs.kbps else prefs.height,onClick={engine.save(if(mode=="mp3")prefs.copy(kbps=q)else prefs.copy(height=q))},label={Text(if(q==0)"最佳（最高 4K）"else if(mode=="mp3")"$q"else"${q}p")})}}
            Text("${mode.uppercase()} 儲存位置",color=Muted);Text(if(prefs.treeFor(mode).isBlank())"App 音樂資料夾（解除安裝會移除）"else"已選擇共用儲存目錄",fontSize=13.sp);OutlinedButton(onClick={chooseFolder(mode)}){Text(uiText("選擇資料夾","Choose folder"))};GradientButton(uiText("↓　開始下載","↓ Start download"),{try{if(prefs.defaultType=="ask")askType=true else enqueued(engine.enqueue(url.trim(),mode))}catch(e:Exception){error(e.message?:"無法新增")}});Text(uiText("MP3 最高雙聲道；320 kbps 不會提升來源本身音質。","MP3 supports up to stereo. 320 kbps cannot improve the source audio."),fontSize=12.sp,color=Muted)
        }}
    }
    if(askType)AlertDialog(onDismissRequest={askType=false},title={Text(text(prefs,"選擇下載類型","Choose download type"))},confirmButton={TextButton(onClick={askType=false;runCatching{enqueued(engine.enqueue(url.trim(),"mp4"))}.onFailure{error(it.message?:"Error")}}){Text(prefs.videoFormat.uppercase())}},dismissButton={TextButton(onClick={askType=false;runCatching{enqueued(engine.enqueue(url.trim(),"mp3"))}.onFailure{error(it.message?:"Error")}}){Text(prefs.audioFormat.uppercase())}})

}
@Composable fun TaskCard(t:TaskItem,selected:Boolean,toggle:()->Unit,pause:()->Unit,resume:()->Unit,cancel:()->Unit,open:()->Unit,diagnose:()->Unit){Card(colors=CardDefaults.cardColors(containerColor=if(selected)Color(0xFF283452)else Panel)){Column(Modifier.padding(14.dp)){Row(verticalAlignment=Alignment.CenterVertically){Checkbox(selected,{toggle()});Column(Modifier.weight(1f)){Text(t.title,fontWeight=FontWeight.SemiBold,maxLines=2);Text("${t.extension.uppercase()} · "+if(t.mode=="mp3")"${t.kbps} kbps"else if(t.height==0)"最佳"else"${t.height}p",color=Muted,fontSize=12.sp)};if(t.state==State.Completed)Text("✓",color=Color(0xFF2DDD83),fontSize=25.sp)};Spacer(Modifier.height(8.dp));if(t.metadataStatus.isNotBlank())Text(t.metadataStatus,color=Muted,fontSize=12.sp);Text(status(t),color=if(t.state==State.Completed)Color(0xFF2DDD83)else Blue,fontSize=13.sp);if(t.state==State.Downloading){LinearProgressIndicator(progress={t.progress/100},modifier=Modifier.fillMaxWidth().padding(vertical=10.dp),color=Purple);Text((if(t.speed<=0)"0 MB/s"else"%.2f MB/s".format(t.speed/1_000_000))+" · ETA ${t.eta}",fontSize=12.sp,color=Muted)};Row{when(t.state){State.Completed->TextButton(onClick=open){Text(uiText("開啟","Open"))};State.Paused,State.Failed->TextButton(onClick=resume){Text(uiText("繼續","Resume"))};State.Downloading,State.Queued,State.Analyzing,State.RetryWait->TextButton(onClick=pause){Text(uiText("暫停","Pause"))};else->Unit};if(t.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.RetryWait))TextButton(onClick=cancel){Text(uiText("取消","Cancel"))};if(t.state==State.Failed)TextButton(onClick=diagnose){Text(uiText("診斷","Diagnostics"))}}}}}
fun status(t:TaskItem)=if(UiLanguage.value=="en")when(t.state){State.PendingChoice->"Waiting for choice";State.Queued->"Queued";State.Analyzing->"Analyzing";State.Downloading->"Downloading ${t.progress.toInt()}%";State.Processing->"Processing";State.Paused->"Paused";State.Completed->"Completed";State.Failed->"Failed";State.Cancelled->"Removed";State.RetryWait->"Retry ${t.retry}"}else when(t.state){State.PendingChoice->"等待選擇";State.Queued->"排隊中";State.Analyzing->"分析中";State.Downloading->"下載中 ${t.progress.toInt()}%";State.Processing->"處理中";State.Paused->"已暫停";State.Completed->"已完成";State.Cancelled->"已取消";State.Failed->"下載失敗";State.RetryWait->"連線異常，將在 ${((t.retryAt-System.currentTimeMillis())/1000).coerceAtLeast(0)} 秒後重試 (${t.retry}/3)"}
@Composable fun TagDialog(engine:Engine,ids:Set<String>,close:()->Unit){val scope=rememberCoroutineScope();var delta by remember{mutableStateOf(mapOf<String,String>())};var raw by remember{mutableStateOf("{}")};var error by remember{mutableStateOf<String?>(null)};var saving by remember{mutableStateOf(false)};var cover by remember{mutableStateOf<ByteArray?>(null)};val initial=remember(ids){ids.first().let(engine::get)}
    val pick=androidx.activity.compose.rememberLauncherForActivityResult(ActivityResultContracts.GetContent()){uri->if(uri!=null)scope.launch{try{cover=withContext(Dispatchers.IO){engine.context.contentResolver.openInputStream(uri)?.use{it.readBytes()}}}catch(e:Exception){error=e.message}}}
    val titleError=delta["TIT2"]?.let(Rules::titleError)
    AlertDialog(onDismissRequest={if(!saving)close()},title={Text("編輯 ${ids.size} 首 MP3")},text={Column(Modifier.heightIn(max=480.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(10.dp)){Text(uiText("只儲存已修改欄位；音軌編號會統一填入固定值。","Only changed fields are saved. Track number applies the same value to all selected files."),fontSize=12.sp);listOf("TIT2" to "Title · 歌曲名","TPE1" to "Artist · 歌手","TALB" to "Album · 專輯","TRCK" to "Track · 音軌","TCON" to "Genre · 類型","TYER" to "Year · 年份").forEach{(id,label)->OutlinedTextField(value=delta[id]?:if(ids.size==1)when(id){"TIT2"->initial.title;"TPE1"->initial.artist;"TALB"->initial.album;else->""}else"",onValueChange={delta=delta+(id to it)},label={Text(label)},isError=id=="TIT2"&&titleError!=null,supportingText={if(id=="TIT2"&&titleError!=null)Text(titleError,color=MaterialTheme.colorScheme.error)},singleLine=true)};OutlinedTextField(raw,{raw=it},label={Text(uiText("進階 Raw Frames：Base64 JSON","Advanced raw frames: Base64 JSON"))});OutlinedButton(onClick={pick.launch("image/*")}){Text(if(cover==null)"選擇封面"else"✓ 封面已選取")};TextButton(onClick={val clip=engine.context.getSystemService(android.content.ClipboardManager::class.java).primaryClip;val uri=if(clip!=null&&clip.itemCount>0)clip.getItemAt(0).uri else null;if(uri!=null)scope.launch{try{cover=withContext(Dispatchers.IO){engine.context.contentResolver.openInputStream(uri)?.use{it.readBytes()}}}catch(e:Exception){error=e.message}}else error="剪貼簿未包含圖片檔案"}){Text(uiText("貼上剪貼簿封面","Paste cover from clipboard"))};error?.let{Text(it,color=MaterialTheme.colorScheme.error)} }},confirmButton={TextButton(enabled=titleError==null&&!saving,onClick={saving=true;scope.launch{try{val json=org.json.JSONObject(raw);val bytes=json.keys().asSequence().associateWith{android.util.Base64.decode(json.getString(it),android.util.Base64.DEFAULT)};TagEditor(engine).apply(ids,delta,bytes,cover);close()}catch(e:Exception){error=e.message}finally{saving=false}}}){Text(uiText("儲存","Save"))}},dismissButton={TextButton(enabled=!saving,onClick=close){Text(uiText("取消","Cancel"))}})
}
