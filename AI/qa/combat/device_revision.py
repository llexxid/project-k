"""Isolated Android combat revision acceptance. Never runs against the production package."""
import os,sys,json,time,runpy
from pathlib import Path
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Iteration3/Device')
m=runpy.run_path('AI/qa/mage/mage-device.py')
c=m['command'];run=m['run'];shot=m['shot'];state=m['state'];tap=m['tap'];back=m['back'];OUT=m['OUT'];PACKAGE=m['PACKAGE']
def click_rect(rect,snapshot):
 control={'name':'selected-test-control','interactable':True,'bounds':rect}
 tap(control['name'],dict(snapshot,controls=[control]))
def click_label(text,snapshot):
 labels=[x for x in snapshot['labels'] if x['text']==text]
 assert len(labels)==1,(text,len(labels))
 click_rect(labels[0]['bounds'],snapshot)
def ui():
 c('fixture-ui','mage-fixture',awaken=10,enhance=0,bloom=True)
 c('open-mage','mage-list');time.sleep(.5)
 s=state('mage-empty');shot('mage-empty')
 cells=sorted([x for x in s['controls'] if x['name']=='Item_MageSkillCell(Clone)'],key=lambda x:(round(x['bounds']['y']),x['bounds']['x']))
 click_rect(cells[0]['bounds'],s);s=state('mage-candidate');tap('Equip',s)
 assert c('equipped-one')['state']['slot'][0]==0
 s=state('mage-equipped');slots=sorted([x for x in s['controls'] if x['name']=='Item_MageEquipSlot(Clone)'],key=lambda x:x['bounds']['y'])
 click_rect(slots[1]['bounds'],s)
 s=state('mage-slot2');cells=sorted([x for x in s['controls'] if x['name']=='Item_MageSkillCell(Clone)'],key=lambda x:(round(x['bounds']['y']),x['bounds']['x']))
 click_rect(cells[1]['bounds'],s);tap('Equip',state('mage-candidate2'))
 assert c('equipped-two')['state']['slot'][:2]==[0,1]
 s=state('mage-swap-select');click_rect(cells[0]['bounds'],s);tap('Equip',state('mage-swap-confirm'))
 assert c('swapped')['state']['slot'][:2]==[1,0]
 shot('mage-swapped');tap('Unequip',state('mage-unequip'))
 assert c('unequipped')['state']['slot'][1]==-1
 back();assert not any(x['name']=='Equip' for x in state('mage-back')['controls'])
 c('mage-ui-reopen','mage-list');shot('mage-reopened');back()
 print('Mage touch equip / second slot / swap / unequip / back / reopen: passed',flush=True)
 # Ordinary menu navigation with actual touches.
 tap('BtnKingdomArmy',state('army-nav'));s=state('army-open');shot('army-open')
 print('ARMY_LABELS',[(x['name'],x['text']) for x in s['labels'] if x['text'] in ['전직','성장','편성'] or '전직' in x['text']],flush=True)
def combat():
 c('core-acceptance','combat-acceptance')
 c('orcs-fixture','combat-fixture',stage=0x20003000A,value=1,enhance=0)
 c('orcs-reset','combat-reset');time.sleep(20);r=c('orcs-20');shot('orcs-20')
 c('knight-hp','combat-hp',value=0);time.sleep(3);r=c('knight-shield');shot('knight-shield')
 c('goblins-fixture','combat-fixture',stage=0x20002000A,value=1,enhance=0)
 c('goblins-reset','combat-reset');time.sleep(20);c('goblins-20');shot('goblins-20')
 print('Combat samples saved',flush=True)
def spells():
 c('spell-fixture','mage-fixture',awaken=10,enhance=0,bloom=True)
 # Five independent slots prevent fixture setup from resetting live cooldowns.
 for ident,slot,delay in [(8,1,850),(0,2,1200),(9,3,600),(3,4,100),(4,0,350)]:
  c('spell-stage-'+str(ident),'stage',stage=0x20003000A)
  c('spell-equip-'+str(ident),'mage-equip',value=ident,awaken=slot)
  r=c('spell-start-'+str(ident),'mage-slot-cast',value=slot)
  if not r.get('result',{}).get('accepted'):print('Rejected cast',ident,flush=True);continue
  time.sleep(delay/1000);shot('spell-'+str(ident));c('spell-state-'+str(ident))
  time.sleep(2.7);shot('spell-late-'+str(ident))
 print('Spell samples saved',flush=True)
if __name__=='__main__':globals()[sys.argv[1]]()
