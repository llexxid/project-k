"""Final build checks, followed by a fresh isolated QA profile sample.
Fresh mode moves only this synthetic QA account to named backups; restore_device.py
must subsequently restore the pre-task backup. Production package is never used.
"""
import os,sys,time,runpy,hashlib,json
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Iteration7c/Device')
m=runpy.run_path('AI/qa/combat/device_revision.py');q=runpy.run_path('AI/qa/settings/settings_checks.py')
c=m['c'];run=m['run'];shot=m['shot'];state=m['state'];OUT=m['OUT'];PACKAGE=m['PACKAGE']
def core():
 assert state('final-entry')['main']
 r=c('final-combat-acceptance','combat-acceptance');assert r['result']['passed']==13
 for name,stage in [('normal',0x20003000A),('boss',0x20003000B)]:
  c('final-'+name+'-fixture','combat-fixture',stage=stage,value=1,enhance=0);time.sleep(2)
  r=c('final-'+name+'-control','combat-control');assert r['result']['passed']==9
 c('final-goblin-fixture','combat-fixture',stage=0x20002000A,value=1,enhance=0)
 c('final-goblin-reset','combat-reset');time.sleep(15)
 r=c('final-goblin-15');shot('final-goblin-15')
 assert any(e['actor']=='GoblinBomb_Flight(Clone)' and e['kind']=='projectile-hit' for e in r['state']['combatEvents'])
 assert not r['state']['lastError'];print('Final combat 13 + live control 18 + atlased goblin bomb hits passed',flush=True)
def fresh():
 digest=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
 folder=f'/sdcard/Android/data/{PACKAGE}/files/progression-local-v1'
 run('shell','am','force-stop',PACKAGE)
 for suffix in ['', '.bak']:
  original=f'{folder}/{digest}.json{suffix}';moved=original+'.combat-smoke-backup'
  # Refuse to overwrite an earlier smoke backup.
  assert run('shell','run-as',PACKAGE,'test','-e',moved,check=False).returncode!=0,moved
  run('shell','run-as',PACKAGE,'mv',original,moved)
 q['launch']('fresh-launch');start=c('fresh-start');shot('fresh-start')
 assert start['state']['AttackLevel']==0 and start['state']['HealthLevel']==0
 for n in range(2):
  time.sleep(30);r=c('fresh-'+str((n+1)*30));shot('fresh-'+str((n+1)*30))
  print('FRESH', {k:r['state'].get(k) for k in ['stage','runState','Kills','AccountLevel','AttackLevel','HealthLevel','lastError']},flush=True)
 assert not r['state']['lastError']
if __name__=='__main__':globals()[sys.argv[1]]()
