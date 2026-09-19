"""Layer/status device QA for the isolated package, with current-user save preservation."""
import os,sys,json,time,hashlib,tarfile,re
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
sys.stdout.reconfigure(encoding='utf8')
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatPresentation/Device')
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
ROOT=Path('Recordings/CombatPresentation')

def save(name,data):
    (m.OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')

def fixture():
    m.command('presentation-close','mage-close')
    m.command('presentation-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
    m.command('presentation-stage','stage',stage=0x20003000a)
    time.sleep(2)

def checks():
    fixture()
    report=m.command('presentation-acceptance','presentation-acceptance')
    assert report['result']['passed'],report['result']
    save('status-checks',report['result'])
    for kind in range(4):
        fixture()
        m.command('status-'+str(kind),'status-fixture',value=kind,captureMs=250)
        time.sleep(.6)
        state=m.command('status-visible-'+str(kind))['state']
        assert state['statusVisuals'],state
        m.shot('status-'+str(kind))
        save('status-layout-'+str(kind),state['statusVisuals'])
        m.command('status-resume-'+str(kind),'pause',value=0)
    print('Status application, expiry, movement, death and pool checks passed',flush=True)

def spells():
    rows=[]
    for skill in (0,1,2,3,4,5,7,8,9):
        fixture()
        tag='layers-skill-'+str(skill)
        m.command(tag+'-equip','mage-equip',value=skill,awaken=0)
        for retry in range(100):
            s=m.command(tag+'-ready-'+str(retry))['state']
            if s['mageCooldown'][0]['ratio']==0 and not s['mageCooldown'][0]['casting']:break
            time.sleep(.25)
        if skill==7:m.command(tag+'-injury','combat-hp',value=0)
        result=m.command(tag+'-cast','mage-cast',value=skill,captureMs=1000 if skill==8 else 650 if skill==5 else 420)
        assert result['result']['accepted']
        time.sleep(1.2 if skill==8 else .9 if skill==5 else .65)
        state=m.command(tag+'-visible')['state'];m.shot(tag)
        visuals=state['mageVisuals']
        for v in visuals:
            for r in v['renderers']:
                ground=v['name'].split('(')[0] in ('GroundTelegraph','SanctuaryHeal','Sanctuary','VenomMist','VoidRift','FireTornado','IceBloomWarning') or (v['name'].startswith('MeteorCrater') and r['name']=='Layer0')
                assert (r['layer']=='Default' and r['order']<2) if ground else (r['layer']=='CombatVFX' and r['order']>=20),(skill,v,r)
        rows.append(dict(skill=skill,renderers=visuals,status=state['statusVisuals']))
        m.command(tag+'-resume','pause',value=0)
        if skill==8:
            time.sleep(.3); state=m.command(tag+'-impact','pause',value=1)['state'];m.shot(tag+'-crater')
            rows.append(dict(skill=skill,phase='crater',renderers=state['mageVisuals']))
            m.command(tag+'-resume2','pause',value=0)
        time.sleep(6)
        events=m.command(tag+'-ended')['state']['mageEvents']
        assert any(e['kind']=='end' for e in events)
        assert any(e['kind']==('heal' if skill==7 else 'damage') and e['amount']>0 for e in events)
        save('spell-layer-checks',rows)
        print(tag+' passed',flush=True)

def files(path):
    with tarfile.open(path) as a:return {i.name:a.extractfile(i).read() for i in a.getmembers() if i.isfile()}

def control():
    results=[]
    for boss in (False,True):
        fixture()
        m.command('control-stage-'+str(boss),'stage',stage=0x20003000b if boss else 0x20003000a)
        time.sleep(2)
        result=m.command('control-rules-'+str(boss),'combat-control')['result']
        assert result['boss']==boss and result['passed']==10,result
        results.append(result)
    save('control-checks',results)
    print('Ordinary/boss taunt, shield, stun and pool checks passed',flush=True)

def healing():
    results=[]
    for elite,index in [(0,0),(0,1),(0,2),(1,0),(1,2)]:
        fixture()
        tag='healing-'+str(elite)+'-'+str(index)
        m.command(tag+'-jobs','combat-fixture',value=elite,stage=0x20003000a)
        m.command(tag+'-equip','mage-equip',value=7,awaken=0)
        for retry in range(100):
            state=m.command(tag+'-ready-'+str(retry))['state']
            if state['mageCooldown'][0]['ratio']==0 and not state['mageCooldown'][0]['casting']:break
            time.sleep(.25)
        m.command(tag+'-injure','combat-hp',value=index)
        cast=m.command(tag+'-cast','mage-cast',value=7,captureMs=450)
        assert cast['result']['accepted']
        time.sleep(.7)
        state=m.command(tag+'-visible')['state']
        player=next(p for p in state['party'] if p['PlayerIndex']==index)
        visual=next(v for v in state['mageVisuals'] if v['name'].startswith('SanctuaryHeal'))
        expected=player['feet']
        assert all(abs(a-b)<.015 for a,b in zip(expected,visual['position'])),(player,visual)
        assert any(e['kind']=='heal' and e['amount']>0 for e in state['mageEvents'])
        m.shot(tag)
        results.append(dict(index=index,job=player['job'],player=player['position'],heal=visual['position'],renderers=visual['renderers']))
        save('healing-checks',results)
        m.command(tag+'-resume','pause',value=0)
        time.sleep(5)
    print('Five active jobs: actual healing pulses track feet',flush=True)

def performance():
    import settings_checks as s
    results=[]
    for low in (False,True):
        fixture()
        for slot,skill in enumerate((0,4,5,7,9)):m.command('performance-equip-'+str(slot),'mage-equip',value=skill,awaken=slot)
        s.settings_open('presentation-perf-open-'+str(low));s.toggle('LowSpec',low)
        m.back();time.sleep(.7)
        m.command('presentation-perf-auto-'+str(low),'mage-auto',value=1)
        time.sleep(3)
        results.append(s.measure('presentation-perf-'+('low' if low else 'normal'),25))
        save('performance-checks',results)
        m.command('perf-indicator-'+str(low),'status-fixture',value=3,captureMs=100)
        time.sleep(.3)
        state=m.command('perf-indicator-state-'+str(low))['state']
        assert any(v['name']=='Status_Shield' for v in state['statusVisuals'])
        m.shot('performance-indicators-'+str(low))
        m.command('perf-indicator-resume-'+str(low),'pause',value=0)
        print('Measured lowSpec='+str(low),flush=True)
    s.settings_open('presentation-perf-restore');s.toggle('LowSpec',False);m.back()

def aspects():
    original=m.run('shell','wm','size').stdout.decode()
    results=[]
    try:
        for name,size in [('narrow','720x1280'),('tall','1080x2400'),('tablet','1200x1600')]:
            m.run('shell','wm','size',size);m.launch('status-aspect-'+name)
            fixture()
            m.command(name+'-stun','status-fixture',value=0)
            m.command(name+'-slow','status-fixture',value=1)
            m.command(name+'-taunt','status-fixture',value=3,captureMs=100)
            time.sleep(.4)
            state=m.command(name+'-status-state')['state']
            m.shot('status-aspect-'+name)
            assert len(state['statusVisuals'])>=4,state['statusVisuals']
            results.append(dict(name=name,size=size,indicators=state['statusVisuals']))
            save('status-aspect-checks',results)
    finally:
        override=next((line.split(': ')[1].strip() for line in original.splitlines() if line.startswith('Override size:')),None)
        m.run('shell','wm','size',override or 'reset');m.launch('status-aspect-restored')

def restore():
    original=files(ROOT/'Original/external.tar');prefs=files(ROOT/'Original/internal.tar')
    qa=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
    out=ROOT/'AfterDiagnostic';out.mkdir(exist_ok=True)
    m.run('shell','am','force-stop',m.PACKAGE)
    external='/sdcard/Android/data/'+m.PACKAGE
    for name,args in [('external.tar',('tar','-cf','-','-C',external,'files')),('internal.tar',('tar','-cf','-','files','shared_prefs'))]:
        assert not (out/name).exists(),'Restore backup exists; inspect before retrying.'
        (out/name).write_bytes(m.run('exec-out','run-as',m.PACKAGE,*args).stdout)
    current=files(out/'external.tar')
    preserved={name:data for name,data in original.items() if name.startswith('files/progression-local-v1/') and qa not in name}
    assert all(current.get(name)==data for name,data in preserved.items()),'Non-QA save changed; preserve and inspect.'
    for name,data in original.items():
        if qa not in name:continue
        assert re.fullmatch(r'files/progression-local-v1/[a-f0-9]+\.json(\.bak)?',name)
        m.run('shell',f"run-as {m.PACKAGE} sh -c 'cat > {external}/{name}'",data=data)
    for name,data in prefs.items():
        if not name.startswith('shared_prefs/'):continue
        assert re.fullmatch(r'shared_prefs/[A-Za-z0-9_.-]+',name)
        m.run('shell',f"run-as {m.PACKAGE} sh -c 'cat > {name}'",data=data)
        if m.run('shell','run-as',m.PACKAGE,'test','-f',name+'.bak',check=False).returncode==0:
            m.run('shell',f"run-as {m.PACKAGE} sh -c 'cat > {name}.bak'",data=data)
    internal=files(out/'internal.tar')
    removed=[n for n in internal if n not in prefs and re.fullmatch(r'files/(balance|hud|settings|lobby)-[A-Za-z0-9_.-]+\.(json|pending)',n)]
    for start in range(0,len(removed),80):m.run('shell','run-as',m.PACKAGE,'rm','-f',*removed[start:start+80])
    for name,data in preserved.items():assert m.run('exec-out','run-as',m.PACKAGE,'cat',external+'/'+name).stdout==data
    report=dict(passed=True,nonQaSavesPreserved=len(preserved),diagnosticFilesRemoved=len(removed),backupSha256=hashlib.sha256((out/'external.tar').read_bytes()).hexdigest())
    (out/'restoration.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)

if __name__=='__main__':
    action=sys.argv[1]
    if action=='checks':checks()
    elif action=='spells':spells()
    elif action=='restore':restore()
    elif action=='aspects':aspects()
    elif action=='control':control()
    elif action=='healing':healing()
    elif action=='performance':performance()
    elif action=='fixture':fixture()
    elif action=='install':
        assert (ROOT/'Original/external.tar').exists()
        build=json.loads(Path(sys.argv[2] if len(sys.argv)>2 else ROOT/'Diagnostic/build.json').read_text(encoding='utf-8-sig'))
        assert build['diagnostics'] and build['package']==m.PACKAGE
        print(m.run('install','-r','-t',build['apk']).stdout.decode(),flush=True)
        m.launch('presentation-login')
