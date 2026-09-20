"""Playability regression suite. Diagnostics only; uses the isolated QA profile."""
import sys, json, time, os
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
sys.stdout.reconfigure(encoding='utf-8')
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/PlayabilityRevision/Diagnostic1')
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()

def save(name,value):
 (m.OUT/(name+'.json')).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
def core():
 report={}
 m.command('core-crowded','crowded-fixture',value=500)
 time.sleep(3)
 before=m.command('core-before','pause',value=1)['state']
 assert before['equipment']==300 and before['pending']==100 and before['reserve']==100,before
 assert before['runState']=='Running' and before['monsters']
 r=m.command('core-gacha','gacha',value=0)
 drawn=sum(i['amount'] for i in r['result']['items'] if i['rewardType']==2)
 assert r['state']['reserve']==100+drawn and r['state']['Wallet']['AncientCoin']==before['Wallet']['AncientCoin']-500
 report['fullInventoryGacha']={'before':500,'after':r['state']['equipment']+r['state']['pending']+r['state']['reserve'],'result':r['result']}
 m.command('core-dungeon-fixture','dungeon-fixture')
 r=m.command('core-dungeon','dungeon',stage=0x210010001)
 report['dungeon']=r['result']
 assert r['result']['accepted']
 time.sleep(3)
 m.shot('full-inventory-dungeon')
 m.command('core-return','return')
 time.sleep(2)
 after=m.command('core-after')['state']
 assert after['runState']=='Running' and after['monsters']
 report['progress']={k:after[k] for k in ('stage','runState','Kills','equipment','pending','reserve','lastError')}
 m.tap('BtnKingdomArmy',m.state('core-main'));m.shot('army-rectangle')
 print([(c['name'],c['bounds']) for c in m.state('core-army')['controls'] if c['interactable']],flush=True)
 save('core-report',report)

def spells():
 report=[]
 m.command('all-mage-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
 m.command('all-auto-off','mage-auto',value=0)
 for bloom in (False,True):
  for id in range(10):
   tag=f'spell-{id}-'+('bloom' if bloom else 'base')
   m.command(tag+'-config','mage-configure',value=id,enhance=0,awaken=10 if bloom else 0,bloom=bloom)
   m.command(tag+'-stage','stage',stage=0x20003000a)
   time.sleep(2)
   m.command(tag+'-equip','mage-equip',value=id,awaken=0)
   for retry in range(45):
    s=m.command(tag+'-ready-'+str(retry))['state']
    if not s['mageCooldown'][0]['casting'] and s['mageCooldown'][0]['ratio']==0:break
    time.sleep(.5)
   else:raise TimeoutError(tag)
   if id==7:m.command(tag+'-injury','combat-hp',value=0)
   r=m.command(tag+'-cast','mage-cast',value=id,captureMs=1100 if id==0 and bloom else 420)
   assert r['result']['accepted'],(tag,r['state']['runState'],r['state']['party'])
   time.sleep(1.4 if id==0 and bloom else .7)
   m.shot(tag)
   m.command(tag+'-resume','pause',value=0)
   time.sleep(9 if bloom and id in (2,4,7) else 6)
   s=m.command(tag+'-after')['state']
   events=s['mageEvents'];hits=[e for e in events if e['kind']==('heal' if id==7 else 'damage')]
   assert hits and any(e['amount']>0 for e in hits),(tag,events)
   assert any(e['kind']=='end' for e in events),(tag,events)
   item={'id':id,'bloom':bloom,'hits':len(hits),'amount':sum(e['amount'] for e in hits),'events':events}
   report.append(item);save('all-spells-report',report)
   print(json.dumps({k:item[k] for k in ('id','bloom','hits','amount')}),flush=True)

def manual():
 import threading
 m.command('manual-fixture','mage-fixture',enhance=0,awaken=10,bloom=True)
 for slot,id in enumerate((0,1,4,5,7)):m.command('manual-equip-'+str(slot),'mage-equip',value=id,awaken=slot)
 m.command('manual-stage','stage',stage=0x20003000a);time.sleep(2)
 ui=m.state('manual-tray');m.shot('manual-tray')
 controls={x['name']:x for x in ui['controls'] if x['name'].startswith('ManualSkill')}
 assert len(controls)==5,controls
 def drag(name,x,y,tag):
  b=controls[name]['bounds'];sx=round(b['x']+b['width']/2);sy=round(b['y']+b['height']/2)
  worker=threading.Thread(target=m.run,args=('shell','input','swipe',str(sx),str(sy),str(x),str(y),'1800'))
  worker.start();time.sleep(1.1);m.shot(tag+'-drag');worker.join();time.sleep(.4)
 before=m.command('random-before')['state']
 drag('ManualSkill1',530,940,'random')
 after=m.command('random-after')['state']
 assert not after['mageCooldown'][1]['casting'] and after['mageCooldown'][1]['ratio']==0
 assert len(after['mageEvents'])==len(before['mageEvents']),'Random drag unexpectedly cast'
 drag('ManualSkill0',530,940,'lightning')
 cast=m.command('manual-lightning-after')['state']
 assert any(e['kind']=='begin' and e['skill']==0 for e in cast['mageEvents']),cast['mageEvents']
 m.tap('ManualSkill1',m.state('random-click-ready'))
 clicked=m.command('random-click-after')['state']
 assert any(e['kind']=='begin' and e['skill']==1 for e in clicked['mageEvents'])
 save('manual-report',{'randomDragRejected':True,'randomClickAccepted':True,'lightningGroundAccepted':True,'lightningEvents':cast['mageEvents']})
 print('Manual cast interactions passed',flush=True)

def menus():
 import settings_checks as settings
 report={}
 m.command('menu-midgame','midgame');time.sleep(2)
 m.tap('BtnKingdomArmy',m.state('menu-main'))
 m.tap('StatsBlock',m.state('menu-army'))
 before=m.command('stats-before')['state']
 m.command('stats-enhance','enhance',value=1);time.sleep(.4)
 after=m.command('stats-after')['state'];ui=m.state('stats-after-ui');m.shot('stats-linked')
 assert after['party'][0]['atk']>before['party'][0]['atk']
 report['stats']={'before':before['party'][0]['atk'],'after':after['party'][0]['atk'],'labels':[x['text'] for x in ui['labels'] if x['bounds']['y']>1200]}
 m.run('shell','input','tap','540','2025');time.sleep(.5)
 s=m.command('equipment-visible')['state'];m.shot('equipment-final')
 assert 0<s['equipmentCards']<=30,s['equipmentCards']
 report['equipmentCards']=s['equipmentCards']
 m.run('shell','input','tap','903','2025');time.sleep(.5);m.shot('promotion-final')
 m.back();m.tap('BtnHamburgerRight',m.state('guide-open-main'));m.tap('BtnMenuGuide',m.state('guide-open-menu'))
 m.shot('guide-final');ui=m.state('guide-before-claim')
 if any(c['name']=='Body' and c['interactable'] for c in ui['controls']):
  m.tap('Body',ui);m.shot('guide-reward-toast')
  report['guideToastCapture']='guide-reward-toast.png (inspect before the short toast expires)'
 m.back();m.tap('BtnHamburgerRight',m.state('bag-open-main'));m.tap('BtnMenuInventory',m.state('bag-open-menu'))
 m.shot('bag-final');top=m.state('bag-top');s=m.command('bag-count')['state'];assert 0<s['equipmentCards']<=30
 for i in range(4):m.run('shell','input','swipe','780','1750','780','800','240')
 time.sleep(.5);bottom=m.state('bag-scrolled');m.shot('bag-scrolled')
 assert [x['text'] for x in top['labels']]!=[x['text'] for x in bottom['labels']]
 report['bagCards']=s['equipmentCards'];report['scrollRebind']=True
 m.back();assert not m.state('menu-back')['panels']
 m.tap('BtnGacha',m.state('gacha-main'));ui=m.state('gacha-tabs')
 tabs=[c for c in ui['controls'] if c['name']=='Item_NavTabButton(Clone)' and c['interactable']]
 ui['controls']=[max(tabs,key=lambda x:x['bounds']['x'])];m.tap('Item_NavTabButton(Clone)',ui)
 ui=m.state('skill-pull-buttons');buttons=sorted([c for c in ui['controls'] if c['name']=='Item_GachaPullButton(Clone)' and c['interactable']],key=lambda x:x['bounds']['x'])
 ui['controls']=[buttons[1]];m.tap('Item_GachaPullButton(Clone)',ui);time.sleep(1.5)
 ui=m.state('skill-pull-result');m.shot('skill-pull-result')
 labels=[x for x in ui['labels'] if '파편' in x['text'] and '중복' in x['text']]
 assert labels and not any(x['isTextTruncated'] for x in labels),labels
 report['duplicateLabels']=[x['text'] for x in labels]
 m.tap('BtnDone',ui);m.back();save('menus-report',report);print(json.dumps(report,ensure_ascii=False),flush=True)

def menu_performance():
 import threading, shlex, statistics
 report=[]
 def measure(tag, control, ui):
  layers=m.run('shell','dumpsys','SurfaceFlinger','--list').stdout.decode().splitlines()
  layer=next(x for x in layers if x.startswith('SurfaceView['+m.PACKAGE) and '(BLAST)' in x)
  seen=set();raw=[]
  def sample():
   started=time.monotonic()
   while time.monotonic()-started<6:
    response=m.run('shell','dumpsys SurfaceFlinger --latency '+shlex.quote(layer)).stdout.decode();raw.append(response)
    for line in response.splitlines()[1:]:
     fields=line.split()
     if len(fields)==3 and 0<int(fields[1])<2**63-1:seen.add(int(fields[1]))
    time.sleep(.5)
  thread=threading.Thread(target=sample);thread.start();time.sleep(.2);m.tap(control,ui);thread.join()
  values=sorted(seen);deltas=sorted((b-a)/1e6 for a,b in zip(values,values[1:]));assert len(deltas)>120
  row=dict(menu=tag,samples=len(deltas),averageMs=statistics.mean(deltas),p95Ms=deltas[round((len(deltas)-1)*.95)],maxMs=max(deltas),over50ms=sum(d>50 for d in deltas))
  report.append(row);save(tag+'-presentation-raw',raw);save('menu-performance',report);print(row,flush=True)
 m.command('perf-crowded','crowded-fixture',value=500)
 m.tap('BtnKingdomArmy',m.state('perf-main'))
 ui=m.state('perf-army')
 tabs=[c for c in ui['controls'] if c['name']=='Item_NavTabButton(Clone)' and c['interactable'] and c['bounds']['y']>ui['height']*.75]
 # The three observed bottom tabs are Summary, Equipment, Promotion.
 ui['controls']=[sorted(tabs,key=lambda c:c['bounds']['x'])[1]]
 measure('equipment-open','Item_NavTabButton(Clone)',ui)
 m.back();m.tap('BtnHamburgerRight',m.state('perf-bag-main'))
 measure('bag-open','BtnMenuInventory',m.state('perf-bag-menu'));m.back()

def aim_guards():
 import threading
 report={}
 if m.state('guards-clean-main')['panels']:m.back()
 m.command('guards-fixture','mage-fixture',enhance=0,awaken=10,bloom=True)
 for slot,id in enumerate((0,1,4,5,7)):m.command('guards-equip-'+str(slot),'mage-equip',value=id,awaken=slot)
 m.command('guards-stage','stage',stage=0x20003000a);time.sleep(1)
 ui=m.state('guards-ui');tower=next(c['bounds'] for c in ui['controls'] if c['name']=='Tower')
 tx=str(round(tower['x']+tower['width']*.5));ty=str(round(tower['y']+tower['height']*.12))
 values=[]
 for n in range(2):
  m.run('shell','input','swipe',tx,ty,tx,ty,'800');time.sleep(.7)
  s=m.command('guards-long-press-'+str(n))['state'];values.append(s['manualAuto'])
 assert values[0]!=values[1],values
 if values[-1]:m.run('shell','input','swipe',tx,ty,tx,ty,'800');time.sleep(.7)
 report['longPressToggles']=values
 for n in range(60):
  ready=m.command('guards-ready-'+str(n))['state']
  if all(not c['casting'] and c['ratio']==0 for c in ready['mageCooldown']):break
  time.sleep(.25)
 else:raise AssertionError('Cooldowns did not finish after turning auto off')
 ui=m.state('guards-ready');b=next(c['bounds'] for c in ui['controls'] if c['name']=='ManualSkill2')
 sx=str(round(b['x']+b['width']/2));sy=str(round(b['y']+b['height']/2))
 before=m.command('guards-before')['state']
 m.run('shell','input','swipe',sx,sy,'980','145','1300')
 after=m.command('guards-ui-drop')['state'];assert after['mageCooldown'][2]['ratio']==0 and len(after['mageEvents'])==len(before['mageEvents'])
 report['toolbarDropRejected']=True
 rejected=m.command('guards-outside','mage-aim',value=2,x=9999,y=9999);assert not rejected['result']['accepted']
 report['outsideGroundRejected']=True
 worker=threading.Thread(target=m.run,args=('shell','input','swipe',sx,sy,'530','940','5500'));worker.start();time.sleep(2)
 preview=m.command('guards-held-preview')['state'];m.shot('guards-held-preview')
 report['preview']=preview.get('aim')
 m.run('shell','input','keyevent','KEYCODE_HOME');worker.join();time.sleep(1)
 m.run('shell','monkey','-p',m.PACKAGE,'1');time.sleep(3)
 resumed=m.command('guards-resume')['state'];assert resumed['mageCooldown'][2]['ratio']==0
 report['backgroundCanceledWithoutCooldown']=True
 # Inspect stone at its authored pillar peak, after the dust lead-in.
 m.command('stone-peak-config','mage-configure',value=5,enhance=0,awaken=0,bloom=False)
 m.command('stone-peak-stage','stage',stage=0x20003000a);time.sleep(2)
 r=m.command('stone-peak-cast','mage-cast',value=5,captureMs=850);assert r['result']['accepted'];time.sleep(1);m.shot('stone-peak');m.command('stone-peak-resume','pause',value=0)
 save('aim-guards-report',report);print(report,flush=True)

def aspects():
 import settings_checks as settings
 import struct
 results=[]
 originalSize=m.run('shell','wm','size').stdout.decode();originalDensity=m.run('shell','wm','density').stdout.decode()
 assert '1080x2316' in originalSize and '450' in originalDensity
 try:
  for tag,size in [('narrow','720x1600'),('tablet','900x1200')]:
   m.run('shell','wm','size',size);settings.launch(tag+'-entry')
   ui=m.state(tag+'-offline')
   if any(c['name']=='BtnConfirm' for c in ui['controls']):m.tap('BtnConfirm',ui)
   m.command(tag+'-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
   for slot,id in enumerate((0,1,4,5,7)):m.command(tag+'-equip-'+str(slot),'mage-equip',value=id,awaken=slot)
   m.command(tag+'-stage','stage',stage=0x20003000a);time.sleep(1)
   ui=m.state(tag+'-tray');m.shot(tag+'-tray')
   cards=[c for c in ui['controls'] if c['name'].startswith('ManualSkill')]
   assert len(cards)==5
   for c in cards:
    b=c['bounds'];assert b['x']>=0 and b['y']>=0 and b['x']+b['width']<=ui['width']+1 and b['y']+b['height']<=ui['height']+1,(tag,c)
    for goal in ui['goals']:
     if goal['visible']:
      g=goal['bounds'];overlap=b['x']<g['x']+g['width'] and b['x']+b['width']>g['x'] and b['y']<g['y']+g['height'] and b['y']+b['height']>g['y']
      assert not overlap,(tag,'Manual skill overlaps guide',c)
   for menu in ('Guide','Inventory'):
    m.tap('BtnHamburgerRight',m.state(tag+'-'+menu+'-main'));m.tap('BtnMenu'+menu,m.state(tag+'-'+menu+'-menu'))
    popup=m.state(tag+'-'+menu+'-popup');m.shot(tag+'-'+menu+'-popup')
    assert popup['panels'],popup
    close=[c for c in popup['controls'] if c['name']=='BtnPanelClose' and c['interactable']]
    assert close
    for c in close:
     b=c['bounds'];assert 0<=b['x']<ui['width'] and 0<=b['y']<ui['height']
    m.back();assert not m.state(tag+'-'+menu+'-back')['panels']
   m.command(tag+'-list','mage-list');ui=m.state(tag+'-list');m.shot(tag+'-list')
   cells=[c for c in ui['controls'] if c['name']=='Item_MageSkillCell(Clone)'];assert len(cells)==10
   first=min(cells,key=lambda c:(round(c['bounds']['y']),c['bounds']['x']));ui['controls']=[first];m.tap(first['name'],ui)
   detail=m.state(tag+'-detail');m.shot(tag+'-detail')
   bad=[x['text'] for x in detail['labels'] if x['isTextTruncated'] and x['name'] in ('Description','BloomDescription','BloomName','BtnBloom','Stats','Title')];assert not bad,bad
   m.back();m.back();assert not m.state(tag+'-back-main')['panels']
   results.append({'emulatedSize':size,'render':[ui['width'],ui['height']],'manualCards':5,'guideAndBagClosePassed':True,'skillDetailTruncated':bad});save('aspects-report',results)
   print('Passed '+tag,flush=True)
 finally:
  m.run('shell','wm','size','1080x2316');m.run('shell','wm','density','450')
  settings.launch('restored-display')

if __name__=='__main__':globals()[sys.argv[1]]()
