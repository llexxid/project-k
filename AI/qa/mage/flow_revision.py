"""Physical Android checks for chapter continuation and unscaled dungeon return."""
import json
import os
import sys
import time
from importlib.machinery import SourceFileLoader
from pathlib import Path

sys.dont_write_bytecode = True
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/FoundationRevision/FlowAfter')
m = SourceFileLoader('flow_mage', str(Path(__file__).with_name('mage-device.py'))).load_module()


def settle(name, expected, timeout=18):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        s = m.command(name)['state']
        if s['stage'] == expected and s['runState'] == 'Running':
            return s
        time.sleep(.5)
    raise AssertionError((name, expected, s['stage'], s['runState']))


def continuation():
    report = m.command('stage-acceptance', 'stage-acceptance')['result']
    assert report['passed'], report
    m.command('flow-fixture', 'combat-fixture', value=1, enhance=0, stage=0x20003000B)
    settle('flow-boss-ready', 0x20003000B)
    m.command('flow-auto', 'boss-auto', value=1)
    m.command('flow-clear3', 'stage-clear')
    s = settle('flow-enter4', 0x200040001)
    time.sleep(3)
    s = m.command('flow-bandits')['state']
    assert s['bossAuto'] and all('BANDIT' in mob['type'] for mob in s['monsters']), s['monsters']
    m.shot('stage4-bandits')
    m.command('flow-boss4', 'stage', stage=0x20004000B)
    settle('flow-boss4-ready', 0x20004000B)
    before = m.command('flow-reward-before')['state']['Wallet']
    m.command('flow-clear4', 'stage-clear')
    first = settle('flow-enter5', 0x200050001)['Wallet']
    m.command('flow-replay4', 'stage', stage=0x20004000B)
    settle('flow-replay4-ready', 0x20004000B)
    m.command('flow-reclear4', 'stage-clear')
    second = settle('flow-reenter5', 0x200050001)['Wallet']
    (m.OUT / 'first-clear-wallets.json').write_text(json.dumps(dict(before=before, first=first, repeat=second), indent=2), encoding='utf8')
    print(json.dumps(dict(acceptance=report['count'], chapter=5, wallets=dict(before=before, first=first, repeat=second))), flush=True)


def dungeons():
    m.command('flow-midgame', 'midgame')
    settle('flow-main-ready', 0x20002000A)
    for kind, stage in [('gold', 0x210010001), ('ruby', 0x220010001)]:
        for result in ('clear', 'defeat'):
            prefix = f'flow-{kind}-{result}'
            m.command(prefix + '-tickets', 'dungeon-fixture')
            entered = m.command(prefix + '-enter', 'dungeon', stage=stage)['result']
            assert entered['accepted'], entered
            current = settle(prefix + '-ready', stage)
            time.sleep(1)
            m.command(prefix + '-result', 'stage-' + result)
            first = m.command(prefix + '-timer')['state']
            assert first['runState'] == ('ResultPending' if result == 'clear' else 'DefeatPending'), first['runState']
            assert 0 < first['returnRemaining'] <= 6 and first['returnDuration'] == 6, first['returnRemaining']
            m.shot(prefix + '-start')
            time.sleep(1.2)
            second = m.command(prefix + '-timer2')['state']
            assert 0 < second['returnRemaining'] < first['returnRemaining'], second['returnRemaining']
            m.shot(prefix + '-end')
            returned = settle(prefix + '-returned', 0x20002000A, 12)
            assert returned['GoldTickets'] == current['GoldTickets'] and returned['RubyTickets'] == current['RubyTickets'], 'Auto-return consumed ticket'
            assert returned['timeScale'] == 1, returned['timeScale']
            m.shot(prefix + '-main')
            print(json.dumps(dict(kind=kind, result=result, countdown=[first['returnRemaining'], second['returnRemaining']], returned=returned['stage'], tickets=[returned['GoldTickets'], returned['RubyTickets']])), flush=True)


if __name__ == '__main__':
    {'continuation': continuation, 'dungeons': dungeons}[sys.argv[1]]()
