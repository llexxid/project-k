"""One connected phone, three emulated display/density pairs; always restore original overrides."""
import json,re,time
from settings_checks import *

def check_labels(s):
 for label in s['labels']:
  assert not label['isTextTruncated'],label
  assert label['height']<=label['logicalHeight']+2,label
  assert label['width']<=label['logicalWidth']+2,label

def override(kind):
 raw=run('shell','wm',kind).stdout.decode()
 m=re.search(r'Override '+('size' if kind=='size' else 'density')+r':\s*(\S+)',raw)
 return m.group(1) if m else 'reset',raw

def main():
 size,raw_size=override('size');density,raw_density=override('density')
 (OUT/'original-display.json').write_text(json.dumps(dict(size=size,density=density,rawSize=raw_size,rawDensity=raw_density),indent=2),encoding='utf-8')
 results=[]
 try:
  for name,dimensions,dpi in [('narrow','720x1280','320'),('tall','720x1600','320'),('tablet','1200x1600','240')]:
   run('shell','wm','size',dimensions);run('shell','wm','density',dpi)
   launch(name+'-title',False);shot(name+'-title')
   tap('Settings',state(name+'-title-controls'))
   title_settings=command(name+'-title-settings')['state'];assert title_settings['settingsOpen'];check_labels(title_settings)
   back()
   launch(name);pause(True,name+'-pause')
   s=settings_open(name+'-top');shot(name+'-top')
   width,height=map(int,dimensions.split('x'));assert (s['width'],s['height'])==(width,height),(name,s['width'],s['height'])
   b=s['panel'];assert b['x']>=0 and b['y']>=0 and b['x']+b['width']<=width+1 and b['y']+b['height']<=height+1
   check_labels(s)
   for i in range(3):
    s=style(i,name+'-format-'+str(i));assert s['settingsOpen'];check_labels(s);shot(name+'-format-'+str(i))
   s,x=reveal('KeepAwake');shot(name+'-bottom')
   for control in s['toggles']:
    assert control['bounds']['width']>0 and control['bounds']['height']>0
   toggle('KeepAwake',False,name+'-keep-off')
   # Full row touch, away from switch, using observed label bounds inside the viewport.
   s=command(name+'-row-state')['state'];label=next(t for t in s['labels'] if t['text']=='화면 켜짐 유지');r=label['bounds'];v=s['viewport']
   assert v['y']<=r['y'] and r['y']+r['height']<=v['y']+v['height']
   run('shell','input','tap',str(round(r['x']+r['width']*.3)),str(round(r['y']+r['height']/2)));time.sleep(.4)
   assert command(name+'-row-click')['state']['keepAwake']
   back();assert not command(name+'-back')['state']['settingsOpen']
   results.append(dict(name=name,width=width,height=height,density=dpi,passed=True));print('PASS '+name,flush=True)
 finally:
  run('shell','wm','size',size);run('shell','wm','density',density)
  launch('restored-display');pause(True,'restored-pause')
  (OUT/'aspect-results.json').write_text(json.dumps(dict(singlePhysicalDevice=True,results=results,restoredSize=size,restoredDensity=density),indent=2),encoding='utf-8')

if __name__=='__main__':main()
