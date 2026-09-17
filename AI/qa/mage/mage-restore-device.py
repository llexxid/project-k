"""Restore this task's original QA account and observed display/settings defaults."""
import sys,json,hashlib
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
m=SourceFileLoader('mage_device',str(Path(__file__).with_name('mage-device.py'))).load_module()
import settings_checks as settings

assert m.PACKAGE=='com.isolatedyouth.idlekingdomrpg.lobbyqa'
backup=Path('.utmp/catalog-integration/mage-validation/device-original-profile.json')
data=backup.read_bytes();original=json.loads(data)
m.command('restore-auto-off','mage-auto',value=0)
m.command('restore-timescale','timescale',value=100)
m.command('restore-popup-close','mage-close')
settings.settings_open('restore-preferences')
for name in ('PowerSave','LowSpec','KeepAwake'):settings.toggle(name,False)
m.tap('BtnSaveClose',m.state('restore-preferences-save'))
current=settings.command('restore-preferences-verified')['state']
assert not any(current[k] for k in ('powerSave','lowSpec','keepAwake'))
m.run('shell','wm','size','1080x2316');m.run('shell','wm','density','450')
m.run('shell','am','force-stop',m.PACKAGE)
digest=hashlib.sha256(b'balance-qa-device-play-20260914').hexdigest()
dest=f'/sdcard/Android/data/{m.PACKAGE}/files/progression-local-v1/{digest}.json'
(m.OUT/'qa-profile-before-restore.json').write_bytes(m.run('exec-out','run-as',m.PACKAGE,'cat',dest).stdout)
for filename in (dest,dest+'.bak'):
 m.run('shell',f"run-as {m.PACKAGE} sh -c 'cat > {filename}.restore-pending && mv {filename}.restore-pending {filename}'",data=data)
restored=m.run('exec-out','run-as',m.PACKAGE,'cat',dest).stdout
assert restored==data
report={'package':m.PACKAGE,'productionPackageTouched':False,'profileByteIdentical':True,'profileSha256':hashlib.sha256(data).hexdigest(),'backupAlsoRestored':True,'attackLevel':original['AttackLevel'],'healthLevel':original['HealthLevel'],'lowSpec':current['lowSpec'],'powerSave':current['powerSave'],'keepAwake':current['keepAwake'],'wmSize':m.run('shell','wm','size').stdout.decode().strip(),'wmDensity':m.run('shell','wm','density').stdout.decode().strip(),'appStoppedAfterRestore':True}
Path('Docs/ArtPreparation/Validation/MageIntegration/device-restore.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf8')
print(json.dumps(report,ensure_ascii=False),flush=True)
