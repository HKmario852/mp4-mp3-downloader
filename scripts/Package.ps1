param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'artifacts'
$windows=Join-Path $out "windows-$Runtime"
foreach($name in @('OMNI.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe','updater.ps1')){if(-not(Test-Path -LiteralPath (Join-Path $windows $name))){throw "Missing release file: $name"}}
foreach($name in @('LICENSE','THIRD-PARTY.md','README.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $windows -Force}
Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $windows -Force}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $windows -Recurse -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Make-Zip([string]$Zip,[string]$Base,[IO.FileInfo[]]$Files){
    if(Test-Path -LiteralPath $Zip){Remove-Item -LiteralPath $Zip}
    $archive=[IO.Compression.ZipFile]::Open($Zip,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($file in $Files){$name=$file.FullName.Substring($Base.Length).TrimStart('\','/').Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
}
$zip=Join-Path $out ('OmniDownloader-windows-'+$Runtime.Replace('win-','')+'.zip')
$releaseFiles=@(Get-ChildItem -LiteralPath $windows -File | Where-Object {$_.Name -ne 'native-host.json' -and $_.Name -notmatch '^App\.(exe|dll|deps\.json|runtimeconfig\.json|pdb)$'})
foreach($directory in @('docs','licenses')){$releaseFiles+=@(Get-ChildItem -LiteralPath (Join-Path $windows $directory) -Recurse -File)}
Make-Zip $zip $windows $releaseFiles
Make-Zip (Join-Path $out 'OmniDownloader-browser-extension.zip') (Join-Path $root 'extension') @(Get-ChildItem -LiteralPath (Join-Path $root 'extension') -File)
# Package only tracked source; local credentials and runtime files cannot enter the archive.
$files=@(& git -C $root -c core.quotepath=false ls-files | ForEach-Object {Get-Item -LiteralPath (Join-Path $root $_)})
if($LASTEXITCODE -ne 0){throw 'Unable to list tracked source files'}
Make-Zip (Join-Path $out 'OmniDownloader-source.zip') $root @($files)
@($zip,(Join-Path $out 'OmniDownloader-browser-extension.zip'),(Join-Path $out 'OmniDownloader-source.zip'),(Join-Path $out 'OmniDownloader-universal-debug.apk')) | Where-Object {Test-Path -LiteralPath $_} | ForEach-Object {Get-Item -LiteralPath $_} | ForEach-Object { $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $($_.Name)" } | Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding UTF8
Write-Host "Packages ready: $out"
