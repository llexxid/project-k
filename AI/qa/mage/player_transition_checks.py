"""Actual Android button release across an explicitly declared QA wave transition."""
import importlib.util
import json
from pathlib import Path
import sys
import threading
import time

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('mage_device', Path(__file__).with_name('mage-device.py'))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
report = []
for attempt in range(3):
    tag = f'boundary-{attempt}'
    m.command(tag + '-main-stage', 'stage', stage=0x20002000A)
    time.sleep(1.5)
    m.command(tag + '-tickets', 'dungeon-fixture')
    m.tap('BtnDungeon', m.state(tag + '-main'))
    time.sleep(.7)
    m.tap('DungeonCard_Gold', m.state(tag + '-card'))
    m.tap('Row_01', m.state(tag + '-row'))
    ui = m.state(tag + '-ready')
    assert (ui['width'], ui['height']) == (1080, 2316)
    button = next(c['bounds'] for c in ui['controls'] if c['name'] == 'EnterButton')
    x, y = str(round(button['x'] + button['width'] / 2)), str(round(button['y'] + button['height'] / 2))
    began = time.monotonic()
    thread = threading.Thread(target=m.run, args=('shell', 'input', 'swipe', x, y, x, y, '2000'))
    thread.start()
    time.sleep(1.7)
    transitioning = m.command(tag + '-transition', 'stage', stage=0x20003000A)['state']
    observed_at = time.monotonic() - began
    thread.join()
    assert transitioning['runState'] == 'Transitioning', transitioning['runState']
    for index in range(15):
        after = m.command(tag + '-after-' + str(index))['state']
        if after['stage'] >> 28 == 0x21 and after['runState'] == 'Running':
            break
        time.sleep(.5)
    else:
        raise AssertionError((tag, 'Entry did not complete', after['stage'], after['runState']))
    assert after['GoldTickets'] == 1
    m.shot(tag + '-entered')
    report.append({'attempt': attempt, 'commandSnapshotState': transitioning['runState'],
                   'observedAfterTouchStartSeconds': observed_at, 'holdMilliseconds': 2000,
                   'enteredGold': True, 'ticketsUsed': 1, 'runtimeError': after['lastError']})
    (m.OUT / 'transition-entry-report.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report[-1]), flush=True)
    m.command(tag + '-return', 'return')
    time.sleep(1.5)
