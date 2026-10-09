"""Generate original synthetic media for both platforms; requires FFmpeg."""
import argparse, subprocess
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('--ffmpeg',default='ffmpeg');args=parser.parse_args()
root=Path(__file__).resolve().parents[1]/'tests'/'Fixtures'/'audio';root.mkdir(parents=True,exist_ok=True)
for ext,codec in [('mp3','libmp3lame'),('opus','libopus'),('m4a','aac'),('flac','flac')]:
 subprocess.run([args.ffmpeg,'-v','error','-y','-f','lavfi','-i','sine=frequency=440:duration=1','-ar','48000','-c:a',codec,'-metadata','title=Demo tone','-metadata','artist=Demo artist','-metadata','album=Demo album','-metadata','comment=Keep this comment',str(root/f'tone.{ext}')],check=True)
subprocess.run([args.ffmpeg,'-v','error','-y','-f','lavfi','-i','color=c=0x6c40dd:s=64x64','-frames:v','1',str(root/'cover.png')],check=True)
print(root)

(root/'fingerprint').mkdir(exist_ok=True)
for ext,codec in [('mp3','libmp3lame'),('opus','libopus'),('m4a','aac'),('flac','flac')]:
 subprocess.run([args.ffmpeg,'-v','error','-y','-f','lavfi','-i','sine=frequency=440:duration=20','-ar','48000','-c:a',codec,str(root/'fingerprint'/f'tone.{ext}')],check=True)
