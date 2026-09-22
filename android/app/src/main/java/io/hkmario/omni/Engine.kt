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

data class DuplicateQuestion(val name:String,val answer:CompletableDeferred<String>)
data class SpaceQuestion(val taskId: String,val answer: CompletableDeferred<Pair<Boolean,Boolean>>)
class Engine(val context: Context) {
    val db=Database(context);private val shared=context.getSharedPreferences("preferences",0)
    val prefs=MutableStateFlow(runCatching{db.json.decodeFromString<Prefs>(shared.getString("value",null)?:"{}")}.getOrDefault(Prefs()).let{it.copy(height=it.height.coerceAtMost(2160))})
    val grants=SessionGrants();val storage=Storage(context,grants)
    val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
    private val mutable=MutableStateFlow(db.tasks().map{if(it.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.Processing,State.RetryWait))it.copy(state=State.Paused,speed=0.0,eta=0)else it})
    val tasks=mutable.asStateFlow();val groups=MutableStateFlow(db.groups());val ready=CompletableDeferred<Unit>();val initError=MutableStateFlow<String?>(null)
    val musicQuestions=MutableStateFlow<List<MusicQuestion>>(emptyList())
    val duplicateQuestions=MutableStateFlow<List<DuplicateQuestion>>(emptyList())
    val completionEvents=kotlinx.coroutines.flow.MutableSharedFlow<TaskItem>(extraBufferCapacity=10)
    private var lastRate:Int?=null;private val networkPaused=mutableSetOf<String>();private var wasBusy=false
    val spaceQuestions=MutableStateFlow<List<SpaceQuestion>>(emptyList())
    private val active=ConcurrentHashMap<String,Job>();@Volatile var serviceRunning=false
    init { UiLanguage.value=prefs.value.language;mutable.value.forEach(db::save);scope.launch{try{YoutubeDL.getInstance().init(context);FFmpeg.getInstance().init(context);ready.complete(Unit)}catch(e: Exception){initError.value="下載引擎初始化失敗：${e.message}";ready.completeExceptionally(e)}} }
    val tagImports=java.util.concurrent.ConcurrentHashMap<String,TaskItem>()
    @Synchronized fun update(id: String,transform: (TaskItem)->TaskItem) { if(tagImports.containsKey(id)){tagImports[id]=transform(tagImports.getValue(id));return};mutable.value=mutable.value.map{if(it.id==id)transform(it).also(db::save)else it} }
    @Synchronized private fun add(t: TaskItem) { db.save(t);mutable.value=mutable.value+t }
    fun get(id: String)=tagImports[id]?:tasks.value.first{it.id==id}
    fun save(p: Prefs) { p.validate();prefs.value=p;UiLanguage.value=p.language;shared.edit().putString("value",db.json.encodeToString(p)).apply() }
    fun compound(url: String): Boolean { val uri=Uri.parse(url);return uri.getQueryParameter("v")!=null&&uri.getQueryParameter("list")!=null }
    fun enqueue(url: String,mode: String,outputFormat:String?=null): String { require(Rules.validUrl(url)){"請輸入有效 HTTPS 影片網址"};require(mode in listOf("mp3","mp4"));val p=prefs.value;require(outputFormat==null||outputFormat in if(mode=="mp3")listOf("mp3","m4a","flac","wav")else listOf("mp4","mkv","webm"));var t=TaskItem(url=url,mode=mode,outputFormat=outputFormat?:if(mode=="mp3")p.audioFormat else p.videoFormat,height=p.height,kbps=p.kbps,targetTree=p.treeFor(mode),state=if(compound(url))State.PendingChoice else State.Queued);val uri=Uri.parse(url);if(uri.path=="/playlist"&&uri.getQueryParameter("list")!=null){val g=PlaylistGroup(title="正在載入播放清單");synchronized(this){groups.value=groups.value+g};db.save(g);t=t.copy(groupId=g.id,groupRoot=true)};add(t);return t.id }
    fun choose(id: String,playlist: Boolean) { if(get(id).state!=State.PendingChoice)return
        if(playlist){val group=PlaylistGroup(title="正在載入播放清單");groups.value=groups.value+group;db.save(group);update(id){it.copy(state=State.Queued,groupId=group.id,groupRoot=true)}}else update(id){it.copy(state=State.Queued)} }
    private fun unrestricted(): Boolean { val cm=context.getSystemService(ConnectivityManager::class.java);val n=cm.getNetworkCapabilities(cm.activeNetwork)?:return false;return n.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED)&&n.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)&&!cm.isActiveNetworkMetered }
    fun permitted(id:String):Boolean {val p=prefs.value;val cm=context.getSystemService(ConnectivityManager::class.java);val caps=cm.getNetworkCapabilities(cm.activeNetwork)?:return false;if(!caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED))return false
        if(p.allowedNetwork=="ethernet"&&!caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET))return false
        return (!(p.wifiOnly||p.allowedNetwork=="wifi")||unrestricted()||grants.mayUseCellular(id))}
    fun needsPermission():TaskItem? {val p=prefs.value;val cm=context.getSystemService(ConnectivityManager::class.java);val caps=cm.getNetworkCapabilities(cm.activeNetwork)?:return null;if(p.allowedNetwork=="ethernet"||!caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED))return null;return tasks.value.firstOrNull{it.state==State.Queued&&!permitted(it.id)}}
    suspend fun pump() {
        if(!serviceRunning)return
        val hasWork=tasks.value.any{it.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.Processing,State.RetryWait)};if(wasBusy&&!hasWork&&tasks.value.none{it.state in listOf(State.Paused,State.PendingChoice)}&&prefs.value.notifyAll)Notices.show(context,"全部任務已結束 / All tasks finished");wasBusy=hasWork
        val rate=prefs.value.effectiveLimit();if(lastRate!=null&&lastRate!=rate){tasks.value.filter{it.state==State.Downloading}.forEach{pause(it.id);resume(it.id)}};lastRate=rate
        active.entries.removeIf{it.value.isCompleted}
        tasks.value.filter{it.state==State.Queued&&permitted(it.id)}.take((prefs.value.concurrency-active.size).coerceAtLeast(0)).forEach{ t ->
            update(t.id){it.copy(state=State.Analyzing)};val job=scope.launch(start=CoroutineStart.LAZY){run(t.id)};active[t.id]=job;job.start()
        }
        groups.value.filter{it.discoveryComplete&&!it.notified}.forEach { g ->val children=tasks.value.filter{it.groupId==g.id&&!it.groupRoot};if(children.all{it.state in listOf(State.Completed,State.Failed,State.Cancelled)}){val failures=children.count{it.state==State.Failed}+g.discoveryFailures;saveGroup(g.copy(notified=true));if(prefs.value.notifyAll)Notices.show(context,"播放清單完成：${g.title}"+if(failures>0)"（含 $failures 個失敗項目）" else "") } }
    }
    private fun saveGroup(g: PlaylistGroup) { synchronized(this){groups.value=groups.value.map{if(it.id==g.id)g else it};db.save(g)} }
    suspend fun findSourceArtwork(url:String,info:JSONObject):Art? {
        AlbumArtwork.source(info)?.let{return it};val uri=android.net.Uri.parse(url);if(uri.host !in listOf("youtube.com","www.youtube.com","m.youtube.com","youtu.be"))return null
        val id=if(uri.host=="youtu.be")uri.lastPathSegment else uri.getQueryParameter("v");if(id==null||!id.matches(Regex("[A-Za-z0-9_-]{11}")))return null
        return try{withTimeout(25000){AlbumArtwork.source(analyze("https://music.youtube.com/watch?v=$id"))}}catch(e:TimeoutCancellationException){null}catch(e:CancellationException){throw e}catch(_:Exception){null}
    }
    suspend fun analyze(url: String,processId: String="analysis-${java.util.UUID.randomUUID()}"): JSONObject { require(Rules.validUrl(url)){"請輸入有效 HTTPS 影片網址"};ready.await();val request=YoutubeDLRequest(url);request.addOption("--ignore-config");Options.apply(request,networkOptions());val jar=applyCookies(request,url,processId);request.addOption("--no-playlist");request.addOption("--dump-single-json");request.addOption("--skip-download");return try{runInterruptible(Dispatchers.IO){JSONObject(YoutubeDL.getInstance().execute(request,processId).out)}}finally{jar?.delete()} }
    private suspend fun run(id: String) {
        val work=workFor(get(id)).apply{mkdirs()};update(id){it.copy(workPath=work.absolutePath)}
        try {
            ready.await();if(!permitted(id)){update(id){it.copy(state=State.Paused)};return}
            val initial=get(id);if(initial.groupRoot){discover(initial);return}
            val info=analyze(initial.url,id);val title=info.optString("track").takeIf{it.isNotBlank()&&it!="null"} ?: info.optString("title",initial.title)
            update(id){it.copy(duration=info.optDouble("duration").takeIf{it.isFinite()},title=if(prefs.value.cleanTitle)Rules.cleanTitle(title)else title,artist=info.optString("artist","").takeUnless{it=="null"}?:"",album=info.optString("album","").takeUnless{it=="null"}?:"",thumbnail=info.optString("thumbnail").takeIf{it.startsWith("https://")})}
            for(attempt in 0..(if(prefs.value.autoRetry)prefs.value.retryCount else 0)) {
                try { download(id,work);break }
                catch(e: CancellationException){throw e}
                catch(e: Exception){if(attempt>=(if(prefs.value.autoRetry)prefs.value.retryCount else 0)||get(id).state in listOf(State.Paused,State.Cancelled)||e.message?.contains("403")==true)throw e;val seconds=(prefs.value.retrySeconds*Math.pow(2.0,attempt.toDouble())).toInt().coerceAtMost(3600);update(id){it.copy(state=State.RetryWait,retry=attempt+1,retryAt=System.currentTimeMillis()+seconds*1000,speed=0.0,eta=0)};delay(seconds*1000L)}
            }
            currentCoroutineContext().ensureActive();update(id){it.copy(state=State.Processing,speed=0.0,eta=0)};var task=get(id)
            var cover: Art?=null
            val file=work.listFiles()?.firstOrNull{it.name=="media.${task.extension}"} ?: error("找不到下載輸出檔案")
            if(task.extension=="mp3") {
                val tag=Id3.read(file);val arts=mutableListOf<Art>()
                if(prefs.value.musicBrainz){update(id){it.copy(metadataStatus="MusicBrainz：正在查詢歌曲及專輯封面…")};var result=MusicRecognition.recognize(context,file.absolutePath,prefs.value.acoustIdClientKey,task.title,task.artist,info.optDouble("duration").takeIf{it.isFinite()&&it>0});if(result.choices.isNotEmpty()){val question=MusicQuestion(result.choices);musicQuestions.value=musicQuestions.value+question;try{question.answer.await()?.let{try{result=MusicRecognition.recording(it.recordingId,it.releaseId)}catch(e:kotlinx.coroutines.CancellationException){throw e}catch(_:Exception){result=MusicResult("MusicBrainz：服務暫時無法使用，已保留來源資料")}}}finally{musicQuestions.value=musicQuestions.value-question}};if(!task.isUserEdited&&prefs.value.keepMetadata)result.tags.forEach{(key,value)->tag.setText(if(key=="TYER"&&tag.version==4)"TDRC"else key,value)};update(id){it.copy(metadataStatus=result.status,title=if(it.isUserEdited)it.title else result.title?:it.title,artist=if(it.isUserEdited)it.artist else result.artist?:it.artist,album=if(it.isUserEdited)it.album else result.album?:it.album)};result.cover?.let(arts::add)}else update(id){it.copy(metadataStatus="MusicBrainz：已在設定關閉")}
                task=get(id);if(prefs.value.keepMetadata){tag.setText("TIT2",task.title);tag.setText("TPE1",task.artist);tag.setText("TALB",task.album)}
                arts.removeAll{!AlbumArtwork.accept(it.bytes)}
                findSourceArtwork(task.url,info)?.let{source->for(i in arts.indices)arts[i]=arts[i].copy(type=0);arts.add(0,source)}
                if(arts.isEmpty()&&!prefs.value.musicBrainz)Metadata.lookup(task.title,task.artist,task.duration).cover?.let(arts::add)
                if(prefs.value.embedThumbnail)tag.covers(arts);cover=arts.firstOrNull()
                update(id){it.copy(metadataStatus=it.metadataStatus+if(cover!=null)" · 已取得近正方形專輯封面"else" · 未找到專輯封面")}
                val temp=File(work,"tagged.mp3");tag.write(file,temp);java.nio.file.Files.move(temp.toPath(),file.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING)
            }
            val group=groups.value.firstOrNull{it.id==task.groupId}
            val finalBytes=file.length();val published=storage.publish(file,task.targetTree ?: prefs.value.treeFor(task.mode),Options.fileStem(task,prefs.value)+".${task.extension}",group?.let{Rules.safeName(it.title)},prefs.value.duplicateAction,{name->askDuplicate(name)}){askSpace(id)}
            if(cover!=null&&prefs.value.keepThumbnail)try{storage.coverFor(published.path,toJpeg(cover!!.bytes),published.parent)}catch(_:Exception){Notices.show(context,"封面更新完成（含 1 個目錄寫入失敗）")}
            update(id){it.copy(bytes=finalBytes,total=finalBytes,state=State.Completed,path=published.path,directory=published.parent,progress=100f,completedAt=System.currentTimeMillis())};if(task.groupId==null&&prefs.value.notifyComplete)Notices.show(context,"下載完成：${task.title}");completionEvents.tryEmit(get(id));playCompletionSound(context,prefs.value);val sideFailures=storage.sidecars(work,published);if(sideFailures>0)update(id){it.copy(error="Media saved; $sideFailures sidecars could not be saved")}
        } catch(e:DuplicateSkipped){update(id){it.copy(state=State.Cancelled,error="Duplicate skipped")}} catch(e: CancellationException){if(get(id).state!=State.Cancelled)update(id){it.copy(state=State.Paused,speed=0.0,eta=0)}}
        catch(e: Exception){if(get(id).state !in listOf(State.Cancelled,State.Paused)){val raw=redact(e.message?:"未知錯誤");update(id){it.copy(state=State.Failed,error=diagnose(raw),stderr=raw.take(32000),speed=0.0,eta=0)};if(get(id).groupRoot){groups.value.firstOrNull{it.id==get(id).groupId}?.let{saveGroup(it.copy(discoveryComplete=true,discoveryFailures=it.discoveryFailures+1))}}else if(get(id).groupId==null&&prefs.value.notifyFailure)Notices.show(context,"下載失敗：${get(id).title}")}}
        finally {val t=get(id);if(t.state==State.Completed&&t.error==null||t.state==State.Cancelled||t.state==State.Failed&&prefs.value.cleanFailed)runCatching{cleanWork(t)}}
    }
    private suspend fun download(id: String,work: File) = coroutineScope {
        update(id){it.copy(state=State.Downloading)};val t=get(id);val request=YoutubeDLRequest(t.url)
        request.addOption("--ignore-config");request.addOption("--no-playlist");request.addOption("--continue");request.addOption("--newline");request.addOption("--progress-delta","0.5");request.addOption("-o",File(work,"media.%(ext)s").absolutePath)
        Options.apply(request,networkOptions());Options.apply(request,Options.format(t,prefs.value));val jar=applyCookies(request,t.url,id)
        val ema=Ema();var previous=android.os.SystemClock.elapsedRealtime()
        val sampler=launch { while(isActive){delay(500);val now=android.os.SystemClock.elapsedRealtime();val bytes=work.listFiles()?.filter{it.extension in listOf("part","mp4","m4a","webm","mp3")}?.sumOf{it.length()}?:0;val task=get(id);val estimate=if(task.progress>0)(bytes*100/task.progress).toLong()else null;val(speed,eta)=ema.sample(bytes,estimate,(now-previous)/1000.0,task.state==State.Downloading);previous=now;update(id){it.copy(bytes=bytes,total=estimate,speed=speed,eta=eta)}} }
        try { runInterruptible(Dispatchers.IO){YoutubeDL.getInstance().execute(request,id){progress,_,_->update(id){it.copy(progress=progress.coerceIn(0f,100f))}}} }
        finally {sampler.cancelAndJoin();jar?.delete()}
    }
    private suspend fun discover(root: TaskItem) {
        val request=YoutubeDLRequest(root.url);Options.apply(request,networkOptions());val jar=applyCookies(request,root.url,root.id);request.addOption("--flat-playlist");request.addOption("--dump-single-json");request.addOption("--yes-playlist");request.addOption("--skip-download")
        val json=try{runInterruptible(Dispatchers.IO){JSONObject(YoutubeDL.getInstance().execute(request,root.id).out)}}finally{jar?.delete()};var group=groups.value.first{it.id==root.groupId}.copy(title=json.optString("title","播放清單"));saveGroup(group)
        val entries=json.getJSONArray("entries");val seen=mutableSetOf<String>();var failures=0
        for(i in 0 until entries.length()){currentCoroutineContext().ensureActive();val e=entries.optJSONObject(i);val video=e?.optString("id");if(video.isNullOrBlank()){failures++;continue};if(!seen.add(video))continue;val url=e.optString("url").takeIf{Rules.validUrl(it)}?:"https://www.youtube.com/watch?v=$video";val child=TaskItem(url=url,title=e.optString("title",url),mode=root.mode,outputFormat=root.extension,height=root.height,kbps=root.kbps,targetTree=root.targetTree,groupId=root.groupId);if(grants.mayUseCellular(root.id))grants.tasks.add(child.id);add(child)}
        saveGroup(group.copy(discoveryComplete=true,discoveryFailures=failures));update(root.id){it.copy(state=State.Completed)}
    }
    private suspend fun askSpace(id: String): Pair<Boolean,Boolean> {val q=SpaceQuestion(id,CompletableDeferred());synchronized(this){spaceQuestions.value=spaceQuestions.value+q};Notices.show(context,"請開啟 App 確認目標儲存空間");return try{q.answer.await()}finally{synchronized(this){spaceQuestions.value=spaceQuestions.value-q}}}
    suspend fun pause(id: String) {val t=get(id);if(t.state !in listOf(State.Queued,State.Analyzing,State.Downloading,State.RetryWait))return;update(id){it.copy(state=State.Paused,speed=0.0,eta=0)};YoutubeDL.getInstance().destroyProcessById(id);active[id]?.cancelAndJoin()}
    suspend fun cancel(ids: Set<String>) {for(id in ids){val t=get(id);if(t.state in listOf(State.Completed,State.Cancelled))continue;update(id){it.copy(state=State.Cancelled,speed=0.0,eta=0)};YoutubeDL.getInstance().destroyProcessById(id);active[id]?.cancelAndJoin();cleanWork(t);grants.tasks.remove(id)}}
    fun resume(id: String){if(get(id).state in listOf(State.Paused,State.Failed)&&active[id]?.isActive!=true)update(id){it.copy(state=State.Queued,error=null,retry=0)}}
    suspend fun onNetworkChanged() {val blocked=tasks.value.filter{it.state in listOf(State.Analyzing,State.Downloading,State.RetryWait)&&!permitted(it.id)};blocked.forEach{pause(it.id)};if(blocked.isNotEmpty())Notices.show(context,"已切換為行動數據或受限網路，下載已自動暫停")}
    suspend fun deleteDownloaded(id:String)=withContext(Dispatchers.IO){val t=get(id);require(t.state==State.Completed&&!t.groupRoot&&t.history){"請選取一個已下載檔案"};val path=t.path?:error("找不到檔案");val deleted=if(path.startsWith("content://"))androidx.documentfile.provider.DocumentFile.fromSingleUri(context,Uri.parse(path))?.delete()==true else File(path).delete();check(deleted){"未能刪除檔案，紀錄已保留"};clearHistory(tasks.value.filter{it.path==path&&it.state==State.Completed}.map{it.id}.toSet())}
    fun clearHistory(ids: Set<String>){db.hide(ids);synchronized(this){mutable.value=mutable.value.map{if(it.id in ids)it.copy(history=false)else it}}}
    private fun networkOptions():List<Pair<String,String?>> {
        val p=prefs.value;val args=Options.network(p).toMutableList()
        if(p.proxyMode=="system"){val proxy=context.getSystemService(ConnectivityManager::class.java).defaultProxy
            if(proxy!=null){require(proxy.pacFileUrl==Uri.EMPTY){"PAC proxy requires a custom proxy URL in Network settings"};if(!proxy.host.isNullOrBlank()&&proxy.port>0)args.add("--proxy" to "http://${proxy.host}:${proxy.port}")}
        };return args
    }
    private fun applyCookies(request:YoutubeDLRequest,url:String,id:String):File? {
        val p=prefs.value;request.addOption("--cache-dir",File(context.cacheDir,"network").absolutePath)
        if(p.cookieFile.isBlank()||java.net.URI(url).host !in listOf("youtube.com","www.youtube.com","m.youtube.com","youtu.be"))return null
        require(Regex("^[a-zA-Z0-9-]+$").matches(id));val source=File(p.cookieFile);require(source.isFile){"Cookie file missing"}
        val dir=File(context.cacheDir,"auth").apply{mkdirs()};val jar=File(dir,"$id.txt");source.copyTo(jar,true);request.addOption("--cookies",jar.absolutePath);return jar
    }

    private fun workFor(t:TaskItem)=t.workPath?.let(::File)?:File(if(prefs.value.tempDirectory=="external")context.getExternalFilesDir(null)?:context.filesDir else context.filesDir,"work/${t.id}")
    private fun cleanWork(t:TaskItem){val dir=workFor(t).canonicalFile;val roots=listOf(File(context.filesDir,"work").canonicalFile,File(context.getExternalFilesDir(null)?:context.filesDir,"work").canonicalFile);require(dir.parentFile in roots&&dir.name==t.id);dir.deleteRecursively()}
    private suspend fun askDuplicate(name:String):String{val q=DuplicateQuestion(name,CompletableDeferred());synchronized(this){duplicateQuestions.value=duplicateQuestions.value+q};Notices.show(context,"請開啟 App 處理重複檔案");return try{q.answer.await()}finally{synchronized(this){duplicateQuestions.value=duplicateQuestions.value-q}}}
    fun clearNetworkCache(){require(tasks.value.none{it.state in listOf(State.Analyzing,State.Downloading)}){"Pause downloads first"};File(context.cacheDir,"network").deleteRecursively()}
    suspend fun renameDownloaded(id:String,name:String)=withContext(Dispatchers.IO){require(name.isNotBlank()&&Rules.titleError(name)==null){Rules.titleError(name)?:"Filename required"};val t=get(id);require(t.state==State.Completed);val path=t.path?:error("File missing");val newPath=if(path.startsWith("content://")){val doc=androidx.documentfile.provider.DocumentFile.fromSingleUri(context,Uri.parse(path))?:error("File missing");check(doc.renameTo(name+"."+t.extension)){"Cannot rename file"};doc.uri.toString()}else{val source=File(path);val target=File(source.parentFile,name+"."+t.extension);require(!target.exists()){"Filename already exists"};check(source.renameTo(target)){"Cannot rename file"};target.absolutePath};tasks.value.filter{it.path==path}.forEach{row->update(row.id){it.copy(path=newPath)}}}
    companion object {
        fun redact(s: String)=s.replace(Regex("https?://\\S+|(?i)(cookie|authorization|token)\\s*[:=].*"),"[已隱藏敏感資料]")
        fun diagnose(s: String)=if(s.contains("403")||s.contains("Sign in",true))"網站要求登入或拒絕存取。可於網絡設定匯入自己的 Cookie；如開啟 VPN，請檢查地區及出口連線。"else if(s.contains("space",true))"儲存空間不足，請釋放空間或選擇另一個目錄。"else "下載失敗，請檢查網路及影片可用性；原始診斷可匯出分享。"
        fun toJpeg(bytes: ByteArray): ByteArray {val bitmap=android.graphics.BitmapFactory.decodeByteArray(bytes,0,bytes.size)?:error("無效封面");val out=java.io.ByteArrayOutputStream();bitmap.compress(android.graphics.Bitmap.CompressFormat.JPEG,95,out);bitmap.recycle();return out.toByteArray()}
    }
}
