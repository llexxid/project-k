"""Same physical phone, actual battle/rendering; isolated diagnostic profile."""
import os,sys,json,time
from pathlib import Path
from importlib.machinery import SourceFileLoader
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/FoundationRevision/EnvironmentAfter')
m=SourceFileLoader('env_device',str(Path(__file__).with_name('mage-device.py'))).load_module()

def battle():
 results=[]
 m.back();m.command('env-fixture','combat-fixture',value=1,enhance=0,stage=0x20001000a)
 for chapter in (1,2,3,1,2,3):
  tag='environment-'+str(chapter)+'-'+str(len(results))
  m.command(tag+'-resume','pause',value=0);m.command(tag+'-stage','stage',stage=0x200000000|(chapter<<16)|10);time.sleep(3)
  data=m.command(tag+'-freeze','pause',value=1)['state'];assert data['environment']['renderers']==3
  m.shot(tag);results.append(data['environment'])
  print(tag,data['environment'],flush=True)
 m.command('env-dungeon-ready','dungeon-fixture');m.command('env-dungeon-resume','pause',value=0)
 for stage in (0x210010001,0x220010001):
  r=m.command('env-dungeon-'+str(stage),'dungeon',stage=stage);assert r['result']['accepted'];time.sleep(3)
  s=m.command('env-dungeon-state-'+str(stage),'pause',value=1)['state'];m.shot('environment-dungeon-'+str(stage));results.append(s['environment'])
  m.command('env-return-'+str(stage),'pause',value=0);m.command('env-main-'+str(stage),'return');time.sleep(2)
 (m.OUT/'environment-report.json').write_text(json.dumps(results,indent=2),encoding='utf8')

if __name__=='__main__':battle()
