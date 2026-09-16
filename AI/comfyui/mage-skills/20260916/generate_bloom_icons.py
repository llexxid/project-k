"""Reference-locked Comfy edit workflow; requests, graphs and responses are retained."""
import json, re, sys, urllib.request
from pathlib import Path
sys.dont_write_bytecode = True
sys.path[:0] = ['AI/comfyui/lobby', 'AI/comfyui/_shared']
from comfy_client import Comfy
import workflow_json

ROOT = Path(__file__).resolve().parent
KEYS = ['Lightning','IceSpike','FireTornado','ArcaneVolley','VenomMist','StoneSeal','GaleBlades','Sanctuary','Meteor','VoidRift']
DETAILS = [
 'Shift only the existing bolt to pale lavender with an ivory core. Add two short violet electrical cracks inside the SAME cloud. Keep the exact cloud and bolt outline.',
 'Keep the exact three ice crystals. Give the center facet an ivory-cyan highlight and add two tiny diamond frost glints beside the base.',
 'Keep the exact flame spiral. Give its inner edge a restrained pale-gold accent and add two small ember diamonds.',
 'Keep the same three bolts in the same positions. Add narrow ivory cores and two short lavender trailing sparks.',
 'Keep the same cloud, bubbles and pool. Add pale sage highlights on the top edges and two small dull-gold mote pixels.',
 'Keep the exact three stone spires. Light only the existing cracks with subdued pale amber and add two small amber fragments.',
 'Keep the exact curved blades. Add a few ivory edge pixels and two small pale teal trailing glints.',
 'Keep the exact healing pillar and pool. Add two small warm-gold star glints and a pale ivory center highlight.',
 'Keep the exact meteor, tail and fragments. Give the rock a few narrow dull-amber fissures and add two restrained gold sparks.',
 'Keep the exact dark oval and two crescent rims. Brighten only their inner edge to muted lavender and add two small pale violet glints.'
]
def save(path, value): path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
def data(result):
 if result.get('structuredContent'): return result['structuredContent']
 for b in result.get('content',[]):
  if b.get('type')=='text':
   try: return json.loads(b['text'])
   except ValueError: pass
 return result

workflow_json.SCHEMA['GeminiNanoBanana2V2']=(['prompt','model','model.aspect_ratio','model.resolution','model.thinking_level','seed','response_modalities'],['model.images.image_1'],['IMAGE','STRING','IMAGE'])
c=Comfy()
ids=[0] if '--pilot' in sys.argv else list(range(1,10))
items=[]
for i in ids:
 key=KEYS[i]; folder=ROOT/key; folder.mkdir(exist_ok=True)
 source=Path('Assets/_Project/Art/Icons/MageTower')/(key+'.png')
 request={'file_path':str(source.resolve()),'client_os':'windows'}
 save(folder/'upload-request.json',request)
 result=c.call('upload_file',request);save(folder/'upload-response.json',result)
 text='\n'.join(b.get('text','') for b in result.get('content',[]))
 url=re.search(r'https://cloud\.comfy\.org/api/uploads/[A-Za-z0-9_-]+',text).group()
 req=urllib.request.Request(url,data=source.read_bytes(),headers={'Content-Type':'image/png'},method='PUT')
 with urllib.request.urlopen(req,timeout=60) as r: uploaded=json.load(r)
 save(folder/'upload-complete.json',uploaded)
 prompt='''Edit the provided existing 48x48 pixel-art spell icon into its subtly awakened variant. This image is the EDIT TARGET, not general inspiration. Preserve its exact silhouette, object count, placement, angle, scale, pixel cluster density, shadows, and background #171c24. Do not redesign it. The player must instantly recognize the same base skill. Keep at least 85% of the icon visually unchanged. Crisp square 48px logical pixel art enlarged nearest-neighbor, at most 16 colors, restrained highlights, no bloom haze or smoothing. No text, frame, logo, characters, decoration border, new props or neon. Change only these small effect accents: '''+DETAILS[i]
 api={
 '1':{'class_type':'LoadImage','inputs':{'image':uploaded['name']}},
 '2':{'class_type':'ImageScale','inputs':{'image':['1',0],'upscale_method':'nearest-exact','width':768,'height':768,'crop':'disabled'}},
 '10':{'class_type':'GeminiNanoBanana2V2','inputs':{'prompt':prompt,'model':'Nano Banana 2 (Gemini 3.1 Flash Image)','model.aspect_ratio':'1:1','model.resolution':'1K','model.thinking_level':'MINIMAL','model.images.image_1':['2',0],'seed':91600+i,'response_modalities':'IMAGE'}},
 '15':{'class_type':'SaveImage','inputs':{'images':['10',0],'filename_prefix':'KingdomIdle/MageBloom/'+key+'_Master'}},
 '20':{'class_type':'ImageScale','inputs':{'image':['10',0],'upscale_method':'nearest-exact','width':48,'height':48,'crop':'disabled'}},
 '30':{'class_type':'ImageQuantize','inputs':{'image':['20',0],'colors':16,'dither':'none'}},
 '40':{'class_type':'SaveImage','inputs':{'images':['30',0],'filename_prefix':'KingdomIdle/MageBloom/'+key+'_48px'}}}
 save(folder/'workflow.api.json',api);save(folder/'workflow.ui.json',workflow_json.convert(api))
 (folder/'prompt.txt').write_text(prompt,encoding='utf8')
 items.append({'tool':'submit_workflow','workflow':api,'description':key+' subtle bloom variant'})
request={'workflow':items[0]['workflow'],'confirm':True} if '--pilot' in sys.argv else {'items':items,'client_os':'windows','confirm':True}
tag='pilot' if '--pilot' in sys.argv else 'remaining'
save(ROOT/(tag+'-request.json'),request)
result=c.call('submit_workflow' if '--pilot' in sys.argv else 'submit_batch',request)
save(ROOT/(tag+'-submission.json'),result);print(json.dumps(data(result),ensure_ascii=False))
