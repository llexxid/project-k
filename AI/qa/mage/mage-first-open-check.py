"""Separate visible first-open latency from the verbose HUD snapshot transport."""
import sys,json,time,threading
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings
settings.launch('first-open-launch')
s=m.state('first-open-main')
assert (s['width'],s['height'])==(1080,2316)
b=next(c['bounds'] for c in s['controls'] if c['name']=='Tower')
result={}
def measure():result['measurement']=settings.measure('first-open-frames',12)
t=threading.Thread(target=measure);t.start();time.sleep(.8)
start=time.monotonic()
# The lower part of Tower overlaps the party HUD; use its observed visible top quarter.
m.run('shell','input','tap',str(round(b['x']+b['width']/2)),str(round(b['y']+b['height']*.25)))
time.sleep(.3);m.shot('first-open-roster-early');result['earlyCaptureWallSeconds']=time.monotonic()-start
time.sleep(.6);m.shot('first-open-roster-settled')
t.join(35);assert not t.is_alive()
m.back()
(m.OUT/'first-open-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(result,ensure_ascii=False),flush=True)
