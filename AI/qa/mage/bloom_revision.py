"""Physical Android checks for mage catalog 5; all fixtures use the diagnostic account."""
import hashlib, json, os, re, subprocess, sys, time
from importlib.machinery import SourceFileLoader
from pathlib import Path
sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/BloomRevision/Device')
m = SourceFileLoader('bloom_device', str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings
from device_checks import ADB
ROOT = Path('Recordings/BloomRevision')

def save(name, data):
    (m.OUT/(name+'.json')).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf8')

def ready(tag, skill, awaken=0, bloom=False):
    m.command(tag+'-resume', 'timescale', value=100)
    m.command(tag+'-close', 'mage-close')
    m.command(tag+'-fixture', 'mage-fixture', enhance=0, awaken=awaken, bloom=bloom)
    m.command(tag+'-stage', 'stage', stage=0x20003000a)
    time.sleep(2)
    m.command(tag+'-equip', 'mage-equip', value=skill, awaken=0)
    for i in range(80):
        state = m.command(tag+'-ready-'+str(i))['state']
        if not state['mageCooldown'][0]['casting'] and state['mageCooldown'][0]['ratio']==0:return
        time.sleep(.3)
    raise TimeoutError(tag)

def shake_setting(enabled):
    settings.settings_open('bloom-shake-setting')
    settings.toggle('ScreenShake', enabled)
    m.back()

def cast(tag, skill, awaken=0, bloom=False, phase=0, record=False, shake=True):
    ready(tag, skill, awaken, bloom)
    recorder=None; remote='/sdcard/bloom-revision-'+tag+'.mp4'
    if record:
        recorder=subprocess.Popen([ADB,'-s',os.environ.get('HUD_QA_SERIAL','R3CN815LZ9L'),'shell','screenrecord','--time-limit','10','--bit-rate','8000000',remote],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        time.sleep(.4)
    m.command(tag+'-speed','timescale',value=50 if phase else 100)
    result=m.command(tag+'-cast','mage-cast',value=skill,captureMs=phase)
    assert result['result']['accepted'],result['result']
    active=None
    if phase:
        time.sleep(phase/500+.15)
        active=m.command(tag+'-active')['state'];m.shot(tag)
        name='MeteorCrater' if skill==3 and bloom and phase>1600 else 'Meteor' if skill==3 and bloom else 'ArcaneVolley' if skill==3 else 'LightningBloomStrike' if bloom else 'Lightning'
        matches=[v for v in active['mageVisuals'] if v['name'].startswith(name+'(')]
        assert matches,(name,active['mageVisuals'])
        if name=='MeteorCrater':
            for r in matches[0]['renderers']:
                if r['name'] in ('MeteorGround','MeteorEmbers') or str(r['sprite']).startswith(('MeteorGround','MeteorEmbers')):
                    assert r['layer']=='Default' and r['order']<0,r
        m.command(tag+'-run','timescale',value=100)
    time.sleep(6)
    state=m.command(tag+'-done')['state'];events=state['mageEvents']
    begin=next(e['time'] for e in events if e['kind']=='begin')
    shakes=[e for e in events if e['kind']=='shake']
    bolts=[e for e in events if e['kind']=='bolt']
    damage=[e for e in events if e['kind']=='damage']
    expected=(1 if bloom else 3+awaken//4) if skill==0 else 1 if bloom and skill==3 else 0
    expected=expected if shake else 0
    assert len(shakes)==expected,(tag,len(shakes),expected)
    assert (state['peakShakeOffset']>0)==bool(expected),(tag,state['peakShakeOffset'])
    assert any(e['kind']=='end' for e in events),(tag,events)
    if skill==0 and not bloom:
        assert len(bolts)==3+awaken//4
        intervals=[b['time']-a['time'] for a,b in zip(bolts,bolts[1:])]
        assert all(abs(t-2/12)<.07 for t in intervals),intervals
    if skill==3 and not bloom:
        launches=[e for e in events if e['kind']=='star-launch']; impacts=[e for e in events if e['kind']=='star-impact']
        assert len(launches)==len(impacts)==18+awaken//4,(len(launches),len(impacts))
        assert all((a['x'],a['y'])==(b['x'],b['y']) for a,b in zip(launches,impacts))
    if skill==3 and bloom:
        contact=next(e['time'] for e in events if e['kind']=='meteor-contact')
        end=next(e['time'] for e in events if e['kind']=='meteor-ground-end')
        assert 1.5<=contact-begin<1.65,contact-begin
        assert 3.5<=end-contact<3.62,end-contact
        assert damage and 1.5<=damage[0]['time']-begin<1.85
    row=dict(tag=tag,skill=skill,awaken=awaken,bloom=bloom,shake=shake,shakeCount=len(shakes),peakShakeOffset=state['peakShakeOffset'],events=events,active=active)
    save(tag+'-result',row)
    if recorder:
        out,err=recorder.communicate(timeout=15);assert recorder.returncode==0,err
        m.run('pull',remote,str(m.OUT/(tag+'.mp4')));m.run('shell','rm','-f',remote)
    print(json.dumps(dict(tag=tag,shakes=len(shakes),peak=state['peakShakeOffset'],hits=len(damage))),flush=True)
    return row

def checks():
    acceptance=m.command('bloom-acceptance','mage-acceptance')['result'];assert acceptance['passed'],acceptance
    save('acceptance',acceptance)
    shake_setting(True);rows=[]
    for args in [('lightning-base',0,0,False,0,True),('lightning-a8',0,8,False),('thunder-bloom',0,10,True,0,True),('starfall-random',3,0,False,0,True),('meteor-bloom',3,10,True,0,True)]:
        rows.append(cast(*args));save('checks',rows)
    shake_setting(False)
    for args in [('lightning-muted',0,0,False),('thunder-muted',0,10,True),('meteor-muted',3,10,True)]:
        rows.append(cast(*args,shake=False));save('checks',rows)
    shake_setting(True)
    rows.append(cast('starfall-local-pulse',3,0,False,850));save('checks',rows)
    rows.append(cast('meteor-heated-ground',3,10,True,2300));save('checks',rows)

def ui():
    ready('bloom-ui',3,10,False)
    m.command('bloom-roster','mage-list');m.shot('bloom-roster')
    rows=[]
    for skill in (0,1,3,2,4,5,7,9):
        m.command('bloom-detail-'+str(skill),'mage-detail',value=skill)
        for enabled in (True,False):
            screen=m.state('detail-before-'+str(skill)+'-'+str(enabled))
            button=next(c['name'] for c in screen['controls'] if 'Bloom' in c['name'] and c['interactable'])
            m.tap(button,screen)
            state=m.command('detail-after-'+str(skill)+'-'+str(enabled))['state']
            assert state['mage'][str(skill)]['BloomEnabled']==enabled
            m.shot('detail-'+str(skill)+'-'+str(enabled))
            rows.append(dict(skill=skill,bloom=enabled))
        m.command('bloom-detail-close','mage-close');m.command('bloom-roster-open','mage-list')
    m.command('bloom-ui-close','mage-close');save('ui-checks',rows)

def aspects():
    original=m.run('shell','wm','size').stdout.decode();rows=[]
    try:
        for name,size in [('narrow','720x1280'),('tall','1080x2400'),('tablet','1200x1600')]:
            m.run('shell','wm','size',size);m.launch('bloom-'+name)
            for skill,bloom,phase in [(3,False,850),(3,True,2300)]:
                row=cast(name+'-'+str(bloom),skill,10 if bloom else 0,bloom,phase)
                rows.append(dict(aspect=name,size=size,skill=skill,bloom=bloom,active=row['active']['mageVisuals']))
                save('aspects',rows)
    finally:
        override=next((l.split(': ')[1].strip() for l in original.splitlines() if l.startswith('Override size:')),None)
        m.run('shell','wm','size',override or 'reset');m.launch('bloom-aspect-restored')

def performance():
    rows=[]
    for low in (False,True):
        ready('bloom-perf-'+str(low),3,10,True)
        for slot,skill in enumerate((0,1,3,4,9)):m.command('bloom-perf-slot-'+str(slot),'mage-equip',value=skill,awaken=slot)
        settings.settings_open('bloom-performance-'+str(low));settings.toggle('LowSpec',low);m.back()
        m.command('bloom-perf-auto','mage-auto',value=1);time.sleep(3)
        rows.append(settings.measure('bloom-performance-'+str(low),30));save('performance',rows)
    settings.settings_open('bloom-performance-restore');settings.toggle('LowSpec',False);m.back()

def drag():
    ready('drag-base',3,10,False)
    screen=m.state('drag-hud');b=next(c['bounds'] for c in screen['controls'] if c['name']=='ManualSkill0')
    points=[round(b['x']+b['width']/2),round(b['y']+b['height']/2),round(screen['width']*.5),round(screen['height']*.432)]
    m.run('shell','input','swipe',*map(str,points),'650');time.sleep(.5)
    base=m.command('drag-base-after')['state']
    assert base['mageCooldown'][0]['ratio']==0 and not base['mageCooldown'][0]['casting']
    m.command('drag-enable','mage-bloom',value=3,bloom=True);time.sleep(.6)
    m.run('shell','input','swipe',*map(str,points),'650');time.sleep(.2)
    bloom=m.command('drag-bloom-after')['state']
    assert bloom['mageCooldown'][0]['ratio']>0 or bloom['mageCooldown'][0]['casting']
    save('drag-checks',dict(baseDoesNotCast=True,bloomCasts=True,events=bloom['mageEvents']))
    m.shot('meteor-drag-cast')

def restore():
    p=SourceFileLoader('bloom_restore',str(Path(__file__).with_name('combat_presentation.py'))).load_module();p.ROOT=ROOT;p.restore()
    original=p.files(ROOT/'Original/external.tar');current=p.files(ROOT/'AfterDiagnostic/external.tar')
    generated=[n for n in current if n not in original and re.fullmatch(r'files/progression-local-v1/[a-f0-9]+\.json(\.bak)?',n)]
    # These files were created exclusively by this diagnostic session; the restorer
    # above already verified every original non-QA profile byte-for-byte.
    for name in generated:m.run('shell','run-as',m.PACKAGE,'rm','-f','/sdcard/Android/data/'+m.PACKAGE+'/'+name)
    save('generated-qa-cleanup',dict(removed=len(generated),backup='AfterDiagnostic/external.tar'))

def install():
    assert (ROOT/'Original/external.tar').exists() and (ROOT/'Original/internal.tar').exists()
    build=json.loads((ROOT/'Diagnostic/build.json').read_text(encoding='utf-8-sig'))
    assert build['diagnostics'] and build['package']==m.PACKAGE
    print(m.run('install','-r','-t',build['apk']).stdout.decode(),flush=True)
    m.run('shell','input','keyevent','KEYCODE_WAKEUP');m.launch('bloom-login')

def audit_final():
    import tarfile
    original=[]
    with tarfile.open(ROOT/'Original/external.tar') as archive:
        for file in archive.getmembers():
            if not file.name.endswith('.json') or 'progression-local-v1/' not in file.name:continue
            state=json.load(archive.extractfile(file))
            if state.get('AccountLevel')==200 and 8 in state.get('MageSlots',[]) and state.get('MageSkills',{}).get('3',{}).get('Awaken')==10:
                original.append((file.name,state))
    assert len(original)==1,'Select the original gameplay profile explicitly if its fixture changes.'
    name,before=original[0]
    raw=m.run('exec-out','run-as',m.PACKAGE,'cat','/sdcard/Android/data/'+m.PACKAGE+'/'+name).stdout
    after=json.loads(raw);(ROOT/'FinalPlay/user-after.json').write_bytes(raw)
    star,meteor=before['MageSkills']['3'],before['MageSkills']['8'];merged=after['MageSkills']['3']
    expectedFragments=star['Fragments']+meteor['Fragments']+min(star['Awaken'],meteor['Awaken'])*(min(star['Awaken'],meteor['Awaken'])+1)//2+30
    assert merged['Enhance']==max(star['Enhance'],meteor['Enhance']) and merged['Awaken']==max(star['Awaken'],meteor['Awaken'])
    assert merged['Fragments']>=expectedFragments and merged['BloomEnabled'] and '8' not in after['MageSkills']
    archive=json.loads(after['Modules']['mage-meteor-merged-v1'])
    assert archive['originalMeteor']==meteor and archive['originalStarfall']==star
    expectedSlots=[3 if value==8 else value for value in before['MageSlots']]
    assert after['MageSlots']==expectedSlots
    fields=('AttackLevel','HealthLevel','RubyGoldLevel','RubyExpLevel','ReincarnationLevel')
    assert all(before.get(k)==after.get(k) for k in fields)
    originalItems={x['Id']:x['Code'] for x in before['Equipment']};newItems={x['Id']:x['Code'] for x in after['Equipment']}
    assert all(newItems.get(k)==v for k,v in originalItems.items())
    assert after['MainStage']>=before['MainStage']
    package=m.run('shell','dumpsys','package',m.PACKAGE).stdout.decode(errors='replace')
    assert 'versionCode=58' in package and 'versionName=0.14.0' in package
    installed=m.run('shell','pm','list','packages','--user','0','com.isolatedyouth.idlekingdomrpg').stdout.decode()
    packages=[line for line in installed.splitlines() if 'com.isolatedyouth.idlekingdomrpg' in line]
    assert packages==['package:'+m.PACKAGE],packages
    files=m.run('shell','run-as',m.PACKAGE,'ls','files').stdout.decode().splitlines()
    probes=[n for n in files if re.match(r'(balance|hud|settings|lobby)-.*\.(json|pending)$',n)]
    assert not probes,probes
    pid=m.run('shell','pidof',m.PACKAGE).stdout.decode().strip();assert pid
    log=m.run('logcat','-d','--pid='+pid).stdout.decode(errors='replace')
    (ROOT/'FinalPlay/runtime.log').write_text(log,encoding='utf8')
    errors=[line for line in log.splitlines() if re.search(r'FATAL EXCEPTION|NullReferenceException|MissingReferenceException|Exception:|ANR in',line)]
    assert not errors,errors[:4]
    report=dict(passed=True,version='0.14.0',build=58,diagnostics=False,actualGuestLogin=True,
        originalEquipmentPreserved=len(originalItems),equipmentAfter=len(newItems),growthPreserved=list(fields),
        meteorMerged=True,slotsPreserved=True,bloomEnabled=merged['BloomEnabled'],fragmentsBefore=star['Fragments'],fragmentsAfter=merged['Fragments'],
        chapterBefore=(before['MainStage']>>16)&0xffff,chapterAfter=(after['MainStage']>>16)&0xffff,
        projectAppCount=len(packages),remainingProbeFiles=len(probes),runtimeErrors=errors)
    (ROOT/'FinalPlay/verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(report,ensure_ascii=False),flush=True)

if __name__=='__main__':
    {'install':install,'checks':checks,'ui':ui,'aspects':aspects,'performance':performance,'drag':drag,'restore':restore,'audit-final':audit_final}[sys.argv[1]]()
