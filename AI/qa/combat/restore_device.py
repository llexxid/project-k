"""Restore the fresh pre-task backup to the isolated QA account and verify bytes."""
import sys,os,runpy,hashlib,json
from pathlib import Path
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Restoration')
m=runpy.run_path('AI/qa/hud/device_checks.py');run=m['run'];PACKAGE=m['PACKAGE']
backup=Path(sys.argv[1] if len(sys.argv)>1 else '.utmp/combat-revision/device-backup')
for name in ['profile.json','profile.json.bak','prefs.xml','stay-awake.txt']:
 assert (backup/name).is_file() and (backup/name).stat().st_size>0,name
digest=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
run('shell','am','force-stop',PACKAGE)
paths={
 'profile.json':f'/sdcard/Android/data/{PACKAGE}/files/progression-local-v1/{digest}.json',
 'profile.json.bak':f'/sdcard/Android/data/{PACKAGE}/files/progression-local-v1/{digest}.json.bak',
 'prefs.xml':f'shared_prefs/{PACKAGE}.v2.playerprefs.xml'
}
verified={}
for name,target in paths.items():
 data=(backup/name).read_bytes()
 run('shell',f"run-as {PACKAGE} sh -c 'cat > {target}.combat-restore && mv {target}.combat-restore {target}'",data=data)
 actual=run('exec-out','run-as',PACKAGE,'cat',target).stdout
 assert actual==data,name
 verified[name]=hashlib.sha256(actual).hexdigest()
 if name=='prefs.xml' and run('shell','run-as',PACKAGE,'test','-e',target+'.bak',check=False).returncode==0:
  run('shell',f"run-as {PACKAGE} sh -c 'cat > {target}.bak'",data=data)
for name in ['profile.json','profile.json.bak']:
 target=paths[name]+'.combat-smoke-backup'
 if run('shell','run-as',PACKAGE,'test','-e',target,check=False).returncode==0:
  run('shell','run-as',PACKAGE,'rm',target)
run('shell','wm','size','1080x2316');run('shell','wm','density','450')
stay=(backup/'stay-awake.txt').read_text(encoding='utf8').strip();assert stay.isdigit()
run('shell','settings','put','global','stay_on_while_plugged_in',stay)
assert run('shell','settings','get','global','stay_on_while_plugged_in').stdout.decode().strip()==stay
run('shell','am','force-stop',PACKAGE)
assert not run('shell','pidof',PACKAGE,check=False).stdout.strip()
result=dict(verified=verified,stopped=True,stayAwakeRestored=int(stay),size=run('shell','wm','size').stdout.decode().strip(),density=run('shell','wm','density').stdout.decode().strip())
(m['OUT']/'restored.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
print('Original profiles/prefs byte-verified, display and stay-awake restored, QA package stopped.')
