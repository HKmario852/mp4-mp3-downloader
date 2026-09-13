param([Parameter(Mandatory)][string]$Destination,[ValidateSet('win-x64','win-arm64','win-x86')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$Destination=[IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $Destination -Force|Out-Null
$headers=@{'User-Agent'='OmniDownloader-build/0.1.0'}
function Download-Verified($Asset,[string]$Output) {
    if(-not $Asset.digest -or $Asset.digest -notmatch '^sha256:([a-fA-F0-9]{64})$'){throw "GitHub did not supply a SHA256 for $($Asset.name); do not download unverified binaries."}
    $expected=$Matches[1]
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $Output
    if((Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash -ne $expected){throw "SHA256 mismatch for $($Asset.name)"}
}
$release=Invoke-RestMethod -Uri 'https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest' -Headers $headers
$ytdlp= switch($Runtime){'win-arm64'{'yt-dlp_arm64.exe'}'win-x86'{'yt-dlp_x86.exe'}default{'yt-dlp.exe'}}
$asset=$release.assets|Where-Object name -eq $ytdlp
if(-not $asset){throw 'No matching yt-dlp release binary'}
Download-Verified $asset (Join-Path $Destination 'yt-dlp.exe')
if($Runtime -ne 'win-x64'){throw 'Provide matching native FFmpeg/ffprobe and JS runtime builds for ARM64/x86. Architecture emulation is intentionally not silently accepted.'}
$ff=Invoke-RestMethod -Uri 'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest' -Headers $headers
$ffasset=$ff.assets|Where-Object name -match '^ffmpeg-master-latest-win64-gpl\.zip$'|Select-Object -First 1
if(-not $ffasset){throw 'Matching FFmpeg build not found'}
$zip=Join-Path $Destination 'ffmpeg-tools.zip'
Download-Verified $ffasset $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{foreach($name in @('ffmpeg.exe','ffprobe.exe')){$entry=$archive.Entries|Where-Object Name -eq $name|Select-Object -First 1;if(-not $entry){throw "$name missing"};[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $Destination $name),$true)}}finally{$archive.Dispose()}
Remove-Item -LiteralPath $zip
$deno=Invoke-RestMethod -Uri 'https://api.github.com/repos/denoland/deno/releases/latest' -Headers $headers
$denoasset=$deno.assets|Where-Object name -eq 'deno-x86_64-pc-windows-msvc.zip'|Select-Object -First 1
$zip=Join-Path $Destination 'deno-tools.zip';Download-Verified $denoasset $zip
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{$entry=$archive.Entries|Where-Object Name -eq 'deno.exe'|Select-Object -First 1;[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $Destination 'deno.exe'),$true)}finally{$archive.Dispose()}
Remove-Item -LiteralPath $zip
@{ytDlp=$release.tag_name;ffmpeg=$ff.tag_name;deno=$deno.tag_name;retrievedAt=[DateTime]::UtcNow.ToString('O');source='GitHub release asset digests verified with SHA256'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Destination 'tool-versions.json') -Encoding UTF8
Write-Host 'yt-dlp, FFmpeg, ffprobe and Deno ready; release SHA256 checks passed.'
