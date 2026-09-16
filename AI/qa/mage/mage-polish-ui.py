"""Real touches for bloom icons, retired menu removal, gacha and tablet scrolling."""
import sys,time,json
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings
report={}
try:
 settings.launch('polish-ui-launch')
 m.command('polish-ui-fixture','mage-fixture',enhance=10,awaken=10,bloom=False)
 m.command('polish-ui-list','mage-list');m.command('polish-ui-detail','mage-detail',value=0)
 s=m.state('polish-detail-off');m.shot('polish-detail-off');m.tap('BtnBloom',s)
 assert m.command('polish-bloom-on')['state']['mage']['0']['BloomEnabled']
 m.shot('polish-detail-on');m.back();m.shot('polish-roster-on');m.back()
 s=m.state('polish-menu-main')
 menu=next(c['name'] for c in s['controls'] if c['name'] in ['BtnMenu','BtnHamburger','BtnMenuToggle','BtnHamburgerRight'])
 m.tap(menu,s);s=m.state('polish-menu-open');m.shot('polish-menu')
 assert not any('Divine' in c['name'] for c in s['controls'])
 assert not any('신 스킬' in c['text'] for c in s['labels'])
 m.back();s=m.state('polish-main-gacha');m.tap('BtnGacha',s)
 s=m.state('polish-gacha-tabs');s['controls']=[max([c for c in s['controls'] if c['name']=='Item_NavTabButton(Clone)'],key=lambda c:c['bounds']['x'])];m.tap('Item_NavTabButton(Clone)',s)
 s=m.state('polish-gacha');m.shot('polish-gacha')
 before=m.command('polish-gacha-before')['state']['Wallet']['AncientCoin']
 s['controls']=[max([c for c in s['controls'] if c['name']=='Item_GachaPullButton(Clone)'],key=lambda c:c['bounds']['x'])];m.tap('Item_GachaPullButton(Clone)',s)
 s=m.state('polish-gacha-result');m.shot('polish-gacha-result')
 assert before-m.command('polish-gacha-after')['state']['Wallet']['AncientCoin']==500
 report['phone']={'bloomTouch':True,'retiredMenuAbsent':True,'tenPullDebit':500}
 # Transaction script consumes the open result popup and leaves the main screen.
 exec(compile(Path(__file__).with_name('mage-ui-transactions.py').read_text('utf8'),'<ui-transactions>','exec'))
 m.run('shell','wm','size','900x1200');settings.launch('polish-tablet-launch')
 m.command('polish-tablet-detail','mage-detail',value=1)
 for i in range(4):
  s=m.state('polish-tablet-scroll-'+str(i))
  b=next(c['bounds'] for c in s['controls'] if c['name']=='BtnBloom')
  if 0<b['y'] and b['y']+b['height']<s['height']*.92:break
  m.run('shell','input','swipe','650','990','650','380','500');time.sleep(.5)
 else:raise AssertionError('Tablet bloom toggle not reachable')
 m.tap('BtnBloom',s);assert m.command('polish-tablet-bloom')['state']['mage']['1']['BloomEnabled']
 m.shot('polish-tablet-bloom');report['tablet']={'render':[s['width'],s['height']],'scrolledToBloom':True,'touchPersisted':True}
finally:
 m.run('shell','wm','size','1080x2316');m.run('shell','wm','density','450')
 (m.OUT/'polish-ui-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(report,flush=True)
