from pathlib import Path
import json, numpy as np, soundfile as sf
from PIL import Image,ImageDraw
ROOT=Path.cwd()
# Replace only the decorative frame; retain the original wood interior as provenance.
art=ROOT/'AI/comfyui/mage-skills/20260916-combat'
src=ROOT/'Assets/UGUI/Art/Gacha/GachaBronzeButton.png'
before=art/'references/GachaBronzeButton.before.png';before.parent.mkdir(parents=True,exist_ok=True)
if not before.exists():before.write_bytes(src.read_bytes())
old=Image.open(before).convert('RGBA');out=Image.new('RGBA',old.size,(35,27,21,255));d=ImageDraw.Draw(out)
d.rectangle((1,1,510,94),outline=(96,74,46,255),width=2)
d.rectangle((3,3,508,92),outline=(49,37,26,255),width=2)
out.paste(old.crop((40,14,472,82)).resize((500,84),Image.Resampling.NEAREST),(6,6));out.save(src)
# Compact mono combat mix; all masters preserved. Source timings and gains are explicit.
folder=ROOT/'Assets/_Project/Audio/Combat';folder.mkdir(parents=True,exist_ok=True)
sources=[('Steel',art/'Audio-v1/Sword.flac',0,.42,.55),('Heavy',art/'Audio-v1/Heavy.flac',0,.46,.55),
 ('Thrust',ROOT/'Assets/_Project/Audio/Slash_Attack_SFX.wav',0,.32,.42),
 ('Throw',ROOT/'Assets/_Project/Audio/Dash.ogg',0,.30,.35),
 ('Magic',ROOT/'Assets/_Project/Audio/Charge_Shot_SFX.wav',0,.38,.38),
 ('Whip',ROOT/'Assets/_Project/Audio/Parrying_SFX.wav',0,.30,.4)]
manifest=[]
for name,path,start,duration,peak in sources:
 x,sr=sf.read(path,always_2d=True);x=x.mean(axis=1)
 # Locate first audible onset, preserving 10 ms lead-in.
 threshold=max(abs(x).max()*.06,.0001);nz=np.flatnonzero(abs(x)>threshold);offset=max(0,int(nz[0])-int(.01*sr)) if len(nz) else 0
 x=x[offset:offset+int(duration*sr)];t=np.arange(round(len(x)*22050/sr))/22050;x=np.interp(t,np.arange(len(x))/sr,x)
 gain=peak/max(abs(x).max(),.001);x*=gain
 fade=min(int(.012*22050),len(x)//3);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
 sf.write(folder/f'{name}.wav',x,22050,subtype='PCM_16')
 manifest.append(dict(name=name,source=str(path.relative_to(ROOT)),onsetSeconds=offset/sr,duration=len(x)/22050,sampleRate=22050,channels=1,gain=float(gain),peak=float(abs(x).max()),rms=float(np.sqrt(np.mean(x*x))),clippedSamples=int((abs(x)>=1).sum())))
(art/'Audio-v1/final-mix.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print(json.dumps(manifest,indent=2))
