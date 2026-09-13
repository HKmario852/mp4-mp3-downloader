[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallPath,
    [Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$AppPid,
    [Parameter(Mandatory)][string]$ZipPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/releases/?$')][string]$ReleasesUrl,
    [switch]$ValidateOnly,
    [switch]$Restart,
    [switch]$NoRestartPrompt
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-SafeAbsolute([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path)) { throw 'Absolute paths are required.' }
    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
}
function Assert-Under([string]$Path,[string]$Root) {
    $absolute = Get-SafeAbsolute $Path
    $prefix = (Get-SafeAbsolute $Root) + [IO.Path]::DirectorySeparatorChar
    if (-not $absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped update workspace: $absolute" }
    return $absolute
}
function Assert-NoReparse([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse points are not supported by the updater.' }
        if ($item -is [IO.FileInfo]) { $item=$item.Directory } else { $item=$item.Parent }
    }
}
function Show-Bar([int]$Done,[int]$Total,[string]$Label) {
    $percent = if ($Total -eq 0) { 100 } else { [int](100*$Done/$Total) }
    $n=[Math]::Min(30,[int]($percent*30/100))
    Write-Host ("`r["+('#'*$n)+('-'*(30-$n))+"] $percent% $Label") -NoNewline
}
function Get-Architecture {
    $arch=if($env:PROCESSOR_ARCHITEW6432){$env:PROCESSOR_ARCHITEW6432}else{$env:PROCESSOR_ARCHITECTURE}
    switch($arch.ToUpperInvariant()) { 'AMD64' {return 'x64'} 'ARM64' {return 'ARM64'} 'X86' {return 'x86'} default {throw "Unsupported system architecture: $arch"} }
}
function Get-PeArchitecture([IO.Stream]$Stream) {
    $reader=New-Object IO.BinaryReader($Stream)
    try {
        if($Stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D){throw 'Not a valid MZ binary.'}
        $Stream.Position=0x3c;$offset=$reader.ReadUInt32()
        if($offset -gt $Stream.Length-24){throw 'Invalid PE offset.'}
        $Stream.Position=$offset;if($reader.ReadUInt32() -ne 0x00004550){throw 'Invalid PE signature.'}
        $machine=$reader.ReadUInt16();$sections=$reader.ReadUInt16();$Stream.Position=$offset+20;$optionalSize=$reader.ReadUInt16();$Stream.Position=$offset+24;$magic=$reader.ReadUInt16()
        # CLR IL-only PE32 assemblies are architecture-neutral unless 32BITREQUIRED.
        if($machine -eq 0x14c -and $magic -eq 0x10b -and $optionalSize -ge 224) {
            $Stream.Position=$offset+24+96+14*8;$clrRva=$reader.ReadUInt32();$clrSize=$reader.ReadUInt32()
            if($clrRva -ne 0 -and $clrSize -ge 20) {
                $table=$offset+24+$optionalSize
                for($i=0;$i -lt $sections;$i++) {
                    $Stream.Position=$table+$i*40+8;$virtualSize=$reader.ReadUInt32();$va=$reader.ReadUInt32();$rawSize=$reader.ReadUInt32();$raw=$reader.ReadUInt32()
                    if($clrRva -ge $va -and $clrRva -lt ($va+[Math]::Max($virtualSize,$rawSize))) {
                        $flagsOffset=$raw+($clrRva-$va)+16
                        if($flagsOffset -gt $Stream.Length-4){throw 'Invalid CLR header.'}
                        $Stream.Position=$flagsOffset;$flags=$reader.ReadUInt32()
                        if(($flags -band 1) -eq 1 -and ($flags -band 2) -eq 0){return 'AnyCPU'}
                    }
                }
            }
        }
        switch($machine){0x8664{return 'x64'}0xAA64{return 'ARM64'}0x14c{return 'x86'}default{throw ('Unknown PE Machine 0x{0:X}' -f $machine)}}
    } finally {$reader.Dispose()}
}

$install=Get-SafeAbsolute $InstallPath
$zip=Get-SafeAbsolute $ZipPath
if($install -eq ([IO.Path]::GetPathRoot($install)).TrimEnd('\')){throw 'Cannot update a volume root.'}
Assert-NoReparse $install
Assert-NoReparse $zip
if(-not(Test-Path -LiteralPath (Join-Path $install 'App.exe') -PathType Leaf)){throw 'App.exe was not found in InstallPath.'}
$hashStream=[IO.File]::OpenRead($zip)
$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash($hashStream)).Replace('-','')}finally{$hashStream.Dispose();$sha.Dispose()}
if($hash -ne $ExpectedSha256){throw 'SHA256 verification failed. Nothing was changed.'}
$stage=Join-Path ([IO.Path]::GetTempPath()) ('Omni-update-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage)|Out-Null
$payload=Join-Path $stage 'payload';$backup=Join-Path $stage 'backup'
[IO.Directory]::CreateDirectory($payload)|Out-Null
[IO.Directory]::CreateDirectory($backup)|Out-Null
$changed=New-Object 'System.Collections.Generic.List[string]'
$created=New-Object 'System.Collections.Generic.List[string]'
$success=$false;$restoreFailed=$false
try {
    $archive=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $files=@{};$expanded=0L
        foreach($entry in $archive.Entries) {
            $name=$entry.FullName.Replace('\','/')
            if($name.StartsWith('/') -or $name.Contains(':') -or @($name.Split('/')|Where-Object{$_ -eq '..' -or $_ -eq '.'}).Count -gt 0){throw "Unsafe ZIP entry: $name"}
            if(($entry.ExternalAttributes -band 0x400) -ne 0 -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000){throw 'ZIP links are prohibited.'}
            if($entry.Name -eq ''){continue}
            if($entry.Name -match '[<>:"|?*\x00-\x1f]' -or $entry.Name -match '[ .]$' -or $entry.Name -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)'){throw 'Invalid ZIP filename.'}
            if($name.Split('/') -contains 'data' -or $entry.Name -match '^(history\.db.*|settings\.json|cookies.*|native-host\.json)$'){throw 'Update archive must not contain user data or local browser configuration.'}
            if($files.ContainsKey($entry.Name)){throw "Flattening collision: $($entry.Name)"}
            $expanded+=$entry.Length;if($expanded -gt 4GB){throw 'Expanded archive exceeds 4 GB.'}
            $files[$entry.Name]=$entry
        }
        foreach($required in @('App.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe','updater.ps1')){if(-not $files.ContainsKey($required)){throw "Required release file missing: $required"}}
        $arch=Get-Architecture
        foreach($entry in $files.Values | Where-Object{$_.Name -match '\.(exe|dll)$'}) {
            $memory=New-Object IO.MemoryStream
            $input=$entry.Open();try{$input.CopyTo($memory)}finally{$input.Dispose()};$memory.Position=0
            try{$found=Get-PeArchitecture $memory}finally{$memory.Dispose()}
            if($found -ne $arch -and $found -ne 'AnyCPU') {
                Write-Host "FATAL: $($entry.Name) is $found but this system is $arch. Update aborted." -ForegroundColor Red
                if(-not $ValidateOnly){Start-Process -FilePath $ReleasesUrl}
                throw 'Download a matching architecture package from GitHub Releases.'
            }
        }
        $done=0
        foreach($entry in $files.Values) {
            $destination=Assert-Under (Join-Path $payload $entry.Name) $stage
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$false)
            $done++;Show-Bar $done $files.Count 'Validated extraction'
        }
        Write-Host ''
    }finally{$archive.Dispose()}
    if($ValidateOnly){Write-Host 'SHA256, ZIP layout, and all PE architectures passed. No installed files changed.';return}
    $target=Get-Process -Id $AppPid -ErrorAction SilentlyContinue
    if($null -ne $target) {
        if(-not [string]::Equals((Get-SafeAbsolute $target.Path),(Join-Path $install 'App.exe'),[StringComparison]::OrdinalIgnoreCase)){throw 'PID does not belong to InstallPath\App.exe.'}
        Write-Host 'Waiting for the application to exit. Choose Exit from its tray menu.'
        # No forced termination: the app shuts down children and checkpoints SQLite.
        $target.WaitForExit()
    }
    $sourceFiles=@(Get-ChildItem -LiteralPath $payload -File)
    # Exclusive access preflight occurs before ANY installed file is changed.
    $handles=New-Object 'System.Collections.Generic.List[System.IO.FileStream]'
    try {foreach($f in $sourceFiles){$dest=Join-Path $install $f.Name;if(Test-Path -LiteralPath $dest){Assert-NoReparse $dest;$handles.Add([IO.File]::Open($dest,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None))}}}
    finally{foreach($h in $handles){$h.Dispose()}}
    $data=Join-Path $install 'data'
    if(Test-Path -LiteralPath $data){Assert-NoReparse $data;Get-ChildItem -LiteralPath $data -Recurse -Force | ForEach-Object {if(($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'User data contains a reparse point.'}};Copy-Item -LiteralPath $data -Destination (Join-Path $backup 'data') -Recurse}
    # User data is never overwritten; full backup exists for recovery.
    foreach($f in $sourceFiles){$dest=Join-Path $install $f.Name;if(Test-Path -LiteralPath $dest){Copy-Item -LiteralPath $dest -Destination (Join-Path $backup $f.Name)}}
    $done=0
    foreach($f in $sourceFiles) {
        $dest=Assert-Under (Join-Path $install $f.Name) $install
        if(Test-Path -LiteralPath $dest){$changed.Add($f.Name)}else{$created.Add($f.Name)}
        Copy-Item -LiteralPath $f.FullName -Destination $dest -Force
        $done++;Show-Bar $done $sourceFiles.Count 'Updating'
    }
    Write-Host ''
    $success=$true
    Remove-Item -LiteralPath $zip -Force
    Write-Host 'Update complete. Settings and history were preserved.' -ForegroundColor Green
    if($Restart){Start-Process -FilePath (Join-Path $install 'App.exe') -WorkingDirectory $install -WindowStyle Hidden}
    elseif(-not $NoRestartPrompt){$answer=Read-Host 'Restart now? [Y/N]';if($answer -match '^[Yy]$'){Start-Process -FilePath (Join-Path $install 'App.exe') -WorkingDirectory $install}}
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    if(-not $success) {
        foreach($name in $changed){try{Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $install $name) -Force}catch{$restoreFailed=$true}}
        foreach($name in $created){try{$remove=Assert-Under (Join-Path $install $name) $install;Remove-Item -LiteralPath $remove -Force}catch{$restoreFailed=$true}}
    }
    if($restoreFailed){Write-Host "Rollback requires attention. Recovery backup retained: $backup" -ForegroundColor Red}
    if($Restart){Add-Type -AssemblyName PresentationFramework;[System.Windows.MessageBox]::Show(('更新未完成：'+$_.Exception.Message),'全能影音下載器更新')|Out-Null}
    throw
} finally {
    if(-not $restoreFailed){$remove=Assert-Under $stage ([IO.Path]::GetTempPath());if(Test-Path -LiteralPath $remove){Remove-Item -LiteralPath $remove -Recurse -Force}}
}
