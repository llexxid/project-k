"""Lossless alpha crops, coordinate-preserving composition and mobile texture sizing.

Run from repository root. Artwork is generated externally; this script never redraws it.
Raw generation outputs stay outside Assets and do not ship in the player.
"""
import json
import runpy
import sys
from pathlib import Path

import numpy as np
from PIL import Image

current_five = Path(__file__).parent / 'revision5'
if (current_five / 'source' / 'Siege_Final.png').exists():
    runpy.run_path(str(current_five / 'build_assets.py'), run_name='__main__')
    sys.exit(0)

latest = Path(__file__).parent / 'revision4'
if (latest / 'source' / 'Background_CrystalBolts.png').exists():
    runpy.run_path(str(latest / 'build_assets.py'), run_name='__main__')
    sys.exit(0)

current = Path(__file__).parent / 'revision3'
if (current / 'source' / 'Background_SingleTower.png').exists():
    runpy.run_path(str(current / 'build_assets.py'), run_name='__main__')
    sys.exit(0)

revision = Path(__file__).parent / 'revision2'
if (revision / 'source' / 'BanditKing_Final.png').exists():
    runpy.run_path(str(revision / 'build_assets.py'), run_name='__main__')
    sys.exit(0)

SOURCE = Path(__file__).parent / 'source'
DEST = Path('Assets/UGUI/Art/Lobby')
DEST.mkdir(parents=True, exist_ok=True)

# Original master is placed at (512,256) in a 2048 square safe-crop background.
background_path = SOURCE / 'Background_Clean.png'
if not background_path.exists():
    raise FileNotFoundError('The reviewed, text-free Background_Clean.png is required.')
background = Image.open(background_path).convert('RGB').resize((2048, 2048), Image.Resampling.LANCZOS)
central = Image.open(SOURCE / 'Lobby_Background_Central.png').convert('RGB')
y, x = np.mgrid[0:1536, 0:1024]
edge = np.minimum(np.minimum(x, 1023-x), np.minimum(y, 1535-y))
mask = Image.fromarray((np.clip(edge/20, 0, 1)*255).astype('uint8'))
background.paste(central, (512, 256), mask)
background.resize((1536, 1536), Image.Resampling.LANCZOS).save(DEST/'Lobby_Background.png', optimize=True)

# Source index, draw order and rest pivots measured against the master (1024x1536).
specs = [
    (5, 'BanditLeft', '', (112, 580), .45, .15, 7.2, .1, True),
    (6, 'BanditRight', '', (962, 718), .7, .2, 8.4, 2.7, True),
    (3, 'Mage', '', (690, 860), .55, 1.4, 4.1, 1.7, False),
    (2, 'Archer', '', (285, 1040), .45, 1.5, 3.8, .5, False),
    (4, 'Knight', '', (565, 1160), .35, 1.1, 4.4, 0, False),
    (7, 'Sword', 'Knight', (708, 1096), 1.35, 0, 3.7, .8, False),
]
layers=[]
for index, name, parent, pivot, angle, rise, period, phase, startled in specs:
    image = Image.open(SOURCE/f'Separated_{index:02}.png').convert('RGBA')
    image = image.resize((1024, 1536), Image.Resampling.LANCZOS)
    # Alpha below 2/255 is invisible spill that otherwise wastes a whole atlas rectangle.
    alpha=np.asarray(image.getchannel('A')).copy()
    alpha[alpha<2]=0
    image.putalpha(Image.fromarray(alpha))
    box=image.getbbox()
    crop=image.crop(box)
    crop.save(DEST/f'Lobby_{name}.png', optimize=True)
    layers.append(dict(name=name,parent=parent,x=box[0],y=box[1],width=crop.width,height=crop.height,
                       pivotX=pivot[0],pivotY=pivot[1],angle=angle,rise=rise,period=period,phase=phase,startled=startled))

for language in ('KO','EN'):
    image=Image.open(SOURCE/f'Logo_{language}.png').convert('RGBA')
    box=image.getchannel('A').point(lambda a:255 if a>12 else 0).getbbox()
    box=(max(0,box[0]-6),max(0,box[1]-6),min(image.width,box[2]+6),min(image.height,box[3]+6))
    image=image.crop(box)
    image.thumbnail((1024,720),Image.Resampling.LANCZOS)
    image.save(DEST/f'Lobby_Logo_{language}.png',optimize=True)

# Code-authored UI fade, no generated decoration or extra full-screen texture.
fade=np.empty((128,4,4),dtype='uint8')
for row in range(128):
    t=row/127
    # Keep the start hint readable even when a square/foldable view places it over the sunlit path.
    fade[row,:,:]=(9,23,29,round((1-(1-t)**3)*.97*255))
Image.fromarray(fade).save(DEST/'Lobby_FooterFade.png',optimize=True)
(DEST/'Lobby.layers.json').write_text(json.dumps({'canvas':[2048,2048],'sourceOffset':[512,256],'layers':layers},indent=2),encoding='utf-8')
for file in sorted(DEST.glob('*.png')):
    image=Image.open(file)
    assert max(image.size)<=2048
    print(file.name,image.size,file.stat().st_size)
print('Total shipped PNG bytes:',sum(p.stat().st_size for p in DEST.glob('*.png')))
