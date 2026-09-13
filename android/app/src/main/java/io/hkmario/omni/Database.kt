package io.hkmario.omni
import android.content.Context
import android.content.ContentValues
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

class Database(context: Context) : SQLiteOpenHelper(context,"history.db",null,1) {
    val json = Json { ignoreUnknownKeys = true; encodeDefaults = true }
    override fun onCreate(db: SQLiteDatabase) { db.execSQL("CREATE TABLE tasks(id TEXT PRIMARY KEY, payload TEXT NOT NULL)"); db.execSQL("CREATE TABLE groups(id TEXT PRIMARY KEY,payload TEXT NOT NULL)") }
    override fun onUpgrade(db: SQLiteDatabase, old: Int, new: Int) = Unit
    @Synchronized fun save(task: TaskItem) { writableDatabase.insertWithOnConflict("tasks",null,ContentValues().apply { put("id",task.id);put("payload",json.encodeToString(task)) },SQLiteDatabase.CONFLICT_REPLACE) }
    @Synchronized fun tasks(): List<TaskItem> = readableDatabase.rawQuery("SELECT payload FROM tasks",null).use { c -> buildList { while(c.moveToNext()) add(json.decodeFromString<TaskItem>(c.getString(0))) } }
    @Synchronized fun save(group: PlaylistGroup) { writableDatabase.insertWithOnConflict("groups",null,ContentValues().apply { put("id",group.id);put("payload",json.encodeToString(group)) },SQLiteDatabase.CONFLICT_REPLACE) }
    @Synchronized fun groups(): List<PlaylistGroup> = readableDatabase.rawQuery("SELECT payload FROM groups",null).use { c -> buildList { while(c.moveToNext()) add(json.decodeFromString<PlaylistGroup>(c.getString(0))) } }
    @Synchronized fun hide(ids: Set<String>) { val db=writableDatabase;db.beginTransaction();try{tasks().filter{it.id in ids}.forEach{save(it.copy(history=false))};db.setTransactionSuccessful()}finally{db.endTransaction()} }
}
