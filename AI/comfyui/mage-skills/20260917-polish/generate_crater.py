"""Two bounded material studies; schemas/requests/UI graphs retained, no diffusion frames."""
import json,sys,re,urllib.request,hashlib
from pathlib import Path
sys.dont_write_bytecode=True
sys.path[:0]=['AI/comfyui/lobby','AI/comfyui/_shared']
from comfy_client import Comfy
import workflow_json
root=Path(__file__).parent
c=Comfy()
source=Path('Assets/_Project/Art/Icons/MageTower/Meteor.png')
(root/'reference-meteor.png').write_bytes(source.read_bytes())
def save(folder,name,obj): (folder/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
request={'file_path':str(source.resolve()),'client_os':'windows'}
save(root,'upload-request.json',request);r=c.call('upload_file',request);save(root,'upload-response.json',r)
url=re.search(r'https://cloud\.comfy\.org/api/uploads/[A-Za-z0-9_-]+','\n'.join(x.get('text','') for x in r['content'])).group()
with urllib.request.urlopen(urllib.request.Request(url,data=source.read_bytes(),headers={'Content-Type':'image/png'},method='PUT'),timeout=60) as response:uploaded=json.load(response)
save(root,'upload-complete.json',uploaded)
prompt='''One pixel-art GAMEPLAY GROUND DECAL of a freshly struck meteor crater, for a medieval 2D idle RPG. The reference is MATERIAL AND PALETTE ONLY, not composition. A shallow charred impact depression seen from a high overhead three-quarter RPG camera, a wide flattened oval footprint, width to height 1.6:1, centred in a square canvas. Broken dark warm-brown stone plates, a few branching molten copper-orange cracks, only four small pale amber hotspots. Outer rim low and jagged, sparse scattering of 6 chunky stone fragments immediately around the rim. No standing rock, no airborne meteor, no flames, no smoke, no sky, no characters, no UI, no border, no text, no light rays. Interior mostly dark, clearly subdued enough that characters will stand visibly in front. Purpose is a 2 second ember afterglow, not a giant lava lake. Strict 64x64 logical pixel grid enlarged nearest-neighbour, maximum SIX flat material colours, chunky connected pixel clusters, no dithering, no smooth shading, no blur, no bloom, no black outline. Crater occupies about 50x32 logical pixels at the exact centre, generous empty margin. Background absolutely uniform bright chroma green #00FF00, no green in the crater itself. ONE SINGLE FRAME, ONE DECAL ONLY, no spritesheet or alternatives.'''
workflow_json.SCHEMA['GeminiNanoBanana2V2']=(['prompt','model','model.aspect_ratio','model.resolution','model.thinking_level','seed','response_modalities'],['model.images.image_1'],['IMAGE','STRING','IMAGE'])
for variant in ['nano-v1','gpt-v1']:
    folder=root/variant;folder.mkdir(exist_ok=False)
    if variant.startswith('nano'):
        node={'class_type':'GeminiNanoBanana2V2','inputs':{'prompt':prompt,'model':'Nano Banana 2 (Gemini 3.1 Flash Image)','model.aspect_ratio':'1:1','model.resolution':'1K','model.thinking_level':'MINIMAL','model.images.image_1':['2',0],'seed':91751,'response_modalities':'IMAGE'}}
    else:
        node={'class_type':'OpenAIGPTImageNodeV2','inputs':{'prompt':prompt,'model':'gpt-image-2','model.size':'1024x1024','model.custom_width':1024,'model.custom_height':1024,'model.background':'opaque','model.quality':'medium','model.images.image_1':['2',0],'n':1,'seed':0}}
    api={'1':{'class_type':'LoadImage','inputs':{'image':uploaded['name']}},'2':{'class_type':'ImageScale','inputs':{'image':['1',0],'upscale_method':'nearest-exact','width':768,'height':768,'crop':'disabled'}},'10':node,'15':{'class_type':'SaveImage','inputs':{'images':['10',0],'filename_prefix':'KingdomIdle/CombatPolish/Crater-'+variant}}}
    save(folder,'workflow.api.json',api)
    graph=workflow_json.convert(api);graph['groups']=[]
    for i,n in enumerate(graph['nodes']):n['pos']=[i*460,80];n['size']=[420,460] if n['id']==10 else [300,130]
    save(folder,'workflow.ui.json',graph)
    check=c.call('submit_workflow',{'workflow':api,'dry_run':True});save(folder,'validation.json',check)
    if check.get('isError'):raise RuntimeError('Preflight failed')
    estimate=c.call('estimate_credits',{'workflow':api});save(folder,'estimate.json',estimate)
    req={'workflow':api,'confirm':True};save(folder,'request.json',req)
    result=c.call('submit_workflow',req);save(folder,'submission.json',result)
    if result.get('isError'):raise RuntimeError(str(result))
    print(variant,json.dumps(result,ensure_ascii=False),flush=True)
    save(folder,'cloud-save.json',c.call('save_workflow',{'workflow_json':api,'name':'KingdomIdle Crater '+variant+' 20260917','overwrite':False}))
