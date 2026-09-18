"""Small, auditable real-touch companion to player_session.py."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from player_session import record_action


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['view', 'balance', 'tap', 'xy', 'swipe', 'back', 'command', 'launch'])
    parser.add_argument('tag')
    parser.add_argument('args', nargs='*')
    args = parser.parse_args()
    path = Path(__file__).with_name('mage-device.py')
    spec = importlib.util.spec_from_file_location('mage_device', path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    record_action(m.OUT, ' '.join([args.action, args.tag, *args.args]))
    if args.action == 'launch':
        m.launch(args.tag)
    elif args.action == 'command':
        value = m.command(args.tag, args.args[0], **(json.loads(args.args[1]) if len(args.args) > 1 else {}))
        print(json.dumps(value.get('result'), ensure_ascii=False))
    elif args.action == 'tap':
        s = m.state(args.tag + '-before')
        name = args.args[0]
        controls = [x for x in s['controls'] if x['name'] == name and x['interactable']]
        if len(args.args) == 2:
            controls = [controls[int(args.args[1])]]
        m.tap(name, dict(s, controls=controls))
    elif args.action == 'xy':
        m.run('shell', 'input', 'tap', *args.args)
        time.sleep(.5)
    elif args.action == 'swipe':
        m.run('shell', 'input', 'swipe', *args.args)
        time.sleep(.5)
    elif args.action == 'back':
        m.back()
    if args.action == 'balance' or args.action == 'command':
        s = m.command(args.tag + '-balance')['state']
        print(json.dumps({k: v for k, v in s.items() if k in ['stage','runState','MainStage','Kills','AccountLevel','Wallet','AttackLevel','HealthLevel','RubyGoldLevel','RubyExpLevel','ReincarnationLevel','GoldTickets','RubyTickets','GoldDungeonClear','RubyDungeonClear','equipment','pending','reserve','claims','manualAuto','party','mage','slot','lastError','time','timeScale','memory','meanFrameMs','maxFrameMs']}, ensure_ascii=False))
    else:
        s = m.state(args.tag)
        m.shot(args.tag)
        print(json.dumps({'main': s['main'], 'stage': s['stage'], 'goals': s['goals'],
                          'labels': [(x['name'], x['text'], x['isTextTruncated']) for x in s['labels']],
                          'controls': [(x['name'], x['interactable'], round(x['bounds']['x'] + x['bounds']['width']/2), round(x['bounds']['y'] + x['bounds']['height']/2))
                                       for x in s['controls'] if x['name'] != 'HPBar(Clone)']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
