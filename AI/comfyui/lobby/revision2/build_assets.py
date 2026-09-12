"""Technical chroma extraction, alpha crops and coordinate-preserving mobile exports.
Illustration changes are authored by ImageGen / Comfy Cloud, never painted here.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).parent
SOURCE = ROOT / 'source'
DEST = Path('Assets/UGUI/Art/Lobby')

def key_magenta(path):
    im = Image.open(path).convert('RGBA')
    pixels = np.asarray(im).copy()
    r,g,b = [pixels[:,:,i].astype(float) for i in range(3)]
    loose = (r>110)&(b>100)&(g<125)&(r-g>50)&(b-g>50)
    flood = Image.fromarray(loose.astype('uint8')*255)
    for point in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]:
        if flood.getpixel(point)==255:ImageDraw.floodfill(flood,point,128)
    background = np.asarray(flood)==128
    background |= (r>190)&(b>180)&(g<85)&(r-g>110)&(b-g>105)
    for _ in range(2):
        adjacent=np.asarray(Image.fromarray(background.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
        background |= adjacent&loose
    pixels[background,3]=0
    pixels[background,:3]=0
    return Image.fromarray(pixels)

manifest_path=DEST/'Lobby.layers.json'
manifest=json.loads(manifest_path.read_text('utf-8'))
layers={layer['name']:layer for layer in manifest['layers']}

def export(name, image, x, y, width=None, height=None, pivot=None):
    box=image.getbbox(); image=image.crop(box)
    if height is not None: width=round(image.width*height/image.height)
    elif width is not None: height=round(image.height*width/image.width)
    image=image.resize((width,height),Image.Resampling.LANCZOS)
    image.save(DEST/f'Lobby_{name}.png',optimize=True)
    layer=layers[name]
    layer.update(x=x,y=y,width=width,height=height)
    if pivot is not None:layer.update(pivotX=pivot[0],pivotY=pivot[1])

background=Image.open(SOURCE/'Background_Siege_Final.png').convert('RGB').resize((1536,1536),Image.Resampling.LANCZOS)
background.save(DEST/'Lobby_Background.png',optimize=True)

# The 1K separation has the cleanest elbow joint. The 2K candidate retained a duplicate forearm.
scale=529/934
for name,index in [('Knight',0),('Sword',1)]:
    im=Image.open(SOURCE/f'knightalpha_{index}.png').convert('RGBA')
    alpha=np.asarray(im.getchannel('A')).copy();alpha[alpha<4]=0;im.putalpha(Image.fromarray(alpha))
    box=im.getbbox()
    x=round(377+box[0]*scale);y=round(699+box[1]*scale)
    export(name,im,x,y,width=round((box[2]-box[0])*scale),pivot=(565,1160) if name=='Knight' else (377+525*scale,699+700*scale))
layers['Knight'].update(angle=.30,rise=2.2,period=2.8)
layers['Sword'].update(angle=3.6,rise=.6,period=5.4)
export('Archer',key_magenta(SOURCE/'Archer_Master.png'),138,734,height=408,pivot=(285,1040))
layers['Archer'].update(angle=.30,rise=2.7,period=2.6)
layers['Mage'].update(angle=.45,rise=2.5,period=3.0)
king=key_magenta(SOURCE/'BanditKing_Final.png')
export('BanditRight',king,810,596,width=175,pivot=(897,713))
layers['BanditRight'].update(angle=.65,rise=.2,period=9.2,phase=2.7)
layers['BanditLeft'].update(angle=.7,rise=.3,period=7.2)

# Flat monochrome UI symbols; the globe is reused directly from Layer Lab.
for name in ['Play','Stop']:
    icon=Image.new('RGBA',(256,256),(0,0,0,0));d=ImageDraw.Draw(icon)
    if name=='Play':d.polygon([(74,39),(208,128),(74,217)],fill='white')
    else:d.rounded_rectangle((58,58,198,198),radius=8,fill='white')
    icon.resize((64,64),Image.Resampling.LANCZOS).save(DEST/f'Lobby_{name}.png',optimize=True)

manifest['layers']=[layers[k] for k in ['BanditLeft','BanditRight','Mage','Archer','Knight','Sword']]
manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
for file in sorted(DEST.glob('*.png')):
    im=Image.open(file);assert max(im.size)<=2048
    print(file.name,im.size,file.stat().st_size)
print('Total PNG bytes',sum(p.stat().st_size for p in DEST.glob('*.png')))
