"""Natural-player device review: observed controls, real ADB touches, immutable evidence.

Requires TitleLobbyDeviceBuild.BuildForPlayerJourney. Never submits game fixture commands.
"""
import argparse
import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import sys
import time

sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
os.environ.setdefault('HUD_QA_OUTPUT', 'Recordings/NewPlayer20260922/Run')
os.environ.setdefault('HUD_QA_SERIAL', 'R3CN815LZ9L')
spec = importlib.util.spec_from_file_location('mage_device', Path(__file__).with_name('mage-device.py'))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


def record(action, details):
    with (m.OUT / 'actions.jsonl').open('a', encoding='utf8') as stream:
        stream.write(json.dumps({'utc': dt.datetime.now(dt.timezone.utc).isoformat(),
                                 'action': action, 'details': details}, ensure_ascii=False) + '\n')


def observe(tag, screenshot=True):
    hud = m.state(tag + '-hud')
    balance = m.command(tag + '-balance')['state']
    if screenshot:
        m.shot(tag)
    result = {k: balance.get(k) for k in ['stage', 'runState', 'AccountLevel', 'AttackLevel', 'HealthLevel',
               'ReincarnationLevel', 'cp', 'Wallet', 'GoldTickets', 'RubyTickets', 'GoldDungeonClear',
               'RubyDungeonClear', 'equipment', 'pending', 'manualAuto', 'slot', 'timeScale', 'lastError']}
    result['party'] = [{k: p.get(k) for k in ['PlayerIndex', 'job', 'atk', 'hp', 'maxHP']} for p in balance.get('party') or []]
    result['goals'] = hud['goals']
    result['labels'] = [(x['name'], x['text'], x['isTextTruncated']) for x in hud['labels']
                        if x['text'] and x['name'] not in ['HPBar(Clone)', 'DamageText(Clone)', 'Item_DamageText(Clone)', 'CdText']]
    result['controls'] = [(i, x['name'], x['interactable'],
                           round(x['bounds']['x'] + x['bounds']['width'] / 2),
                           round(x['bounds']['y'] + x['bounds']['height'] / 2))
                          for i, x in enumerate(hud['controls']) if x['name'] != 'HPBar(Clone)']
    print(json.dumps(result, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['view', 'tap', 'xy', 'swipe', 'back', 'profile'])
    parser.add_argument('tag')
    parser.add_argument('args', nargs='*')
    args = parser.parse_args()
    record(args.action, [args.tag, *args.args])
    if args.action == 'tap':
        hud = m.state(args.tag + '-before')
        options = [x for x in hud['controls'] if x['name'] == args.args[0] and x['interactable']]
        if len(args.args) > 1:
            options = [options[int(args.args[1])]]
        m.tap(args.args[0], dict(hud, controls=options))
    elif args.action in ['xy', 'swipe']:
        m.run('shell', 'input', 'tap' if args.action == 'xy' else 'swipe', *args.args)
        time.sleep(1)
    elif args.action == 'back':
        m.back()
    elif args.action == 'profile':
        folder = f'/sdcard/Android/data/{m.PACKAGE}/files/progression-local-v1'
        names = m.run('shell', 'run-as', m.PACKAGE, 'ls', '-t', folder).stdout.decode().splitlines()
        profiles = []
        for name in names:
            if not name.endswith('.json') or len(name) != 69:
                continue
            data = m.run('exec-out', 'run-as', m.PACKAGE, 'cat', folder + '/' + name).stdout
            state = json.loads(data)
            (m.OUT / (args.tag + '-' + name)).write_bytes(data)
            profiles.append({k: state.get(k) for k in ['Revision', 'AccountLevel', 'MainStage', 'HighestMainClear',
                'ReincarnationCount', 'ReincarnationLevel', 'CycleBossStage', 'CycleStartedUtc', 'LastReincarnationUtc',
                'ReincarnationsToday', 'AttackLevel', 'HealthLevel', 'Wallet', 'GoldDungeonClear', 'RubyDungeonClear']})
        print(json.dumps(profiles, ensure_ascii=False))
        return
    observe(args.tag)


if __name__ == '__main__':
    main()
