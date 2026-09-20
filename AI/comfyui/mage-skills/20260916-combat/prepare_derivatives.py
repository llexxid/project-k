"""Versioned, deterministic finishing from owned sprites; never edits ExternalAssets."""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib,json,shutil
root=Path.cwd();here=Path(__file__).parent;output=root/'Assets/_Project/Art/VFX/MageTower'
source=root/'Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures'
references=here/'references';references.mkdir(exist_ok=True)
records=[]
def record(image,name,sources,process):
 path=output/(name+'.png');image.save(path)
 records.append(dict(output=str(path.relative_to(root)),size=image.size,colors=len(set(c for c in image.getdata() if c[3])),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),sources=[dict(path=str(s.relative_to(root)),sha256=hashlib.sha256(s.read_bytes()).hexdigest()) for s in sources],process=process))
def frames(path):
 im=Image.open(path).convert('RGBA');return [im.crop((0,i*64,64,(i+1)*64)) for i in range(6)]
def sheet(parts):
 result=Image.new('RGBA',(64,384))
 for i,frame in enumerate(parts):result.alpha_composite(frame,(0,i*64))
 return result
def remap(im,fn):
 out=im.copy();out.putdata([fn(c) if c[3] else (0,0,0,0) for c in im.getdata()]);return out
bolt=source/'Electricity/ElectricLighting1.png'
violet=lambda c:(233,216,250,255) if c[0]>220 else (161,117,213,255)
strike=[]
for f in frames(bolt):
 f=remap(f,violet);f=f.crop((14,5,50,60)).resize((64,64),Image.Resampling.NEAREST);strike.append(f)
record(sheet(strike),'ThunderViolet',[bolt],'Base Lightning 1, common transparent margin crop (14,5,50,60), nearest resize, ivory/violet 2-tone remap; six original animation frames retained.')
cloud_src=references/'LightningCloud.before.png'
if not cloud_src.exists():shutil.copyfile(output/'LightningCloud.png',cloud_src)
cloud=Image.open(cloud_src).convert('RGBA')
palette=[(39,42,51,255),(55,58,68,255),(73,77,87,255),(96,100,109,255)]
opaque=sorted(set(c for c in cloud.getdata() if c[3]),key=lambda c:sum(c[:3]))
mapping={c:palette[min(3,i*4//len(opaque))] for i,c in enumerate(opaque)}
cloud=remap(cloud,lambda c:mapping[c]);record(cloud,'LightningCloud',[cloud_src],'Preserve silhouette, four neutral storm-gray values, binary alpha; no purple fill.')
sparks=[];mask=cloud.resize((64,24),Image.Resampling.NEAREST).getchannel('A')
for i,f in enumerate(frames(bolt)):
 spark=Image.new('RGBA',(64,64)); crack=f.crop((14,5,50,60)).rotate(90,expand=True).resize((48,13),Image.Resampling.NEAREST)
 crack=remap(crack,violet)
 spark.alpha_composite(crack,(8,26))
 # Confine the branching electricity to the cloud interior, with quiet frames between pulses.
 for y in range(64):
  for x in range(64):
   if not(20<=y<44) or mask.getpixel((x,y-20))==0 or i in (0,4):spark.putpixel((x,y),(0,0,0,0))
 sparks.append(spark)
record(sheet(sparks),'CloudSparksViolet',[bolt,cloud_src],'Original bolt branches rotated horizontally and clipped to cloud silhouette; 2 quiet frames; no electric shield rings.')
void_src=source/'Void/VoidBlackHole.png'
portal=[]
for f in frames(void_src):
 f=remap(f,lambda c:(204,176,225,255) if c[0]>180 else (125,98,159,255) if c[0]>70 else (27,26,40,255))
 portal.append(f.crop((13,17,51,53)).resize((64,64),Image.Resampling.NEAREST))
record(sheet(portal),'VoidRiftMuted',[void_src],'Six original circular rupture frames, trim common padding, square silhouette; opaque dark core and two violet rim values.')
meteor_src=root/'Assets/_Project/Art/Icons/MageTower/Meteor.png';meteor=Image.open(meteor_src).convert('RGBA')
rock_src=source/'Earth/EarthRock.png'
rock=frames(rock_src)[2].crop((14,5,49,54)).resize((23,23),Image.Resampling.NEAREST)
rock=remap(rock,lambda c:(150,84,44,255) if c[0]>170 else (95,63,44,255) if c[0]>135 else (53,43,39,255))
disk=Image.new('L',rock.size);ImageDraw.Draw(disk).ellipse((1,1,21,22),fill=255)
for y in range(23):
 for x in range(23):
  c=rock.getpixel((x,y));rock.putpixel((x,y),(53,43,39,255) if disk.getpixel((x,y)) and c[3]==0 else (*c[:3],255) if disk.getpixel((x,y)) else (0,0,0,0))
fire_src=source/'Fire/FireFlamme.png';meteors=[]
for i,flame in enumerate(frames(fire_src)):
 canvas=Image.new('RGBA',(64,64));flame=flame.crop(flame.getbbox()).resize((18,31),Image.Resampling.NEAREST).rotate(-35,resample=Image.Resampling.NEAREST,expand=True)
 canvas.alpha_composite(flame,(24,4));canvas.alpha_composite(rock.rotate(i*15,resample=Image.Resampling.NEAREST),(14,30));meteors.append(canvas)
record(sheet(meteors),'MeteorProjectile',[rock_src,fire_src],'Owned EarthRock fractured surface remapped to three molten stone values; opaque circular mask; rock rotates 15 degrees/frame; six owned flame frames form diagonal tail. 64px nearest composite, no diffusion frames.')
star_src=here/'Starfall-v1/icon48.png';icon=Image.open(star_src).convert('RGBA')
points={(x,y) for y in range(48) for x in range(48) if max(icon.getpixel((x,y))[:3])>75};groups=[]
while points:
 todo=[points.pop()];group=[]
 while todo:
  x,y=todo.pop();group.append((x,y))
  for dx,dy in ((-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,1),(-1,1),(1,-1)):
   p=(x+dx,y+dy)
   if p in points:points.remove(p);todo.append(p)
 groups.append(group)
star=max(groups,key=len);starframes=[]
for i in range(6):
 f=Image.new('RGBA',(64,64))
 for x,y in star:
  light=max(icon.getpixel((x,y))[:3]);color=(238,239,219,255) if light>220 else (174,208,224,255) if light>165 else (104,147,181,255) if light>115 else (66,92,129,255)
  f.putpixel((x+8,y+8),color)
 starframes.append(f)
record(sheet(starframes),'ArcaneProjectile',[star_src],'Largest connected falling star from approved icon; four-color exact pixel remap. Travel comes from runtime, no character muzzle.')
inputs=[root/'Assets/_Project/Art/Sprites/Goblins/GoblinBomber/Goblin Bomber Sprite Sheet.png']
bomb=Image.open(inputs[0]).convert('RGBA').crop((9,102,20,113));dest=root/'Assets/_Project/Art/VFX/Projectiles/GoblinBomb.png';dest.parent.mkdir(parents=True,exist_ok=True);bomb.save(dest)
records.append(dict(output=str(dest.relative_to(root)),sources=[str(p.relative_to(root)) for p in inputs],process='Crop airborne bomb and fuse from Attack2_000, pixels (9,102)-(20,113), unchanged source pixels.'))
(here/'derivatives-v1.json').write_text(json.dumps(dict(pillow=Image.__version__,assets=records),ensure_ascii=False,indent=2),encoding='utf8')
preview=Image.new('RGBA',(384,320),(33,39,38,255))
for row,fs in enumerate([meteors,strike,sparks,portal,starframes]):
 for i,f in enumerate(fs):preview.alpha_composite(f,(i*64,row*64))
preview.resize((1152,960),Image.Resampling.NEAREST).save(here/'derivatives-preview.png')
