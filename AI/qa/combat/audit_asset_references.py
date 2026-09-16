"""Check introduced serialized GUID references against Unity asset/package metadata."""
import subprocess,re,json
from pathlib import Path
def git(*args):return subprocess.check_output(['git',*args],stderr=subprocess.DEVNULL).decode('utf8',errors='replace')
introduced=git('diff','--no-ext-diff','--no-textconv','--unified=0','--','Assets')
text='\n'.join(line[1:] for line in introduced.splitlines() if line.startswith('+') and not line.startswith('+++'))
new=git('ls-files','--others','--exclude-standard','-z','--','Assets').split('\0')
for name in new:
 p=Path(name)
 if name and p.suffix in ['.prefab','.asset','.anim','.controller','.spriteatlasv2']:
  text+='\n'+p.read_text(encoding='utf8')
needed=set(re.findall(r'guid: ([a-f0-9]{32})',text))
needed={g for g in needed if not g.startswith('0000000000000000')}
meta=subprocess.run(['rg','--no-heading','-N','-uu','-g','*.meta','^guid: ','Assets','Packages','Library/PackageCache'],capture_output=True,check=False).stdout.decode('utf8')
known=set(re.findall(r'guid: ([a-f0-9]{32})',meta));missing=sorted(needed-known)
out={'introducedReferenceGuids':len(needed),'resolved':len(needed & known),'missing':missing}
p=Path('Docs/ArtPreparation/Validation/CombatRevision');p.mkdir(parents=True,exist_ok=True)
(p/'asset-reference-audit.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf8')
print(out);assert not missing
