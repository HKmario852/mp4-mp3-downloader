param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$master=[Drawing.Image]::FromFile((Join-Path $root 'assets/branding/omni-master.png'))
function Png-Bytes([int]$Size,[double]$Scale=1) {
    $bitmap=[Drawing.Bitmap]::new($Size,$Size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    $stream=[IO.MemoryStream]::new()
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $side=[int][Math]::Round($Size*$Scale);$offset=[int](($Size-$side)/2)
        $graphics.DrawImage($master,[Drawing.Rectangle]::new($offset,$offset,$side,$side))
        $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }finally{$stream.Dispose();$graphics.Dispose();$bitmap.Dispose()}
}
function Save-Png([string]$Relative,[int]$Size,[double]$Scale=1) {
    $path=Join-Path $root $Relative
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force|Out-Null
    [IO.File]::WriteAllBytes($path,(Png-Bytes $Size $Scale))
}
try {
    Save-Png 'src/Windows/Assets/brand.png' 256
    Save-Png 'android/app/src/main/res/drawable-nodpi/omni_mark.png' 256
    foreach($size in @(16,32,48,128)){Save-Png "extension/icon$size.png" $size}
    foreach($density in @(@('mdpi',1),@('hdpi',1.5),@('xhdpi',2),@('xxhdpi',3),@('xxxhdpi',4))) {
        $folder="android/app/src/main/res/mipmap-$($density[0])"
        Save-Png "$folder/ic_launcher.png" ([int](48*$density[1]))
        # 70% canvas scale puts the mark inside Android's central 66/108 safe zone.
        Save-Png "$folder/ic_launcher_foreground.png" ([int](108*$density[1])) 0.70
    }
    $sizes=@(16,20,24,32,40,48,64,128,256)
    $frames=[Collections.Generic.List[byte[]]]::new()
    foreach($size in $sizes){$frames.Add((Png-Bytes $size))}
    $file=[IO.File]::Create((Join-Path $root 'src/Windows/Assets/omni.ico'))
    $writer=[IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
        $offset=6+16*$sizes.Count
        for($i=0;$i -lt $sizes.Count;$i++) {
            $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
            $writer.Write([byte]$dimension);$writer.Write([byte]$dimension)
            $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset)
            $offset+=$frames[$i].Length
        }
        foreach($frame in $frames){$writer.Write([byte[]]$frame)}
    }finally{$writer.Dispose()}
}finally{$master.Dispose()}
Write-Host 'Exported Windows ICO, app marks, Android adaptive layers, and browser PNG sizes.'
