"""Record reviewed outcomes and refresh reported costs; never submits generation jobs."""
import json, re, sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parent/'lobby'))
from comfy_client import Comfy

def write(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')

assessments={
 1:('adopted in b48 Android diagnostic; final cross-aspect QA pending','Coherent molten body and tail at 32 source pixels/world unit; stable master transformed with masks, not per-frame diffusion. Flight and impact inspected in actual SM-N986N battle context.'),
 2:('rejected','Reference-conditioned Qwen edit reproduced enlarged source pixel blocks. It did not recover meaningful facets. Preserved for comparison; no output included in game.'),
 3:('Anime6B adopted in b48; UltraSharp rejected','Anime6B preserves authored silhouette with less speckled contour than UltraSharp. Shared six-color palette and binary alpha restore consistent logical pixel density. Applied to void and bloom thunder only; holy reference branch not used.'),
 4:('adopted in b48 Android diagnostic; final cross-aspect QA pending','Glacier, opaque Sanctuary crest and cloud masters passed single-cast Android checks. Motion derives from one master per effect. Glacier grows from feet; Sanctuary rises once, holds while ground wave pulses, then lowers. Cloud lower hanging bolts excluded from cloud-only crop.')}
for revision,(status,assessment) in assessments.items():
 p=ROOT/f'revision{revision}'/'manifest.json';d=json.loads(p.read_text(encoding='utf8'))
 d['status']=status;d['assessment']=assessment;d['finishing']='../finish-manifest.json'
 d['qa']='Recordings/FoundationRevision/VfxAfter/spell-report.json; VfxStatus/healing-checks.json'
 if revision>1:d['cost']='../cost-report.json: actual aggregate workspace increment; individual job attribution unavailable'
 if revision==1:d.pop('next',None)
 write(p,d)

# Preserve requests/settings, but never version a single-use signed upload credential.
p=ROOT/'revision2/upload-request.json'
def redact(value):
 if isinstance(value,dict):return {k:redact(v) for k,v in value.items()}
 if isinstance(value,list):return [redact(v) for v in value]
 if isinstance(value,str):
  return re.sub(r'https?://[^\s"<>\\]+',lambda m:'[single-use upload URL redacted]' if any(t in m[0].lower() for t in ('x-amz-','signature=','sig=','token=')) else m[0],value)
 return value
d=redact(json.loads(p.read_text(encoding='utf8')));d['credential_redaction']='Signed upload authorization removed; filename, input path, content type and workflow settings preserved.';write(p,d)

if '--usage' in sys.argv:
 r=Comfy().call('get_usage_report',{'months':1,'granularity':'hour','group_by':'product'})
 write(ROOT/'usage-latest.json',r)
 print('\n'.join(x['text'] for x in r.get('content',[]) if x.get('type')=='text' and x['text'].startswith('Total spend')))
print('Adoption records updated; no generation submitted.')
