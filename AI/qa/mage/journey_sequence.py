"""Execute a short explicit plan using fresh UI bounds and actual Android touches."""
import json
import sys
from journey_actions import m, observe, record

tag = sys.argv[1]
sys.stdin.reconfigure(encoding='utf-8-sig')
steps = json.loads(sys.stdin.read() if sys.argv[2] == '-' else sys.argv[2])
assert len(steps) <= 12
for i, step in enumerate(steps):
    mark = f'{tag}-{i}'
    hud = m.state(mark + '-before')
    record('sequence-touch', {'tag': mark, 'step': step})
    if 'tap' in step:
        m.tap(step['tap'], hud)
    elif 'label' in step:
        labels = [x for x in hud['labels'] if x['text'] == step['label']]
        assert len(labels) == 1, (step, len(labels))
        target = dict(name='label', interactable=True, bounds=labels[0]['bounds'])
        m.tap('label', dict(hud, controls=[target]))
    elif 'xy' in step:
        m.run('shell', 'input', 'tap', *map(str, step['xy']))
        m.time.sleep(1)
    elif 'back' in step:
        m.back()
    else:
        raise ValueError(step)
    m.shot(mark)
observe(tag)
