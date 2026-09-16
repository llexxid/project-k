"""Three aspect ratios on ONE physical Android device; restores its original overrides."""
import os,runpy,time,json
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/CombatRevision/Iteration4/Aspects')
m=runpy.run_path('AI/qa/combat/device_revision.py');settings=runpy.run_path('AI/qa/settings/settings_checks.py');run=m['run'];state=m['state'];shot=m['shot'];tap=m['tap'];c=m['c'];results=[]
try:
 for name,size,density in [('long','1080x2316',450),('narrow','720x1600',320),('tablet','1200x1600',320)]:
  run('shell','wm','size',size);run('shell','wm','density',str(density));settings['launch'](name+'-launch')
  c(name+'-fixture','combat-fixture',stage=0x20003000A,value=1,enhance=0)
  time.sleep(1);s=state(name+'-main');shot(name+'-main')
  row=dict(name=name,override=size,width=s['width'],height=s['height'],safeArea=s['safeArea'],mainTruncated=[x['text'] for x in s['labels'] if x['isTextTruncated']])
  tap('BtnKingdomArmy',s);s=state(name+'-army');m['click_label']('전직',s);s=state(name+'-tree1');shot(name+'-tree1')
  assert not any('궁수' in x['text'] for x in s['labels'])
  cards=[x for x in s['controls'] if 'JobCard' in x['name']];assert sum(not x['interactable'] for x in cards)==2
  # Begin inside the visible scroll viewport, never on a clipped offscreen label.
  x=int(s['width']*.55);y=int(s['height']*.70)
  for _ in range(3):
   run('shell','input','swipe',str(x),str(y),str(x),str(int(s['height']*.50)),'600');time.sleep(.4)
  t=state(name+'-tree2');shot(name+'-tree2');row['promotionTruncated']=[x['text'] for x in t['labels'] if x['isTextTruncated']]
  m['back']();c(name+'-mage-open','mage-list');t=state(name+'-mage');shot(name+'-mage');row['mageTruncated']=[x['text'] for x in t['labels'] if x['isTextTruncated']];m['back']()
  settings['command'](name+'-settings-open','open');t=state(name+'-settings');shot(name+'-settings');row['settingsTruncated']=[x['text'] for x in t['labels'] if x['isTextTruncated']];m['back']()
  results.append(row);print(row,flush=True)
finally:
 run('shell','wm','size','1080x2316');run('shell','wm','density','450')
 (m['OUT']/'aspect-summary.json').write_text(json.dumps(results,indent=2),encoding='utf8')
