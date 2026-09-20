import sys,json,time
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from importlib.machinery import SourceFileLoader
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
def ready(tag):
 for i in range(45):
  s=m.command(f'{tag}-ready-{i}')['state']
  if not s['mageCooldown'][0]['casting'] and s['mageCooldown'][0]['ratio']==0:return
  time.sleep(.75)
 raise TimeoutError(tag)
try:
 ready('lightning-bloom')
 m.command('lightning-bloom-config','mage-bloom',value=0,bloom=True)
 m.command('lightning-bloom-stage','stage',stage=0x20003000b);time.sleep(2)
 cast=m.command('lightning-bloom-cast','mage-cast',value=0);assert cast['result']['accepted']
 toggle=m.command('lightning-midcast-off','mage-bloom',value=0,bloom=False)
 assert toggle['result']['accepted'] and toggle['result']['cooldown']>0
 m.command('cloud-freeze','pause',value=1);m.shot('lightning-cloud')
 m.command('cloud-resume','pause',value=0);time.sleep(1.1)
 m.command('strike-freeze','pause',value=1);m.shot('lightning-strike')
 m.command('strike-resume','pause',value=0);time.sleep(2)
 end=m.command('lightning-bloom-after')['state'];events=[x for x in end['mageEvents'] if x['kind'] in ('damage','rejected')]
 assert events and all(x['bloom'] and x['amount']==2660 for x in events),events
 print(json.dumps({'lightning':events,'cooldownAfterToggle':end['mageCooldown'][0]},ensure_ascii=False),flush=True)
 ready('ice-boss')
 m.command('ice-bloom-config','mage-bloom',value=1,bloom=True)
 m.command('ice-boss-stage','stage',stage=0x20003000b);time.sleep(2)
 s=m.command('ice-boss-before')['state'];assert len(s['monsters'])==1
 cast=m.command('ice-boss-cast','mage-cast',value=1);assert cast['result']['accepted']
 time.sleep(.2);m.command('ice-boss-freeze','pause',value=1);m.shot('ice-boss-crystal')
 cc=m.command('ice-boss-active')['state']
 m.command('ice-boss-resume','pause',value=0);time.sleep(3)
 end=m.command('ice-boss-after')['state'];events=[x for x in end['mageEvents'] if x['kind'] in ('damage','rejected')]
 assert len(events)==1 and events[0]['amount']==1443 and events[0]['bloom'],events
 print(json.dumps({'iceBoss':events,'ccAtCapture':cc['crowdControl'],'ccAfter3sec':end['crowdControl']},ensure_ascii=False),flush=True)
 ready('ice-crowd')
 m.command('ice-crowd-stage','stage',stage=0x200030006);time.sleep(3)
 cast=m.command('ice-crowd-cast','mage-cast',value=1);assert cast['result']['accepted']
 m.command('ice-crowd-freeze','pause',value=1);m.shot('ice-crowd')
 m.command('ice-crowd-resume','pause',value=0);time.sleep(2)
 end=m.command('ice-crowd-after')['state'];events=[x for x in end['mageEvents'] if x['kind'] in ('damage','rejected')]
 assert len(events)==16 and all(x['amount']==111 for x in events),events
 print(json.dumps({'iceCrowdHits':len(events),'total':sum(x['amount'] for x in events)},ensure_ascii=False),flush=True)
finally:m.command('bloom-final-resume','pause',value=0)
