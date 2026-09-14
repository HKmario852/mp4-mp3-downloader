using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Shell;
using Microsoft.Win32;
using Omni.Core;
namespace Omni.Windows;
public static class DesktopIntegration
{
    public static void ApplyStartup(Preferences p){using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(p.StartAtLogin)key.SetValue("OmniDownloader",$"\"{Environment.ProcessPath}\""+(p.StartMinimized?" --minimized":""));else key.DeleteValue("OmniDownloader",false);}
    public static bool NetworkAllowed(Preferences p){if(!NetworkInterface.GetIsNetworkAvailable())return false;if(p.AllowedNetwork=="any")return true;return NetworkInterface.GetAllNetworkInterfaces().Any(n=>n.OperationalStatus==OperationalStatus.Up&&(p.AllowedNetwork=="wifi"?n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211:n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet));}
    public static void PlaySound(Preferences p){try{(p.SoundName switch{"asterisk"=>System.Media.SystemSounds.Asterisk,"exclamation"=>System.Media.SystemSounds.Exclamation,_=>System.Media.SystemSounds.Beep}).Play();}catch{}}
    public static void Progress(Window w,Downloader engine){w.TaskbarItemInfo??=new TaskbarItemInfo();var jobs=engine.Jobs.Where(j=>j.State is JobState.Downloading or JobState.Processing or JobState.Analyzing).ToArray();w.TaskbarItemInfo.ProgressState=!engine.Settings.TaskbarProgress||jobs.Length==0?TaskbarItemProgressState.None:jobs.All(j=>j.TotalBytes is >0)?TaskbarItemProgressState.Normal:TaskbarItemProgressState.Indeterminate;w.TaskbarItemInfo.ProgressValue=jobs.Length==0?0:jobs.Average(j=>j.Progress)/100;}
    public static void Completion(DownloadJob j,Preferences p){if(p.Sound&&!p.IsQuiet(DateTime.Now))PlaySound(p);if(j.FilePath is null)return;try{if(p.CompletionAction=="file")Process.Start(new ProcessStartInfo(j.FilePath){UseShellExecute=true});else if(p.CompletionAction=="folder")ShellFiles.Select(j.FilePath);}catch{}}
}
