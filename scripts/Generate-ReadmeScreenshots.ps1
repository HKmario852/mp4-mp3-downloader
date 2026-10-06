param([string]$FfmpegPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $FfmpegPath){$FfmpegPath=Join-Path $root 'artifacts/windows-win-x64/ffmpeg.exe'}
if(-not(Test-Path -LiteralPath $FfmpegPath)){throw 'Provide -FfmpegPath or run scripts/Build-Windows.ps1 first.'}
$work=Join-Path $root ('artifacts/readme-demo-'+[Guid]::NewGuid().ToString('N'))
$output=Join-Path $root 'docs/screenshots'
& dotnet run --project (Join-Path $root 'tests/Readme.Screenshots') -c Release -- $work $output ([IO.Path]::GetFullPath($FfmpegPath))
if($LASTEXITCODE -ne 0){throw 'README screenshot generation failed'}
Write-Host "Screenshots: $output"
