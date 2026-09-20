"""Real touch checks against the diagnostic account; never modifies a normal profile."""
import json,os,sys,time,re
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
sys.stdout.reconfigure(encoding='utf8')
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/FoundationRevision/EquipmentAfter')
m=SourceFileLoader('equipment_device',str(Path(__file__).with_name('mage-device.py'))).load_module()

def click(tag,name):
 m.command(tag+'-hold','pause',value=1)
 ui=m.state(tag+'-before');m.tap(name,ui)
 return m.state(tag+'-after')
def labels(ui):return [x['text'] for x in ui['labels']]
def total(s):return s['equipment']+s['pending']+s['reserve']

def install():
 assert Path('Recordings/FoundationRevision/Original/external.tar').exists()
 b=json.loads(Path(os.environ.get('EQUIPMENT_QA_BUILD','Recordings/FoundationRevision/BuildEquipment2')+'/build.json').read_text(encoding='utf-8-sig'))
 assert b['diagnostics'] and b['package']==m.PACKAGE
 print(m.run('install','-r','-t',b['apk']).stdout.decode(),flush=True)
 m.launch('equipment-launch')

def checks():
 r=m.command('equipment-acceptance','equipment-acceptance')['result'];assert r['passed']>=21
 print('Equipment transaction checks passed:',r['passed'],flush=True)
 r=m.command('balance-acceptance','acceptance')['result'];assert r['passed']>100
 print('Balance regression checks passed:',r['passed'],flush=True)

def menus():
 report={}
 m.command('equipment-crowded','crowded-fixture',value=500);time.sleep(3)
 before=m.command('full-before','pause',value=1)['state'];assert total(before)>=500 and before['runState']=='Running'
 r=m.command('full-gacha','gacha',value=0)
 assert r['result']['count']==10 and r['state']['Wallet']['AncientCoin']==before['Wallet']['AncientCoin']-500
 report['fullGacha']=r['result']
 m.command('full-dungeon-ready','dungeon-fixture')
 r=m.command('full-dungeon','dungeon',stage=0x210010001);assert r['result']['accepted'];time.sleep(2)
 m.shot('full-dungeon');m.command('full-return','return');time.sleep(2)
 m.tap('BtnHamburgerRight',m.state('bag-main'));m.tap('BtnMenuInventory',m.state('bag-menu'))
 ui=m.state('bag-open');m.shot('bag-open')
 for name in ['JobFilter','RarityFilter','Sort','AutoDismantle','BulkDismantle']:
  assert len([x for x in ui['controls'] if x['name']==name and x['interactable']])==1,name
  control=next(x for x in ui['controls'] if x['name']==name);assert control['bounds']['width']>85 and control['bounds']['height']>65,control
 m.command('bulk-cancel-before','pause',value=1)
 ui=click('bulk-preview','BulkDismantle');m.shot('bulk-preview')
 assert any('되돌릴 수' in x for x in labels(ui))
 before=m.command('bulk-dialog-count')['state'];m.back();after=m.command('bulk-back-count')['state'];assert total(before)==total(after)
 click('filter-normal','RarityFilter');ui=click('filter-rare','RarityFilter');assert '등급: 레어' in labels(ui)
 m.shot('filter-rare');ui=click('sort-attack','Sort');assert '정렬: 공격력' in labels(ui)
 ui=click('rare-preview','BulkDismantle');m.shot('rare-preview')
 text=next(x for x in labels(ui) if '되돌릴 수' in x)
 count=int(re.search(r'장비 ([\d,]+)개',text)[1].replace(',',''))
 stones=int(re.search(r'강화석 \+([\d,]+)',text)[1].replace(',',''))
 before=m.command('rare-confirm-before','pause',value=1)['state']
 click('rare-confirm','Confirm');after=m.command('rare-confirm-after')['state']
 assert total(before)-total(after)==count and after['Wallet']['EquipmentStone']-before['Wallet']['EquipmentStone']==stones,(count,stones,before,after)
 report['bulkRare']={'count':count,'stones':stones};m.shot('rare-empty')
 ui=click('auto-open','AutoDismantle');m.shot('auto-open')
 click('auto-normal','Rarity0');click('auto-save','Confirm');m.shot('auto-saved')
 m.back();m.command('ui-speed','timescale',value=100)
 m.tap('BtnCurrency',m.state('currency-main'));ui=m.state('currency-lines');m.shot('currency-lines')
 currency_names=['골드','전직 파편','비전지식','루비','강화석']
 found=[x for x in ui['labels'] if x['text'] in currency_names]
 assert len(found)==5 and not any(x['isTextTruncated'] for x in found),found
 assert all(x['bounds']['x']>20 and x['bounds']['x']+x['bounds']['width']<ui['width']-20 for x in found),found
 assert max(x['bounds']['x'] for x in found)-min(x['bounds']['x'] for x in found)<2,found
 report['currencyRows']=found
 m.back();m.command('equipment-speed-restore','timescale',value=100)
 (m.OUT/'menus-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
 print(json.dumps(report,ensure_ascii=False),flush=True)

if __name__=='__main__':
 {'install':install,'checks':checks,'menus':menus}[sys.argv[1]]()
