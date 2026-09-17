import json,sys,urllib.request
from pathlib import Path
from PIL import Image,ImageDraw
sys.dont_write_bytecode=True
sys.path.insert(0,'AI/comfyui/lobby')
from comfy_client import Comfy
root=Path(__file__).resolve().parent
keys=['Lightning','IceSpike','FireTornado','ArcaneVolley','VenomMist','StoneSeal','GaleBlades','Sanctuary','Meteor','VoidRift']
r=json.loads((root/'remaining-submission.json').read_text('utf8'))
d=r.get('structuredContent') or json.loads(r['content'][0]['text'])
r=Comfy().call('get_batch_output',{'batch_id':d['batch_id'],'client_os':'windows'})
(root/'remaining-output.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf8')
outputs=r.get('structuredContent',{}).get('outputs',[])
jobs=dict(zip(d['job_ids'],keys[1:]))
for output in outputs:
 key=jobs[output['job_id']]; name='master.png' if str(output['source_node_id'])=='15' else 'icon48.png'
 (root/key/name).write_bytes(urllib.request.urlopen(output['url'],timeout=90).read())
 print(key,name,flush=True)
sheet=Image.new('RGB',(1200,650),'#171c24');draw=ImageDraw.Draw(sheet)
for i,key in enumerate(keys):
 x=(i%5)*240;y=(i//5)*325
 for j,path in enumerate([Path('Assets/_Project/Art/Icons/MageTower')/(key+'.png'),root/key/'icon48.png']):
  im=Image.open(path);assert im.size==(48,48)
  sheet.paste(im.resize((96,96),Image.Resampling.NEAREST),(x+j*120,y+30))
  sheet.paste(im,(x+j*120+24,y+154))
 draw.text((x+4,y+6),key,fill='white');draw.text((x+4,y+219),'Base          Bloom',fill='white')
sheet.save(root/'bloom-comparison.png')
