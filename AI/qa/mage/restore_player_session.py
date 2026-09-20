"""Restore only this run's synthetic account/preferences; preserve all real accounts.

Arguments: original backup directory, output directory. Requires the three completed
session timing reports alongside the original backup. Does not launch the app.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import tarfile

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('device', Path(__file__).parents[1] / 'hud/device_checks.py')
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)
backup, output = map(Path, sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
qa = hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
for i in range(1, 4):
    timing = json.loads((backup.parent / f'Run{i}/Timing/session.json').read_text())
    assert timing['complete'] and timing['eligibleSeconds'] >= 1800
verification = json.loads((backup / 'backup-verification.json').read_text())
assert verification['verified'] and verification['externalSha256'] == hashlib.sha256((backup / 'external.tar').read_bytes()).hexdigest()
d.run('shell', 'input', 'keyevent', 'KEYCODE_HOME')
d.run('shell', 'am', 'force-stop', d.PACKAGE)
external_root = f'/sdcard/Android/data/{d.PACKAGE}'
for filename, args in (
    ('external.tar', ('tar', '-cf', '-', '-C', external_root, 'files')),
    ('internal.tar', ('tar', '-cf', '-', 'files', 'shared_prefs')),
):
    destination = output / filename
    assert not destination.exists(), destination
    destination.write_bytes(d.run('exec-out', 'run-as', d.PACKAGE, *args).stdout)
    with tarfile.open(destination) as archive:
        assert archive.getmembers()

with tarfile.open(output / 'external.tar') as current:
    preserved = {member.name: current.extractfile(member).read() for member in current.getmembers()
                 if member.isfile() and member.name.startswith('files/progression-local-v1/')
                 and qa not in member.name and member.name.endswith(('.json', '.json.bak'))}

restored = []
with tarfile.open(backup / 'external.tar') as archive:
    for suffix in ('.json', '.json.bak'):
        name = f'files/progression-local-v1/{qa}{suffix}'
        data = archive.extractfile(name).read()
        target = f'{external_root}/{name}'
        d.run('shell', f"run-as {d.PACKAGE} sh -c 'cat > {target}.session-restore && mv {target}.session-restore {target}'", data=data)
        assert d.run('exec-out', 'run-as', d.PACKAGE, 'cat', target).stdout == data
        restored.append('qa' + suffix)
with tarfile.open(backup / 'internal.tar') as archive:
    original_files = {member.name for member in archive.getmembers() if member.isfile()}
    for member in archive.getmembers():
        if not member.isfile() or not member.name.startswith('shared_prefs/'):
            continue
        assert re.fullmatch(r'shared_prefs/[A-Za-z0-9_.-]+', member.name)
        data = archive.extractfile(member).read()
        d.run('shell', f"run-as {d.PACKAGE} sh -c 'cat > {member.name}.session-restore && mv {member.name}.session-restore {member.name}'", data=data)
        assert d.run('exec-out', 'run-as', d.PACKAGE, 'cat', member.name).stdout == data
        if d.run('shell', 'run-as', d.PACKAGE, 'test', '-f', member.name + '.bak', check=False).returncode == 0:
            d.run('shell', f"run-as {d.PACKAGE} sh -c 'cat > {member.name}.bak'", data=data)
        restored.append('preferences')

files = d.run('shell', 'run-as', d.PACKAGE, 'find', 'files', '-maxdepth', '1', '-type', 'f').stdout.decode().splitlines()
removed = [name for name in files if name not in original_files and re.fullmatch(r'files/(balance|hud|settings|lobby)-[A-Za-z0-9_.-]+\.(json|pending)', name)]
for start in range(0, len(removed), 80):
    d.run('shell', 'run-as', d.PACKAGE, 'rm', '-f', *removed[start:start + 80])
for name, data in preserved.items():
    assert d.run('exec-out', 'run-as', d.PACKAGE, 'cat', f'{external_root}/{name}').stdout == data, name
d.run('shell', 'wm', 'size', '1080x2316')
d.run('shell', 'wm', 'density', '450')
report = {'restored': restored, 'nonQaSavesPreserved': len(preserved), 'diagnosticFilesRemoved': len(removed),
          'postQaBackupVerified': True, 'appStopped': True, 'displayRestored': '1080x2316 / 450'}
(output / 'restoration.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print(json.dumps(report))
