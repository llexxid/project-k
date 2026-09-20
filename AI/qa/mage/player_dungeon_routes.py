"""Complete every dungeon through actual buttons, using declared late-game QA stats."""
import importlib.util
import json
import sys
import time
from decimal import Decimal
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('mage_device', Path(__file__).with_name('mage-device.py'))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
ACCOUNT = '70d202bade6b4b6b5353a895c319576dadedbb76b47f1b826a33eb4abcccaf9d'


def saved():
    path = f'/sdcard/Android/data/{m.PACKAGE}/files/progression-local-v1/{ACCOUNT}.json'
    return json.loads(m.run('exec-out', 'run-as', m.PACKAGE, 'cat', path).stdout)


def main():
    report = []
    for kind in ('Gold', 'Ruby'):
        for difficulty in range(1, 6):
            tag = f'{kind.lower()}-{difficulty}'
            # Unlocks/tickets are explicitly injected. Entry, combat, rewards and return are real.
            m.command(tag + '-access', 'dungeon-fixture')
            time.sleep(.5)
            m.tap('BtnDungeon', m.state(tag + '-main'))
            time.sleep(.7)
            m.tap('DungeonCard_' + kind, m.state(tag + '-card'))
            if difficulty >= 4:
                m.run('shell', 'input', 'swipe', '300', '1390', '300', '1060', '600')
                time.sleep(.6)
            m.tap(f'Row_{difficulty:02}', m.state(tag + '-select'))
            ui = m.state(tag + '-description')
            m.shot(tag + '-description')
            descriptions = [x for x in ui['labels'] if x['name'] == 'Description' and ('매일' in x['text'])]
            assert descriptions and not any(x['isTextTruncated'] for x in descriptions), descriptions
            before = saved()
            m.tap('EnterButton', ui)
            began = time.monotonic()
            for index in range(42):
                state = m.command(tag + '-combat-' + str(index))['state']
                assert not state['lastError'], state['lastError']
                if state['runState'] == 'ResultPending':
                    break
                time.sleep(3)
            else:
                raise AssertionError((tag, 'No clear result', state['stage'], state['runState']))
            after = saved()
            expected = (int(Decimal(450) * Decimal('1.6') ** (difficulty - 1)) * 20 if kind == 'Gold'
                        else int(Decimal(50) * Decimal('1.35') ** (difficulty - 1))
                        + (25 * difficulty if f'ruby-first:{difficulty}' not in before['Claims'] else 0))
            paid = after['LastDungeonGold' if kind == 'Gold' else 'LastDungeonRuby']
            assert paid == expected, (tag, paid, expected)
            assert after[kind + 'Tickets'] == before[kind + 'Tickets'] - 1
            result = m.state(tag + '-result')
            m.shot(tag + '-result')
            m.tap('BtnExit', result)
            time.sleep(1)
            returned = m.command(tag + '-returned')['state']
            assert returned['stage'] >> 28 == 0x20 and returned['runState'] == 'Running', returned['stage']
            row = {'dungeon': tag, 'combatWallSeconds': time.monotonic() - began,
                   'reward': paid, 'ticketsUsed': 1, 'returnedToMain': True,
                   'description': descriptions[0]['text']}
            report.append(row)
            (m.OUT / 'full-dungeon-routes.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
            print(json.dumps(row, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
