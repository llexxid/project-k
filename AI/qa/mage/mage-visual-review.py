"""Capture each skill in real battle at quarter speed, then finish at normal speed."""
import sys,json,time
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
report=[]
try:
 m.command('visual-close','mage-close');m.command('visual-speed','timescale',value=100)
 m.command('visual-fixture','mage-fixture',enhance=10,awaken=10,bloom=False)
 for skill in [0,1,2,3,4,5,6,8,9,7]:
  for i in range(50):
   s=m.command(f'visual-ready-{skill}-{i}')['state']
   if all(not c['casting'] and c['ratio']==0 for c in s['mageCooldown']):break
   time.sleep(.5)
  m.command(f'visual-stage-{skill}','stage',stage=0x20003000a);time.sleep(3)
  if skill==7:
   for i in range(90):
    s=m.command(f'visual-injury-{i}')['state']
    if any(0<p['hp']<p['maxHP']*.9 for p in s['party']):break
    time.sleep(1)
  m.command(f'visual-slow-{skill}','timescale',value=25)
  peak={0:180,1:180,2:300,3:90,4:300,5:590,6:180,7:200,8:1310,9:300}[skill]
  s=m.command(f'visual-cast-{skill}','mage-cast',value=skill,captureMs=peak)
  assert s['result']['accepted'],(skill,s['state']['mageCooldown'])
  time.sleep(peak/250+.3)
  active=m.command(f'visual-active-{skill}')['state'];m.shot(f'visual-{skill}')
  m.command(f'visual-resume-{skill}','timescale',value=100);time.sleep(9)
  final=m.command(f'visual-ended-{skill}')['state']
  ev=[e for e in final['mageEvents'] if e['kind'] in ('damage','heal')]
  assert ev,(skill,final['mageEvents'])
  item={'id':skill,'events':len(ev),'amount':sum(e['amount'] for e in ev),'visuals':active['mageVisuals'],'ended':not final['mageCooldown'][0]['casting']}
  report.append(item);print(json.dumps({'id':skill,'events':len(ev),'amount':item['amount']}),flush=True)
  (m.OUT/'visual-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
finally:m.command('visual-final-speed','timescale',value=100)
