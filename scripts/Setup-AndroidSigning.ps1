# Points OMNI's Android release builds at the signing key that installed copies already use: this PC's Android
# debug keystore, which signed every Android build up to 0.2.20 (0.2.22 and later as release builds). Android only
# installs an update signed with the same key, so this key must stay the same.
#
# Run once on the PC that builds releases, from the repository folder:
#   powershell -ExecutionPolicy Bypass -File scripts\Setup-AndroidSigning.ps1
# Add -UploadSecrets to also store it as GitHub Actions secrets for signed CI builds (needs `gh auth login`).
#
# BACK UP %USERPROFILE%\.android\debug.keystore: if it is lost, installed copies can no longer update.
param([switch]$UploadSecrets)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repo = 'HKmario852/mp4-mp3-downloader'
$keystore = Join-Path $HOME '.android\debug.keystore'
$properties = Join-Path $root 'android/key.properties'
# SHA-256 of the certificate installed copies are signed with.
$expected = '0CCFB3F2D2245BB084C2339997FA96110A0574EB7B295235126CC10C6C21CE19'

if (-not (Test-Path $keystore)) { throw "No keystore at $keystore." }
$keytool = (Get-Command keytool -ErrorAction SilentlyContinue).Source
if (-not $keytool) {
    $candidates = @("$env:JAVA_HOME\bin\keytool.exe", (Join-Path $root '.tools\jdk\jdk-21.0.12.1+1\bin\keytool.exe'),
        "$env:ProgramFiles\Android\Android Studio\jbr\bin\keytool.exe")
    $keytool = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $keytool) { throw 'keytool not found. Install a JDK (or Android Studio), then run again.' }

# Refuse a different key (for example another PC's debug keystore): it could never update installed copies.
$listing = & $keytool -list -v -keystore $keystore -storepass android -alias androiddebugkey 2>$null | Out-String
$actual = ([regex]::Match($listing, 'SHA256:\s*([0-9A-F:]{95})')).Groups[1].Value -replace ':', ''
if ($actual -ne $expected) { throw "This keystore's certificate ($actual) is not OMNI's signing key ($expected)." }

# Local builds (scripts/Build-Android.ps1) read this file; it is gitignored.
$store = $keystore.Replace('\', '/')
Set-Content -Path $properties -Encoding ASCII -Value "storeFile=$store`nstorePassword=android`nkeyAlias=androiddebugkey`nkeyPassword=android"

if ($UploadSecrets) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) not found: https://cli.github.com' }
    gh secret set OMNI_KEYSTORE_BASE64 -R $repo --body ([Convert]::ToBase64String([IO.File]::ReadAllBytes($keystore)))
    gh secret set OMNI_KEYSTORE_PASSWORD -R $repo --body 'android'
    gh secret set OMNI_KEY_ALIAS -R $repo --body 'androiddebugkey'
}

Write-Host 'Done: release builds are signed with the key installed copies use (android/key.properties).'
Write-Host "Back up $keystore (password manager or USB). Never commit it."
