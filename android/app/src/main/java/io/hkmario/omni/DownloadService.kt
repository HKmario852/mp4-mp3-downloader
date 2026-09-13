package io.hkmario.omni
import android.app.*
import android.content.*
import android.content.pm.ServiceInfo
import android.net.*
import android.os.*
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.*

object Notices {
    const val CHANNEL="downloads"
    fun create(context: Context) { context.getSystemService(NotificationManager::class.java).createNotificationChannel(NotificationChannel(CHANNEL,"下載狀態",NotificationManager.IMPORTANCE_DEFAULT)) }
    private fun open(context: Context)=PendingIntent.getActivity(context,1,Intent(context,MainActivity::class.java).putExtra("page","history").addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP),PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
    fun ongoing(context: Context,count: Int): Notification {
        val pause=PendingIntent.getBroadcast(context,2,Intent(context,DownloadActionReceiver::class.java).setAction("io.hkmario.omni.PAUSE"),PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val cancel=PendingIntent.getBroadcast(context,3,Intent(context,DownloadActionReceiver::class.java).setAction("io.hkmario.omni.CANCEL"),PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        return NotificationCompat.Builder(context,CHANNEL).setSmallIcon(android.R.drawable.stat_sys_download).setContentTitle("全能影音下載器").setContentText("$count 個下載任務").setContentIntent(open(context)).setOngoing(true).setOnlyAlertOnce(true).addAction(0,"暫停",pause).addAction(0,"取消",cancel).build()
    }
    fun show(context: Context,text: String) {try{NotificationManagerCompat.from(context).notify((System.currentTimeMillis()%100000+100).toInt(),NotificationCompat.Builder(context,CHANNEL).setSmallIcon(android.R.drawable.stat_sys_download_done).setContentTitle("全能影音下載器").setContentText(text).setStyle(NotificationCompat.BigTextStyle().bigText(text)).setAutoCancel(true).setContentIntent(open(context)).build())}catch(_: SecurityException){} }
}
class DownloadActionReceiver:BroadcastReceiver() {
    override fun onReceive(context: Context,intent: Intent) {
        if(intent.action !in listOf("io.hkmario.omni.PAUSE","io.hkmario.omni.CANCEL"))return
        val pending=goAsync();val engine=(context.applicationContext as OmniApp).engine
        engine.scope.launch { try {val ids=engine.tasks.value.map{it.id}.toSet();if(intent.action!!.endsWith("PAUSE"))ids.forEach{engine.pause(it)}else engine.cancel(ids)} finally{pending.finish()} }
    }
}
class DownloadService:Service() {
    private lateinit var engine:Engine;private lateinit var cm:ConnectivityManager
    private val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
    private var wake:PowerManager.WakeLock?=null
    private val callback=object:ConnectivityManager.NetworkCallback(){override fun onCapabilitiesChanged(network:Network,capabilities:NetworkCapabilities){scope.launch{engine.onNetworkChanged()}};override fun onLost(network:Network){scope.launch{engine.onNetworkChanged()}}}
    override fun onCreate() {
        super.onCreate();engine=(application as OmniApp).engine;engine.serviceRunning=true;cm=getSystemService(ConnectivityManager::class.java);Notices.create(this)
        if(Build.VERSION.SDK_INT>=29)startForeground(1,Notices.ongoing(this,0),ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)else startForeground(1,Notices.ongoing(this,0))
        wake=getSystemService(PowerManager::class.java).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"Omni:download").apply{acquire(6*60*60*1000L)}
        cm.registerDefaultNetworkCallback(callback)
        scope.launch { while(isActive){engine.pump();val count=engine.tasks.value.count{it.state in listOf(State.Queued,State.Analyzing,State.Downloading,State.Processing,State.RetryWait)};try{NotificationManagerCompat.from(this@DownloadService).notify(1,Notices.ongoing(this@DownloadService,count))}catch(_:SecurityException){};if(count==0){stopSelf();break};delay(500)} }
    }
    override fun onStartCommand(intent:Intent?,flags:Int,startId:Int)=START_NOT_STICKY
    override fun onBind(intent:Intent?)=null
    override fun onTimeout(startId:Int,fgsType:Int){engine.serviceRunning=false;engine.scope.launch{engine.tasks.value.forEach{engine.pause(it.id)}};Notices.show(this,"背景執行時間已達系統限制，下載已暫停；請開啟 App 繼續。");stopSelf()}
    override fun onDestroy(){engine.serviceRunning=false;scope.cancel();runCatching{cm.unregisterNetworkCallback(callback)};if(wake?.isHeld==true)wake?.release();super.onDestroy()}
    companion object { fun start(context:Context){ContextCompat.startForegroundService(context,Intent(context,DownloadService::class.java))} }
}
