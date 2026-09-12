"""Real Android input and resolution checks for the isolated Lobby QA package."""
import argparse
import json
from pathlib import Path
import time

from device_qa import Device, OUTPUT, PACKAGE


def restart(device):
    device.run('shell', 'am', 'force-stop', '--user', '0', PACKAGE)
    device.run('shell', 'run-as', PACKAGE, 'rm', '-f', 'files/lobby-ready.json', 'files/lobby-command.json')
    device.run('shell', 'am', 'start', '-W', '--user', '0', '-n', PACKAGE + '/com.unity3d.player.UnityPlayerActivity')
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if device.read_result('ready'):
            return
        time.sleep(.5)
    raise TimeoutError('No fresh title after activity restart.')


def check_layout(state):
    screen = state['screen']
    safe = screen['safeArea']
    top = screen['height'] - safe['y'] - safe['height']
    failures = []
    for rect in state['controls']:
        if not (rect['x'] >= safe['x'] - 2 and rect['y'] >= top - 2
                and rect['x'] + rect['width'] <= safe['x'] + safe['width'] + 2
                and rect['y'] + rect['height'] <= screen['height'] - safe['y'] + 2):
            failures.append(rect['name'])
    return failures


def interactions(device, prefix):
    results = []

    def verify(condition, description):
        results.append(dict(passed=bool(condition), description=description))
        print(('PASS ' if condition else 'FAIL ') + description, flush=True)

    def state(name):
        return device.command(prefix + '-' + name, 'state')

    current = state('start')
    verify(not check_layout(current), 'Controls and title fit the physical safe area')
    device.tap_control('BtnLogin', current)
    current = state('popup')
    device.screenshot(prefix + '-popup')
    verify(current['popup'], 'Account button opens sign-in')
    device.run('shell', 'input', 'keyevent', '4')
    current = state('back')
    verify(not current['popup'], 'Android Back dismisses sign-in')
    device.run('shell', 'input', 'tap', str(current['screen']['width'] // 2), str(current['screen']['height'] // 3))
    current = state('background-tap')
    verify(current['popup'] and current['scene'] == 'title', 'Background tap respects the unauthenticated gate')
    device.run('shell', 'input', 'tap', '24', '120')
    current = state('outside')
    verify(not current['popup'], 'Outside tap dismisses sign-in')
    previous_language = current['language']
    device.tap_control('Language', current)
    current = state('language')
    language_capture = device.screenshot(prefix + '-language')
    from PIL import Image
    picture = Image.open(language_capture).convert('RGB')
    w, h = picture.size
    patch = picture.crop((int(w*.15), int(h*.15), int(w*.85), int(h*.50)))
    verify(len(patch.getcolors(patch.width * patch.height) or []) > 256,
           'Language popup keeps the illustrated background visible')
    verify(current['languagePopup'] and current['language'] == previous_language and not current['popup'], 'Globe opens language popup without changing language or signing in')
    verify(not check_layout(current), 'Language popup fits the physical safe area')
    device.tap_control('English' if previous_language == 'ko' else 'Korean', current)
    current = state('language-selected')
    verify(current['language'] != previous_language and not current['languagePopup'], 'Language choice applies and closes popup')
    device.tap_control('Language', current)
    current = state('language-current')
    verify(('English' if current['language'] == 'en' else '한국어') in current['currentLanguage'], 'Popup identifies the current language')
    options = [r for r in current['controls'] if r['name'] in ('Korean', 'English')]
    verify(len(options) == 2 and abs(options[0]['width'] - options[1]['width']) < 2,
           'Chosen language button restores its size when popup reopens')
    device.run('shell', 'input', 'keyevent', '4')
    current = state('language-back')
    verify(not current['languagePopup'], 'Android Back dismisses language popup')
    verify(current['sceneArt']['crystalIdleGlow'] and not current['sceneArt']['towerAttackExists'], 'Crystal idle glow exists and no tower attack objects remain')
    verify(not current['sceneArt']['effectsButtonExists'], 'Title has no effects button or hidden hit target')
    globe = next(r for r in current['controls'] if r['name'] == 'Language')
    verify(globe['x'] < current['screen']['width']*.2 and globe['y'] < current['screen']['height']*.2,
           'Globe is at the upper-left corner')
    current = device.command(prefix + '-settings-off', 'settings')
    if current['lowSpec']:
        device.tap_control('Row_LowSpec', current)
        current = state('settings-on-first')
    device.tap_control('Row_LowSpec', current)
    current = state('settings-disabled')
    verify(current['lowSpec'] and current['lowSpecToggle'] and not current['motion'],
           'In-game settings disables lobby animation')
    device.tap_control('BtnSaveClose', current)
    first = state('motion-off')
    time.sleep(2)
    second = state('motion-off-still')
    verify(not second['motion'] and abs(first['swordAngle']) < .001 and abs(second['swordAngle']) < .001
           and not second['sceneArt']['cloudActive'] and second['sceneArt']['cloudIntensity'] == 0,
           'Low-spec holds neutral poses and completely disables cloud lightning')
    restart(device)
    current = state('persisted')
    verify(current['lowSpec'] and not current['motion'] and not current['sceneArt']['cloudActive'],
           'Low-spec preference persists after app restart')
    current = device.command(prefix + '-settings-on', 'settings')
    device.tap_control('Row_LowSpec', current)
    current = state('settings-enabled')
    device.tap_control('BtnSaveClose', current)
    first = state('motion-on')
    time.sleep(1.3)
    second = state('motion-on-moving')
    verify(second['motion'] and second['sceneArt']['cloudActive'] and abs(first['swordAngle'] - second['swordAngle']) > .01,
           'Settings restores sword animation and cloud effect')
    for _ in range(10):
        device.tap_control('BtnLogin', second)
        device.run('shell', 'input', 'keyevent', '4')
    current = state('popup-cycles')
    verify(not current['popup'] and current['scene'] == 'title', 'Ten sign-in open/back cycles remain usable')
    for _ in range(10):
        device.tap_control('Language', current)
        device.run('shell', 'input', 'keyevent', '4')
    final = state('toggle-cycles')
    verify(final['language'] == current['language'] and final['motion'] == current['motion'] and not final['popup'],
           'Repeated language taps do not leak to background input')
    current = device.command(prefix + '-settings', 'settings')
    device.screenshot(prefix + '-settings')
    verify(current['lowSpecToggle'] == current['lowSpec'], 'In-game settings reflects current low-spec preference')
    previous = current['lowSpec']
    device.tap_control('Row_LowSpec', current)
    current = state('settings-toggle')
    verify(current['lowSpec'] != previous and current['motion'] == (not current['lowSpec']) and current['targetFps'] == 60,
           'Settings low-spec toggle updates title immediately and preserves 60 FPS input cadence')
    device.tap_control('BtnSaveClose', current)
    current = state('settings-close')
    verify(current['lowSpecToggle'] is None, 'Settings Done closes cleanly')
    device.command(prefix + '-restore-motion', 'motion', 'on')
    device.command(prefix + '-restore-language', 'language', 'ko')
    (OUTPUT / f'{prefix}-checks.json').write_text(json.dumps(results, indent=2), encoding='utf-8')


def resolutions(device, prefix):
    original = json.loads((OUTPUT / 'device-before.json').read_text('utf-8'))
    def restore_arg(text):
        return text.split('Override ')[1].split(': ')[1].strip() if 'Override ' in text else 'reset'
    results = []
    try:
        for name, size, density in [('small', '720x1280', '320'), ('native', '1440x3088', '560'),
                                    ('tablet', '1536x2048', '320'), ('fold', '1440x1600', '420')]:
            device.run('shell', 'wm', 'size', size)
            device.run('shell', 'wm', 'density', density)
            restart(device)
            time.sleep(1)
            current = device.command(prefix + '-' + name, 'state')
            device.screenshot(prefix + '-' + name)
            failures = check_layout(current)
            device.tap_control('BtnLogin', current)
            opened = device.command(prefix + '-' + name + '-popup', 'state')
            device.run('shell', 'input', 'keyevent', '4')
            closed = device.command(prefix + '-' + name + '-back', 'state')
            device.tap_control('Language', closed)
            language = device.command(prefix + '-' + name + '-language', 'state')
            device.screenshot(prefix + '-' + name + '-language')
            device.run('shell', 'input', 'keyevent', '4')
            settings = device.command(prefix + '-' + name + '-settings', 'settings')
            device.screenshot(prefix + '-' + name + '-settings')
            device.run('shell', 'input', 'keyevent', '4')
            item = dict(case=name, requestedSize=size, requestedDensity=density,
                        actual=current['screen'], controlsOutsideSafeArea=failures,
                        loginTapAndBack=opened['popup'] and not closed['popup'],
                        languagePopupFits=not check_layout(language), settingsFit=not check_layout(settings),
                        effectsButtonAbsent=not current['sceneArt']['effectsButtonExists'],
                        languageAtTopLeft=next(r for r in current['controls'] if r['name'] == 'Language')['y'] < current['screen']['height']*.2)
            results.append(item)
            print(json.dumps(item, ensure_ascii=False), flush=True)
    finally:
        device.run('shell', 'wm', 'size', restore_arg(original['size']))
        device.run('shell', 'wm', 'density', restore_arg(original['density']))
        restart(device)
        (OUTPUT / f'{prefix}-resolution-checks.json').write_text(json.dumps(results, indent=2), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--serial', required=True)
    parser.add_argument('phase', choices=['interactions', 'resolutions'])
    parser.add_argument('prefix')
    args = parser.parse_args()
    globals()[args.phase](Device(args.serial), args.prefix)
