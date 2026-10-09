"""Offline download/remux/transcode verification using the real C# command builder.
Only synthetic local audio is served. No YouTube credentials or network are used.
"""
import argparse, http.server, json, subprocess, threading, re
from functools import partial
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('--tools',required=True);args=parser.parse_args()
root=Path(__file__).resolve().parents[1];tools=Path(args.tools).resolve();out=root/'artifacts'/'audio-pipeline-qa';out.mkdir(parents=True,exist_ok=True)
project=root/'tests'/'AudioPipeline.Smoke'/'AudioPipeline.Smoke.csproj'
subprocess.run(['dotnet','build',str(project),'-v','q'],check=True)
dll=project.parent/'bin'/'Debug'/'net8.0'/'AudioPipeline.Smoke.dll'
def run(cmd):return subprocess.run([str(x) for x in cmd],capture_output=True,text=True,encoding='utf-8',errors='replace',check=True).stdout
profiles=json.loads(run(['dotnet',dll,'commands']))
fixtures=root/'tests'/'Fixtures'/'audio'
class Handler(http.server.SimpleHTTPRequestHandler):
 def log_message(self,*a):pass
server=http.server.ThreadingHTTPServer(('127.0.0.1',0),partial(Handler,directory=str(fixtures)));threading.Thread(target=server.serve_forever,daemon=True).start()
url=f'http://127.0.0.1:{server.server_port}'
def info(formats):return {'id':'synthetic','title':'Synthetic tone','duration':1.0,'extractor':'generic','webpage_url':url,'formats':formats}
formats=[{'format_id':'251','ext':'webm','acodec':'opus','vcodec':'none','abr':96,'url':url+'/tone.opus'},{'format_id':'140','ext':'m4a','acodec':'mp4a.40.2','vcodec':'none','abr':128,'url':url+'/tone.m4a'}]
# WebM source is encoded once from an original sine wave, then remuxed without re-encoding.
run([tools/'ffmpeg.exe','-v','error','-y','-i',fixtures/'tone.opus','-c:a','copy',out/'tone.webm'])
class QAHandler(Handler):
 def translate_path(self,path):return str(out/'tone.webm') if path=='/tone.webm' else super().translate_path(path)
server.RequestHandlerClass=partial(QAHandler,directory=str(fixtures));formats[0]['url']=url+'/tone.webm'
manifest=out/'source.json';manifest.write_text(json.dumps(info(formats)),encoding='utf-8')
def hashes(file):return re.findall(r'SHA256:[0-9a-f]{64}',run([tools/'ffprobe.exe','-v','error','-select_streams','a:0','-show_packets','-show_data_hash','sha256','-show_entries','packet=data_hash','-of','csv=p=0',file]))
results=[]
try:
 for p in profiles:
  folder=out/p['name'];folder.mkdir(exist_ok=True)
  cmd=[tools/'yt-dlp.exe','--ignore-config','--no-update','--encoding','utf-8','--ffmpeg-location',tools,'--load-info-json',manifest,'-o',str(folder/'media.%(ext)s'),*p['args']]
  log=run(cmd);(folder/'pipeline.log').write_text(log,encoding='utf-8')
  file=folder/f"media.{p['format']}";run(['dotnet',dll,'validate',file,tools/'ffmpeg.exe','1'])
  if p['format'] in ['opus','m4a']:
   assert hashes(file)==hashes(out/'tone.webm' if p['format']=='opus' else fixtures/'tone.m4a'),p['name']+' re-encoded audio'
  results.append({'profile':p['name'],'valid':True,'native_packets_preserved':p['format'] in ['opus','m4a']})
 # An AAC-only source must fail strict Opus selection rather than quietly transcode.
 missing=out/'missing-opus.json';missing.write_text(json.dumps(info(formats[1:])),encoding='utf-8')
 failed=subprocess.run([str(tools/'yt-dlp.exe'),'--ignore-config','--no-update','--load-info-json',str(missing),*profiles[0]['args']],capture_output=True,text=True,encoding='utf-8')
 assert failed.returncode!=0 and 'Requested format is not available' in failed.stderr
 results.append({'profile':'missing-native-opus','rejected':True})
 (out/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8');print(json.dumps(results,indent=2))
finally:server.shutdown();server.server_close()
