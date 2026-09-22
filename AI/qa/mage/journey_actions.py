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
if not os.environ.get('HUD_QA_SERIAL'):
    raise RuntimeError('Set HUD_QA_SERIAL to the connected test device before running journey tools.')
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
    result = {k: balance.get(k) for k in ['stage', 'runState', 'bossAuto', 'AccountLevel', 'AttackLevel', 'HealthLevel',
               'ReincarnationLevel', 'cp', 'Wallet', 'GoldTickets', 'RubyTickets', 'GoldDungeonClear',
               'RubyDungeonClear', 'equipment', 'pending', 'manualAuto', 'slot', 'timeScale', 'lastError']}
    result['party'] = [{k: p.get(k) for k in ['PlayerIndex', 'job', 'atk', 'hp', 'maxHP']} for p in balance.get('party') or []]
    result['goals'] = hud['goals']
    result['labels'] = [(x['text'], round(x['bounds']['x'] + x['bounds']['width']/2), round(x['bounds']['y'] + x['bounds']['height']/2), x['isTextTruncated']) for x in hud['labels']
                        if x['text'] and x['name'] not in ['HPBar(Clone)', 'DamageText(Clone)', 'Item_DamageText(Clone)', 'CdText']]
    result['controls'] = [(i, x['name'], x['interactable'],
                           round(x['bounds']['x'] + x['bounds']['width'] / 2),
                           round(x['bounds']['y'] + x['bounds']['height'] / 2))
                          for i, x in enumerate(hud['controls']) if x['name'] != 'HPBar(Clone)']
    print(json.dumps(result, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['view', 'tap', 'label', 'claim', 'panelclaim', 'xy', 'swipe', 'back', 'profile'])
    parser.add_argument('tag')
    parser.add_argument('args', nargs='*')
    args = parser.parse_args()
    record(args.action, [args.tag, *args.args])
    if args.action == 'panelclaim':
        for i in range(20):
            hud = m.state(args.tag + '-claim-' + str(i))
            labels = sorted([x for x in hud['labels'] if x['text'] == '받기' and 500 < x['bounds']['y'] < 1880], key=lambda x:x['bounds']['y'])
            if not labels or any(x['name'] in ['BtnDeathYes', 'BtnDone'] for x in hud['controls']):
                break
            record('panel-claim', {'tag':args.tag, 'index':i, 'bounds':labels[0]['bounds']})
            m.tap('claim', dict(hud, controls=[dict(name='claim', interactable=True, bounds=labels[0]['bounds'])]))
            m.shot(args.tag + '-claim-' + str(i))
    elif args.action == 'claim':
        for i in range(20):
            hud = m.state(args.tag + '-claim-' + str(i))
            goals = [g for g in hud['goals'] if g['visible'] and '보상 받기' in g['action']]
            if len(goals) != 1 or any(x['name'] in ['BtnDeathYes', 'BtnDone'] for x in hud['controls']):
                break
            record('guide-claim', {'tag':args.tag, 'index':i, 'description':goals[0]['description']})
            m.tap('Body', hud)
            m.shot(args.tag + '-claim-' + str(i))
    elif args.action == 'tap':
        hud = m.state(args.tag + '-before')
        options = [x for x in hud['controls'] if x['name'] == args.args[0] and x['interactable']]
        if len(args.args) > 1:
            options = [options[int(args.args[1])]]
        m.tap(args.args[0], dict(hud, controls=options))
    elif args.action == 'label':
        hud = m.state(args.tag + '-before')
        matches = [x for x in hud['labels'] if x['text'] == args.args[0]]
        if len(args.args) > 1:
            matches = [matches[int(args.args[1])]]
        assert len(matches) == 1, (args.args, len(matches))
        b = matches[0]['bounds']
        assert 0 <= b['y'] < hud['height'], 'Label is outside the screen; scroll first.'
        target = dict(name='observed-label', interactable=True, bounds=b)
        m.tap('observed-label', dict(hud, controls=[target]))
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
