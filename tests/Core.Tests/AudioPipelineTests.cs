using Omni.Core;
using Xunit;
namespace Omni.Tests;

public sealed class AudioPipelineTests
{
    static string Fixture(string name)=>Path.Combine(AppContext.BaseDirectory,"audio",name);
    [Theory][InlineData("opus")][InlineData("m4a")]
    public void NativeFormatsCannotFallBackToLossyEncoding(string format){
        var args=AudioPipeline.Arguments(format,new(),320);
        Assert.DoesNotContain("--audio-quality",args);
        Assert.Contains("ExtractAudio+ffmpeg_o:-c:a copy",args);
        Assert.DoesNotContain("/bestaudio/best",AudioPipeline.Selector(format));
    }
    [Fact] public void V0AndId3PreferencesAreExplicit(){
        var p=new Preferences{Mp3Encoding="v0",Id3Version=4};p.Validate();
        var args=AudioPipeline.Arguments("mp3",p,320);
        Assert.Equal("0",args[args.IndexOf("--audio-quality")+1]);
        Assert.Contains("Metadata+ffmpeg_o:-id3v2_version 4",args);
        Assert.Equal(3,new Preferences().Id3Version);
    }
    [Theory][InlineData("mp3")][InlineData("opus")][InlineData("m4a")][InlineData("flac")]
    public async Task TagsArtworkAndUndoPreserveUnselectedValues(string extension){
        var root=Path.Combine(Path.GetTempPath(),"omni-audio-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try{
            var path=Path.Combine(root,"tone."+extension);File.Copy(Fixture("tone."+extension),path);
            var service=new TagLibAudioTagService();var before=await service.ReadTagsAsync(path);
            var store=new Store(Path.Combine(root,"data"));var song=new DownloadJob{FilePath=path,OutputFormat=extension,Mode=DownloadMode.Mp3,State=JobState.Completed};store.Save(song);
            var original=await TagReview.Hash(path);
            var fields=new Dictionary<string,string>{{"TIT2","測試 · 日本語 🎵"},{"TPE2","Demo album artist"},{"TRCK","2/9"},{"TPOS","1/2"},{"TYER","2026"},{"TDOR",extension=="mp3"?"2020":"2020-02-03"},{"TPUB","Demo label"},{"TSRC","HKAAA2600001"},{"TXXX:MusicBrainz Recording Id","cb39b5d8-ebb8-4bad-9f17-9d952108ecb7"},{"TXXX:MusicBrainz Album Id","885b1ba8-65f0-476b-939c-704db7a696de"},{"TSOT","Demo sort"},{"TSOP","Demo artist sort"}};
            var art=new Cover(File.ReadAllBytes(Fixture("cover.png")),"image/png","Demo cover",3);
            var undo=await TagReviewUndo.Apply(store,song,fields,art,true,original);
            var after=await service.ReadTagsAsync(path);
            foreach(var pair in fields)Assert.Equal(pair.Value,after.Fields[pair.Key]);
            Assert.Equal(before.Fields["TPE1"],after.Fields["TPE1"]);Assert.Equal(before.Fields["TALB"],after.Fields["TALB"]);
            Assert.Equal(art.Bytes,Assert.Single(after.Covers!).Bytes);
            if(extension=="mp3"){using var independent=TagLib.File.Create(path);Assert.Equal(fields["TXXX:MusicBrainz Recording Id"],independent.Tag.MusicBrainzTrackId);}
            var encodedBefore=await PacketHashes(Fixture("tone."+extension));var encodedAfter=await PacketHashes(path);
            if(encodedBefore is not null)Assert.Equal(encodedBefore,encodedAfter);
            await undo!.Restore(store,song);Assert.Equal(original,await TagReview.Hash(path));
        }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
    }
    // Optional external check supplements portable tag/undo tests on builds with FFprobe.
    static async Task<string?> PacketHashes(string path){var probe=Environment.GetEnvironmentVariable("OMNI_TEST_FFPROBE");if(string.IsNullOrEmpty(probe))return null;return await ProcessRunner.Run(probe,["-v","error","-select_streams","a:0","-show_packets","-show_data_hash","sha256","-show_entries","packet=data_hash","-of","csv=p=0",path],null,CancellationToken.None);}
    [Fact] public async Task NewMp3VersionChoiceDoesNotChangeAudio(){
        var root=Path.Combine(Path.GetTempPath(),"omni-version-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try{foreach(var version in new[]{3,4}){var path=Path.Combine(root,version+".mp3");File.Copy(Fixture("tone.mp3"),path);var previous=await PacketHashes(path);await AudioTagDocument.SetNewMp3Version(path,version);Assert.Equal(version,(int)Id3Document.Read(path).Version);if(previous is not null)Assert.Equal(previous,await PacketHashes(path));}}
        finally{Directory.Delete(root,true);}
    }
    [Fact] public void FolderImportFindsAllSupportedFormatsOnce(){
        var result=TagLibrary.Read([TagLibrary.Source(Path.GetDirectoryName(Fixture("tone.mp3"))!),TagLibrary.Source(Fixture("tone.m4a"))]);
        Assert.Empty(result.Unavailable);Assert.Equal(4,result.Songs.Length);
        Assert.All(result.Songs,s=>Assert.Equal("Demo artist",s.Artist));
    }
}
