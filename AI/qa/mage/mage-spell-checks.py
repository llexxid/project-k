import sys,json,time
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from importlib.machinery import SourceFileLoader
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
ui=m.state('spells-close')
if any(x['name']=='BtnDone' for x in ui['controls']):m.tap('BtnDone',ui)
ui=m.state('spells-close-panel')
if any(x['name']=='BtnPanelClose' for x in ui['controls']):m.tap('BtnPanelClose',ui)
m.command('spells-bloom-off','mage-bloom',value=0,bloom=False)
results=[]
for skill in [7,0,1,2,3,4,5,6,8,9]:
 for retry in range(45):
  s=m.command(f'ready-{skill}-{retry}')['state']
  if s['mageCooldown'][0]['ratio']==0 and not s['mageCooldown'][0]['casting']:break
  time.sleep(.75)
 if skill!=7:
  m.command(f'stage-{skill}','stage',stage=0x200030006)
  time.sleep(3)
 else:
  for retry in range(90):
   current=m.command(f'heal-ready-{retry}')['state']
   if any(p['hp']>0 and p['hp']<p['maxHP']*.9 for p in current['party']):break
   time.sleep(1)
 s=m.command(f'cast-{skill}','mage-cast',value=skill)
 assert s['result']['accepted'],(skill,s['state']['runState'],s['state']['party'])
 time.sleep(.35 if skill in (0,1,3,6) else 1.25 if skill==8 else .65)
 m.command(f'freeze-{skill}','pause',value=1)
 m.shot(f'spell-{skill}')
 active=m.command(f'active-{skill}')['state']
 m.command(f'resume-{skill}','pause',value=0)
 time.sleep(8 if skill in (2,4,7) else 5)
 final=m.command(f'after-{skill}')['state']
 ev=final['mageEvents'];damages=[x for x in ev if x['kind'] in ('damage','rejected','heal')]
 result={'id':skill,'accepted':True,'events':len(damages),'amount':sum(x['amount'] for x in damages),'kinds':sorted(set(x['kind'] for x in ev)),'activeCC':len(active['crowdControl']),'ended':not final['mageCooldown'][0]['casting'],'party':[(p['hp'],p['maxHP']) for p in final['party']]}
 results.append(result);print(json.dumps(result),flush=True)
 (m.OUT/'spell-checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
