"""Foundation combat checks against the isolated Android diagnostic build."""
import json
import os
import sys
import time
from importlib.machinery import SourceFileLoader
from pathlib import Path

sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/FoundationRevision/DeviceCombat')
m = SourceFileLoader('foundation_mage', str(Path(__file__).with_name('mage-device.py'))).load_module()


def control():
    results = []
    for boss in (False, True):
        m.command('foundation-close', 'mage-close')
        m.command('foundation-fixture', 'combat-fixture', value=1, enhance=0,
                  stage=0x20003000b if boss else 0x20003000a)
        time.sleep(2)
        result = m.command('foundation-control-' + str(boss), 'combat-control')['result']
        assert result['boss'] == boss and result['passed'] == 10, result
        results.append(result)
        m.shot('foundation-control-' + str(boss))
    (m.OUT / 'control-summary.json').write_text(json.dumps(results, indent=2), encoding='utf8')


def iron_will():
    m.command('foundation-ironwill-fixture', 'combat-fixture', value=1, enhance=0, stage=0x20003000a)
    seen = False
    for index in range(30):
        snapshot = m.command('ironwill-' + str(index))['state']
        knight, combat = snapshot['party'][0], snapshot['combatParty'][0]
        if combat['ShieldHP'] > 0:
            assert knight['ratio'] > .5, knight
            m.shot('ironwill-shield-' + str(index))
            seen = True
            break
        time.sleep(.7)
    assert seen, 'Iron Will did not cast at high health within its 20-second cooldown.'


if __name__ == '__main__':
    {'control': control, 'iron-will': iron_will}[sys.argv[1]]()
