"""Physical-device visual and functional checks for the refined spell assets."""
import json,os,sys,time
from importlib.machinery import SourceFileLoader
from pathlib import Path
sys.dont_write_bytecode=True
os.environ.setdefault('HUD_QA_OUTPUT','Recordings/FoundationRevision/VfxAfter')
m=SourceFileLoader('vfx_device',str(Path(__file__).with_name('mage-device.py'))).load_module()

def ready(tag,skill,bloom=False):
    m.command(tag+'-resume','timescale',value=100)
    m.command(tag+'-close','mage-close')
    m.command(tag+'-fixture','mage-fixture',enhance=0,awaken=10 if bloom else 0,bloom=bloom)
    m.command(tag+'-stage','stage',stage=0x20003000b if bloom and skill==1 else 0x20003000a)
    time.sleep(2)
    m.command(tag+'-equip','mage-equip',value=skill,awaken=0)
    for retry in range(100):
        s=m.command(tag+'-ready-'+str(retry))['state']
        if not s['mageCooldown'][0]['casting'] and s['mageCooldown'][0]['ratio']==0:return
        time.sleep(.3)
    raise TimeoutError(tag)

def cast(tag,skill,phase,bloom=False):
    ready(tag,skill,bloom)
    if skill==7:m.command(tag+'-injure','combat-hp',value=0)
    m.command(tag+'-slow','timescale',value=25)
    result=m.command(tag+'-cast','mage-cast',value=skill,captureMs=phase)
    assert result['result']['accepted'],(tag,result)
    time.sleep(phase/250+.25)
    state=m.command(tag+'-active')['state'];m.shot(tag)
    assert state['mageVisuals'],tag
    for v in state['mageVisuals']:
        for r in v['renderers']:
            ground=v['name'].split('(')[0] in ('GroundTelegraph','SanctuaryHeal','Sanctuary','VenomMist','VoidRift','FireTornado','IceBloomWarning') or (v['name'].startswith('MeteorCrater') and r['name']=='Layer0')
            assert (r['layer']=='Default' and r['order']<2) if ground else (r['layer']=='CombatVFX' and r['order']>=20),(tag,v,r)
    m.command(tag+'-run','timescale',value=100)
    time.sleep(8)
    final=m.command(tag+'-done')['state'];events=final['mageEvents']
    assert any(e['kind']=='end' for e in events),(tag,events)
    assert any(e['kind']==('heal' if skill==7 else 'damage') and e['amount']>0 for e in events),(tag,events)
    print(json.dumps(dict(tag=tag,visuals=len(state['mageVisuals']),events=len(events))),flush=True)
    return dict(tag=tag,active=state,events=events)

def spells():
    rows=[]
    for skill,phase in [(0,210),(1,210),(2,400),(3,360),(4,380),(5,630),(7,540),(8,530),(9,380)]:
        rows.append(cast('vfx-'+str(skill),skill,phase))
        (m.OUT/'spell-report.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
    for skill,phase in [(0,2020),(1,620)]:
        rows.append(cast('vfx-bloom-'+str(skill),skill,phase,True))
        (m.OUT/'spell-report.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')

if __name__=='__main__':
    try:
        if sys.argv[1]=='spells':spells()
        else:cast(sys.argv[1],int(sys.argv[2]),int(sys.argv[3]),len(sys.argv)>4)
    finally:m.command('vfx-final-speed','timescale',value=100)
