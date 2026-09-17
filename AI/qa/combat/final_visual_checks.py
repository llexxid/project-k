"""Final targeted captures on the isolated QA package. Run only one device script at a time."""
import os,sys,runpy,time,json
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Iteration6/Final')
m=runpy.run_path('AI/qa/combat/device_revision.py');q=runpy.run_path('AI/qa/settings/settings_checks.py')
c=m['c'];state=m['state'];shot=m['shot'];tap=m['tap'];run=m['run'];back=m['back']

def spells():
 back();c('extra-auto-off','mage-auto',value=0)
 c('extra-fixture','mage-fixture',awaken=10,enhance=0,bloom=True)
 for ident,slot,stage,delay in [(4,4,0x20003000A,900),(1,1,0x20003000B,200),(0,0,0x20003000A,180)]:
  c('extra-stage-'+str(ident),'stage',stage=stage)
  if ident==0:c('extra-lightning-base','mage-bloom',value=0,bloom=False)
  c('extra-equip-'+str(ident),'mage-equip',value=ident,awaken=slot)
  r=c('extra-cast-'+str(ident),'mage-slot-cast',value=slot,captureMs=delay)
  if not r['result']['accepted']:
   time.sleep(30)
   r=c('extra-retry-'+str(ident),'mage-slot-cast',value=slot,captureMs=delay)
  assert r['result']['accepted'],r['state']['mageCooldown']
  time.sleep(delay/1000+.2);shot('extra-skill-'+str(ident));c('extra-visual-'+str(ident))
  c('extra-resume-'+str(ident),'timescale',value=100);time.sleep(3);c('extra-after-'+str(ident))
 c('spear-fixture','combat-fixture',stage=0x20003000A,value=1,enhance=0)
 c('spear-reset','combat-reset');time.sleep(12);r=c('spear-12');shot('spear-12')
 print('projectiles',[e for e in r['state']['combatEvents'] if e['kind']=='projectile-hit'][-6:],flush=True)

def low():
 q['settings_open']('low-open');tap('Tab2',state('low-device-tab'));q['toggle']('LowSpec',True,'low-on');shot('low-settings');back()
 c('low-reset','combat-reset');time.sleep(30);r=c('low-30');shot('low-30')
 print('LOW',{k:r['state'][k] for k in ['timeScale','meanFrameMs','maxFrameMs','memory','lastError']},flush=True)
 assert r['state']['timeScale']==1 and not r['state']['lastError']
 q['settings_open']('low-close-open');tap('Tab2',state('low-off-tab'));q['toggle']('LowSpec',False,'low-off');back()

def menus():
 tap('BtnGacha',state('gacha-entry'));shot('gacha');s=state('gacha-screen');print('GACHA',[(x['name'],x['text']) for x in s['labels']],flush=True);back()
 tap('BtnDevelopment',state('development-entry'));shot('enhance');state('enhance');back()
 tap('BtnKingdomArmy',state('tree-entry'));s=state('tree-army');m['click_label']('전직',s);s=state('tree-top');shot('tree-top')
 for n in range(2):
  run('shell','input','swipe',str(int(s['width']*.55)),str(int(s['height']*.75)),str(int(s['width']*.55)),str(int(s['height']*.47)),'650');time.sleep(.4)
 s=state('tree-bottom');shot('tree-bottom');assert not any('궁수' in x['text'] for x in s['labels']);back()

def tablet():
 try:
  run('shell','wm','size','1200x1600');run('shell','wm','density','320');q['launch']('tablet-final')
  tap('BtnKingdomArmy',state('tablet-entry'));m['click_label']('전직',state('tablet-army'));s=state('tablet-top');shot('tablet-top')
  for _ in range(3):
   run('shell','input','swipe','660','1120','660','800','650');time.sleep(.4)
  s=state('tablet-bottom');shot('tablet-bottom');print('TABLET',[(x['text'],x['bounds']) for x in s['labels'] if x['text'] in ['정예 기사','정예 마법사','곧 추가 예정']],flush=True)
 finally:
  run('shell','wm','size','1080x2316');run('shell','wm','density','450')

if __name__=='__main__':globals()[sys.argv[1]]()
