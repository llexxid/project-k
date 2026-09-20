"""Focused physical-device checks; the backup belongs to this revision only."""
import json, os, sys, time, subprocess
from importlib.machinery import SourceFileLoader
from pathlib import Path
sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/SpellAnimationRevision/Device')
m = SourceFileLoader('animation_device', str(Path(__file__).with_name('mage-device.py'))).load_module()
ROOT = Path('Recordings/SpellAnimationRevision')

def save(name, data):
    (m.OUT / (name + '.json')).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf8')

def ready(tag, skill, awaken=0, bloom=False):
    m.command(tag+'-resume', 'timescale', value=100)
    m.command(tag+'-close', 'mage-close')
    m.command(tag+'-fixture', 'mage-fixture', enhance=0, awaken=awaken, bloom=bloom)
    m.command(tag+'-stage', 'stage', stage=0x20003000a)
    time.sleep(2)
    m.command(tag+'-equip', 'mage-equip', value=skill, awaken=0)
    for i in range(100):
        s = m.command(tag+'-ready-'+str(i))['state']
        if not s['mageCooldown'][0]['casting'] and s['mageCooldown'][0]['ratio']==0: return
        time.sleep(.3)
    raise TimeoutError(tag)

def cast(tag, skill, phase=0, awaken=0, bloom=False, record=False):
    ready(tag, skill, awaken, bloom)
    recorder = None
    remote = '/sdcard/spell-animation-'+tag+'.mp4'
    if record:
        from device_checks import ADB
        recorder = subprocess.Popen([ADB, '-s', os.environ.get('HUD_QA_SERIAL','R3CN815LZ9L'),
            'shell', 'screenrecord', '--time-limit', '12', '--bit-rate', '8000000', remote],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        time.sleep(.5)
    m.command(tag+'-speed', 'timescale', value=25 if phase else 100)
    accepted = m.command(tag+'-cast', 'mage-cast', value=skill, captureMs=phase)
    assert accepted['result']['accepted'], accepted['result']
    active = None
    if phase:
        time.sleep(phase/250+.2)
        active = m.command(tag+'-active')['state']
        m.shot(tag)
        name = {0:'LightningBloomStrike' if bloom else 'Lightning', 2:'FireTornado', 8:'Meteor'}[skill]
        visuals = [v for v in active['mageVisuals'] if v['name'].startswith(name+'(')]
        assert visuals, (tag,active['mageVisuals'])
        for visual in visuals:
            for r in visual['renderers']:
                assert (r['layer']=='Default' and r['order']<2) if skill==2 else (r['layer']=='CombatVFX' and r['order']>=20),(tag,r)
                if skill==0 and not bloom: assert r['sprite'].startswith('LightningOriginal_'),r
        m.command(tag+'-run', 'timescale', value=100)
    time.sleep(7)
    final = m.command(tag+'-done')['state']
    events = [e for e in final['mageEvents'] if e['skill']==skill]
    start = next(e['time'] for e in events if e['kind']=='begin')
    damage = [e for e in events if e['kind']=='damage']
    bolts = [e for e in events if e['kind']=='bolt']
    intervals = [b['time']-a['time'] for a,b in zip(bolts,bolts[1:])]
    result = dict(tag=tag,skill=skill,awaken=awaken,bloom=bloom,active=active,events=events,
        strikes=len(bolts),intervals=intervals,firstDamage=damage[0]['time']-start if damage else None)
    save(tag+'-result',result)
    assert any(e['kind']=='end' for e in events),events
    assert damage and all(e['amount']>0 for e in damage),(tag,events)
    if skill==0 and not bloom:
        assert len(bolts)==3+awaken//4,(tag,bolts)
        assert all(abs(d-2/12)<.085 for d in intervals),(tag,intervals)
    if skill==0 and bloom: assert not bolts and all(e['bloom'] for e in damage),events
    if skill==8: assert 2.4<=result['firstDamage']<=2.8,result['firstDamage']
    if recorder:
        out,err = recorder.communicate(timeout=20)
        assert recorder.returncode==0,err
        m.run('pull',remote,str(m.OUT/(tag+'.mp4')))
        m.run('shell','rm','-f',remote)
    print(json.dumps({k:result[k] for k in ['tag','strikes','intervals','firstDamage']}),flush=True)
    return result

def checks():
    rows=[]
    for a in (0,4,8,10):
        rows.append(cast('lightning-a'+str(a),0,0,a,False,a==0))
        save('checks',rows)
    for args in [('lightning-original',0,220,0,False),('lightning-bloom',0,2030,10,True),
                 ('fire-crown',2,400,0,False),('meteor-flight',8,1200,0,False),
                 ('meteor-repeat',8,0,0,False)]:
        rows.append(cast(*args,record=args[0]=='meteor-repeat'))
        save('checks',rows)

def aspects():
    original=m.run('shell','wm','size').stdout.decode()
    rows=[]
    try:
        for name,size in [('narrow','720x1280'),('tall','1080x2400'),('tablet','1200x1600')]:
            m.run('shell','wm','size',size);m.launch('animation-'+name)
            for skill,phase in [(2,400),(8,1200)]:
                row=cast(name+'-spell-'+str(skill),skill,phase)
                rows.append(dict(aspect=name,size=size,skill=skill,active=row['active']['mageVisuals']))
                save('aspects',rows)
    finally:
        override=next((l.split(': ')[1].strip() for l in original.splitlines() if l.startswith('Override size:')),None)
        m.run('shell','wm','size',override or 'reset');m.launch('animation-aspect-restored')

def restore():
    # Reuse the preservation-only restorer, never the inventory-clearing foundation tool.
    p=SourceFileLoader('animation_restore',str(Path(__file__).with_name('combat_presentation.py'))).load_module()
    p.ROOT=ROOT
    p.restore()

def performance():
    import settings_checks as settings
    rows=[]
    for low in (False,True):
        ready('perf-'+str(low),8,4)
        for slot,skill in enumerate((0,2,8,4,9)):
            m.command('perf-slot-'+str(slot),'mage-equip',value=skill,awaken=slot)
        settings.settings_open('animation-performance-'+str(low))
        settings.toggle('LowSpec',low);m.back()
        m.command('perf-auto','mage-auto',value=1)
        time.sleep(3)
        rows.append(settings.measure('animation-performance-'+str(low),30))
        save('performance',rows)
    settings.settings_open('animation-performance-restore')
    settings.toggle('LowSpec',False);m.back()

def install():
    assert (ROOT/'Original/external.tar').exists() and (ROOT/'Original/internal.tar').exists()
    build=json.loads((ROOT/'DiagnosticBuild/build.json').read_text(encoding='utf-8-sig'))
    assert build['diagnostics'] and build['package']==m.PACKAGE
    print(m.run('install','-r','-t',build['apk']).stdout.decode(),flush=True)
    m.run('shell','input','keyevent','KEYCODE_WAKEUP');m.launch('animation-login')

if __name__=='__main__':
    {'checks':checks,'aspects':aspects,'performance':performance,'restore':restore,'install':install}[sys.argv[1]]()
