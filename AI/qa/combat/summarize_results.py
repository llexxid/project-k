"""Export sanitized evidence. Full screenshots and account fixtures stay in ignored Recordings."""
import json,collections
from pathlib import Path
root=Path('Recordings/CombatRevision/Iteration6/Device')
def state(name):return json.loads((root/(name+'.json')).read_text(encoding='utf8'))['state']
def metrics(s):return {k:s[k] for k in ['meanFrameMs','maxFrameMs','frameCount','memory','timeScale','lastError']}
e=state('melee-30')['combatEvents'];hits=[x for x in e if 'melee-hit' in x['kind']]
report={
 'unity':'6000.3.21f1','device':'Samsung SM-N986N / Android 13 / Adreno 650',
 'physicalDeviceCount':1,'gameVersion':'0.10.0','sampleBuildVersionCode':7,
 'acceptance':{'combat':13,'mage':186,'balance':99,'ordinaryControl':9,'bossControl':9},
 'melee':{'hits':len(hits),'minimumFootDistance':min(x['distance'] for x in hits),'maximumAbsoluteLaneOffset':max(abs(x['deltaY']) for x in hits),'byActor':dict(collections.Counter(x['actor'] for x in hits)),'retreatDurations':[x['interval'] for x in e if x['kind']=='retreat-end']},
 'melee30Seconds':metrics(state('melee-30')),
 'automaticFiveSpells':[dict(seconds=n,**metrics(state('stress-'+str(n)))) for n in [30,60,90]],
 'backgroundResumeTimeScale':state('resume-background')['timeScale'],
 'projectileHitsInMeleeSample':dict(collections.Counter(x['actor'] for x in e if x['kind']=='projectile-hit')),
 'aspectSimulation':json.loads(Path('Recordings/CombatRevision/Iteration4/Aspects/aspect-summary.json').read_text(encoding='utf8')),
 'limits':['One actual device. Aspect overrides are not separate tablet/low-end devices.','Short development-build samples, not a long thermal soak or release GPU profile.','Low-spec mode preserves progress; these samples do not establish a memory or frame-time reduction.','Audio signal/voice/gain checks completed; acoustic listening on speakers/headphones was not performed.']
}
p=Path('Docs/ArtPreparation/Validation/CombatRevision');p.mkdir(parents=True,exist_ok=True)
(p/'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'melee':report['melee'],'stress':report['automaticFiveSpells'][-1]},ensure_ascii=False))
