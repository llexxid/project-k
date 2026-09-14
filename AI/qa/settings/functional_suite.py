"""Sequential physical-device settings checks. Inspect initial screenshots before running this suite."""
import json,sys,time
from settings_checks import *

def close(tag):
 s=state(tag+'-controls');tap('BtnSaveClose',s)
 assert not command(tag+'-closed')['state']['settingsOpen']

def audio_effects(s,value):
 active=[a for a in s['sources'] if a['effect'] and a['isPlaying']]
 assert active,'No actual pooled audio effect playing'
 assert all(abs(a['volume']-value)<.015 for a in active),(value,active)

def main():
 pause(True,'suite-pause')
 checks=[]
 def passed(s):checks.append(s);print('PASS '+s,flush=True)
 s=command('suite-start')['state']; original_gold=s['gold']
 try:
  settings_open('suite-defaults');slider('SldEffects',1);slider('SldMusic',1);slider('SldVolume',1)
  if command('suite-mute-state')['state']['muted']:tap('BtnMute',state('suite-unmute-ui'))
  command('high-gold','amount','1234567890')
  for i,expected in enumerate(['1.23B','12.34억','1.23e9']):
   s=style(i,'format-'+str(i));assert s['mainGold']==expected,(s['mainGold'],expected)
   assert not any(t['isTextTruncated'] for t in s['labels'])
   shot('format-'+str(i));close('format-'+str(i))
   d=command('damage-'+str(i),'damage',str(2**64-1))['state'];assert any(h['text']==['18.44Qi','1,844경','1.84e19'][i] for h in d['hits']),d['hits']
   shot('damage-'+str(i));passed('format '+str(i)+' updates actual HUD and UInt64 damage')
  s=command('pure-acceptance','acceptance');assert s['result']['passed']>=70;passed('70 pure acceptance checks on IL2CPP')
  settings_open('audio')
  # Persist all three independent gains, including a live pooled SFX.
  command('effect-loop','effect','on');time.sleep(1)
  s=slider('SldVolume',.62,'master-62');assert abs(s['volume']-.62)<.02
  s=slider('SldMusic',.35,'music-35');assert abs(s['music']-.35)<.02
  music=[a for a in s['sources'] if a['clip'] and not a['effect'] and a['isPlaying']]
  assert music and max(a['volume'] for a in music)<=.65*.36,music
  s=slider('SldEffects',.27,'effects-27');audio_effects(s,.27)
  s=slider('SldEffects',0,'effects-zero');audio_effects(s,0)
  s=slider('SldEffects',.27,'effects-restored');audio_effects(s,.27)
  passed('master, BGM, existing pooled SFX gains and SFX zero')
  # Reopening returns to top, making mute reachable by real input.
  settings_open('mute')
  tap('BtnMute',state('mute-controls'));s=command('muted')['state'];assert s['muted'] and s['volume']==0
  s=slider('SldVolume',.73,'muted-master');assert s['muted'] and s['volume']==0
  settings_open('unmute');tap('BtnMute',state('unmute-controls'));s=command('unmuted')['state'];assert not s['muted'] and abs(s['volume']-.73)<.02
  assert abs(s['effects']-.27)<.02 and abs(s['music']-.35)<.02
  passed('mute keeps chosen channel gains and restores master')
  command('effect-loop-off','effect','off')
  s=toggle('PowerSave',True);assert s['powerSave'] and s['fps']==30
  s=toggle('LowSpec',True);assert s['lowSpec'] and s['fps']==30
  s=toggle('PowerSave',False);assert not s['powerSave'] and s['fps']==60 and s['lowSpec']
  s=toggle('KeepAwake',True);assert s['keepAwake'] and s['sleepTimeout']==-1
  s=toggle('KeepAwake',False);assert not s['keepAwake'] and (s['sleepTimeout']==-2 or s['sleepTimeout']>=0)
  passed('independent power-save/low-spec and keep-awake flags')
  s=toggle('DamageText',True);command('damage-fixture','fixture','on');time.sleep(.35)
  assert command('damage-visible')['state']['hits']
  s=toggle('DamageText',False);assert not s['damage'] and not s['hits']
  s=command('damage-stays-off')['state'];assert not s['hits']
  command('damage-fixture-off','fixture','off');s=toggle('DamageText',True)
  passed('damage off clears active hits and blocks new ones')
  toggle('ScreenShake',True);base=command('shake-base')['state']['camera'];command('shake-start','shake');time.sleep(.2)
  moving=command('shake-moving')['state']['camera'];assert moving!=base,(base,moving)
  s=toggle('ScreenShake',False);assert all(abs(a-b)<.0001 for a,b in zip(s['camera'],base)),(s['camera'],base)
  command('shake-rejected','shake');time.sleep(.2);s=command('shake-stopped')['state'];assert s['camera']==base
  passed('screen-shake off restores camera and rejects new shake')
  toggle('HideItem',False);close('loot-visible');s=command('grant-visible','loot');assert s['result']['ok'];time.sleep(1)
  s=command('loot-visible-state')['state'];assert s['toast'] and ('장비' in s['toast']);before=s['equipment']
  settings_open('hide-loot');toggle('HideItem',True);close('loot-hidden');s=command('grant-hidden','loot');assert s['result']['ok'];time.sleep(1)
  s=command('loot-hidden-state')['state'];assert s['equipment']==before+1 and s['toast'] is None
  passed('equipment still stored while acquisition toast hidden')
  settings_open('persist');toggle('KeepAwake',True);toggle('PowerSave',True)
  close('persist');before=command('before-relaunch')['state']
  after=launch('persistence-relaunch');pause(True,'persistence-pause')
  for key in ['style','master','music','effects','muted','powerSave','lowSpec','keepAwake','sleepTimeout','hideItem','shake','damage','fps']:assert before[key]==after[key],(key,before[key],after[key])
  passed('settings survive force-stop and fresh launch')
  run('shell','input','keyevent','3');time.sleep(1);run('shell','monkey','-p',PACKAGE,'-c','android.intent.category.LAUNCHER','1');time.sleep(2)
  after=command('after-background')['state'];assert after['keepAwake'] and after['fps']==30
  settings_open('back-close');back();assert not command('back-closed')['state']['settingsOpen']
  settings_open('outside-close');s=command('outside-bounds')['state'];b=s['panel'];run('shell','input','tap','8',str(round(b['y']+b['height']/2)));time.sleep(.3);assert not command('outside-closed')['state']['settingsOpen']
  passed('background return, Android back and outside-dismiss')
 finally:
  command('restore-gold','amount',str(original_gold))
  (OUT/'functional-results.json').write_text(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(dict(passed=len(checks),checks=checks),ensure_ascii=False),flush=True)

if __name__=='__main__':main()
