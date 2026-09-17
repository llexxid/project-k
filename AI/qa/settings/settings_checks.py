"""Settings acceptance on the isolated development APK, driven with real adb touches.
Use HUD_QA_OUTPUT to choose a recording directory. Never targets the user's production package.
"""
import json,sys,time,os
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'hud'))
from device_checks import run,state,tap,shot,back,OUT,PACKAGE

def command(name,action='state',value=''):
 if not name.replace('-','').replace('_','').isalnum(): raise ValueError(name)
 filename=f'files/settings-{name}.json'
 run('shell','run-as',PACKAGE,'rm','-f',filename)
 payload=json.dumps(dict(id=name,action=action,value=str(value))).encode()
 run('shell',f"run-as {PACKAGE} sh -c 'cat > files/settings-command.pending && mv files/settings-command.pending files/settings-command.json'",data=payload)
 for _ in range(120):
  r=run('exec-out','run-as',PACKAGE,'cat',filename,check=False)
  if r.returncode==0 and r.stdout.strip():
   try:obj=json.loads(r.stdout)
   except json.JSONDecodeError:time.sleep(.1);continue
   (OUT/f'{name}.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
   if 'error' in obj:raise RuntimeError(obj['error'])
   return obj
  time.sleep(.1)
 raise TimeoutError(name)

def settings_open(name):
 command(name+'-open','open');time.sleep(.5)
 return command(name)['state']

def find_visible(name,snapshot,kind='toggles'):
 v=snapshot['viewport']
 matches=[x for x in snapshot[kind] if x['name']==name]
 if len(matches)!=1:raise AssertionError((name,matches))
 x=matches[0];b=x['bounds']
 if b['y']<v['y'] or b['y']+b['height']>v['y']+v['height']:return None
 return x

def reveal(name,kind='toggles'):
 for i in range(9):
  s=command('reveal-'+name+'-'+str(i))['state']
  if not any(x['name']==name for x in s[kind]):
   tab=2 if name in ['PowerSave','LowSpec','KeepAwake'] else 1 if kind=='sliders' else 0
   ui=state('reveal-tab-'+name+'-'+str(i))
   if any(x['name']=='Tab'+str(tab) for x in ui['controls']):
    tap('Tab'+str(tab),ui);time.sleep(.3);continue
  x=find_visible(name,s,kind)
  if x:return s,x
  v=s['viewport'];b=next(x for x in s[kind] if x['name']==name)['bounds']
  middle=v['y']+v['height']/2
  delta=max(-v['height']*.62,min(v['height']*.62,b['y']+b['height']/2-middle))
  sx=v['x']+v['width']*.10
  y0=middle+delta/2;y1=middle-delta/2
  run('shell','input','swipe',str(round(sx)),str(round(y0)),str(round(sx)),str(round(y1)),'450');time.sleep(.5)
 raise AssertionError('Could not reveal '+name)

def toggle(name,on,tag=None):
 s,x=reveal(name)
 if x['isOn']!=on:
  b=x['bounds'];run('shell','input','tap',str(round(b['x']+b['width']/2)),str(round(b['y']+b['height']/2)));time.sleep(.3)
 s=command(tag or name+'-'+str(on))['state']
 assert next(t for t in s['toggles'] if t['name']==name)['isOn']==on,(name,on,s)
 return s

def slider(name,value,tag=None):
 s,x=reveal(name,'sliders');b=x['bounds']
 # Actual track endpoints, not a direct call to Slider.value.
 run('shell','input','tap',str(round(b['x']+b['width']*value)),str(round(b['y']+b['height']/2)));time.sleep(.3)
 s=command(tag or name+'-'+str(round(value*100)))['state']
 actual=next(v for v in s['sliders'] if v['name']==name)['value']
 assert abs(actual-value)<.02,(name,value,actual)
 return s

def style(index,tag):
 settings_open(tag+'-settings')
 s=state(tag+'-ui');tap('NumberStyle'+str(index),s)
 out=command(tag)['state'];assert out['style']==['Standard','Korean','Scientific'][index]
 return out

def launch(tag='launch',login=True):
 run('shell','am','force-stop',PACKAGE);run('shell','monkey','-p',PACKAGE,'-c','android.intent.category.LAUNCHER','1');time.sleep(7)
 if login:
  s=state(tag+'-screen')
  if not s['main']:
   if not any(c['name']=='BtnLoginGuest' and c['interactable'] for c in s['controls']):
    tap('BtnLogin',s);s=state(tag+'-login')
   tap('BtnLoginGuest',s);time.sleep(7)
  s=state(tag+'-main')
  assert s['main'], 'QA entry must reach the combat screen before stage commands.'
  if any(c['name']=='BtnConfirm' for c in s['controls']):tap('BtnConfirm',s)
 return command(tag)['state']

def pause(on=True,tag='pause'):
 filename=f'files/balance-{tag}.json'
 run('shell','run-as',PACKAGE,'rm','-f',filename)
 payload=json.dumps(dict(id=tag,action='pause',value=1 if on else 0)).encode()
 run('shell',f"run-as {PACKAGE} sh -c 'cat > files/balance-command.pending && mv files/balance-command.pending files/balance-command.json'",data=payload)
 for _ in range(80):
  r=run('exec-out','run-as',PACKAGE,'cat',filename,check=False)
  if r.returncode==0 and r.stdout.strip():
   try:obj=json.loads(r.stdout)
   except json.JSONDecodeError:time.sleep(.1);continue
   if 'error' in obj:raise RuntimeError(obj['error'])
   return obj
  time.sleep(.1)
 raise TimeoutError(tag)

def measure(name,seconds=30):
 filename=f'files/lobby-{name}.json'
 run('shell','run-as',PACKAGE,'rm','-f',filename)
 payload=json.dumps(dict(id=name,action='measure',seconds=seconds)).encode()
 run('shell',f"run-as {PACKAGE} sh -c 'cat > files/lobby-command.pending && mv files/lobby-command.pending files/lobby-command.json'",data=payload)
 print('Measuring '+name,flush=True)
 for _ in range(int((seconds+20)*4)):
  r=run('exec-out','run-as',PACKAGE,'cat',filename,check=False)
  if r.returncode==0 and r.stdout.strip():
   try:obj=json.loads(r.stdout)
   except json.JSONDecodeError:time.sleep(.1);continue
   (OUT/f'{name}.json').write_text(json.dumps(obj,indent=2),encoding='utf-8');return obj
  time.sleep(.25)
 raise TimeoutError(name)

if __name__=='__main__':
 if sys.argv[1]=='launch': print(json.dumps(launch(),ensure_ascii=True))
 else:print(json.dumps(command(sys.argv[2],sys.argv[1],sys.argv[3] if len(sys.argv)>3 else ''),ensure_ascii=True))
