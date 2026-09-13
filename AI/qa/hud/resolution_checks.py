import sys,time,json
sys.path.insert(0,'AI/qa/hud')
from device_checks import *
def launch_main(prefix):
    run('shell','am','force-stop',PACKAGE)
    run('shell','monkey','-p',PACKAGE,'-c','android.intent.category.LAUNCHER','1')
    time.sleep(7)
    s=state(prefix+'-start')
    if not s['main']:
        tap('BtnLogin',s);s=state(prefix+'-login');tap('BtnLoginGuest',s)
        for i in range(12):
            time.sleep(1);s=state(prefix+f'-await-{i}')
            if s['main']: break
    if not s['main']:raise AssertionError('Main scene failed to load')
    state(prefix+'-pause','pause',1)
    time.sleep(1)
    return state(prefix+'-ready')
def settled(prefix):
    last=None
    for i in range(20):
        time.sleep(.4)
        s=state(prefix+f'-settle-{i}')
        rects=[g['bounds'] for g in s['goals'] if g['visible']]
        key=json.dumps(rects,sort_keys=True)
        if key==last and all(in_safe(b,s) for b in rects):return s
        last=key
    raise AssertionError('Layout did not settle inside safe area')
def in_safe(b,s):
    a=s['safeArea'];top=s['height']-a['y']-a['height']
    return b['x']>=a['x']-1 and b['y']>=top-1 and b['x']+b['width']<=a['x']+a['width']+1 and b['y']+b['height']<=s['height']-a['y']+1
if __name__=='__main__':
    original={k:run('shell','wm',k).stdout.decode().strip() for k in ['size','density']}
    (OUT/'original-display.json').write_text(json.dumps(original,indent=2))
    results=[]
    try:
        for name,size,density in [('narrow','720x1280','320'),('tall','1440x3088','560'),('tablet','1536x2048','320')]:
            run('shell','wm','size',size);run('shell','wm','density',density)
            s=launch_main(name);shot(name+'-hud')
            assert s['width']==int(size.split('x')[0]),s['width']
            assert in_safe(s['stage']['bounds'],s),'Stage outside safe area'
            assert all(in_safe(g['bounds'],s) for g in s['goals'] if g['visible']),'Goal outside safe area'
            tap('BtnHamburgerRight',s);s=state(name+'-menu');shot(name+'-menu')
            controls=[c for c in s['controls'] if c['name'].startswith('BtnMenu') or c['name']=='TglBossChain']
            assert all(in_safe(c['bounds'],s) for c in controls),'Menu outside safe area'
            assert all(c['bounds']['height']/s['dpi']*160>=44 for c in controls),'Menu touch target below 44dp'
            tap('BtnMenuGuide',s);time.sleep(1);s=settled(name+'-guide');shot(name+'-guide')
            assert s['panels'],'Guide not open'
            assert all(in_safe(g['bounds'],s) for g in s['goals'] if g['visible']),'Current quest outside safe area'
            truncated=[t['name'] for t in s['labels'] if t['isTextTruncated'] and t['name'] in ['Description','Step','Progress','MenuLabel']]
            assert not truncated,truncated
            back();s=state(name+'-return');assert not s['panels'],'Back did not close guide'
            results.append(dict(name=name,width=s['width'],height=s['height'],dpi=s['dpi'],passed=True))
            print(name,'passed',flush=True)
    finally:
        for kind,value in original.items():
            override=next((x.split(':',1)[1].strip() for x in value.splitlines() if x.startswith('Override')),None)
            run('shell','wm',kind,override or 'reset')
        (OUT/'resolution-results.json').write_text(json.dumps(results,indent=2))
