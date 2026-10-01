"""Physical-phone environment simulation in its own package and unique local profile.

Run from project-k. Requires BuildForEnvironmentSimulation; never invokes a login provider.
The natural .lobbyqa app is only read to check that its progression/preferences stay intact.
"""
from pathlib import Path
from importlib.machinery import SourceFileLoader
import argparse, hashlib, io, json, os, re, subprocess, sys, tarfile, time

sys.dont_write_bytecode = True
PROJECT = Path(__file__).resolve().parents[3]
PACKAGE = 'com.isolatedyouth.idlekingdomrpg.environmentqa'
NATURAL = 'com.isolatedyouth.idlekingdomrpg.lobbyqa'
OUT = Path(os.environ.get('ENVIRONMENT_SIM_OUTPUT', 'Recordings/EnvironmentSimulation'))
OUT.mkdir(parents=True, exist_ok=True)
os.environ['HUD_QA_OUTPUT'] = str(OUT)
m = SourceFileLoader('environment_sim_mage', str(PROJECT/'AI/qa/mage/mage-device.py')).load_module()
# These imported helpers retain their own module globals. Retarget each explicitly,
# without changing the repository's default tools or the installed natural app.
for module in (m, sys.modules['device_checks'], sys.modules['settings_checks']):
    module.PACKAGE = PACKAGE
assert m.PACKAGE == sys.modules['device_checks'].PACKAGE == sys.modules['settings_checks'].PACKAGE == PACKAGE

_base_run = m.run

def transport(*args, check=True, data=None):
    # Read-side ADB failures can occur when another Unity process shuts its server down.
    # Never replay an uncertain gameplay/file-command mutation or input event here.
    safe = args[0] == 'exec-out' or args[:2] == ('shell','pm') or (len(args)>4 and args[:3] == ('shell','run-as',PACKAGE) and args[3]=='rm')
    attempts=5 if safe else 1
    for attempt in range(attempts):
        try:
            result=_base_run(*args,check=False,data=data)
            transient=any(word in result.stderr.decode(errors='replace').lower() for word in ('device offline','device not found','no devices','cannot connect','closed','protocol fault'))
            if result.returncode and transient and attempt+1<attempts:
                time.sleep(.5*(attempt+1)); continue
            if check and result.returncode: raise subprocess.CalledProcessError(result.returncode,result.args,result.stdout,result.stderr)
            return result
        except (subprocess.TimeoutExpired,subprocess.CalledProcessError):
            if attempt+1>=attempts:raise
            time.sleep(.5*(attempt+1))

for module in (m, sys.modules['device_checks'], sys.modules['settings_checks']): module.run=transport


def save(name, value):
    (OUT/(name+'.json')).write_text(json.dumps(value, indent=2, ensure_ascii=False)+'\n', encoding='utf8')


def natural_snapshot(tag):
    hashes = {}
    for area, cwd, child in [('internal', '.', 'shared_prefs'), ('external', '/sdcard/Android/data/'+NATURAL+'/files', 'progression-local-v1')]:
        raw = m.run('exec-out', 'run-as', NATURAL, 'tar', '-cf', '-', '-C', cwd, child).stdout
        (OUT/(tag+'-'+area+'.tar')).write_bytes(raw)
        with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
            for member in archive.getmembers():
                if member.isfile(): hashes[area+'/'+member.name] = hashlib.sha256(archive.extractfile(member).read()).hexdigest()
    save(tag, hashes)
    return hashes


def command(name, action='state', **args):
    assert re.fullmatch(r'[A-Za-z0-9_-]+',name)
    filename='files/balance-'+name+'.json'
    m.run('shell','run-as',PACKAGE,'rm','-f',filename)
    payload=json.dumps(dict(id=name,action=action,**args)).encode()
    send_error=None
    try:
        m.run('shell',f"run-as {PACKAGE} sh -c 'cat > files/balance-command.pending && mv files/balance-command.pending files/balance-command.json'",data=payload)
    except subprocess.CalledProcessError as error:
        send_error=str(error)
    # A send failure is ambiguous: poll the same ID, never resend a mutation.
    until=time.monotonic()+60
    while time.monotonic()<until:
        read=m.run('exec-out','run-as',PACKAGE,'cat',filename,check=False)
        if read.returncode==0 and read.stdout.strip():
            try:result=json.loads(read.stdout)
            except json.JSONDecodeError:time.sleep(.15);continue
            save(name,result)
            if 'error' in result:raise RuntimeError(result['error'])
            break
        time.sleep(.15)
    else:raise TimeoutError(name+'; command was not replayed after unknown send outcome: '+str(send_error))
    evidence = result['state'].get('simulation')
    assert evidence and all(evidence[k] for k in ('isolatedPackage','uniqueLocalAccount','localAuthority','ready')), 'Isolated simulation is not ready'
    assert not evidence['authenticatedCloudSession'] and not evidence['hasNetworkSession'], 'Unexpected authentication in simulation'
    assert result['state']['lastError'] is None, 'Local simulation save error'
    return result


def wait_stage(tag, stage):
    for attempt in range(35):
        state = command(tag+'-ready-'+str(attempt))['state']
        if state['stage'] == stage and state['runState'] == 'Running' and len(state['party'] or []) == 3 and state.get('environment'):
            return state
        time.sleep(.5)
    raise TimeoutError('Stage did not become ready: '+str(stage))


def enter(tag, pool, mode):
    state = command(tag+'-before')['state']
    forest = pool in POOLS[:3]
    stage = (0x200000000 | ((POOLS.index(pool)+1)<<16) | (11 if mode == 'Boss' else 10)) if forest else (0x210010001 if pool == 'GoldDungeon' else 0x220010001)
    if state['stage'] == stage and state['runState'] == 'Running': return state
    if state['stage'] >> 28 != 0x20:
        command(tag+'-return','return'); wait_stage(tag+'-returned', state['MainStage'])
    if forest:
        command(tag+'-boss-auto','boss-auto',value=1 if mode=='Boss' else 0)
        command(tag+'-fixture','combat-fixture',value=1,enhance=0,stage=stage)
    else:
        command(tag+'-fixture','combat-fixture',value=1,enhance=0,stage=0x20003000A)
        wait_stage(tag+'-base',0x20003000A)
        command(tag+'-tickets','dungeon-fixture')
        assert command(tag+'-enter','dungeon',stage=stage)['result']['accepted']
    return wait_stage(tag, stage)


POOLS = ['Stage01_ForestDirt','Stage02_ForestGrass','Stage03_DeepForest','GoldDungeon','RubyWasteland']
MODES = ['Main','Boss','Special']

def install(manifest_path):
    manifest = json.loads(Path(manifest_path).read_text(encoding='utf-8-sig'))
    assert manifest['package'] == PACKAGE and manifest['isolatedSimulation'] and manifest['diagnostics'] and not manifest['naturalProfile']
    apk=Path(manifest['apk']); assert apk.is_file()
    existing=m.run('shell','pm','path',PACKAGE,check=False)
    assert not existing.stdout.strip(), 'Temporary simulation package already exists; inspect and preserve it before replacement'
    before=natural_snapshot('natural-before-simulation')
    assert before == natural_snapshot('natural-before-simulation-confirmed'), 'Natural app data changed during baseline read'
    natural_uid_before=m.run('shell','pm','list','packages','--user','0','-U',NATURAL).stdout.decode().strip()
    result=m.run('install','-r','-t',str(apk)).stdout.decode().strip(); assert 'Success' in result
    natural_uid_after=m.run('shell','pm','list','packages','--user','0','-U',NATURAL).stdout.decode().strip()
    simulation_uid=m.run('shell','pm','list','packages','--user','0','-U',PACKAGE).stdout.decode().strip()
    assert natural_uid_before==natural_uid_after
    assert re.search(r'uid:(\d+)',natural_uid_after).group(1)!=re.search(r'uid:(\d+)',simulation_uid).group(1)
    save('package-isolation',dict(naturalUidUnchanged=True,distinctAndroidUids=True,naturalPackage=NATURAL,simulationPackage=PACKAGE))
    assert before == natural_snapshot('natural-after-simulation-install'), 'Natural app data changed while installing separate package'
    save('installed',dict(manifest,apkSha256=hashlib.sha256(apk.read_bytes()).hexdigest(),naturalProfileFilesUnchanged=len(before)))
    m.run('shell','input','keyevent','KEYCODE_WAKEUP')
    m.run('shell','monkey','-p',PACKAGE,'-c','android.intent.category.LAUNCHER','1')
    ready_path='/sdcard/Android/data/'+PACKAGE+'/files/environment-simulation-ready.json'
    for _ in range(45):
        result=m.run('exec-out','run-as',PACKAGE,'cat',ready_path,check=False)
        if result.returncode == 0 and result.stdout.strip():
            try:ready=json.loads(result.stdout)
            except json.JSONDecodeError:time.sleep(.2);continue
            if ready.get('startupError'): raise RuntimeError(ready['startupError'])
            if ready.get('ready'): break
        time.sleep(2)
    else: raise TimeoutError('Local simulation bootstrap did not finish')
    first=command('simulation-initial')['state']; m.shot('simulation-initial')
    print(json.dumps(dict(installed=True,version=manifest['version'],versionCode=manifest['versionCode'],ready=first['simulation'],naturalFilesUnchanged=len(before))),flush=True)


def backgrounds(resume=False):
    entries=json.loads((PROJECT/'Docs/ArtPreparation/Manifests/environment-presets.json').read_text(encoding='utf-8-sig'))
    entries.sort(key=lambda row:(POOLS.index(row['pool']),MODES.index(row['mode']),row['name']))
    assert len(entries)==90
    rows=json.loads((OUT/'backgrounds.json').read_text(encoding='utf8'))['checks'] if resume else []
    assert all(row['number']==index+1 and row['preset']==entries[index]['name'] and row['pixels']['valid'] for index,row in enumerate(rows))
    # A new local profile has no dungeon unlock receipts. Reuse the existing local
    # fixture for chapter 1 boss / chapter 2 wave 5, then normal combat fixtures
    # restore the intended low attack and high health for every tested region.
    command('background-access-fixture','midgame')
    wait_stage('background-access',0x20002000A)
    command('background-auto-off','mage-auto',value=0)
    for index,item in enumerate(entries,1):
        if index<=len(rows):continue
        tag='device-'+str(index).zfill(3)
        command(tag+'-resume','pause',value=0)
        live_before=enter(tag,item['pool'],item['mode'])
        choice=command(tag+'-preset','environment-preset',preset=item['name'])['result']
        time.sleep(.18)
        pixels=command(tag+'-pixels','environment-pixels')
        state=pixels['state']; env=state['environment']
        assert state['runState']=='Running', 'Battle left Running before pixel validation'
        assert env['CurrentPresetId']==item['name'] and env['renderers']==3 and env['activeColliders']==0 and env['activeAnimators']==0
        assert pixels['result']['valid'], (tag,pixels['result'])
        assert state['time']>live_before['time'], 'Battle did not advance before the preset sample'
        held=command(tag+'-capture-hold','pause',value=1)['state']
        assert held['runState']=='Running', 'Battle ended before numbered capture'
        ui=m.state(tag+'-capture-ui'); assert ui['main'] and not ui['panels'], 'Modal obscures numbered capture'
        m.shot(tag)
        after=command(tag+'-after-shot')['state']
        assert after['runState']=='Running' and after['environment']['CurrentPresetId']==item['name'], 'Battle/preset changed before its numbered capture'
        rows.append(dict(number=index,preset=item['name'],mode=item['mode'],selection=choice,environment=env,pixels=pixels['result'],screenshot=tag+'.png',simulationTimeBefore=live_before['time'],simulationTimeAtPixels=state['time'],battleAdvanced=state['time']>live_before['time']))
        save('backgrounds',dict(valid=len(rows)==90,completed=len(rows),checks=rows))
        if index % 5 == 0: print('Device backgrounds '+str(index)+'/90 passed',flush=True)
    assert len({r['preset'] for r in rows})==90


def recapture(number):
    """Replace one numbered review image only after preserving its original evidence."""
    assert 1<=number<=90
    document=json.loads((OUT/'backgrounds.json').read_text(encoding='utf8'))
    old=document['checks'][number-1]; tag='device-'+str(number).zfill(3)
    archive=OUT/(tag+'-original-capture');archive.mkdir(exist_ok=False)
    for suffix in ('.png','-pixels.json','-after-shot.json','-capture-hold.json','-preset.json'):
        source=OUT/(tag+suffix)
        if source.exists():(archive/source.name).write_bytes(source.read_bytes())
    (archive/'record.json').write_text(json.dumps(old,indent=2)+'\n',encoding='utf8')
    item=next(row for row in json.loads((PROJECT/'Docs/ArtPreparation/Manifests/environment-presets.json').read_text(encoding='utf-8-sig')) if row['name']==old['preset'])
    command(tag+'-recapture-resume','pause',value=0)
    current=command(tag+'-recapture-current')['state']
    if current['stage']>>28!=0x20:
        command(tag+'-recapture-return','return');wait_stage(tag+'-recapture-main',current['MainStage'])
    before=enter(tag+'-recapture',item['pool'],item['mode'])
    choice=command(tag+'-preset','environment-preset',preset=item['name'])['result']
    time.sleep(.18)
    pixels=command(tag+'-pixels','environment-pixels');state=pixels['state'];env=state['environment']
    assert state['runState']=='Running' and state['time']>before['time']
    assert env['CurrentPresetId']==item['name'] and env['renderers']==3 and env['activeColliders']==0 and env['activeAnimators']==0 and pixels['result']['valid']
    held=command(tag+'-capture-hold','pause',value=1)['state'];assert held['runState']=='Running'
    ui=m.state(tag+'-capture-ui');assert ui['main'] and not ui['panels']
    m.shot(tag)
    after=command(tag+'-after-shot')['state'];assert after['runState']=='Running' and after['environment']['CurrentPresetId']==item['name']
    document['checks'][number-1]=dict(number=number,preset=item['name'],mode=item['mode'],selection=choice,environment=env,pixels=pixels['result'],screenshot=tag+'.png',simulationTimeBefore=before['time'],simulationTimeAtPixels=state['time'],battleAdvanced=True,runStateBeforeAndAfterCapture='Running',modalAbsent=True,recaptureReason='Original screenshot included the normal dungeon completion popup; original retained privately. Re-entered actual dungeon and repeated GPU/order/live-state checks.')
    save('backgrounds',document)
    print('Numbered capture '+str(number)+' revalidated and replaced; original retained',flush=True)


def hit_flash():
    enter('flash','Stage01_ForestDirt','Main')
    command('flash-fresh-stage','combat-fixture',value=1,enhance=0,stage=0x20001000A)
    wait_stage('flash-fresh',0x20001000A)
    rows=[]
    for phase in ('baseline','active','restored'):
        if phase=='restored':
            command('flash-resume','timescale',value=100);time.sleep(.35)
        row=command('flash-'+phase,'environment-hit-flash',preset=phase)['result']
        assert row['valid'],row
        m.shot('device-hit-flash-'+phase)
        rows.append(dict(row,screenshot='device-hit-flash-'+phase+'.png'))
    command('flash-finish','timescale',value=100)
    save('hit-flash',dict(valid=True,checks=rows,scope='Controlled 1 HP hit on an actually spawned enemy in the isolated local profile; real shader/property block/coroutine, same live materials'))
    print('Actual enemy damage flash baseline/active/restored and target-only pixels passed',flush=True)


def spells_and_icon():
    rows=[]
    for skill,phase in [(0,220),(9,380)]:
        tag='device-skill-'+str(skill)
        enter(tag,'Stage03_DeepForest','Main')
        command(tag+'-fixture','mage-fixture',enhance=0,awaken=0,bloom=False)
        command(tag+'-equip','mage-equip',value=skill,awaken=0)
        for attempt in range(60):
            s=command(tag+'-cooldown-'+str(attempt))['state']
            if not s['mageCooldown'][0]['casting'] and s['mageCooldown'][0]['ratio']==0:break
            time.sleep(.3)
        else:raise TimeoutError('Skill cooldown')
        command(tag+'-slow','timescale',value=25)
        assert command(tag+'-cast','mage-cast',value=skill,captureMs=phase)['result']['accepted']
        time.sleep(phase/250+.25)
        active=command(tag+'-active')['state']
        assert active['mageVisuals'], 'No actual skill renderer captured'
        if skill==0:
            lightning=[v for v in active['mageVisuals'] if v['name'].startswith('Lightning(')]
            assert lightning and all(r['sprite'].startswith('LightningOriginal_') for v in lightning for r in v['renderers'])
        m.shot(tag)
        pixels=command(tag+'-pixels','environment-pixels')['result']; assert pixels['valid']
        command(tag+'-resume','timescale',value=100);time.sleep(7)
        done=command(tag+'-done')['state']
        events=[e for e in done['mageEvents'] if e['skill']==skill]
        assert any(e['kind']=='end' for e in events) and any(e['kind']=='damage' and e['amount']>0 for e in events)
        if skill==0:assert len([e for e in events if e['kind']=='bolt'])==3
        rows.append(dict(skill=skill,bloom=False,pixels=pixels,events=events,activeEnvironment=active['environment'],activeVisuals=active['mageVisuals'],screenshot=tag+'.png'))
    command('device-icon-open','mage-detail',value=0)
    state=command('device-icon-state')['state']; assert not state['mage']['0']['BloomEnabled']
    m.shot('device-lightning')
    ui=m.state('device-icon-ui');save('device-icon-ui',ui)
    m.back(); time.sleep(.5)
    command('device-icon-close','mage-close')
    save('skills',dict(valid=True,checks=rows,iconScreenshot='device-lightning.png',normalLightning=True))
    print('Actual base Lightning (three strikes), Void Rift, ordering/pixels and base icon passed',flush=True)


def touches():
    command('touch-close-skills','mage-close')
    enter('touch-main','Stage03_DeepForest','Main')
    rows=[]
    def foreground():
        windows=m.run('shell','dumpsys','window').stdout.decode(errors='replace')
        focus=[line for line in windows.splitlines() if 'mCurrentFocus=' in line or 'mFocusedApp=' in line]
        assert any(PACKAGE in line for line in focus),'Simulation app is not the foreground surface; no input sent'
    foreground()
    initial=m.state('touch-initial-ui')
    if initial['menu'] or initial['panels'] or any(c['name']=='BtnSaveClose' for c in initial['controls']):m.back()
    for control in ('BtnDevelopment','BtnKingdomArmy','BtnDungeon','BtnGacha'):
        foreground();before=m.state('touch-'+control+'-before');assert before['main'] and not before['panels']
        m.tap(control,before)
        opened=m.state('touch-'+control+'-opened');assert opened['main'] and opened['panels']
        m.shot('device-menu-'+control)
        foreground();m.back();closed=m.state('touch-'+control+'-closed')
        assert closed['main'] and not closed['panels']
        rows.append(dict(control=control,actualAdbTouch=True,panelOpened=True,hardwareBackClosed=True,screenshot='device-menu-'+control+'.png'))
    foreground();before=m.state('touch-hamburger-before');m.tap('BtnHamburgerRight',before)
    menu=m.state('touch-hamburger-opened');assert menu['menu']
    foreground();m.tap('BtnMenuSettings',menu)
    opened=m.state('touch-settings-opened')
    # Settings is its own overlay and is intentionally outside UIManager's tab/
    # blocking-panel count. Prove its actual rendered controls, then their removal.
    assert any(c['name']=='BtnSaveClose' and c['interactable'] for c in opened['controls'])
    m.shot('device-menu-settings')
    foreground();m.back();closed=m.state('touch-settings-closed')
    assert closed['main'] and not closed['panels'] and not any(c['name']=='BtnSaveClose' for c in closed['controls'])
    rows.append(dict(control='BtnHamburgerRight → BtnMenuSettings',actualAdbTouch=True,panelOpened=True,hardwareBackClosed=True,screenshot='device-menu-settings.png'))
    save('touches',dict(valid=True,checks=rows,scope='Observed UI control bounds -> real Android touch and hardware Back. No purchase, grant or progression action selected.'))
    print('Five native menu touch/back routes passed',flush=True)


def performance():
    enter('perf','Stage03_DeepForest','Main')
    command('perf-close','mage-close');command('perf-auto-off','mage-auto',value=0)
    command('perf-resume','timescale',value=100)
    time.sleep(4)
    before=command('perf-before')['state']; started=time.monotonic()
    time.sleep(20)
    after=command('perf-after')['state'];elapsed=time.monotonic()-started
    frames=after['frameCount']-before['frameCount']
    total_ms=after['meanFrameMs']*after['frameCount']-before['meanFrameMs']*before['frameCount']
    assert before['timeScale']==after['timeScale']==1 and frames>0 and total_ms>0
    save('performance',dict(scope='New isolated QA build on one physical phone; no b66 before baseline and no improvement comparison',wallElapsedSeconds=elapsed,deltaFrames=frames,sampledFrameTimeMs=total_ms,meanFrameMs=total_ms/frames,framesPerSecond=frames*1000/total_ms,memoryBefore=before['memory'],memoryAfter=after['memory'],environment=after['environment'],pixelReadbacksDuringInterval=0,explicitQaCommandsBetweenSnapshots=0,backgroundAutosnapshotNote='Existing BalanceDeviceProbe still writes its normal diagnostic live snapshot every 10 seconds.'))
    print('20-second unpaused battle observation saved',flush=True)


def preservation():
    before=json.loads((OUT/'natural-before-simulation-confirmed.json').read_text())
    after=natural_snapshot('natural-after-simulation')
    assert before==after,'Natural profile changed during separate-package simulation; investigate before any restore'
    save('natural-preservation',dict(progressionAndPreferenceFilesUnchanged=len(before),storageRestored=False,normalPackageModifiedByHarness=False))
    print('Natural progression/preferences unchanged: '+str(len(before))+' files',flush=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('action',choices=['install','backgrounds','recapture','flash','skills','touches','performance','preservation','all']);parser.add_argument('--manifest');parser.add_argument('--number',type=int);parser.add_argument('--resume',action='store_true',help='Explicitly continue validated numbered captures from this still-running isolated installation')
    args=parser.parse_args()
    if args.action=='install':install(args.manifest);return
    assert (OUT/'installed.json').exists(),'Install exact isolated build first'
    if args.action=='recapture':recapture(args.number);return
    if args.action in ('backgrounds','all'):backgrounds(args.resume)
    if args.action in ('flash','all'):hit_flash()
    if args.action in ('skills','all'):spells_and_icon()
    if args.action in ('touches','all'):touches()
    if args.action in ('performance','all'):performance()
    if args.action in ('preservation','all'):preservation()

if __name__=='__main__':main()
