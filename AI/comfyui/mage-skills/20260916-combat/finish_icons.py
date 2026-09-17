from pathlib import Path
from PIL import Image
import json,hashlib
root=Path(__file__).parent
def reduced(folder):
 im=Image.open(folder/'master.png').convert('RGB').resize((48,48),Image.Resampling.NEAREST)
 im.putdata([(21,27,33) if max(c)<65 else c for c in im.getdata()])
 return im.quantize(colors=12,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB')
base=reduced(root/'Starfall-v1');generated=reduced(root/'StarfallBloom-v1');bloom=base.copy()
for y in range(48):
 for x in range(48):
  b=base.getpixel((x,y));g=generated.getpixel((x,y))
  if max(b)>65 and g[2]>g[0]*1.10 and g[2]>g[1]*1.04:bloom.putpixel((x,y),(146,148,195))
extras={(x,y) for y in range(48) for x in range(48) if max(base.getpixel((x,y)))<65 and max(generated.getpixel((x,y)))>140}
while extras:
 pending=[extras.pop()];group=[]
 while pending:
  x,y=pending.pop();group.append((x,y))
  for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
   n=x+dx,y+dy
   if n in extras:extras.remove(n);pending.append(n)
 if len(group)<=12 and max(x for x,y in group)-min(x for x,y in group)<=4 and max(y for x,y in group)-min(y for x,y in group)<=4:
  for xy in group:bloom.putpixel(xy,(180,190,208))
bloom=bloom.quantize(colors=16,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB')
base.save(root/'Starfall-v1/icon48.png');bloom.save(root/'StarfallBloom-v1/icon48.png')
changed=sum(b!=a for a,b in zip(base.getdata(),bloom.getdata()))
report=dict(process='Normalize background RGB below65 before palette reduction. Raw Comfy quantization spent its palette on background noise and collapsed foreground shading: rejected. Preserve base silhouette/colors in bloom; adopt generated violet streaks and only isolated <=12-pixel glints.',size=[48,48],baseColors=len(set(base.getdata())),bloomColors=len(set(bloom.getdata())),changedPixels=changed,changedFraction=changed/2304)
(root/'icon-finish-v2.json').write_text(json.dumps(report,indent=2),encoding='utf8')
preview=Image.new('RGB',(96,48));preview.paste(base);preview.paste(bloom,(48,0));preview.resize((768,384),Image.Resampling.NEAREST).save(root/'icon-compare-v2.png')
print(json.dumps(report))
