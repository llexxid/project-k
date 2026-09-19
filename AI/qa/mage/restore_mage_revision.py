"""Restore this revision's isolated QA profile/preferences, after a verified local backup."""
import hashlib,json,re,sys,tarfile
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,'AI/qa/hud')
import device_checks as d
root=Path('Recordings/MageRevision');out=root/'AfterDiagnostic';out.mkdir(exist_ok=True)
def files(path):
    with tarfile.open(path) as archive:return {m.name:archive.extractfile(m).read() for m in archive.getmembers() if m.isfile()}
baseline=files(root/'BeforeDiagnostic/external.tar');original=files(root/'Original/external.tar');prefs=files(root/'Original/internal.tar')
qa=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
d.run('shell','am','force-stop',d.PACKAGE)
external=f'/sdcard/Android/data/{d.PACKAGE}'
for filename,args in [('external.tar',('tar','-cf','-','-C',external,'files')),('internal.tar',('tar','-cf','-','files','shared_prefs'))]:
    target=out/filename
    assert not target.exists(), 'Restoration already started; inspect its preserved backup before retrying.'
    target.write_bytes(d.run('exec-out','run-as',d.PACKAGE,*args).stdout)
current=files(out/'external.tar')
preserved={name:data for name,data in baseline.items() if name.startswith('files/progression-local-v1/') and qa not in name}
assert all(current.get(name)==data for name,data in preserved.items()), 'A non-QA save changed; preserve it and inspect before restoration.'
restored=[]
for suffix in ('.json','.json.bak'):
    name=f'files/progression-local-v1/{qa}{suffix}';target=f'{external}/{name}';data=original[name]
    d.run('shell',f"run-as {d.PACKAGE} sh -c 'cat > {target}.revision-restore && mv {target}.revision-restore {target}'",data=data)
    assert d.run('exec-out','run-as',d.PACKAGE,'cat',target).stdout==data
    restored.append('QA'+suffix)
for name,data in prefs.items():
    if not name.startswith('shared_prefs/'):continue
    assert re.fullmatch(r'shared_prefs/[A-Za-z0-9_.-]+',name)
    d.run('shell',f"run-as {d.PACKAGE} sh -c 'cat > {name}.revision-restore && mv {name}.revision-restore {name}'",data=data)
    assert d.run('exec-out','run-as',d.PACKAGE,'cat',name).stdout==data
    restored.append('preferences')
    if d.run('shell','run-as',d.PACKAGE,'test','-f',name+'.bak',check=False).returncode==0:
        d.run('shell',f"run-as {d.PACKAGE} sh -c 'cat > {name}.bak'",data=data)
internal=files(out/'internal.tar')
removed=[name for name in internal if name not in prefs and re.fullmatch(r'files/(balance|hud|settings|lobby)-[A-Za-z0-9_.-]+\.(json|pending)',name)]
for start in range(0,len(removed),80):d.run('shell','run-as',d.PACKAGE,'rm','-f',*removed[start:start+80])
for name,data in preserved.items():assert d.run('exec-out','run-as',d.PACKAGE,'cat',external+'/'+name).stdout==data
report={'restored':restored,'nonQaSavesPreserved':len(preserved),'diagnosticFilesRemoved':len(removed),'postDiagnosticBackupSha256':hashlib.sha256((out/'external.tar').read_bytes()).hexdigest(),'passed':True}
(out/'restoration.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report))
