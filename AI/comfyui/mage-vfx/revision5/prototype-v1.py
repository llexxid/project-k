"""Bake coherent spell animation from original frames and one Comfy material master.

No independently diffused animation frames. Originals and previous passes are preserved.
"""
from pathlib import Path
import json, math, hashlib
import numpy as np
from PIL import Image, ImageDraw
import PIL

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OUT = ROOT / 'Assets/_Project/Art/VFX/MageTower/Refined'
REVIEW = ROOT / 'Recordings/SpellAnimationRevision/Art'
REVIEW.mkdir(parents=True, exist_ok=True)
FIRE = np.array([[76, 30, 25], [143, 48, 25], [222, 73, 18],
                 [255, 134, 31], [255, 199, 64], [255, 239, 156]], dtype=np.uint8)
records = []


def sheet(name, frames, pivot, fps, loop, source):
    w, h = frames[0].size
    columns = min(8, len(frames))
    image = Image.new('RGBA', (w * columns, h * math.ceil(len(frames) / columns)))
    for i, frame in enumerate(frames):
        image.paste(frame, (i % columns * w, i // columns * h))
    image.save(OUT / (name + '.png'))
    records.append(dict(name=name, width=w, height=h, count=len(frames), columns=columns,
                        pivot=pivot, fps=fps, loop=loop, ppu=32, source=source))
    review = Image.new('RGB', (w * 4, h * 2), '#526a37')
    for i, frame in enumerate(frames[::max(1, len(frames) // 8)][:8]):
        review.paste(frame, (i % 4 * w, i // 4 * h), frame)
    review.resize((review.width * 2, review.height * 2), Image.Resampling.NEAREST).save(REVIEW / (name + '-contact.png'))
    frames[0].save(REVIEW / (name + '.gif'), save_all=True, append_images=frames[1:], duration=round(1000/fps), loop=0, disposal=2)


def original_lightning():
    source = ROOT / 'Assets/_Project/Tests/Fixtures/MageTowerLegacy/Lightning/ThunderEffects.png'
    raw = Image.open(source).convert('RGBA')
    frames = [raw.crop((i*70, 0, (i+1)*70, 149)) for i in range(5)]
    sheet('LightningOriginal', frames, [.5, 0], 12, False,
          'Original ThunderEffects.png: exact pixels and five authored frames; 884652ec9 chain timing')


def tornado():
    source = ROOT / 'Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Fire/FireTornado.png'
    raw = Image.open(source).convert('RGBA')
    frames = []
    for i in range(12):
        old = raw.crop((0, (i//2)*64, 64, (i//2+1)*64))
        a = np.asarray(old)
        # Retain the rotating ribbons; sculpt only the solid, flat top into a
        # shallow irregular crown and add two outward-curling flame tongues.
        canvas = Image.new('RGBA', (64, 80)); canvas.paste(old, (0, 16))
        d = ImageDraw.Draw(canvas)
        occupied = np.argwhere(a[:, :, 3] > 0)
        top = int(occupied[:, 0].min())
        cap = np.flatnonzero(a[top, :, 3] > 0)
        for x in cap:
            edge = abs((x - cap.mean()) / max(1, len(cap)/2))
            if edge > .55:
                d.line((int(x), top+16, int(x), top+16+round((edge-.55)*5)), fill=(0,0,0,0))
        orange = tuple(a[top, cap[len(cap)//2]])
        gold = (250, 177, 95, 255)
        for j, x in enumerate([int(cap.mean())-4, int(cap.mean())+5]):
            curl = round(math.sin(i*math.tau/12+j*2)*3)
            base = top+20+j
            d.polygon([(x-3, base), (x-2+curl, base-5), (x+curl+1, base-10-j),
                       (x+curl+1, base-6), (x+4, base-2), (x+3, base+2)], fill=orange)
            d.line([(x,base), (x+1+curl,base-4), (x+curl+1,base-7-j)], fill=gold, width=1)
        frames.append(canvas)
    sheet('FireVortexCrown', frames, [.5, 14/80], 24, True,
          'Purchased FireTornado six frames; unchanged lower ribbons, local crown edit and 16px headroom')


def quantize(rgb):
    return np.argmin(((rgb.astype(np.int32)[...,None,:]-FIRE.astype(np.int32))**2).sum(-1), axis=-1)


def meteor():
    texture = np.asarray(Image.open(HERE/'surface-master.png').convert('RGB').resize((256,256), Image.Resampling.BOX))
    tex = quantize(texture)
    h,w=192,160; cy,cx=142,50
    yy,xx=np.mgrid[:h,:w]; X=xx-cx;Y=yy-cy
    u=(X-Y)/math.sqrt(2);v=(X+Y)/math.sqrt(2)
    r=np.sqrt((X/25)**2+(Y/28)**2)
    theta=np.arctan2(Y,X)
    frames=[]
    for frame in range(48):
        t=frame/20
        indices=np.zeros((h,w),dtype=np.uint8);alpha=np.zeros((h,w),dtype=bool)
        # Advect connected turbulent tongues away from the leading edge. The
        # broad plume changes silhouette, width and internal energy every frame.
        wave=4.1*np.sin(u*.105-t*5.2)+2*np.sin(u*.22-t*8.3)
        width=np.maximum(0,23*(1-np.clip(u,0,124)/124)**.72)
        lobe=2.4*np.sin(u*.3-t*7+v*.14)+1.8*np.sin(v*.27+u*.08-t*3)
        distance=np.abs(v-wave)+lobe
        plume=(u>-9)&(u<124)&(distance<width)
        energy=np.clip(1-distance/np.maximum(width,1),0,1)
        heat=np.clip(2+energy*2.7-.008*np.maximum(u,0)+.4*np.sin(u*.25-t*9),1,5).astype(np.uint8)
        indices[plume]=heat[plume];alpha|=plume
        # Small detached flame curls peel off both sides instead of translating
        # a rigid flame drawing along with the rock.
        dimage=Image.new('RGBA',(w,h));d=ImageDraw.Draw(dimage)
        for k in range(14):
            age=(t*.55+k*.137)%1
            along=24+age*103;across=(1 if k%2 else -1)*(15+age*15)+3*math.sin(age*8+k)
            px=cx+(along+across)/math.sqrt(2);py=cy+(-along+across)/math.sqrt(2)
            size=max(1,round(3*(1-age)));length=round(2+5*(1-age))
            col=tuple(FIRE[4 if age<.5 else 2])+(255,)
            d.line((round(px),round(py),round(px+length),round(py-length)),fill=col,width=size)
        # Spherical texture coordinates rotate on two axes. Fractures disappear
        # around the limb and new facets enter view: real changing volume cues.
        nx=X/25;ny=-Y/28;nz=np.sqrt(np.maximum(0,1-nx*nx-ny*ny))
        yaw=.72*t-.34;roll=-.21*t+.2
        rx=nx*math.cos(roll)-ny*math.sin(roll);ry=nx*math.sin(roll)+ny*math.cos(roll)
        qx=rx*math.cos(yaw)+nz*math.sin(yaw);qz=-rx*math.sin(yaw)+nz*math.cos(yaw)
        tu=((np.arctan2(qx,qz)/math.tau+.5)*256).astype(int)%256
        tv=(np.clip(.5-np.arcsin(np.clip(ry,-1,1))/math.pi,0,.999)*256).astype(int)
        material=tex[tv,tu].copy()
        # Opaque faceted shading and a sparse molten pulse retain six colors.
        lighting=np.clip((nx*-.3+ny*-.35+nz*.8),0,1)
        material=np.maximum(0,material.astype(int)-(lighting<.4).astype(int))
        molten=(material>=3)&(np.sin(t*6+qx*8+ry*5)>.65)
        material=np.minimum(5,material+molten)
        contour=1+.027*np.sin(theta*7+t*.6)+.018*np.sin(theta*11-t*.8)
        body=r<contour
        indices[body]=material[body];alpha|=body
        # Narrow, irregular hot bow shock wraps the leading lower-left rim.
        rim=(r>=contour)&(r<contour+.09+.035*np.sin(theta*12-t*9))&(u<12)
        indices[rim]=np.where(u[rim]<-15,5,4);alpha|=rim
        rgba=np.zeros((h,w,4),dtype=np.uint8);rgba[:,:,:3]=FIRE[indices];rgba[:,:,3]=alpha*255
        image=Image.fromarray(rgba);image.alpha_composite(dimage)
        # Distinct rock chips rotate and lag into the wake, separate from sparks.
        d=ImageDraw.Draw(image)
        for k in range(3):
            age=(t*.3+k*.31)%1;along=22+age*40;across=(1 if k%2 else -1)*(22+age*10)
            px=cx+(along+across)/math.sqrt(2);py=cy+(-along+across)/math.sqrt(2)
            rr=3.2*(1-.5*age);a=t*1.8+k
            points=[(round(px+rr*math.cos(a+j*math.tau/5)),round(py+rr*math.sin(a+j*math.tau/5))) for j in range(5)]
            d.polygon(points,fill=tuple(FIRE[1])+(255,));d.line(points[:3],fill=tuple(FIRE[3])+(255,),width=1)
        frames.append(image)
    sheet('MeteorFlight',frames,[cx/w,(h-cy)/h],20,False,
          'Comfy v5 basalt master; baked two-axis spherical rotation, six-color shading, advected plume, bow shock, sparks and rock chips')


if __name__=='__main__':
    original_lightning();tornado()
    if (HERE/'surface-master.png').exists():meteor()
    catalog=OUT/'sheets.json';old=json.loads(catalog.read_text('utf8'))
    updated={s['name']:s for s in records}
    merged=[updated.pop(s['name'],s) for s in old]+list(updated.values())
    catalog.write_text(json.dumps(merged,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (HERE/'finish-manifest.json').write_text(json.dumps(dict(pillow=PIL.__version__,numpy=np.__version__,
        command='python AI/comfyui/mage-vfx/revision5/finalize.py',sheets=records,
        notes='32 logical pixels/unit except exact original lightning displayed at its original 1.5/64 scale. No per-frame diffusion.'),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print([(s['name'],s['count']) for s in records])
