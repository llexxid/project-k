import sys,json,time
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings

def low(on):
 settings.settings_open('perf-settings-'+str(on));settings.toggle('PowerSave',False);settings.toggle('LowSpec',on)
 m.tap('BtnSaveClose',m.state('perf-save-'+str(on)))
def fixture(tag):
 m.command(tag+'-fixture','mage-fixture',enhance=10,awaken=10,bloom=False)
 m.command(tag+'-ice-bloom','mage-bloom',value=1,bloom=True)
 for slot,skill in enumerate([1,2,4,7,9]):
  assert m.command(f'{tag}-equip-{slot}','mage-equip',value=skill,awaken=slot)['result']['accepted']
 m.command(tag+'-stage','stage',stage=0x200030006);time.sleep(2)
 m.command(tag+'-auto','mage-auto',value=1)

report={}
try:
 settings.command('perf-remove-fixture','fixture','off');settings.command('perf-remove-effect','effect','off')
 for on in [False,True]:
  tag='mage-perf-'+str(on)
  low(on);fixture(tag);time.sleep(4)
  before=m.command(tag+'-before')['state'];measured=settings.measure(tag,20);after=m.command(tag+'-after')['state'];m.shot(tag)
  report[tag]={'before':{'time':before['time'],'environment':before['environment'],'mageSlots':before.get('mageSlots')},'after':{'time':after['time'],'environment':after['environment'],'events':after['mageEvents'],'lastError':after['lastError']},'measurement':measured}
  assert not after['lastError']
  m.command(tag+'-auto-off','mage-auto',value=0)
  time.sleep(17)
 # Cancel a live, long field during a battle transition. Automatic casts stay off.
 m.command('transition-stage-before','stage',stage=0x200030006);time.sleep(2)
 cast=m.command('transition-fire-cast','mage-cast',value=2);assert cast['result']['accepted']
 assert cast['state']['mageCooldown'][0]['casting']
 m.command('transition-new-stage','stage',stage=0x200020006);time.sleep(1)
 first=m.command('transition-first')['state'];time.sleep(2);end=m.command('transition-settled')['state']
 assert not any(x['casting'] for x in end['mageCooldown'])
 assert not end['mageVisuals']
 assert first['mageEvents']==end['mageEvents']
 report['transition']={'stage':end['stage'],'visuals':len(end['mageVisuals']),'casting':False,'noLateDamage':True,'events':end['mageEvents']}
finally:
 m.command('perf-auto-off','mage-auto',value=0);m.command('perf-timescale','timescale',value=100);low(False)
 (m.OUT/'release-performance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v.get('measurement',v) for k,v in report.items()},ensure_ascii=False),flush=True)
