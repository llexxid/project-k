"""Deterministic 48px compositions from the actual in-game VFX working copies.
No diffusion model, external original modification, or generated animation frames.
"""
from pathlib import Path
import hashlib, json, shutil
from PIL import Image, ImageDraw, __version__ as pillow_version

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
ART = ROOT / 'Assets/_Project/Art'
SOURCES = {
    'stone': ART / 'VFX/PixelArtRPGVFX/Textures/Earth/EarthRock.png',
    'cross': ART / 'VFX/PixelArtRPGVFX/Textures/Holy/HolyCross.png',
    'ring': ART / 'VFX/PixelArtRPGVFX/Textures/Holy/HolyBlessing.png',
    'poison': ART / 'VFX/PoisonEffect/Animation/Sprites/Poison_Effect_05-1.png',
    'bubbles': ART / 'VFX/PoisonEffect/Animation/Sprites/Poison_Effect_05-2.png',
}

def frame(key, n=0):
    im = Image.open(SOURCES[key]).convert('RGBA')
    if key not in ('poison', 'bubbles'):
        im = im.crop((0, 64*n, 64, 64*(n+1)))
    elif key == 'bubbles':
        im = im.crop((0, 0, 96, 96))
    im.putalpha(im.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return im.crop(im.getbbox())

def paste(canvas, source, box):
    x,y,w,h=box
    canvas.alpha_composite(source.resize((w,h),Image.Resampling.NEAREST),(x,y))

def finish(im):
    alpha=im.getchannel('A')
    result=im.convert('RGB').quantize(colors=15,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGBA')
    result.putalpha(alpha.point(lambda a: 255 if a>=128 else 0))
    return result

def main():
    output=HERE/'v2';output.mkdir(exist_ok=True)
    old=HERE/'before';old.mkdir(exist_ok=True)
    for key in ('VenomMist','StoneSeal','Sanctuary'):
        for suffix in ('','_Bloom'):
            src=ART/'Icons/MageTower'/f'{key}{suffix}.png'
            if not (old/src.name).exists():shutil.copy2(src,old/src.name)
    stone=Image.new('RGBA',(48,48))
    # Match the actual overlapping prefab layers and their proportions/tints.
    rock=frame('stone',3)
    for box,tint in [((8,16,22,29),(.84,.79,.70)),((12,8,28,37),(.91,.86,.77)),((23,20,20,26),(.84,.79,.70))]:
        tinted=rock.copy();pixels=tinted.load()
        for y in range(tinted.height):
            for x in range(tinted.width):
                r,g,b,a=pixels[x,y];pixels[x,y]=(round(r*tint[0]),round(g*tint[1]),round(b*tint[2]),a)
        paste(stone,tinted,box)
    poison=Image.new('RGBA',(48,48))
    paste(poison,frame('poison'),(3,21,42,23))
    paste(poison,frame('bubbles'),(11,14,27,19))
    sanctuary=Image.new('RGBA',(48,48))
    ring=frame('ring',0)
    # The authored ground rune is the lower 9px of the cropped VFX frame.
    ring=ring.crop((0,ring.height-9,ring.width,ring.height))
    paste(sanctuary,ring,(3,32,42,12))
    paste(sanctuary,frame('cross',3),(12,3,26,35))
    outputs=[]
    for key,im in [('VenomMist',poison),('StoneSeal',stone),('Sanctuary',sanctuary)]:
        im=finish(im)
        for bloom in (False,True):
            result=im.copy()
            if bloom:
                pixels=result.load()
                for y in range(48):
                    for x in range(48):
                        r,g,b,a=pixels[x,y]
                        if a:
                            # Preserve every silhouette pixel; bloom only brightens the existing effect.
                            pixels[x,y]=(min(255,round(r*1.10+5)),min(255,round(g*1.08+5)),min(255,round(b*1.12+8)),a)
            name=key+('_Bloom' if bloom else '')+'.png'
            result.save(output/name)
            shutil.copy2(output/name,ART/'Icons/MageTower'/name)
            outputs.append({'file':str((output/name).relative_to(ROOT)).replace('\\','/'),'colors':len(set(result.getdata())), 'logicalSize':[48,48],'sha256':hashlib.sha256((output/name).read_bytes()).hexdigest()})
    sheet=Image.new('RGB',(768,440),(28,25,22));draw=ImageDraw.Draw(sheet)
    for i,key in enumerate(('VenomMist','StoneSeal','Sanctuary')):
        draw.text((i*256+8,4),key+' BEFORE / AFTER',fill='white')
        for j,folder in enumerate((old,output)):
            im=Image.open(folder/(key+'.png')).convert('RGBA').resize((192,192),Image.Resampling.NEAREST)
            sheet.paste(im,(i*256+24,24+j*212),im)
    sheet.save(output/'comparison.png')
    manifest={'pipeline':'deterministic actual-VFX crop / NEAREST / binary alpha / <=16 RGBA colors','version':'v2',
        'model':None,'seed':'not applicable; deterministic','prompt':None,'jobId':None,'apiJson':None,'uiGraph':None,
        'comfyCatalog':'../mcp-catalog.json','comfyNodes':'../available-transform-nodes.json',
        'reason':'Existing VFX exactly define the silhouettes. Pixel-level local composition preserves their identity, masks and 48px palette with no diffusion variance, paid calls or GPU overhead. Comfy Cloud node schemas inspected; no workflow submitted.',
        'cost':{'actualGenerationSpendUSD':0,'paidCalls':0,'usageReport':'../usage-before.json','usageAfter':'../usage-after-matching.json','sameWindowTotalBeforeUSD':38.154966,'sameWindowTotalAfterUSD':38.154966},'Pillow':pillow_version,
        'sources':{k:{'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for k,p in SOURCES.items()},
        'settings':{'stoneFrame':3,'stoneLayers':[{'box':[8,16,22,29],'tint':[.84,.79,.70]},{'box':[12,8,28,37],'tint':[.91,.86,.77]},{'box':[23,20,20,26],'tint':[.84,.79,.70]}],'crossFrame':3,'ringFrame':0,'palette':15,'alphaThreshold':128,'resize':'NEAREST'},
        'changes':{'VenomMist':'Unchanged from v1: actual poison puddle and bubbles.', 'StoneSeal':'Android peak-frame inspection rejected v1 separated tall columns. v2 overlaps three proportionate rocks with the exact prefab tints; silhouette matches the actual compact rock seal.', 'Sanctuary':'Unchanged from v1: authored golden cross over the ground rune.'},'previousVersion':'../v1/manifest.json','outputs':outputs}
    (output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(outputs))

if __name__=='__main__':main()
