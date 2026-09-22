"""Read-only wall/game-clock evidence for an Android progression journey."""
import datetime as dt
import json
from pathlib import Path
import subprocess
import time

ADB = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
SERIAL = 'R3CN815LZ9L'
PACKAGE = 'com.isolatedyouth.idlekingdomrpg.lobbyqa'
OUT = Path('Recordings/NewPlayer20260922/Run/Timing')


def read(*args):
    return subprocess.run([ADB, '-s', SERIAL, *args], capture_output=True, timeout=20, check=True).stdout


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    if (OUT / 'samples.jsonl').exists():
        raise RuntimeError('Do not overwrite journey evidence.')
    started = time.monotonic()
    start_utc = dt.datetime.now(dt.timezone.utc).isoformat()
    previous = None
    played = 0.0
    samples = 0
    while time.monotonic() - started < 14400 and not (OUT / 'stop').exists():
        now = time.monotonic()
        sample = {'utc': dt.datetime.now(dt.timezone.utc).isoformat(), 'monotonic': now}
        try:
            state = json.loads(read('exec-out', 'run-as', PACKAGE, 'cat', 'files/balance-live.json'))
            activity = read('shell', 'dumpsys', 'activity', 'activities').decode('utf8', errors='replace')
            foreground = any(PACKAGE in line and ('mResumedActivity' in line or 'topResumedActivity=' in line)
                             for line in activity.splitlines())
            valid = foreground and state['timeScale'] == 1 and state['stage'] != 0
            if previous and previous['valid'] and valid:
                game_delta = state['time'] - previous['game']
                wall_delta = now - previous['wall']
                if 0 < game_delta < 30 and wall_delta < 30:
                    played += min(game_delta, wall_delta)
            previous = {'valid': valid, 'game': state['time'], 'wall': now}
            sample.update(state=state, foreground=foreground, valid=valid)
        except Exception as error:
            sample['error'] = str(error)
            previous = None
        samples += 1
        sample['activePlaySeconds'] = played
        with (OUT / 'samples.jsonl').open('a', encoding='utf8') as stream:
            stream.write(json.dumps(sample, ensure_ascii=False) + '\n')
        status = {'startedUtc': start_utc, 'lastUtc': sample['utc'], 'samples': samples,
                  'activePlaySeconds': played, 'elapsedWallSeconds': time.monotonic() - started,
                  'method': 'Read-only telemetry. Foreground timeScale=1 advancement; min(game, wall). Tutorial pauses, restarts and stale intervals excluded from activePlaySeconds.'}
        (OUT / 'status.json').write_text(json.dumps(status, indent=2), encoding='utf8')
        if samples == 1 or samples % 6 == 0:
            print(json.dumps(status), flush=True)
        time.sleep(10)


if __name__ == '__main__':
    main()
