"""Deterministic quest-card concept, not a Unity asset or runtime change."""
from pathlib import Path
import hashlib
import json
import PIL
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
FONT = ROOT / 'Assets/ExternalAssets/Fonts/Galmuri11.ttf'
COIN = ROOT / 'Assets/ExternalAssets/Layer Lab/GUI Pro-MinimalGame/Shared/Icons/UniqueIcon/128/Economy_Coin_02_Bronze.png'
THEME = ROOT / 'Assets/UGUI/Scripts/Core/UguiTheme.cs'
SPEC = json.loads((HERE / 'spec.json').read_text(encoding='utf-8'))
COLORS = SPEC['colors']


def font(size=11):
    return ImageFont.truetype(str(FONT), size)


def label(draw, xy, value, color, size=11, center=False, stroke=False):
    f = font(size)
    box = draw.textbbox((0, 0), value, font=f)
    x, y = xy
    if center:
        x -= (box[2] - box[0]) // 2
    # Galmuri is drawn at its native 11 px grid, with binary glyph coverage.
    draw.fontmode = '1'
    draw.text((x - box[0], y - box[1]), value, font=f, fill=color,
              stroke_width=1 if stroke else 0,
              stroke_fill=COLORS['empty'] if stroke else None)


def render_card(width=407, complete=False):
    h = 72
    card = Image.new('RGBA', (width, h), COLORS['card'])
    d = ImageDraw.Draw(card)
    d.rectangle((0, 0, width - 1, h - 1), outline=COLORS['bronze'])
    # The coin slot is the old circle's new purpose. Quantity stays on the slot.
    d.ellipse((12, 12, 59, 59), fill=COLORS['deep'], outline=COLORS['bronze'])
    coin = Image.open(COIN).convert('RGBA').resize((40, 40), Image.Resampling.LANCZOS)
    card.alpha_composite(coin, (16, 10))
    d.rectangle((20, 47, 51, 61), fill=COLORS['deep'], outline=COLORS['bronze'])
    label(d, (36, 49), '×50', COLORS['text'], center=True)

    label(d, (76, 13), '주간', COLORS['muted'])
    label(d, (105, 13), '·', COLORS['muted'])
    label(d, (117, 13), '몬스터 처치', COLORS['text'])

    # The lower row contains both progress and the action.
    bx, by, bw, bh = width - 83, 32, 71, 30
    tx, ty, tw, th = 76, 38, bx - 12 - 76, 18
    d.rectangle((tx, ty, tx + tw - 1, ty + th - 1),
                fill=COLORS['empty'], outline=COLORS['bronze'])
    inner_w = tw - 4
    numerator, denominator = (3000, 3000) if complete else (1500, 3000)
    filled = round(inner_w * numerator / denominator)
    d.rectangle((tx + 2, ty + 2, tx + 1 + filled, ty + th - 3),
                fill=COLORS['green'])
    label(d, (tx + tw // 2, ty + 4), f'{numerator:,} / {denominator:,}',
          COLORS['text'], center=True, stroke=True)

    d.rectangle((bx, by, bx + bw - 1, by + bh - 1),
                fill=COLORS['confirm'] if complete else COLORS['surface'],
                outline=COLORS['bronze_light'] if complete else COLORS['bronze'])
    if complete:
        ink = COLORS['deep']
        d.line([(bx + 15, by + 14), (bx + 18, by + 17), (bx + 24, by + 10)], fill=ink, width=2)
        label(d, (bx + 33, by + 10), '받기', ink)
    else:
        label(d, (bx + 16, by + 10), '이동', COLORS['text'])
        d.line([(bx + 48, by + 11), (bx + 52, by + 15), (bx + 48, by + 19)],
               fill=COLORS['text'], width=1)
    return card, {'canvas': [width, h], 'track': [tx, ty, tw, th],
                  'fill_pixels': filled, 'available_fill_pixels': inner_w,
                  'progress': numerator / denominator,
                  'button_visual_bounds': [bx, by, bw, bh]}


def save(image, name):
    path = HERE / name
    image.save(path, optimize=True)
    return {'path': name, 'size': list(image.size), 'mode': image.mode,
            'bytes': path.stat().st_size, 'rgba_memory_bytes': image.width * image.height * 4,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    outputs, checks = [], []
    states = [('in-progress', False), ('claim-ready', True)]
    board = Image.new('RGB', (479, 310), COLORS['board'])
    bd = ImageDraw.Draw(board)
    label(bd, (36, 23), '퀘스트 카드', COLORS['text'], size=22)
    label(bd, (36, 57), '보상 · 목표 · 진행도 · 행동', COLORS['muted'])

    for index, (name, complete) in enumerate(states):
        card, metrics = render_card(407, complete)
        checks.append({'state': name, **metrics})
        outputs.append(save(card, f'{name}-407.png'))
        outputs.append(save(card.resize((1221, 216), Image.Resampling.NEAREST), f'{name}-3x.png'))
        top = 109 + index * 113
        label(bd, (36, top - 20), '02  보상 수령 가능' if complete else '01  진행 중',
              COLORS['bronze_light'] if complete else COLORS['muted'])
        board.paste(card, (36, top), card)

    outputs.append(save(board.resize((1437, 930), Image.Resampling.NEAREST), 'quest-card-preview.png'))

    # Re-layout at narrow widths instead of shrinking all text or the action.
    contact = Image.new('RGB', (479, 350), COLORS['board'])
    cd = ImageDraw.Draw(contact)
    y = 14
    for width in (407, 360, 320):
        label(cd, (36, y), f'{width} px', COLORS['muted'])
        card, metrics = render_card(width, False)
        contact.paste(card, (36, y + 20), card)
        checks.append({'state': 'in-progress', **metrics})
        y += 112
    outputs.append(save(contact, 'readability-1x.png'))

    for check in checks:
        assert abs(check['fill_pixels'] / check['available_fill_pixels'] - check['progress']) < .005
        assert check['track'][2] >= 140
    manifest = {
        'version': '20260920-v1', 'purpose': 'Quest UI visual concept only',
        'method': 'Existing currency sprite + deterministic flat UI in Pillow; native pixel font, 3x nearest-neighbor presentation',
        'tool_versions': {'Pillow': PIL.__version__},
        'source_assets': [
            {'path': str(COIN.relative_to(ROOT)).replace('\\', '/'), 'resolution': [128, 128],
             'role': 'AncientCoin reward, confirmed via UIViewCatalog.asset'},
            {'path': str(FONT.relative_to(ROOT)).replace('\\', '/'), 'role': 'Existing Galmuri11 font'},
            {'path': str(THEME.relative_to(ROOT)).replace('\\', '/'), 'role': 'Color tokens'}
        ],
        'spec': 'spec.json', 'editable_source': 'render.py',
        'comfyui': {
            'mcp_catalog_tools_found': [], 'run': False,
            'reason': 'No novel illustration needed. Existing icon reuse and code-drawn flat panels follow AGENTS.md, preserve exact Korean and ratios, and allow deterministic inexpensive revisions.',
            'api_json': None, 'ui_graph_json': None, 'job_id': None,
            'prompt': None, 'model': None, 'seed': None
        },
        'cost': {'external_api_calls': 0, 'external_api_spend': 0,
                 'currency': 'USD', 'basis': 'Local rendering only; no paid request submitted, no usage report applicable'},
        'outputs': outputs, 'layout_checks': checks,
        'validation': {'rendered_widths': [320, 360, 407],
                       'progress': '50% and 100% fills numerically checked',
                       'unity_runtime_tested': False, 'android_tested': False,
                       'limitation': 'Static concept only; mobile touch hit areas, safe areas and runtime rendering cost require implementation validation.'},
        'runtime_cost_note': 'No texture or script imported into Unity. Reuse catalog icon and existing font; render the panel, track and button with flat UI graphics rather than importing the presentation board.'
    }
    (HERE / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'outputs': outputs, 'layout_checks': checks}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
