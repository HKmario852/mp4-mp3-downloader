param([string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'artifacts'
$windows=Join-Path $out "windows-$Runtime"
foreach($name in @('App.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe','deno.exe','updater.ps1')){if(-not(Test-Path -LiteralPath (Join-Path $windows $name))){throw "Missing release file: $name"}}
foreach($name in @('LICENSE','THIRD-PARTY.md','README.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $windows -Force}
Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $windows -Force}
$portableReadme=Join-Path $windows 'README.md'
[IO.File]::WriteAllText($portableReadme,[IO.File]::ReadAllText($portableReadme).Replace('](docs/',']('))
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Make-Zip([string]$Zip,[string]$Base,[IO.FileInfo[]]$Files){
    if(Test-Path -LiteralPath $Zip){Remove-Item -LiteralPath $Zip}
    $archive=[IO.Compression.ZipFile]::Open($Zip,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($file in $Files){$name=$file.FullName.Substring($Base.Length).TrimStart('\','/').Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
}
$zip=Join-Path $out ('OmniDownloader-windows-'+$Runtime.Replace('win-','')+'.zip')
Make-Zip $zip $windows @(Get-ChildItem -LiteralPath $windows -File | Where-Object {$_.Name -ne 'native-host.json'})
Make-Zip (Join-Path $out 'OmniDownloader-browser-extension.zip') (Join-Path $root 'extension') @(Get-ChildItem -LiteralPath (Join-Path $root 'extension') -File)
$files=Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object {$_.FullName.Substring($root.Length) -notmatch '[\\/](\.git|\.tools|artifacts|bin|obj|build|\.gradle|__pycache__|node_modules|test-results|data)[\\/]' -and $_.Name -ne 'local.properties'}
Make-Zip (Join-Path $out 'OmniDownloader-source.zip') $root @($files)
Get-ChildItem -LiteralPath $out -File | Where-Object {$_.Extension -in @('.zip','.apk')} | ForEach-Object { $hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $($_.Name)" } | Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding UTF8
Write-Host "Packages ready: $out"
