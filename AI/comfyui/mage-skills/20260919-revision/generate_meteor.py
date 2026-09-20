"""One static meteor body; existing native VFX supply its animated trail."""
import json,sys,re,urllib.request
from pathlib import Path
sys.dont_write_bytecode=True
sys.stdout.reconfigure(encoding='utf8')
sys.path[:0]=['AI/comfyui/lobby','AI/comfyui/_shared']
from comfy_client import Comfy
import workflow_json
root=Path(__file__).parent
folder=root/'meteor-v1';folder.mkdir(exist_ok=False)
c=Comfy()
def save(name,obj): (folder/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
source=Path('Assets/_Project/Art/Icons/MageTower/Meteor.png')
(folder/'reference-icon48.png').write_bytes(source.read_bytes())
request={'file_path':str(source.resolve()),'client_os':'windows'}
save('upload-request.json',request);r=c.call('upload_file',request);save('upload-response.json',r)
url=re.search(r'https://cloud\.comfy\.org/api/uploads/[A-Za-z0-9_-]+','\n'.join(x.get('text','') for x in r['content'])).group()
with urllib.request.urlopen(urllib.request.Request(url,data=source.read_bytes(),headers={'Content-Type':'image/png'},method='PUT'),timeout=60) as response:uploaded=json.load(response)
save('upload-complete.json',uploaded)
prompt='''A single pixel-art airborne meteor BOULDER BODY for a medieval quarter-view 2D RPG, one static sprite only. The reference icon is a PALETTE and rounded silhouette reference only. Produce ONLY the roughly spherical irregular charcoal rock, without the flame tail from the reference. An asymmetrical rounded angular silhouette, large connected facets of dark warm slate, 3 readable fractured planes and a few restrained molten copper-orange fissures, a small warm ochre highlight along the upper-right rim. This is a flying round heavy stone, NOT a pillar, slab, cliff, obelisk, crystal, ground mound, crater or flat disk. Visible rock width and height nearly equal. High three-quarter view of the free-floating stone, no floor, no cast shadow. Strict 64x64 logical pixel canvas enlarged nearest-neighbour. Rock occupies central 44x42 pixels, empty margin all around. SIX flat colors total excluding background: charcoal brown, dark slate, warm medium grey, dull copper, orange, pale ochre. Contiguous readable pixel clusters, cracks 1-2 logical pixels wide, no noise, no dithering, no smooth gradients, no black outline, no bloom, no lens flare, no fire, no sparks, no text, no UI, no frame, no multiple options. Completely uniform vivid chroma green #00FF00 background, no green anywhere in the boulder.'''
api={'1':{'class_type':'LoadImage','inputs':{'image':uploaded['name']}},'2':{'class_type':'ImageScale','inputs':{'image':['1',0],'upscale_method':'nearest-exact','width':768,'height':768,'crop':'disabled'}},'10':{'class_type':'GeminiNanoBanana2V2','inputs':{'prompt':prompt,'model':'Nano Banana 2 (Gemini 3.1 Flash Image)','model.aspect_ratio':'1:1','model.resolution':'1K','model.thinking_level':'MINIMAL','model.images.image_1':['2',0],'seed':91951,'response_modalities':'IMAGE'}},'15':{'class_type':'SaveImage','inputs':{'images':['10',0],'filename_prefix':'KingdomIdle/MageRevision/MeteorBody-v1'}}}
save('workflow.api.json',api)
workflow_json.SCHEMA['GeminiNanoBanana2V2']=(['prompt','model','model.aspect_ratio','model.resolution','model.thinking_level','seed','response_modalities'],['model.images.image_1'],['IMAGE','STRING','IMAGE'])
save('workflow.ui.json',workflow_json.convert(api))
check=c.call('submit_workflow',{'workflow':api,'dry_run':True});save('validation.json',check)
if check.get('isError'):raise RuntimeError('Preflight failed')
estimate=c.call('estimate_credits',{'workflow':api});save('estimate.json',estimate)
print('Estimate:',json.dumps(estimate,ensure_ascii=False),flush=True)
request={'workflow':api,'confirm':True};save('request.json',request)
r=c.call('submit_workflow',request);save('submission.json',r);print('Submission:',json.dumps(r,ensure_ascii=False),flush=True)
save('cloud-save.json',c.call('save_workflow',{'workflow_json':api,'name':'KingdomIdle Meteor Body 20260919 v1','overwrite':False}))
