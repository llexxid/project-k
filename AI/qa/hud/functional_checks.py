import sys,time,json
sys.path.insert(0,'AI/qa/hud')
from device_checks import *
from resolution_checks import launch_main,settled,in_safe
checks=[]
def check(ok,label):
    checks.append(dict(passed=bool(ok),label=label))
    (OUT/'functional-results.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
    if not ok: raise AssertionError(label)
def hud(s):return next(g for g in s['goals'] if g['compact'])
s=launch_main('final')
for n in [0,7,10,7]:
    state(f'final-fixture-{n}','quest',n);s=settled(f'final-progress-{n}')
    check(hud(s)['progress']==f'{n}/10',f'Progress {n}/10 is exact')
    if n==10:check('다음 목표' in hud(s)['action'],'Completed guide exposes advancement')
shot('final-hud-seven')
tap('Body',s);s=settled('final-direct-guide');shot('final-direct-guide')
check(s['panels'] and not hud(s)['visible'],'Goal tap opens guide and removes duplicate HUD')
check(next(g for g in s['goals'] if not g['compact'])['progress']=='7/10','Detail and HUD share current count')
check(any('예정' in t['text'] for t in s['labels']),'Actual upcoming guides shown')
check(not next(c for c in s['controls'] if c['name']=='Body')['interactable'],'Passive battle objective has no dead action button')
back();s=settled('final-back');check(hud(s)['visible'] and hud(s)['progress']=='7/10','Back restores goal and count')
for i in range(4):
    tap('BtnHamburgerRight',s);s=settled(f'final-menu-{i}');check(s['menu'],f'Menu opens {i+1}')
    if i==0:shot('final-menu')
    if i%2==0:
        tap('BtnMenuGuide',s);s=settled(f'final-menu-guide-{i}');check(s['panels'] and not s['menu'],'Menu guide closes dropdown before panel');back()
    else:
        run('shell','input','tap','70','800');time.sleep(.6)
    s=settled(f'final-menu-return-{i}');check(not s['menu'] and not s['panels'],'Menu dismissal does not activate content beneath')
tap('BtnDevelopment',s);s=settled('final-growth');check(not hud(s)['visible'],'Growth sheet hides tracker');back();s=settled('final-growth-back');check(hud(s)['visible'],'Growth dismissal restores tracker')
state('final-reopen-command','reopen');s=settled('final-reopen');check(hud(s)['progress']=='7/10','Main reactivation reconnects current quest')
run('shell','input','keyevent','3');time.sleep(1);run('shell','monkey','-p',PACKAGE,'-c','android.intent.category.LAUNCHER','1');time.sleep(2)
s=settled('final-resumed');check(hud(s)['progress']=='7/10','Android background/resume retains tracker')
state('final-complete-command','quest',10);s=settled('final-complete');shot('final-hud-complete');tap('Body',s);s=settled('final-advanced')
check(hud(s)['description']!='몬스터 10마리 처치','Single tap advances completed guide')
check(all(in_safe(g['bounds'],s) for g in s['goals'] if g['visible']),'Tracker remains inside safe area')
print(f'{len(checks)}/{len(checks)} passed',flush=True)
