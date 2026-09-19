"""Mage UI/VFX device checks. Run after backing up the isolated package's saves."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from importlib.machinery import SourceFileLoader

sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf8')
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/MageRevision/Device')
m = SourceFileLoader('mage_device', str(Path(__file__).with_name('mage-device.py'))).load_module()

def save(name, value):
    (m.OUT / (name+'.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')

def fixture():
    m.command('revision-close','mage-close')
    m.command('revision-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
    for slot, skill in enumerate((0,1,5,7,8)):
        m.command('revision-equip-'+str(slot),'mage-equip',value=skill,awaken=slot)
    m.command('revision-stage','stage',stage=0x20003000a)
    time.sleep(2)
    m.command('revision-auto-off','mage-auto',value=0)
    time.sleep(.6)

def manual():
    fixture()
    ui=m.state('revision-ready')
    controls=[c for c in ui['controls'] if c['name'].startswith('ManualSkill')]
    assert len(controls)==5,controls
    bounds=[c['bounds'] for c in controls]
    width=ui['width'];height=ui['height']
    assert all(b['x']>width*.65 and b['x']+b['width']<=width and b['y']>=0 and b['y']+b['height']<height*.86 for b in bounds),bounds
    assert max(b['x'] for b in bounds)-min(b['x'] for b in bounds)<2,bounds
    m.shot('manual-ready')
    m.tap('ManualSkill0',ui)
    cast=m.command('revision-clicked')['state']
    assert any(e['kind']=='begin' and e['skill']==0 for e in cast['mageEvents'])
    m.shot('manual-cooldown')
    m.command('revision-auto-on','mage-auto',value=1)
    time.sleep(.65)
    hidden=m.state('revision-hidden')
    assert not any(c['name'].startswith('ManualSkill') and c['interactable'] for c in hidden['controls'])
    m.shot('manual-hidden')
    m.command('revision-auto-off-again','mage-auto',value=0)
    time.sleep(.6)
    shown=m.state('revision-shown')
    assert len([c for c in shown['controls'] if c['name'].startswith('ManualSkill')])==5
    m.command('revision-detail','mage-detail',value=0)
    time.sleep(.5)
    m.shot('lightning-detail')
    detail=m.state('revision-detail-ui')
    labels='\n'.join(t['text'] for t in detail['labels'])
    assert '4각성' in labels and '8각성' in labels and '다음 1각성' in labels,labels
    m.command('revision-close-again','mage-close')
    save('manual-checks',{'aligned':True,'bounds':bounds,'tapCast':True,'autoHide':True,'reshow':True,'awakeningDetail':True})
    print('Manual checks passed',flush=True)

def spells(ids=(0,1,2,3,4,5,7,8,9)):
    report=[]
    m.command('spells-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
    for skill in ids:
        tag='revision-spell-'+str(skill)
        m.command(tag+'-stage','stage',stage=0x20003000a)
        time.sleep(2)
        m.command(tag+'-equip','mage-equip',value=skill,awaken=0)
        for retry in range(50):
            state=m.command(tag+'-ready-'+str(retry))['state']
            if state['mageCooldown'][0]['ratio']==0 and not state['mageCooldown'][0]['casting']:break
            time.sleep(.4)
        else: raise TimeoutError(tag)
        if skill==7:m.command(tag+'-injury','combat-hp',value=0)
        result=m.command(tag+'-cast','mage-cast',value=skill,captureMs=600 if skill==8 else 350)
        assert result['result']['accepted'],tag
        time.sleep(.8)
        m.shot(tag)
        m.command(tag+'-resume','pause',value=0)
        if skill==8:
            time.sleep(.6);m.shot(tag+'-crater')
        time.sleep(7)
        state=m.command(tag+'-result')['state']
        events=state['mageEvents']
        assert any(e['kind']=='end' for e in events),(tag,events)
        assert any(e['kind']==('heal' if skill==7 else 'damage') and e['amount']>0 for e in events),(tag,events)
        report.append({'id':skill,'events':events})
        save('spell-checks',report)
        print(tag+' passed',flush=True)

def drag(slot, name):
    ui=m.state(name+'-before')
    b=next(c['bounds'] for c in ui['controls'] if c['name']=='ManualSkill'+str(slot))
    import struct
    width,height=struct.unpack('>II',m.run('exec-out','screencap','-p').stdout[16:24])
    scale=min(width/ui['width'],height/ui['height'])
    x=(width-ui['width']*scale)/2+(b['x']+b['width']/2)*scale
    y=(height-ui['height']*scale)/2+(b['y']+b['height']/2)*scale
    from device_checks import ADB
    process=subprocess.Popen([ADB,'-s',os.environ.get('HUD_QA_SERIAL','R3CN815LZ9L'),'shell','input','swipe',str(round(x)),str(round(y)),str(width//2),str(round(height*.40)),'3000'],creationflags=subprocess.CREATE_NO_WINDOW)
    time.sleep(1.6)
    m.shot(name)
    state=m.command(name+'-held')['state']
    process.wait(timeout=8)
    time.sleep(.3)
    return state,m.command(name+'-after')['state']

def aiming():
    fixture()
    time.sleep(12)
    before=m.command('aim-cooldown-before')['state']
    held,after=drag(1,'ice-drag-rejected')
    assert held['aim'] is None and after['mageCooldown'][1]['ratio']==0
    held,after=drag(0,'lightning-ground-aim')
    assert held['aim'] is not None and held['aim']['valid'] and after['mageCooldown'][0]['ratio']>0
    save('aim-checks',{'randomDragRejected':True,'aimVisible':True,'aimValid':True,'releaseCasts':True,'held':held})
    print('Aim checks passed',flush=True)

def aspects():
    from settings_checks import launch
    original=m.run('shell','wm','size').stdout.decode()
    results=[]
    try:
        for name,size in [('narrow','720x1280'),('tall','1080x2400'),('tablet','1200x1600')]:
            m.run('shell','wm','size',size)
            launch('aspect-'+name)
            fixture()
            ui=m.state('aspect-'+name+'-hud')
            controls=[c for c in ui['controls'] if c['name'].startswith('ManualSkill')]
            assert len(controls)==5
            boxes=[c['bounds'] for c in controls]
            for b in boxes:
                assert b['x']>=0 and b['x']+b['width']<=ui['width']+1 and b['y']>0 and b['y']+b['height']<ui['height']*.88,(name,b)
            for i,a in enumerate(boxes):
                for b in boxes[i+1:]:
                    assert a['x']+a['width']<=b['x'] or b['x']+b['width']<=a['x'] or a['y']+a['height']<=b['y'] or b['y']+b['height']<=a['y'],(name,a,b)
            m.shot('aspect-'+name)
            m.command('aspect-'+name+'-detail','mage-detail',value=1)
            time.sleep(.5)
            detail=m.state('aspect-'+name+'-detail-hud')
            assert not any(t['isTextTruncated'] for t in detail['labels'] if t['name'] in ('AwakeningEffects','NextAwakening'))
            m.shot('aspect-'+name+'-detail')
            m.run('shell','input','keyevent','4');time.sleep(.5)
            m.command('aspect-'+name+'-close','mage-close')
            results.append({'name':name,'override':size,'render':[ui['width'],ui['height']],'safeArea':ui['safeArea'],'bounds':boxes})
            save('aspect-checks',results)
            print(name+' passed',flush=True)
    finally:
        override=next((line.split(': ')[1].strip() for line in original.splitlines() if line.startswith('Override size:')),None)
        m.run('shell','wm','size',override or 'reset')
        launch('aspect-restored')

def sustain():
    fixture()
    m.command('sustain-equip','mage-equip',value=7,awaken=0)
    time.sleep(19)
    m.command('sustain-injury','combat-hp',value=0)
    result=m.command('sustain-cast','mage-cast',value=7,captureMs=700)
    assert result['result']['accepted']
    time.sleep(1)
    a=m.command('sustain-rise-complete')['state']
    m.shot('sanctuary-hold-start')
    m.command('sustain-resume','pause',value=0);time.sleep(1.2)
    b=m.command('sustain-hold','pause',value=1)['state']
    m.shot('sanctuary-hold-middle')
    def crest(s):return next(v for v in s['mageVisuals'] if v['name'].startswith('Sanctuary('))['renderers'][2]
    ca,cb=crest(a),crest(b)
    assert ca['sprite']==cb['sprite'] and ca['localPosition']==cb['localPosition'],(ca,cb)
    m.command('sustain-end-resume','pause',value=0);time.sleep(4)
    end=m.command('sustain-end')['state']
    assert not any(v['name'].startswith('Sanctuary(') for v in end['mageVisuals'])
    save('sustain-checks',{'crestA':ca,'crestB':cb,'held':True,'released':True,'events':end['mageEvents']})
    print('Sanctuary hold and release passed',flush=True)

def performance():
    import settings_checks as s
    results=[]
    for low in (False,True):
        fixture()
        s.settings_open('perf-open-'+str(low));s.toggle('LowSpec',low)
        m.back();time.sleep(.7)
        m.command('perf-auto-'+str(low),'mage-auto',value=1)
        time.sleep(3)
        results.append(s.measure('perf-'+('low' if low else 'normal'),25))
        save('performance-checks',results)
        print('Measured lowSpec='+str(low),flush=True)
    s.settings_open('perf-restore');s.toggle('LowSpec',False);m.back()

if __name__=='__main__':
    action=sys.argv[1]
    if action=='install':
        assert Path('Recordings/MageRevision/Original/external.tar').exists()
        build=json.loads(Path(sys.argv[2] if len(sys.argv)>2 else 'Recordings/MageRevision/Diagnostic/build.json').read_text(encoding='utf-8-sig'))
        assert build['diagnostics'] and build['package']==m.PACKAGE
        print(m.run('install','-r','-t',build['apk']).stdout.decode(),flush=True)
        from settings_checks import launch
        launch('revision-login')
        print('Diagnostic login passed',flush=True)
    elif action=='manual':manual()
    elif action=='spells':spells()
    elif action=='fixture':fixture()
    elif action=='aim':aiming()
    elif action=='aspects':aspects()
    elif action=='revised-spells':spells((7,8,9))
    elif action=='sustain':sustain()
    elif action=='performance':performance()
