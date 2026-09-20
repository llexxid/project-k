import sys,json,time
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
def wallet(tag):return m.command(tag)['state']['Wallet']['AncientCoin']
def point(s,name,right=False):
 candidates=[x for x in s['controls'] if x['name']==name and x['interactable']]
 c=max(candidates,key=lambda x:x['bounds']['x']) if right else candidates[0]
 assert s['width']==1080 and s['height']==2316, 'This rapid-input test uses the observed default render size.'
 b=c['bounds'];return round(b['x']+b['width']/2),round(b['y']+b['height']/2)
# Start with the real ten-pull result popup open.
s=m.state('repull-cancel-before');x,y=point(s,'BtnRePullN');before=wallet('repull-cancel-wallet-before')
m.run('shell',f'input tap {x} {y}; input keyevent 4');time.sleep(1)
after=wallet('repull-cancel-wallet-after');assert after==before,(before,after)
s=m.state('repull-cancel-after');assert not any(x['name']=='BtnDone' for x in s['controls']);m.shot('repull-cancelled')
# Both taps arrive during the first flare; exactly one transaction commits.
x,y=point(s,'Item_GachaPullButton(Clone)',right=True)
m.run('shell',f'input tap {x} {y}; input tap {x} {y}');time.sleep(1.2)
after2=wallet('rapid-pull-wallet-after');assert after-after2==500,(after,after2)
s=m.state('rapid-pull-result');m.shot('rapid-pull-result');assert any(x['name']=='BtnDone' for x in s['controls'])
truncated=[x['text'] for x in s['labels'] if x['isTextTruncated'] and ('다시 뽑기' in x['text'] or x['name']=='Desc')];assert not truncated,truncated
# Re-pull remains usable after the earlier cancellation.
m.tap('BtnRePull1',s);after3=wallet('repull-one-wallet-after');assert after2-after3==50,(after2,after3)
s=m.state('repull-one-result');m.shot('repull-one-result');m.tap('BtnDone',s);m.back()
result={'cancelDebit':before-after,'rapidDoubleTapDebit':after-after2,'repullDebit':after2-after3,'buttonTruncation':truncated}
(m.OUT/'ui-transactions.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(result)
