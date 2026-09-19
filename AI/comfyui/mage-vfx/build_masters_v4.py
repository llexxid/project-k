"""Alternative process after reference edit reproduced coarse pixels. No frame diffusion."""
import copy, json, sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'revision3'))
from build import Comfy, unpack
OUT=ROOT/'revision4'; OUT.mkdir(exist_ok=True)
def save(name,obj): (OUT/(name+'.json')).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
base=json.loads((ROOT/'revision1/meteor-zimage-api.json').read_text(encoding='utf8'))
graph={k:copy.deepcopy(v) for k,v in base.items() if int(k)<=5}
shared=' Single isolated complete fantasy RPG spell sprite. Elevated three-quarter view, deliberate hard-edge pixel clusters at 128 by 128 logical pixels with fine one-pixel highlights and intricate internal facets, NOT large blocky 32-pixel art. Limited six-color palette, no black outlines, no text, no symbols resembling letters, no gradient haze, no environment. Solid pure black background, generous empty margins, complete object fully visible. '
prompts={
 'Glacier':'A tall luminous icy crystal formation with a large tapered center shard and two shorter distinct side shards. Sharp irregular ice blades, transparent-looking internal fracture planes rendered as opaque geometric shapes, layered pale cyan facets and white edge highlights, rich deep blue shadow planes. Base rests on a small compact cluster of ice splinters, not a broad floor. Cyan, turquoise, light blue and ivory only.'+shared,
 'Sanctuary':'An elegant sacred healing emblem, a luminous pale mint-green and ivory cross suspended above a slim horizontal gold-green diamond halo. The cross has beveled symmetrical arms, a tall slender shaft and a radiant star at its center. Small distinct leaf-like luminous fragments beside it, clean strong silhouette, refined angular facets. Entire crest compact with open space around it; no large floor ring, no pedestal, no walls, no building. Restrained emerald, mint, pale yellow and ivory.'+shared,
 'StormCloud':'A low wide dramatic thundercloud made from three or four overlapping rolling storm masses, dark desaturated plum and slate shadow volumes, soft-edged shapes represented with crisp small pixel clusters, a faint lavender highlight along the lower rim, two small bright violet electric fissures inside the cloud. Cloud silhouette horizontal twice as wide as tall. No falling lightning bolt, no ground, no face, no moon.'+shared,
}
for index,(key,prompt) in enumerate(prompts.items()):
 start=6+index*6
 for old in range(6,12):
  n=copy.deepcopy(base[str(old)])
  for field,value in n['inputs'].items():
   if isinstance(value,list) and value and isinstance(value[0],str) and value[0].isdigit() and int(value[0])>=6:
    value[0]=str(int(value[0])+start-6)
  if old==6:n['inputs']['text']=prompt
  if old==9:n['inputs']['seed']=2026092004+index
  if old==11:n['inputs']['filename_prefix']='ProjectK_v4_'+key
  graph[str(old+start-6)]=n
save('prompts',prompts);save('masters-api',graph)
c=Comfy();r=c.call('submit_workflow',{'workflow':graph,'dry_run':True});save('preflight',r)
if r.get('isError'):raise RuntimeError('Preflight rejected')
r=c.call('save_workflow',{'workflow_json':graph,'name':'ProjectK spell masters v4','description':'Z-Image pixel LoRA changes process after Qwen reference edit copied coarse pixels. Three single masters; motion derived from masks and existing animation. No frame-by-frame diffusion.'});save('cloud-save',r);wid=unpack(r)['workflow_id']
r=c.call('get_saved_workflow',{'workflow_id':wid});save('cloud-workflow-response',r);save('masters-ui',unpack(r)['workflow_json'])
args={'workflow':graph,'confirm':True};save('submitted-request',args);r=c.call('submit_workflow',args);save('submission',r);pid=unpack(r)['prompt_id']
save('manifest',dict(status='submitted',prompt_id=pid,workflow_id=wid,workflow_url='https://cloud.comfy.org/#'+wid,api='masters-api.json',ui='masters-ui.json',prompts='prompts.json',references='No image conditioning. Art direction from inspected purchased ice/heal/cloud silhouettes; original assets remain unchanged.',models={k:v['inputs'] for k,v in graph.items() if int(k)<=5},seed=[2026092004,2026092005,2026092006],node_versions='Same Cloud schemas validated for revision1; package versions unavailable',settings='8 steps, cfg 1, euler/simple, LoRA .85, 1024x1024',cost='Pending usage report',rationale='Reference edit v2 enlarged coarse blocks. Change to higher-detail independent master generation; retain palette and derive motion deterministically. Reject candidates that fail in-game comparison.'))
print(json.dumps(dict(prompt_id=pid,workflow_id=wid)),flush=True)
