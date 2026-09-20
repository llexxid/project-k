"""Actual Comfy Cloud color/palette finish. Run submit, then collect after completion."""
import json, sys, urllib.request, hashlib, re
from PIL import Image
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parents[2]/'lobby'))
from comfy_client import Comfy
HERE=Path(__file__).parent

def save(name,value): (HERE/(name+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def unpack(r):
    if r.get('isError'): raise RuntimeError(str(r))
    if r.get('structuredContent'): return r['structuredContent']
    for b in r.get('content',[]):
        if b.get('type')=='text':
            try: return json.loads(b['text'])
            except json.JSONDecodeError: pass
    return r

def submit():
    c=Comfy();keys=json.loads((HERE/'icon-layout.json').read_text(encoding='utf8'))
    uploaded=json.loads((HERE/'upload-response.json').read_text(encoding='utf-8-sig'))
    source='/'.join(v for v in [uploaded.get('subfolder'),uploaded['name']] if v)
    graph={'1':dict(class_type='LoadImage',inputs=dict(image=source))};outputs={}
    for i,key in enumerate(keys):
        base=10+i*10
        graph[str(base)]=dict(class_type='ImageCrop',inputs=dict(image=['1',0],width=48,height=48,x=i*48,y=0))
        image=[str(base),0]
        if key=='Lightning_Bloom':
            graph[str(base+1)]=dict(class_type='RadianceGPUColorMatrix',inputs=dict(image=image,preset='Custom',matrix_type='RGB (3x3)',r_vector='0.85, 0.0, 0.20',g_vector='0.0, 0.78, 0.0',b_vector='0.0, 0.0, 1.05',clamp_output=True))
            image=[str(base+1),0]
        graph[str(base+2)]=dict(class_type='ImageQuantize',inputs=dict(image=image,colors=16,dither='none'))
        graph[str(base+3)]=dict(class_type='SaveImage',inputs=dict(images=[str(base+2),0],filename_prefix='projectk-v6-'+key))
        outputs[str(base+3)]=key
    save('icons-api',graph);save('output-node-map',outputs)
    request=dict(workflow=graph)
    preflight=c.call('submit_workflow',dict(request,dry_run=True));save('icons-preflight',preflight)
    if preflight.get('isError'): raise RuntimeError(str(preflight))
    saved=c.call('save_workflow',dict(workflow_json=graph,name='ProjectK Mage bloom icon finish v6'));save('icons-cloud-save',saved)
    workflow=c.call('get_saved_workflow',dict(workflow_id=unpack(saved)['workflow_id']));save('icons-cloud-workflow',workflow)
    data=unpack(workflow)
    save('icons-ui',data['workflow_json'])
    save('icons-submitted-request',request)
    submitted=c.call('submit_workflow',request);save('icons-submission',submitted)
    print(json.dumps(unpack(submitted),ensure_ascii=False))

def collect():
    c=Comfy();submission=json.loads((HERE/'icons-submission.json').read_text(encoding='utf8'))
    job=unpack(submission)['result']['prompt_id']
    status=c.call('get_job_status',dict(prompt_id=job));save('icons-status',status)
    if unpack(status)['result']['job_status']!='completed': raise RuntimeError('Job not completed')
    output=c.call('get_output',dict(prompt_id=job,client_os='windows'))
    mapping=json.loads((HERE/'output-node-map.json').read_text(encoding='utf8'))
    (HERE/'icons').mkdir(exist_ok=True);manifest=[]
    for block in output.get('content',[]):
        text=block.get('text','')
        url=re.search(r'Download URL[^\n]*: (https://\S+)',text)
        node=re.search(r'Source: node (\d+)',text)
        if not url or not node: continue
        key=mapping[node[1]]
        with urllib.request.urlopen(url[1],timeout=60) as response: data=response.read()
        (HERE/'icons'/f'{key}.png').write_bytes(data)
        manifest.append(dict(icon=key,job=job,node=node[1],source=url[1].split('?')[0],sha256=hashlib.sha256(data).hexdigest()))
    if len(manifest)!=len(mapping): raise RuntimeError('Incomplete icon outputs')
    save('icons-output-provenance',manifest)
    restore_alpha()
    print('Collected',len(manifest),'Comfy-finished icons')

def restore_alpha():
    # Comfy's quantizer outputs RGB. Preserve each authored transparency mask exactly.
    manifest=json.loads((HERE/'icons-output-provenance.json').read_text(encoding='utf8'))
    (HERE/'cloud-icons').mkdir(exist_ok=True)
    for item in manifest:
        key=item['icon'];raw=HERE/'cloud-icons'/f'{key}.png';target=HERE/'icons'/f'{key}.png'
        if not raw.exists(): raw.write_bytes(target.read_bytes())
        image=Image.open(raw).convert('RGBA');image.putalpha(Image.open(HERE/(key+'-draft.png')).convert('RGBA').getchannel('A'))
        image.save(target)
        item['adoptedSha256']=hashlib.sha256(target.read_bytes()).hexdigest()
        item['alphaMask']=key+'-draft.png'
    save('icons-output-provenance',manifest)

if __name__=='__main__':
    if sys.argv[1:] == ['collect']: collect()
    elif sys.argv[1:] == ['alpha']: restore_alpha()
    else: submit()
