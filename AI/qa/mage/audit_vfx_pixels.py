"""Read authored Unity sprite metadata and prefab scale; never modify source assets."""
import csv
import json
import re
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Recordings/FoundationRevision/Research'


def unity_documents(path):
    raw = path.read_text(encoding='utf8')
    raw = re.sub(r'^%.*\n', '', raw, flags=re.M)
    ids = re.findall(r'^--- !u!\d+ &(-?\d+)', raw, re.M)
    raw = re.sub(r'^--- !u!\d+ &-?\d+', '---', raw, flags=re.M)
    return dict(zip(ids, yaml.safe_load_all(raw)))


def run():
    guid_paths = {}
    for folder in ['Assets/_Project/Art', 'Assets/UGUI/Art']:
        for meta in (ROOT / folder).rglob('*.png.meta'):
            raw = meta.read_text(encoding='utf8')
            guid = re.search(r'^guid: (\w+)', raw, re.M)
            if guid:
                guid_paths[guid[1]] = meta
    rows = []
    for prefab in (ROOT / 'Assets/_Project/Prefabs/VFX/MageTower').glob('*.prefab'):
        docs = unity_documents(prefab)
        transforms = {str(d['Transform']['m_GameObject']['fileID']): d['Transform'] for d in docs.values() if 'Transform' in d}
        for d in docs.values():
            renderer = d.get('SpriteRenderer')
            if renderer is None or not renderer['m_Sprite'].get('guid'):
                continue
            reference = renderer['m_Sprite']
            meta = guid_paths.get(reference['guid'])
            if meta is None:
                raise ValueError(reference)
            importer = yaml.safe_load(meta.read_text(encoding='utf8'))['TextureImporter']
            transform = transforms[str(renderer['m_GameObject']['fileID'])]
            scale = dict(transform['m_LocalScale'])
            parent = str(transform['m_Father']['fileID'])
            while parent != '0':
                ancestor = docs[parent]['Transform']
                for axis in scale:
                    scale[axis] *= ancestor['m_LocalScale'][axis]
                parent = str(ancestor['m_Father']['fileID'])
            ppu = importer['spritePixelsToUnits']
            sprites = importer['spriteSheet']['sprites'] or []
            sprite = next((s for s in sprites if s.get('internalID') == reference['fileID']), None)
            rows.append(dict(skill=prefab.stem, texture=str(meta.relative_to(ROOT))[:-5],
                frame=None if sprite is None else sprite['rect'], ppu=ppu,
                worldPixelsX=round(ppu / abs(scale['x']), 2), worldPixelsY=round(ppu / abs(scale['y']), 2),
                alpha=renderer['m_Color']['a'], layer=renderer['m_SortingLayerID'], order=renderer['m_SortingOrder'],
                filter=importer['textureSettings']['filterMode']))
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'vfx-pixel-audit.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf8')
    with (OUT / 'vfx-pixel-audit.csv').open('w', encoding='utf8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=rows[0].keys())
        writer.writeheader(); writer.writerows(rows)
    for row in rows:
        print(row['skill'], row['worldPixelsX'], row['worldPixelsY'], row['alpha'], Path(row['texture']).name)


if __name__ == '__main__':
    run()
