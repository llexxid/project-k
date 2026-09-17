"""Real Android input checks for the isolated .lobbyqa build."""
import json, os, subprocess, time
from pathlib import Path
ADB = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
PACKAGE = 'com.isolatedyouth.idlekingdomrpg.lobbyqa'
OUT = Path(os.environ.get('HUD_QA_OUTPUT','Recordings/HudRevision/Device'))
OUT.mkdir(parents=True, exist_ok=True)

def run(*args, check=True, data=None):
    return subprocess.run([ADB, '-s', os.environ.get('HUD_QA_SERIAL','R3CN815LZ9L'), *args], input=data, capture_output=True, check=check)

def state(name, action='state', value=0):
    if not name.replace('-','').replace('_','').isalnum(): raise ValueError('Use a simple QA result identifier.')
    run('shell','run-as',PACKAGE,'rm','-f',f'files/hud-{name}.json')
    payload=json.dumps(dict(id=name,action=action,value=value)).encode()
    run('shell',f"run-as {PACKAGE} sh -c 'cat > files/hud-command.pending && mv files/hud-command.pending files/hud-command.json'",data=payload)
    for _ in range(60):
        result=run('exec-out','run-as',PACKAGE,'cat',f'files/hud-{name}.json',check=False)
        if result.returncode==0:
            try:
                obj=json.loads(result.stdout)
                (OUT/f'{name}.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
                if 'error' in obj: raise RuntimeError(obj['error'])
                return obj
            except json.JSONDecodeError: pass
        time.sleep(.25)
    raise TimeoutError(name)

def tap(name,snapshot):
    controls=[c for c in snapshot['controls'] if c['name']==name and c['interactable']]
    if len(controls)!=1: raise ValueError(f'{name}: {len(controls)} matches')
    b=controls[0]['bounds']
    # Android may letterbox the Unity surface or scale its retained render size
    # after a display override. Map observed Unity coordinates to physical pixels.
    import struct
    png=run('exec-out','screencap','-p').stdout
    width,height=struct.unpack('>II',png[16:24])
    render_width=snapshot.get('width',width);render_height=snapshot.get('height',height)
    scale=min(width/render_width,height/render_height)
    x=(width-render_width*scale)/2+(b['x']+b['width']/2)*scale
    y=(height-render_height*scale)/2+(b['y']+b['height']/2)*scale
    run('shell','input','tap',str(round(x)),str(round(y)))
    time.sleep(1)

def shot(name):
    (OUT/f'{name}.png').write_bytes(run('exec-out','screencap','-p').stdout)

def back():
    run('shell','input','keyevent','4'); time.sleep(.5)

if __name__=='__main__':
    import sys
    if sys.argv[1]=='shot': shot(sys.argv[2])
    else:
        result=state(sys.argv[2],sys.argv[1],int(sys.argv[3]) if len(sys.argv)>3 else 0)
        print(json.dumps({k:v for k,v in result.items() if k!='labels'},ensure_ascii=False,indent=2))
