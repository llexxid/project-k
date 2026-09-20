"""Compare coherent sheet upscalers without generating independent animation frames."""
import json, re, sys, urllib.request
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lobby'))
from comfy_client import Comfy
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]

def save(name, value):
    (OUT / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')

def unpack(response):
    if response.get('isError'): raise RuntimeError(response)
    if response.get('structuredContent'): return response['structuredContent'].get('result', response['structuredContent'])
    for part in response.get('content', []):
        if part['type'] == 'text':
            try: return json.loads(part['text'])
            except json.JSONDecodeError: pass
    raise ValueError('No structured result')

def main():
    paths = ['Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Void/VoidBlackHole.png',
             'Assets/_Project/Art/VFX/MageTower/ThunderViolet.png',
             'Assets/_Project/Art/VFX/MageTower/ThunderImpactViolet.png',
             'Assets/_Project/Art/VFX/PixelArtRPGVFX/Textures/Holy/HolyBlessing.png']
    sheet = Image.new('RGB', (256,384))
    for i, path in enumerate(paths):
        sprite = Image.open(ROOT / path).convert('RGBA')
        assert sprite.size == (64,384)
        sheet.paste(sprite, (i*64,0), sprite)
    sheet.save(OUT / 'source-sheet.png')
    save('references', {'paths':paths, 'layout':'four columns of six 64x64 frames, original top-to-bottom order', 'background':'black', 'preprocessing':'RGBA composite only; no rescaling'})
    c = Comfy()
    request = {'file_path':str(OUT/'source-sheet.png'), 'client_os':'windows'}
    upload = c.call('upload_file', request)
    url = re.search(r'https://[^\s"\']+', upload['content'][0]['text']).group()
    save('upload-provenance', {'request':request, 'response':re.sub(r'https://[^\s"\']+', '<single-use capability URL redacted>', upload['content'][0]['text'])})
    with urllib.request.urlopen(urllib.request.Request(url, data=(OUT/'source-sheet.png').read_bytes(), method='PUT', headers={'Content-Type':'image/png'}), timeout=60) as response:
        uploaded = json.load(response)
    save('upload-response', uploaded)
    graph={'1':{'class_type':'LoadImage','inputs':{'image':'/'.join(x for x in [uploaded['subfolder'],uploaded['name']] if x)}}}
    for base, model, label in [(2,'4x-UltraSharp.pth','UltraSharp'),(6,'RealESRGAN_x4plus_anime_6B.pth','Anime6B')]:
        graph[str(base)]={'class_type':'UpscaleModelLoader','inputs':{'model_name':model}}
        graph[str(base+1)]={'class_type':'ImageUpscaleWithModel','inputs':{'upscale_model':[str(base),0],'image':['1',0]}}
        graph[str(base+2)]={'class_type':'ImageScale','inputs':{'image':[str(base+1),0],'upscale_method':'area','width':512,'height':768,'crop':'disabled'}}
        graph[str(base+3)]={'class_type':'SaveImage','inputs':{'images':[str(base+2),0],'filename_prefix':'ProjectK_VFX_v3_'+label}}
    save('sheet-upscale-api',graph)
    preflight=c.call('submit_workflow',{'workflow':graph,'dry_run':True});save('preflight',preflight)
    if preflight.get('isError'): raise RuntimeError('Preflight rejected')
    saved=c.call('save_workflow',{'workflow_json':graph,'name':'ProjectK VFX coherent sheet upscale v3','description':'Compare UltraSharp and Anime6B at 2x final resolution, all authored frames in one sheet. No independent frame diffusion.'});save('cloud-save',saved)
    wid=unpack(saved)['workflow_id']; reopened=c.call('get_saved_workflow',{'workflow_id':wid});save('cloud-workflow-response',reopened);save('sheet-upscale-ui',unpack(reopened)['workflow_json'])
    args={'workflow':graph,'confirm':True};save('submitted-request',args)
    result=c.call('submit_workflow',args);save('submission',result);pid=unpack(result)['prompt_id']
    save('manifest',dict(status='submitted',prompt_id=pid,workflow_id=wid,workflow_url='https://cloud.comfy.org/#'+wid,api='sheet-upscale-api.json',ui='sheet-upscale-ui.json',references='references.json',prompts='None; non-diffusion upscalers, no seed parameter',models=['4x-UltraSharp.pth','RealESRGAN_x4plus_anime_6B.pth'],node_versions='Cloud package versions unavailable; actual schemas in get_node-correct.json',rationale='Qwen edit copied coarse pixels. Compare spatial reconstruction while preserving existing animated silhouettes, then shared palette and binary alpha. Does not claim new authored facets.',cost='Pending usage report'))
    print(json.dumps({'prompt_id':pid,'workflow_id':wid}),flush=True)

if __name__=='__main__': main()
