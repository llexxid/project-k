"""Record adopted source hashes, pixel budgets and actual Comfy billing evidence."""
import hashlib, json
from pathlib import Path
from PIL import Image
from finish_cloud import unpack
HERE=Path(__file__).parent
def read(name):return json.loads((HERE/(name+'.json')).read_text(encoding='utf8'))
def write(name,value):(HERE/(name+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
job=unpack(read('icons-submission'))['result']['prompt_id']
billing=unpack(read('billing-after'))
event=next(e for e in billing['events'] if e.get('params',{}).get('job_id')==job)
# Preserve only this job's public usage evidence; account identifiers and unrelated
# workspace events are unnecessary for the asset provenance.
event['params'].pop('user_id',None)
write('billing-job',event)
raw=HERE/'billing-after.json'
if raw.exists():
    private=Path('Recordings/BloomRevision/comfy-billing-response.json')
    private.write_bytes(raw.read_bytes());raw.unlink()
usage=unpack(read('usage-after'))
records=[]
for file in sorted((HERE/'icons').glob('*.png')):
    im=Image.open(file).convert('RGBA');colors={p[:3] for p in im.getdata() if p[3]}
    records.append(dict(file=str(file).replace('\\','/'),size=list(im.size),opaqueColors=len(colors),
        binaryAlpha=set(im.getchannel('A').getdata())<={0,255},sha256=hashlib.sha256(file.read_bytes()).hexdigest()))
write('manifest',dict(version=6,sourceCommit='c2a6a4b0d',pipeline=['prepare.py','icons-submitted-request.json','icons-ui.json','finish_cloud.py alpha','MageSkillAssetPreparation.Build'],
    model=None,prompt=None,seed=None,reason='Existing pixel art is recomposed deterministically. Comfy color matrix and 16-color quantization preserve its silhouette and logical pixel grid without diffusion drift.',
    nodes=['LoadImage','ImageCrop','RadianceGPUColorMatrix','ImageQuantize','SaveImage'],nodeVersions='Cloud node schema captured in nodes.json and related catalogs; service did not expose pinned package versions.',
    job=job,savedWorkflow=unpack(read('icons-cloud-save')),inputs='references/ and *-draft.png',mask='Each *-draft.png alpha channel',outputs=records,
    billing=dict(gpuSeconds=event['params']['gpu_seconds'],gpuType=event['params']['gpu_type'],actualJobDollars=None,reason='Invoice report ending 2026-09-20T12:00Z precedes this job at 12:01Z. No dollar amount or credits were returned for this job; no estimate is reported as actual spend.'),
    decisions=[dict(change='Default lightning',before='Cloud and one bolt',after='Three existing bolts',reason='User requested three visible bolts'),
        dict(change='Thunderbloom',before='Separate older bloom icon',after='Original cloud/bolt tinted purple by Comfy color matrix',reason='Retain the requested composition'),
        dict(change='Comfy alpha',before='RGB quantizer output black rectangle',after='Original authored binary mask restored',reason='Reject opaque background without redrawing the art'),
        dict(change='Starfall',before='Cool homing projectile',after='Same six frames, three warm colors, random landing and small eight-frame ground pulse',reason='Preserve existing effect while making local AOE readable'),
        dict(change='Meteor crater',before='Static brown 52x28 occupied pixels',after='108x68 nearest source on 112x76 canvas with 16-frame red crack loop',reason='About 17% wider in world space, with thin glowing cracks and no flame sprites')]))
print('Recorded',len(records),'icons; actual GPU seconds',event['params']['gpu_seconds'])
