"""Deep combat/VFX regression on the isolated device build. Never enables production probes.

HUD_QA_OUTPUT selects the evidence directory. Run after logging into the diagnostic player.
Modes: layered, matrix, stress. Matrix includes real dungeon entry/return transactions.
"""
import os, sys, time, json, runpy, collections, subprocess
from pathlib import Path
sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/CombatPolish20260917/Iteration1/Checks')
m = runpy.run_path('AI/qa/combat/device_revision.py')
c, shot, run, OUT, PACKAGE = (m[k] for k in ['c', 'shot', 'run', 'OUT', 'PACKAGE'])

def save(name, value):
    (OUT / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')

def running(name, stage):
    for attempt in range(30):
        s=c(name+'-'+str(attempt))['state']
        if s['stage']==stage and s['runState']=='Running' and s['monsters']:return s
        time.sleep(.2)
    raise AssertionError((name,s['stage'],s['runState'],s['lastError']))

def layered():
    c('layered-fixture', 'combat-fixture', stage=0x20003000A, value=1, enhance=0)
    time.sleep(1)
    result = c('layered-controls', 'polish-acceptance')['result']
    print('LAYERED', result, flush=True)
    assert result['passed'], result
    c('layered-reset', 'stage', stage=0x20003000A)

def matrix():
    cases = [(f'main-{i}', 0x200000000 | i << 16 | 10, False) for i in range(1,4)]
    cases += [(f'boss-{i}', 0x200000000 | i << 16 | 11, False) for i in range(1,4)]
    cases += [(f'{kind}-{i}', base | i << 16 | 1, True) for kind,base in [('gold',0x210000000),('ruby',0x220000000)] for i in range(1,6)]
    selected = set(sys.argv[2:])
    if selected: cases = [case for case in cases if case[0] in selected]
    if any(case[2] for case in cases):
        # Fresh-account smoke deliberately resets story/account unlocks. Restore
        # the existing midgame QA fixture before exercising real dungeon gates.
        c('matrix-account-access','midgame')
    report = json.loads((OUT/'matrix.json').read_text('utf8')) if selected and (OUT/'matrix.json').exists() else []
    for name, stage, dungeon in cases:
        c(name+'-fixture', 'combat-fixture', stage=0x20003000A if dungeon else stage, value=1, enhance=0)
        entry_mode = 'main fixture'
        if dungeon:
            running(name+'-main-ready',0x20003000A)
            c(name+'-access','dungeon-fixture')
            entered = c(name+'-enter', 'dungeon', stage=stage)
            assert entered['result']['accepted'], entered
            entry_mode = 'ticketed entry with isolated QA unlocks and tickets'
        initial = running(name+'-initial',stage)
        c(name+'-reset', 'combat-reset')
        recorder=None
        if os.environ.get('HUD_QA_RECORD_COMBAT')=='1' and name in ['main-2','main-3','boss-3','gold-5','ruby-5']:
            build=json.loads((OUT.parent/'build.json').read_text('utf-8-sig'))
            video=f"CombatPolish-{build['version']}-b{build['versionCode']}-{name}.mp4"
            remote='/sdcard/Download/'+video
            recorder=subprocess.Popen([run.__globals__['ADB'],'-s',os.environ.get('HUD_QA_SERIAL','R3CN815LZ9L'),
                'shell','screenrecord','--size','720x1544','--bit-rate','4000000','--time-limit','12',remote],
                stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        time.sleep(18)
        if recorder is not None:
            _,error=recorder.communicate(timeout=15)
            assert recorder.returncode==0,error
            run('pull',remote,str(OUT/video));run('shell','rm','--',remote)
        s = c(name+'-end')['state']; shot(name)
        events = s['combatEvents']; counts = dict(collections.Counter(e['kind'] for e in events))
        hits = [e for e in events if e['kind'] in ('player-melee-hit','monster-melee-hit')]
        invalid = [e for e in hits if abs(e['deltaX']) < .515 or abs(e['deltaY']) > (.245 if e['kind']=='player-melee-hit' else .365) or e['facing']*e['deltaX']<=0]
        retreats = [e['interval'] for e in events if e['kind']=='retreat-end']
        entry = dict(name=name,entryMode=entry_mode,initialMonsters=initial['monsters'],stage=s['stage'],runState=s['runState'],counts=counts,invalidMelee=invalid,
                     maxRetreat=max(retreats,default=0),monsters=s['monsters'],party=s['party'],error=s['lastError'])
        report.append(entry);save('matrix',report)
        print('MATRIX',name,counts,'invalid',len(invalid),'retreat',entry['maxRetreat'],flush=True)
        assert not s['lastError'] and not invalid, entry
        assert entry['maxRetreat'] <= .82, entry
        assert counts.get('player-windup',0)>0 and counts.get('monster-windup',0)>0, entry
        if dungeon:
            returned=c(name+'-return','return')['state']
            assert returned['stage']==0x20003000A,returned['stage']
            running(name+'-returned',0x20003000A)
    print('Selected combat scenarios passed:',len(cases),flush=True)

def stress():
    settings=runpy.run_path('AI/qa/settings/settings_checks.py')
    report=[]
    for low in (False,True):
        tag='stress-'+str(low)
        settings['settings_open'](tag+'-settings')
        settings['toggle']('PowerSave',False);settings['toggle']('LowSpec',low)
        m['tap']('BtnSaveClose',m['state'](tag+'-close'))
        c(tag+'-party','combat-fixture',stage=0x20003000A,value=1,enhance=0)
        c(tag+'-skills','mage-fixture',awaken=10,enhance=0,bloom=True)
        for slot,skill in enumerate([8,0,9,3,4]):c(tag+'-equip-'+str(slot),'mage-equip',value=skill,awaken=slot)
        c(tag+'-auto','mage-auto',value=1)
        time.sleep(5)
        start=c(tag+'-start')['state'];c(tag+'-reset','combat-reset')
        for n in range(3):
            measurement=settings['measure'](tag+'-window-'+str(n),30)
            s=c(tag+'-sample-'+str(n))['state']
            row=dict(lowSpec=low,window=n,measurement=measurement,
                     state={k:s[k] for k in ['time','timeScale','memory','meanFrameMs','maxFrameMs','frameCount','lastError','stage']})
            report.append(row);save('stress',report)
            print('STRESS',low,n,row['state'],flush=True)
            assert s['timeScale']==1 and not s['lastError'] and s['time']>start['time']+10
        shot(tag)
        c(tag+'-auto-off','mage-auto',value=0)
    run('shell','input','keyevent','KEYCODE_HOME');time.sleep(3)
    run('shell','monkey','-p',PACKAGE,'1');time.sleep(4)
    resumed=c('stress-background-resume')['state'];shot('stress-background-resume')
    assert resumed['timeScale']==1 and not resumed['lastError']
    settings['settings_open']('stress-restore-settings');settings['toggle']('LowSpec',False)
    m['tap']('BtnSaveClose',m['state']('stress-restore-close'))
    print('Both 90-second overlapping-spell runs and resume passed',flush=True)

if __name__=='__main__':
    globals()[sys.argv[1]]()
