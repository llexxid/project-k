"""Reproducible 64 px finishing. Generation masters remain untouched for comparison."""
from pathlib import Path
from PIL import Image,ImageDraw
from collections import Counter,deque
import json,hashlib
root=Path(__file__).parent
palette=[(48,37,35),(73,48,39),(104,68,47),(155,72,36),(206,113,51),(244,178,89)]
records=[]
for variant in ['nano-v1','gpt-v1']:
    source=root/variant/'master.png';im=Image.open(source).convert('RGB')
    # First classify the matte, flood its boundary, then remove enclosed chroma too.
    def green(c):return c[1]>130 and c[1]>c[0]*1.6 and c[1]>c[2]*1.6
    key={i for i,c in enumerate(im.getdata()) if green(c)};w,h=im.size
    queue=deque(i for i in key if i<w or i>=w*(h-1) or i%w in [0,w-1]);outside=set(queue)
    while queue:
        i=queue.popleft()
        for j in [i-w,i+w,i-1,i+1]:
            if j in key and j not in outside:outside.add(j);queue.append(j)
    pixels=list(im.getdata());rgba=Image.new('RGBA',im.size)
    rgba.putdata([(0,0,0,0) if i in key else (*c,255) for i,c in enumerate(pixels)])
    result=Image.new('RGBA',(64,64))
    # Mode of each logical tile preserves connected clusters; no blurred edges.
    for y in range(64):
        for x in range(64):
            tile=rgba.crop((x*w//64,y*h//64,(x+1)*w//64,(y+1)*h//64))
            c=Counter(tile.getdata()).most_common(1)[0][0]
            if c[3]:
                c=(*min(palette,key=lambda p:sum((p[j]-c[j])**2 for j in range(3))),255)
            result.putpixel((x,y),c)
    result.save(root/variant/'crater64.png')
    records.append({'variant':variant,'masterSize':im.size,'outputSize':result.size,'sha256':hashlib.sha256((root/variant/'crater64.png').read_bytes()).hexdigest(),'palette':palette,'source':str(source),'seedNote':'GPT node seed unsupported; Gemini seed best effort only','decision':'adopt: flatter footprint, restrained cracks, lower rim' if variant=='gpt-v1' else 'reject: rim too tall and footprint too round'})
target=Path('Assets/_Project/Art/VFX/MageTower/MeteorScorch.png');target.write_bytes((root/'gpt-v1/crater64.png').read_bytes())
(root/'finish-manifest.json').write_text(json.dumps({'pillow':Image.__version__,'process':'boundary chroma flood, strict enclosed chroma removal, mode tile 16px, six-color nearest palette; no frame diffusion','palette':palette,'variants':records,'output':str(target)},indent=2),encoding='utf8')
canvas=Image.new('RGB',(768,432),(51,77,42));draw=ImageDraw.Draw(canvas)
for i,v in enumerate(['nano-v1','gpt-v1']):
    im=Image.open(root/v/'crater64.png');canvas.paste(im.resize((384,384),Image.Resampling.NEAREST),(i*384,32),im.resize((384,384),Image.Resampling.NEAREST));draw.text((i*384+8,8),v,fill='white')
canvas.save(root/'comparison.png')
