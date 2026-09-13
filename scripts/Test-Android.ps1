param([Parameter(Mandatory)][string]$DeviceId,[Parameter(Mandatory)][string]$AndroidHome,[string]$BuildDirectory=(Join-Path $env:USERPROFILE '.cache/omni-android-build'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$adb=Join-Path $AndroidHome 'platform-tools/adb.exe'
& $adb -s $DeviceId install -r -t (Join-Path $root 'artifacts/OmniDownloader-universal-debug.apk')
if($LASTEXITCODE -ne 0){throw 'App installation failed'}
& $adb -s $DeviceId install -r -t (Join-Path $BuildDirectory 'app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk')
if($LASTEXITCODE -ne 0){throw 'Instrumentation installation failed'}
$result=& $adb -s $DeviceId shell am instrument -w -r io.hkmario.omni.test/androidx.test.runner.AndroidJUnitRunner 2>&1
$out=Join-Path $root 'artifacts/android-native-test.txt'
$result|Set-Content -LiteralPath $out -Encoding UTF8
$result|Write-Output
if(($result -join "`n") -notmatch 'OK \(1 test\)'){throw 'Android native download test failed'}
