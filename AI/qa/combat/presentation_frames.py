"""Read actual SurfaceFlinger presentation times; no in-game diagnostic probe required."""
import os,sys,json,time,shlex,runpy,statistics
from pathlib import Path
sys.dont_write_bytecode=True
m=runpy.run_path('AI/qa/hud/device_checks.py');run=m['run'];package=m['PACKAGE'];out=m['OUT']
layers=run('shell','dumpsys','SurfaceFlinger','--list').stdout.decode().splitlines()
layer=next(x for x in layers if x.startswith('SurfaceView['+package) and '(BLAST)' in x)
seconds=int(sys.argv[1]) if len(sys.argv)>1 else 30
seen=set();started=time.monotonic();raw=[]
while time.monotonic()-started<seconds:
    response=run('shell','dumpsys SurfaceFlinger --latency '+shlex.quote(layer)).stdout.decode()
    raw.append(response)
    for line in response.splitlines()[1:]:
        fields=line.split()
        if len(fields)==3:
            actual=int(fields[1])
            if 0<actual<2**63-1:seen.add(actual)
    time.sleep(.75)
times=sorted(seen);deltas=[(b-a)/1e6 for a,b in zip(times,times[1:])]
assert len(deltas)>seconds*20, 'Too few presented frames; verify the current surface.'
ordered=sorted(deltas)
def percentile(p):return ordered[min(len(ordered)-1,round((len(ordered)-1)*p))]
result=dict(seconds=seconds,samples=len(deltas),averageMs=statistics.mean(deltas),p95Ms=percentile(.95),p99Ms=percentile(.99),maxMs=max(deltas),over50ms=sum(d>50 for d in deltas),note='One physical device; actual display presentation intervals, not GPU execution time.')
(out/'surface-presentation.json').write_text(json.dumps(result,indent=2),encoding='utf8')
(out/'surface-presentation-raw.json').write_text(json.dumps(raw),encoding='utf8')
print(json.dumps(result),flush=True)
