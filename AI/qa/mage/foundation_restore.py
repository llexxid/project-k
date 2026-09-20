"""Restore pre-QA settings and perform the user's one-time, backed-up bag clear.

Normal accounts are identified from the original account-scoped offline preference,
never from the diagnostic login. No archive is extracted onto the device wholesale.
"""
import os,sys,json,time,tarfile,hashlib,re,copy,xml.etree.ElementTree as ET
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'hud'))
from device_checks import run,PACKAGE
BASE=Path('Recordings/FoundationRevision')
QA=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
EXTERNAL='/sdcard/Android/data/'+PACKAGE

def files(path):
 with tarfile.open(path) as t:return {f.name:t.extractfile(f).read() for f in t if f.isfile()}
def original():
 ext=files(BASE/'Original/external.tar');prefs=files(BASE/'Original/internal.tar')
 accounts=set()
 for name,raw in prefs.items():
  if name.startswith('shared_prefs/'):
   for element in ET.fromstring(raw):
    match=re.fullmatch(r'offline_reward_last_active_utc_ms_([A-Fa-f0-9]+)',element.attrib.get('name',''))
    if match:accounts.add(match[1])
 assert len(accounts)==1,'Identify the actual account before clearing any inventory.'
 account=next(iter(accounts));key=hashlib.sha256(account.encode()).hexdigest()
 assert key!=QA
 path='files/progression-local-v1/'+key+'.json';assert path in ext
 return ext,prefs,path
def clear_bag(raw):
 state=json.loads(raw);before=copy.deepcopy(state);count=stones=0
 def amount(code):
  rarity=(code>>12)&15;assert rarity in (0,1,2);return (1,4,12)[rarity]
 for name in ('Equipment','PendingEquipment'):
  kept=[]
  for item in state[name]:
   if item.get('Player') is not None or item.get('Locked',False):kept.append(item);continue
   count+=1;spent=item.get('EnhancementStonesSpent',0);assert spent>=0
   stones+=amount(item['Code'])+spent*4//5
  state[name]=kept
 for stack in state.get('LegacyEquipment',[]):
  assert stack['Count']>=0;count+=stack['Count'];stones+=amount(stack['Code'])*stack['Count']
 state['LegacyEquipment']=[]
 state['Wallet']['EquipmentStone']=state['Wallet'].get('EquipmentStone',0)+stones
 state['Revision']+=1
 allowed={'Equipment','PendingEquipment','LegacyEquipment','Wallet','Revision'}
 assert all(state[k]==v for k,v in before.items() if k not in allowed)
 assert all(state['Wallet'][k]==v for k,v in before['Wallet'].items() if k!='EquipmentStone')
 result=json.dumps(state,ensure_ascii=False,separators=(',',':')).encode('utf8')
 report=dict(cleared=count,stones=stones,protected=sum(len(state[n]) for n in ('Equipment','PendingEquipment')),originalSha256=hashlib.sha256(raw).hexdigest(),preparedSha256=hashlib.sha256(result).hexdigest())
 return result,report
def preview():
 ext,prefs,path=original();prepared,report=clear_bag(ext[path]);print(json.dumps(report),flush=True)

def restore():
 ext,prefs,path=original();prepared,report=clear_bag(ext[path])
 out=BASE/'FinalRestore';out.mkdir(exist_ok=True)
 assert not (out/'restoration.json').exists(),'Already restored; do not repeat the bag conversion.'
 run('shell','am','force-stop',PACKAGE)
 for name,args in [('external.tar',('tar','-cf','-','-C',EXTERNAL,'files')),('internal.tar',('tar','-cf','-','files','shared_prefs'))]:
  target=out/name;assert not target.exists(),'A backup exists: inspect it before retrying.'
  target.write_bytes(run('exec-out','run-as',PACKAGE,*args).stdout)
 current=files(out/'external.tar');internal=files(out/'internal.tar')
 saved={n:b for n,b in ext.items() if n.startswith('files/progression-local-v1/') and QA not in n}
 assert all(current.get(n)==b for n,b in saved.items()),'A non-diagnostic save changed: preserve it and inspect.'
 def write_external(name,raw):
  assert re.fullmatch(r'files/progression-local-v1/[a-f0-9]{64}\.json(\.bak)?',name)
  destination=EXTERNAL+'/'+name
  run('shell',f"run-as {PACKAGE} sh -c 'cat > {destination}.foundation-tmp && mv {destination}.foundation-tmp {destination}'",data=raw)
  assert run('exec-out','run-as',PACKAGE,'cat',destination).stdout==raw
 for name,raw in ext.items():
  if QA in name:write_external(name,raw)
 # Preserve the exact original separately; primary and recovery backup agree after the requested clear.
 (out/'actual-user-before.json').write_bytes(ext[path]);(out/'actual-user-after.json').write_bytes(prepared)
 write_external(path,prepared);write_external(path+'.bak',prepared)
 for name,raw in prefs.items():
  if not name.startswith('shared_prefs/'):continue
  assert re.fullmatch(r'shared_prefs/[A-Za-z0-9_.-]+',name)
  run('shell',f"run-as {PACKAGE} sh -c 'cat > {name}'",data=raw)
  if run('shell','run-as',PACKAGE,'test','-f',name+'.bak',check=False).returncode==0:
   run('shell',f"run-as {PACKAGE} sh -c 'cat > {name}.bak'",data=raw)
  assert run('exec-out','run-as',PACKAGE,'cat',name).stdout==raw
 removed=[n for n in internal if n not in prefs and re.fullmatch(r'files/(balance|hud|settings|lobby)-[A-Za-z0-9_.-]+\.(json|pending)',n)]
 for start in range(0,len(removed),60):run('shell','run-as',PACKAGE,'rm','-f',*removed[start:start+60])
 for name,raw in saved.items():
  if name in (path,path+'.bak'):continue
  assert run('exec-out','run-as',PACKAGE,'cat',EXTERNAL+'/'+name).stdout==raw
 report.update(passed=True,otherSavedFilesVerified=len(saved)-2,diagnosticFilesRemoved=len(removed),externalBackupSha256=hashlib.sha256((out/'external.tar').read_bytes()).hexdigest())
 (out/'restoration.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report),flush=True)

if __name__=='__main__':{'preview':preview,'restore':restore}[sys.argv[1]]()
