"""Same physical phone, Android display-size emulations, actual UI touches."""
import sys,json,time,struct
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings

def hud(tag):
 assert tag.replace('-','').isalnum()
 filename=f'files/hud-{tag}.json'
 m.run('shell','run-as',m.PACKAGE,'rm','-f',filename)
 payload=json.dumps({'id':tag,'action':'state','value':0}).encode()
 m.run('shell',f"run-as {m.PACKAGE} sh -c 'cat > files/hud-command.pending && mv files/hud-command.pending files/hud-command.json'",data=payload)
 started=time.monotonic()
 # Shell protocol reports file errors separately; exec-out produced intermittent
 # empty/truncated reads on this host/device during the first popup snapshots.
 for _ in range(150):
  r=m.run('shell','run-as',m.PACKAGE,'cat',filename,check=False)
  try:s=json.loads(r.stdout)
  except json.JSONDecodeError:time.sleep(.25);continue
  if 'error' in s:raise RuntimeError(s['error'])
  (m.OUT/f'{tag}.json').write_text(json.dumps(s,ensure_ascii=False),encoding='utf8')
  print(f'{tag}: {time.monotonic()-started:.1f}s',flush=True);return s
 raise TimeoutError(tag)
def choose(s,name,key):
 s=dict(s);s['controls']=[key([x for x in s['controls'] if x['name']==name and x['interactable']])];m.tap(name,s)
def swipe(s,x,y0,y1):
 png=m.run('exec-out','screencap','-p').stdout;w,h=struct.unpack('>II',png[16:24]);scale=min(w/s['width'],h/s['height'])
 px=round((w-s['width']*scale)/2+x*scale)
 ys=[round((h-s['height']*scale)/2+y*scale) for y in [y0,y1]]
 m.run('shell','input','swipe',str(px),str(ys[0]),str(px),str(ys[1]),'450');time.sleep(.6)

results=[]
originalSize=m.run('shell','wm','size').stdout.decode();originalDensity=m.run('shell','wm','density').stdout.decode()
assert 'Override size: 1080x2316' in originalSize and 'Override density: 450' in originalDensity,(originalSize,originalDensity)
try:
 for tag,size in [('narrow','720x1600'),('tablet','900x1200')]:
  print('Checking '+tag,flush=True)
  m.run('shell','wm','size',size);settings.launch(tag+'-launch')
  m.command(tag+'-auto-off','mage-auto',value=0)
  m.command(tag+'-list','mage-list');time.sleep(.8);s=hud(tag+'-roster');m.shot(tag+'-roster')
  cells=[x for x in s['controls'] if x['name']=='Item_MageSkillCell(Clone)'];assert len(cells)==10
  choose(s,'Item_MageSkillCell(Clone)',lambda rows:min(rows,key=lambda x:(round(x['bounds']['y']),x['bounds']['x'])))
  detail=hud(tag+'-detail');m.shot(tag+'-detail')
  bad=[x['text'] for x in detail['labels'] if x['isTextTruncated'] and x['name'] in ['Description','BloomDescription','BloomName','BtnBloom','Stats','Title']];assert not bad,bad
  assert any(x['name']=='BtnBloom' for x in detail['controls'])
  m.back();s=hud(tag+'-list-return')
  rows=[x['bounds'] for x in s['controls'] if x['name']=='Item_MageSkillCell(Clone)']
  # Scroll inside the observed card column to reveal the tenth skill.
  top=min(b['y'] for b in rows);bottom=min(s['height']*.77,max(b['y']+b['height'] for b in rows))
  swipe(s,s['width']*.7,bottom-20,top+40);s=hud(tag+'-roster-scroll');m.shot(tag+'-roster-scroll')
  m.back();s=hud(tag+'-main');m.tap('BtnGacha',s);s=hud(tag+'-gacha-tab-before')
  choose(s,'Item_NavTabButton(Clone)',lambda rows:max(rows,key=lambda x:x['bounds']['x']))
  s=hud(tag+'-gacha');m.shot(tag+'-gacha')
  before=m.command(tag+'-coins-before')['state']['Wallet']['AncientCoin']
  choose(s,'Item_GachaPullButton(Clone)',lambda rows:min(rows,key=lambda x:x['bounds']['x']))
  result=hud(tag+'-gacha-result');m.shot(tag+'-gacha-result')
  after=m.command(tag+'-coins-after')['state']['Wallet']['AncientCoin'];assert before-after==50
  bad=[x['text'] for x in result['labels'] if x['isTextTruncated'] and ('다시 뽑기' in x['text'] or x['name']=='Desc')];assert not bad,bad
  m.tap('BtnDone',result);m.back();s=hud(tag+'-returned');assert s['main']
  results.append({'emulatedSize':size,'render':[s['width'],s['height']],'rosterCount':len(cells),'detailAndResultTruncation':bad,'singlePullDebit':before-after,'backReturned':s['main']})
  print('Passed '+tag,flush=True)
finally:
 m.run('shell','wm','size','1080x2316');m.run('shell','wm','density','450')
 (m.OUT/'aspect-validation.json').write_text(json.dumps({'physicalDevices':1,'originalSize':originalSize,'originalDensity':originalDensity,'results':results},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(results,ensure_ascii=False),flush=True)
