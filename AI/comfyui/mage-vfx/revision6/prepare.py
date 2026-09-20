"""Deterministic reference edits; cloud palette/color finishing is in finish_cloud.py.
No external originals or frame-by-frame diffusion are used.
"""
from pathlib import Path
import json, math, subprocess
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).parent
REF = HERE / 'references'
OUT = Path('Assets/_Project/Art/VFX/MageTower/Refined')
KEYS = ['Lightning','IceSpike','FireTornado','ArcaneVolley','VenomMist','StoneSeal','Sanctuary','VoidRift']
REF.mkdir(exist_ok=True)

def original(path):
    dest = REF / Path(path).name
    if not dest.exists(): dest.write_bytes(subprocess.check_output(['git','show','c2a6a4b0d:'+path]))
    return Image.open(dest).convert('RGBA')

def icon(key): return original('Assets/_Project/Art/Icons/MageTower/'+key+'.png')
def foreground(im, threshold=56):
    a = np.array(im); a[:,:,3] = np.where(a[:,:,:3].max(axis=2)>threshold,255,0)
    return Image.fromarray(a)
def canvas(): return Image.new('RGBA',(48,48),(21,26,33,255))

def icons():
    result={}
    bolt=foreground(icon('Lightning')).crop((10,18,35,44)).resize((13,30),Image.Resampling.NEAREST)
    normal=canvas()
    for x,y in [(1,15),(17,4),(33,14)]: normal.alpha_composite(bolt,(x,y))
    result['Lightning']=normal
    # Keep the approved cloud/one heavy bolt; violet finishing happens in Comfy.
    result['Lightning_Bloom']=icon('Lightning')
    ice=canvas(); spike=foreground(icon('IceSpike'))
    for x,y in [(0,19),(26,19)]: ice.alpha_composite(spike.resize((22,27),Image.Resampling.NEAREST),(x,y))
    ice.alpha_composite(spike.resize((36,44),Image.Resampling.NEAREST),(6,0))
    d=ImageDraw.Draw(ice);d.line([(4,42),(12,45),(35,45),(44,41)],fill=(175,226,238),width=1)
    result['IceSpike_Bloom']=ice
    stars=icon('ArcaneVolley'); a=np.array(stars)
    mask=a[:,:,:3].max(axis=2)>60
    rgb=a[:,:,:3].copy().astype(float)
    a[:,:,:3][mask]=np.stack([np.maximum(rgb[:,:,2],rgb[:,:,0]),rgb[:,:,1]*.72+rgb[:,:,0]*.1,rgb[:,:,2]*.57+rgb[:,:,0]*.12],axis=2)[mask].clip(0,255).astype('uint8')
    result['ArcaneVolley']=Image.fromarray(a)
    meteor=icon('Meteor');d=ImageDraw.Draw(meteor)
    d.line([(6,42),(15,44),(26,44),(34,40)],fill=(220,73,33),width=1)
    d.line([(11,38),(15,35),(14,30),(19,27),(25,28)],fill=(255,174,71),width=1)
    d.point([(13,40),(22,42),(29,38)],fill=(255,193,97))
    result['ArcaneVolley_Bloom']=meteor
    for k in ['FireTornado','VenomMist','StoneSeal','Sanctuary','VoidRift']:
        im=icon(k+'_Bloom');a=np.array(im);m=a[:,:,:3].max(axis=2)>62
        a[:,:,:3][m]=np.minimum(255,a[:,:,:3][m].astype(float)*1.16+5).astype('uint8')
        im=Image.fromarray(a);d=ImageDraw.Draw(im)
        if k=='FireTornado':
            d.line([(6,22),(4,18),(6,12),(12,8)],fill=(255,146,52),width=1)
            d.line([(38,16),(42,21),(41,28),(36,32)],fill=(255,199,103),width=1)
        elif k=='VenomMist':
            for x,y in [(13,12),(30,8),(36,18)]: d.rectangle((x,y,x+2,y+2),fill=(177,225,90))
        elif k=='StoneSeal':
            d.line([(11,35),(11,22),(15,16),(17,8),(20,5)],fill=(229,195,139),width=1)
            d.line([(29,12),(33,20),(36,33)],fill=(203,169,114),width=1)
        elif k=='Sanctuary':
            d.arc((4,31,44,45),5,170,fill=(255,219,97),width=1)
            for x,y in [(7,25),(39,25)]: d.line((x-2,y,x+2,y),fill=(255,230,161));d.line((x,y-2,x,y+2),fill=(255,230,161))
        else:
            for points in [[(5,13),(10,15),(8,18)],[(37,5),(35,10),(32,8)],[(43,33),(38,31),(40,28)],[(12,43),(15,38),(18,40)]]:
                d.polygon(points,fill=(190,124,227))
        result[k+'_Bloom']=im
    strip=Image.new('RGBA',(48*len(result),48))
    for i,(key,im) in enumerate(result.items()): im.save(HERE/(key+'-draft.png'));strip.paste(im,(i*48,0))
    strip.save(HERE/'icon-reference-strip.png')
    (HERE/'icon-layout.json').write_text(json.dumps(list(result),indent=2),encoding='utf8')

records=[]
def sheet(name, frames, fps, columns=6, pivot=(.5,.5)):
    w,h=frames[0].size; rows=math.ceil(len(frames)/columns)
    out=Image.new('RGBA',(w*columns,h*rows))
    for i,im in enumerate(frames): out.paste(im,(i%columns*w,i//columns*h))
    out.save(OUT/(name+'.png'))
    records.append(dict(name=name,width=w,height=h,count=len(frames),columns=columns,pivot=list(pivot),fps=fps,loop=name=='MeteorEmbers',ppu=32,source='revision6/prepare.py; existing art and deterministic ground masks'))

def effects():
    projectile=original('Assets/_Project/Art/VFX/MageTower/ArcaneProjectile.png');a=np.array(projectile)
    colors=sorted({tuple(p[:3]) for p in a.reshape(-1,4) if p[3]},key=lambda c:sum(int(v) for v in c))
    palette=[(205,93,89),(245,154,128),(255,234,199)]
    for before,after in zip(colors,palette): a[:,:,:3][np.all(a[:,:,:3]==before,axis=2)]=after
    warm=Image.fromarray(a)
    sheet('StarfallWarm',[warm.crop((0,i*64,64,(i+1)*64)) for i in range(6)],12,columns=3)
    pulses=[]
    for i in range(8):
        im=Image.new('RGBA',(40,28));d=ImageDraw.Draw(im);t=i/7
        rx=3+16*t;ry=rx*.65
        color=[(255,226,175),(250,170,113),(225,105,73),(166,61,50)][min(3,i//2)]
        d.ellipse((round(20-rx),round(14-ry),round(20+rx),round(14+ry)),outline=color+(255,),width=2 if i<3 else 1)
        if i<3: d.ellipse((18-i,13-i,22+i,15+i),fill=(255,226,175,255))
        pulses.append(im)
    sheet('StarfallPulse',pulses,20,columns=4)
    # Same brown crater, slightly enlarged. A denser logical canvas prevents larger pixels.
    scorch=original('Assets/_Project/Art/VFX/MageTower/MeteorScorch.png')
    ground=scorch.crop((6,18,58,46)).resize((108,68),Image.Resampling.NEAREST)
    base=Image.new('RGBA',(112,76));base.alpha_composite(ground,(2,4))
    sheet('MeteorGround',[base],1,columns=1)
    mask=np.array(base)[:,:,3]>0
    branches=[[(55,35),(46,31),(34,33),(25,27),(13,28)],[(48,31),(45,23),(32,20),(30,12)],
              [(54,35),(67,28),(76,30),(87,21),(97,24)],[(75,30),(78,40),(94,43),(100,50)],
              [(55,37),(48,45),(51,53),(40,62)],[(49,46),(33,45),(23,51),(12,47)],
              [(57,38),(68,45),(66,56),(78,63)],[(63,28),(62,19),(72,12)]]
    embers=[]
    for i in range(16):
        im=Image.new('RGBA',base.size);d=ImageDraw.Draw(im)
        for j,line in enumerate(branches):
            phase=.5+.5*math.sin(i/16*math.tau-j*.45)
            color=[(119,39,29),(174,47,29),(221,70,31),(246,113,43)][min(3,int(phase*3.99))]
            d.line(line,fill=color+(255,),width=1)
        a=np.array(im);a[:,:,3][~mask]=0;im=Image.fromarray(a);embers.append(im)
    sheet('MeteorEmbers',embers,16,columns=4)
    catalog=OUT/'sheets.json';old=json.loads(catalog.read_text(encoding='utf8'))
    byname={s['name']:s for s in records}
    merged=[byname.pop(s['name'],s) for s in old]+list(byname.values())
    catalog.write_text(json.dumps(merged,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (HERE/'effect-settings.json').write_text(json.dumps(records,indent=2),encoding='utf8')

if __name__=='__main__': icons();effects();print('Prepared icon references, warm existing flight frames, small pulses and ground cracks.')
