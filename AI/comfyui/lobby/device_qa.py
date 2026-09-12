"""ADB helpers for the opt-in Lobby QA build. Never modifies production app data."""
import argparse
import json
import os
import re
from pathlib import Path
import subprocess
import time

PACKAGE = 'com.isolatedyouth.idlekingdomrpg.lobbyqa'
OUTPUT = Path(os.environ.get('LOBBY_QA_OUTPUT', 'Recordings/LobbyRevision5/Device'))
ADB = os.environ.get('LOBBY_ADB', 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe')


class Device:
    def __init__(self, serial):
        self.serial = serial
        OUTPUT.mkdir(parents=True, exist_ok=True)

    def run(self, *args, check=True, data=None):
        return subprocess.run([ADB, '-s', self.serial, *args], input=data, capture_output=True, check=check)

    def read_result(self, identifier):
        result = self.run('exec-out', 'run-as', PACKAGE, 'cat', f'files/lobby-{identifier}.json', check=False)
        if result.returncode:
            return None
        try:
            payload = json.loads(result.stdout)
        except (json.JSONDecodeError, UnicodeDecodeError):
            return None  # Missing or not-yet-flushed device output.
        (OUTPUT / f'{identifier}.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding='utf-8')
        return payload

    def command(self, identifier, action, value='', seconds=30, wait=15):
        if not re.fullmatch(r'[A-Za-z0-9_-]+', identifier):
            raise ValueError('Use a simple identifier for local QA files.')
        self.run('shell', 'run-as', PACKAGE, 'rm', '-f', f'files/lobby-{identifier}.json')
        payload = json.dumps(dict(id=identifier, action=action, value=value, seconds=seconds)).encode()
        self.run('shell', f"run-as {PACKAGE} sh -c 'cat > files/lobby-command.pending && mv files/lobby-command.pending files/lobby-command.json'", data=payload)
        deadline = time.monotonic() + wait
        while time.monotonic() < deadline:
            result = self.read_result(identifier)
            if result is not None:
                return result
            time.sleep(.4)
        raise TimeoutError(f'No device result for {identifier}.')

    def screenshot(self, identifier):
        path = OUTPUT / f'{identifier}.png'
        path.write_bytes(self.run('exec-out', 'screencap', '-p').stdout)
        return str(path.resolve())

    def tap_control(self, name, state):
        rect = next(rect for rect in state['controls'] if rect['name'] == name)
        x, y = round(rect['x'] + rect['width'] / 2), round(rect['y'] + rect['height'] / 2)
        self.run('shell', 'input', 'tap', str(x), str(y))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--serial', required=True)
    parser.add_argument('action')
    parser.add_argument('identifier')
    parser.add_argument('--value', default='')
    parser.add_argument('--seconds', type=float, default=30)
    args = parser.parse_args()
    device = Device(args.serial)
    if args.action == 'screenshot':
        print(device.screenshot(args.identifier))
    elif args.action == 'read':
        print(json.dumps(device.read_result(args.identifier), ensure_ascii=False, indent=2))
    else:
        print(json.dumps(device.command(args.identifier, args.action, args.value, args.seconds,
                                        wait=args.seconds + 10 if args.action == 'measure' else 15), ensure_ascii=False, indent=2))
