"""Verify separate real-time play rounds and summarize their raw Android evidence."""
import argparse
import datetime as dt
import json
from pathlib import Path


def summarize(directory):
    timing = json.loads((directory / 'Timing/session.json').read_text(encoding='utf8'))
    samples = [json.loads(line) for line in (directory / 'Timing/samples.jsonl').read_text(encoding='utf8').splitlines()]
    states = [sample['state'] for sample in samples if sample.get('valid')]
    assert timing['complete'] and timing['eligibleSeconds'] >= 1800, directory
    elapsed = (dt.datetime.fromisoformat(timing['lastUtc']) - dt.datetime.fromisoformat(timing['startedUtc'])).total_seconds()
    assert elapsed >= timing['eligibleSeconds']
    errors = sorted(set(str(s['lastError']) for s in states if s.get('lastError')))
    gaps = [s for s in samples if 'error' in s]
    return {
        'round': directory.name,
        **timing,
        'wallSeconds': elapsed,
        'validSamples': len(states),
        'readFailuresExcluded': len(gaps),
        'stageIdsObserved': sorted(set(s['stage'] for s in states)),
        'runStatesObserved': sorted(set(s['runState'] for s in states)),
        'runtimeErrors': errors,
        'first': {k: states[0].get(k) for k in ('Kills', 'AccountLevel', 'AttackLevel', 'HealthLevel', 'ReincarnationLevel', 'equipment', 'pending', 'reserve')},
        'last': {k: states[-1].get(k) for k in ('Kills', 'AccountLevel', 'AttackLevel', 'HealthLevel', 'ReincarnationLevel', 'equipment', 'pending', 'reserve')},
        'rawEvidence': str(directory / 'Timing/samples.jsonl'),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    reports = [summarize(args.root / f'Run{i}') for i in range(1, 4)]
    for previous, current in zip(reports, reports[1:]):
        assert dt.datetime.fromisoformat(previous['lastUtc']) <= dt.datetime.fromisoformat(current['startedUtc']), 'Rounds overlap'
    report = {'passed': all(not r['runtimeErrors'] for r in reports), 'rounds': reports,
              'totalEligibleSeconds': sum(r['eligibleSeconds'] for r in reports),
              'deviceCount': 1, 'deviceModel': 'SM-N986N',
              'scope': 'Runs 1–2: earned progression. Run 3: declared isolated late-game fixtures. Foreground, timeScale 1 only.'}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
