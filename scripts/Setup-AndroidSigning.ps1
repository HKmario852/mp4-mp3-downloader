# Creates the permanent Android release signing key for OMNI and points local builds at it, so every published
# APK is signed with the same key and later versions install over earlier ones.
#
# Run once on your own PC, from the repository folder:
#   powershell -ExecutionPolicy Bypass -File scripts\Setup-AndroidSigning.ps1
# Add -UploadSecrets to also store the key as GitHub Actions secrets for CI builds (needs `gh auth login`).
#
# Keep the folder it prints somewhere safe. If the key is lost, the next update needs one uninstall.
param([switch]$UploadSecrets)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repo = 'HKmario852/mp4-mp3-downloader'
$alias = 'omni'
$dir = Join-Path $HOME 'omni-signing'
$jks = Join-Path $dir 'omni-release.jks'
$properties = Join-Path $root 'android/key.properties'

if (Test-Path $jks) { throw "A key already exists at $jks. Not overwriting it (installed apps could no longer update)." }
$keytool = (Get-Command keytool -ErrorAction SilentlyContinue).Source
if (-not $keytool) {
    $candidates = @("$env:JAVA_HOME\bin\keytool.exe", (Join-Path $root '.tools\jdk\jdk-21.0.12.1+1\bin\keytool.exe'),
        "$env:ProgramFiles\Android\Android Studio\jbr\bin\keytool.exe")
    $keytool = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $keytool) { throw 'keytool not found. Install a JDK (or Android Studio), then run again.' }
if ($UploadSecrets) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) not found: https://cli.github.com' }
    # A key already in GitHub secrets means CI signs with it; replacing it would stop installed copies from updating.
    if (gh secret list -R $repo | Select-String -SimpleMatch 'OMNI_KEYSTORE_BASE64') { throw 'OMNI_KEYSTORE_BASE64 is already set on GitHub. Keep using that key.' }
}

New-Item -ItemType Directory -Force $dir | Out-Null
$bytes = New-Object byte[] 24
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$pw = ([Convert]::ToBase64String($bytes) -replace '[/+=]', '')
& $keytool -genkeypair -keystore $jks -storetype PKCS12 -alias $alias -keyalg RSA -keysize 4096 `
    -validity 36500 -storepass $pw -keypass $pw -dname 'CN=HKmario852, C=HK' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }
Set-Content -Path (Join-Path $dir 'password.txt') -Value $pw -NoNewline

# Local builds (scripts/Build-Android.ps1) read this file; it is gitignored.
$store = $jks.Replace('\', '/')
Set-Content -Path $properties -Encoding ASCII -Value "storeFile=$store`nstorePassword=$pw`nkeyAlias=$alias`nkeyPassword=$pw"

if ($UploadSecrets) {
    gh secret set OMNI_KEYSTORE_BASE64 -R $repo --body ([Convert]::ToBase64String([IO.File]::ReadAllBytes($jks)))
    gh secret set OMNI_KEYSTORE_PASSWORD -R $repo --body $pw
    gh secret set OMNI_KEY_ALIAS -R $repo --body $alias
}

Write-Host ''
Write-Host "Done. Signing key saved in: $dir"
Write-Host "Local release builds now use it (android/key.properties)."
Write-Host 'Back up that folder (password manager or USB). Never commit it.'
