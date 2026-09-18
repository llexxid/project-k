"""Wall-clock Android play sessions; never counts builds, background or paused time.

Run only against the isolated diagnostic profile after backing up device saves.
Actual interactions are performed separately through adb and recorded in actions.jsonl.
The sampler reads the existing ten-second game telemetry without changing game state.
"""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import time

ADB = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
PACKAGE = 'com.isolatedyouth.idlekingdomrpg.lobbyqa'


def adb(*args):
    return subprocess.run([ADB, '-s', os.environ.get('HUD_QA_SERIAL', 'R3CN815LZ9L'), *args],
                          capture_output=True, timeout=20, check=True).stdout


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def record_action(directory, description):
    with (directory / 'actions.jsonl').open('a', encoding='utf8') as f:
        f.write(json.dumps({'utc': utc(), 'description': description}, ensure_ascii=False) + '\n')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('directory', type=Path)
    parser.add_argument('--seconds', type=float, default=1800)
    parser.add_argument('--action')
    args = parser.parse_args()
    args.directory.mkdir(parents=True, exist_ok=True)
    if args.action:
        record_action(args.directory, args.action)
        return
    started = utc()
    previous = None
    eligible = 0.0
    samples = 0
    output = args.directory / 'session.json'
    if output.exists():
        raise RuntimeError('Choose a new directory; session evidence is immutable.')
    while eligible < args.seconds:
        now = time.monotonic()
        sample = {'utc': utc(), 'monotonic': now}
        try:
            state = json.loads(adb('exec-out', 'run-as', PACKAGE, 'cat', 'files/balance-live.json'))
            activities = adb('shell', 'dumpsys', 'activity', 'activities').decode('utf8', errors='replace')
            resumed = [line.strip() for line in activities.splitlines()
                       if 'mResumedActivity' in line or 'topResumedActivity=' in line]
            sample.update(state=state, resumed=resumed)
            valid = any(PACKAGE in line for line in resumed) and state['timeScale'] == 1 and state['stage'] != 0
            # The probe writes at ten-second intervals. A fresh game timestamp at both
            # ends proves the app actually advanced; stale/paused or restart gaps do not count.
            if previous and valid and previous['valid']:
                game_delta = state['time'] - previous['gameTime']
                wall_delta = now - previous['monotonic']
                if 0 < game_delta < 25 and wall_delta < 25:
                    eligible += min(game_delta, wall_delta)
            previous = {'valid': valid, 'monotonic': now, 'gameTime': state['time']}
            sample['valid'] = valid
        except Exception as error:
            sample['error'] = str(error)
            previous = None
        samples += 1
        sample['eligibleSeconds'] = eligible
        with (args.directory / 'samples.jsonl').open('a', encoding='utf8') as f:
            f.write(json.dumps(sample, ensure_ascii=False) + '\n')
        report = {'startedUtc': started, 'lastUtc': utc(), 'eligibleSeconds': eligible,
                  'requiredSeconds': args.seconds, 'samples': samples, 'complete': eligible >= args.seconds,
                  'method': 'Foreground Android gameplay at timeScale 1; conservative min(game, wall) intervals; builds/restarts/paused time excluded.'}
        output.write_text(json.dumps(report, indent=2), encoding='utf8')
        if samples == 1 or samples % 6 == 0 or report['complete']:
            print(json.dumps(report), flush=True)
        if eligible < args.seconds:
            time.sleep(10)


if __name__ == '__main__':
    main()
