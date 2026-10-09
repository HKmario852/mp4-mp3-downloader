using Omni.Core;
if(args[0]=="commands"){
    var profiles=new[]{("opus","opus","cbr"),("m4a","m4a","cbr"),("mp3-320","mp3","cbr"),("mp3-v0","mp3","v0"),("flac","flac","cbr")};
    Console.WriteLine(Json.Encode(profiles.Select(p=>new{name=p.Item1,format=p.Item2,args=AudioPipeline.Arguments(p.Item2,new(){Mp3Encoding=p.Item3},320)})));return;
}
if(args[0]=="validate"){await AudioPipeline.Validate(args[1],args[2],double.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture),CancellationToken.None);return;}
throw new ArgumentException("Use commands or validate file ffmpeg duration");
