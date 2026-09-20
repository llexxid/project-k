"""Preserve the master, key the chroma, then make a six-color 64 px combat sprite."""
from collections import Counter,deque
from pathlib import Path
from PIL import Image
import json,hashlib
root=Path(__file__).parent
source=root/'meteor-v1/raw.png';im=Image.open(source).convert('RGB');w,h=im.size
def chroma(c):return c[1]>115 and c[1]>c[0]*1.5 and c[1]>c[2]*1.5
pixels=list(im.getdata());key={i for i,c in enumerate(pixels) if chroma(c)}
queue=deque(i for i in key if i<w or i>=w*(h-1) or i%w in (0,w-1));outside=set(queue)
while queue:
    i=queue.popleft()
    for j in (i-w,i+w,i-1,i+1):
        if j in key and j not in outside:outside.add(j);queue.append(j)
# Strict enclosed-key pass plus only green-biased pixels immediately adjoining keyed pixels.
fringe={i for i,c in enumerate(pixels) if i not in key and c[1]>c[0]+15 and c[1]>c[2]+15 and any(j in key for j in (i-w,i+w,i-1,i+1))}
rgba=Image.new('RGBA',(w,h));rgba.putdata([(0,0,0,0) if i in key or i in fringe else (*c,255) for i,c in enumerate(pixels)])
palette=[(61,48,43),(69,72,81),(111,100,89),(151,79,43),(206,111,51),(240,180,101)]
result=Image.new('RGBA',(64,64))
for y in range(64):
    for x in range(64):
        tile=rgba.crop((x*w//64,y*h//64,(x+1)*w//64,(y+1)*h//64))
        c=Counter(tile.getdata()).most_common(1)[0][0]
        if c[3]:c=(*min(palette,key=lambda p:sum((p[j]-c[j])**2 for j in range(3))),255)
        result.putpixel((x,y),c)
output=root/'meteor-v1/body64.png';result.save(output)
target=Path('Assets/_Project/Art/VFX/MageTower/MeteorBody64.png');target.write_bytes(output.read_bytes())
preview=Image.new('RGBA',(384,384),(75,103,48,255));preview.alpha_composite(result.resize((384,384),Image.Resampling.NEAREST));preview.convert('RGB').save(root/'meteor-v1/preview.png')
(root/'finish-manifest.json').write_text(json.dumps({'source':str(source),'masterSize':im.size,'target':str(target),'outputSize':result.size,'palette':palette,'sourceHash':hashlib.sha256(source.read_bytes()).hexdigest(),'outputHash':hashlib.sha256(target.read_bytes()).hexdigest(),'chromaBoundaryPixels':len(outside),'enclosedChromaPixels':len(key-outside),'fringePixels':len(fringe),'pillow':Image.__version__,'process':'boundary flood -> strict enclosed chroma removal -> adjacent green fringe -> 16px mode tiles -> six-color snap; no frame diffusion','decision':'replace rejected pillar-shaped reuse; rounded static body with stable fractured planes; native FireFlamme animation retained behind it'},indent=2),encoding='utf8')
print('Meteor body saved:',target)
