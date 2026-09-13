param([ValidateSet('win-x64','win-arm64','win-x86')][string]$Runtime='win-x64',[switch]$SkipTools)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root "artifacts/windows-$Runtime"
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach($project in @('Windows','NativeHost')) {
    & dotnet publish (Join-Path $root "src/$project/$project.csproj") -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $output
    if($LASTEXITCODE -ne 0){throw "$project publish failed"}
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'updater.ps1') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output -Force
if(-not $SkipTools){& (Join-Path $PSScriptRoot 'Prepare-Tools.ps1') -Destination $output -Runtime $Runtime}
Write-Host "Windows build: $output"
