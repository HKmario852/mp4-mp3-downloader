using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using Omni.Core;
using static Omni.Windows.UiKit;

namespace Omni.Windows;

// Capture OLE data while Drop/Clipboard still owns it; await only after it is copied.
public sealed record ArtworkInput(byte[]? Bytes=null,Uri? Url=null)
{
    public const int MaxBytes=32*1024*1024;
    static readonly HttpClient Http=new(new HttpClientHandler{UseCookies=false,MaxAutomaticRedirections=5}){Timeout=TimeSpan.FromSeconds(20)};
    static readonly string[] ImageExtensions=[".png",".jpg",".jpeg",".jfif",".bmp",".gif",".tif",".tiff",".webp",".avif"];
    static readonly string[] Formats=[DataFormats.FileDrop,"PNG","image/png",DataFormats.Bitmap,"FileContents",DataFormats.Html,"UniformResourceLocatorW","UniformResourceLocator","text/uri-list","text/x-moz-url",DataFormats.UnicodeText,DataFormats.Text];
    public static bool CanRead(System.Windows.IDataObject data)=>Formats.Any(f=>data.GetDataPresent(f));
    static object? Data(System.Windows.IDataObject data,string format){try{return data.GetData(format);}catch(Exception e)when(e is ExternalException or NotSupportedException or ArgumentException){return null;}}
    static bool ImagePath(string path){if(!File.Exists(path))return false;if(ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))return true;try{using var file=File.OpenRead(path);var header=new byte[32];var count=file.Read(header);return ImageBytes(header[..count]);}catch(IOException){return false;}}
    static byte[] Local(string path){if(new FileInfo(path).Length>MaxBytes)throw TooLarge();return File.ReadAllBytes(path);}
    static IOException TooLarge()=>new(T("封面不可超過 32 MB","Artwork cannot exceed 32 MB"));
    static byte[] Copy(Stream stream){var position=stream.CanSeek?stream.Position:0;try{if(stream.CanSeek)stream.Position=0;using var output=new MemoryStream();var buffer=new byte[65536];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+n>MaxBytes)throw TooLarge();output.Write(buffer,0,n);}return output.ToArray();}finally{if(stream.CanSeek)stream.Position=position;}}
    static byte[]? Binary(object? value)=>value switch{byte[] bytes when bytes.Length<=MaxBytes=>bytes,byte[]=>throw TooLarge(),Stream stream=>Copy(stream),Stream[] streams when streams.Length>0=>Copy(streams[0]),_=>null};
    static string? TextData(object? value,bool unicode=false){if(value is string s)return s;var bytes=Binary(value);return bytes is null?null:(unicode?Encoding.Unicode:Encoding.UTF8).GetString(bytes).TrimEnd('\0').TrimStart('\uFEFF');}
    public static ArtworkInput Capture(System.Windows.IDataObject data)
    {
        if(Data(data,DataFormats.FileDrop) is string[] paths&&paths.FirstOrDefault(ImagePath) is string path)return new(Local(path));
        foreach(var format in new[]{"PNG","image/png"})if(Binary(Data(data,format)) is {Length:>0} png)return new(png);
        if(Data(data,DataFormats.Bitmap) is BitmapSource bitmap){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=new MemoryStream();encoder.Save(output);if(output.Length>MaxBytes)throw TooLarge();return new(output.ToArray());}
        if(Data(data,DataFormats.Bitmap) is System.Drawing.Bitmap drawing){using var output=new MemoryStream();drawing.Save(output,System.Drawing.Imaging.ImageFormat.Png);if(output.Length>MaxBytes)throw TooLarge();return new(output.ToArray());}
        if(Binary(Data(data,"FileContents")) is {Length:>0} contents&&ImageBytes(contents))return new(contents);

        // Chrome/Brave frequently transfer an <img> fragment, not a local file or bitmap.
        var html=TextData(Data(data,DataFormats.Html));
        if(html is not null&&html.Length<=2_000_000){
            var image=Regex.Match(html,@"<img\b[^>]*?(?<![\w:-])src\s*=\s*(?:""(?<src>[^""]+)""|'(?<src>[^']+)'|(?<src>[^\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
            if(image.Success){var source=WebUtility.HtmlDecode(image.Groups["src"].Value);var baseMatch=Regex.Match(html,@"(?m)^SourceURL:(.+)$",RegexOptions.None,TimeSpan.FromSeconds(1));Uri? basis=null;if(baseMatch.Success)Uri.TryCreate(baseMatch.Groups[1].Value.Trim(),UriKind.Absolute,out basis);if(FromText(source,basis) is { } result)return result;}
        }
        foreach(var format in new[]{"UniformResourceLocatorW","UniformResourceLocator","text/uri-list","text/x-moz-url",DataFormats.UnicodeText,DataFormats.Text}){
            var text=TextData(Data(data,format),format is "UniformResourceLocatorW" or "text/x-moz-url");
            if(text is null)continue;
            foreach(var line in text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Where(s=>!s.StartsWith('#')))if(FromText(line.Trim().Trim('"')) is { } result)return result;
        }
        throw new IOException(T("拖放內容未提供圖片或圖片網址。請拖放圖片本身，或先另存圖片再拖入。","The drop contains no image or image URL. Drag the image itself, or save it and drag the file here."));
    }
    static ArtworkInput? FromText(string text,Uri? basis=null)
    {
        if(ImagePath(text))return new(Local(text));
        if(text.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)){
            var comma=text.IndexOf(',');if(comma<0||!text[..comma].EndsWith(";base64",StringComparison.OrdinalIgnoreCase))return null;
            if(text.Length>MaxBytes*4L/3+1024)throw TooLarge();var bytes=Convert.FromBase64String(text[(comma+1)..]);if(bytes.Length>MaxBytes)throw TooLarge();return new(bytes);
        }
        if(text.StartsWith("//"))text="https:"+text;
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri)&&(basis is null||!Uri.TryCreate(basis,text,out uri)))return null;
        if(uri.IsFile&&ImagePath(uri.LocalPath))return new(Local(uri.LocalPath));
        return uri.Scheme is "http" or "https"&&string.IsNullOrEmpty(uri.UserInfo)?new(Url:uri):null;
    }
    static bool ImageBytes(byte[] b)=>b.Length>12&&(
        b[0]==137&&b[1]==80||b[0]==255&&b[1]==216||b[0]==66&&b[1]==77||
        Encoding.ASCII.GetString(b,0,3)=="GIF"||Encoding.ASCII.GetString(b,8,4)=="WEBP"||
        Encoding.ASCII.GetString(b,4,4)=="ftyp"||b[0]==73&&b[1]==73||b[0]==77&&b[1]==77);
    public async Task<byte[]> Read(string ffmpeg,CancellationToken ct,HttpClient? client=null)
    {
        byte[] bytes;
        if(Bytes is not null)bytes=Bytes;
        else {
            using var request=new HttpRequestMessage(HttpMethod.Get,Url!);request.Headers.UserAgent.ParseAdd("OmniDownloader/"+(typeof(ArtworkInput).Assembly.GetName().Version?.ToString(3)??"0"));
            using var response=await (client??Http).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentType?.MediaType is "text/html" or "application/xhtml+xml")throw new IOException(T("拖入嘅係網頁連結，未提供圖片本身。請右鍵複製圖片，或另存圖片後再拖入。","The drop links to a web page rather than an image. Copy the image itself, or save it and drag the file here."));
            if(response.Content.Headers.ContentLength>MaxBytes)throw TooLarge();
            await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[65536];int n;
            while((n=await input.ReadAsync(buffer,ct))>0){if(output.Length+n>MaxBytes)throw TooLarge();await output.WriteAsync(buffer.AsMemory(0,n),ct);}bytes=output.ToArray();
        }
        if(bytes.Length>MaxBytes)throw TooLarge();
        var webp=bytes.Length>12&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP";
        var avif=bytes.Length>16&&Encoding.ASCII.GetString(bytes,4,4)=="ftyp"&&Encoding.ASCII.GetString(bytes,8,8).Contains("avi",StringComparison.Ordinal);
        if(!webp&&!avif)return bytes;
        var temp=Path.Combine(Path.GetTempPath(),"omni-cover-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            var input=Path.Combine(temp,webp?"image.webp":"image.avif");var output=Path.Combine(temp,"image.png");await File.WriteAllBytesAsync(input,bytes,ct);
            await ProcessRunner.Run(ffmpeg,["-nostdin","-loglevel","error","-i",input,"-frames:v","1",output],null,ct);
            if(new FileInfo(output).Length>MaxBytes)throw TooLarge();return await File.ReadAllBytesAsync(output,ct);
        }finally{Directory.Delete(temp,true);}
    }
}
