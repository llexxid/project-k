"""ABBA paired presentation measurements on the connected QA app; no gameplay services."""
from pathlib import Path
import json
import sys
import time
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from device_qa import Device, OUTPUT

device=Device(sys.argv[1])
phase=sys.argv[2]
seconds=float(sys.argv[3]) if len(sys.argv)>3 else 40
device.command(phase+'-power', 'power', 'off')
device.command(phase+'-fixture', 'fixture', 'on' if phase=='damage' else 'off')
device.command(phase+'-music', 'music', 'combat' if phase=='damage' else 'lobby')
time.sleep(5)
results=[]
for label,mode in [('normal-a','on'),('low-a','off'),('low-b','off'),('normal-b','on')]:
    identifier=f'{phase}-{label}'
    device.command(identifier+'-mode','motion',mode)
    time.sleep(3)
    print('MEASURE',identifier,seconds,'seconds',flush=True)
    result=device.command(identifier,'measure',seconds=seconds,wait=seconds+20)
    results.append(result)
    print(json.dumps({'id':identifier,'fps':result['fps'],'p99':result['frameMs']['p99'],
                      'cpuPercent':result['processCpu']['corePercent'],'gc':result['gc']['bytes'],
                      'presentation':result['presentation']},ensure_ascii=False),flush=True)
normal=[r for r in results if not r['lowSpec']];low=[r for r in results if r['lowSpec']]
def average(items,field):return sum(field(r) for r in items)/len(items)
nc=average(normal,lambda r:r['processCpu']['corePercent']);lc=average(low,lambda r:r['processCpu']['corePercent'])
summary={'phase':phase,'secondsPerTrial':seconds,'sequence':['normal','low','low','normal'],
         'normalCpuCorePercent':nc,'lowCpuCorePercent':lc,'cpuReductionPercent':(nc-lc)/nc*100,
         'normalFps':average(normal,lambda r:r['fps']),'lowFps':average(low,lambda r:r['fps']),
         'normalPresentation':{k:average(normal,lambda r:r['presentation'][k]) for k in ['lobbyMicrosecondsPerFrame','damageMicrosecondsPerFrame','verticesPerFrame']},
         'lowPresentation':{k:average(low,lambda r:r['presentation'][k]) for k in ['lobbyMicrosecondsPerFrame','damageMicrosecondsPerFrame','verticesPerFrame']}}
(OUTPUT/(phase+'-paired-summary.json')).write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2),flush=True)
device.command(phase+'-restore-motion','motion','on')
device.command(phase+'-restore-fixture','fixture','off')
device.command(phase+'-restore-music','music','lobby')
