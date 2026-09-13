"""Install verified Google Android SDK packages into an isolated build directory."""
import hashlib, io, pathlib, shutil, sys, urllib.request, xml.etree.ElementTree as ET, zipfile

root = pathlib.Path(sys.argv[1]).resolve()
root.mkdir(parents=True, exist_ok=True)
with urllib.request.urlopen('https://dl.google.com/android/repository/repository2-1.xml') as response:
    document = ET.fromstring(response.read())
for package in ('platforms;android-35', 'build-tools;35.0.0', 'platform-tools'):
    target = root.joinpath(*package.split(';'))
    if (target / 'source.properties').exists():
        print('Already installed:', package, flush=True)
        continue
    entries = [e for e in document if e.tag.endswith('remotePackage') and e.get('path') == package]
    if not entries:
        raise RuntimeError('Package missing: ' + package)
    entry = next((e for e in entries if '-ext' not in e.findtext('archives/archive/complete/url', '')), entries[0])
    archives = entry.find('archives')
    archive = next(a for a in archives.findall('archive') if a.findtext('host-os') in (None, 'windows'))
    complete = archive.find('complete')
    url = 'https://dl.google.com/android/repository/' + complete.findtext('url')
    expected = complete.findtext('checksum').strip()
    algorithm = complete.find('checksum').get('type', 'sha1')
    print('Downloading', package, url, flush=True)
    with urllib.request.urlopen(url) as response:
        payload = response.read()
    if hashlib.new(algorithm, payload).hexdigest().lower() != expected.lower():
        raise RuntimeError('SDK checksum mismatch: ' + package)
    with zipfile.ZipFile(io.BytesIO(payload)) as z:
        names = [n for n in z.namelist() if not n.endswith('/')]
        prefix = names[0].split('/')[0] + '/'
        for name in names:
            if not name.startswith(prefix):
                raise RuntimeError('Unexpected SDK archive layout')
            relative = pathlib.PurePosixPath(name[len(prefix):])
            if relative.is_absolute() or '..' in relative.parts:
                raise RuntimeError('Unsafe SDK archive path')
            destination = target.joinpath(*relative.parts)
            destination.parent.mkdir(parents=True, exist_ok=True)
            with z.open(name) as source, destination.open('wb') as output:
                shutil.copyfileobj(source, output)
    print('Verified and installed:', target, flush=True)
