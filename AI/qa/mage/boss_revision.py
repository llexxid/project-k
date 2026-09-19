"""Real Android combat regression for health, boss reach and authored shaman summons."""
import json
import os
import sys
import time
from collections import Counter
from importlib.machinery import SourceFileLoader
from pathlib import Path

sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/FoundationRevision/BossAfter')
m = SourceFileLoader('boss_mage', str(Path(__file__).with_name('mage-device.py'))).load_module()


def fixture(chapter, elite=True):
    return m.command('boss-fixture', 'combat-fixture', value=int(elite), enhance=0,
                     stage=0x200000000 + chapter * 0x10000 + 11)['state']


def health():
    rows = []
    for elite in (False, True):
        fixture(1, elite)
        result = m.command('health-' + str(elite), 'health-audit')['result']
        rows.append(result)
    (m.OUT / 'health-summary.json').write_text(json.dumps(rows, indent=2), encoding='utf8')
    print(json.dumps(rows), flush=True)


def solo():
    rows = []
    for chapter in (1, 2, 3):
        for index in (0, 1, 2):
            fixture(chapter)
            start = m.command(f'boss{chapter}-solo{index}', 'boss-solo', value=index)['state']
            samples = []
            for tick in range(14):
                time.sleep(.65)
                samples.append(m.command(f'boss{chapter}-solo{index}-{tick}')['state'])
            last = samples[-1]
            hp = last['party'][index]
            events = Counter(x['kind'] for x in last['combatEvents'])
            assert hp['hp'] < start['party'][index]['hp'], (chapter, index, hp, events)
            row = dict(chapter=chapter, job=hp['job'], hp=hp['hp'], maxHP=hp['maxHP'], events=events)
            rows.append(row)
            print(json.dumps(row), flush=True)
            m.shot(f'boss{chapter}-solo{index}')
    (m.OUT / 'boss-solo-summary.json').write_text(json.dumps(rows, indent=2), encoding='utf8')


def retarget():
    fixture(3)
    for tick in range(24):
        time.sleep(.65)
        before = m.command('retarget-before-' + str(tick))['state']
        if any(x['tauntOwner'] == 0 for x in before['monsters']):
            break
    assert any(x['tauntOwner'] == 0 for x in before['monsters']), before['monsters']
    m.command('retarget-remove-owner', 'boss-solo', value=1)
    time.sleep(7)
    after = m.command('retarget-after')['state']
    spear = after['party'][1]
    assert any(x['kind'] == 'monster-melee-hit' and x['targetId'] == spear['id']
               for x in after['combatEvents']), after['combatEvents']
    assert spear['hp'] < before['party'][1]['hp']
    m.shot('retarget-after')
    print('Boss retargeted after its taunt owner left combat.', flush=True)


def shamans():
    for chapter in (2, 3):
        m.command(f'shaman{chapter}-fixture', 'combat-fixture', value=0, enhance=0,
                  stage=0x200000000 + chapter * 0x10000 + 4)
        time.sleep(2)
        m.command(f'shaman{chapter}-slow', 'timescale', value=20)
        captured = False
        for tick in range(28):
            sample = m.command(f'shaman{chapter}-{tick}')['state']
            summons = [x for x in sample['combatEvents'] if x['kind'] == 'monster-summon']
            if summons and not captured:
                m.shot(f'shaman{chapter}-emerge')
                time.sleep(.8)
                m.shot(f'shaman{chapter}-settle')
                captured = True
            if any(x['kind'] == 'totem-hit' for x in sample['combatEvents']) and captured:
                break
            time.sleep(.25)
        m.command(f'shaman{chapter}-resume', 'timescale', value=100)
        assert captured and any(x['kind'] == 'totem-hit' for x in sample['combatEvents']), sample['combatEvents']
        print(json.dumps({'chapter': chapter, 'events': Counter(x['kind'] for x in sample['combatEvents'])}), flush=True)


if __name__ == '__main__':
    {'health': health, 'solo': solo, 'retarget': retarget, 'shamans': shamans}[sys.argv[1]]()
