namespace Omni.Core;

public static class LibraryActions
{
    public static bool InQueue(DownloadJob job) => job.State is JobState.PendingChoice or JobState.Queued or JobState.Analyzing or JobState.Downloading or JobState.Processing or JobState.RetryWait or JobState.Paused;
    public static void DeleteFile(Store store, string id, string expectedPath, Action<string> recycle)
    {
        var job = store.Load(true).SingleOrDefault(j => j.Id == id);
        if (job is null || job.IsGroupRoot || job.State != JobState.Completed || job.FilePath != expectedPath) throw new IOException("檔案資料已變更，請重新選取。");
        if (!File.Exists(expectedPath)) throw new FileNotFoundException("檔案已移動或刪除，原本的歷史紀錄仍然保留。", expectedPath);
        recycle(expectedPath);
        if (File.Exists(expectedPath)) throw new IOException("檔案未有刪除，歷史紀錄已保留。");
        var affected = store.Load(true).Where(j => j.State == JobState.Completed && string.Equals(j.FilePath, expectedPath, StringComparison.OrdinalIgnoreCase)).Select(j => j.Id).ToArray();
        store.ClearHistory(affected);
    }
}
