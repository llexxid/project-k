"""Release combat checks on the isolated Android QA package. Uses real gameplay time."""
import sys,json,time
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings

def ready(tag):
 for i in range(50):
  s=m.command(f'{tag}-ready-{i}')['state']
  if all(not x['casting'] and x['ratio']==0 for x in s['mageCooldown']):return
  time.sleep(.6)
 raise TimeoutError(tag)

def speed(value,tag):return m.command(tag,'timescale',value=value)['state']
def cast(skill,tag):
 s=m.command(tag,'mage-cast',value=skill)
 assert s['result']['accepted'],s['result']
 return s['state']
def events(s):return [e for e in s['mageEvents'] if e['kind']=='damage']
def low(on):
 settings.settings_open('combat-settings-'+str(on))
 settings.toggle('LowSpec',on)
 m.tap('BtnSaveClose',m.state('combat-save-'+str(on)))

report={}
try:
 report['persistedAfterUpgrade']=m.command('upgrade-persistence')['state']['mage']['0']['BloomEnabled']
 m.command('combat-fixture','mage-fixture',enhance=10,awaken=10,bloom=False)
 m.command('combat-auto-off','mage-auto',value=0)
 ready('combat-initial')
 low(False)
 m.command('lightning-enable','mage-bloom',value=0,bloom=True)
 m.command('lightning-boss','stage',stage=0x20003000b);time.sleep(2)
 speed(25,'lightning-slow')
 begin=cast(0,'lightning-cast')
 time.sleep(1)
 speed(0,'lightning-cloud-freeze');cloud=m.command('lightning-cloud-state')['state'];m.shot('lightning-cloud')
 assert any('LightningBloomCloud' in v['name'] for v in cloud['mageVisuals'])
 assert not events(cloud)
 toggle=m.command('lightning-midcast-toggle','mage-bloom',value=0,bloom=False)
 assert toggle['result']['accepted']
 speed(25,'lightning-charge-resume')
 for i in range(35):
  s=m.command(f'lightning-charge-{i}')['state']
  if events(s):break
  time.sleep(.15)
 else:raise AssertionError('No lightning strike')
 speed(0,'lightning-strike-freeze');strike=m.command('lightning-strike-state')['state'];m.shot('lightning-strike')
 assert events(strike) and all(e['amount']==2660 and e['bloom'] for e in events(strike))
 report['lightning']={'beginTime':begin['time'],'strikeObservedTime':s['time'],'damage':events(strike),'cloud':cloud['mageVisuals'],'strike':strike['mageVisuals'],'cooldown':strike['mageCooldown'][0]}
 speed(100,'ice-normal');ready('ice')
 m.command('ice-enable','mage-bloom',value=1,bloom=True)
 m.command('ice-boss-stage','stage',stage=0x20003000b);time.sleep(2)
 speed(25,'ice-slow');begin=cast(1,'ice-boss-cast')
 for i in range(15):
  s=m.command(f'ice-stun-{i}')['state']
  if s['crowdControl']:break
  time.sleep(.1)
 else:raise AssertionError('No ice stun')
 speed(0,'ice-crystal-freeze');active=m.command('ice-crystal-state')['state'];m.shot('ice-boss-crystal')
 cc=active['crowdControl'][0];expiry=active['time']+cc['remaining']
 assert cc['kind']=='Stun' and 1.7<cc['remaining']<=2
 assert len(events(active))==1 and events(active)[0]['amount']==1443
 speed(100,'ice-stun-resume')
 samples=[]
 for i in range(14):
  s=m.command(f'ice-expiry-{i}')['state'];samples.append({'time':s['time'],'cc':s['crowdControl']})
  if not s['crowdControl']:break
  time.sleep(.1)
 assert not s['crowdControl'] and s['time']>=expiry-.04
 report['iceBoss']={'damage':events(active),'remainingAtCapture':cc['remaining'],'expiryTime':expiry,'samples':samples}
 report['iceCrowd']=[]
 for on in (False,True):
  ready('crowd-'+str(on));low(on)
  m.command('crowd-skill-'+str(on),'mage-configure',value=1,enhance=0,awaken=10,bloom=True)
  m.command('crowd-stage-'+str(on),'stage',stage=0x20003000a);time.sleep(2)
  speed(25,'crowd-slow-'+str(on));cast(1,'crowd-cast-'+str(on))
  speed(0,'crowd-freeze-'+str(on));s=m.command('crowd-state-'+str(on))['state'];m.shot('ice-crowd-'+str(on))
  count=sum('IceSpike' in v['name'] for v in s['mageVisuals'])
  assert count==(4 if on else 8),count
  speed(100,'crowd-resume-'+str(on));time.sleep(2)
  end=m.command('crowd-end-'+str(on))['state'];hits=events(end)
  assert len(hits)==16 and all(e['amount']==75 for e in hits),hits
  report['iceCrowd'].append({'lowSpec':on,'firstVolleyVisuals':count,'hits':len(hits),'damage':sum(e['amount'] for e in hits)})
 print(json.dumps(report,ensure_ascii=False),flush=True)
finally:
 speed(100,'release-combat-final-speed')
 low(False)
 (m.OUT/'release-combat.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
