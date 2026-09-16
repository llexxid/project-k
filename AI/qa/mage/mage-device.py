import sys,json,os,time,hashlib
from pathlib import Path
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CatalogIntegration/Mage/Final/Device')
sys.path.insert(0,'AI/qa/hud');sys.path.insert(0,'AI/qa/settings')
from device_checks import run,OUT,PACKAGE,shot,state,tap,back
from settings_checks import launch

def command(name,action='state',**args):
 assert name.replace('-','').replace('_','').isalnum()
 filename=f'files/balance-{name}.json';run('shell','run-as',PACKAGE,'rm','-f',filename)
 payload=json.dumps(dict(id=name,action=action,**args)).encode()
 run('shell',f"run-as {PACKAGE} sh -c 'cat > files/balance-command.pending && mv files/balance-command.pending files/balance-command.json'",data=payload)
 for _ in range(160):
  r=run('exec-out','run-as',PACKAGE,'cat',filename,check=False)
  if r.returncode==0 and r.stdout.strip():
   try:obj=json.loads(r.stdout)
   except json.JSONDecodeError:time.sleep(.1);continue
   (OUT/f'{name}.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')
   if 'error' in obj:raise RuntimeError(obj['error'])
   return obj
  time.sleep(.1)
 raise TimeoutError(name)

if __name__=='__main__':
 action=sys.argv[1]
 if action=='install':
  backup=Path('.utmp/catalog-integration/mage-validation/device-original-profile.json')
  if not backup.exists():
   run('shell','am','force-stop',PACKAGE)
   digest=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
   backup.write_bytes(run('exec-out','run-as',PACKAGE,'cat',f'/sdcard/Android/data/{PACKAGE}/files/progression-local-v1/{digest}.json').stdout)
  print(run('install','-r','-t',str(OUT/'KingdomIdle-LobbyQA.apk')).stdout.decode())
  run('shell','input','keyevent','KEYCODE_WAKEUP');launch('mage-launch')
  print(json.dumps({'main':state('initial-hud')['main'],'roster':len(command('initial-state')['state']['mage'])}))
 elif action=='shot':shot(sys.argv[2])
 elif action=='hud':print(json.dumps(state(sys.argv[2]),ensure_ascii=False))
 else:
  args=json.loads(sys.argv[3]) if len(sys.argv)>3 else {}
  obj=command(sys.argv[2],action,**args)
  print(json.dumps({'result':obj.get('result'),'stage':obj['state'].get('stage'),'runState':obj['state'].get('runState'),'mage':obj['state'].get('mage'),'events':obj['state'].get('mageEvents'),'cc':obj['state'].get('crowdControl'),'cooldown':obj['state'].get('mageCooldown')},ensure_ascii=False))
