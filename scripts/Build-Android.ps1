param([string]$JavaHome=$env:JAVA_HOME,[string]$AndroidHome=$env:ANDROID_HOME,[string]$BuildDirectory=(Join-Path $env:USERPROFILE '.cache/omni-android-build'))
$ErrorActionPreference='Stop'
if($JavaHome){$env:JAVA_HOME=$JavaHome};if($AndroidHome){$env:ANDROID_HOME=$AndroidHome}
$root=Split-Path $PSScriptRoot -Parent
$source=Join-Path $root 'android'
$BuildDirectory=[IO.Path]::GetFullPath($BuildDirectory)
New-Item -ItemType Directory -Path $BuildDirectory -Force|Out-Null
# Windows Android tools and forked JVM tests need an ASCII build path.
Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object {$_.FullName.Substring($source.Length) -notmatch '[\\/](build|\.gradle)[\\/]' -and $_.Name -ne 'local.properties'} | ForEach-Object {
    $relative=$_.FullName.Substring($source.Length).TrimStart('\','/')
    $destination=Join-Path $BuildDirectory $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Push-Location $BuildDirectory
try{& .\gradlew.bat assembleDebug assembleDebugAndroidTest testDebugUnitTest --console=plain --no-daemon;if($LASTEXITCODE -ne 0){throw 'Android build failed'};$out=Join-Path $root 'artifacts';New-Item -ItemType Directory -Path $out -Force|Out-Null;Copy-Item -LiteralPath 'app/build/outputs/apk/debug/app-debug.apk' -Destination (Join-Path $out 'OmniDownloader-universal-debug.apk') -Force;$reports=Join-Path $out 'android-test-results';New-Item -ItemType Directory -Path $reports -Force|Out-Null;Copy-Item -Path 'app/build/test-results/testDebugUnitTest/*.xml' -Destination $reports -Force}finally{Pop-Location}
