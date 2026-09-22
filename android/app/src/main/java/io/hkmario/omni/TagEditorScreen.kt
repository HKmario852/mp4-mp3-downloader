package io.hkmario.omni
import android.content.Intent
import androidx.compose.foundation.draganddrop.dragAndDropTarget
import androidx.compose.ui.draganddrop.*
import androidx.compose.ui.platform.LocalContext
import android.app.Activity
import android.content.ClipboardManager
import android.content.Context
import android.media.MediaPlayer
import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.documentfile.provider.DocumentFile
import coil.compose.AsyncImage
import kotlinx.coroutines.*
import java.io.File
import java.util.UUID

data class SongTags(val values:Map<String,String>,val cover:ByteArray?)
val tagFields=listOf("TIT2" to "標題 / Title","TPE1" to "演出者 / Artist","TALB" to "專輯 / Album","TPE2" to "專輯演出者 / Album artist","TRCK" to "曲目 / Track","TPOS" to "光碟 / Disc","TYER" to "年份 / Year","TCON" to "類型 / Genre","TCOM" to "作曲者 / Composer","COMM" to "註解 / Comment")
suspend fun readSong(engine:Engine,t:TaskItem):SongTags=withContext(Dispatchers.IO){
 val path=t.path?:error("找不到檔案");val temporary=path.startsWith("content://");val source=if(temporary)File(engine.context.cacheDir,"tag-read-${UUID.randomUUID()}.mp3")else File(path)
 try{if(temporary)engine.context.contentResolver.openInputStream(Uri.parse(path))!!.use{i->source.outputStream().use{i.copyTo(it)}};val d=Id3.read(source);SongTags(tagFields.associate{(id,_)->id to d.text(if(id=="TYER"&&d.version==4)"TDRC"else id)},d.artwork())}finally{if(temporary)source.delete()}
}
@OptIn(ExperimentalFoundationApi::class)
@Composable fun TagEditorScreen(engine:Engine,initial:Set<String> = emptySet(),onDirty:(Boolean)->Unit = {},onBusy:(Boolean)->Unit = {},onReview:(Boolean)->Unit = {}) {
 val prefs by engine.prefs.collectAsState();val tasks by engine.tasks.collectAsState();val scope=rememberCoroutineScope()
 var imports by remember{mutableStateOf(engine.tagImports.values.toList())};val songs=(tasks.filter{it.history&&it.state==State.Completed&&it.extension=="mp3"}+imports).distinctBy{it.path}
 var selected by remember{mutableStateOf(initial)};var query by remember{mutableStateOf("")};var baseline by remember{mutableStateOf<Map<String,String>>(emptyMap())};var delta by remember{mutableStateOf<Map<String,String>>(emptyMap())};var raw by remember{mutableStateOf("{}")};var art by remember{mutableStateOf<ByteArray?>(null)};var changedArt by remember{mutableStateOf(false)};var rename by remember{mutableStateOf(false)};var advanced by remember{mutableStateOf(false)};var busy by remember{mutableStateOf(false)};var error by remember{mutableStateOf<String?>(null)};var revision by remember{mutableIntStateOf(0)};var pending by remember{mutableStateOf<Set<String>?>(null)};var discardImport by remember{mutableStateOf<(() -> Unit)?>(null)}
 var reviewSong by remember{mutableStateOf<TaskItem?>(null)};var reviewNotice by remember{mutableStateOf("")};var undoReview by remember{mutableStateOf<ReviewUndo?>(null)}
 val listState=androidx.compose.foundation.lazy.rememberLazyListState();val editorScroll=rememberScrollState()
 val dirty=delta.isNotEmpty()||raw.trim()!="{}"||changedArt;SideEffect{onDirty(dirty);onBusy(busy)}
 val player=remember{MediaPlayer()};var playing by remember{mutableStateOf(false)};var position by remember{mutableFloatStateOf(0f)};var duration by remember{mutableFloatStateOf(1f)};var volume by remember{mutableFloatStateOf(.7f)};var prepared by remember{mutableStateOf(false)}
 DisposableEffect(Unit){onDispose{player.release()}}
 LaunchedEffect(playing){while(playing){position=runCatching{player.currentPosition/1000f}.getOrDefault(0f);delay(300)}}
 LaunchedEffect(selected,revision){player.reset();playing=false;prepared=false;delta=emptyMap();raw="{}";changedArt=false;baseline=emptyMap();art=null;if(selected.isNotEmpty()){busy=true;try{val values=selected.map{readSong(engine,engine.get(it))};baseline=tagFields.associate{(id,_)->val v=values.map{it.values[id]?:""}.distinct();id to if(v.size==1)v[0]else""};art=values.first().cover}catch(e:Exception){error=e.message}finally{busy=false}}}
 suspend fun importFiles(uris:List<Pair<Uri,String>>){busy=true;try{val rows=withContext(Dispatchers.IO){uris.filter{(u,_)->DocumentFile.fromSingleUri(engine.context,u)?.name?.endsWith(".mp3",true)==true}.map{(uri,parent)->runCatching{engine.context.contentResolver.takePersistableUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)};val name=DocumentFile.fromSingleUri(engine.context,uri)?.name?:"MP3";val task=TaskItem(url="",mode="mp3",outputFormat="mp3",title=name.removeSuffix(".mp3"),path=uri.toString(),directory=parent,state=State.Completed,history=false);val info=readSong(engine,task);task.copy(title=info.values["TIT2"].orEmpty().ifBlank{task.title},artist=info.values["TPE1"].orEmpty(),album=info.values["TALB"].orEmpty())}};rows.forEach{if(songs.none{s->s.path==it.path})engine.tagImports[it.id]=it};imports=engine.tagImports.values.toList();selected=(songs+imports).filter{s->rows.any{it.path==s.path}}.map{it.id}.toSet();revision++}catch(e:Exception){error=e.message}finally{busy=false}}
 val picker=rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()){uris->scope.launch{importFiles(uris.map{it to ""})}}
 val tree=rememberLauncherForActivityResult(ActivityResultContracts.OpenDocumentTree()){uri->if(uri!=null)scope.launch{try{engine.context.contentResolver.takePersistableUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION);val files=withContext(Dispatchers.IO){val result=mutableListOf<Pair<Uri,String>>();val seen=mutableSetOf<String>();fun walk(folder:DocumentFile){if(!seen.add(folder.uri.toString()))return;folder.listFiles().forEach{if(it.isDirectory)walk(it)else if(it.name?.endsWith(".mp3",true)==true)result+=it.uri to folder.uri.toString()}};DocumentFile.fromTreeUri(engine.context,uri)?.let(::walk);result};importFiles(files)}catch(e:Exception){error=e.message}}}
 suspend fun acceptCover(uri:Uri){try{require(selected.isNotEmpty()){ "請先選擇歌曲" };val bytes=withContext(Dispatchers.IO){engine.context.contentResolver.openInputStream(uri)!!.use{input->val out=java.io.ByteArrayOutputStream();val b=ByteArray(8192);while(out.size()<=32*1024*1024){val n=input.read(b);if(n<0)break;out.write(b,0,n)};out.toByteArray()}};require(bytes.size<=32*1024*1024&&AlbumArtwork.accept(bytes)){"請選取 32 MB 以下近正方形圖片；不會自動裁切或拉伸"};art=bytes;changedArt=true}catch(e:Exception){error=e.message}}
 val coverPicker=rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()){uri->if(uri!=null)scope.launch{acceptCover(uri)}}
 val localContext=LocalContext.current
 val latestAccept by rememberUpdatedState<(Uri)->Unit>({uri->scope.launch{acceptCover(uri)}})
 val dropTarget=remember(localContext){object:DragAndDropTarget{override fun onDrop(event:DragAndDropEvent):Boolean{val native=event.toAndroidDragEvent();val uri=native.clipData?.let{if(it.itemCount>0)it.getItemAt(0).uri else null}?:return false;var c:Context=localContext;while(c is android.content.ContextWrapper&&c !is Activity)c=c.baseContext;val permission=(c as? Activity)?.requestDragAndDropPermissions(native);scope.launch{try{acceptCover(uri)}finally{permission?.release()}};return true}}}
 fun pasteCover(){val clip=(engine.context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager).primaryClip;val item=clip?.let{if(it.itemCount>0)it.getItemAt(0)else null};val uri=item?.uri?:item?.text?.toString()?.takeIf{it.startsWith("content://")||it.startsWith("file://")}?.let(Uri::parse);if(uri==null)error="剪貼簿沒有可用圖片，請複製圖片檔案。"else latestAccept(uri)}
 var searchMusic by remember{mutableStateOf(false)};var musicTitle by remember{mutableStateOf("")};var musicArtist by remember{mutableStateOf("")}
 var musicChoices by remember{mutableStateOf<List<MusicCandidate>>(emptyList())}
 fun preview(result:MusicResult){if(result.title==null){error=result.status;return};val values=result.tags.ifEmpty{mapOf("TIT2" to result.title.orEmpty(),"TPE1" to result.artist.orEmpty(),"TALB" to result.album.orEmpty())};delta=values.filter{(key,value)->baseline[key]!=value};val song=songs.first{it.id in selected};if(result.cover!=null&&!song.coverUserEdited&&!song.isUserEdited){art=result.cover.bytes;changedArt=true};error="已載入配對預覽，請核對後按「儲存標籤」。手動封面會保留。"}
 fun lookup(scan:Boolean){if(scan){if(busy)return;if(selected.size!=1){reviewNotice="請選擇一首 MP3 再 Scan 音訊辨識";return};player.reset();playing=false;prepared=false;reviewSong=songs.first{it.id in selected};onReview(true);return};if(selected.size!=1||dirty){error="請先儲存變更，並選擇一首歌曲預覽配對。";return};scope.launch{busy=true;try{val song=songs.first{it.id in selected};if(scan){val result=MusicRecognition.scan(engine.context,song.path!!,prefs.acoustIdClientKey);if(result.choices.isNotEmpty())musicChoices=result.choices else preview(result)}else{musicTitle=song.title;musicArtist=song.artist;searchMusic=true}}catch(e:CancellationException){throw e}catch(e:Exception){error=e.message}finally{busy=false}}}
 if(searchMusic)AlertDialog(onDismissRequest={searchMusic=false},title={Text("MusicBrainz 查找")},text={Column{OutlinedTextField(musicTitle,{musicTitle=it},label={Text("歌曲名稱或 recording 連結 / MBID")});OutlinedTextField(musicArtist,{musicArtist=it},label={Text("演出者（選填）")})}},confirmButton={TextButton(enabled=musicTitle.isNotBlank(),onClick={searchMusic=false;scope.launch{busy=true;try{val id=MusicRecognition.recordingId(musicTitle);if(id!=null){val result=MusicRecognition.recording(id);if(result.choices.isNotEmpty())musicChoices=result.choices else preview(result)}else{musicChoices=MusicRecognition.search(musicTitle,musicArtist);if(musicChoices.isEmpty())error="查無結果，可嘗試 Scan 音訊辨識。"}}catch(e:CancellationException){throw e}catch(e:Exception){error=e.message}finally{busy=false}}}){Text("查找")}},dismissButton={TextButton(onClick={searchMusic=false}){Text("取消")}})
 if(musicChoices.isNotEmpty())MusicChoiceDialog(musicChoices,{choice->musicChoices=emptyList();scope.launch{busy=true;try{preview(MusicRecognition.recording(choice.recordingId,choice.releaseId))}catch(e:Exception){error=e.message}finally{busy=false}}},{musicChoices=emptyList()})

 fun changeSelection(next:Set<String>){if(busy)return;if(dirty)pending=next else selected=next}
 fun requestImport(action:()->Unit){if(dirty)discardImport=action else action()}
 fun play(){try{if(!prepared){val song=songs.firstOrNull{it.id in selected}?:return;player.reset();player.setDataSource(engine.context,Uri.parse(song.path));player.setOnPreparedListener{prepared=true;duration=(it.duration/1000f).coerceAtLeast(1f);it.setVolume(volume,volume);it.start();playing=true};player.setOnCompletionListener{playing=false;position=0f};player.setOnErrorListener{_,_,_->error="無法試聽此音訊";playing=false;prepared=false;true};player.prepareAsync()}else{if(playing)player.pause()else player.start();playing=!playing}}catch(e:Exception){error=e.message}}
 val renameAllowed=selected.all{id->val s=engine.get(id);s.path?.startsWith("content://")!=true||s.directory.startsWith("content://")}
 val titleError=if(rename)delta["TIT2"]?.let(Rules::titleError)else null
 reviewSong?.let{song->AcoustIdReviewScreen(engine,song,{reviewSong=null;onReview(false)},{values,cover,undo,count->
  scope.launch{val draft=delta-values.keys;val updated=readSong(engine,engine.get(song.id));baseline=updated.values;delta=draft;if(cover!=null){art=updated.cover;changedArt=false};undoReview=undo;reviewNotice="已套用 $count 個標籤變更";reviewSong=null;imports=engine.tagImports.values.toList();onReview(false)}
 });return}
 Column(Modifier.fillMaxSize()){
  Text(text(prefs,"標籤編輯","Tag editor"),fontSize=26.sp)
  Row(Modifier.horizontalScroll(rememberScrollState())){Button(enabled=!busy,onClick={requestImport{picker.launch(arrayOf("audio/mpeg"))}}){FeatureIcon("add");Text(text(prefs,"加入 MP3","Add MP3"))};OutlinedButton(enabled=!busy,onClick={requestImport{tree.launch(null)}}){FeatureIcon("folder");Text(text(prefs,"加入資料夾","Add folder"))}}
  OutlinedTextField(query,{query=it},placeholder={Text(text(prefs,"搜尋歌曲、演出者、專輯或檔名…","Search songs, artist, album or filename…"))},leadingIcon={FeatureIcon("search")},singleLine=true,modifier=Modifier.fillMaxWidth().testTag("tag-search"))
  Row(Modifier.horizontalScroll(rememberScrollState())){TextButton(enabled=!busy,onClick={changeSelection(songs.filter{Rules.matches(it,query)}.map{it.id}.toSet())}){Text(text(prefs,"全選","Select all"))};TextButton(enabled=!busy,onClick={changeSelection(emptySet())}){Text(text(prefs,"取消全選","Clear selection"))};TextButton(enabled=!busy&&selected.isNotEmpty()&&!dirty,onClick={scope.launch{busy=true;player.reset();prepared=false;playing=false;var found=0;var missing=0;var protected=0;var failed=0;try{for(id in selected){try{val song=engine.get(id);val current=readSong(engine,song);if(song.coverUserEdited||(current.cover!=null&&(song.isUserEdited||song.url.isBlank()))){protected++;continue};var cover:Art?=null;if(song.url.isNotBlank())try{cover=withTimeout(45000){engine.findSourceArtwork(song.url,engine.analyze(song.url))}}catch(e:TimeoutCancellationException){}catch(e:CancellationException){throw e}catch(_:Exception){};if(cover==null)cover=Metadata.lookup(song.title,song.artist,song.duration).cover;if(cover==null||!AlbumArtwork.accept(cover.bytes)){missing++;continue};TagEditor(engine).apply(setOf(id),emptyMap(),emptyMap(),cover.bytes,renameFile=false,automaticCover=true);found++}catch(e:CancellationException){throw e}catch(_:Exception){failed++}};revision++;error="封面：更新 $found、未找到 $missing、保留手動封面 $protected、失敗 $failed"}finally{busy=false}}}){FeatureIcon("search");Text(text(prefs,"重新尋找封面","Find artwork"))}}
  Row{TextButton(enabled=!busy&&selected.size==1&&!dirty,onClick={lookup(false)}){Text("查找歌曲標籤")};TextButton(enabled=!busy,onClick={lookup(true)}){Text("Scan 音訊辨識")}}
  if(reviewNotice.isNotBlank())Text(reviewNotice,color=MaterialTheme.colorScheme.secondary)
  undoReview?.let{undo->TextButton(enabled=!busy,onClick={scope.launch{busy=true;try{undo.restore(engine);val updated=readSong(engine,engine.get(undo.before.id));baseline=updated.values;if(!changedArt)art=updated.cover;imports=engine.tagImports.values.toList();undoReview=null;reviewNotice="已復原上次標籤套用"}catch(e:Exception){reviewNotice=e.message.orEmpty()}finally{busy=false}}}){Text("復原上次套用")}}
  Text(text(prefs,"已選取 ${selected.size} 首歌曲","${selected.size} songs selected"),fontSize=13.sp)
  BoxWithConstraints(Modifier.weight(1f)){
   val wide=maxWidth>800.dp
   @Composable fun SongList(modifier:Modifier){LazyColumn(modifier,state=listState){items(songs.filter{Rules.matches(it,query)},key={it.id}){s->var rowArt by remember(s.path,revision){mutableStateOf<ByteArray?>(null)};LaunchedEffect(s.path,revision){rowArt=runCatching{readSong(engine,s).cover}.getOrNull()};Card(Modifier.fillMaxWidth().padding(vertical=3.dp).testTag("tag-row-${s.id}").combinedClickable(enabled=!busy,onClick={changeSelection(if(selected.size>=2){if(s.id in selected)selected-s.id else selected+s.id}else setOf(s.id))},onLongClick={changeSelection(selected+s.id)}),colors=CardDefaults.cardColors(containerColor=if(s.id in selected)MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface)){Row(verticalAlignment=Alignment.CenterVertically){if(selected.size>=2)AppCheckbox(s.id in selected,{v->changeSelection(if(v)selected+s.id else selected-s.id)},modifier=Modifier.testTag("tag-select-${s.id}"),enabled=!busy);if(rowArt!=null)AsyncImage(rowArt,null,Modifier.size(48.dp))else FeatureIcon("audio",Modifier.size(36.dp));Column(Modifier.weight(1f).padding(8.dp)){Text(s.title,maxLines=2);Row{FeatureIcon("mp3",Modifier.size(18.dp));Text(s.artist+" · MP3",fontSize=12.sp)}}}}}}}
   @Composable fun Editor(modifier:Modifier){Column(modifier.verticalScroll(editorScroll),verticalArrangement=Arrangement.spacedBy(9.dp)){
    Text(if(dirty)text(prefs,"● 尚未儲存","● Unsaved")else text(prefs,"歌曲資料","Song details"),fontSize=20.sp)
    Box(Modifier.fillMaxWidth().height(190.dp).dragAndDropTarget(shouldStartDragAndDrop={selected.isNotEmpty()&&!busy},target=dropTarget),contentAlignment=Alignment.Center){if(art==null)Text("尚未加入封面 · 可拖放圖片")else AsyncImage(art,text(prefs,"專輯封面","Album cover"),Modifier.fillMaxSize())}
    TextButton(enabled=selected.isNotEmpty()&&!busy,onClick=::pasteCover){Text("貼上剪貼簿封面")}
    Row(Modifier.horizontalScroll(rememberScrollState())){OutlinedButton(enabled=selected.isNotEmpty()&&!busy,onClick={coverPicker.launch(arrayOf("image/*"))}){Text(text(prefs,"更換封面","Change artwork"))};TextButton(enabled=selected.isNotEmpty()&&!busy,onClick={art=null;changedArt=true}){FeatureIcon("delete");Text(text(prefs,"移除封面","Remove artwork"))};TextButton(enabled=selected.isNotEmpty()&&!busy,onClick=::play){Text(if(playing)"Ⅱ 暫停"else"▶ 試聽")}}
    Slider(position,{position=it;if(prepared)player.seekTo((it*1000).toInt())},valueRange=0f..duration,enabled=prepared&&!busy);Text("${position.toInt()/60}:${(position.toInt()%60).toString().padStart(2,'0')} / ${duration.toInt()/60}:${(duration.toInt()%60).toString().padStart(2,'0')}",fontSize=12.sp)
    Row(verticalAlignment=Alignment.CenterVertically){Text(text(prefs,"音量","Volume"));Slider(volume,{volume=it;player.setVolume(it,it)},Modifier.weight(1f))}
    tagFields.forEach{(id,label)->OutlinedTextField(delta[id]?:baseline[id].orEmpty(),{v->delta=if(v==baseline[id])delta-id else delta+(id to v)},enabled=selected.isNotEmpty()&&!busy,label={Text(label)},isError=id=="TIT2"&&titleError!=null,supportingText={if(id=="TIT2"&&titleError!=null)Text(titleError)},trailingIcon={TextButton(enabled=!busy,onClick={delta=delta+(id to "")}){Text("×")}},singleLine=id!="COMM",modifier=Modifier.fillMaxWidth().widthIn(max=440.dp))}
    TextButton(onClick={advanced=!advanced}){Text(text(prefs,"更多標籤","More tags")+if(advanced)" ▴"else" ▾")};if(advanced)OutlinedTextField(raw,{raw=it},enabled=!busy,label={Text("ID3 Frames · Base64 JSON")},modifier=Modifier.fillMaxWidth())
   }}
   if(wide)Row(horizontalArrangement=Arrangement.spacedBy(18.dp)){SongList(Modifier.width(280.dp).fillMaxHeight());Editor(Modifier.weight(1f))}else Column{SongList(Modifier.fillMaxWidth().heightIn(max=170.dp));Editor(Modifier.weight(1f))}
  }
  Row(verticalAlignment=Alignment.CenterVertically){AppCheckbox(rename&&renameAllowed,{rename=it},enabled=!busy&&renameAllowed);Text(text(prefs,"同時重新命名檔案","Also rename files"),fontSize=13.sp)}
  if(!renameAllowed)Text(text(prefs,"如需重新命名，請用「加入資料夾」授權歌曲所在目錄。","To rename, use Add folder to grant access to the parent folder."),fontSize=12.sp)
  Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.End){TextButton(enabled=!busy,onClick={revision++}){Text(text(prefs,"復原變更","Revert changes"))};Button(enabled=dirty&&selected.isNotEmpty()&&!busy&&titleError==null,onClick={scope.launch{busy=true;player.reset();prepared=false;playing=false;try{val json=org.json.JSONObject(raw);val bytes=json.keys().asSequence().associateWith{android.util.Base64.decode(json.getString(it),android.util.Base64.DEFAULT)};TagEditor(engine).apply(selected,delta,bytes,if(changedArt)art else null,changedArt&&art==null,rename&&renameAllowed);imports=engine.tagImports.values.toList();revision++}catch(e:Exception){error=e.message}finally{busy=false}}}){Text(text(prefs,"儲存標籤","Save tags"))}}
 }
 pending?.let{next->AlertDialog(onDismissRequest={pending=null},title={Text(text(prefs,"未儲存變更","Unsaved changes"))},text={Text(text(prefs,"捨棄修改並切換歌曲？","Discard changes and switch songs?"))},confirmButton={TextButton(onClick={pending=null;selected=next;revision++}){Text(text(prefs,"捨棄變更","Discard"))}},dismissButton={TextButton(onClick={pending=null}){Text(text(prefs,"返回","Back"))}})}
 discardImport?.let{action->AlertDialog(onDismissRequest={discardImport=null},title={Text(text(prefs,"未儲存變更","Unsaved changes"))},text={Text(text(prefs,"捨棄修改並加入歌曲？","Discard changes and import songs?"))},confirmButton={TextButton(onClick={discardImport=null;revision++;action()}){Text(text(prefs,"捨棄變更","Discard"))}},dismissButton={TextButton(onClick={discardImport=null}){Text(text(prefs,"返回","Back"))}})}
 error?.let{AlertDialog(onDismissRequest={error=null},title={Text(text(prefs,"提示","Notice"))},text={Text(it)},confirmButton={TextButton(onClick={error=null}){Text("OK")}})}
}

@Composable fun MusicChoiceDialog(choices:List<MusicCandidate>,choose:(MusicCandidate)->Unit,close:()->Unit){
 AlertDialog(onDismissRequest=close,title={Text("選擇歌曲及專輯版本")},text={LazyColumn(Modifier.heightIn(max=400.dp)){items(choices){choice->TextButton(onClick={choose(choice)}){Text(choice.toString())}}}},confirmButton={},dismissButton={TextButton(onClick=close){Text("保留原有資料")}})
}
