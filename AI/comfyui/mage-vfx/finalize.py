"""Deterministic sprite finish. Purchased originals and Comfy outputs remain untouched."""
import json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import PIL
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OUT=ROOT/'Assets/_Project/Art/VFX/MageTower/Refined'
REVIEW=ROOT/'Recordings/FoundationRevision/VfxReview'
OUT.mkdir(parents=True,exist_ok=True); REVIEW.mkdir(parents=True,exist_ok=True)
FIRE=['#551d16','#b6380b','#f36614','#ffb42b','#ffdf60','#fff3b0']
ICE=['#154da9','#287ddd','#4db8e8','#74dcdf','#b3f1e8','#eefbda']
HOLY=['#287957','#50ab79','#87d6a0','#bcebb3','#e2efb0','#fff2c7']
VOID=['#221d35','#433459','#705095','#a16bc2','#c599e2','#ece2f6']
records=[]

def rgb(hexes): return np.array([tuple(bytes.fromhex(h.lstrip('#'))) for h in hexes],dtype=np.int32)

def palette(image, colors, key=False, threshold=20):
    arr=np.asarray(image.convert('RGBA')).copy()
    if key: arr[:,:,3]=np.where(arr[:,:,:3].max(axis=2)>threshold,255,0)
    pal=rgb(colors)
    distances=((arr[:,:,:3,None].astype(np.int32)-pal.T[None,None,:,:])**2).sum(axis=2)
    arr[:,:,:3]=pal[np.argmin(distances,axis=2)]
    arr[:,:,3]=np.where(arr[:,:,3]>=100,255,0)
    arr[arr[:,:,3]==0,:3]=0
    return Image.fromarray(arr)

def master(file, size, colors, threshold=24, crop=None):
    image=Image.open(HERE/file).convert('RGBA')
    pixels=np.asarray(image).copy()
    pixels[:,:,3]=np.where(pixels[:,:,:3].max(axis=2)>threshold,255,0)
    image=Image.fromarray(pixels)
    if crop: image=image.crop(crop)
    # BOX evaluates logical pixel coverage; palette quantization then binary alpha.
    return palette(image.resize(size,Image.Resampling.BOX),colors)

def write(name,frames,pivot=(.5,.5),fps=12,loop=False,source=''):
    w,h=frames[0].size
    # Pack up to four columns to keep every source sheet below 2048 pixels.
    columns=min(4,len(frames));rows=math.ceil(len(frames)/columns)
    sheet=Image.new('RGBA',(w*columns,h*rows))
    for i,frame in enumerate(frames):
        assert frame.size==(w,h)
        sheet.paste(frame,((i%columns)*w,(i//columns)*h))
    sheet.save(OUT/(name+'.png'))
    records.append(dict(name=name,width=w,height=h,count=len(frames),columns=columns,pivot=list(pivot),fps=fps,loop=loop,ppu=32,source=source))
    return sheet

def meteor():
    m=master('revision1/meteor-zimage-original.png',(128,128),FIRE)
    arr=np.asarray(m);frames=[]
    # Rock remains stable. Only the trailing flame tongues receive a 1–2 logical-pixel wave.
    for phase in range(8):
        output=np.zeros_like(arr)
        for y in range(128):
            for x in range(128):
                if arr[y,x,3]==0: continue
                tail=max(0,min(1,(x-y+30)/85))
                shift=round(1.8*tail*math.sin(phase*math.tau/8+(x+y)*.14))
                nx=x+shift; ny=y+shift
                if 0<=nx<128 and 0<=ny<128:output[ny,nx]=arr[y,x]
        frames.append(Image.fromarray(output))
    write('MeteorFlight',frames,pivot=(.285,.28),fps=18,loop=True,source='Comfy revision1; masked flame advection, stable rock')

def glacier():
    m=master('revision4/Glacier.png',(112,128),ICE)
    # Reveal upward from the foot anchor, hold, then fracture down. Never squash the artwork.
    frames=[];arr=np.asarray(m);base=124
    for i,height in enumerate([.12,.62,1,1,1,1,1,.82,.52,.22,0]):
        f=arr.copy();f[:max(0,base-round(120*height)),:,3]=0
        if i>=7:
            yy,xx=np.indices(f.shape[:2]);f[((xx//7+yy//8)%5)<(i-7),3]=0
        frames.append(Image.fromarray(f))
    write('Glacier',frames,pivot=(.5,4/128),fps=14,source='Comfy revision4; foot-anchored reveal/hold/fracture masks')

def sanctuary():
    crest=master('revision4/Sanctuary.png',(64,64),HOLY)
    write('SanctuaryCrest',[crest],pivot=(.5,.08),source='Comfy revision4; static opaque emblem, motion from SanctuarySustainVfx')
    frames=[]
    colors=[tuple(c)+(255,) for c in rgb(HOLY)]
    for frame in range(12):
        image=Image.new('RGBA',(176,112));draw=ImageDraw.Draw(image)
        cx,cy=88,56
        for r,yr,c in [(82,48,colors[1]),(77,45,colors[3]),(66,38,colors[0])]:
            draw.ellipse((cx-r,cy-yr,cx+r,cy+yr),outline=c,width=1)
        for k in range(12):
            a=k*math.tau/12; x=round(cx+math.cos(a)*72);y=round(cy+math.sin(a)*41)
            c=colors[4 if (k-frame)%12 in (0,1,2) else 2]
            draw.line((x-2,y,x+2,y),fill=c,width=1);draw.line((x,y-2,x,y+2),fill=c,width=1)
        # One broad outward wave for each healing interval; silhouette and rune ring stay put.
        r=12+frame*6;yr=round(r*.58)
        draw.ellipse((cx-r,cy-yr,cx+r,cy+yr),outline=colors[3 if frame<8 else 1],width=1)
        frames.append(image)
    write('SanctuaryGround',frames,fps=12/.7,loop=True,source='Code-native 32px/unit ritual rings; HolyBlessing quarter-view and green palette reference; no stretched source pixels')
    frames=[]
    for frame in range(6):
        image=Image.new('RGBA',(48,32));draw=ImageDraw.Draw(image);r=6+frame*2
        draw.ellipse((24-r,23-round(r*.45),24+r,23+round(r*.45)),outline=colors[4 if frame<4 else 2],width=1)
        for x in (12,35):
            y=20-frame*2;draw.line((x-1,y,x+1,y),fill=colors[3]);draw.line((x,y-1,x,y+1),fill=colors[3])
        frames.append(image)
    write('HealingFeet',frames,pivot=(.5,9/32),source='Code-native foot pulse; quarter-view ring and two short rising motes, opaque active pixels')

def telegraph():
    image=Image.new('RGBA',(80,48));draw=ImageDraw.Draw(image)
    for r,h,c in [(36,20,'#a78842'),(32,18,'#ead896'),(25,14,'#bea55d')]:draw.ellipse((40-r,24-h,40+r,24+h),outline=c,width=1)
    for k in range(8):
        a=k*math.tau/8;x=round(40+29*math.cos(a));y=round(24+16*math.sin(a));draw.line((x-1,y-1,x+1,y+1),fill='#fff2c7')
    write('StoneGlyph',[image],source='Code-native 32px/unit quarter-view ritual ring; replaces enlarged 64px HolyBlessing')

def reconstructed():
    atlas=Image.open(HERE/'revision3/Anime6B.png').convert('RGBA')
    for col,name,colors in [(0,'VoidRing',VOID),(1,'ThunderBolt',VOID),(2,'ThunderImpact',VOID)]:
        frames=[]
        for i in range(6):frames.append(palette(atlas.crop((col*128,i*128,(col+1)*128,(i+1)*128)),colors,key=True,threshold=19))
        write(name,frames,loop=col==0,source='Comfy revision3 Anime6B 4x → area 2x; original six authored poses, shared six-color finish')
    cloud=master('revision4/StormCloud.png',(112,64),['#211b35','#332d4b','#484360','#625678','#a778d1','#d2adf0'],threshold=20,crop=(16,160,1008,760))
    write('StormCloud',[cloud],source='Comfy revision4; removed hanging bolt element by masked lower crop, cloud body retained')

def explosion():
    path=ROOT/'Assets/ExternalAssets/2D_PFX/FX/Fire/11/Sprites/11.png'
    source=Image.open(path).convert('RGBA');frames=[]
    colors=['#594d42','#817a6b','#b1ad9b','#f37a28','#ffc15a','#fff2bb']
    # Original author order is rows left-to-right; 18 nonempty frames in the metadata.
    import yaml
    meta=yaml.safe_load(Path(str(path)+'.meta').read_text(encoding='utf8'))['TextureImporter']
    sprites=meta['spriteSheet']['sprites']
    sprites.sort(key=lambda s:int(s['name'].split()[-1]))
    for item in sprites:
        r=item['rect'];x,y,w,h=[round(r[k]) for k in ['x','y','width','height']]
        frame=source.crop((x,source.height-y-h,x+w,source.height-y)).resize((112,58),Image.Resampling.BOX)
        canvas=Image.new('RGBA',(112,64));canvas.paste(palette(frame,colors),(0,0));frames.append(canvas)
    write('MeteorImpact',frames,pivot=(.5,.20),fps=18,source=str(path.relative_to(ROOT))+'; authored sprite order; 162x84 → 112x58 coverage downsample + shared palette')

meteor();glacier();sanctuary();telegraph();reconstructed();explosion()
(OUT/'sheets.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8')
(HERE/'finish-manifest.json').write_text(json.dumps(dict(tool='Python/Pillow/NumPy',pillow=PIL.__version__,numpy=np.__version__,command='PYTHONPATH=.utmp/qa-deps python AI/comfyui/mage-vfx/finalize.py',output=str(OUT.relative_to(ROOT)),settings='32 logical pixels/unit, binary alpha, max six visible colors per sheet, point rendering, no frame diffusion',sheets=records),ensure_ascii=False,indent=2),encoding='utf8')
# Review at a shared logical-pixel scale, with a neutral game-like green background.
contact=Image.new('RGB',(640,math.ceil(len(records)/4)*210),'#536740');draw=ImageDraw.Draw(contact)
for index,r in enumerate(records):
    sheet=Image.open(OUT/(r['name']+'.png'));frame=min(2,r['count']-1);x=frame%r['columns']*r['width'];y=frame//r['columns']*r['height']
    sprite=sheet.crop((x,y,x+r['width'],y+r['height']));sprite.thumbnail((150,175),Image.Resampling.NEAREST)
    px=(index%4)*160;py=(index//4)*210;contact.paste(sprite,(px+(160-sprite.width)//2,py+24),sprite);draw.text((px+4,py+4),r['name'],fill='white')
contact.save(REVIEW/'refined-contact.png')
print(json.dumps({'sheets':len(records),'frames':sum(r['count'] for r in records),'contact':str(REVIEW/'refined-contact.png')}))
