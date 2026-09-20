import sys,json
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,'AI/comfyui/lobby')
from comfy_client import Comfy
root=Path(__file__).resolve().parent;c=Comfy()
def data(r): return r.get('structuredContent') or json.loads(r['content'][0]['text'])
def save(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
for folder in sorted(root.iterdir()):
 if not folder.is_dir() or not (folder/'workflow.api.json').exists():continue
 path=folder/'cloud-save.json'
 if not path.exists():save(path,c.call('save_workflow',{'workflow_json':json.loads((folder/'workflow.api.json').read_text('utf8')),'name':'ProjectK Mage Bloom '+folder.name+' 20260916','overwrite':False}))
 d=data(json.loads(path.read_text('utf8')));name=d.get('filename') or d.get('workflow',{}).get('filename')
 if not name:raise ValueError(d)
 path=folder/'cloud-workflow-response.json'
 if not path.exists():save(path,c.call('get_saved_workflow',{'filename':name}))
 save(folder/'workflow.cloud.ui.json',data(json.loads(path.read_text('utf8')))['workflow_json'])
 print(folder.name,name,flush=True)
save(root/'usage-after-bloom.json',c.call('get_usage_report',{'group_by':'model','granularity':'hour','months':1}))
save(root/'billing-after-bloom.json',c.call('get_billing_activity',{'page':1,'limit':30}))
print('Usage archived',flush=True)
