"""Capture fixed impact times in real Android combat, with the HUD unobstructed."""
import sys,time,json
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings

def ready(tag):
 for i in range(70):
  s=m.command(f'{tag}-ready-{i}')['state']
  if all(not x['casting'] and x['ratio']==0 for x in s['mageCooldown']):return
  time.sleep(.5)
 raise TimeoutError(tag)

def capture(tag,skill,peak,bloom,stage,enhance=10):
 ready(tag)
 m.command(tag+'-configure','mage-configure',value=skill,enhance=enhance,awaken=10,bloom=bloom)
 m.command(tag+'-stage','stage',stage=stage);time.sleep(2)
 m.command(tag+'-close','mage-close')
 hud=m.state(tag+'-hud')
 assert not any(c['name'] in ('BtnBloom','BtnSaveClose') for c in hud['controls'])
 m.command(tag+'-slow','timescale',value=25)
 result=m.command(tag+'-cast','mage-cast',value=skill,captureMs=peak)
 assert result['result']['accepted']
 time.sleep(peak/250+.4)
 s=m.command(tag+'-state')['state'];m.shot(tag)
 expected={0:'LightningBloomStrike',1:'IceSpike',5:'StoneSeal',8:'Meteor'}[skill]
 assert any(expected+'(Clone)'==v['name'] for v in s['mageVisuals']),(tag,s['mageVisuals'])
 m.command(tag+'-resume','timescale',value=100)
 return {'tag':tag,'peakMs':peak,'visuals':s['mageVisuals'],'events':s['mageEvents']}

report=[]
try:
 m.command('impact-close','mage-close');m.command('impact-speed','timescale',value=100)
 m.command('impact-fixture','mage-fixture',enhance=10,awaken=10,bloom=False)
 m.command('impact-auto-off','mage-auto',value=0)
 for tag,skill,peak,bloom,stage in [
  ('thunder-peak',0,1980,True,0x20003000b),
  ('stone-readable',5,590,False,0x20003000a),
  ('meteor-flight',8,660,False,0x20003000a)]:
  report.append(capture(tag,skill,peak,bloom,stage))
 settings.settings_open('impact-low');settings.toggle('LowSpec',True)
 m.tap('BtnSaveClose',m.state('impact-low-save'))
 report.append(capture('ice-low-visible',1,180,True,0x20003000a,enhance=0))
 assert sum('IceSpike' in v['name'] for v in report[-1]['visuals'])==4
finally:
 m.command('impact-final-speed','timescale',value=100)
 settings.settings_open('impact-restore');settings.toggle('LowSpec',False)
 m.tap('BtnSaveClose',m.state('impact-restore-save'));m.command('impact-final-close','mage-close')
 (m.OUT/'impact-review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf8')
print('Reviewed impacts:',len(report),flush=True)
