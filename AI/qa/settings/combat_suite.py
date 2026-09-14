"""Short actual-combat sample after restoring the isolated QA app's presentation defaults."""
from settings_checks import *

def main():
 settings_open('combat-defaults')
 for slider_name in ['SldVolume','SldMusic','SldEffects']:slider(slider_name,1)
 for name,on in [('PowerSave',False),('LowSpec',False),('DamageText',True),('ScreenShake',True),('HideItem',False),('KeepAwake',True)]:toggle(name,on)
 style(0,'combat-standard');tap('BtnSaveClose',state('combat-close'))
 command('combat-fixture-off','fixture','off');command('combat-effect-off','effect','off')
 before=pause(False,'combat-start')['state']
 result=measure('combat-after',30)
 after=pause(True,'combat-stop')['state'];shot('combat-after')
 assert after['Kills']>before['Kills'],(before['Kills'],after['Kills'])
 assert not after['lastError'],after['lastError']
 assert result['fps']>50,result
 (OUT/'combat-progress.json').write_text(json.dumps(dict(before=before,after=after),ensure_ascii=False,indent=2),encoding='utf-8')
 settings_open('combat-cleanup');toggle('KeepAwake',False);tap('BtnSaveClose',state('combat-cleanup-close'))
 pause(False,'combat-resumed')
 print('PASS actual combat: '+str(after['Kills']-before['Kills'])+' kills / 30 seconds; '+str(round(result['fps'],2))+' FPS',flush=True)

if __name__=='__main__':main()
