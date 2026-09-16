"""Deterministic derivatives of owned sheets and generated icon; external originals untouched."""
from pathlib import Path
from PIL import Image
from collections import Counter
import json,hashlib
root=Path.cwd();out=root/'Assets/_Project/Art/VFX/MageTower';out.mkdir(exist_ok=True)
records=[]
def derivative(source,key,fn):
 source=root/source;im=Image.open(source).convert('RGBA');im.putdata([fn(c) for c in im.getdata()]);dest=out/(key+'.png');im.save(dest)
 records.append({'source':str(source.relative_to(root)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'output':str(dest.relative_to(root)),'output_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'colors':len({c for c in im.getdata() if c[3]>0}),'size':im.size})
base='Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/'
for filename,key in [('ElectricLighting2','ThunderViolet'),('ElectricShield','CloudSparksViolet'),('ElectricExplosion','ThunderImpactViolet')]:
 derivative(base+'Electricity/'+filename+'.png',key,lambda c:(0,0,0,0) if c[3]==0 else ((202,171,236,c[3]) if c[0]>220 else (126,88,187,c[3])))
for filename,key in [('ElectricExplosion','ArcaneImpact')]:
 derivative(base+'Electricity/'+filename+'.png',key,lambda c:(0,0,0,0) if c[3]==0 else ((212,212,244,c[3]) if c[0]>220 else (123,133,190,c[3])))
for filename,key in [('VoidBlackHole','VoidRiftMuted'),('VoidExplosion2','VoidCollapseMuted')]:
 derivative(base+'Void/'+filename+'.png',key,lambda c:(0,0,0,0) if c[3]==0 else ((187,157,208,c[3]) if c[0]>180 else (111,90,138,c[3]) if c[0]>70 else (35,29,46,c[3])))
# Background is RGB 21..26/26..29/32..35; the distinct charcoal rock is RGB 42/47/58.
derivative('Assets/_Project/Art/Icons/MageTower/Meteor.png','MeteorProjectile',lambda c:(0,0,0,0) if max(c[:3])<=35 else c)
# The icon shares a few background shades with the rock's deepest pits. Restore
# enclosed transparent regions so terrain cannot show through the solid projectile.
meteor_path=out/'MeteorProjectile.png';meteor=Image.open(meteor_path).convert('RGBA')
transparent={(x,y) for y in range(meteor.height) for x in range(meteor.width) if meteor.getpixel((x,y))[3]==0}
filled=0
while transparent:
 todo=[transparent.pop()];region=[]
 while todo:
  x,y=todo.pop();region.append((x,y))
  for neighbor in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
   if neighbor in transparent:transparent.remove(neighbor);todo.append(neighbor)
 if any(x==0 or y==0 or x==meteor.width-1 or y==meteor.height-1 for x,y in region):continue
 for point in region:meteor.putpixel(point,(42,47,58,255))
 filled+=len(region)
meteor.save(meteor_path)
records[-1].update(output_sha256=hashlib.sha256(meteor_path.read_bytes()).hexdigest(),enclosedAlphaPixelsRestored=filled)
# Isolate the central arrow of the approved icon; preserve its exact pixel silhouette.
source=root/'Assets/_Project/Art/Icons/MageTower/ArcaneVolley.png';icon=Image.open(source).convert('RGB')
points={(x,y) for y in range(48) for x in range(48) if max(icon.getpixel((x,y)))>50};groups=[]
while points:
 todo=[points.pop()];group=[]
 while todo:
  x,y=todo.pop();group.append((x,y))
  for nx in range(x-1,x+2):
   for ny in range(y-1,y+2):
    if (nx,ny) in points:points.remove((nx,ny));todo.append((nx,ny))
 groups.append(group)
arrow=max(groups,key=len);sheet=Image.new('RGBA',(64,384))
for frame in range(6):
 for x,y in arrow:
  light=max(icon.getpixel((x,y)))
  color=(210,205,239) if light>195 else (164,160,211) if light>145 else (119,123,178) if light>100 else (77,87,135)
  sheet.putpixel((x+8,y+8+frame*64),(*color,255))
dest=out/'ArcaneProjectile.png';sheet.save(dest)
records.append({'source':str(source.relative_to(root)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'output':str(dest.relative_to(root)),'output_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'colors':4,'size':sheet.size,'process':'largest 8-connected foreground component; four-color mapping; six identical frames, motion supplied by gameplay'})
(Path(__file__).parent/'vfx-derivatives.json').write_text(json.dumps({'process':'exact palette remap with preserved alpha; meteor removes measured background RGB range and restores enclosed rock alpha with its existing charcoal color','pillow':Image.__version__,'assets':records},ensure_ascii=False,indent=2),encoding='utf8')
