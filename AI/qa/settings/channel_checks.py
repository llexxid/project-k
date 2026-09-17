"""Real touch checks for the seven sound controls and tabbed device settings.

Run only with the isolated diagnostic APK. The handoff process restores the
pre-test user preferences after these checks.
"""
import json, time
from settings_checks import *

def main():
    settings_open('channels-open')
    expected={'SldVolume':.72,'SldMusic':.34,'SldEffects':.63,
              'SldGuard':.26,'SldMonsters':.18,'SldSpells':.48,'SldInterface':.31}
    for name,value in expected.items():slider(name,value,'channels-'+name)
    shot('channels-lower')
    tap('BtnSaveClose',state('channels-save'))
    launch('channels-relaunch')
    settings_open('channels-reopen')
    actual={}
    for name,value in expected.items():
        _,control=reveal(name,'sliders');actual[name]=control['value']
        assert abs(control['value']-value)<.02,(name,value,control['value'])
    slider('SldMonsters',0,'channels-monsters-zero')
    _,guard=reveal('SldGuard','sliders');assert abs(guard['value']-.26)<.02
    _,spells=reveal('SldSpells','sliders');assert abs(spells['value']-.48)<.02
    # Switching pages is a real touch; the old suite assumed every control was visible.
    tap('Tab0',state('channels-display-tab'))
    toggle('DamageText',False);toggle('DamageText',True)
    toggle('ScreenShake',False);toggle('ScreenShake',True)
    tap('Tab2',state('channels-device-tab'))
    assert toggle('PowerSave',True)['fps']==30
    assert toggle('LowSpec',True)['lowSpec']
    assert toggle('PowerSave',False)['fps']==60
    assert not toggle('LowSpec',False)['lowSpec']
    toggle('KeepAwake',True);toggle('KeepAwake',False)
    shot('channels-device-settings')
    back();assert not command('channels-back')['state']['settingsOpen']
    settings_open('channels-restore')
    for name in expected:slider(name,1)
    tap('BtnSaveClose',state('channels-restore-save'))
    report=dict(persisted=actual,independentMonsterMute=True,displayToggles=True,
                powerSaveFps=[30,60],lowSpecIndependent=True,keepAwake=True,androidBack=True)
    (OUT/'channel-checks.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(report,flush=True)

if __name__=='__main__':main()
