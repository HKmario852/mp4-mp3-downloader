using Omni.Core;
using Xunit;
namespace Omni.Tests;
public sealed class SizeEstimates
{
    [Fact] public void Mp3TracksBitrateAndDuration() { var info=new MediaInfo("song","","",null,120); Assert.Equal(4_800_000,SizeEstimator.Estimate(info,DownloadMode.Mp3,320)); Assert.Equal(1_920_000,SizeEstimator.Estimate(info,DownloadMode.Mp3,128)); }
    [Fact] public void Mp4AddsAudioAndHonorsHeight() { var info=new MediaInfo("video","","",null,120,[new("mp4",1080,true,false,30_000_000,2000),new("mp4",720,true,false,12_000_000,800),new("m4a",null,false,true,2_000_000,128)]); Assert.Equal(14_000_000,SizeEstimator.Estimate(info,DownloadMode.Mp4,720)); Assert.Equal(32_000_000,SizeEstimator.Estimate(info,DownloadMode.Mp4,1080)); }
    [Fact] public void MuxedSizeDoesNotCountAudioTwice() { var info=new MediaInfo("video","","",null,120,[new("mp4",720,true,true,12_000_000,800),new("m4a",null,false,true,2_000_000,128)]); Assert.Equal(12_000_000,SizeEstimator.Estimate(info,DownloadMode.Mp4,720)); }
    [Fact] public void UnknownStaysUnknownAndBitrateCanEstimate() { Assert.Null(SizeEstimator.Estimate(new("live","","",null,null),DownloadMode.Mp3,320)); Assert.Null(SizeEstimator.Estimate(new("video","","",null,120),DownloadMode.Mp4,1080)); Assert.Equal(15_000_000,SizeEstimator.Estimate(new("video","","",null,120,[new("mp4",720,true,true,null,1000)]),DownloadMode.Mp4,720)); }
}
