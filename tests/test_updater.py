import hashlib, pathlib, struct, subprocess, tempfile, zipfile, unittest

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / 'scripts' / 'updater.ps1'
def pe(machine=0x8664):
    b=bytearray(512);b[:2]=b'MZ';struct.pack_into('<I',b,0x3c,128);b[128:132]=b'PE\0\0';struct.pack_into('<H',b,132,machine);struct.pack_into('<H',b,152,0x20b if machine==0x8664 else 0x10b);return bytes(b)
class UpdaterTests(unittest.TestCase):
    def run_package(self,extra=None,machine=0x8664,wrong_hash=False):
        with tempfile.TemporaryDirectory(prefix='omni-update-test-') as folder:
            root=pathlib.Path(folder);install=root/'install';install.mkdir();(install/'App.exe').write_bytes(pe());archive=root/'release.zip'
            with zipfile.ZipFile(archive,'w') as z:
                for name in ['App.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe']:
                    z.writestr('package/'+name,pe(machine if name=='ffmpeg.exe' else 0x8664))
                z.writestr('package/updater.ps1','test')
                if extra:z.writestr(extra,b'bad')
            sha='0'*64 if wrong_hash else hashlib.sha256(archive.read_bytes()).hexdigest()
            result=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(SCRIPT),'-InstallPath',str(install),'-AppPid','2147483647','-ZipPath',str(archive),'-ExpectedSha256',sha,'-ReleasesUrl','https://github.com/example/omni/releases','-ValidateOnly'],capture_output=True,text=True,errors='replace',timeout=30)
            self.assertEqual((install/'App.exe').read_bytes(),pe())
            return result
    def test_real_install_keeps_data_and_removes_zip(self):
        with tempfile.TemporaryDirectory(prefix='omni-install-test-') as folder:
            root=pathlib.Path(folder);install=root/'install';install.mkdir();(install/'data').mkdir();(install/'data/history.db').write_bytes(b'USER-DATA');(install/'native-host.json').write_text('LOCAL-CONFIG');(install/'App.exe').write_bytes(pe()+b'old');archive=root/'release.zip'
            with zipfile.ZipFile(archive,'w') as z:
                for name in ['App.exe','Omni.NativeHost.exe','yt-dlp.exe','ffmpeg.exe','ffprobe.exe']:z.writestr(name,pe()+b'new')
                z.writestr('updater.ps1','test')
            sha=hashlib.sha256(archive.read_bytes()).hexdigest()
            result=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(SCRIPT),'-InstallPath',str(install),'-AppPid','2147483647','-ZipPath',str(archive),'-ExpectedSha256',sha,'-ReleasesUrl','https://github.com/example/omni/releases','-NoRestartPrompt'],capture_output=True,text=True,errors='replace',timeout=30)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
            self.assertEqual((install/'App.exe').read_bytes(),pe()+b'new');self.assertFalse(archive.exists())
            self.assertEqual((install/'data/history.db').read_bytes(),b'USER-DATA');self.assertEqual((install/'native-host.json').read_text(),'LOCAL-CONFIG')
    def test_valid(self):
        r=self.run_package();self.assertEqual(r.returncode,0,r.stdout+r.stderr)
    def rejection(self,needle,**kwargs):
        r=self.run_package(**kwargs);self.assertNotEqual(r.returncode,0);self.assertIn(needle,r.stdout+r.stderr)
    def test_architecture(self):self.rejection('this system is',machine=0xAA64)
    def test_traversal(self):self.rejection('Unsafe ZIP entry',extra='../escape.exe')
    def test_flat_collision(self):self.rejection('Flattening collision',extra='other/App.exe')
    def test_data_protected(self):self.rejection('must not contain user data',extra='data/history.db')
    def test_bad_digest(self):self.rejection('SHA256 verification failed',wrong_hash=True)
if __name__=='__main__':unittest.main()
