namespace Omni.Core;

public static class DesktopExecutable
{
    public static string Find(string directory)
    {
        // Prefer the branded executable when both old and new installs coexist.
        foreach(var name in new[]{"OMNI.exe","App.exe"})
        {
            var path=Path.Combine(directory,name);
            if(File.Exists(path))return path;
        }
        throw new FileNotFoundException("OMNI.exe was not found. Keep the native host beside the desktop application.");
    }
}
