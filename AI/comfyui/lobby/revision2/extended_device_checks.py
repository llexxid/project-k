"""Audio playlist, lifecycle and full-range damage checks on the isolated QA app."""
import sys, time, json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from device_qa import Device, OUTPUT, PACKAGE
from device_checks import restart
x=Device(sys.argv[1]); results=[]
def check(ok,label,details=None):
 results.append(dict(passed=bool(ok),description=label,details=details))
 print(('PASS ' if ok else 'FAIL ')+label,flush=True)
def state(name):return x.command('extended-'+name,'state')
def music(name):
 x.command('extended-music-'+name,'music',name);time.sleep(3)
 return state('music-'+name+'-settled')
for kind,count in [('lobby',2),('combat',4)]:
 s=music(kind);clips=[]
 for i in range(count):
  clips.append(s['music']['clip'])
  check(s['music']['isPlaying'] and s['music']['loadType']=='Streaming' and s['audioOutputRms']>0.00001,kind+' streaming/output '+str(i+1),s['music'])
  x.command('extended-end-'+kind+str(i),'music-end');time.sleep(5)
  s=state('after-'+kind+str(i))
 check(len(set(clips))==count and s['music']['clip']==clips[0],kind+' playlist visits every track then loops',clips)
music('lobby')
for mode in ['on','off']:
 x.command('extended-damage-mode-'+mode,'motion',mode)
 s=x.command('extended-damage-values-'+mode,'damage-check')
 check('18,446,744,073,709,551,615' in s.get('values',[]) and '0' in s.get('values',[]),'Full UInt64 and zero damage preserved, motion '+mode,s)
x.command('extended-language-en','language','en');x.command('extended-persist-off','motion','off')
restart(x);s=state('persisted')
check(s['language']=='en' and s['lowSpec'] and s['motionSymbol']=='Lobby_Stop' and s['targetFps']==60,'Language and shared low-spec persist after process restart')
x.command('extended-resume-on','motion','on');time.sleep(2);a=state('before-home')
x.run('shell','input','keyevent','3');time.sleep(3)
x.run('shell','am','start','-W','--user','0','-n',PACKAGE+'/com.unity3d.player.UnityPlayerActivity');time.sleep(3)
b=state('after-home');time.sleep(1.3);c=state('resumed-motion')
check(b['music']['isPlaying'] and b['audioOutputRms']>0.00001,'Music resumes after Android background/foreground',b['music'])
check(b['motion'] and abs(b['swordAngle']-c['swordAngle'])>.01,'Character motion resumes after Android background/foreground')
x.command('extended-restore-ko','language','ko');x.command('extended-restore-on','motion','on');x.command('extended-restore-power','power','off')
(OUTPUT/'extended-checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
