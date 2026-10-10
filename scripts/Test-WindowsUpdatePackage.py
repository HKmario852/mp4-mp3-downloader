"""Reproduce the old package failure and install the new real ZIP in isolation."""
import argparse, hashlib, json, pathlib, shutil, subprocess, zipfile

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('--package', type=pathlib.Path, required=True)
parser.add_argument('--legacy-install', type=pathlib.Path, required=True)
parser.add_argument('--broken-package', type=pathlib.Path, required=True)
parser.add_argument('--output', type=pathlib.Path, required=True)
args = parser.parse_args()
output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
install = output / 'isolated-install'; install.mkdir(exist_ok=True)
shutil.copy2(args.legacy_install / 'OMNI.exe', install / 'OMNI.exe')
(install / 'data').mkdir(exist_ok=True)
(install / 'data/history.db').write_bytes(b'ISOLATED-HISTORY-SENTINEL')
(install / 'data/settings.json').write_bytes(b'ISOLATED-SETTINGS-SENTINEL')
(install / 'native-host.json').write_bytes(b'ISOLATED-BROWSER-CONFIG')
sentinels = {str(p.relative_to(install)): digest(p) for p in [install/'data/history.db', install/'data/settings.json', install/'native-host.json']}
old_hash = digest(install/'OMNI.exe')

def run(package, validate, log):
    command = ['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str((args.legacy_install/'updater.ps1').resolve()), '-InstallPath', str(install), '-AppPid', '2147483647', '-ZipPath', str(package.resolve()), '-ExpectedSha256', digest(package), '-ReleasesUrl', 'https://github.com/HKmario852/mp4-mp3-downloader/releases', '-ValidateOnly' if validate else '-NoRestartPrompt']
    with (output/log).open('w', encoding='utf-8') as stream:
        result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, timeout=180)
    return result.returncode, (output/log).read_text(encoding='utf-8')

code, log = run(args.broken_package, True, 'broken-package.txt')
assert code != 0 and 'Flattening collision: ARCHITECTURE.md' in log
assert digest(install/'OMNI.exe') == old_hash
with zipfile.ZipFile(args.package) as archive:
    names = archive.namelist()
    assert len({pathlib.PurePosixPath(n.replace('\\','/')).name.casefold() for n in names}) == len(names)
    assert all('/' not in n and '\\' not in n for n in names)
    for name in ['OMNI.exe', 'TagLibSharp-LGPL-2.1.txt', 'Jaudiotagger-LICENSE.txt', 'JCodec-LICENSE.txt']:
        assert name in names
    assert 'App.exe' not in names and 'native-host.json' not in names
    expected = hashlib.sha256(archive.read('OMNI.exe')).hexdigest()
staged_zip = output/'install-package.zip'; shutil.copy2(args.package, staged_zip)
code, log = run(staged_zip, False, 'successful-install.txt')
assert code == 0, log[-3000:]
assert not staged_zip.exists() and digest(install/'OMNI.exe') == expected
assert all(digest(install/name) == value for name, value in sentinels.items())
record = {'passed': True, 'legacy_updater': str(args.legacy_install/'updater.ps1'), 'broken_package_reproduced': True, 'real_package_installed': True, 'unique_flat_entries': len(names), 'data_and_browser_config_preserved': True}
(output/'checks.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps(record))
