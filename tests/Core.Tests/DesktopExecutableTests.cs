using Omni.Core;
using Xunit;
namespace Omni.Tests;

public sealed class DesktopExecutableTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"omni-launch-"+Guid.NewGuid());
    public DesktopExecutableTests()=>Directory.CreateDirectory(root);
    public void Dispose()=>Directory.Delete(root,true);
    [Fact] public void BrandedAppWinsWhenLegacyAndCurrentFilesCoexist()
    {
        File.WriteAllText(Path.Combine(root,"App.exe"),"legacy");
        File.WriteAllText(Path.Combine(root,"OMNI.exe"),"current");
        Assert.Equal(Path.Combine(root,"OMNI.exe"),DesktopExecutable.Find(root));
    }
    [Fact] public void LegacyInstallCanStillBeLaunchedByAnUpdatedBridge()
    {
        File.WriteAllText(Path.Combine(root,"App.exe"),"legacy");
        Assert.Equal(Path.Combine(root,"App.exe"),DesktopExecutable.Find(root));
    }
    [Fact] public void MissingAppDoesNotLaunchAnUnrelatedExecutable()
    {
        File.WriteAllText(Path.Combine(root,"other.exe"),"other");
        Assert.Throws<FileNotFoundException>(()=>DesktopExecutable.Find(root));
    }
}
