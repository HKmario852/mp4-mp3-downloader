package io.hkmario.omni
import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.MediaStore
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import kotlinx.coroutines.launch
import java.io.File
fun text(p:Prefs,zh:String,en:String)=if(p.language=="en")en else zh
fun finished(t:TaskItem)=t.completedAt?.let{java.time.Instant.ofEpochMilli(it).atZone(java.time.ZoneId.systemDefault()).format(java.time.format.DateTimeFormatter.ofPattern("yyyy/MM/dd HH:mm"))}?:"—"
fun quality(t:TaskItem)=if(t.mode=="mp3")if(t.extension in listOf("flac","wav"))"Lossless"else"${t.kbps} kbps"else if(t.height==0)"Best · 4K"else"${t.height}p"
fun size(t:TaskItem)=t.total?.let{"%.1f MB".format(it/1000000.0)}?:"—"
fun folder(engine:Engine,t:TaskItem){val uri=if(t.directory.startsWith("content://"))Uri.parse(t.directory)else if(t.path?.startsWith("content://")==true)Uri.parse(t.path)else null
 val intent=if(uri!=null)Intent(Intent.ACTION_VIEW).setDataAndType(uri,"vnd.android.document/directory").addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)else Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
 engine.context.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}
@Composable fun LibraryScreen(engine:Engine,open:(TaskItem)->Unit){val tasks by engine.tasks.collectAsStateWithLifecycle();val p by engine.prefs.collectAsStateWithLifecycle();val scope=rememberCoroutineScope();var q by remember{mutableStateOf("")};var format by remember{mutableStateOf("all")};var date by remember{mutableIntStateOf(0)};var order by remember{mutableIntStateOf(0)};var selected by remember{mutableStateOf(setOf<String>())};var detail by remember{mutableStateOf<TaskItem?>(null)};var deleting by remember{mutableStateOf<List<TaskItem>?>(null)};var rename by remember{mutableStateOf<TaskItem?>(null)};var editing by remember{mutableStateOf<Set<String>?>(null)};var clearing by remember{mutableStateOf<Set<String>?>(null)};var message by remember{mutableStateOf<String?>(null)};var trashPending by remember{mutableStateOf<List<TaskItem>>(emptyList())};var permanentPending by remember{mutableStateOf<List<TaskItem>>(emptyList())}
 val library=tasks.filter{it.history&&!it.groupRoot&&it.state==State.Completed};val matches=library.filter{Rules.matches(it,q)&&(format=="all"||it.extension==format)&&(date==0||(it.completedAt?:0)>=System.currentTimeMillis()-date*86400000L)};val shown=when(order){1->matches.sortedBy{it.completedAt};2->matches.sortedBy{it.title.lowercase()};3->matches.sortedByDescending{it.total?:0};else->matches.sortedByDescending{it.completedAt}}
 fun mediaUri(t:TaskItem):Uri?=try{if(Build.VERSION.SDK_INT>=30&&t.path?.startsWith("content://")==true){val uri=Uri.parse(t.path);(if(uri.authority=="media")uri else MediaStore.getMediaUri(engine.context,uri))?.takeIf{it.authority=="media"}}else null}catch(_:Exception){null}
 suspend fun permanent(rows:List<TaskItem>){var failures=0;rows.distinctBy{it.path}.forEach{try{engine.deleteDownloaded(it.id)}catch(_:Exception){failures++}};selected=emptySet();detail=null;if(failures>0)message=text(p,"$failures 個檔案刪除失敗，紀錄已保留","$failures files could not be deleted; records kept")}
 val trash=rememberLauncherForActivityResult(ActivityResultContracts.StartIntentSenderForResult()){result->if(result.resultCode==Activity.RESULT_OK){val paths=trashPending.map{it.path}.toSet();engine.clearHistory(tasks.filter{it.path in paths}.map{it.id}.toSet());val rest=permanentPending.toList();scope.launch{permanent(rest)}};trashPending=emptyList();permanentPending=emptyList()}
 Column(Modifier.fillMaxSize()){
  Text(text(p,"已下載","Downloaded"),fontSize=26.sp,fontWeight=FontWeight.Bold);Text(text(p,"管理影片、音訊及標籤","Manage video, audio and tags"),color=MaterialTheme.colorScheme.onSurfaceVariant)
  OutlinedTextField(q,{q=it},label={Text(text(p,"搜尋標題、歌手、專輯、網址…","Search title, artist, album, URL…"))},modifier=Modifier.fillMaxWidth().padding(vertical=10.dp),singleLine=true)
  Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)){listOf("all","mp4","mkv","webm","mp3","m4a","flac","wav").forEach{f->FilterChip(format==f,{format=f},label={Text(if(f=="all")text(p,"全部格式","All formats")else f.uppercase())})}}
  Row(Modifier.horizontalScroll(rememberScrollState())){TextButton(onClick={date=when(date){0->7;7->30;else->0}}){Text(if(date==0)text(p,"所有日期","Any date")else text(p,"最近 $date 日","Last $date days"))};TextButton(onClick={order=(order+1)%4}){Text(listOf(text(p,"最新完成","Newest"),text(p,"最早完成","Oldest"),text(p,"檔名 A–Z","Name A–Z"),text(p,"大小由大至細","Largest first"))[order])}}
  Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(6.dp)){listOf(text(p,"已完成","Completed") to library.size.toString(),text(p,"影片","Video") to library.count{it.mode=="mp4"}.toString(),text(p,"音訊","Audio") to library.count{it.mode=="mp3"}.toString(),text(p,"總大小","Total size") to "%.2f GB".format(library.sumOf{it.total?:0}/1e9)).forEach{(label,value)->Card(Modifier.weight(1f)){Column(Modifier.padding(10.dp)){Text(label,fontSize=11.sp);Text(value,fontSize=18.sp)}}}}
  Row(Modifier.horizontalScroll(rememberScrollState()),verticalAlignment=Alignment.CenterVertically){Checkbox(shown.isNotEmpty()&&shown.all{it.id in selected},{checked->selected=if(checked)selected+shown.map{it.id}else selected-shown.map{it.id}.toSet()});Text(text(p,"全選","Select all"));TextButton(enabled=selected.isNotEmpty(),onClick={deleting=library.filter{it.id in selected}}){Text(text(p,"移除所選","Remove selected"))};TextButton(enabled=shown.isNotEmpty(),onClick={clearing=shown.map{it.id}.toSet()}){Text(text(p,"清除紀錄","Clear history"))};TextButton(enabled=selected.any{id->library.any{it.id==id&&it.extension=="mp3"}},onClick={editing=library.filter{it.id in selected&&it.extension=="mp3"}.map{it.id}.toSet()}){Text(text(p,"MP3 標籤編輯","Edit MP3 tags"))}}
  LazyColumn(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(8.dp)) {
   items(shown,key={it.id}) { t ->
    var menu by remember(t.id){mutableStateOf(false)}
    Card(colors=CardDefaults.cardColors(containerColor=if(t.id in selected)MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surface),modifier=Modifier.fillMaxWidth().clickable{detail=t}) {
     Row(Modifier.padding(10.dp),verticalAlignment=Alignment.CenterVertically) {
      Checkbox(t.id in selected,{v->selected=if(v)selected+t.id else selected-t.id})
      AsyncImage(t.thumbnail,null,Modifier.size(64.dp,48.dp))
      Column(Modifier.weight(1f).padding(horizontal=12.dp)) {
       Text(t.title,maxLines=2,fontWeight=FontWeight.SemiBold)
       Text("${t.extension.uppercase()} · ${quality(t)} · ${size(t)}",fontSize=12.sp)
       Text(finished(t),fontSize=12.sp,color=MaterialTheme.colorScheme.onSurfaceVariant)
       Text(text(p,"✓ 已完成","✓ Completed"),color=androidx.compose.ui.graphics.Color(0xFF29B96F),fontSize=12.sp)
      }
      Box {
       TextButton(onClick={menu=true}){Text("⋯",fontSize=24.sp)}
       DropdownMenu(menu,{menu=false}) {
        val actions:List<Pair<String,()->Unit>> = listOf(
         text(p,"播放","Play") to {open(t)},
         text(p,"開啟檔案位置","Show in folder") to {folder(engine,t)},
         text(p,"重新命名","Rename") to {rename=t},
         text(p,"重新下載","Download again") to {engine.enqueue(t.url,t.mode,t.extension);DownloadService.start(engine.context)},
         text(p,"查看檔案資訊","File information") to {detail=t},
         text(p,"刪除檔案","Delete file") to {deleting=listOf(t)})
        actions.forEach{(label,action)->DropdownMenuItem(text={Text(label)},onClick={menu=false;runCatching{action()}.onFailure{message=it.message}})}
       }
      }
     }
    }
   }
  }
  Text(text(p,"共 ${shown.size} 項 · 已選 ${selected.size} 項","${shown.size} files · ${selected.size} selected"),fontSize=12.sp)
 }
 detail?.let { t ->
  AlertDialog(
   onDismissRequest={detail=null}, title={Text(text(p,"檔案詳情","File details"))},
   text={Column(Modifier.heightIn(max=480.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(12.dp)) {
    AsyncImage(t.thumbnail,null,Modifier.fillMaxWidth().height(170.dp));Text(t.title,fontWeight=FontWeight.Bold)
    Text("${t.extension.uppercase()} · ${quality(t)} · ${size(t)}");Text(finished(t));Text(text(p,"✓ 已完成","✓ Completed"))
    Text(t.artist);Text(t.album);Text(t.path?:"",fontSize=12.sp);Text(t.url,fontSize=12.sp)
    TextButton(onClick={runCatching{folder(engine,t)}.onFailure{message=it.message}}){Text(text(p,"開啟檔案位置","Show in folder"))}
   }},
   confirmButton={TextButton(onClick={runCatching{open(t)}.onFailure{message=it.message}}){Text(text(p,"播放","Play"))}},
   dismissButton={TextButton(onClick={detail=null}){Text(text(p,"關閉","Close"))}})
 }
 deleting?.let{rows->val trashRows=rows.filter{mediaUri(it)!=null};val permanentRows=rows-trashRows.toSet();AlertDialog(onDismissRequest={deleting=null},title={Text(text(p,"刪除所選檔案","Delete selected files"))},text={Text(text(p,"${trashRows.size} 個檔案可移至系統垃圾桶。${permanentRows.size} 個檔案所在儲存位置不支援垃圾桶，將永久刪除，無法復原。對應紀錄亦會移除。","${trashRows.size} files support system trash. ${permanentRows.size} files cannot be trashed and will be permanently deleted. Their history records will also be removed."))},confirmButton={TextButton(onClick={deleting=null;if(trashRows.isNotEmpty()&&Build.VERSION.SDK_INT>=30){try{trashPending=trashRows;permanentPending=permanentRows;trash.launch(IntentSenderRequest.Builder(MediaStore.createTrashRequest(engine.context.contentResolver,trashRows.mapNotNull{mediaUri(it)},true).intentSender).build())}catch(e:Exception){message=e.message}}else scope.launch{permanent(permanentRows)}}){Text(text(p,"確認刪除","Confirm deletion"))}},dismissButton={TextButton(onClick={deleting=null}){Text(text(p,"取消","Cancel"))}})}
 rename?.let{t->var value by remember(t.id){mutableStateOf(if(t.path?.startsWith("content://")==true)t.title else File(t.path?:"").nameWithoutExtension)};val error=Rules.titleError(value);AlertDialog(onDismissRequest={rename=null},title={Text(text(p,"重新命名","Rename"))},text={Column{Text(text(p,"只更改檔名；中繼資料請用標籤編輯。","Changes filename only; use the tag editor for metadata."));OutlinedTextField(value,{value=it},isError=error!=null,supportingText={if(error!=null)Text(error)})}},confirmButton={TextButton(enabled=value.isNotBlank()&&error==null,onClick={scope.launch{try{engine.renameDownloaded(t.id,value);rename=null;detail=null}catch(e:Exception){message=e.message}}}){Text(text(p,"儲存","Save"))}},dismissButton={TextButton(onClick={rename=null}){Text(text(p,"取消","Cancel"))}})}
 clearing?.let{ids->AlertDialog(onDismissRequest={clearing=null},title={Text(text(p,"清除紀錄","Clear history"))},text={Text(text(p,"清除 ${ids.size} 筆紀錄？實體檔案會保留。","Clear ${ids.size} records? Files will be kept."))},confirmButton={TextButton(onClick={engine.clearHistory(ids);selected=selected-ids;clearing=null}){Text(text(p,"清除","Clear"))}},dismissButton={TextButton(onClick={clearing=null}){Text(text(p,"取消","Cancel"))}})}
 editing?.let{TagDialog(engine,it){editing=null}}
 message?.let{AlertDialog(onDismissRequest={message=null},title={Text(text(p,"提示","Notice"))},text={Text(it)},confirmButton={TextButton(onClick={message=null}){Text("OK")}})}
}
