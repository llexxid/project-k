"""Current schema checked before execution. Local API/UI graphs include every applied input."""
import json,re,sys,urllib.request
from pathlib import Path
sys.dont_write_bytecode=True
sys.path[:0]=['AI/comfyui/lobby','AI/comfyui/_shared']
from comfy_client import Comfy
import workflow_json
root=Path(__file__).parent
bloom='--bloom' in sys.argv
folder=root/('StarfallBloom-v1' if bloom else 'Starfall-v1');folder.mkdir(exist_ok=True)
def save(name,value): (folder/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
source=root/'Starfall-v1/icon48.png' if bloom else Path('Assets/_Project/Art/Icons/MageTower/ArcaneVolley.png')
c=Comfy()
request={'file_path':str(source.resolve()),'client_os':'windows'}
save('upload-request.json',request);result=c.call('upload_file',request);save('upload-response.json',result)
text='\n'.join(b.get('text','') for b in result.get('content',[]))
url=re.search(r'https://cloud\.comfy\.org/api/uploads/[A-Za-z0-9_-]+',text).group()
with urllib.request.urlopen(urllib.request.Request(url,data=source.read_bytes(),headers={'Content-Type':'image/png'},method='PUT'),timeout=60) as r: uploaded=json.load(r)
save('upload-complete.json',uploaded)
prompt='''Create one readable 48x48 logical pixel-art spell icon for a medieval idle RPG: a shower of falling starlight. The provided existing spell icon is a STYLE AND PALETTE REFERENCE, not the required arrow subject. Replace the arrows with THREE falling stars distributed across the square. Each has a compact ivory diamond head at its LOWER LEFT end and a short blue-silver tapered tail extending toward the UPPER RIGHT. The main middle star is slightly larger, with two smaller falling stars left and right. Convey rain from the sky, never arrows fired upward. At 48px each star must read instantly with clear negative space. No meteor rocks, clouds, moon, landscape, border, text, faces, weapons or UI frame. Flat dark blue-charcoal background #171c24, desaturated steel blue, muted periwinkle, pale silver and tiny ivory highlights; maximum 12 colors. Crisp chunky pixel clusters based on a 48px grid, no single-pixel noise, no smooth gradients, no bloom fog. Match the reference's restrained luminosity but make the star heads clearly visible. Square canvas, generous 5px margin, image enlarged nearest neighbor.'''
if bloom:
 prompt='''Edit the reference pixel-art falling-star spell icon into its subtly awakened variant. Preserve the same THREE stars in their exact positions and diagonal direction: bright diamond heads at lower left, tails extending upper right. Preserve at least 85 percent of the composition. Add only a slim muted periwinkle highlight on each tail and two tiny silver glints in the negative space. The icon must remain instantly recognizable as the same spell at 48x48. Keep dark charcoal-blue background, steel blue and ivory heads, maximum 12 colors, crisp chunky 48px logical grid, no smooth gradients, no bloom fog, no borders, text, faces, extra stars or purple wash. Overall brightness only slightly above the reference. Square canvas, nearest-neighbor pixel finish.'''
(folder/'prompt.txt').write_text(prompt,encoding='utf8')
api={
 '1':{'class_type':'LoadImage','inputs':{'image':uploaded['name']}},
 '2':{'class_type':'ImageScale','inputs':{'image':['1',0],'upscale_method':'nearest-exact','width':768,'height':768,'crop':'disabled'}},
 '10':{'class_type':'GeminiNanoBanana2V2','inputs':{'prompt':prompt,'model':'Nano Banana 2 (Gemini 3.1 Flash Image)','model.aspect_ratio':'1:1','model.resolution':'1K','model.thinking_level':'MINIMAL','model.images.image_1':['2',0],'seed':91631,'response_modalities':'IMAGE'}},
 '15':{'class_type':'SaveImage','inputs':{'images':['10',0],'filename_prefix':'KingdomIdle/CombatRevision/Starfall_Master'}},
 '20':{'class_type':'ImageScale','inputs':{'image':['10',0],'upscale_method':'nearest-exact','width':48,'height':48,'crop':'disabled'}},
 '30':{'class_type':'ImageQuantize','inputs':{'image':['20',0],'colors':12,'dither':'none'}},
 '40':{'class_type':'SaveImage','inputs':{'images':['30',0],'filename_prefix':'KingdomIdle/CombatRevision/Starfall_48px'}}}
if bloom:
 api['10']['inputs']['seed']=91632
 api['15']['inputs']['filename_prefix']='KingdomIdle/CombatRevision/StarfallBloom_Master'
 api['40']['inputs']['filename_prefix']='KingdomIdle/CombatRevision/StarfallBloom_48px'
workflow_json.SCHEMA['GeminiNanoBanana2V2']=(['prompt','model','model.aspect_ratio','model.resolution','model.thinking_level','seed','response_modalities'],['model.images.image_1'],['IMAGE','STRING','IMAGE'])
save('workflow.api.json',api);save('workflow.ui.json',workflow_json.convert(api))
validation=c.call('submit_workflow',{'workflow':api,'dry_run':True});save('validation.json',validation)
if validation.get('isError'):raise RuntimeError('Preflight failed; inspect validation.json')
request={'workflow':api,'confirm':True};save('request.json',request)
result=c.call('submit_workflow',request);save('submission.json',result)
print(json.dumps(result,ensure_ascii=False))
save('cloud-save.json',c.call('save_workflow',{'workflow_json':api,'name':'KingdomIdle '+folder.name+' 20260916','overwrite':False}))
