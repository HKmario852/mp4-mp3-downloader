using Microsoft.Data.Sqlite;
namespace Omni.Core;
public sealed class Store
{
    public string DataDirectory {get;}
    readonly string connection; readonly object gate = new();
    public Store(string directory)
    {
        DataDirectory=Path.GetFullPath(directory);System.IO.Directory.CreateDirectory(directory);
        connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "history.db") }.ToString();
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS jobs(id TEXT PRIMARY KEY, requestId TEXT NOT NULL, history INTEGER NOT NULL DEFAULT 1, json TEXT NOT NULL); CREATE INDEX IF NOT EXISTS requests ON jobs(requestId); CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS groups(id TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS tag_library_sources(path TEXT PRIMARY KEY COLLATE NOCASE,isFolder INTEGER NOT NULL);"; c.ExecuteNonQuery();
    }
    SqliteConnection Open() { var db = new SqliteConnection(connection); db.Open(); return db; }
    public void Save(DownloadJob j) { lock (gate) { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "INSERT INTO jobs(id,requestId,json) VALUES($id,$r,$j) ON CONFLICT(id) DO UPDATE SET json=$j"; c.Parameters.AddWithValue("$id", j.Id); c.Parameters.AddWithValue("$r", j.RequestId); c.Parameters.AddWithValue("$j", Json.Encode(j)); c.ExecuteNonQuery(); } }
    public void SaveMany(IEnumerable<DownloadJob> records) { lock(gate){using var db=Open();using var tx=db.BeginTransaction();foreach(var j in records){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE jobs SET json=$j WHERE id=$id";c.Parameters.AddWithValue("$id",j.Id);c.Parameters.AddWithValue("$j",Json.Encode(j));c.ExecuteNonQuery();}tx.Commit();} }
    public List<DownloadJob> Load(bool historyOnly = false) { lock (gate) { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "SELECT json FROM jobs" + (historyOnly ? " WHERE history=1" : ""); using var r = c.ExecuteReader(); var a = new List<DownloadJob>(); while (r.Read()) a.Add(Json.Decode<DownloadJob>(r.GetString(0))); return a; } }
    public void ClearHistory(IReadOnlyCollection<string> ids) { lock (gate) { using var db = Open(); using var tx = db.BeginTransaction(); foreach (var id in ids) { using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = "UPDATE jobs SET history=0 WHERE id=$id"; c.Parameters.AddWithValue("$id", id); c.ExecuteNonQuery(); } tx.Commit(); } }
    public Preferences Preferences() { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "SELECT json FROM settings WHERE key='preferences'"; var saved=c.ExecuteScalar() as string; var p = saved is not null ? Json.Decode<Preferences>(saved) : new(); if(saved is not null && !saved.Contains("\"mp3MetadataMode\"",StringComparison.OrdinalIgnoreCase))p.Mp3MetadataMode=p.MusicBrainz?"after":"off"; if(saved is not null && !saved.Contains("\"thumbnailPreferenceVersion\"",StringComparison.OrdinalIgnoreCase)){p.KeepThumbnail=false;p.ThumbnailPreferenceVersion=1;} if (p.VideoHeight > 2160) p.VideoHeight = 2160; if (string.IsNullOrWhiteSpace(p.ReleaseRepository)) p.ReleaseRepository = new Preferences().ReleaseRepository; return p; }
    public void SavePreferences(Preferences p) { p.Validate(); using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "INSERT OR REPLACE INTO settings VALUES('preferences',$j)"; c.Parameters.AddWithValue("$j", Json.Encode(p)); c.ExecuteNonQuery(); }
    public void SaveGroup(DownloadGroup g) { lock (gate) { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "INSERT OR REPLACE INTO groups VALUES($id,$j)"; c.Parameters.AddWithValue("$id", g.Id); c.Parameters.AddWithValue("$j", Json.Encode(g)); c.ExecuteNonQuery(); } }
    public List<DownloadGroup> Groups() { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "SELECT json FROM groups"; using var r = c.ExecuteReader(); var a = new List<DownloadGroup>(); while (r.Read()) a.Add(Json.Decode<DownloadGroup>(r.GetString(0))); return a; }
    public List<TagLibrarySource> TagLibrarySources()
    {
        lock(gate){using var db=Open();using var c=db.CreateCommand();c.CommandText="SELECT path,isFolder FROM tag_library_sources ORDER BY rowid";using var r=c.ExecuteReader();var sources=new List<TagLibrarySource>();while(r.Read())sources.Add(new(r.GetString(0),r.GetBoolean(1)));return sources;}
    }
    public void RememberTagLibrarySources(IEnumerable<TagLibrarySource> sources)
    {
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();foreach(var source in sources){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO tag_library_sources(path,isFolder) VALUES($p,$f) ON CONFLICT(path) DO UPDATE SET isFolder=$f";c.Parameters.AddWithValue("$p",Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.Path)));c.Parameters.AddWithValue("$f",source.IsFolder);c.ExecuteNonQuery();}tx.Commit();}
    }
    public void MoveTagLibraryFile(string previous,string current)
    {
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="UPDATE OR REPLACE tag_library_sources SET path=$new WHERE path=$old AND isFolder=0";c.Parameters.AddWithValue("$old",Path.GetFullPath(previous));c.Parameters.AddWithValue("$new",Path.GetFullPath(current));c.ExecuteNonQuery();tx.Commit();}
    }
    public void ForgetTagLibrarySource(TagLibrarySource source)
    {
        lock(gate){using var db=Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM tag_library_sources WHERE path=$p AND isFolder=$f";c.Parameters.AddWithValue("$p",Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.Path)));c.Parameters.AddWithValue("$f",source.IsFolder);c.ExecuteNonQuery();}
    }
    public void Checkpoint() { using var db = Open(); using var c = db.CreateCommand(); c.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)"; c.ExecuteNonQuery(); }
}
