"""Boost owned venom field contrast without modifying any ExternalAssets originals."""
from pathlib import Path
from PIL import Image
import hashlib,json
root=Path.cwd();here=Path(__file__).parent;records=[]
for n,palette in [(1,[(43,67,40),(77,103,53),(130,157,72),(178,198,107)]),(2,[(83,110,58),(128,155,76),(177,194,114),(204,216,151)])]:
 p=root/f'Assets/_Project/Art/VFX/PoisonEffect/Animation/Sprites/Poison_Effect_05-{n}.png';ref=here/'references'/f'Poison05-{n}.before.png'
 if not ref.exists():ref.write_bytes(p.read_bytes())
 im=Image.open(ref).convert('RGBA');colors=sorted(set(c for c in im.getdata() if c[3]),key=lambda c:.2126*c[0]+.7152*c[1]+.0722*c[2]);mapping={c:(*palette[min(3,i*4//len(colors))],255) for i,c in enumerate(colors)}
 out=im.copy();out.putdata([mapping[c] if c[3] else c for c in im.getdata()]);out.save(p)
 records.append(dict(source=str(ref.relative_to(root)),output=str(p.relative_to(root)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),palette=palette,size=im.size,reason='Preserve source animation/silhouette; stronger muted sage contrast against grass; remove residual alpha within filled pool. Renderer opacity separately .96 base / .88 gas.'))
(here/'venom-finish-v1.json').write_text(json.dumps(records,indent=2),encoding='utf8')
