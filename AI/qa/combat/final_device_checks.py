"""Post-build isolated device checks. Use mode core, spells, or stress."""
import os,sys,time,json,runpy
from pathlib import Path
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Iteration4/Device')
m=runpy.run_path('AI/qa/combat/device_revision.py');c=m['c'];state=m['state'];shot=m['shot'];run=m['run'];OUT=m['OUT'];PACKAGE=m['PACKAGE']

def core():
 assert state('core-entry')['main'], 'Start the QA combat screen before fixtures.'
 for action in ['combat-acceptance','mage-acceptance','acceptance']:
  r=c(action,action);print(action,{k:v for k,v in r.get('result',{}).items() if k!='checks'},flush=True)
 for name,stage in [('normal',0x20003000A),('boss',0x20003000B)]:
  c(name+'-fixture','combat-fixture',stage=stage,value=1,enhance=0)
  time.sleep(2)
  r=c(name+'-control','combat-control');assert r['result']['passed']==9;print(name,r['result'],flush=True)
 c('taunt-fixture','combat-fixture',stage=0x20003000A,value=1,enhance=0)
 time.sleep(6);c('taunt-hp','combat-hp',value=0);time.sleep(1.5)
 r=c('taunt-auto');shot('taunt-auto');print('AUTO_SHIELD',r['state']['combatParty'], 'TAUNTS',[x for x in r['state']['combatEvents'] if x['kind']=='taunt'],flush=True)
 assert any(p['ShieldHP']>0 for p in r['state']['combatParty'])
 assert any(x['tauntOwner']==0 for x in r['state']['monsters'])
 c('melee-reset','combat-reset');time.sleep(30);r=c('melee-30');shot('melee-30')
 print('MELEE_PERF',{k:r['state'][k] for k in ['meanFrameMs','maxFrameMs','frameCount','memory']},flush=True)

def spells():
 c('spell-fixture','mage-fixture',awaken=10,enhance=0,bloom=True)
 for batch,items in enumerate([[(8,350),(0,2020),(9,600),(3,90),(4,1000)],[(1,190),(2,900),(5,590),(6,150),(7,850)]]):
  if batch:time.sleep(35)
  for slot,(ident,ms) in enumerate(items):
   c('stage-'+str(ident),'stage',stage=0x20003000A)
   if ident==7:c('heal-hp','combat-hp',value=0)
   c('equip-'+str(ident),'mage-equip',value=ident,awaken=slot)
   r=c('cast-'+str(ident),'mage-slot-cast',value=slot,captureMs=ms)
   assert r['result']['accepted'],(ident,r['state']['mageCooldown'])
   time.sleep(ms/1000+.2);shot('skill-'+str(ident));r=c('visual-'+str(ident));print('CAPTURE',ident,'visuals',[x['name'] for x in r['state']['mageVisuals']],flush=True)
   if ident==8:
    c('meteor-slow','timescale',value=25);time.sleep(.45);shot('meteor-frame2');c('meteor-frame2');time.sleep(.45);shot('meteor-frame3');c('meteor-frame3')
   c('resume-'+str(ident),'timescale',value=100);time.sleep(3)
   r=c('after-'+str(ident));shot('late-'+str(ident))
   assert any(x['skill']==ident and x['kind'] in ['damage','heal'] for x in r['state']['mageEvents']),('no hit',ident)
 print('All ten spell captures and impacts passed',flush=True)

def stress():
 c('stress-fixture','combat-fixture',stage=0x20003000A,value=1,enhance=0)
 c('stress-skills','mage-fixture',awaken=10,enhance=0,bloom=True)
 for slot,ident in enumerate([8,0,9,3,4]):c('stress-equip'+str(slot),'mage-equip',value=ident,awaken=slot)
 c('stress-auto','mage-auto',value=1);c('stress-reset','combat-reset')
 for n in range(3):
  time.sleep(30);r=c('stress-'+str((n+1)*30));shot('stress-'+str((n+1)*30))
  print('STRESS',n,{k:r['state'][k] for k in ['timeScale','meanFrameMs','maxFrameMs','frameCount','memory','lastError']},flush=True)
  assert r['state']['timeScale']==1 and not r['state']['lastError']
 run('shell','input','keyevent','KEYCODE_HOME');time.sleep(3)
 run('shell','monkey','-p',PACKAGE,'1');time.sleep(4)
 r=c('resume-background');shot('resume-background');assert r['state']['timeScale']==1
 print('Stress and background resume passed',flush=True)
if __name__=='__main__':globals()[sys.argv[1]]()
