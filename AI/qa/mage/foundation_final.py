"""Final device viewport, touch and sustained-combat checks on the isolated QA profile."""
import os,sys,json,time,re
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/FoundationRevision/FinalDiagnostic')
m=SourceFileLoader('final_device',str(Path(__file__).with_name('mage-device.py'))).load_module()

def save(name,data):
 (m.OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
def click(name,tag):
 m.tap(name,m.state(tag+'-before'));return m.state(tag+'-after')
def visible(ui,name):
 control=next(c for c in ui['controls'] if c['name']==name and c['interactable']);b=control['bounds']
 assert b['x']>=-1 and b['y']>=-1 and b['x']+b['width']<=ui['width']+1 and b['y']+b['height']<=ui['height']+1,(name,b,ui['width'],ui['height'])
 return b
def aspects():
 original=m.run('shell','wm','size').stdout.decode();results=[]
 try:
  for name,size in [('narrow','720x1280'),('tall','1080x2400'),('tablet','1200x1600')]:
   m.run('shell','wm','size',size);m.launch('final-'+name)
   m.command(name+'-fixture','combat-fixture',value=1,enhance=0,stage=0x20003000a)
   m.command(name+'-mage','mage-fixture',enhance=10,awaken=4,bloom=False)
   for slot,skill in enumerate([8,1,5,7,9]):m.command(name+'-equip-'+str(slot),'mage-equip',value=skill,awaken=slot)
   m.command(name+'-manual','mage-auto',value=0);time.sleep(.8)
   ui=m.state(name+'-battle');m.shot(name+'-battle');visible(ui,'BossChallenge')
   click('BtnCurrency',name+'-currency');ui=m.state(name+'-currency-layout');m.shot(name+'-currency')
   rows=[x for x in ui['labels'] if x['text'] in ['골드','전직 파편','비전지식','루비','강화석']]
   assert len(rows)==5 and all(not r['isTextTruncated'] for r in rows),rows
   assert all(r['bounds']['x']>=0 and r['bounds']['x']+r['bounds']['width']<=ui['width'] for r in rows)
   m.back();click('BtnHamburgerRight',name+'-hamburger');click('BtnMenuInventory',name+'-inventory')
   ui=m.state(name+'-bag');m.shot(name+'-bag')
   controls={n:visible(ui,n) for n in ['JobFilter','RarityFilter','Sort','AutoDismantle','BulkDismantle']}
   click('AutoDismantle',name+'-auto');ui=m.state(name+'-auto-layout');m.shot(name+'-auto')
   for n in ['Rarity0','Rarity1','Rarity2','Confirm']:visible(ui,n)
   m.back();m.back();click('BtnDevelopment',name+'-growth');click('RubyGrowthTab',name+'-ruby')
   ui=m.state(name+'-ruby-layout');m.shot(name+'-ruby');visible(ui,'RubyGrowthTab');visible(ui,'GoldGrowthTab')
   m.back();m.command(name+'-stun','status-fixture',value=0);m.command(name+'-slow','status-fixture',value=1);m.command(name+'-taunt','status-fixture',value=3,captureMs=100)
   time.sleep(.4);s=m.command(name+'-status')['state'];m.shot(name+'-status');assert len(s['statusVisuals'])>=4
   results.append(dict(name=name,requested=size,render=[ui['width'],ui['height']],controls=controls,status=s['statusVisuals']))
   save('aspect-report',results);print(name+' UI and status passed',flush=True)
 finally:
  override=next((l.split(': ')[1].strip() for l in original.splitlines() if l.startswith('Override size:')),None)
  m.run('shell','wm','size',override or 'reset');m.launch('final-aspect-restored')

def sustained():
 m.command('sustained-fixture','combat-fixture',value=1,enhance=0,stage=0x20003000a)
 m.command('sustained-mage','mage-fixture',enhance=10,awaken=4,bloom=False)
 for slot,skill in enumerate([8,4,5,7,9]):m.command('sustained-equip-'+str(slot),'mage-equip',value=skill,awaken=slot)
 m.command('sustained-auto','mage-auto',value=1);m.command('sustained-resume','pause',value=0)
 samples=[];started=time.monotonic()
 for i in range(120):
  time.sleep(5);s=m.command('sustained-'+str(i))['state']
  assert s['environment']['renderers']==3
  samples.append(dict(seconds=round(time.monotonic()-started,2),stage=s['stage'],runState=s['runState'],party=s['party'],environment=s['environment'],events=s['mageEvents']))
  if i%10==0:save('sustained-report',samples);m.shot('sustained-'+str(i));print('Sustained sample',i,'stage',s['stage'],'state',s['runState'],flush=True)
 save('sustained-report',samples)

if __name__=='__main__':{'aspects':aspects,'sustained':sustained}[sys.argv[1]]()
