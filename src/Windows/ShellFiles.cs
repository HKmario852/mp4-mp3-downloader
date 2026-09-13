using System.IO;
using System.Runtime.InteropServices;
namespace Omni.Windows;

public static class ShellFiles
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr item, uint attributes, out uint result);
    [DllImport("shell32.dll")] static extern int SHOpenFolderAndSelectItems(IntPtr item, uint count, IntPtr children, uint flags);
    public static void Select(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("檔案已移動或刪除，請確認原本的儲存位置。", path);
        Marshal.ThrowExceptionForHR(SHParseDisplayName(Path.GetFullPath(path), IntPtr.Zero, out var item, 0, out _));
        try { Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0)); }
        finally { Marshal.FreeCoTaskMem(item); }
    }
}
