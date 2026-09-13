package io.hkmario.omni
import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.Uri
import com.yausername.youtubedl_android.YoutubeDL
import com.yausername.youtubedl_android.YoutubeDLRequest
import com.yausername.ffmpeg.FFmpeg
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.serialization.encodeToString
import org.json.JSONObject
import java.io.File
import java.util.concurrent.ConcurrentHashMap

data class SpaceQuestion(val taskId: String,val answer: CompletableDeferred<Pair<Boolean,Boolean>>)
class Engine(val context: Context) {
    val db=Database(context);private val shared=context.getSharedPreferences("preferences",0)
    val prefs=MutableStateFlow(runCatching{db.json.decodeFromString<Prefs>(shared.getString("value",null)?:"{}")}.getOrDefault(Prefs()).let{it.copy(height=it.height.coerceAtMost(2160))})
    val grants=SessionGrants();val storage=Storage(context,grants)
    val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
    private val mutable=MutableStateFlow(db.tasks().map{if(it.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.Processing,State.RetryWait))it.copy(state=State.Paused,speed=0.0,eta=0)else it})
    val tasks=mutable.asStateFlow();val groups=MutableStateFlow(db.groups());val ready=CompletableDeferred<Unit>();val initError=MutableStateFlow<String?>(null)
    val spaceQuestions=MutableStateFlow<List<SpaceQuestion>>(emptyList())
    private val active=ConcurrentHashMap<String,Job>();@Volatile var serviceRunning=false
    init { mutable.value.forEach(db::save);scope.launch{try{YoutubeDL.getInstance().init(context);FFmpeg.getInstance().init(context);ready.complete(Unit)}catch(e: Exception){initError.value="下載引擎初始化失敗：${e.message}";ready.completeExceptionally(e)}} }
    @Synchronized fun update(id: String,transform: (TaskItem)->TaskItem) { mutable.value=mutable.value.map{if(it.id==id)transform(it).also(db::save)else it} }
    @Synchronized private fun add(t: TaskItem) { db.save(t);mutable.value=mutable.value+t }
    fun get(id: String)=tasks.value.first{it.id==id}
    fun save(p: Prefs) { require(p.concurrency in 1..5&&p.kbps in listOf(128,192,256,320)&&p.height in listOf(0,720,1080,1440,2160));prefs.value=p;shared.edit().putString("value",db.json.encodeToString(p)).apply() }
    fun compound(url: String): Boolean { val uri=Uri.parse(url);return uri.getQueryParameter("v")!=null&&uri.getQueryParameter("list")!=null }
    fun enqueue(url: String,mode: String): String { require(Rules.validUrl(url)){"請輸入有效 HTTPS 影片網址"};require(mode in listOf("mp3","mp4"));val p=prefs.value;var t=TaskItem(url=url,mode=mode,height=p.height,kbps=p.kbps,targetTree=p.treeFor(mode),state=if(compound(url))State.PendingChoice else State.Queued);val uri=Uri.parse(url);if(uri.path=="/playlist"&&uri.getQueryParameter("list")!=null){val g=PlaylistGroup(title="正在載入播放清單");synchronized(this){groups.value=groups.value+g};db.save(g);t=t.copy(groupId=g.id,groupRoot=true)};add(t);return t.id }
    fun choose(id: String,playlist: Boolean) { if(get(id).state!=State.PendingChoice)return
        if(playlist){val group=PlaylistGroup(title="正在載入播放清單");groups.value=groups.value+group;db.save(group);update(id){it.copy(state=State.Queued,groupId=group.id,groupRoot=true)}}else update(id){it.copy(state=State.Queued)} }
    private fun unrestricted(): Boolean { val cm=context.getSystemService(ConnectivityManager::class.java);val n=cm.getNetworkCapabilities(cm.activeNetwork)?:return false;return n.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED)&&n.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)&&!cm.isActiveNetworkMetered }
    fun permitted(id: String)=!prefs.value.wifiOnly||unrestricted()||grants.mayUseCellular(id)
    fun needsPermission()=tasks.value.firstOrNull{it.state==State.Queued&&!permitted(it.id)}
    suspend fun pump() {
        if(!serviceRunning)return
        active.entries.removeIf{it.value.isCompleted}
        tasks.value.filter{it.state==State.Queued&&permitted(it.id)}.take((prefs.value.concurrency-active.size).coerceAtLeast(0)).forEach{ t ->
            update(t.id){it.copy(state=State.Analyzing)};val job=scope.launch(start=CoroutineStart.LAZY){run(t.id)};active[t.id]=job;job.start()
        }
        groups.value.filter{it.discoveryComplete&&!it.notified}.forEach { g ->val children=tasks.value.filter{it.groupId==g.id&&!it.groupRoot};if(children.all{it.state in listOf(State.Completed,State.Failed,State.Cancelled)}){val failures=children.count{it.state==State.Failed}+g.discoveryFailures;saveGroup(g.copy(notified=true));Notices.show(context,"播放清單完成：${g.title}"+if(failures>0)"（含 $failures 個失敗項目）" else "") } }
    }
    private fun saveGroup(g: PlaylistGroup) { synchronized(this){groups.value=groups.value.map{if(it.id==g.id)g else it};db.save(g)} }
    suspend fun analyze(url: String,processId: String="analysis-${java.util.UUID.randomUUID()}"): JSONObject { require(Rules.validUrl(url)){"請輸入有效 HTTPS 影片網址"};ready.await();val request=YoutubeDLRequest(url);request.addOption("--ignore-config");request.addOption("--no-playlist");request.addOption("--dump-single-json");request.addOption("--skip-download");return runInterruptible(Dispatchers.IO){JSONObject(YoutubeDL.getInstance().execute(request,processId).out)} }
    private suspend fun run(id: String) {
        val work=File(context.filesDir,"work/$id").apply{mkdirs()}
        try {
            ready.await();if(!permitted(id)){update(id){it.copy(state=State.Paused)};return}
            val initial=get(id);if(initial.groupRoot){discover(initial);return}
            val info=analyze(initial.url,id);val title=info.optString("track").takeIf{it.isNotBlank()&&it!="null"} ?: info.optString("title",initial.title)
            update(id){it.copy(title=if(prefs.value.cleanTitle)Rules.cleanTitle(title)else title,artist=info.optString("artist","").takeUnless{it=="null"}?:"",album=info.optString("album","").takeUnless{it=="null"}?:"",thumbnail=info.optString("thumbnail").takeIf{it.startsWith("https://")})}
            for(attempt in 0..3) {
                try { download(id,work);break }
                catch(e: CancellationException){throw e}
                catch(e: Exception){if(attempt==3||get(id).state in listOf(State.Paused,State.Cancelled)||e.message?.contains("403")==true)throw e;val seconds=1 shl(attempt+1);update(id){it.copy(state=State.RetryWait,retry=attempt+1,retryAt=System.currentTimeMillis()+seconds*1000,speed=0.0,eta=0)};delay(seconds*1000L)}
            }
            currentCoroutineContext().ensureActive();update(id){it.copy(state=State.Processing,speed=0.0,eta=0)};val task=get(id)
            var cover: Art?=null
            val file=work.listFiles()?.firstOrNull{it.name=="media.${task.mode}"} ?: error("找不到下載輸出檔案")
            if(task.mode=="mp3") {
                val tag=Id3.read(file);tag.setText("TIT2",task.title);tag.setText("TPE1",task.artist);tag.setText("TALB",task.album)
                val arts=mutableListOf<Art>();if(prefs.value.musicBrainz)Metadata.find(task.title,task.artist)?.let(arts::add)
                task.thumbnail?.let{url->try{Metadata.fetch(url,"Video thumbnail",if(arts.isEmpty())3 else 0)?.let(arts::add)}catch(e: CancellationException){throw e}catch(_:Exception){}}
                if(arts.isNotEmpty()){tag.covers(arts);cover=arts.first()};val temp=File(work,"tagged.mp3");tag.write(file,temp);java.nio.file.Files.move(temp.toPath(),file.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING)
            }
            val group=groups.value.firstOrNull{it.id==task.groupId}
            val published=storage.publish(file,task.targetTree ?: prefs.value.treeFor(task.mode),Rules.safeName(task.title)+".${task.mode}",group?.let{Rules.safeName(it.title)}){askSpace(id)}
            if(cover!=null)try{storage.coverFor(published.path,toJpeg(cover!!.bytes),published.parent)}catch(_:Exception){Notices.show(context,"封面更新完成（含 1 個目錄寫入失敗）")}
            update(id){it.copy(state=State.Completed,path=published.path,directory=published.parent,progress=100f,completedAt=System.currentTimeMillis())};if(task.groupId==null)Notices.show(context,"下載完成：${task.title}")
        } catch(e: CancellationException){if(get(id).state!=State.Cancelled)update(id){it.copy(state=State.Paused,speed=0.0,eta=0)}}
        catch(e: Exception){if(get(id).state !in listOf(State.Cancelled,State.Paused)){val raw=redact(e.message?:"未知錯誤");update(id){it.copy(state=State.Failed,error=diagnose(raw),stderr=raw.take(32000),speed=0.0,eta=0)};if(get(id).groupRoot){groups.value.firstOrNull{it.id==get(id).groupId}?.let{saveGroup(it.copy(discoveryComplete=true,discoveryFailures=it.discoveryFailures+1))}}else if(get(id).groupId==null)Notices.show(context,"下載失敗：${get(id).title}")}}
    }
    private suspend fun download(id: String,work: File) = coroutineScope {
        update(id){it.copy(state=State.Downloading)};val t=get(id);val request=YoutubeDLRequest(t.url)
        request.addOption("--ignore-config");request.addOption("--no-playlist");request.addOption("--continue");request.addOption("--newline");request.addOption("--progress-delta","0.5");request.addOption("-o",File(work,"media.%(ext)s").absolutePath)
        if(t.mode=="mp3"){request.addOption("-f","bestaudio/best");request.addOption("-x");request.addOption("--audio-format","mp3");request.addOption("--audio-quality","${t.kbps}k");request.addOption("--postprocessor-args","ExtractAudio+ffmpeg_o:-ar 44100");request.addOption("--embed-metadata")}
        else {val cap="[height<=?${if(t.height>0)t.height.coerceAtMost(2160)else 2160}]";request.addOption("-f","bv*$cap[ext=mp4]+ba[ext=m4a]/b$cap[ext=mp4]/bv*$cap+ba/b$cap");request.addOption("--merge-output-format","mp4");request.addOption("--remux-video","mp4")}
        val ema=Ema();var previous=android.os.SystemClock.elapsedRealtime()
        val sampler=launch { while(isActive){delay(500);val now=android.os.SystemClock.elapsedRealtime();val bytes=work.listFiles()?.filter{it.extension in listOf("part","mp4","m4a","webm","mp3")}?.sumOf{it.length()}?:0;val task=get(id);val estimate=if(task.progress>0)(bytes*100/task.progress).toLong()else null;val(speed,eta)=ema.sample(bytes,estimate,(now-previous)/1000.0,task.state==State.Downloading);previous=now;update(id){it.copy(bytes=bytes,total=estimate,speed=speed,eta=eta)}} }
        try { runInterruptible(Dispatchers.IO){YoutubeDL.getInstance().execute(request,id){progress,_,_->update(id){it.copy(progress=progress.coerceIn(0f,100f))}}} }
        finally {sampler.cancelAndJoin()}
    }
    private suspend fun discover(root: TaskItem) {
        val request=YoutubeDLRequest(root.url);request.addOption("--flat-playlist");request.addOption("--dump-single-json");request.addOption("--yes-playlist");request.addOption("--skip-download")
        val json=runInterruptible(Dispatchers.IO){JSONObject(YoutubeDL.getInstance().execute(request,root.id).out)};var group=groups.value.first{it.id==root.groupId}.copy(title=json.optString("title","播放清單"));saveGroup(group)
        val entries=json.getJSONArray("entries");val seen=mutableSetOf<String>();var failures=0
        for(i in 0 until entries.length()){currentCoroutineContext().ensureActive();val e=entries.optJSONObject(i);val video=e?.optString("id");if(video.isNullOrBlank()){failures++;continue};if(!seen.add(video))continue;val url=e.optString("url").takeIf{Rules.validUrl(it)}?:"https://www.youtube.com/watch?v=$video";val child=TaskItem(url=url,title=e.optString("title",url),mode=root.mode,height=root.height,kbps=root.kbps,targetTree=root.targetTree,groupId=root.groupId);if(grants.mayUseCellular(root.id))grants.tasks.add(child.id);add(child)}
        saveGroup(group.copy(discoveryComplete=true,discoveryFailures=failures));update(root.id){it.copy(state=State.Completed)}
    }
    private suspend fun askSpace(id: String): Pair<Boolean,Boolean> {val q=SpaceQuestion(id,CompletableDeferred());synchronized(this){spaceQuestions.value=spaceQuestions.value+q};Notices.show(context,"請開啟 App 確認目標儲存空間");return try{q.answer.await()}finally{synchronized(this){spaceQuestions.value=spaceQuestions.value-q}}}
    suspend fun pause(id: String) {val t=get(id);if(t.state !in listOf(State.Queued,State.Analyzing,State.Downloading,State.RetryWait))return;update(id){it.copy(state=State.Paused,speed=0.0,eta=0)};YoutubeDL.getInstance().destroyProcessById(id);active[id]?.cancelAndJoin()}
    suspend fun cancel(ids: Set<String>) {for(id in ids){val t=get(id);if(t.state !in listOf(State.Queued,State.Analyzing,State.Downloading,State.RetryWait))continue;update(id){it.copy(state=State.Cancelled,speed=0.0,eta=0)};YoutubeDL.getInstance().destroyProcessById(id);active[id]?.cancelAndJoin();val root=File(context.filesDir,"work").canonicalFile;val dir=File(root,id).canonicalFile;require(dir.parentFile==root);dir.deleteRecursively();grants.tasks.remove(id)}}
    fun resume(id: String){if(get(id).state in listOf(State.Paused,State.Failed)&&active[id]?.isActive!=true)update(id){it.copy(state=State.Queued,error=null,retry=0)}}
    suspend fun onNetworkChanged() {if(!prefs.value.wifiOnly)return;val blocked=tasks.value.filter{it.state in listOf(State.Analyzing,State.Downloading,State.RetryWait)&&!permitted(it.id)};blocked.forEach{pause(it.id)};if(blocked.isNotEmpty())Notices.show(context,"已切換為行動數據或受限網路，下載已自動暫停")}
    fun clearHistory(ids: Set<String>){db.hide(ids);synchronized(this){mutable.value=mutable.value.map{if(it.id in ids)it.copy(history=false)else it}}}
    companion object {
        fun redact(s: String)=s.replace(Regex("https?://\\S+|(?i)(cookie|authorization|token)\\s*[:=].*"),"[已隱藏敏感資料]")
        fun diagnose(s: String)=if(s.contains("403")||s.contains("Sign in",true))"網站要求登入或拒絕存取。Android 目前沒有瀏覽器登入轉發；請使用 Windows 擴充功能處理需登入的影片。"else if(s.contains("space",true))"儲存空間不足，請釋放空間或選擇另一個目錄。"else "下載失敗，請檢查網路及影片可用性；原始診斷可匯出分享。"
        fun toJpeg(bytes: ByteArray): ByteArray {val bitmap=android.graphics.BitmapFactory.decodeByteArray(bytes,0,bytes.size)?:error("無效封面");val out=java.io.ByteArrayOutputStream();bitmap.compress(android.graphics.Bitmap.CompressFormat.JPEG,95,out);bitmap.recycle();return out.toByteArray()}
    }
}
