param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'artifacts'
$windows=Join-Path $out "windows-$Runtime"
foreach($name in @('OMNI.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe','updater.ps1')){if(-not(Test-Path -LiteralPath (Join-Path $windows $name))){throw "Missing release file: $name"}}
foreach($name in @('LICENSE','THIRD-PARTY.md','README.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $windows -Force}
Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $windows -Force}
# Existing installed updaters flatten ZIP paths. Ship each basename exactly once.
Get-ChildItem -LiteralPath (Join-Path $root 'docs/licenses') -File | Copy-Item -Destination $windows -Force
$version=([xml](Get-Content -LiteralPath (Join-Path $root 'src/Windows/Windows.csproj') -Raw)).Project.PropertyGroup.Version
$portableReadme=Join-Path $windows 'README.md'
$readme=[IO.File]::ReadAllText($portableReadme)
$readme=$readme.Replace('](docs/',"](https://github.com/HKmario852/mp4-mp3-downloader/blob/v$version/docs/")
$readme=$readme.Replace('href="docs/',"href=`"https://github.com/HKmario852/mp4-mp3-downloader/blob/v$version/docs/")
$readme=$readme.Replace('src="docs/',"src=`"https://raw.githubusercontent.com/HKmario852/mp4-mp3-downloader/v$version/docs/")
[IO.File]::WriteAllText($portableReadme,$readme)
$portableNotices=Join-Path $windows 'THIRD-PARTY.md'
[IO.File]::WriteAllText($portableNotices,[IO.File]::ReadAllText($portableNotices).Replace('](docs/licenses/',']('))
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Make-Zip([string]$Zip,[string]$Base,[IO.FileInfo[]]$Files){
    if(Test-Path -LiteralPath $Zip){Remove-Item -LiteralPath $Zip}
    $archive=[IO.Compression.ZipFile]::Open($Zip,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($file in $Files){$name=$file.FullName.Substring($Base.Length).TrimStart('\','/').Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
}
$zip=Join-Path $out ('OmniDownloader-windows-'+$Runtime.Replace('win-','')+'.zip')
$releaseNames=@('OMNI.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe','updater.ps1','LICENSE','THIRD-PARTY.md','README.md','Deno-LICENSE.txt','DotNet-NOTICES.txt','FFmpeg-LICENSE.txt','tool-versions.json')
$releaseNames+=@(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File | ForEach-Object Name)
$releaseNames+=@(Get-ChildItem -LiteralPath (Join-Path $root 'docs/licenses') -File | ForEach-Object Name)
$releaseNames=@($releaseNames|Select-Object -Unique)
$releaseFiles=@($releaseNames|ForEach-Object {Get-Item -LiteralPath (Join-Path $windows $_)})
if(@($releaseFiles|Group-Object Name|Where-Object Count -gt 1).Count -gt 0){throw 'Duplicate Windows ZIP basenames are incompatible with installed updaters'}
Make-Zip $zip $windows $releaseFiles
Make-Zip (Join-Path $out 'OmniDownloader-browser-extension.zip') (Join-Path $root 'extension') @(Get-ChildItem -LiteralPath (Join-Path $root 'extension') -File)
# Package only tracked source; local credentials and runtime files cannot enter the archive.
$files=@(& git -C $root -c core.quotepath=false ls-files | ForEach-Object {Get-Item -LiteralPath (Join-Path $root $_)})
if($LASTEXITCODE -ne 0){throw 'Unable to list tracked source files'}
Make-Zip (Join-Path $out 'OmniDownloader-source.zip') $root @($files)
@($zip,(Join-Path $out 'OmniDownloader-browser-extension.zip'),(Join-Path $out 'OmniDownloader-source.zip'),(Join-Path $out 'OmniDownloader-universal-debug.apk')) | Where-Object {Test-Path -LiteralPath $_} | ForEach-Object {Get-Item -LiteralPath $_} | ForEach-Object { $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $($_.Name)" } | Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding UTF8
Write-Host "Packages ready: $out"
