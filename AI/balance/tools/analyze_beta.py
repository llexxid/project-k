from pathlib import Path
from decimal import Decimal,ROUND_CEILING,ROUND_FLOOR,ROUND_HALF_UP,getcontext
import json,hashlib
getcontext().prec=50
D=Decimal
ceil=lambda x:int(x.quantize(D('.000001'),rounding=ROUND_HALF_UP).to_integral_value(rounding=ROUND_CEILING))
floor=lambda x:int(x.to_integral_value(rounding=ROUND_FLOOR))
gold=[ceil(50*D('1.07')**i) for i in range(300)]
rows=[]
for level in [0,3,10,25,50,75,100,150,200,250,299,300]:
 rows.append(dict(level=level,totalCost=sum(gold[:level]),nextCost=gold[level] if level<300 else None,statMultiplier=str(D('1.025')**level)))
stages=[]
for stage in range(1,4):
 for wave in range(1,12):
  n=11*(stage-1)+wave-1;boss=wave==11
  stages.append(dict(stage=stage,wave=wave,hp=ceil(80*D('1.1')**n*(18 if boss else 1)),attack=ceil(5*D('1.065')**n*(3 if boss else 1)),gold=floor(10*D('1.08')**n*(10 if boss else 1)),exp=floor(3*D('1.07')**n*(20 if boss else 1))))
scenarios=[]
for stage,wave,kpm in [(1,10,15),(1,10,30),(2,10,30),(3,10,30)]:
 e=next(e for e in stages if e['stage']==stage and e['wave']==wave)
 online=kpm*30;offline=floor(480*D(kpm)*D('.6'))
 scenarios.append(dict(stage=stage,wave=wave,kpm=kpm,onlineMinutes=30,offlineHours=8,mainGold=(online+offline)*e['gold'],goldDungeon1Twice=18000,mainExperience=(online+offline)*e['exp']))
manifest={'balanceVersion':'beta-20260913-v1','sourcePriority':'Planning TXT primary; DOCX contextual cross-check; embedded development directions are reference, not user authorization','inputs':[],'independentDecimalCheck':{'goldCurve':rows,'stages':stages,'dailyScenarios':scenarios,'assumption':'Constant safe-wave KPM, no travel/down time, no ruby bonus, no quests. Projection, not observed retention or clear time.'}}
for p in [Path('AI/balance/20260913/planning-data.txt')]:
 manifest['inputs'].append({'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
manifest['inputs'].append({'name':'왕국군_키우기_상세기획서.docx','sha256':'a28688859b82f644c7685ec9e14198420dd658e1614c533b6d95848f86a09187','verification':'Original supplied file hash recorded at extraction; not redistributed.'})
Path('AI/balance/20260913/analysis.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(scenarios,ensure_ascii=True))
